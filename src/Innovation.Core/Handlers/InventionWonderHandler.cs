namespace Innovation.Core.Handlers;

/// <summary>
/// Invention (age 4, Green/Lightbulb) — effect 2: "If you have five
/// colors splayed, each in any direction, claim the Wonder achievement."
///
/// This is the card-granted path to Wonder, distinct from the icon-based
/// auto-claim in <see cref="SpecialAchievements.TryClaimWonder"/> (which
/// requires Up/Right, not any direction).
/// </summary>
public sealed class InventionWonderHandler : IDogmaHandler
{
    public bool Execute(GameState g, PlayerState target, DogmaContext ctx)
    {
        foreach (var s in target.Stacks)
            if (s.Splay == Splay.None) return false;
        return AchievementRules.ClaimSpecial(g, target, SpecialAchievements.Wonder);
    }
}
