using System;
using FFXIVClientStructs.FFXIV.Client.Graphics.Environment;
using CurveVector = FFXIVClientStructs.FFXIV.Common.Math.Vector4;
using TintVector = System.Numerics.Vector3;

namespace GposeFilterXIV;

/// <summary>
/// Les valeurs à écrire dans EnvManager.ColorFilterOverride, calculées comme le jeu le fait pour GPose.
/// </summary>
internal static class FilterValues
{
    /// <summary>
    /// Ce que le jeu écrit pour un filtre de GPose (copie 0x1417CA810, version 2026.11.03) : les colonnes sont copiées
    /// telles quelles, la courbe prend les tons bas et hauts ; avec la luminosité/contraste non linéaire, la luminosité
    /// (doublée) et le contraste passent dans la courbe et les champs linéaires restent à 0.
    /// Les paramètres « Dark » restent à 0, comme après la remise à zéro du jeu (GPose ne les touche pas).
    /// L'intensité est celle du curseur de GPose : entier de 0 à 100, divisé par 100 en flottant simple.
    /// </summary>
    public static EnvColorFilterParameters For(FilterRow row, int intensityPercent)
    {
        var values = Identity;
        bool nonlinear = row.UseNonlinearBrightnessContrast;

        values.Curve = new CurveVector(
            row.ToneCurveLow,
            row.ToneCurveHigh,
            nonlinear ? row.Brightness + row.Brightness : 0f,
            nonlinear ? row.Contrast : 0f);
        values.Hue = row.Hue;
        values.Saturation = row.Saturation;
        values.Brightness = nonlinear ? 0f : row.Brightness;
        values.Contrast = nonlinear ? 0f : row.Contrast;
        values.TintColor = new TintVector(row.TintR, row.TintG, row.TintB);
        values.TintStrength = row.TintStrength;
        values.Sepia = row.Sepia;
        values.Monochrome = row.Monochrome;
        values.Invert = row.Invert;
        values.Strength = Strength(intensityPercent);
        return values;
    }

    /// <summary>
    /// La remise à zéro du jeu (0x140066950) : courbe (0, 1, 0, 0), intensité 1, tout le reste à 0.
    /// </summary>
    public static EnvColorFilterParameters Identity => new()
    {
        Curve = new CurveVector(0f, 1f, 0f, 0f),
        Strength = 1f,
    };

    /// <summary>L'intensité écrite par GPose pour une position du curseur (0x14100BD3B : entier → flottant ÷ 100).</summary>
    public static float Strength(int intensityPercent) => Math.Clamp(intensityPercent, 0, 100) / 100f;

    /// <summary>
    /// Les valeurs à garder dans le jeu, ou null s'il ne faut pas de filtre : plugin désactivé, pas connecté
    /// (écran titre, choix du personnage), « Standard » choisi, ou filtre absent de la liste de GPose.
    /// </summary>
    public static EnvColorFilterParameters? Wanted(bool enabled, bool loggedIn, uint filterId, int intensityPercent, FilterCatalog catalog)
    {
        if (!enabled || !loggedIn || filterId == FilterCatalog.StandardId)
            return null;
        return catalog.TryGet(filterId, out var row) ? For(row, intensityPercent) : null;
    }
}
