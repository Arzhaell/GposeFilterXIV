using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Dalamud.Game;
using Dalamud.Game.Command;
using Dalamud.IoC;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using KamiToolKit;

namespace GposeFilterXIV;

public sealed class Plugin : IAsyncDalamudPlugin
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

    // Null si le code du jeu ou la feuille ColorFilter ne sont pas ceux qu'on connaît : on n'écrit alors rien.
    private GameFilterSlot? slot;

    private FilterWindow? window;

    private SlotState? lastState;
    private byte lastOwner;

    // La pause a été annoncée dans le chat (une fois par chargement du plugin, une fois connecté).
    private bool pauseAnnounced;

    public Configuration Configuration { get; private set; } = new();

    /// <summary>Les filtres de GPose, dans son ordre et dans la langue du jeu.</summary>
    internal FilterCatalog Catalog { get; private set; } = new([]);

    /// <summary>Pourquoi le plugin ne fait rien (jeu mis à jour sans lui…), ou null s'il marche.</summary>
    internal string? PauseReason { get; private set; }

    /// <summary>Ce qui s'est passé à la dernière image (pour la fenêtre).</summary>
    internal SlotState State { get; private set; } = SlotState.Unavailable;

    /// <summary>Qui tenait l'emplacement à la dernière image.</summary>
    internal byte Owner { get; private set; }

    private static bool GameIsFrench => ClientState.ClientLanguage == ClientLanguage.French;

    public async Task LoadAsync(CancellationToken cancellationToken)
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

        await KamiToolKitLibrary.InitializeAsync(PluginInterface, "GposeFilterXIV");
        window = new FilterWindow
        {
            InternalName = "GposeFilterXIV",
            Title = FilterWindow.WindowTitle,
            Size = FilterWindow.DefaultSize,
            Plugin = this,
        };

        PluginInterface.UiBuilder.OpenMainUi += ToggleWindow;
        PluginInterface.UiBuilder.OpenConfigUi += ToggleWindow;
        Framework.Update += OnFrameworkUpdate;
        RegisterCommand();
    }

    public async ValueTask DisposeAsync()
    {
        Framework.Update -= OnFrameworkUpdate;
        CommandManager.RemoveHandler(CommandName);
        PluginInterface.UiBuilder.OpenMainUi -= ToggleWindow;
        PluginInterface.UiBuilder.OpenConfigUi -= ToggleWindow;

        // Rendre l'emplacement sur le fil du jeu : le filtre de zone revient.
        if (slot != null)
            await Framework.RunOnFrameworkThread(() => FilterSlot.Release(slot.Current()));

        // La suite de l'await reprend sur le fil du jeu, or KamiToolKit attend la fermeture de ses fenêtres hors de
        // ce fil (sinon le jeu se bloque) : on passe sur le pool de threads.
        await Task.Run(async () =>
        {
            if (window != null)
                await window.DisposeAsync();
            await KamiToolKitLibrary.DisposeAsync();
        });
    }

    internal void SetLanguage(PluginLanguage language)
    {
        Configuration.Language = language;
        Configuration.Save();
        Loc.Update(language, GameIsFrench);
        CommandManager.RemoveHandler(CommandName);
        RegisterCommand();

        // À l'image suivante : on est peut-être dans le clic de la liste des langues, qu'on va reconstruire.
        Framework.RunOnTick(() => window?.RefreshTexts());
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

    // La fenêtre du jeu s'ouvre et se ferme sur le fil du jeu (la commande y est déjà, le bouton de /xlplugins non).
    private void ToggleWindow() => Framework.RunOnFrameworkThread(() => window?.Toggle());

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

    private void OnCommand(string command, string arguments) => ToggleWindow();

    private static string CommandHelp => Loc.T(
        "/gposefilter: choose the GPose filter to keep on outside of GPose.",
        "/gposefilter : choisir le filtre de GPose à garder hors GPose.");

    private void RegisterCommand() =>
        CommandManager.AddHandler(CommandName, new CommandInfo(OnCommand) { HelpMessage = CommandHelp });
}
