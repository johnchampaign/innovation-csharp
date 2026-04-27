namespace Innovation.Core.Handlers;

/// <summary>
/// Industrialization (age 6, Red/Factory) — effect 1: "Draw and tuck
/// a 6 for every two [Factory] icons on your board."
///
/// Per the rulebook, draws happen first as a batch, and the player gets
/// to choose the tuck order. Implemented as: (1) draw N age-6 cards into
/// the player's hand, (2) ask the player for the tuck order, (3) tuck
/// each in the chosen order.
///
/// Factory count is read from the frozen activation-time snapshot per
/// the icons-don't-update-during-dogma rule.
/// </summary>
public sealed class IndustrializationTuckHandler : IDogmaHandler
{
    public bool Execute(GameState g, PlayerState target, DogmaContext ctx)
    {
        // Stage A: first entry — draw N cards, then ask for tuck order.
        if (ctx.HandlerState is null && ctx.PendingChoice is null)
        {
            int factories = ctx.FrozenIconCounts is { } frozen
                ? frozen[target.Index]
                : IconCounter.Count(target, Icon.Factory, g.Cards);
            int n = factories / 2;
            if (n == 0) return false;

            var drawn = new List<int>();
            for (int i = 0; i < n; i++)
            {
                int id = Mechanics.DrawFromAge(g, target, 6);
                if (id < 0)
                {
                    // Deck cascade exhausted; game may have ended. Tuck
                    // whatever we already drew (input order — no choice
                    // when only one will tuck cleanly anyway).
                    foreach (var d in drawn)
                    {
                        Mechanics.Tuck(g, target, d);
                        if (g.IsGameOver) return true;
                    }
                    return drawn.Count > 0;
                }
                drawn.Add(id);
                if (g.IsGameOver) break;
            }

            if (drawn.Count == 0) return false;
            if (drawn.Count == 1)
            {
                Mechanics.Tuck(g, target, drawn[0]);
                return true;
            }

            ctx.HandlerState = drawn.ToArray();
            ctx.PendingChoice = new SelectCardOrderRequest
            {
                Prompt = "Industrialization: choose the tuck order for the 6s you drew "
                       + "(last tucked goes to the bottom of its color pile).",
                PlayerIndex = target.Index,
                Action = "tuck",
                CardIds = drawn.ToArray(),
            };
            ctx.Paused = true;
            return false;
        }

        // Stage B: order resolved — apply tucks.
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
