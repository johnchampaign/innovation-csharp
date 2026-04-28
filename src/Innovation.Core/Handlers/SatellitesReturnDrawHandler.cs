namespace Innovation.Core.Handlers;

/// <summary>
/// Satellites (age 9, Green/Clock) — effect 1: "Return all cards from your
/// hand, and draw three 8s." Player picks the return order.
/// </summary>
public sealed class SatellitesReturnDrawHandler : IDogmaHandler
{
    public bool Execute(GameState g, PlayerState target, DogmaContext ctx)
    {
        if (ctx.PendingChoice is null && ctx.HandlerState is null)
        {
            var hand = target.Hand.ToArray();
            if (hand.Length == 0)
            {
                DrawThreeEights(g, target);
                return true;
            }
            if (hand.Length == 1 || !Mechanics.OrderMatters(hand, id => g.Cards[id].Age))
            {
                foreach (var id in hand) Mechanics.Return(g, target, id);
                if (!g.IsGameOver) DrawThreeEights(g, target);
                return true;
            }
            ctx.HandlerState = hand;
            ctx.PendingChoice = new SelectCardOrderRequest
            {
                Prompt = "Satellites: choose the return order for your entire hand.",
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
        DrawThreeEights(g, target);
        return true;
    }

    private static void DrawThreeEights(GameState g, PlayerState target)
    {
        for (int i = 0; i < 3; i++)
        {
            if (g.IsGameOver) return;
            Mechanics.DrawFromAge(g, target, 8);
        }
    }
}
