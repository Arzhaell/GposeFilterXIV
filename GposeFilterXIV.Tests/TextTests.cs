namespace GposeFilterXIV.Tests;

/// <summary>
/// La langue et la ligne d'état de la fenêtre. Une seule classe : Loc est partagé, et xUnit lance les classes en parallèle.
/// </summary>
public class TextTests
{
    private static (StatusKind Kind, string Text) Status(
        bool paused = false, bool enabled = true, uint filterId = 13, bool listed = true,
        SlotState state = SlotState.Holding, byte owner = FilterSlot.OurOwner) =>
        FilterStatus.For(paused, enabled, filterId, listed, state, owner);

    // Les cas de la ligne d'état, chacun avec le genre attendu.
    private static readonly (string Case, Func<(StatusKind, string)> Get, StatusKind Kind)[] Cases =
    [
        ("paused", () => Status(paused: true, enabled: false), StatusKind.Error),
        ("standard", () => Status(filterId: FilterCatalog.StandardId), StatusKind.Info),
        ("unlisted", () => Status(filterId: 4242, listed: false), StatusKind.Warning),
        ("holding", () => Status(), StatusKind.Ok),
        ("gpose", () => Status(state: SlotState.Yielded, owner: FilterSlot.SceneOwner), StatusKind.Info),
        ("other owner", () => Status(state: SlotState.Yielded, owner: 0x80), StatusKind.Info),
        ("loading", () => Status(state: SlotState.Unavailable, owner: 0), StatusKind.Info),
        ("logged out", () => Status(state: SlotState.Idle, owner: 0), StatusKind.Info),
    ];

    [Fact]
    public void AutomaticFollowsTheGameAndForcedIgnoresIt()
    {
        Loc.Update(PluginLanguage.Auto, gameIsFrench: true);
        Assert.True(Loc.IsFrench);
        Loc.Update(PluginLanguage.Auto, gameIsFrench: false);
        Assert.False(Loc.IsFrench);
        Loc.Update(PluginLanguage.French, gameIsFrench: false);
        Assert.True(Loc.IsFrench);
        Loc.Update(PluginLanguage.English, gameIsFrench: true);
        Assert.False(Loc.IsFrench);
    }

    [Fact]
    public void EveryStatusHasItsKindAndBothLanguages()
    {
        foreach (var (name, get, kind) in Cases)
        {
            Loc.Update(PluginLanguage.English, gameIsFrench: false);
            var (englishKind, english) = get();
            Loc.Update(PluginLanguage.French, gameIsFrench: false);
            var (frenchKind, french) = get();

            Assert.True(englishKind == kind && frenchKind == kind, $"{name}: {englishKind} / {frenchKind}, expected {kind}");
            Assert.False(string.IsNullOrWhiteSpace(english), name);
            Assert.False(string.IsNullOrWhiteSpace(french), name);
            Assert.NotEqual(english, french);
        }
    }

    [Fact]
    public void DisabledShowsNothing()
    {
        Assert.Equal(StatusKind.None, Status(enabled: false).Kind);
        Assert.Equal(StatusKind.None, Status(enabled: false, state: SlotState.Yielded, owner: FilterSlot.SceneOwner).Kind);
    }

    [Fact]
    public void GposeAndOtherOwnersAreTold()
    {
        Loc.Update(PluginLanguage.English, gameIsFrench: false);

        Assert.Contains("GPose", Status(state: SlotState.Yielded, owner: FilterSlot.SceneOwner).Text);
        Assert.DoesNotContain("GPose", Status(state: SlotState.Yielded, owner: 0x80).Text);
    }

    [Fact]
    public void PauseWinsOverEverything()
    {
        Loc.Update(PluginLanguage.French, gameIsFrench: true);

        var (kind, text) = Status(paused: true, state: SlotState.Holding);

        Assert.Equal(StatusKind.Error, kind);
        Assert.Equal(FilterStatus.PausedText, text);
        Assert.StartsWith("En pause", text);
    }
}
