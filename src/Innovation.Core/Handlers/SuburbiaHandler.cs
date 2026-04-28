namespace Innovation.Core.Handlers;

/// <summary>
/// Suburbia (age 9, Yellow/Leaf): "You may tuck any number of cards from
/// your hand. Draw and score a 1 for each card you tucked."
/// </summary>
public sealed class SuburbiaHandler : IDogmaHandler
{
    public bool Execute(GameState g, PlayerState target, DogmaContext ctx)
    {
        if (target.Hand.Count == 0) return false;

        if (ctx.PendingChoice is null)
        {
            ctx.PendingChoice = new SelectHandCardSubsetRequest
            {
                Prompt = "Suburbia: tuck any number of cards. Draw and score a 1 for each.",
                PlayerIndex = target.Index,
                EligibleCardIds = target.Hand.ToArray(),
                MinCount = 0,
                MaxCount = target.Hand.Count,
            };
            ctx.Paused = true;
            return false;
        }

        if (ctx.PendingChoice is SelectHandCardSubsetRequest subset)
        {
            ctx.PendingChoice = null;
            var picks = subset.ChosenCardIds.ToArray();
            if (picks.Length == 0) return false;
            if (picks.Length == 1 || !Mechanics.OrderMatters(picks, id => g.Cards[id].Color))
            {
                foreach (var id in picks)
                {
                    Mechanics.Tuck(g, target, id);
                    if (g.IsGameOver) return true;
                }
                for (int i = 0; i < picks.Length; i++)
                {
                    if (g.IsGameOver) break;
                    Mechanics.DrawAndScore(g, target, 1);
                }
                return true;
            }
            ctx.HandlerState = picks;
            ctx.PendingChoice = new SelectCardOrderRequest
            {
                Prompt = "Suburbia: choose the tuck order (last tucked is at the bottom).",
                PlayerIndex = target.Index,
                Action = "tuck",
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
        foreach (var id in ordered)
        {
            Mechanics.Tuck(g, target, id);
            if (g.IsGameOver) return true;
        }
        for (int i = 0; i < ordered.Count; i++)
        {
            Mechanics.DrawAndScore(g, target, 1);
            if (g.IsGameOver) return true;
        }
        return ordered.Count > 0;
    }
}
