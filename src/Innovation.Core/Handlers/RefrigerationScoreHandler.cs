namespace Innovation.Core.Handlers;

/// <summary>
/// Refrigeration effect 2 (age 7, Yellow/Leaf): "You may score a card
/// from your hand."
/// </summary>
public sealed class RefrigerationScoreHandler : IDogmaHandler
{
    public bool Execute(GameState g, PlayerState target, DogmaContext ctx)
    {
        if (ctx.PendingChoice is null)
        {
            if (target.Hand.Count == 0) return false;
            ctx.PendingChoice = new SelectHandCardRequest
            {
                Prompt = "Refrigeration: score a card from your hand?",
                PlayerIndex = target.Index,
                EligibleCardIds = target.Hand.ToArray(),
                AllowNone = true,
            };
            ctx.Paused = true;
            return false;
        }

        var req = (SelectHandCardRequest)ctx.PendingChoice;
        ctx.PendingChoice = null;
        if (req.ChosenCardId is not int id) return false;
        Mechanics.Score(g, target, id);
        return true;
    }
}
