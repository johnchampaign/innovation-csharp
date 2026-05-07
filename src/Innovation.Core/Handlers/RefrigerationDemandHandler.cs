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

        if (ctx.PendingChoice is SelectHandCardSubsetRequest subset)
        {
            ctx.PendingChoice = null;
            var picks = subset.ChosenCardIds.ToArray();
            if (picks.Length == 0) return false;
            if (picks.Length == 1 || !Mechanics.OrderMatters(picks, id => g.Cards[id].Age))
            {
                foreach (var id in picks) Mechanics.Return(g, target, id);
                ctx.DemandSuccessful = true;
                return true;
            }
            ctx.HandlerState = picks;
            ctx.PendingChoice = new SelectCardOrderRequest
            {
                Prompt = "Refrigeration: choose the return order.",
                PlayerIndex = target.Index,
                Action = "return",
                CardIds = picks,
            };
            ctx.Paused = true;
            return false;
        }

        // Order resolved: chosen order is deck-arrangement top-first; reverse.
        var orderReq = (SelectCardOrderRequest)ctx.PendingChoice!;
        var input = (int[])ctx.HandlerState!;
        ctx.PendingChoice = null;
        ctx.HandlerState = null;
        var ordered = Mechanics.ValidateOrder(orderReq.ChosenOrder, input);
        for (int i = ordered.Count - 1; i >= 0; i--)
            Mechanics.Return(g, target, ordered[i]);
        ctx.DemandSuccessful = true;
        return true;
    }
}
