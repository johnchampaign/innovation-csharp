namespace Innovation.Core.Handlers;

/// <summary>
/// Clothing first effect (age 1, Green/Crown, non-demand): "Meld a card
/// from your hand of different color from any card on your board."
///
/// Mirrors VB6 main.frm 4319–4336 (AI path) and 8147–8152 (human phase).
/// Mandatory if any hand card has a board-color-absent match; otherwise
/// a no-op. "Different color from any card on your board" reduces to
/// "color pile is empty" — because a pile is all one color, if pile X has
/// any card then color X is "present on the board".
/// </summary>
public sealed class ClothingMeldDifferentColorHandler : IDogmaHandler
{
    public bool Execute(GameState g, PlayerState target, DogmaContext ctx)
    {
        if (ctx.PendingChoice is null)
        {
            // Eligible = hand cards whose color pile on own board is empty.
            var eligible = target.Hand
                .Where(id => target.Stack(g.Cards[id].Color).IsEmpty)
                .ToArray();
            if (eligible.Length == 0) return false;

            ctx.PendingChoice = new SelectHandCardRequest
            {
                Prompt = "Clothing: meld a card from your hand whose color "
                       + "isn't already on your board.",
                PlayerIndex = target.Index,
                EligibleCardIds = eligible,
                AllowNone = false,
            };
            ctx.Paused = true;
            return false;
        }

        var req = (SelectHandCardRequest)ctx.PendingChoice;
        ctx.PendingChoice = null;

        if (req.ChosenCardId is not int cardId) return false;   // shouldn't happen

        Mechanics.Meld(g, target, cardId);
        return true;
    }
}
