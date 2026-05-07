namespace Innovation.Core.Handlers;

/// <summary>
/// Physics (age 5, Blue/Lightbulb): "Draw three 6 and reveal them. If
/// two or more of the drawn cards are of the same color, return the
/// drawn cards and all the cards in your hand. Otherwise, keep them."
///
/// When the rebound triggers (2+ same-color among drawn) we return all
/// hand cards (which includes the just-drawn three, since they were
/// added to hand by the draws) to their decks. Multiple cards of the
/// same age go to the same deck, so the player picks the return order.
/// </summary>
public sealed class PhysicsHandler : IDogmaHandler
{
    public bool Execute(GameState g, PlayerState target, DogmaContext ctx)
    {
        // Stage A: draw three 6s, decide if the rebound triggers.
        if (ctx.HandlerState is null && ctx.PendingChoice is null)
        {
            var drawn = new List<int>(3);
            for (int i = 0; i < 3; i++)
            {
                int id = Mechanics.DrawFromAge(g, target, 6);
                if (id < 0 || g.IsGameOver) return true;
                drawn.Add(id);
            }

            var seen = new HashSet<CardColor>();
            bool anyDup = false;
            foreach (var id in drawn)
            {
                if (!seen.Add(g.Cards[id].Color)) { anyDup = true; break; }
            }
            if (!anyDup) return true;   // keep them

            // Rebound: return all hand cards (which includes the three
            // just drawn). Order matters when 2+ share an age.
            var hand = target.Hand.ToArray();
            if (hand.Length == 0) return true;
            if (hand.Length == 1 || !Mechanics.OrderMatters(hand, id => g.Cards[id].Age))
            {
                foreach (var id in hand) Mechanics.Return(g, target, id);
                return true;
            }

            ctx.HandlerState = hand;
            ctx.PendingChoice = new SelectCardOrderRequest
            {
                Prompt = "Physics: two of the three 6s match. Choose the return order for your entire hand (including the drawn cards).",
                PlayerIndex = target.Index,
                Action = "return",
                CardIds = hand,
            };
            ctx.Paused = true;
            return false;
        }

        // Stage B: order resolved.
        var orderReq = (SelectCardOrderRequest)ctx.PendingChoice!;
        var input = (int[])ctx.HandlerState!;
        ctx.PendingChoice = null;
        ctx.HandlerState = null;
        var ordered = Mechanics.ValidateOrder(orderReq.ChosenOrder, input);
        for (int i = ordered.Count - 1; i >= 0; i--)
            Mechanics.Return(g, target, ordered[i]);
        return true;
    }
}
