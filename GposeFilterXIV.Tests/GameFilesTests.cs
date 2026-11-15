using Lumina.Data;

namespace GposeFilterXIV.Tests;

/// <summary>
/// Sur le jeu installé (lecture seule) : à relancer après chaque mise à jour du jeu pour savoir si le plugin
/// marche encore, sans lancer le jeu.
/// </summary>
public class GameFilesTests
{
    private static FilterCatalog? Catalog(Language language) =>
        GameFiles.Data?.Excel.GetSheet<ColorFilterSheetRow>(language) is { } sheet
            ? new FilterCatalog(sheet.Select(r => r.ToFilterRow()))
            : null;

    [Fact]
    public void SheetHasTheColumnsWeRead()
    {
        if (GameFiles.Data is not { } data)
            return;

        var sheet = data.Excel.GetSheet<ColorFilterSheetRow>(Language.English);

        Assert.True(ColorFilterSheet.LayoutMatches(sheet.Columns));
    }

    [Fact]
    public void ListStartsWithStandardInGposeOrder()
    {
        if (Catalog(Language.French) is not { } catalog)
            return;

        // 25 filtres au patch du 3 novembre 2026, « Standard » (ligne 0) en premier.
        Assert.True(catalog.Entries.Count >= 25, $"{catalog.Entries.Count} filters");
        Assert.Equal(FilterCatalog.StandardId, catalog.Entries[0].Id);
        Assert.Equal("Standard", catalog.Entries[0].Name);
        Assert.Equal(catalog.Entries.OrderBy(e => e.SortOrder).Select(e => e.Id), catalog.Entries.Select(e => e.Id));
        Assert.All(catalog.Entries, e => Assert.False(string.IsNullOrWhiteSpace(e.Name)));
    }

    [Fact]
    public void NamesFollowTheGameLanguage()
    {
        if (Catalog(Language.French) is not { } french || Catalog(Language.English) is not { } english)
            return;

        Assert.True(french.TryGet(1, out var echoFr));
        Assert.True(english.TryGet(1, out var echoEn));
        Assert.Equal("Vision du passé", echoFr.Name);
        Assert.Equal("Echo", echoEn.Name);
        Assert.Equal(english.Entries.Select(e => e.Id), french.Entries.Select(e => e.Id));
    }

    [Fact]
    public void RowsReadThroughLuminaMatchTheGame()
    {
        if (Catalog(Language.English) is not { } catalog)
            return;

        // Valeurs lues par l'outil de désassemblage dans la même version du jeu.
        Assert.True(catalog.TryGet(Rows.ColoredPencil.Id, out var pencil));
        Assert.Equal(Rows.ColoredPencil, pencil);
        Assert.True(catalog.TryGet(Rows.StrengthenedEffects.Id, out var strengthened));
        Assert.Equal(Rows.StrengthenedEffects, strengthened);
        Assert.True(catalog.TryGet(Rows.Pastel1.Id, out var pastel));
        Assert.Equal(Rows.Pastel1, pastel);
        Assert.False(catalog.TryGet(Rows.Unlisted.Id, out _));
    }

    [Fact]
    public void GameCodeIsTheOneWeKnow()
    {
        if (GameFiles.Exe is not { } exe)
            return;

        var sites = exe.FindAll(GameCode.SetColorFilterSignature);
        Assert.True(sites.Count == 1, $"SetColorFilter signature found {sites.Count} times in game {GameFiles.Version}");

        var addresses = GameCode.Resolve(sites[0], exe.Read, GameFilterSlot.OverrideOffset, GameFilterSlot.EnvManagerSize, out var error);

        Assert.True(addresses != null, $"Game {GameFiles.Version}: {error}");
        Assert.Single(exe.FindAll(GameCode.ReserveSignature), addresses.ReserveFunction);
    }
}
