using System.Text;
using Innovation.Core;
using Xunit;

namespace Innovation.Tests;

public class MonumentTests
{
    static MonumentTests()
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
    }

    private static IReadOnlyList<Card> AllCards => CardDataLoader.LoadFromEmbeddedResource();

    private static GameState NewGame(int players = 2)
    {
        var g = GameSetup.Create(AllCards, players, new Random(42));
        return g;
    }

    /// <summary>Six distinct card IDs that exist in a fresh game's decks.</summary>
    private static List<int> SixCards(GameState g)
        => Enumerable.Range(0, g.Cards.Count).Take(6).ToList();

    [Fact]
    public void Score_Five_Times_DoesNotTriggerMonument()
    {
        var g = NewGame();
        var p = g.Players[0];
        var five = SixCards(g).Take(5).ToList();
        foreach (var id in five) p.Hand.Add(id);

        foreach (var id in five)
            Mechanics.Score(g, p, id);

        Assert.Equal(5, p.ScoredThisTurn);
        Assert.DoesNotContain("Monument", p.SpecialAchievements);
        Assert.Contains("Monument", g.AvailableSpecialAchievements);
    }

    [Fact]
    public void Score_Six_Times_ClaimsMonument()
    {
        var g = NewGame();
        var p = g.Players[0];
        var six = SixCards(g);
        foreach (var id in six) p.Hand.Add(id);

        foreach (var id in six)
            Mechanics.Score(g, p, id);

        Assert.Equal(6, p.ScoredThisTurn);
        Assert.Contains("Monument", p.SpecialAchievements);
        Assert.DoesNotContain("Monument", g.AvailableSpecialAchievements);
    }

    [Fact]
    public void Tuck_Six_Times_ClaimsMonument()
    {
        var g = NewGame();
        var p = g.Players[0];
        var six = SixCards(g);
        foreach (var id in six) p.Hand.Add(id);

        foreach (var id in six)
            Mechanics.Tuck(g, p, id);

        Assert.Equal(6, p.TuckedThisTurn);
        Assert.Contains("Monument", p.SpecialAchievements);
    }

    [Fact]
    public void MixedScoreAndTuck_DoesNotTriggerMonument()
    {
        // 3 scores + 3 tucks shouldn't claim — Monument is one OR the other.
        var g = NewGame();
        var p = g.Players[0];
        var six = SixCards(g);
        foreach (var id in six) p.Hand.Add(id);

        Mechanics.Score(g, p, six[0]);
        Mechanics.Score(g, p, six[1]);
        Mechanics.Score(g, p, six[2]);
        Mechanics.Tuck(g, p, six[3]);
        Mechanics.Tuck(g, p, six[4]);
        Mechanics.Tuck(g, p, six[5]);

        Assert.DoesNotContain("Monument", p.SpecialAchievements);
    }

    [Fact]
    public void Counters_Reset_AtTurnAdvance()
    {
        var g = NewGame();
        var tm = new TurnManager(g);
        tm.CompleteInitialMeld(g.Players.Select(pl => pl.Hand[0]).ToList());

        var p = g.Active;
        // Simulate 5 scores this turn (not enough to trigger).
        foreach (var id in SixCards(g).Take(5))
        {
            p.Hand.Add(id);
            Mechanics.Score(g, p, id);
        }
        Assert.Equal(5, p.ScoredThisTurn);

        // End the turn by taking a Draw action.
        tm.Apply(new DrawAction());

        // Counters for every player wiped.
        foreach (var pl in g.Players)
        {
            Assert.Equal(0, pl.ScoredThisTurn);
            Assert.Equal(0, pl.TuckedThisTurn);
        }
    }
}
