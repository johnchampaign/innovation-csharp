namespace Innovation.Core.Handlers;

/// <summary>
/// Sanitation (age 7, Yellow/Leaf, demand): "I demand you exchange the
/// two highest cards in your hand with the lowest card in my hand!"
///
/// Defender transfers their two highest hand cards to the activator; the
/// activator transfers their single lowest hand card to the defender.
/// Ties broken by whichever player is giving up the card.
/// </summary>
public sealed class SanitationDemandHandler : IDogmaHandler
{
    private enum Stage { DefenderForced, DefenderTied, ActivatorTied, Done }

    // Snapshot carried across pauses: which stage we're on + the activator's
    // hand as it stood BEFORE any transfer (so the "lowest card in my hand"
    // pick ignores cards just received from the defender — exchanges are
    // simultaneous).
    private sealed class State
    {
        public Stage Stage;
        public int[] ActivatorOriginalHand = Array.Empty<int>();
    }

    public bool Execute(GameState g, PlayerState target, DogmaContext ctx)
    {
        var state = (State?)ctx.HandlerState ?? new State { Stage = Stage.DefenderForced };
        ctx.HandlerState = state;
        var activator = g.Players[ctx.ActivatingPlayerIndex];

        if (state.Stage == Stage.DefenderForced)
        {
            state.ActivatorOriginalHand = activator.Hand.ToArray();

            int take = Math.Min(2, target.Hand.Count);
            if (take == 0)
            {
                state.Stage = Stage.ActivatorTied;
                return PromptActivator(g, target, activator, ctx, state);
            }

            var byAge = target.Hand.OrderByDescending(id => g.Cards[id].Age).ToList();
            int thresholdAge = g.Cards[byAge[take - 1]].Age;
            var forced = byAge.Where(id => g.Cards[id].Age > thresholdAge).ToList();
            var tied = byAge.Where(id => g.Cards[id].Age == thresholdAge).ToList();
            int tieSlots = take - forced.Count;

            foreach (var id in forced) Mechanics.TransferHandToHand(g, target, activator, id);
            if (forced.Count > 0 || tied.Count > 0) ctx.DemandSuccessful = true;

            if (tied.Count == tieSlots)
            {
                foreach (var id in tied) Mechanics.TransferHandToHand(g, target, activator, id);
                state.Stage = Stage.ActivatorTied;
                return PromptActivator(g, target, activator, ctx, state);
            }

            ctx.PendingChoice = new SelectHandCardSubsetRequest
            {
                Prompt = $"Sanitation: choose {tieSlots} of your age-{thresholdAge} cards to send.",
                PlayerIndex = target.Index,
                EligibleCardIds = tied.ToArray(),
                MinCount = tieSlots,
                MaxCount = tieSlots,
            };
            state.Stage = Stage.DefenderTied;
            ctx.Paused = true;
            return false;
        }

        if (state.Stage == Stage.DefenderTied)
        {
            var req = (SelectHandCardSubsetRequest)ctx.PendingChoice!;
            ctx.PendingChoice = null;
            foreach (var id in req.ChosenCardIds)
                Mechanics.TransferHandToHand(g, target, activator, id);
            state.Stage = Stage.ActivatorTied;
            return PromptActivator(g, target, activator, ctx, state);
        }

        // Stage.ActivatorTied — resolving the activator's hand-card pick.
        {
            var req = (SelectHandCardRequest)ctx.PendingChoice!;
            ctx.PendingChoice = null;
            ctx.HandlerState = null;
            if (req.ChosenCardId is int id)
                Mechanics.TransferHandToHand(g, activator, target, id);
            return true;
        }
    }

    private static bool PromptActivator(GameState g, PlayerState target, PlayerState activator, DogmaContext ctx, State state)
    {
        // "Lowest card in my hand" means the activator's hand as it stood
        // before the exchange — cards just received from the defender don't
        // count. Intersect with the current hand in case some were removed
        // by other effects (shouldn't happen, but defensive).
        var eligible = state.ActivatorOriginalHand
            .Where(activator.Hand.Contains)
            .ToList();
        if (eligible.Count == 0) { ctx.HandlerState = null; return true; }

        int lowestAge = eligible.Min(id => g.Cards[id].Age);
        var tied = eligible.Where(id => g.Cards[id].Age == lowestAge).ToList();

        if (tied.Count == 1)
        {
            Mechanics.TransferHandToHand(g, activator, target, tied[0]);
            ctx.HandlerState = null;
            return true;
        }

        ctx.PendingChoice = new SelectHandCardRequest
        {
            Prompt = $"Sanitation: choose which of your age-{lowestAge} cards to send.",
            PlayerIndex = activator.Index,
            EligibleCardIds = tied.ToArray(),
            AllowNone = false,
        };
        ctx.Paused = true;
        return true;
    }
}
