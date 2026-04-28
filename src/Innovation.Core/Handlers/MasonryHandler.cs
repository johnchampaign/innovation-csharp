namespace Innovation.Core.Handlers;

/// <summary>
/// Masonry (age 1, Yellow/Castle): "You may meld any number of cards from
/// your hand, each with a [Castle]. If you melded four or more cards, claim
/// the Monument achievement."
///
/// Mirrors VB6 main.frm 4407–4434. Two-stage prompt:
///   1. Subset pick — which castle-bearing cards to meld.
///   2. Order pick — when more than one was picked, the player chooses the
///      meld order (last melded ends up on top of its color pile).
/// </summary>
public sealed class MasonryHandler : IDogmaHandler
{
    private enum Stage { Subset, Order }

    private sealed class State
    {
        public Stage Stage;
        public int[] PickedIds = Array.Empty<int>();
    }

    public bool Execute(GameState g, PlayerState target, DogmaContext ctx)
    {
        var state = (State?)ctx.HandlerState;

        if (state is null)
        {
            var eligible = target.Hand.Where(id => HasCastle(g.Cards[id])).ToArray();
            if (eligible.Length == 0) return false;

            ctx.PendingChoice = new SelectHandCardSubsetRequest
            {
                Prompt = "Masonry: meld any number of castle-bearing cards. "
                       + "Melding four or more claims Monument.",
                PlayerIndex = target.Index,
                EligibleCardIds = eligible,
                MinCount = 0,
                MaxCount = eligible.Length,
            };
            ctx.HandlerState = new State { Stage = Stage.Subset };
            ctx.Paused = true;
            return false;
        }

        if (state.Stage == Stage.Subset)
        {
            var req = (SelectHandCardSubsetRequest)ctx.PendingChoice!;
            ctx.PendingChoice = null;

            var picks = req.ChosenCardIds.ToArray();
            if (picks.Length == 0) { ctx.HandlerState = null; return false; }

            // Skip the order prompt when every meld lands on a different
            // color stack — orderings are equivalent in that case.
            if (picks.Length == 1 || !Mechanics.OrderMatters(picks, id => g.Cards[id].Color))
            {
                foreach (var id in picks)
                {
                    Mechanics.Meld(g, target, id);
                    if (g.IsGameOver) { ctx.HandlerState = null; return true; }
                }
                if (picks.Length >= 4)
                    AchievementRules.ClaimSpecial(g, target, SpecialAchievements.Monument);
                ctx.HandlerState = null;
                return true;
            }

            state.PickedIds = picks;
            state.Stage = Stage.Order;
            ctx.PendingChoice = new SelectCardOrderRequest
            {
                Prompt = "Masonry: choose the meld order (last melded ends up on top).",
                PlayerIndex = target.Index,
                Action = "meld",
                CardIds = picks,
            };
            ctx.Paused = true;
            return false;
        }

        // Stage.Order — apply the chosen order, then check Monument.
        var orderReq = (SelectCardOrderRequest)ctx.PendingChoice!;
        ctx.PendingChoice = null;
        ctx.HandlerState = null;

        var ordered = Mechanics.ValidateOrder(orderReq.ChosenOrder, state.PickedIds);
        // ChosenOrder is the final pile arrangement (top-first). Apply melds
        // in reverse so the first listed ends up on top.
        for (int i = ordered.Count - 1; i >= 0; i--)
        {
            Mechanics.Meld(g, target, ordered[i]);
            if (g.IsGameOver) return true;
        }

        if (ordered.Count >= 4)
            AchievementRules.ClaimSpecial(g, target, SpecialAchievements.Monument);

        return true;
    }

    private static bool HasCastle(Card c) =>
        c.Top == Icon.Castle || c.Left == Icon.Castle ||
        c.Middle == Icon.Castle || c.Right == Icon.Castle;
}
