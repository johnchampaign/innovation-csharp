namespace Innovation.Core.Handlers;

/// <summary>
/// Refrigeration effect 1 (age 7, Yellow/Leaf, demand): "I demand you
/// return half (rounded down) of the cards in your hand!" Target picks
/// which.
/// </summary>
public sealed class RefrigerationDemandHandler : IDogmaHandler
{
    public bool Execute(GameState g, PlayerState target, DogmaContext ctx)
    {
        if (ctx.PendingChoice is null)
        {
            int toReturn = target.Hand.Count / 2;
            if (toReturn == 0) return false;
            ctx.PendingChoice = new SelectHandCardSubsetRequest
            {
                Prompt = $"Refrigeration: return {toReturn} card(s) from your hand.",
                PlayerIndex = target.Index,
                EligibleCardIds = target.Hand.ToArray(),
                MinCount = toReturn,
                MaxCount = toReturn,
            };
            ctx.Paused = true;
            return false;
        }

        var req = (SelectHandCardSubsetRequest)ctx.PendingChoice;
        ctx.PendingChoice = null;
        foreach (var id in req.ChosenCardIds) Mechanics.Return(g, target, id);
        if (req.ChosenCardIds.Count > 0) ctx.DemandSuccessful = true;
        return req.ChosenCardIds.Count > 0;
    }
}
