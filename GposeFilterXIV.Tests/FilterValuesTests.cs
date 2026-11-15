using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using FFXIVClientStructs.FFXIV.Client.Graphics.Environment;

namespace GposeFilterXIV.Tests;

/// <summary>
/// Ligne de la feuille → valeurs écrites, comparées octet par octet à ce que fait la copie du jeu (0x1417CA810) :
/// les positions viennent du code du jeu, pas des noms de ClientStructs.
/// </summary>
public class FilterValuesTests
{
    // Une ligne aux valeurs toutes différentes : une colonne copiée au mauvais endroit se voit tout de suite.
    private static FilterRow Distinct(bool nonlinear) =>
        new(99, "Test", 1, Hue: 1, Saturation: 2, Brightness: 3, Contrast: 4, Monochrome: 5, Sepia: 6, TintStrength: 7,
            TintR: 8, TintG: 9, TintB: 10, Invert: 11, ToneCurveLow: 12, ToneCurveHigh: 13, UseNonlinearBrightnessContrast: nonlinear);

    /// <summary>Les 23 flottants de la structure (0x5C octets), par position.</summary>
    private static float[] Floats(EnvColorFilterParameters values)
    {
        var bytes = MemoryMarshal.CreateReadOnlySpan(ref Unsafe.As<EnvColorFilterParameters, byte>(ref values), Unsafe.SizeOf<EnvColorFilterParameters>());
        return MemoryMarshal.Cast<byte, float>(bytes).ToArray();
    }

    private static float At(float[] floats, int offset) => floats[offset / 4];

    [Fact]
    public void StructureIsTheSizeTheGameUses()
    {
        // 0x3D0 → 0x42C dans EnvManager ; la remise à zéro du jeu écrit jusqu'à 0x58 + 4.
        Assert.Equal(0x5C, Unsafe.SizeOf<EnvColorFilterParameters>());
        Assert.Equal(0x3D0, GameFilterSlot.OverrideOffset);
    }

    [Fact]
    public void LinearRowIsCopiedLikeTheGame()
    {
        var f = Floats(FilterValues.For(Distinct(nonlinear: false), 100));

        Assert.Equal(12f, At(f, 0x00)); // Curve.X ← ToneCurveLow (ligne + 0x30)
        Assert.Equal(13f, At(f, 0x04)); // Curve.Y ← ToneCurveHigh (ligne + 0x34)
        Assert.Equal(0f, At(f, 0x08));  // Curve.Z ← 0
        Assert.Equal(0f, At(f, 0x0C));  // Curve.W ← 0
        Assert.Equal(1f, At(f, 0x10));  // Hue ← ligne + 4
        Assert.Equal(2f, At(f, 0x14));  // Saturation ← ligne + 8
        Assert.Equal(3f, At(f, 0x18));  // Brightness ← ligne + 0xC
        Assert.Equal(4f, At(f, 0x1C));  // Contrast ← ligne + 0x10
        Assert.Equal(8f, At(f, 0x20));  // TintColor.R ← ligne + 0x20
        Assert.Equal(9f, At(f, 0x24));  // TintColor.G ← ligne + 0x24
        Assert.Equal(10f, At(f, 0x28)); // TintColor.B ← ligne + 0x28
        Assert.Equal(7f, At(f, 0x2C));  // TintStrength ← ligne + 0x1C
        Assert.Equal(6f, At(f, 0x30));  // Sepia ← ligne + 0x18
        Assert.Equal(5f, At(f, 0x34));  // Monochrome ← ligne + 0x14
        Assert.Equal(11f, At(f, 0x38)); // Invert ← ligne + 0x2C
        Assert.Equal(1f, At(f, 0x58));  // Strength ← curseur / 100
    }

    [Fact]
    public void NonlinearRowMovesBrightnessAndContrastIntoTheCurve()
    {
        var f = Floats(FilterValues.For(Distinct(nonlinear: true), 100));

        Assert.Equal(12f, At(f, 0x00)); // Curve.X ← ToneCurveLow
        Assert.Equal(13f, At(f, 0x04)); // Curve.Y ← ToneCurveHigh
        Assert.Equal(6f, At(f, 0x08));  // Curve.Z ← 2 × Brightness (addss xmm0, xmm0)
        Assert.Equal(4f, At(f, 0x0C));  // Curve.W ← Contrast
        Assert.Equal(0f, At(f, 0x18));  // Brightness ← 0
        Assert.Equal(0f, At(f, 0x1C));  // Contrast ← 0
        Assert.Equal(1f, At(f, 0x10));  // le reste ne change pas
        Assert.Equal(11f, At(f, 0x38));
    }

    [Fact]
    public void DarkParametersStayAtZero()
    {
        // GPose ne touche pas DarkSaturation, DarkTintColor, DarkThreshold, DarkRange, DarkTintStrength (0x3C à 0x54) :
        // ils restent à 0 après la remise à zéro du jeu.
        var f = Floats(FilterValues.For(Distinct(nonlinear: false), 100));
        for (int offset = 0x3C; offset <= 0x54; offset += 4)
            Assert.Equal(0f, At(f, offset));
    }

    [Fact]
    public void RealRowsGiveTheGameValues()
    {
        var pencil = FilterValues.For(Rows.ColoredPencil, 100);
        Assert.Equal(0.4f, pencil.Curve.X);
        Assert.Equal(0.6f, pencil.Curve.Y);
        Assert.Equal(0.1f, pencil.Brightness);
        Assert.Equal(-0.3f, pencil.Contrast);
        Assert.Equal(-0.1f, pencil.Monochrome);
        Assert.Equal(-0.2f, pencil.Invert);

        var strengthened = FilterValues.For(Rows.StrengthenedEffects, 100);
        Assert.Equal(0.25f, strengthened.Curve.X);
        Assert.Equal(0.5f, strengthened.Curve.Y);
        Assert.Equal(-0.4f, strengthened.Curve.Z);
        Assert.Equal(-0.2f, strengthened.Curve.W);
        Assert.Equal(0f, strengthened.Brightness);
        Assert.Equal(0f, strengthened.Contrast);

        var pastel = FilterValues.For(Rows.Pastel1, 100);
        Assert.Equal(new System.Numerics.Vector3(0.6f, 0.6f, 0.6f), pastel.TintColor);
        Assert.Equal(-0.1f, pastel.TintStrength);
        Assert.Equal(0.2f, pastel.Sepia);
    }

    [Theory]
    [InlineData(100, 1f)]
    [InlineData(50, 0.5f)]
    [InlineData(0, 0f)]
    [InlineData(-5, 0f)]
    [InlineData(250, 1f)]
    public void StrengthIsTheSliderOverAHundred(int percent, float expected)
    {
        Assert.Equal(expected, FilterValues.Strength(percent));
        Assert.Equal(expected, FilterValues.For(Rows.ColoredPencil, percent).Strength);
    }

    [Fact]
    public void StrengthIsComputedLikeGpose()
    {
        // GPose : cvtsi2ss puis divss par 100f. Même arrondi en flottant simple pour chaque position du curseur.
        for (int percent = 0; percent <= 100; percent++)
            Assert.Equal(BitConverter.SingleToInt32Bits((float)percent / 100f), BitConverter.SingleToInt32Bits(FilterValues.Strength(percent)));
    }

    [Fact]
    public void IdentityIsTheGameReset()
    {
        // 0x140066950 : tout à 0, sauf [+4] = 1 (Curve.Y) et [+0x58] = 1 (Strength).
        var f = Floats(FilterValues.Identity);
        for (int offset = 0; offset < 0x5C; offset += 4)
            Assert.Equal(offset is 0x04 or 0x58 ? 1f : 0f, At(f, offset));
    }

    [Fact]
    public void NothingWantedWhenDisabledLoggedOutStandardOrUnknown()
    {
        var catalog = Rows.Catalog();

        Assert.Null(FilterValues.Wanted(enabled: false, loggedIn: true, 13, 100, catalog));
        Assert.Null(FilterValues.Wanted(enabled: true, loggedIn: false, 13, 100, catalog));
        Assert.Null(FilterValues.Wanted(enabled: true, loggedIn: true, FilterCatalog.StandardId, 100, catalog));
        Assert.Null(FilterValues.Wanted(enabled: true, loggedIn: true, Rows.Unlisted.Id, 100, catalog));
        Assert.Null(FilterValues.Wanted(enabled: true, loggedIn: true, 4242, 100, catalog));
    }

    [Fact]
    public void WantedGivesTheRowValuesAtTheChosenIntensity()
    {
        var wanted = FilterValues.Wanted(enabled: true, loggedIn: true, 34, 40, Rows.Catalog());

        Assert.NotNull(wanted);
        Assert.Equal(-0.4f, wanted.Value.Curve.Z);
        Assert.Equal(0.4f, wanted.Value.Strength);
    }
}
