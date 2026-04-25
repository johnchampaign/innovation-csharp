namespace Innovation.Core.Handlers;

/// <summary>
/// Translation (age 3, Blue/Crown) — effect 2: "If each top card on
/// your board has a [Crown], claim the World achievement."
///
/// Every non-empty pile's top card must show a Crown. Empty piles are
/// skipped — the check is over "each top card on your board", not over
/// all five colors. At least one top card must exist (claiming World
/// off a completely empty board would be absurd).
/// </summary>
public sealed class TranslationWorldHandler : IDogmaHandler
{
    public bool Execute(GameState g, PlayerState target, DogmaContext ctx)
    {
        bool any = false;
        foreach (var s in target.Stacks)
        {
            if (s.IsEmpty) continue;
            any = true;
            if (!Mechanics.HasIcon(g.Cards[s.Top], Icon.Crown)) return false;
        }
        if (!any) return false;
        return AchievementRules.ClaimSpecial(g, target, SpecialAchievements.World);
    }
}
