namespace Innovation.Core;

/// <summary>
/// Age-tile achievement logic. Special achievements (Monument, Empire,
/// World, Wonder, Universe) live in a separate pass run after every
/// state-changing event.
/// </summary>
public static class AchievementRules
{
    /// <summary>
    /// A player may claim age-N if:
    ///   • the tile is still available,
    ///   • their score is at least 5×N, and
    ///   • their highest top card is at least age N.
    /// Mirrors VB6 <c>can_achieve</c> (main.frm 8717).
    /// </summary>
    public static bool CanClaim(GameState g, PlayerState p, int age)
    {
        if (age < 1 || age > 9) return false;
        if (!g.AvailableAgeAchievements.Contains(age)) return false;
        if (p.Score(g.Cards) < 5 * age) return false;
        if (Mechanics.HighestTopCardAge(g, p) < age) return false;
        return true;
    }

    /// <summary>
    /// Claim the age-N tile for player. Returns true on success.
    /// Does NOT decrement actions; the caller is responsible via TurnManager.
    /// </summary>
    public static bool Claim(GameState g, PlayerState p, int age)
    {
        if (!CanClaim(g, p, age)) return false;
        g.AvailableAgeAchievements.Remove(age);
        p.AgeAchievements.Add(age);
        CheckAchievementWin(g);
        return true;
    }

    /// <summary>
    /// Claim a special achievement tile for player. Returns true on success.
    /// No eligibility check here — call only from a path that knows the
    /// trigger conditions were met.
    /// </summary>
    public static bool ClaimSpecial(GameState g, PlayerState p, string name)
    {
        if (!g.AvailableSpecialAchievements.Remove(name)) return false;
        p.SpecialAchievements.Add(name);
        CheckAchievementWin(g);
        return true;
    }

    /// <summary>
    /// End-of-game check by achievement count. Mirrors VB6 <c>end_game_points</c>:
    ///   • 2 players: 6 achievements
    ///   • 3 players: 5 achievements
    ///   • 4 players: 4 achievements
    /// </summary>
    public static void CheckAchievementWin(GameState g)
    {
        if (g.IsGameOver) return;
        int threshold = g.Players.Length switch
        {
            2 => 6,
            3 => 5,
            4 => 4,
            _ => int.MaxValue,
        };
        List<int>? winners = null;
        foreach (var p in g.Players)
        {
            if (p.AchievementCount >= threshold)
            {
                winners ??= new List<int>();
                winners.Add(p.Index);
            }
        }
        if (winners is null) return;
        g.Winners.Clear();
        g.Winners.AddRange(winners);
        g.Phase = GamePhase.GameOver;
    }
}
