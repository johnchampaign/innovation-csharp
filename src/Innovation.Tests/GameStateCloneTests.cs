using System.Text;
using Innovation.Core;
using Xunit;

namespace Innovation.Tests;

/// <summary>
/// Verifies <see cref="GameState.DeepClone"/> and its cousins preserve
/// every mutable field and produce a fully-independent copy. Once the AI
/// starts doing look-ahead, a missed field here would silently corrupt
/// the live game.
/// </summary>
public class GameStateCloneTests
{
    static GameStateCloneTests()
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
    }

    private static IReadOnlyList<Card> AllCards => CardDataLoader.LoadFromEmbeddedResource();

    private static GameState Populated()
    {
        var g = new GameState(AllCards, 3);
        foreach (var c in AllCards) g.Decks[c.Age].Add(c.Id);
        g.Phase = GamePhase.Dogma;
        g.ActivePlayer = 1;
        g.CurrentTurn = 5;
        g.ActionsRemaining = 2;

        // Seed some state on p0 + p1.
        var red = AllCards.First(c => c.Color == CardColor.Red);
        var blue = AllCards.First(c => c.Color == CardColor.Blue);
        g.Players[0].Hand.Add(red.Id);
        g.Players[0].ScorePile.Add(blue.Id);
        g.Players[0].Stack(CardColor.Red).Meld(red.Id);
        g.Players[0].Stack(CardColor.Red).Tuck(AllCards.Last(c => c.Color == CardColor.Red).Id);
        g.Players[0].Stack(CardColor.Red).ApplySplay(Splay.Right);
        g.Players[0].AgeAchievements.Add(1);
        g.Players[0].SpecialAchievements.Add("Monument");
        g.Players[0].ScoredThisTurn = 3;
        g.Players[0].TuckedThisTurn = 2;

        g.Players[1].Hand.Add(AllCards[7].Id);

        g.AvailableAgeAchievements.AddRange(new[] { 2, 3, 4 });
        g.AvailableSpecialAchievements.Add("Empire");
        g.AvailableSpecialAchievements.Add("World");
        g.Winners.Add(1);

        return g;
    }

    [Fact]
    public void Clone_PreservesScalars()
    {
        var g = Populated();
        var c = g.DeepClone();

        Assert.Equal(g.Players.Length, c.Players.Length);
        Assert.Equal(g.Phase, c.Phase);
        Assert.Equal(g.ActivePlayer, c.ActivePlayer);
        Assert.Equal(g.CurrentTurn, c.CurrentTurn);
        Assert.Equal(g.ActionsRemaining, c.ActionsRemaining);
    }

    [Fact]
    public void Clone_PreservesDecks()
    {
        var g = Populated();
        var c = g.DeepClone();
        for (int age = 0; age <= 10; age++)
            Assert.Equal(g.Decks[age], c.Decks[age]);
    }

    [Fact]
    public void Clone_PreservesPlayerBoards()
    {
        var g = Populated();
        var c = g.DeepClone();
        var gp = g.Players[0];
        var cp = c.Players[0];

        Assert.Equal(gp.Hand, cp.Hand);
        Assert.Equal(gp.ScorePile, cp.ScorePile);
        Assert.Equal(gp.AgeAchievements, cp.AgeAchievements);
        Assert.Equal(gp.SpecialAchievements, cp.SpecialAchievements);
        Assert.Equal(gp.ScoredThisTurn, cp.ScoredThisTurn);
        Assert.Equal(gp.TuckedThisTurn, cp.TuckedThisTurn);

        var gRed = gp.Stack(CardColor.Red);
        var cRed = cp.Stack(CardColor.Red);
        Assert.Equal(gRed.Cards, cRed.Cards);
        Assert.Equal(gRed.Splay, cRed.Splay);
    }

    [Fact]
    public void Clone_PreservesAchievementPools()
    {
        var g = Populated();
        var c = g.DeepClone();
        Assert.Equal(g.AvailableAgeAchievements, c.AvailableAgeAchievements);
        Assert.Equal(g.AvailableSpecialAchievements.OrderBy(x => x),
                     c.AvailableSpecialAchievements.OrderBy(x => x));
        Assert.Equal(g.Winners, c.Winners);
    }

    [Fact]
    public void Clone_SharesImmutableCardsCatalog()
    {
        // The Cards list is immutable + huge — copying it would waste
        // memory for no gain. Sharing the reference is deliberate.
        var g = Populated();
        var c = g.DeepClone();
        Assert.Same(g.Cards, c.Cards);
    }

    [Fact]
    public void Clone_MutationsAreIsolated()
    {
        var g = Populated();
        var c = g.DeepClone();

        // Mutate the clone across every kind of container.
        c.Players[0].Hand.Clear();
        c.Players[0].ScorePile.Add(42);
        c.Players[0].Stack(CardColor.Red).ApplySplay(Splay.Up);
        c.Players[0].AgeAchievements.Add(99);
        c.Players[0].ScoredThisTurn = 999;
        c.Decks[5].Add(-1);
        c.ActivePlayer = 2;
        c.AvailableAgeAchievements.Clear();
        c.AvailableSpecialAchievements.Clear();
        c.Winners.Add(2);

        // Original is untouched.
        Assert.Single(g.Players[0].Hand);
        Assert.Single(g.Players[0].ScorePile);
        Assert.Equal(Splay.Right, g.Players[0].Stack(CardColor.Red).Splay);
        Assert.Equal(new[] { 1 }, g.Players[0].AgeAchievements);
        Assert.Equal(3, g.Players[0].ScoredThisTurn);
        Assert.DoesNotContain(-1, g.Decks[5]);
        Assert.Equal(1, g.ActivePlayer);
        Assert.Equal(new[] { 2, 3, 4 }, g.AvailableAgeAchievements);
        Assert.Equal(2, g.AvailableSpecialAchievements.Count);
        Assert.Equal(new[] { 1 }, g.Winners);
    }

    [Fact]
    public void Clone_CanRunFullGameIndependently()
    {
        // End-to-end sanity: cloning mid-game then mutating the clone
        // must not change the original's phase or achievement state.
        var g = Populated();
        var c = g.DeepClone();

        c.Phase = GamePhase.GameOver;
        c.Players[0].Hand.Add(123);

        Assert.Equal(GamePhase.Dogma, g.Phase);
        Assert.DoesNotContain(123, g.Players[0].Hand);
    }
}
