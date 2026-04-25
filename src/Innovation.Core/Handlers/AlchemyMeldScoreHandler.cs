namespace Innovation.Core.Handlers;

/// <summary>
/// Alchemy (age 3, Blue/Castle) — effect 2: "Meld a card from your hand,
/// then score a card from your hand."
///
/// Both picks are mandatory when the hand is non-empty. If the hand is
/// empty at entry, the effect is skipped. If the meld empties the hand,
/// the score step is skipped.
/// </summary>
public sealed class AlchemyMeldScoreHandler : IDogmaHandler
{
    public bool Execute(GameState g, PlayerState target, DogmaContext ctx)
    {
        // Phase 1: meld.
        if (ctx.HandlerState is null && ctx.PendingChoice is null)
        {
            if (target.Hand.Count == 0) return false;
            ctx.HandlerState = "meld";
            ctx.PendingChoice = new SelectHandCardRequest
            {
                Prompt = "Alchemy: meld a card from your hand.",
                PlayerIndex = target.Index,
                EligibleCardIds = target.Hand.ToArray(),
                AllowNone = false,
            };
            ctx.Paused = true;
            return false;
        }

        if (ctx.HandlerState as string == "meld")
        {
            var req = (SelectHandCardRequest)ctx.PendingChoice!;
            ctx.PendingChoice = null;
            ctx.HandlerState = null;
            if (req.ChosenCardId is int mid) Mechanics.Meld(g, target, mid);
            if (g.IsGameOver) return true;
            if (target.Hand.Count == 0) return true;

            ctx.HandlerState = "score";
            ctx.PendingChoice = new SelectHandCardRequest
            {
                Prompt = "Alchemy: score a card from your hand.",
                PlayerIndex = target.Index,
                EligibleCardIds = target.Hand.ToArray(),
                AllowNone = false,
            };
            ctx.Paused = true;
            return true;
        }

        var sreq = (SelectHandCardRequest)ctx.PendingChoice!;
        ctx.PendingChoice = null;
        ctx.HandlerState = null;
        if (sreq.ChosenCardId is int sid) Mechanics.Score(g, target, sid);
        return true;
    }
}
