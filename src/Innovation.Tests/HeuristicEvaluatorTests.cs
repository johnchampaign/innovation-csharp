using System.Text;
using Innovation.Core;
using Xunit;

namespace Innovation.Tests;

/// <summary>
/// Tests for the VB6-port heuristic. We don't assert exact magnitudes
/// (those are entangled with the formulas); we assert ordering:
/// strictly-better positions must score strictly higher.
/// </summary>
public class HeuristicEvaluatorTests
{
    static HeuristicEvaluatorTests()
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
    public void Winning_DominatesEverythingElse()
    {
        var g = Fresh();
        // P0 has nothing. P1 has a huge board but hasn't won.
        var ten = g.Cards.First(c => c.Age == 10);
        g.Players[1].Stack(ten.Color).Meld(ten.Id);
        long beforeWin = HeuristicEvaluator.ScoreIndividual(g, 0);

        g.Phase = GamePhase.GameOver;
        g.Winners.Add(0);
        long afterWin = HeuristicEvaluator.ScoreIndividual(g, 0);

        Assert.True(afterWin - beforeWin >= 999_900,
            $"Winning bonus should add ~1M, got {afterWin - beforeWin}.");
    }

    [Fact]
    public void SearchDepth_ReducesWinBonusSlightly()
    {
        // Winning sooner is worth more — that's what the depth discount
        // models (VB6: 1_000_000 - 100 * gss_depth).
        var g = Fresh();
        g.Phase = GamePhase.GameOver;
        g.Winners.Add(0);
        long d0 = HeuristicEvaluator.ScoreIndividual(g, 0, searchDepth: 0);
        long d5 = HeuristicEvaluator.ScoreIndividual(g, 0, searchDepth: 5);
        Assert.Equal(500, d0 - d5);
    }

    [Fact]
    public void AchievementsContributeTenK_Each()
    {
        var g = Fresh();
        long before = HeuristicEvaluator.ScoreIndividual(g, 0);
        g.Players[0].AgeAchievements.Add(1);
        long after = HeuristicEvaluator.ScoreIndividual(g, 0);
        Assert.Equal(10_000, after - before);
    }

    [Fact]
    public void HigherTopCardScoresHigher()
    {
        var g = Fresh();
        var age1 = g.Cards.First(c => c.Age == 1);
        g.Players[0].Stack(age1.Color).Meld(age1.Id);
        long low = HeuristicEvaluator.ScoreIndividual(g, 0);

        var g2 = Fresh();
        var age5 = g2.Cards.First(c => c.Age == 5);
        g2.Players[0].Stack(age5.Color).Meld(age5.Id);
        long high = HeuristicEvaluator.ScoreIndividual(g2, 0);

        Assert.True(high > low);
    }

    [Fact]
    public void Splay_IncreasesScore_UpBeatsRightBeatsLeftBeatsNone()
    {
        // Build four identical 2-card Red piles with different splays
        // and check ordering.
        long Score(Splay splay)
        {
            var g = Fresh();
            var reds = g.Cards.Where(c => c.Color == CardColor.Red).Take(2).ToList();
            g.Players[0].Stack(CardColor.Red).Meld(reds[0].Id);
            g.Players[0].Stack(CardColor.Red).Tuck(reds[1].Id);
            if (splay != Splay.None) g.Players[0].Stack(CardColor.Red).ApplySplay(splay);
            return HeuristicEvaluator.ScoreIndividual(g, 0);
        }

        long none = Score(Splay.None);
        long left = Score(Splay.Left);
        long right = Score(Splay.Right);
        long up = Score(Splay.Up);

        Assert.True(up > right, $"up={up} right={right}");
        Assert.True(right > left, $"right={right} left={left}");
        Assert.True(left > none, $"left={left} none={none}");
    }

    [Fact]
    public void HandCards_AddValueEqualToTwiceAge()
    {
        var g = Fresh();
        long before = HeuristicEvaluator.ScoreIndividual(g, 0);
        // Add an age-3 hand card — should add 6.
        var age3 = g.Cards.First(c => c.Age == 3);
        g.Players[0].Hand.Add(age3.Id);
        long after = HeuristicEvaluator.ScoreIndividual(g, 0);
        Assert.Equal(6, after - before);
    }

    [Fact]
    public void ScorePile_IncreasesScoreButWithDiminishingReturns()
    {
        // 1 age-1 in score < 5 age-1s in score (strictly). Both are below
        // the cap so both use the 3·S^1.5 curve.
        var g1 = Fresh();
        g1.Players[0].ScorePile.Add(g1.Cards.First(c => c.Age == 1).Id);
        long s1 = HeuristicEvaluator.ScoreIndividual(g1, 0);

        var g5 = Fresh();
        var age1s = g5.Cards.Where(c => c.Age == 1).Take(5).ToList();
        foreach (var c in age1s) g5.Players[0].ScorePile.Add(c.Id);
        long s5 = HeuristicEvaluator.ScoreIndividual(g5, 0);

        Assert.True(s5 > s1);
    }

    [Fact]
    public void ScoreRelative_2p_SubtractsOpponent()
    {
        var g = Fresh();
        // Only p1 has any achievements.
        g.Players[1].AgeAchievements.Add(1);
        long s0 = HeuristicEvaluator.ScoreIndividual(g, 0);
        long s1 = HeuristicEvaluator.ScoreIndividual(g, 1);
        long rel0 = HeuristicEvaluator.ScoreRelative(g, 0);
        long rel1 = HeuristicEvaluator.ScoreRelative(g, 1);
        // p1 strictly better than p0.
        Assert.True(rel1 > rel0);
        // 2p: rel = 2·self − opp. Expressing in terms of Individual so the
        // assertion doesn't bake in whatever flat contributions the VB6-
        // faithful icon bug adds to both sides.
        Assert.Equal(2L * s0 - s1, rel0);
        Assert.Equal(2L * s1 - s0, rel1);
    }

    [Fact]
    public void ScoreRelative_WeightsSelfByTwoTimesNMinusOne()
    {
        // 3-player game: self weight = 4, subtract two opponents.
        var g = Fresh(3);
        g.Players[0].AgeAchievements.Add(1);
        long s0 = HeuristicEvaluator.ScoreIndividual(g, 0);
        long s1 = HeuristicEvaluator.ScoreIndividual(g, 1);
        long s2 = HeuristicEvaluator.ScoreIndividual(g, 2);
        long rel = HeuristicEvaluator.ScoreRelative(g, 0);
        Assert.Equal(2L * 2 * s0 - s1 - s2, rel);
    }

    [Fact]
    public void IconRaceBonus_NeverFires_WhenSomeoneHasIcons_VB6Bug()
    {
        // Documents the ported VB6 bug: leader bonus is only awarded when
        // every player has zero of the icon. Here p0 has Red-melded
        // (board icons > 0), so the "someone leads" bonus doesn't fire —
        // the VB6-faithful code reads a stale OOB zero and the
        // `staleIconRead == max` check fails.
        //
        // We verify: giving p0 lots of icons doesn't produce the +75
        // "sole leader" cliff — score increases only linearly (2 per
        // icon). If someone ever "fixes" the port and the bonus starts
        // firing, this test flags the behavior change loudly.
        var g = Fresh();

        // Zero icons: all-empty board, no meld.
        long zero = HeuristicEvaluator.ScoreIndividual(g, 0);

        // Now meld a Blue (Lightbulb-icon-heavy) card on p0 only. The
        // non-stale version of the heuristic would award +75 for sole
        // leader; the VB6-faithful version only gives +2 per icon.
        var blue = g.Cards.First(c => c.Color == CardColor.Blue);
        g.Players[0].Stack(CardColor.Blue).Meld(blue.Id);
        long one = HeuristicEvaluator.ScoreIndividual(g, 0);

        long delta = one - zero;
        // Delta = per-color block (20 + 1 + age) + icons (2 per visible).
        // With the bug, delta < 75 as long as visible icons are modest.
        // Blue age-1 cards have at most 4 icon slots. Most don't hit the
        // "4 of the same icon" density. The upper bound on the icon-race
        // contribution (ignoring bonus) is 2*4 = 8 per icon-type per pile.
        // Practical ceiling for this delta: 20 + 1 + 1 + (2*4 per icon type
        // across 6 types) = 22 + 48 = 70. If a +75 leader bonus fired,
        // delta would exceed 70 cleanly.
        Assert.True(delta < 75, $"delta={delta} — leader bonus unexpectedly fired.");
    }
}
