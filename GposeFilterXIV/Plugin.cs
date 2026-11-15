using System;
using System.Linq;
using Dalamud.Game;
using Dalamud.Game.Command;
using Dalamud.Interface.Windowing;
using Dalamud.IoC;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;

namespace GposeFilterXIV;

public sealed class Plugin : IDalamudPlugin
{
    private const string CommandName = "/gposefilter";

    [PluginService] internal static IDalamudPluginInterface PluginInterface { get; private set; } = null!;
    [PluginService] internal static IDataManager DataManager { get; private set; } = null!;
    [PluginService] internal static IFramework Framework { get; private set; } = null!;
    [PluginService] internal static IClientState ClientState { get; private set; } = null!;
    [PluginService] internal static ISigScanner SigScanner { get; private set; } = null!;
    [PluginService] internal static ICommandManager CommandManager { get; private set; } = null!;
    [PluginService] internal static IChatGui ChatGui { get; private set; } = null!;
    [PluginService] internal static IPluginLog Log { get; private set; } = null!;

    private readonly WindowSystem windowSystem = new("GposeFilterXIV");
    private readonly MainWindow mainWindow;

    // Null si le code du jeu ou la feuille ColorFilter ne sont pas ceux qu'on connaît : on n'écrit alors rien.
    private readonly GameFilterSlot? slot;

    private SlotState? lastState;
    private byte lastOwner;

    // La pause a été annoncée dans le chat (une fois par chargement du plugin, une fois connecté).
    private bool pauseAnnounced;

    public Plugin()
    {
        Configuration = LoadConfiguration();
        Loc.Update(Configuration.Language, GameIsFrench);
        Catalog = LoadCatalog(out var sheetError);

        var addresses = GameFilterSlot.Locate(SigScanner, out var codeError);
        PauseReason = sheetError ?? codeError;
        if (PauseReason != null || addresses == null)
        {
            Log.Error($"Paused, nothing will be written: {PauseReason}");
        }
        else
        {
            slot = new GameFilterSlot(addresses);
            Log.Information(
                $"{Catalog.Entries.Count} GPose filters. Reservation function 0x{addresses.ReserveFunction:X}, "
                + $"argument EnvManager+0x{addresses.ReserveArgumentOffset:X}, owner byte EnvManager+0x{addresses.OwnerOffset:X}.");
        }

        mainWindow = new MainWindow(this);
        windowSystem.AddWindow(mainWindow);

        // Le filtre se règle aussi en GPose : Dalamud y cache sinon les fenêtres des plugins.
        PluginInterface.UiBuilder.DisableGposeUiHide = true;
        PluginInterface.UiBuilder.Draw += windowSystem.Draw;
        PluginInterface.UiBuilder.OpenMainUi += mainWindow.Toggle;
        PluginInterface.UiBuilder.OpenConfigUi += mainWindow.Toggle;
        Framework.Update += OnFrameworkUpdate;
        RegisterCommand();
    }

    public Configuration Configuration { get; }

    /// <summary>Les filtres de GPose, dans son ordre et dans la langue du jeu.</summary>
    internal FilterCatalog Catalog { get; }

    /// <summary>Pourquoi le plugin ne fait rien (jeu mis à jour sans lui…), ou null s'il marche.</summary>
    internal string? PauseReason { get; }

    /// <summary>Ce qui s'est passé à la dernière image (pour la fenêtre).</summary>
    internal SlotState State { get; private set; } = SlotState.Unavailable;

    /// <summary>Qui tenait l'emplacement à la dernière image.</summary>
    internal byte Owner { get; private set; }

    private static bool GameIsFrench => ClientState.ClientLanguage == ClientLanguage.French;

    public void Dispose()
    {
        Framework.Update -= OnFrameworkUpdate;
        CommandManager.RemoveHandler(CommandName);
        PluginInterface.UiBuilder.Draw -= windowSystem.Draw;
        PluginInterface.UiBuilder.OpenMainUi -= mainWindow.Toggle;
        PluginInterface.UiBuilder.OpenConfigUi -= mainWindow.Toggle;
        windowSystem.RemoveAllWindows();

        // Rendre l'emplacement sur le fil du jeu : le filtre de zone revient.
        if (slot != null)
            Framework.RunOnFrameworkThread(() => FilterSlot.Release(slot.Current())).Wait();
    }

    internal void SetLanguage(PluginLanguage language)
    {
        Configuration.Language = language;
        Configuration.Save();
        Loc.Update(language, GameIsFrench);
        CommandManager.RemoveHandler(CommandName);
        RegisterCommand();
    }

    // Un fichier de configuration illisible ne doit pas empêcher le plugin de se charger : on repart des réglages par défaut.
    private static Configuration LoadConfiguration()
    {
        try
        {
            return PluginInterface.GetPluginConfig() as Configuration ?? new Configuration();
        }
        catch (Exception e)
        {
            Log.Error(e, "Could not read the configuration, falling back to default settings.");
            return new Configuration();
        }
    }

    private static FilterCatalog LoadCatalog(out string? error)
    {
        try
        {
            var sheet = DataManager.GetExcelSheet<ColorFilterSheetRow>();
            if (!ColorFilterSheet.LayoutMatches(sheet.Columns))
            {
                error = "The ColorFilter sheet columns are not the expected ones.";
                return new FilterCatalog([]);
            }

            error = null;
            return new FilterCatalog(sheet.Select(r => r.ToFilterRow()));
        }
        catch (Exception e)
        {
            error = $"Cannot read the ColorFilter sheet: {e.Message}";
            return new FilterCatalog([]);
        }
    }

    private void OnFrameworkUpdate(IFramework framework)
    {
        if (slot == null)
        {
            AnnouncePause();
            return;
        }

        var wanted = FilterValues.Wanted(
            Configuration.Enabled, ClientState.IsLoggedIn, Configuration.FilterId, Configuration.Intensity, Catalog);
        var current = slot.Current();
        State = FilterSlot.Update(current, wanted);
        Owner = current?.Owner ?? 0;

        if (State != lastState || (State == SlotState.Yielded && Owner != lastOwner))
        {
            Log.Information($"Filter slot: {State} (owner 0x{Owner:X2}).");
            lastState = State;
            lastOwner = Owner;
        }
    }

    // En pause, le dire une fois dans le chat, si on attendait un filtre (sinon la fenêtre suffit).
    private void AnnouncePause()
    {
        if (pauseAnnounced || !Configuration.Enabled || !ClientState.IsLoggedIn)
            return;
        pauseAnnounced = true;
        ChatGui.PrintError(FilterStatus.PausedText, "GposeFilterXIV");
    }

    private void OnCommand(string command, string arguments) => mainWindow.Toggle();

    private static string CommandHelp => Loc.T(
        "/gposefilter: choose the GPose filter to keep on outside of GPose.",
        "/gposefilter : choisir le filtre de GPose à garder hors GPose.");

    private void RegisterCommand() =>
        CommandManager.AddHandler(CommandName, new CommandInfo(OnCommand) { HelpMessage = CommandHelp });
}
