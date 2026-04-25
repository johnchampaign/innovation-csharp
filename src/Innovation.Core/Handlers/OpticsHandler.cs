namespace Innovation.Core.Handlers;

/// <summary>
/// Optics (age 3, Red/Crown): "Draw and meld a 3. If it has a [Crown],
/// draw and score a 4. Otherwise, transfer a card from your score pile
/// to the score pile of an opponent with fewer points than you."
///
/// Branch A: meld has a Crown → draw-and-score a 4.
/// Branch B: no Crown → if any opponent has strictly fewer points, the
/// target picks which score-pile card to transfer and which opponent
/// receives it. We simplify multi-opponent by auto-picking the lowest-
/// score opponent (tiebreak: next seat in turn order). Still ask the
/// target which of their score cards to give. If no such opponent
/// exists, the "otherwise" branch is a no-op.
/// </summary>
public sealed class OpticsHandler : IDogmaHandler
{
    private sealed class AwaitingScorePick { public int OpponentIndex; }

    public bool Execute(GameState g, PlayerState target, DogmaContext ctx)
    {
        // Phase 1: draw and meld a 3, then branch.
        if (ctx.HandlerState is null && ctx.PendingChoice is null)
        {
            int melded = Mechanics.DrawAndMeld(g, target, 3);
            if (melded < 0 || g.IsGameOver) return true;

            if (FeudalismDemandHandler.HasIcon(g.Cards[melded], Icon.Crown))
            {
                Mechanics.DrawAndScore(g, target, 4);
                return true;
            }

            // Branch B: find a poorer opponent.
            int myScore = target.Score(g.Cards);
            int? bestIdx = null;
            int bestScore = int.MaxValue;
            int n = g.Players.Length;
            for (int i = 1; i < n; i++)
            {
                var p = g.Players[(target.Index + i) % n];
                int s = p.Score(g.Cards);
                if (s >= myScore) continue;
                if (s < bestScore) { bestScore = s; bestIdx = p.Index; }
            }
            if (bestIdx is null) return true;
            if (target.ScorePile.Count == 0) return true;

            ctx.HandlerState = new AwaitingScorePick { OpponentIndex = bestIdx.Value };
            ctx.PendingChoice = new SelectScoreCardRequest
            {
                Prompt = $"Optics: transfer a card from your score pile to "
                       + $"player {bestIdx.Value + 1}'s score pile.",
                PlayerIndex = target.Index,
                EligibleCardIds = target.ScorePile.ToArray(),
                AllowNone = false,
            };
            ctx.Paused = true;
            return true;
        }

        // Phase 2 resume.
        var wait = (AwaitingScorePick)ctx.HandlerState!;
        var req = (SelectScoreCardRequest)ctx.PendingChoice!;
        ctx.PendingChoice = null;
        ctx.HandlerState = null;
        if (req.ChosenCardId is not int cardId) return true;

        var opponent = g.Players[wait.OpponentIndex];
        Mechanics.TransferScoreToScore(g, target, opponent, cardId);
        return true;
    }
}
