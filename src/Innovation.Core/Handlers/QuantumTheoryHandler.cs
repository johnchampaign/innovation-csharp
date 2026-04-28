namespace Innovation.Core.Handlers;

/// <summary>
/// Quantum Theory (age 8, Blue/Clock): "You may return up to two cards from
/// your hand. If you return two, draw a 10 and then draw and score a 10."
/// </summary>
public sealed class QuantumTheoryHandler : IDogmaHandler
{
    public bool Execute(GameState g, PlayerState target, DogmaContext ctx)
    {
        if (target.Hand.Count == 0) return false;

        if (ctx.PendingChoice is null)
        {
            ctx.PendingChoice = new SelectHandCardSubsetRequest
            {
                Prompt = "Quantum Theory: return up to two cards. If you return two, draw a 10 and draw+score a 10.",
                PlayerIndex = target.Index,
                EligibleCardIds = target.Hand.ToArray(),
                MinCount = 0,
                MaxCount = Math.Min(2, target.Hand.Count),
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
                Mechanics.Return(g, target, picks[0]);
                return true;
            }
            // 2 picks. If they're different ages the order is irrelevant.
            if (!Mechanics.OrderMatters(picks, id => g.Cards[id].Age))
            {
                foreach (var id in picks) Mechanics.Return(g, target, id);
                Mechanics.DrawFromAge(g, target, 10);
                if (g.IsGameOver) return true;
                Mechanics.DrawAndScore(g, target, 10);
                return true;
            }
            // Same age — order matters; prompt.
            ctx.HandlerState = picks;
            ctx.PendingChoice = new SelectCardOrderRequest
            {
                Prompt = "Quantum Theory: choose the return order.",
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
        // Reverse: chosen is deck-arrangement top-first.
        for (int i = ordered.Count - 1; i >= 0; i--)
            Mechanics.Return(g, target, ordered[i]);

        // 2 returns triggers the bonus.
        Mechanics.DrawFromAge(g, target, 10);
        if (g.IsGameOver) return true;
        Mechanics.DrawAndScore(g, target, 10);
        return true;
    }
}
