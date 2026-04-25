namespace Innovation.Core.Handlers;

/// <summary>
/// Mathematics (age 2, Blue/Lightbulb): "You may return a card from your
/// hand. If you do, draw and meld a card of value one higher than the
/// card you returned."
///
/// Same two-phase pattern as Agriculture — raise an optional hand-card
/// choice; if the player picks one, return it and draw-and-meld at
/// age+1 (walking up on empty decks, per <see cref="Mechanics.DrawAndMeld"/>).
/// </summary>
public sealed class MathematicsHandler : IDogmaHandler
{
    public bool Execute(GameState g, PlayerState target, DogmaContext ctx)
    {
        if (target.Hand.Count == 0) return false;

        if (ctx.PendingChoice is null)
        {
            ctx.PendingChoice = new SelectHandCardRequest
            {
                Prompt = "Mathematics: return a card from your hand? "
                       + "(Skip to do nothing; otherwise draw-and-meld at age+1.)",
                PlayerIndex = target.Index,
                EligibleCardIds = target.Hand.ToArray(),
                AllowNone = true,
            };
            ctx.Paused = true;
            return false;
        }

        var req = (SelectHandCardRequest)ctx.PendingChoice;
        ctx.PendingChoice = null;

        if (req.ChosenCardId is not int chosen) return false;

        int returnedAge = g.Cards[chosen].Age;
        Mechanics.Return(g, target, chosen);
        int drawn = Mechanics.DrawAndMeld(g, target, returnedAge + 1);
        return drawn >= 0 || g.IsGameOver;
    }
}
