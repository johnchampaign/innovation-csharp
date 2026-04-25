namespace Innovation.Core.Handlers;

/// <summary>
/// Archery (age 1, Red/Castle, demand): "I demand you draw a 1, then
/// transfer the highest card in your hand to my hand!"
///
/// Mirrors VB6 main.frm 4283–4293 and phase-handler 8121–8125. Two-step
/// because ties in "highest card" go to the <em>target</em>'s choice
/// (rulebook p.5): we draw the 1 first, then pause for the target to pick
/// one of their tied-highest cards. A single-highest tie still pauses — the
/// caller can auto-advance when <see cref="SelectHandCardRequest.EligibleCardIds"/>
/// has a single entry.
///
/// <see cref="DogmaContext.HandlerState"/> stores a sentinel so the second
/// call knows it's a resume, not cold re-entry (the caller may have cleared
/// <see cref="DogmaContext.PendingChoice"/> before we see it).
/// </summary>
public sealed class ArcheryHandler : IDogmaHandler
{
    public bool Execute(GameState g, PlayerState target, DogmaContext ctx)
    {
        // Cold entry: draw a 1 for the target, then pause to pick a transfer.
        if (ctx.HandlerState is null)
        {
            Mechanics.DrawFromAge(g, target, 1);
            if (g.IsGameOver) return true;

            // No cards to transfer — draw still counts as progress, but the
            // transfer is a no-op. (In practice the just-drawn card would
            // make the hand non-empty, but be defensive in case the deck
            // ran dry mid-search without ending the game.)
            if (target.Hand.Count == 0) return true;

            int maxAge = target.Hand.Max(id => g.Cards[id].Age);
            var eligible = target.Hand
                .Where(id => g.Cards[id].Age == maxAge)
                .ToArray();

            ctx.HandlerState = new object();   // sentinel — "resume step 2"
            ctx.PendingChoice = new SelectHandCardRequest
            {
                Prompt = $"Archery: transfer one of your age-{maxAge} cards to "
                       + $"player {ctx.ActivatingPlayerIndex + 1}'s hand.",
                PlayerIndex = target.Index,
                EligibleCardIds = eligible,
                AllowNone = false,
            };
            ctx.Paused = true;
            return true;   // the draw itself is progress
        }

        // Resume: apply the transfer.
        var req = (SelectHandCardRequest)ctx.PendingChoice!;
        ctx.PendingChoice = null;
        ctx.HandlerState = null;

        if (req.ChosenCardId is int cardId)
        {
            var activator = g.Players[ctx.ActivatingPlayerIndex];
            Mechanics.TransferHandToHand(g, target, activator, cardId);
        }

        return true;
    }
}
