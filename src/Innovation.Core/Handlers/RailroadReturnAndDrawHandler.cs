namespace Innovation.Core.Handlers;

/// <summary>
/// Railroad effect 1 (age 7, Purple/Clock): "Return all cards from your
/// hand, then draw three 6s." All hand cards get returned, but the player
/// chooses the order so deck-bottom layout is theirs.
/// </summary>
public sealed class RailroadReturnAndDrawHandler : IDogmaHandler
{
    public bool Execute(GameState g, PlayerState target, DogmaContext ctx)
    {
        if (ctx.PendingChoice is null && ctx.HandlerState is null)
        {
            var hand = target.Hand.ToArray();
            if (hand.Length == 0)
            {
                DrawThreeSixes(g, target);
                return true;
            }
            if (hand.Length == 1 || !Mechanics.OrderMatters(hand, id => g.Cards[id].Age))
            {
                foreach (var id in hand) Mechanics.Return(g, target, id);
                if (!g.IsGameOver) DrawThreeSixes(g, target);
                return true;
            }
            ctx.HandlerState = hand;
            ctx.PendingChoice = new SelectCardOrderRequest
            {
                Prompt = "Railroad: choose the return order for your entire hand.",
                PlayerIndex = target.Index,
                Action = "return",
                CardIds = hand,
            };
            ctx.Paused = true;
            return false;
        }

        var orderReq = (SelectCardOrderRequest)ctx.PendingChoice!;
        var input = (int[])ctx.HandlerState!;
        ctx.PendingChoice = null;
        ctx.HandlerState = null;
        var ordered = Mechanics.ValidateOrder(orderReq.ChosenOrder, input);
        // Reverse: chosen order is deck-arrangement top-first.
        for (int i = ordered.Count - 1; i >= 0; i--)
        {
            Mechanics.Return(g, target, ordered[i]);
            if (g.IsGameOver) return true;
        }
        DrawThreeSixes(g, target);
        return true;
    }

    private static void DrawThreeSixes(GameState g, PlayerState target)
    {
        for (int i = 0; i < 3; i++)
        {
            if (g.IsGameOver) return;
            Mechanics.DrawFromAge(g, target, 6);
        }
    }
}
