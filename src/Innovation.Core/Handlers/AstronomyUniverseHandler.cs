namespace Innovation.Core.Handlers;

/// <summary>
/// Astronomy (age 5, Purple/Lightbulb) — effect 2: "If all non-purple
/// top cards on your board are value 6 or higher, claim the Universe
/// achievement."
///
/// Non-purple tops must exist (at least one) and each must be ≥6. Empty
/// non-purple piles count as failing the "top card" condition, so they
/// block the claim (standard Innovation reading — claim needs all four
/// non-purple colors melded and ≥6).
/// </summary>
public sealed class AstronomyUniverseHandler : IDogmaHandler
{
    public bool Execute(GameState g, PlayerState target, DogmaContext ctx)
    {
        foreach (CardColor c in Enum.GetValues<CardColor>())
        {
            if (c == CardColor.Purple) continue;
            var s = target.Stack(c);
            if (s.IsEmpty) return false;
            if (g.Cards[s.Top].Age < 6) return false;
        }
        return AchievementRules.ClaimSpecial(g, target, SpecialAchievements.Universe);
    }
}
