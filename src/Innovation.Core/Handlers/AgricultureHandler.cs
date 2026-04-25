namespace Innovation.Core.Handlers;

/// <summary>
/// Agriculture (age 1, Yellow/Leaf): "You may return a card from your hand.
/// If you do, draw and score a card of value one higher than the card you
/// returned."
///
/// Mirrors VB6 main.frm 4269–4282. The VB6 path has two branches — AI
/// unconditionally returns the HIGHEST card (hand index n-1), while the
/// human sees a prompt and chooses any card (or cancels). We collapse both
/// into a single <see cref="SelectHandCardRequest"/> with
/// <see cref="SelectHandCardRequest.AllowNone"/>=true; the UI and the AI
/// resolver both plug into the same choice. Empty hand short-circuits with
/// no progress, matching the VB6 <c>If hand(player, 0) &gt; -1</c> guard.
/// </summary>
public sealed class AgricultureHandler : IDogmaHandler
{
    public bool Execute(GameState g, PlayerState target, DogmaContext ctx)
    {
        // Empty hand: nothing to do. VB6 would silently skip the effect.
        if (target.Hand.Count == 0) return false;

        // First entry: raise the choice and pause. Engine returns control
        // to the caller; when the caller fills in ChosenCardId and resumes
        // we re-enter with PendingChoice still populated.
        if (ctx.PendingChoice is null)
        {
            ctx.PendingChoice = new SelectHandCardRequest
            {
                Prompt = "Agriculture: return a card from your hand? "
                       + "(Skip to do nothing; otherwise you'll draw-and-score at age+1.)",
                PlayerIndex = target.Index,
                EligibleCardIds = target.Hand.ToArray(),
                AllowNone = true,
            };
            ctx.Paused = true;
            return false;
        }

        // Resumed: apply the answer.
        var req = (SelectHandCardRequest)ctx.PendingChoice;
        ctx.PendingChoice = null;

        if (req.ChosenCardId is not int chosen)
            return false;  // player declined — no progress, no shared bonus

        int returnedAge = g.Cards[chosen].Age;
        Mechanics.Return(g, target, chosen);

        // Draw-and-score a card one age higher. Draw off the top of
        // (returnedAge + 1) — walking up if that deck is empty — then score
        // the drawn card. If age+1 > 10 and no higher deck has cards, the
        // game ends cleanly (Mechanics.DrawFromAge returns -1).
        int drawn = Mechanics.DrawFromAge(g, target, returnedAge + 1);
        if (drawn < 0 || g.IsGameOver) return true;

        Mechanics.Score(g, target, drawn);
        return true;
    }
}
