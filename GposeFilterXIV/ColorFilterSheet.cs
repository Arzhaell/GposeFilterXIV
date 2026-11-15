using System.Collections.Generic;
using System.Linq;
using Lumina.Data.Structs.Excel;
using Lumina.Excel;
using Lumina.Text.ReadOnly;

namespace GposeFilterXIV;

/// <summary>
/// Une ligne de la feuille ColorFilter (les filtres de GPose), lue telle quelle.
/// Le Lumina.Excel de Dalamud n'a pas cette feuille : colonnes d'après le schéma EXDSchema, vérifiées dans le jeu
/// (version 2026.11.03) et contrôlées au démarrage par <see cref="ColorFilterSheet.LayoutMatches"/>.
/// </summary>
[Sheet("ColorFilter")]
public readonly struct ColorFilterSheetRow(ExcelPage page, uint offset, uint row) : IExcelRow<ColorFilterSheetRow>
{
    public ExcelPage ExcelPage => page;
    public uint RowOffset => offset;
    public uint RowId => row;

    public ReadOnlySeString Name => page.ReadString(offset, offset);
    public float Hue => page.ReadFloat32(offset + 4);
    public float Saturation => page.ReadFloat32(offset + 8);
    public float Brightness => page.ReadFloat32(offset + 12);
    public float Contrast => page.ReadFloat32(offset + 16);
    public float Monochrome => page.ReadFloat32(offset + 20);
    public float Sepia => page.ReadFloat32(offset + 24);
    public float TintStrength => page.ReadFloat32(offset + 28);
    public float TintR => page.ReadFloat32(offset + 32);
    public float TintG => page.ReadFloat32(offset + 36);
    public float TintB => page.ReadFloat32(offset + 40);
    public float Invert => page.ReadFloat32(offset + 44);
    public float ToneCurveLow => page.ReadFloat32(offset + 48);
    public float ToneCurveHigh => page.ReadFloat32(offset + 52);
    public byte SortOrder => page.ReadUInt8(offset + 56);
    public bool UseNonlinearBrightnessContrast => page.ReadPackedBool(offset + 57, 0);

    static ColorFilterSheetRow IExcelRow<ColorFilterSheetRow>.Create(ExcelPage page, uint offset, uint row) =>
        new(page, offset, row);
}

/// <summary>Une ligne de la feuille ColorFilter, sans Lumina (pour les calculs et les tests).</summary>
internal sealed record FilterRow(
    uint Id,
    string Name,
    byte SortOrder,
    float Hue,
    float Saturation,
    float Brightness,
    float Contrast,
    float Monochrome,
    float Sepia,
    float TintStrength,
    float TintR,
    float TintG,
    float TintB,
    float Invert,
    float ToneCurveLow,
    float ToneCurveHigh,
    bool UseNonlinearBrightnessContrast);

internal static class ColorFilterSheet
{
    /// <summary>
    /// Les colonnes que <see cref="ColorFilterSheetRow"/> lit, dans l'ordre de la feuille (type, position dans la ligne).
    /// Si une mise à jour du jeu les change, on ne lit plus rien plutôt que d'afficher de faux filtres.
    /// </summary>
    internal static readonly (ExcelColumnDataType Type, ushort Offset)[] ExpectedColumns =
    [
        (ExcelColumnDataType.String, 0),        // Name
        (ExcelColumnDataType.UInt8, 56),        // SortOrder
        (ExcelColumnDataType.Float32, 4),       // Hue
        (ExcelColumnDataType.Float32, 8),       // Saturation
        (ExcelColumnDataType.PackedBool0, 57),  // UseNonlinearBrightnessContrast
        (ExcelColumnDataType.Float32, 12),      // Brightness
        (ExcelColumnDataType.Float32, 16),      // Contrast
        (ExcelColumnDataType.Float32, 20),      // Monochrome
        (ExcelColumnDataType.Float32, 24),      // Sepia
        (ExcelColumnDataType.Float32, 28),      // TintStrength
        (ExcelColumnDataType.Float32, 32),      // TintR
        (ExcelColumnDataType.Float32, 36),      // TintG
        (ExcelColumnDataType.Float32, 40),      // TintB
        (ExcelColumnDataType.Float32, 44),      // Invert
        (ExcelColumnDataType.Float32, 48),      // ToneCurveLow
        (ExcelColumnDataType.Float32, 52),      // ToneCurveHigh
    ];

    /// <summary>Les colonnes de la feuille sont celles que l'on sait lire.</summary>
    internal static bool LayoutMatches(IReadOnlyList<ExcelColumnDefinition> columns) =>
        columns.Count == ExpectedColumns.Length
        && columns.Select(c => (c.Type, c.Offset)).SequenceEqual(ExpectedColumns);

    internal static FilterRow ToFilterRow(this ColorFilterSheetRow row) => new(
        row.RowId,
        row.Name.ExtractText(),
        row.SortOrder,
        row.Hue,
        row.Saturation,
        row.Brightness,
        row.Contrast,
        row.Monochrome,
        row.Sepia,
        row.TintStrength,
        row.TintR,
        row.TintG,
        row.TintB,
        row.Invert,
        row.ToneCurveLow,
        row.ToneCurveHigh,
        row.UseNonlinearBrightnessContrast);
}
