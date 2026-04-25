namespace Innovation.Core.Handlers;

/// <summary>
/// Medicine (age 3, Yellow/Leaf) — demand: "I demand you exchange the
/// highest card in your score pile with the lowest card in my score pile!"
///
/// Ties are resolved by whichever player is giving up the card (defender
/// for their highest, activator for their lowest). Single-candidate sides
/// transfer without prompting. Each side resolves independently — if one
/// pile is empty, only the other direction happens.
/// </summary>
public sealed class MedicineDemandHandler : IDogmaHandler
{
    private enum Stage { DefenderPick, ActivatorPick, Done }

    public bool Execute(GameState g, PlayerState target, DogmaContext ctx)
    {
        var stage = (Stage?)ctx.HandlerState ?? Stage.DefenderPick;
        var activator = g.Players[ctx.ActivatingPlayerIndex];

        if (stage == Stage.DefenderPick)
        {
            if (ctx.PendingChoice is null)
            {
                if (target.ScorePile.Count == 0)
                {
                    ctx.HandlerState = Stage.ActivatorPick;
                    return PromptActivator(g, target, activator, ctx);
                }

                int hi = target.ScorePile.Max(id => g.Cards[id].Age);
                var tied = target.ScorePile.Where(id => g.Cards[id].Age == hi).ToList();

                if (tied.Count == 1)
                {
                    Mechanics.TransferScoreToScore(g, target, activator, tied[0]);
                    ctx.DemandSuccessful = true;
                    ctx.HandlerState = Stage.ActivatorPick;
                    return PromptActivator(g, target, activator, ctx);
                }

                ctx.PendingChoice = new SelectScoreCardRequest
                {
                    Prompt = $"Medicine: choose which of your age-{hi} cards to give up.",
                    PlayerIndex = target.Index,
                    EligibleCardIds = tied.ToArray(),
                    AllowNone = false,
                };
                ctx.HandlerState = Stage.DefenderPick;
                ctx.Paused = true;
                return false;
            }

            var req = (SelectScoreCardRequest)ctx.PendingChoice;
            ctx.PendingChoice = null;
            if (req.ChosenCardId is int id)
            {
                Mechanics.TransferScoreToScore(g, target, activator, id);
                ctx.DemandSuccessful = true;
            }
            ctx.HandlerState = Stage.ActivatorPick;
            return PromptActivator(g, target, activator, ctx);
        }

        // Stage.ActivatorPick
        {
            var req = (SelectScoreCardRequest)ctx.PendingChoice!;
            ctx.PendingChoice = null;
            ctx.HandlerState = null;
            if (req.ChosenCardId is int id)
                Mechanics.TransferScoreToScore(g, activator, target, id);
            return true;
        }
    }

    private static bool PromptActivator(GameState g, PlayerState target, PlayerState activator, DogmaContext ctx)
    {
        if (activator.ScorePile.Count == 0) { ctx.HandlerState = null; return true; }

        int lo = activator.ScorePile.Min(id => g.Cards[id].Age);
        var tied = activator.ScorePile.Where(id => g.Cards[id].Age == lo).ToList();

        if (tied.Count == 1)
        {
            Mechanics.TransferScoreToScore(g, activator, target, tied[0]);
            ctx.HandlerState = null;
            return true;
        }

        ctx.PendingChoice = new SelectScoreCardRequest
        {
            Prompt = $"Medicine: choose which of your age-{lo} cards to send.",
            PlayerIndex = activator.Index,
            EligibleCardIds = tied.ToArray(),
            AllowNone = false,
        };
        ctx.Paused = true;
        return true;
    }
}
