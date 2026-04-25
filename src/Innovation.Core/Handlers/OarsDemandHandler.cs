namespace Innovation.Core.Handlers;

/// <summary>
/// Oars first effect (age 1, Red/Castle, <b>demand</b>): "I demand you
/// transfer a card with a [Crown] from your hand to my score pile! If you
/// do, draw a 1."
///
/// Mirrors VB6 main.frm 4471–4495 (AI path) and 8171–8179 (human phase).
/// Target picks any Crown card in their hand (VB6 AI auto-picks the
/// lowest-age one; we always raise the choice so the caller decides).
/// After the transfer, the <em>target</em> draws a 1 — not the activator.
///
/// Sets <see cref="DogmaContext.DemandSuccessful"/> when a transfer
/// happens, so <see cref="OarsDrawIfNoDemandHandler"/> (effect 2) can
/// decide whether to fire.
/// </summary>
public sealed class OarsDemandHandler : IDogmaHandler
{
    public bool Execute(GameState g, PlayerState target, DogmaContext ctx)
    {
        // Cold entry: find Crown-bearing cards in the target's hand.
        if (ctx.HandlerState is null && ctx.PendingChoice is null)
        {
            var eligible = target.Hand
                .Where(id => HasCrown(g.Cards[id]))
                .ToArray();
            if (eligible.Length == 0) return false;   // nothing to transfer

            ctx.HandlerState = new object();   // sentinel: "resume"
            ctx.PendingChoice = new SelectHandCardRequest
            {
                Prompt = $"Oars: transfer a [Crown] card from your hand to "
                       + $"player {ctx.ActivatingPlayerIndex + 1}'s score pile.",
                PlayerIndex = target.Index,
                EligibleCardIds = eligible,
                AllowNone = false,
            };
            ctx.Paused = true;
            return false;
        }

        // Resume: perform the transfer, then the target draws a 1.
        var req = (SelectHandCardRequest)ctx.PendingChoice!;
        ctx.PendingChoice = null;
        ctx.HandlerState = null;

        if (req.ChosenCardId is not int cardId) return false;   // shouldn't happen

        var activator = g.Players[ctx.ActivatingPlayerIndex];
        Mechanics.TransferHandToScore(g, target, activator, cardId);
        ctx.DemandSuccessful = true;
        if (g.IsGameOver) return true;

        Mechanics.DrawFromAge(g, target, 1);
        return true;
    }

    private static bool HasCrown(Card c) =>
        c.Top == Icon.Crown || c.Left == Icon.Crown ||
        c.Middle == Icon.Crown || c.Right == Icon.Crown;
}
