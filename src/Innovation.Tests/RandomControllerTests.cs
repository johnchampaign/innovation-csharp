using System.Text;
using Innovation.Core;
using Innovation.Core.Players;
using Xunit;

namespace Innovation.Tests;

/// <summary>
/// Unit-level checks for <see cref="RandomController"/>. Focus is on the
/// invariants handlers rely on — answers are in-bounds, subset counts
/// match constraints, same seed produces same sequence.
/// </summary>
public class RandomControllerTests
{
    static RandomControllerTests()
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
    }

    private static IReadOnlyList<Card> AllCards => CardDataLoader.LoadFromEmbeddedResource();

    private static GameState Fresh()
    {
        var g = new GameState(AllCards, 2);
        foreach (var c in AllCards) g.Decks[c.Age].Add(c.Id);
        g.Phase = GamePhase.Action;
        return g;
    }

    [Fact]
    public void SameSeed_ProducesSameInitialMeldSequence()
    {
        var g = Fresh();
        foreach (var c in AllCards.Take(5)) g.Players[0].Hand.Add(c.Id);

        var a = new RandomController(seed: 42);
        var b = new RandomController(seed: 42);
        for (int i = 0; i < 20; i++)
            Assert.Equal(a.ChooseInitialMeld(g, g.Players[0]),
                         b.ChooseInitialMeld(g, g.Players[0]));
    }

    [Fact]
    public void ChooseAction_ReturnsSomethingFromLegalList()
    {
        var g = Fresh();
        var rc = new RandomController(seed: 1);
        var legal = LegalActions.Enumerate(g, g.Players[0]);
        for (int i = 0; i < 20; i++)
        {
            var picked = rc.ChooseAction(g, g.Players[0], legal);
            Assert.Contains(picked, legal);
        }
    }

    [Fact]
    public void ChooseHandCard_Required_NeverReturnsNull_AlwaysEligible()
    {
        var g = Fresh();
        var req = new SelectHandCardRequest
        {
            EligibleCardIds = new[] { 1, 2, 3 },
            AllowNone = false,
        };
        var rc = new RandomController(seed: 1);
        for (int i = 0; i < 50; i++)
        {
            int? picked = rc.ChooseHandCard(g, g.Players[0], req);
            Assert.NotNull(picked);
            Assert.Contains(picked.Value, req.EligibleCardIds);
        }
    }

    [Fact]
    public void ChooseHandCard_Optional_ReturnsBothBranchesOverTime()
    {
        var g = Fresh();
        var req = new SelectHandCardRequest
        {
            EligibleCardIds = new[] { 1, 2, 3 },
            AllowNone = true,
        };
        var rc = new RandomController(seed: 1);
        int nulls = 0, picks = 0;
        for (int i = 0; i < 200; i++)
        {
            int? picked = rc.ChooseHandCard(g, g.Players[0], req);
            if (picked is null) nulls++; else picks++;
        }
        // A seeded 50/50 over 200 trials won't be pathological.
        Assert.InRange(nulls, 50, 150);
        Assert.InRange(picks, 50, 150);
    }

    [Fact]
    public void ChooseHandCardSubset_RespectsMinMax_NoDuplicates()
    {
        var g = Fresh();
        var req = new SelectHandCardSubsetRequest
        {
            EligibleCardIds = new[] { 10, 20, 30, 40, 50 },
            MinCount = 2,
            MaxCount = 3,
        };
        var rc = new RandomController(seed: 1);
        for (int i = 0; i < 50; i++)
        {
            var picked = rc.ChooseHandCardSubset(g, g.Players[0], req);
            Assert.InRange(picked.Count, 2, 3);
            Assert.All(picked, id => Assert.Contains(id, req.EligibleCardIds));
            Assert.Equal(picked.Count, picked.Distinct().Count());
        }
    }

    [Fact]
    public void ChooseHandCardSubset_MoreThanEligible_Clamps()
    {
        // Min=5 but only 3 eligible — shouldn't crash, should return all 3.
        var g = Fresh();
        var req = new SelectHandCardSubsetRequest
        {
            EligibleCardIds = new[] { 10, 20, 30 },
            MinCount = 5,
            MaxCount = 10,
        };
        var rc = new RandomController(seed: 1);
        var picked = rc.ChooseHandCardSubset(g, g.Players[0], req);
        Assert.Equal(3, picked.Count);
    }

    [Fact]
    public void ChooseColor_ReturnsEligibleColor()
    {
        var g = Fresh();
        var req = new SelectColorRequest
        {
            EligibleColors = new[] { CardColor.Red, CardColor.Blue },
        };
        var rc = new RandomController(seed: 1);
        for (int i = 0; i < 20; i++)
        {
            var picked = rc.ChooseColor(g, g.Players[0], req);
            Assert.NotNull(picked);
            Assert.Contains(picked.Value, req.EligibleColors);
        }
    }
}
