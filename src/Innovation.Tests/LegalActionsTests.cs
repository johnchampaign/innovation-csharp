using System.Text;
using Innovation.Core;
using Xunit;

namespace Innovation.Tests;

/// <summary>
/// Exercises <see cref="LegalActions.Enumerate"/>. Covers each of the four
/// action buckets (Draw / Meld / Dogma / Achieve) plus the age-10 edge.
/// </summary>
public class LegalActionsTests
{
    static LegalActionsTests()
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
    }

    private static IReadOnlyList<Card> AllCards => CardDataLoader.LoadFromEmbeddedResource();

    private static GameState Fresh(int players = 2)
    {
        var g = new GameState(AllCards, players);
        foreach (var c in AllCards) g.Decks[c.Age].Add(c.Id);
        g.Phase = GamePhase.Action;
        return g;
    }

    [Fact]
    public void Enumerate_EmptyBoard_NoHand_OnlyDraw()
    {
        var g = Fresh();
        var acts = LegalActions.Enumerate(g, g.Players[0]);
        Assert.Single(acts);
        Assert.IsType<DrawAction>(acts[0]);
    }

    [Fact]
    public void Enumerate_MeldPerHandCard()
    {
        var g = Fresh();
        g.Players[0].Hand.Add(g.Cards[0].Id);
        g.Players[0].Hand.Add(g.Cards[1].Id);

        var acts = LegalActions.Enumerate(g, g.Players[0]);
        var melds = acts.OfType<MeldAction>().ToList();

        Assert.Equal(2, melds.Count);
        Assert.Contains(melds, m => m.CardId == g.Cards[0].Id);
        Assert.Contains(melds, m => m.CardId == g.Cards[1].Id);
    }

    [Fact]
    public void Enumerate_DogmaPerNonEmptyPile()
    {
        var g = Fresh();
        var red = g.Cards.First(c => c.Color == CardColor.Red);
        var blue = g.Cards.First(c => c.Color == CardColor.Blue);
        g.Players[0].Stack(CardColor.Red).Meld(red.Id);
        g.Players[0].Stack(CardColor.Blue).Meld(blue.Id);

        var acts = LegalActions.Enumerate(g, g.Players[0]);
        var dogmas = acts.OfType<DogmaAction>().Select(d => d.Color).ToHashSet();

        Assert.Equal(2, dogmas.Count);
        Assert.Contains(CardColor.Red, dogmas);
        Assert.Contains(CardColor.Blue, dogmas);
        Assert.DoesNotContain(CardColor.Green, dogmas);
    }

    [Fact]
    public void Enumerate_NoAchievementsWhenIneligible()
    {
        // AvailableAgeAchievements is empty out of Fresh(), so none
        // qualify regardless of score / top-card.
        var g = Fresh();
        var acts = LegalActions.Enumerate(g, g.Players[0]);
        Assert.DoesNotContain(acts, a => a is AchieveAction);
    }

    [Fact]
    public void Enumerate_AchievementWhenEligible()
    {
        var g = Fresh();
        // Score ≥ 5 (five age-1s) + a top card at age 1 → eligible for age-1.
        var age1s = g.Cards.Where(c => c.Age == 1).Take(6).ToList();
        g.Players[0].ScorePile.AddRange(age1s.Take(5).Select(c => c.Id));
        var topper = age1s[5];
        g.Players[0].Stack(topper.Color).Meld(topper.Id);
        g.AvailableAgeAchievements.Clear();
        g.AvailableAgeAchievements.Add(1);

        var acts = LegalActions.Enumerate(g, g.Players[0]);
        var achieves = acts.OfType<AchieveAction>().ToList();

        Assert.Single(achieves);
        Assert.Equal(1, achieves[0].Age);
    }

    [Fact]
    public void Enumerate_NeverIncludesAge10Achievement()
    {
        var g = Fresh();
        // Big enough score (≥45) + age-10 top card satisfies CanClaim for
        // every age 1–9. If LegalActions ever added age-10 to its loop,
        // this test would flag it. Ten age-5s gives 50 score — clears the
        // age-9 threshold (45) with headroom.
        var ten = g.Cards.First(c => c.Age == 10);
        var fives = g.Cards.Where(c => c.Age == 5).Take(10).ToList();
        g.Players[0].ScorePile.AddRange(fives.Select(c => c.Id));
        g.Players[0].Stack(ten.Color).Meld(ten.Id);
        for (int a = 1; a <= 9; a++) g.AvailableAgeAchievements.Add(a);

        var acts = LegalActions.Enumerate(g, g.Players[0]);
        Assert.DoesNotContain(acts, a => a is AchieveAction aa && aa.Age == 10);
        Assert.Equal(9, acts.OfType<AchieveAction>().Count());
    }

    [Fact]
    public void Enumerate_OrderIsAchievesThenDrawThenDogmasThenMelds()
    {
        // Lock in the ordering from the header doc — tests rely on it for
        // predictable index math.
        var g = Fresh();
        g.Players[0].Hand.Add(g.Cards[0].Id);
        var blueSeed = g.Cards.First(c => c.Color == CardColor.Blue);
        g.Players[0].Stack(CardColor.Blue).Meld(blueSeed.Id);

        var acts = LegalActions.Enumerate(g, g.Players[0]);
        // No achieves eligible. First should be Draw, then Dogma(Blue), then Meld.
        Assert.IsType<DrawAction>(acts[0]);
        Assert.IsType<DogmaAction>(acts[1]);
        Assert.IsType<MeldAction>(acts[2]);
    }
}
