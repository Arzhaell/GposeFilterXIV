using FFXIVClientStructs.FFXIV.Client.Graphics.Environment;

namespace GposeFilterXIV.Tests;

/// <summary>La règle prendre / laisser / rendre, puis des scènes avec un faux jeu qui réserve comme le vrai.</summary>
public class FilterSlotTests
{
    private static readonly EnvColorFilterParameters Pencil = FilterValues.For(Rows.ColoredPencil, 100);

    [Theory]
    [InlineData(0x00, true, SlotAction.Take)]
    [InlineData(0x20, true, SlotAction.Write)]
    [InlineData(0x40, true, SlotAction.None)]  // GPose, cinématique : elle passe devant
    [InlineData(0x80, true, SlotAction.None)]  // module du jeu
    [InlineData(0x10, true, SlotAction.None)]  // plus petit que nous : on ne le vole pas non plus
    [InlineData(0x00, false, SlotAction.None)]
    [InlineData(0x20, false, SlotAction.Release)]
    [InlineData(0x40, false, SlotAction.None)]
    [InlineData(0x80, false, SlotAction.None)]
    public void Rule(byte owner, bool wanted, object expected) => // object : SlotAction est interne au plugin
        Assert.Equal((SlotAction)expected, FilterSlot.Decide(owner, wanted));

    [Fact]
    public void FreeSlotIsTakenAndFilled()
    {
        var game = new FakeGame();

        Assert.Equal(SlotState.Holding, FilterSlot.Update(game, Pencil));

        Assert.Equal(FilterSlot.OurOwner, game.OwnerByte);
        Assert.Equal(Pencil, game.Override);
        Assert.False(game.ZoneFilterShown);
    }

    [Fact]
    public void ValuesAreWrittenAgainEveryFrame()
    {
        var game = new FakeGame();
        FilterSlot.Update(game, Pencil);
        game.Override = FilterValues.Identity; // quelqu'un a remis à zéro

        FilterSlot.Update(game, Pencil);

        Assert.Equal(Pencil, game.Override);
        Assert.Equal(2, game.PluginWrites);
    }

    [Fact]
    public void IntensityChangeIsAppliedNextFrame()
    {
        var game = new FakeGame();
        FilterSlot.Update(game, Pencil);

        FilterSlot.Update(game, FilterValues.For(Rows.ColoredPencil, 30));

        Assert.Equal(0.3f, game.Override.Strength);
    }

    [Fact]
    public void GposeFilterTakesOverThenOursComesBack()
    {
        var game = new FakeGame();
        FilterSlot.Update(game, Pencil);

        // GPose : un filtre est choisi, il passe devant (0x40 > 0x20) et le plugin n'écrit plus rien.
        game.GposeSelect(Rows.Pastel1, 70);
        var gposeValues = game.Override;
        int writes = game.PluginWrites;
        for (int frame = 0; frame < 5; frame++)
            Assert.Equal(SlotState.Yielded, FilterSlot.Update(game, Pencil));
        Assert.Equal(FakeGame.GposeOwner, game.OwnerByte);
        Assert.Equal(gposeValues, game.Override);
        Assert.Equal(writes, game.PluginWrites);

        // Sortie de GPose : le jeu remet à zéro et libère ; notre filtre revient à l'image suivante.
        game.GposeClear();
        Assert.Equal(0, game.OwnerByte);
        Assert.Equal(SlotState.Holding, FilterSlot.Update(game, Pencil));
        Assert.Equal(FilterSlot.OurOwner, game.OwnerByte);
        Assert.Equal(Pencil, game.Override);
    }

    [Fact]
    public void GposeStandardKeepsOurFilter()
    {
        var game = new FakeGame();
        FilterSlot.Update(game, Pencil);

        // « Standard » dans GPose : ClearColorFilter ne touche à rien si l'emplacement n'est pas à GPose.
        game.GposeSelect(Rows.Standard, 100);

        Assert.Equal(FilterSlot.OurOwner, game.OwnerByte);
        Assert.Equal(Pencil, game.Override);
        Assert.Equal(SlotState.Holding, FilterSlot.Update(game, Pencil));
    }

    [Fact]
    public void ForeignOwnerIsNeverTouched()
    {
        foreach (byte owner in new byte[] { 0x10, 0x40, 0x80 })
        {
            var game = new FakeGame { OwnerByte = owner };
            var theirs = FilterValues.For(Rows.Pastel1, 50);
            game.Override = theirs;

            Assert.Equal(SlotState.Yielded, FilterSlot.Update(game, Pencil));
            Assert.Equal(SlotState.Idle, FilterSlot.Update(game, null));
            FilterSlot.Release(game);

            Assert.Equal(owner, game.OwnerByte);
            Assert.Equal(theirs, game.Override);
            Assert.Equal(0, game.PluginWrites);
            Assert.Equal(0, game.EffectDisabled);
        }
    }

    [Fact]
    public void TurningOffResetsAndGivesBackTheZoneFilter()
    {
        var game = new FakeGame();
        FilterSlot.Update(game, Pencil);

        Assert.Equal(SlotState.Idle, FilterSlot.Update(game, null));

        Assert.Equal(0, game.OwnerByte);
        Assert.True(game.ZoneFilterShown);
        Assert.Equal(FilterValues.Identity, game.Override);
        Assert.Equal(1, game.EffectDisabled);

        // Plus rien ensuite.
        int writes = game.PluginWrites;
        FilterSlot.Update(game, null);
        Assert.Equal(writes, game.PluginWrites);
    }

    [Fact]
    public void UnloadReleasesOnlyOurSlot()
    {
        var ours = new FakeGame();
        FilterSlot.Update(ours, Pencil);
        FilterSlot.Release(ours);
        Assert.Equal(0, ours.OwnerByte);
        Assert.Equal(FilterValues.Identity, ours.Override);

        var free = new FakeGame();
        FilterSlot.Release(free);
        Assert.Equal(0, free.PluginWrites);
        Assert.Equal(0, free.EffectDisabled);
    }

    [Fact]
    public void LeftoverOwnerFromACrashedSessionIsReused()
    {
        // Le plugin a été arrêté sans rendre l'emplacement : la nouvelle instance le reprend ou le rend.
        var game = new FakeGame { OwnerByte = FilterSlot.OurOwner };
        Assert.Equal(SlotState.Holding, FilterSlot.Update(game, Pencil));

        var other = new FakeGame { OwnerByte = FilterSlot.OurOwner };
        Assert.Equal(SlotState.Idle, FilterSlot.Update(other, null));
        Assert.Equal(0, other.OwnerByte);
    }

    [Fact]
    public void NoEnvManagerMeansNothingHappens()
    {
        Assert.Equal(SlotState.Unavailable, FilterSlot.Update(null, Pencil));
        Assert.Equal(SlotState.Unavailable, FilterSlot.Update(null, null));
        FilterSlot.Release(null);
    }

    [Fact]
    public void CutsceneDuringOurFilterThenBack()
    {
        // Une scène d'événement réserve avec 0x40 comme GPose : même chemin.
        var game = new FakeGame();
        FilterSlot.Update(game, Pencil);
        game.Reserve(true, FakeGame.GposeOwner);

        Assert.Equal(SlotState.Yielded, FilterSlot.Update(game, Pencil));

        game.Reserve(false, FakeGame.GposeOwner);
        Assert.Equal(SlotState.Holding, FilterSlot.Update(game, Pencil));
    }
}
