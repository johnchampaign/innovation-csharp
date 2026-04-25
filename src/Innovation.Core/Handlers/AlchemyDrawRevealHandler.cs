namespace Innovation.Core.Handlers;

/// <summary>
/// Alchemy (age 3, Blue/Castle) — effect 1: "Draw and reveal a 4 for
/// every three [Castle] icons on your board. If any of the drawn cards
/// are red, return the cards drawn and all cards in your hand.
/// Otherwise, keep them."
///
/// Two-stage flow that mirrors VB6's interactive return sequence:
///   1. Draw the N age-4 cards into the hand so the player can see the
///      reveal. If none are red, handler returns (keep them).
///   2. Otherwise prompt the player to return one hand card at a time
///      until the hand is empty — each click returns a single card.
/// AI controllers pick any hand card per prompt and end up draining the
/// hand the same way; the staged flow only changes the user experience.
/// </summary>
public sealed class AlchemyDrawRevealHandler : IDogmaHandler
{
    private enum Stage { Draw, ReturnOne }

    public bool Execute(GameState g, PlayerState target, DogmaContext ctx)
    {
        var stage = (Stage?)ctx.HandlerState ?? Stage.Draw;

        if (stage == Stage.Draw)
        {
            int castles = IconCounter.Count(target, Icon.Castle, g.Cards);
            int n = castles / 3;
            if (n == 0) return false;

            bool anyRed = false;
            for (int i = 0; i < n; i++)
            {
                int drawn = Mechanics.DrawFromAge(g, target, 4);
                if (drawn < 0) return true;   // game ended
                if (g.Cards[drawn].Color == CardColor.Red) anyRed = true;
            }

            if (!anyRed) return true;     // keep them

            return PromptReturnNext(target, ctx);
        }

        // Stage.ReturnOne — consume the single-card pick, return it, then
        // either pause for the next card or finish.
        var req = (SelectHandCardRequest)ctx.PendingChoice!;
        ctx.PendingChoice = null;
        if (req.ChosenCardId is int id)
            Mechanics.Return(g, target, id);

        return PromptReturnNext(target, ctx);
    }

    private static bool PromptReturnNext(PlayerState target, DogmaContext ctx)
    {
        if (target.Hand.Count == 0)
        {
            ctx.HandlerState = null;
            return true;
        }

        ctx.PendingChoice = new SelectHandCardRequest
        {
            Prompt = "Alchemy: a red card was drawn — return every card in your hand, one at a time.",
            PlayerIndex = target.Index,
            EligibleCardIds = target.Hand.ToArray(),
            AllowNone = false,
        };
        ctx.HandlerState = Stage.ReturnOne;
        ctx.Paused = true;
        return false;
    }
}
