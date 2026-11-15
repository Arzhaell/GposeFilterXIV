using System;
using System.Linq;
using Dalamud.IoC;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;

namespace GposeFilterXIV;

public sealed class Plugin : IDalamudPlugin
{
    [PluginService] internal static IDalamudPluginInterface PluginInterface { get; private set; } = null!;
    [PluginService] internal static IDataManager DataManager { get; private set; } = null!;
    [PluginService] internal static IFramework Framework { get; private set; } = null!;
    [PluginService] internal static IClientState ClientState { get; private set; } = null!;
    [PluginService] internal static ISigScanner SigScanner { get; private set; } = null!;
    [PluginService] internal static IPluginLog Log { get; private set; } = null!;

    // Null si le code du jeu ou la feuille ColorFilter ne sont pas ceux qu'on connaît : on n'écrit alors rien.
    private readonly GameFilterSlot? slot;

    private SlotState? lastState;
    private byte lastOwner;

    public Plugin()
    {
        Configuration = PluginInterface.GetPluginConfig() as Configuration ?? new Configuration();
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

        Framework.Update += OnFrameworkUpdate;
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

    public void Dispose()
    {
        Framework.Update -= OnFrameworkUpdate;

        // Rendre l'emplacement sur le fil du jeu : le filtre de zone revient.
        if (slot != null)
            Framework.RunOnFrameworkThread(() => FilterSlot.Release(slot.Current())).Wait();
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
            return;

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
}
