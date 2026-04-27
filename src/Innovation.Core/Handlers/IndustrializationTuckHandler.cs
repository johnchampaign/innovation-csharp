namespace Innovation.Core.Handlers;

/// <summary>
/// Industrialization (age 6, Red/Factory) — effect 1: "Draw and tuck a 6
/// for every two [Factory] icons on your board."
///
/// Each "draw and tuck a 6" is an atomic unit, repeated N times — RAW the
/// player doesn't get to see all the drawn cards before tucking, so there's
/// no order choice. The card-yet-to-be-drawn is unknown until the draw
/// happens, which distinguishes this from "tuck cards from your hand"
/// where the cards are visible up front.
///
/// Factory count comes from the frozen activation-time snapshot per the
/// icons-don't-update-during-dogma rule.
/// </summary>
public sealed class IndustrializationTuckHandler : IDogmaHandler
{
    public bool Execute(GameState g, PlayerState target, DogmaContext ctx)
    {
        int factories = ctx.FrozenIconCounts is { } frozen
            ? frozen[target.Index]
            : IconCounter.Count(target, Icon.Factory, g.Cards);
        int n = factories / 2;
        if (n == 0) return false;

        for (int i = 0; i < n; i++)
        {
            if (Mechanics.DrawAndTuck(g, target, 6) < 0 || g.IsGameOver) return true;
        }
        return true;
    }
}
