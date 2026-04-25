namespace Innovation.Core.Handlers;

/// <summary>
/// Code of Laws (age 1, Purple/Crown): "You may tuck a card from your hand
/// of the same color as any card on your board. If you do, you may splay
/// that color of your cards left."
///
/// Mirrors VB6 main.frm 4354–4390. Multi-step: first a card choice, then
/// (only if a card was tucked) a yes/no for splay-left. Uses
/// <see cref="DogmaContext.HandlerState"/> to remember the tucked color
/// across the second pause.
/// </summary>
public sealed class CodeOfLawsHandler : IDogmaHandler
{
    /// <summary>Scratch state stashed on <see cref="DogmaContext.HandlerState"/>.</summary>
    private sealed class State
    {
        public required CardColor TuckedColor { get; init; }
    }

    public bool Execute(GameState g, PlayerState target, DogmaContext ctx)
    {
        // ---- Step 1: choose a card to tuck (or decline) ----
        // Only the cold-entry path checks eligibility; once we've tucked, the
        // hand may legitimately be empty and we still have splay work to do.
        if (ctx.HandlerState is null && ctx.PendingChoice is null)
        {
            if (target.Hand.Count == 0) return false;

            // Cards in hand whose color matches some non-empty pile on the
            // board. VB6 checks `color(board(player, i, 0)) = color(hand(player, j))`
            // — since a pile is all one color, that reduces to "pile of color C
            // is non-empty."
            var eligible = target.Hand
                .Where(id => !target.Stack(g.Cards[id].Color).IsEmpty)
                .ToArray();
            if (eligible.Length == 0) return false;

            ctx.PendingChoice = new SelectHandCardRequest
            {
                Prompt = "Code of Laws: tuck a card whose color matches one of "
                       + "your piles. If you do, you may splay that color left.",
                PlayerIndex = target.Index,
                EligibleCardIds = eligible,
                AllowNone = true,
            };
            ctx.Paused = true;
            return false;
        }

        // Returning from step 1 with a card choice in hand.
        if (ctx.HandlerState is null)
        {
            var pickReq = (SelectHandCardRequest)ctx.PendingChoice!;
            ctx.PendingChoice = null;

            if (pickReq.ChosenCardId is not int cardId)
                return false;   // declined

            var color = g.Cards[cardId].Color;
            Mechanics.Tuck(g, target, cardId);
            if (g.IsGameOver) return true;

            // ---- Step 2: ask about splay-left ----
            ctx.HandlerState = new State { TuckedColor = color };
            ctx.PendingChoice = new YesNoChoiceRequest
            {
                Prompt = $"Splay your {color} cards left?",
                PlayerIndex = target.Index,
            };
            ctx.Paused = true;
            return true;   // the tuck itself counts as progress
        }

        // Returning from step 2.
        var state = (State)ctx.HandlerState;
        var splayReq = (YesNoChoiceRequest)ctx.PendingChoice!;
        ctx.PendingChoice = null;
        ctx.HandlerState = null;

        if (splayReq.ChosenYes)
            Mechanics.Splay(g, target, state.TuckedColor, Splay.Left);

        return true;
    }
}
