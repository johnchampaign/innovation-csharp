namespace Innovation.Core;

/// <summary>
/// Auto-detection for the four "board-state" special achievements. Monument
/// is counter-based and handled inline by <see cref="Mechanics.Score"/> /
/// <see cref="Mechanics.Tuck"/>; the four here look only at static board
/// state and so can be re-run any time state changes.
///
/// Mirrors the checks inside VB6 <c>update_icon_total</c> (World, Empire)
/// and <c>check_for_achievements</c> (Universe, Wonder): player iteration
/// starts at the active player (turn-order tiebreaker), one claim per
/// invocation, <see cref="CheckAll"/> loops until nothing new fires to pick
/// up any chains.
/// </summary>
public static class SpecialAchievements
{
    public const string Monument = "Monument";
    public const string Empire   = "Empire";
    public const string World    = "World";
    public const string Wonder   = "Wonder";
    public const string Universe = "Universe";

    /// <summary>
    /// Run every auto-detected check. Intended to be called after any
    /// state-changing event (meld, tuck, score, splay, transfer, …).
    /// </summary>
    public static void CheckAll(GameState g)
    {
        if (g.IsGameOver) return;
        // Loop until no new claim happens. Usually terminates in 1 pass;
        // cascades (Empire triggering via an icon gained from the same event
        // that triggered World, etc.) are handled here.
        bool changed;
        do
        {
            changed = false;
            if (TryClaimWorld(g))    changed = true;
            if (TryClaimEmpire(g))   changed = true;
            if (TryClaimUniverse(g)) changed = true;
            if (TryClaimWonder(g))   changed = true;
        }
        while (changed && !g.IsGameOver);
    }

    /// <summary>≥12 Clock icons claims World.</summary>
    public static bool TryClaimWorld(GameState g)
    {
        if (!g.AvailableSpecialAchievements.Contains(World)) return false;
        foreach (var p in InActiveOrder(g))
        {
            if (IconCounter.Count(p, Icon.Clock, g.Cards) >= 12)
                return AchievementRules.ClaimSpecial(g, p, World);
        }
        return false;
    }

    /// <summary>
    /// ≥3 of each of the six icon types claims Empire. Matches both the
    /// rulebook ("three or more icons of all six types") and VB6 main.frm 7117.
    /// </summary>
    public static bool TryClaimEmpire(GameState g)
    {
        if (!g.AvailableSpecialAchievements.Contains(Empire)) return false;
        foreach (var p in InActiveOrder(g))
        {
            if (HasAtLeastThreeOfEveryIcon(g, p))
                return AchievementRules.ClaimSpecial(g, p, Empire);
        }
        return false;
    }

    /// <summary>
    /// All 5 colors present, every top card age ≥8 claims Universe. Matches
    /// both the rulebook ("five top cards, each of value 8 or higher") and
    /// VB6 main.frm 7149.
    /// </summary>
    public static bool TryClaimUniverse(GameState g)
    {
        if (!g.AvailableSpecialAchievements.Contains(Universe)) return false;
        foreach (var p in InActiveOrder(g))
        {
            bool ok = true;
            foreach (var stack in p.Stacks)
            {
                if (stack.IsEmpty || g.Cards[stack.Top].Age < 8) { ok = false; break; }
            }
            if (ok) return AchievementRules.ClaimSpecial(g, p, Universe);
        }
        return false;
    }

    /// <summary>All 5 colors present and each splayed Up or Right claims Wonder.</summary>
    public static bool TryClaimWonder(GameState g)
    {
        if (!g.AvailableSpecialAchievements.Contains(Wonder)) return false;
        foreach (var p in InActiveOrder(g))
        {
            bool ok = true;
            foreach (var stack in p.Stacks)
            {
                if (stack.IsEmpty) { ok = false; break; }
                if (stack.Splay != Splay.Up && stack.Splay != Splay.Right) { ok = false; break; }
            }
            if (ok) return AchievementRules.ClaimSpecial(g, p, Wonder);
        }
        return false;
    }

    // ---- helpers ----

    /// <summary>
    /// Players in turn order starting from the active player. Matches VB6's
    /// <c>For i = active_player To active_player + num_players - 1</c>
    /// iteration for achievement-claim tiebreakers.
    /// </summary>
    private static IEnumerable<PlayerState> InActiveOrder(GameState g)
    {
        int n = g.Players.Length;
        for (int i = 0; i < n; i++)
            yield return g.Players[(g.ActivePlayer + i) % n];
    }

    private static readonly Icon[] AllSixIcons =
    {
        Icon.Leaf, Icon.Castle, Icon.Lightbulb, Icon.Crown, Icon.Factory, Icon.Clock,
    };

    private static bool HasAtLeastThreeOfEveryIcon(GameState g, PlayerState p)
    {
        foreach (var icon in AllSixIcons)
        {
            if (IconCounter.Count(p, icon, g.Cards) < 3) return false;
        }
        return true;
    }
}
