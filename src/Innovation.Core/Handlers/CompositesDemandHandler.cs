namespace Innovation.Core.Handlers;

/// <summary>
/// Composites (age 9, Red/Factory) — demand: "I demand you transfer all but
/// one card from your hand to my hand! Also, transfer the highest card from
/// your score pile to my score pile!"
///
/// Target picks which card to keep (if hand has ≥2), and which of their
/// tied-highest score-pile cards to give up (if ties).
/// </summary>
public sealed class CompositesDemandHandler : IDogmaHandler
{
    private enum Stage { HandKeep, ScorePick, Done }

    public bool Execute(GameState g, PlayerState target, DogmaContext ctx)
    {
        var activator = g.Players[ctx.ActivatingPlayerIndex];
        var stage = (Stage?)ctx.HandlerState ?? Stage.HandKeep;

        if (stage == Stage.HandKeep)
        {
            if (target.Hand.Count <= 1)
            {
                // Nothing to prompt — keep the single card (or no cards).
                stage = Stage.ScorePick;
            }
            else
            {
                if (ctx.PendingChoice is null)
                {
                    ctx.PendingChoice = new SelectHandCardRequest
                    {
                        Prompt = "Composites: choose the one card to keep in your hand.",
                        PlayerIndex = target.Index,
                        EligibleCardIds = target.Hand.ToArray(),
                        AllowNone = false,
                    };
                    ctx.HandlerState = Stage.HandKeep;
                    ctx.Paused = true;
                    return false;
                }

                var req = (SelectHandCardRequest)ctx.PendingChoice;
                ctx.PendingChoice = null;
                int keep = req.ChosenCardId ?? target.Hand[0];
                var toTransfer = target.Hand.Where(id => id != keep).ToArray();
                foreach (var id in toTransfer)
                    Mechanics.TransferHandToHand(g, target, activator, id);
                if (toTransfer.Length > 0) ctx.DemandSuccessful = true;
                stage = Stage.ScorePick;
            }
        }

        if (stage == Stage.ScorePick)
        {
            if (target.ScorePile.Count == 0)
            {
                ctx.HandlerState = null;
                return ctx.DemandSuccessful;
            }

            int hi = target.ScorePile.Max(id => g.Cards[id].Age);
            var tied = target.ScorePile.Where(id => g.Cards[id].Age == hi).ToArray();

            if (ctx.PendingChoice is SelectScoreCardRequest prior)
            {
                ctx.PendingChoice = null;
                ctx.HandlerState = null;
                int pick = prior.ChosenCardId ?? tied[0];
                Mechanics.TransferScoreToScore(g, target, activator, pick);
                ctx.DemandSuccessful = true;
                return true;
            }

            if (tied.Length == 1)
            {
                Mechanics.TransferScoreToScore(g, target, activator, tied[0]);
                ctx.DemandSuccessful = true;
                ctx.HandlerState = null;
                return true;
            }

            ctx.PendingChoice = new SelectScoreCardRequest
            {
                Prompt = $"Composites: choose which of your age-{hi} score cards to give up.",
                PlayerIndex = target.Index,
                EligibleCardIds = tied,
                AllowNone = false,
            };
            ctx.HandlerState = Stage.ScorePick;
            ctx.Paused = true;
            return ctx.DemandSuccessful;
        }

        ctx.HandlerState = null;
        return ctx.DemandSuccessful;
    }
}
