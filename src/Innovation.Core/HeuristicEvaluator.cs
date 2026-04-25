namespace Innovation.Core;

/// <summary>
/// Heuristic position evaluator — a faithful port of the VB6 AI's
/// <c>score_game_individual</c> + <c>score_game</c> (AIFunctions.bas
/// lines 547–627). Higher values are better for the named player.
///
/// Components (VB6 line-for-line):
///   • Winning the game: 1_000_000 − 100 × searchDepth.
///   • 10_000 per achievement (age + special).
///   • 5 × (highest top-card age)².
///   • Per non-empty color: 20 base + pile depth + top-card age + splay
///     bonus (Up=40, Right=25, Left=10).
///   • Icon race: 2 per visible icon owned; +75 for sole leader, +50 for
///     tied leader (see KNOWN-BUG below on when the bonus actually fires).
///   • 2 per age of hand cards.
///   • Score pile: 3·S^1.5 up to a cap of 5·(min(top,8)+1); overage
///     contributes at half rate.
///
/// KNOWN-BUG (VB6, faithfully reproduced): the icon leader-bonus check
/// at AIFunctions.bas 606–607 reads <c>icon_total(i, j)</c> where <c>i</c>
/// is one past the last valid player — VB6 leaves loop variables at
/// limit+1 after a For..Next exits. In VB6 that OOB read returns 0 (large
/// arrays are zero-initialized), so the bonus only fires when <em>every
/// player</em> has 0 of the icon — effectively never. The port preserves
/// this behavior so Phase 5 AI decisions stay bit-compatible with the
/// original; future work may add an opt-in "fixed" path.
///
/// The VB6 scorer also calls <c>update_scores</c> and <c>update_icon_total</c>
/// at the top (main.frm 565–566). Those are no-ops here: <see cref="PlayerState.Score"/>
/// and <see cref="IconCounter.Count"/> compute fresh each call, so there's
/// no cached state to refresh.
/// </summary>
public static class HeuristicEvaluator
{
    /// <summary>
    /// Raw heuristic score for one player, looking at the board in
    /// isolation. <see cref="ScoreRelative"/> is what an AI that's
    /// maximizing its own position should use; this one is the per-player
    /// component.
    /// </summary>
    public static long ScoreIndividual(GameState g, int playerIndex, int searchDepth = 0)
    {
        var p = g.Players[playerIndex];
        long total = 0;

        // Winning trumps everything. VB6 lines 570–573. gss_depth becomes
        // searchDepth — the VB6 lookahead tallies depth to break ties in
        // favor of winning sooner.
        if (g.IsGameOver && g.Winners.Contains(playerIndex))
            total += 1_000_000 - 100 * searchDepth;

        // Achievements. VB6 line 576.
        total += 10_000L * p.AchievementCount;

        // Top-card quadratic. VB6 lines 578–580.
        int topCard = Mechanics.HighestTopCardAge(g, p);
        total += 5L * topCard * topCard;

        // Per-color pile bonuses. VB6 lines 585–592.
        foreach (var stack in p.Stacks)
        {
            if (stack.IsEmpty) continue;
            total += 20 + stack.Count + g.Cards[stack.Top].Age;
            total += stack.Splay switch
            {
                Splay.Up => 40,
                Splay.Right => 25,
                Splay.Left => 10,
                _ => 0,
            };
        }

        // Icon race. VB6 lines 594–608.
        for (int iconIdx = 1; iconIdx <= 6; iconIdx++)
        {
            var icon = (Icon)iconIdx;
            int max = 0;
            int numAtMax = 1;
            for (int otherIdx = 0; otherIdx < g.Players.Length; otherIdx++)
            {
                int cnt = IconCounter.Count(g.Players[otherIdx], icon, g.Cards);
                if (cnt == max) numAtMax++;
                if (cnt > max) { max = cnt; numAtMax = 1; }
            }
            total += 2L * IconCounter.Count(p, icon, g.Cards);

            // BUG-preserve: VB6's stale-`i` read evaluates to 0. Bonus
            // only applies when max is also 0, i.e. nobody has the icon.
            const int staleIconRead = 0;
            if (staleIconRead == max && numAtMax == 1) total += 75;
            if (staleIconRead == max && numAtMax > 1) total += 50;
        }

        // Hand value. VB6 lines 611–613.
        foreach (var id in p.Hand) total += 2L * g.Cards[id].Age;

        // Score pile curve. VB6 lines 616–622.
        int cappedTop = topCard > 8 ? 8 : topCard;
        int topScoreCap = 5 * (cappedTop + 1);
        int scoreSum = p.Score(g.Cards);
        if (scoreSum > topScoreCap)
            total += (long)(3 * Math.Pow(topScoreCap, 1.5) + (scoreSum - topScoreCap) / 2.0);
        else
            total += (long)(3 * Math.Pow(scoreSum, 1.5));

        return total;
    }

    /// <summary>
    /// Zero-sum-ish relative score: weight the named player's individual
    /// score by <c>2 × (numPlayers − 1)</c> and subtract each opponent's
    /// individual score. Matches VB6 <c>score_game</c> (AIFunctions.bas
    /// lines 547–558). This is what an AI picking its own move should
    /// maximize — "make me better <em>and</em> make them worse".
    /// </summary>
    public static long ScoreRelative(GameState g, int playerIndex, int searchDepth = 0)
    {
        int n = g.Players.Length;
        long self = ScoreIndividual(g, playerIndex, searchDepth);
        long total = 2L * (n - 1) * self;
        for (int i = 0; i < n; i++)
        {
            if (i == playerIndex) continue;
            total -= ScoreIndividual(g, i, searchDepth);
        }
        return total;
    }
}
