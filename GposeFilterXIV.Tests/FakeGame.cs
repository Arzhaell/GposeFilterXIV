using FFXIVClientStructs.FFXIV.Client.Graphics.Environment;

namespace GposeFilterXIV.Tests;

/// <summary>
/// Un faux emplacement de filtre qui se comporte comme le jeu (lu dans son code, version 2026.11.03) :
/// la fonction de réservation, et ce que GPose fait quand on choisit un filtre ou qu'on le quitte.
/// </summary>
internal sealed class FakeGame : IFilterSlot
{
    public const byte GposeOwner = 0x40;

    /// <summary>L'octet propriétaire (EnvManager + 0x45B).</summary>
    public byte OwnerByte { get; set; }

    /// <summary>EnvManager.ColorFilterOverride.</summary>
    public EnvColorFilterParameters Override { get; set; } = FilterValues.Identity;

    /// <summary>Nombre d'écritures du plugin dans ColorFilterOverride.</summary>
    public int PluginWrites { get; private set; }

    /// <summary>Nombre d'appels à PostEffectManager.SetEffectEnabled(15, false) faits par la libération.</summary>
    public int EffectDisabled { get; private set; }

    /// <summary>Le filtre de zone (EnvState.ColorFilter) est dessiné : le jeu le saute dès que l'octet n'est pas 0.</summary>
    public bool ZoneFilterShown => OwnerByte == 0;

    byte IFilterSlot.Owner => OwnerByte;

    void IFilterSlot.Reserve(bool enable) => Reserve(enable, FilterSlot.OurOwner);

    void IFilterSlot.Write(in EnvColorFilterParameters values)
    {
        Override = values;
        PluginWrites++;
    }

    /// <summary>La fonction de réservation 0x140066E50.</summary>
    public void Reserve(bool enable, byte owner)
    {
        if (enable)
        {
            if (OwnerByte < owner)
                OwnerByte = owner;
        }
        else if (OwnerByte == owner)
        {
            OwnerByte = 0;
            EffectDisabled++;
        }
    }

    /// <summary>
    /// GPose : SetColorFilter (0x1417DC190) réserve avec 0x40, puis la copie (0x1417CA810) écrit les valeurs
    /// si l'emplacement est bien à GPose. Le filtre 0 (« Standard ») revient à <see cref="GposeClear"/>.
    /// </summary>
    public void GposeSelect(FilterRow row, int intensityPercent)
    {
        if (row.Id == FilterCatalog.StandardId)
        {
            GposeClear();
            return;
        }

        Reserve(true, GposeOwner);
        if (OwnerByte == GposeOwner)
            Override = FilterValues.For(row, intensityPercent);
    }

    /// <summary>ClearColorFilter (0x1417DC290) : remise à zéro et libération, seulement si l'emplacement est à GPose.</summary>
    public void GposeClear()
    {
        if (OwnerByte == GposeOwner)
            Override = FilterValues.Identity;
        Reserve(false, GposeOwner);
    }
}

/// <summary>Des lignes de la feuille ColorFilter (valeurs du jeu, version 2026.11.03).</summary>
internal static class Rows
{
    public static readonly FilterRow Standard = new(0, "Standard", 1, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 1, false);

    // Ligne 13 « Colored Pencil » : luminosité/contraste linéaires.
    public static readonly FilterRow ColoredPencil = new(13, "Colored Pencil", 30, 0, 0, 0.1f, -0.3f, -0.1f, 0.2f, 0, 0, 0, 0, -0.2f, 0.4f, 0.6f, false);

    // Ligne 34 « Strengthened Effects » : luminosité/contraste non linéaires.
    public static readonly FilterRow StrengthenedEffects = new(34, "Strengthened Effects", 70, 0, 0, -0.2f, -0.2f, 0, 0, 0, 0, 0, 0, 0, 0.25f, 0.5f, true);

    // Ligne 15 « Pastel 1 » : teinte.
    public static readonly FilterRow Pastel1 = new(15, "Pastel 1", 50, 0, 0, 0.1f, -0.3f, 0, 0.2f, -0.1f, 0.6f, 0.6f, 0.6f, -0.2f, 0.1f, 0.4f, false);

    // Ligne 2 : sans nom ni rang, absente de la liste de GPose.
    public static readonly FilterRow Unlisted = new(2, "", 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, false);

    public static FilterCatalog Catalog() => new([Standard, ColoredPencil, StrengthenedEffects, Pastel1, Unlisted]);
}
