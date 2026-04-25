namespace Innovation.Core.Handlers;

/// <summary>
/// Publications effect 1 (age 7, Blue/Lightbulb): "You may rearrange the
/// order of one color of cards on your board." Pick a color with ≥2 cards,
/// then supply a new top-to-bottom order.
/// </summary>
public sealed class PublicationsRearrangeHandler : IDogmaHandler
{
    private enum Stage { PickColor, PickOrder }

    public bool Execute(GameState g, PlayerState target, DogmaContext ctx)
    {
        var stage = (Stage?)ctx.HandlerState ?? Stage.PickColor;

        if (stage == Stage.PickColor)
        {
            if (ctx.PendingChoice is null)
            {
                var eligible = new List<CardColor>();
                for (int i = 0; i < target.Stacks.Length; i++)
                    if (target.Stacks[i].Count >= 2) eligible.Add((CardColor)i);
                if (eligible.Count == 0) return false;

                ctx.PendingChoice = new SelectColorRequest
                {
                    Prompt = "Publications: choose a color to rearrange.",
                    PlayerIndex = target.Index,
                    EligibleColors = eligible,
                    AllowNone = true,
                };
                ctx.HandlerState = Stage.PickColor;
                ctx.Paused = true;
                return false;
            }

            var sc = (SelectColorRequest)ctx.PendingChoice;
            ctx.PendingChoice = null;
            if (sc.ChosenColor is not CardColor col) { ctx.HandlerState = null; return false; }

            var stack = target.Stack(col);
            ctx.PendingChoice = new SelectStackOrderRequest
            {
                Prompt = $"Publications: set the new top-to-bottom order of your {col} pile.",
                PlayerIndex = target.Index,
                Color = col,
                CurrentOrder = stack.Cards.ToArray(),
            };
            ctx.HandlerState = Stage.PickOrder;
            ctx.Paused = true;
            return true;
        }

        // Stage.PickOrder
        var req = (SelectStackOrderRequest)ctx.PendingChoice!;
        ctx.PendingChoice = null;
        ctx.HandlerState = null;

        var stack2 = target.Stack(req.Color);
        // Validate permutation; any mismatch means leave unchanged.
        var original = new HashSet<int>(stack2.Cards);
        if (req.ChosenOrder.Count == stack2.Count && req.ChosenOrder.All(original.Contains)
            && req.ChosenOrder.Distinct().Count() == req.ChosenOrder.Count)
        {
            if (!req.ChosenOrder.SequenceEqual(stack2.Cards))
            {
                stack2.ReplaceOrder(req.ChosenOrder);
                GameLog.Log($"{GameLog.P(target)} rearranges {req.Color} pile");
            }
        }
        return true;
    }
}
