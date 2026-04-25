namespace Innovation.Core.Handlers;

/// <summary>
/// Machinery (age 3, Yellow/Leaf) — non-demand: "Score a card from your
/// hand with a [Castle]. You may splay your red cards left."
///
/// The score is mandatory when the hand has a castle-iconed card; the
/// splay is optional. If no castle card is in hand, the score step is
/// skipped silently and we still fall through to the splay offer.
/// </summary>
public sealed class MachineryScoreCastleSplayHandler : IDogmaHandler
{
    private sealed class AwaitingSplay { }

    public bool Execute(GameState g, PlayerState target, DogmaContext ctx)
    {
        // Phase 1: pick a castle-iconed hand card to score.
        if (ctx.HandlerState is null && ctx.PendingChoice is null)
        {
            var eligible = target.Hand
                .Where(id => FeudalismDemandHandler.HasIcon(g.Cards[id], Icon.Castle))
                .ToArray();
            if (eligible.Length == 0)
            {
                // Skip score step; proceed directly to splay offer.
                return OfferSplay(g, target, ctx);
            }

            ctx.HandlerState = "score";
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
        if (ctx.HandlerState as string == "score")
        {
            var req = (SelectHandCardRequest)ctx.PendingChoice!;
            ctx.PendingChoice = null;
            ctx.HandlerState = null;
            if (req.ChosenCardId is int id)
                Mechanics.Score(g, target, id);
            if (g.IsGameOver) return true;
            return OfferSplay(g, target, ctx);
        }

        // Phase 2 resume: splay yes/no.
        var yn = (YesNoChoiceRequest)ctx.PendingChoice!;
        ctx.PendingChoice = null;
        ctx.HandlerState = null;
        if (!yn.ChosenYes) return true;
        Mechanics.Splay(g, target, CardColor.Red, Splay.Left);
        return true;
    }

    private static bool OfferSplay(GameState g, PlayerState target, DogmaContext ctx)
    {
        var s = target.Stack(CardColor.Red);
        if (s.Count < 2 || s.Splay == Splay.Left) return true;

        ctx.HandlerState = new AwaitingSplay();
        ctx.PendingChoice = new YesNoChoiceRequest
        {
            Prompt = "Machinery: splay your red cards left?",
            PlayerIndex = target.Index,
        };
        ctx.Paused = true;
        return true;
    }
}
