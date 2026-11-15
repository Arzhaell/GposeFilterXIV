using System.Linq;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Colors;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Interface.Windowing;

namespace GposeFilterXIV;

/// <summary>La fenêtre : activer, choisir le filtre de GPose et son intensité, voir ce que fait le plugin.</summary>
internal sealed class MainWindow : Window
{
    // Largeur du contenu (texte d'état replié à cette largeur) et des listes, à l'échelle de l'interface.
    private const float ContentWidth = 320;
    private const float FieldWidth = 220;

    private readonly Plugin plugin;

    public MainWindow(Plugin plugin)
        : base("GposeFilterXIV###GposeFilterXIVMain", ImGuiWindowFlags.AlwaysAutoResize)
    {
        this.plugin = plugin;
    }

    private Configuration Config => plugin.Configuration;

    public override void Draw()
    {
        float scale = ImGuiHelpers.GlobalScale;

        bool enabled = Config.Enabled;
        if (ImGui.Checkbox(Loc.T("Enabled", "Activé") + "###enabled", ref enabled))
        {
            Config.Enabled = enabled;
            Config.Save();
        }

        ImGui.Spacing();
        DrawFilterList(FieldWidth * scale);
        DrawIntensity(FieldWidth * scale);

        var (kind, text) = FilterStatus.For(
            plugin.PauseReason != null,
            Config.Enabled,
            Config.FilterId,
            plugin.Catalog.TryGet(Config.FilterId, out _),
            plugin.State,
            plugin.Owner);
        if (kind != StatusKind.None)
        {
            ImGui.Spacing();
            using var wrap = ImRaii.TextWrapPos(ImGui.GetCursorPosX() + ContentWidth * scale);
            ImGui.TextColored(Color(kind), text);
        }

        ImGui.Spacing();
        ImGui.Separator();
        ImGui.Spacing();
        DrawLanguage(FieldWidth * scale);
    }

    private void DrawFilterList(float width)
    {
        var entries = plugin.Catalog.Entries;
        string current = plugin.Catalog.TryGet(Config.FilterId, out var selected) ? selected.Name : "—";

        ImGui.SetNextItemWidth(width);
        using var combo = ImRaii.Combo(Loc.T("Filter", "Filtre") + "###filter", current);
        if (!combo.Success)
            return;

        foreach (var entry in entries)
        {
            bool isSelected = entry.Id == Config.FilterId;
            if (ImGui.Selectable($"{entry.Name}###filter{entry.Id}", isSelected))
            {
                Config.FilterId = entry.Id;
                Config.Save();
            }

            if (isSelected)
                ImGui.SetItemDefaultFocus();
        }
    }

    private void DrawIntensity(float width)
    {
        int intensity = Config.Intensity;
        ImGui.SetNextItemWidth(width);
        if (ImGui.SliderInt(Loc.T("Intensity", "Intensité") + "###intensity", ref intensity, 0, 100, "%d %%", ImGuiSliderFlags.AlwaysClamp))
            Config.Intensity = intensity; // appliqué tout de suite, enregistré au lâcher du curseur

        if (ImGui.IsItemDeactivatedAfterEdit())
            Config.Save();
    }

    private void DrawLanguage(float width)
    {
        var languages = new[]
        {
            (PluginLanguage.Auto, Loc.T("Automatic (game)", "Automatique (jeu)")),
            (PluginLanguage.English, "English"),
            (PluginLanguage.French, "Français"),
        };
        string current = languages.First(l => l.Item1 == Config.Language).Item2;

        ImGui.SetNextItemWidth(width);
        using var combo = ImRaii.Combo(Loc.T("Language", "Langue") + "###language", current);
        if (!combo.Success)
            return;

        foreach (var (language, name) in languages)
        {
            if (ImGui.Selectable(name, language == Config.Language))
                plugin.SetLanguage(language);
        }
    }

    private static Vector4 Color(StatusKind kind) => kind switch
    {
        StatusKind.Ok => ImGuiColors.HealerGreen,
        StatusKind.Warning => ImGuiColors.DalamudYellow,
        StatusKind.Error => ImGuiColors.DalamudRed,
        _ => ImGuiColors.DalamudGrey,
    };
}
