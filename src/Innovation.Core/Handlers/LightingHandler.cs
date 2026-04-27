namespace Innovation.Core.Handlers;

/// <summary>
/// Lighting (age 7, Purple/Leaf): "You may tuck up to three cards from
/// your hand. If you do, draw and score a 7 for every different value of
/// card you tucked."
/// </summary>
public sealed class LightingHandler : IDogmaHandler
{
    public bool Execute(GameState g, PlayerState target, DogmaContext ctx)
    {
        if (ctx.PendingChoice is null)
        {
            if (target.Hand.Count == 0) return false;
            ctx.PendingChoice = new SelectHandCardSubsetRequest
            {
                Prompt = "Lighting: tuck up to 3 cards; draw and score a 7 for each distinct age tucked.",
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
                Mechanics.Tuck(g, target, picks[0]);
                if (!g.IsGameOver) Mechanics.DrawAndScore(g, target, 7);
                return true;
            }
            // Multiple tucks — ask the player for tuck order. The order
            // affects which card ends up on the bottom of which stack and
            // therefore which icons are revealed by future splays.
            ctx.HandlerState = picks;
            ctx.PendingChoice = new SelectCardOrderRequest
            {
                Prompt = "Lighting: choose the tuck order (last tucked goes to the bottom of its color pile).",
                PlayerIndex = target.Index,
                Action = "tuck",
                CardIds = picks,
            };
            ctx.Paused = true;
            return false;
        }

        // Resume after tuck-order pick: apply tucks then award draws.
        var orderReq = (SelectCardOrderRequest)ctx.PendingChoice!;
        var input = (int[])ctx.HandlerState!;
        ctx.PendingChoice = null;
        ctx.HandlerState = null;
        var ordered = Mechanics.ValidateOrder(orderReq.ChosenOrder, input);
        var distinct = new HashSet<int>();
        foreach (var id in ordered)
        {
            distinct.Add(g.Cards[id].Age);
            Mechanics.Tuck(g, target, id);
            if (g.IsGameOver) return true;
        }
        foreach (var _ in distinct)
        {
            if (g.IsGameOver) break;
            Mechanics.DrawAndScore(g, target, 7);
        }
        return true;
    }
}
