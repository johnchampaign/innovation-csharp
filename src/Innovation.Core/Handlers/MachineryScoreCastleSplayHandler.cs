namespace Innovation.Core.Handlers;

/// <summary>
/// Machinery (age 3, Yellow/Leaf) — non-demand: "Score a card from your
/// hand with a [Castle]. You may splay your red cards left."
///
/// The score is mandatory when the hand has a castle-iconed card; the
/// splay is optional. If no castle card is in hand, the score step is
/// skipped silently and we still fall through to the splay offer.
///
/// Returns true (progressed → activator's share-bonus fires) iff at
/// least one of (scored a castle, splayed red) actually happened. A
/// sharer who has no castle and declines or can't splay reports no-op
/// so the bonus draw doesn't fire spuriously.
/// </summary>
public sealed class MachineryScoreCastleSplayHandler : IDogmaHandler
{
    private sealed class State
    {
        public string Phase = "score";   // "score" or "splay"
        public bool Scored;
    }

    public bool Execute(GameState g, PlayerState target, DogmaContext ctx)
    {
        var state = (State?)ctx.HandlerState;

        // Cold entry — phase 1 (score).
        if (state is null && ctx.PendingChoice is null)
        {
            state = new State();

            var eligible = target.Hand
                .Where(id => FeudalismDemandHandler.HasIcon(g.Cards[id], Icon.Castle))
                .ToArray();
            if (eligible.Length == 0)
            {
                // No castle to score; jump straight to splay offer.
                return OfferSplayOrFinish(g, target, ctx, state);
            }

            state.Phase = "score";
            ctx.HandlerState = state;
            ctx.PendingChoice = new SelectHandCardRequest
            {
                Prompt = "Machinery: score a [Castle] card from your hand.",
                PlayerIndex = target.Index,
                EligibleCardIds = eligible,
                AllowNone = false,
            };
            ctx.Paused = true;
            return false;
        }

        // Phase 1 resume.
        if (state!.Phase == "score")
        {
            var req = (SelectHandCardRequest)ctx.PendingChoice!;
            ctx.PendingChoice = null;
            if (req.ChosenCardId is int id)
            {
                Mechanics.Score(g, target, id);
                state.Scored = true;
            }
            if (g.IsGameOver) { ctx.HandlerState = null; return state.Scored; }
            return OfferSplayOrFinish(g, target, ctx, state);
        }

        // Phase 2 resume — splay yes/no.
        var yn = (YesNoChoiceRequest)ctx.PendingChoice!;
        ctx.PendingChoice = null;
        ctx.HandlerState = null;

        if (!yn.ChosenYes)
        {
            // Declined splay. Progressed iff we scored in phase 1.
            return state.Scored;
        }
        bool splayed = Mechanics.Splay(g, target, CardColor.Red, Splay.Left);
        return state.Scored || splayed;
    }

    /// <summary>
    /// Offers the splay yes/no when eligible. When the red pile can't be
    /// re-splayed (size &lt; 2 or already Left), we finish here and the
    /// return value reflects whether scoring already happened.
    /// </summary>
    private static bool OfferSplayOrFinish(GameState g, PlayerState target, DogmaContext ctx, State state)
    {
        var s = target.Stack(CardColor.Red);
        if (s.Count < 2 || s.Splay == Splay.Left)
        {
            ctx.HandlerState = null;
            return state.Scored;
        }

        state.Phase = "splay";
        ctx.HandlerState = state;
        ctx.PendingChoice = new YesNoChoiceRequest
        {
            Prompt = "Machinery: splay your red cards left?",
            PlayerIndex = target.Index,
        };
        ctx.Paused = true;
        // While paused, this return value is ignored by the engine; the
        // final phase-2 resume reports the actual progress.
        return false;
    }
}
