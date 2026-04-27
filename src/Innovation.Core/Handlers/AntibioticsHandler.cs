namespace Innovation.Core.Handlers;

/// <summary>
/// Antibiotics (age 8, Yellow/Leaf): "You may return up to three cards from
/// your hand. For every different value of card that you returned, draw
/// two 8s."
/// </summary>
public sealed class AntibioticsHandler : IDogmaHandler
{
    public bool Execute(GameState g, PlayerState target, DogmaContext ctx)
    {
        if (target.Hand.Count == 0) return false;

        if (ctx.PendingChoice is null)
        {
            ctx.PendingChoice = new SelectHandCardSubsetRequest
            {
                Prompt = "Antibiotics: return up to three cards. Draw two 8s for each distinct age returned.",
                PlayerIndex = target.Index,
                EligibleCardIds = target.Hand.ToArray(),
                MinCount = 0,
                MaxCount = Math.Min(3, target.Hand.Count),
            };
            ctx.Paused = true;
            return false;
        }

        if (ctx.PendingChoice is SelectHandCardSubsetRequest subset)
        {
            ctx.PendingChoice = null;
            var picks = subset.ChosenCardIds.ToArray();
            if (picks.Length == 0) return false;
            if (picks.Length == 1)
            {
                ApplyReturnsAndDraws(g, target, picks);
                return true;
            }
            ctx.HandlerState = picks;
            ctx.PendingChoice = new SelectCardOrderRequest
            {
                Prompt = "Antibiotics: choose the return order.",
                PlayerIndex = target.Index,
                Action = "return",
                CardIds = picks,
            };
            ctx.Paused = true;
            return false;
        }

        var orderReq = (SelectCardOrderRequest)ctx.PendingChoice!;
        var input = (int[])ctx.HandlerState!;
        ctx.PendingChoice = null;
        ctx.HandlerState = null;
        var ordered = Mechanics.ValidateOrder(orderReq.ChosenOrder, input);
        ApplyReturnsAndDraws(g, target, ordered);
        return ordered.Count > 0;
    }

    private static void ApplyReturnsAndDraws(GameState g, PlayerState target, IReadOnlyList<int> ids)
    {
        // ids is final deck-arrangement top-first. Reverse for application.
        var distinctAges = new HashSet<int>();
        for (int i = ids.Count - 1; i >= 0; i--)
        {
            int id = ids[i];
            distinctAges.Add(g.Cards[id].Age);
            Mechanics.Return(g, target, id);
        }
        int draws = distinctAges.Count * 2;
        for (int i = 0; i < draws; i++)
        {
            if (g.IsGameOver) return;
            Mechanics.DrawFromAge(g, target, 8);
        }
    }
}
