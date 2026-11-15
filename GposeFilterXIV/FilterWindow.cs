using System;
using System.Linq;
using System.Numerics;
using FFXIVClientStructs.FFXIV.Component.GUI;
using KamiToolKit.BaseTypes;
using KamiToolKit.Extensions;
using KamiToolKit.Nodes;

namespace GposeFilterXIV;

/// <summary>
/// La fenêtre, construite avec les éléments d'interface du jeu (KamiToolKit) : activer, choisir le filtre de GPose et son
/// intensité, voir ce que fait le plugin, choisir la langue. À ouvrir et fermer sur le fil du jeu.
/// </summary>
internal sealed class FilterWindow : NativeAddon
{
    public static readonly Vector2 DefaultSize = new(400.0f, 270.0f);

    // Hauteur d'une ligne, écart entre deux lignes, largeur de la colonne des libellés.
    private const float RowHeight = 24.0f;
    private const float RowGap = 10.0f;
    private const float LabelWidth = 110.0f;
    private const float StatusHeight = 40.0f;

    // Le curseur enregistre la configuration une fois immobile depuis ce délai (pas à chaque cran pendant qu'on le tire).
    private static readonly TimeSpan IntensitySaveDelay = TimeSpan.FromSeconds(1);

    private static readonly Vector4 OkColor = new(0.55f, 0.95f, 0.55f, 1.0f);
    private static readonly Vector4 InfoColor = new(0.85f, 0.85f, 0.85f, 1.0f);
    private static readonly Vector4 WarningColor = new(1.0f, 0.85f, 0.4f, 1.0f);
    private static readonly Vector4 ErrorColor = new(1.0f, 0.45f, 0.45f, 1.0f);

    private CheckboxNode? enabledNode;
    private TextNode? filterLabel;
    private DropDownNode<FilterRow>? filterNode;
    private TextNode? intensityLabel;
    private SliderNode? intensityNode;
    private TextNode? statusNode;
    private TextNode? languageLabel;
    private DropDownNode<PluginLanguage>? languageNode;

    private (StatusKind Kind, string Text) shownStatus = (StatusKind.None, string.Empty);
    private DateTime? intensitySaveDue;

    public required Plugin Plugin { get; init; }

    private Configuration Config => Plugin.Configuration;

    /// <summary>Remet les textes dans la langue choisie (titre, libellés, langues).</summary>
    public void RefreshTexts()
    {
        // Fenêtre fermée : ses nœuds sont détruits par le jeu (KamiToolKit garde pourtant le cadre, qu'on ne doit plus
        // toucher). La langue ne se change que depuis la fenêtre ouverte.
        if (!IsOpen || enabledNode == null)
            return;

        Title = WindowTitle;
        enabledNode.String = Loc.T("Enabled", "Activé");
        filterLabel!.String = Loc.T("Filter", "Filtre");
        intensityLabel!.String = Loc.T("Intensity (%)", "Intensité (%)");
        languageLabel!.String = Loc.T("Language", "Langue");
        languageNode!.Options = [.. Enum.GetValues<PluginLanguage>()];
        languageNode.SelectedOption = Config.Language;
        shownStatus = (StatusKind.None, string.Empty);
        UpdateStatus();
    }

    public static string WindowTitle => Loc.T("GPose filter", "Filtre de GPose");

    protected override unsafe void OnSetup(AtkUnitBase* addon, Span<AtkValue> atkValueSpan)
    {
        base.OnSetup(addon, atkValueSpan);

        float y = 0.0f;
        float fieldWidth = ContentSize.X - LabelWidth;

        enabledNode = new CheckboxNode
        {
            Position = ContentStartPosition,
            Size = new Vector2(ContentSize.X, RowHeight),
            String = Loc.T("Enabled", "Activé"),
            IsChecked = Config.Enabled,
            OnClick = isChecked =>
            {
                Config.Enabled = isChecked;
                Config.Save();
                UpdateStatus();
            },
        };
        enabledNode.AttachNode(this);

        y += RowHeight + RowGap;
        filterLabel = Label(Loc.T("Filter", "Filtre"), y);
        filterNode = new DropDownNode<FilterRow>
        {
            Position = ContentStartPosition + new Vector2(LabelWidth, y),
            Size = new Vector2(fieldWidth, RowHeight),
            MaxListOptions = 10,
            GetLabelFunction = row => row.Name,
            OnOptionSelected = row =>
            {
                Config.FilterId = row.Id;
                Config.Save();
                UpdateStatus();
            },
        };
        filterNode.AttachNode(this);
        filterNode.Options = [.. Plugin.Catalog.Entries];
        if (Plugin.Catalog.TryGet(Config.FilterId, out var selected))
            filterNode.SelectedOption = selected;

        y += RowHeight + RowGap;
        intensityLabel = Label(Loc.T("Intensity (%)", "Intensité (%)"), y);
        intensityNode = new SliderNode
        {
            Position = ContentStartPosition + new Vector2(LabelWidth, y + 2.0f),
            Size = new Vector2(fieldWidth, 20.0f),
            Range = 0..100, // KamiToolKit : fin comprise (Min, Max du curseur)
            Step = 1,
        };
        intensityNode.Value = Math.Clamp(Config.Intensity, 0, 100);
        intensityNode.OnValueChanged = value =>
        {
            Config.Intensity = Math.Clamp(value, 0, 100); // appliqué tout de suite par le plugin
            intensitySaveDue = DateTime.UtcNow + IntensitySaveDelay;
        };
        intensityNode.AttachNode(this);

        y += RowHeight + RowGap;
        statusNode = new TextNode
        {
            Position = ContentStartPosition + new Vector2(0.0f, y),
            Size = new Vector2(ContentSize.X, StatusHeight),
            FontSize = 12,
            LineSpacing = 14,
            AlignmentType = AlignmentType.TopLeft,
            TextFlags = TextFlags.WordWrap | TextFlags.MultiLine,
        };
        statusNode.AttachNode(this);

        y += StatusHeight + RowGap / 2.0f;
        var line = new HorizontalLineNode
        {
            Position = ContentStartPosition + new Vector2(0.0f, y),
            Size = new Vector2(ContentSize.X, 4.0f),
        };
        line.AttachNode(this);

        y += 4.0f + RowGap;
        languageLabel = Label(Loc.T("Language", "Langue"), y);
        languageNode = new DropDownNode<PluginLanguage>
        {
            Position = ContentStartPosition + new Vector2(LabelWidth, y),
            Size = new Vector2(fieldWidth, RowHeight),
            MaxListOptions = 3,
            GetLabelFunction = language => LanguageName(language),
            OnOptionSelected = language => Plugin.SetLanguage(language),
        };
        languageNode.AttachNode(this);
        languageNode.Options = [.. Enum.GetValues<PluginLanguage>()];
        languageNode.SelectedOption = Config.Language;

        shownStatus = (StatusKind.None, string.Empty);
        UpdateStatus();
    }

    protected override unsafe void OnUpdate(AtkUnitBase* addon)
    {
        base.OnUpdate(addon);
        UpdateStatus();

        if (intensitySaveDue is { } due && DateTime.UtcNow >= due)
            SaveIntensity();
    }

    protected override unsafe void OnHide(AtkUnitBase* addon)
    {
        SaveIntensity();
        base.OnHide(addon);
    }

    protected override unsafe void OnFinalize(AtkUnitBase* addon)
    {
        SaveIntensity();

        // Les nœuds sont détruits avec la fenêtre : on les oublie jusqu'à la prochaine ouverture.
        enabledNode = null;
        filterLabel = null;
        filterNode = null;
        intensityLabel = null;
        intensityNode = null;
        statusNode = null;
        languageLabel = null;
        languageNode = null;
        base.OnFinalize(addon);
    }

    private static string LanguageName(PluginLanguage language) => language switch
    {
        PluginLanguage.English => "English",
        PluginLanguage.French => "Français",
        _ => Loc.T("Automatic (game)", "Automatique (jeu)"),
    };

    private static Vector4 Color(StatusKind kind) => kind switch
    {
        StatusKind.Ok => OkColor,
        StatusKind.Warning => WarningColor,
        StatusKind.Error => ErrorColor,
        _ => InfoColor,
    };

    private TextNode Label(string text, float y)
    {
        var label = new TextNode
        {
            Position = ContentStartPosition + new Vector2(0.0f, y),
            Size = new Vector2(LabelWidth - 8.0f, RowHeight),
            FontSize = 14,
            AlignmentType = AlignmentType.Left,
            String = text,
        };
        label.AttachNode(this);
        return label;
    }

    private void SaveIntensity()
    {
        if (intensitySaveDue == null)
            return;
        intensitySaveDue = null;
        Config.Save();
    }

    // La ligne d'état ne change de texte que si elle a changé (pas de réécriture à chaque image).
    private void UpdateStatus()
    {
        if (statusNode == null)
            return;

        var status = FilterStatus.For(
            Plugin.PauseReason != null,
            Config.Enabled,
            Config.FilterId,
            Plugin.Catalog.TryGet(Config.FilterId, out _),
            Plugin.State,
            Plugin.Owner);
        if (status == shownStatus)
            return;

        shownStatus = status;
        statusNode.String = status.Text;
        statusNode.TextColor = Color(status.Kind);
    }
}
