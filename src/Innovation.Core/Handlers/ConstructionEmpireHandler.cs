namespace Innovation.Core.Handlers;

/// <summary>
/// Construction second effect (age 2, Red/Castle, non-demand): "If you are
/// the only player with five top cards, claim the Empire achievement."
///
/// "Five top cards" = all five color piles non-empty. This is a second
/// (card-granted) path to Empire distinct from the icon-based auto-claim
/// in <see cref="SpecialAchievements.TryClaimEmpire"/>. Matches VB6
/// main.frm 4641–4656.
/// </summary>
public sealed class ConstructionEmpireHandler : IDogmaHandler
{
    public bool Execute(GameState g, PlayerState target, DogmaContext ctx)
    {
        if (!HasAllFiveColors(target)) return false;
        foreach (var p in g.Players)
        {
            if (p.Index == target.Index) continue;
            if (HasAllFiveColors(p)) return false;
        }
        return AchievementRules.ClaimSpecial(g, target, SpecialAchievements.Empire);
    }

    private static bool HasAllFiveColors(PlayerState p)
    {
        foreach (var stack in p.Stacks)
            if (stack.IsEmpty) return false;
        return true;
    }
}
