namespace Innovation.Core.Handlers;

/// <summary>
/// Reformation (age 4, Purple/Leaf) — effect 1: "You may tuck a card
/// from your hand for every two [Leaf] icons on your board."
///
/// Subset pick: 0..min(leafs/2, hand.Count). All chosen cards are
/// tucked, each in its color's stack. Monument eligibility via
/// <see cref="Mechanics.Tuck"/>.
/// </summary>
public sealed class ReformationTuckHandler : IDogmaHandler
{
    public bool Execute(GameState g, PlayerState target, DogmaContext ctx)
    {
        if (ctx.HandlerState is null && ctx.PendingChoice is null)
        {
            int leafs = IconCounter.Count(target, Icon.Leaf, g.Cards);
            int max = Math.Min(leafs / 2, target.Hand.Count);
            if (max <= 0) return false;

            ctx.HandlerState = new object();
            ctx.PendingChoice = new SelectHandCardSubsetRequest
            {
                Prompt = $"Reformation: tuck up to {max} card(s) from your hand.",
                PlayerIndex = target.Index,
                EligibleCardIds = target.Hand.ToArray(),
                MinCount = 0,
                MaxCount = max,
            };
            ctx.Paused = true;
            return false;
        }

        if (ctx.PendingChoice is SelectHandCardSubsetRequest subset)
        {
            ctx.PendingChoice = null;
            var picks = subset.ChosenCardIds.ToArray();
            if (picks.Length == 0) { ctx.HandlerState = null; return false; }
            if (picks.Length == 1 || !Mechanics.OrderMatters(picks, id => g.Cards[id].Color))
            {
                foreach (var id in picks)
                {
                    Mechanics.Tuck(g, target, id);
                    if (g.IsGameOver) { ctx.HandlerState = null; return true; }
                }
                ctx.HandlerState = null;
                return true;
            }
            ctx.HandlerState = picks;
            ctx.PendingChoice = new SelectCardOrderRequest
            {
                Prompt = "Reformation: choose the tuck order (last tucked is at the bottom).",
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
        return ordered.Count > 0;
    }
}
