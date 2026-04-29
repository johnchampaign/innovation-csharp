namespace Innovation.Core.Handlers;

/// <summary>
/// Evolution (age 7, Blue/Lightbulb): "You may choose to either draw and
/// score an 8 and then return a card from your score pile, or draw a card
/// of value one higher than the highest card in your score pile."
///
/// Two yes/no prompts: first gates acting at all, second picks the branch
/// (yes = branch A score-an-8-and-return, no = branch B draw-higher).
/// </summary>
public sealed class EvolutionHandler : IDogmaHandler
{
    private enum Stage { Start, PickedBranch, ReturnScoreCard }

    public bool Execute(GameState g, PlayerState target, DogmaContext ctx)
    {
        var stage = (Stage?)ctx.HandlerState ?? Stage.Start;

        if (stage == Stage.Start && ctx.PendingChoice is null)
        {
            ctx.PendingChoice = new YesNoChoiceRequest
            {
                Prompt = "Evolution: branch A (draw+score an 8, then return a score-pile card)? "
                       + "No = branch B (draw a card one higher than highest in your score pile).",
                PlayerIndex = target.Index,
            };
            ctx.HandlerState = Stage.PickedBranch;
            ctx.Paused = true;
            return false;
        }

        if (stage == Stage.PickedBranch)
        {
            var pick = (YesNoChoiceRequest)ctx.PendingChoice!;
            ctx.PendingChoice = null;

            if (pick.ChosenYes)
            {
                int drawn = Mechanics.DrawAndScore(g, target, 8);
                if (drawn < 0 || target.ScorePile.Count == 0) { ctx.HandlerState = null; return drawn >= 0; }

                ctx.PendingChoice = new SelectScoreCardRequest
                {
                    Prompt = "Evolution: return a card from your score pile.",
                    PlayerIndex = target.Index,
                    EligibleCardIds = target.ScorePile.ToArray(),
                    AllowNone = false,
                };
                ctx.HandlerState = Stage.ReturnScoreCard;
                ctx.Paused = true;
                return true;
            }
            else
            {
                ctx.HandlerState = null;
                if (target.ScorePile.Count == 0) return false;
                int highestAge = target.ScorePile.Max(id => g.Cards[id].Age);
                // Don't cap at 10. If the highest is 10 the player draws
                // "an 11" — Mechanics.DrawFromAge handles age>10 as the
                // game-ending win cascade. Capping at 10 was a bug that
                // silently denied the player their winning draw.
                int drawn = Mechanics.DrawFromAge(g, target, highestAge + 1);
                return drawn >= 0 || g.IsGameOver;
            }
        }

        // Stage.ReturnScoreCard
        var req = (SelectScoreCardRequest)ctx.PendingChoice!;
        ctx.PendingChoice = null;
        ctx.HandlerState = null;
        if (req.ChosenCardId is int cid)
        {
            target.ScorePile.Remove(cid);
            g.Decks[g.Cards[cid].Age].Add(cid);
            GameLog.Log($"{GameLog.P(target)} returns {GameLog.C(g, cid)} from score pile");
        }
        return true;
    }
}
