namespace Innovation.Core.Handlers;

/// <summary>
/// Medicine (age 3, Yellow/Leaf) — demand: "I demand you exchange the
/// highest card in your score pile with the lowest card in my score pile!"
///
/// Ties are resolved by whichever player is giving up the card (defender
/// for their highest, activator for their lowest). Single-candidate sides
/// transfer without prompting. Each side resolves independently — if one
/// pile is empty, only the other direction happens.
///
/// Exchanges are simultaneous: the activator's "lowest card in my score
/// pile" is computed from the activator's score pile as it stood BEFORE
/// the defender's card arrived. Otherwise the just-received card pollutes
/// the eligibility set and can be sent right back. Snapshot the
/// activator's pre-transfer pile in <see cref="State"/>.
/// </summary>
public sealed class MedicineDemandHandler : IDogmaHandler
{
    private enum Stage { DefenderPick, ActivatorPick, Done }

    private sealed class State
    {
        public Stage Stage;
        public int[] ActivatorOriginalScorePile = Array.Empty<int>();
    }

    public bool Execute(GameState g, PlayerState target, DogmaContext ctx)
    {
        var activator = g.Players[ctx.ActivatingPlayerIndex];
        var state = (State?)ctx.HandlerState;
        if (state is null)
        {
            // Snapshot activator's pile on first entry — used in
            // PromptActivator so cards just received from defender don't
            // count as "lowest in my score pile".
            state = new State
            {
                Stage = Stage.DefenderPick,
                ActivatorOriginalScorePile = activator.ScorePile.ToArray(),
            };
            ctx.HandlerState = state;
        }

        if (state.Stage == Stage.DefenderPick)
        {
            if (ctx.PendingChoice is null)
            {
                if (target.ScorePile.Count == 0)
                {
                    state.Stage = Stage.ActivatorPick;
                    return PromptActivator(g, target, activator, ctx, state);
                }

                int hi = target.ScorePile.Max(id => g.Cards[id].Age);
                var tied = target.ScorePile.Where(id => g.Cards[id].Age == hi).ToList();

                if (tied.Count == 1)
                {
                    Mechanics.TransferScoreToScore(g, target, activator, tied[0]);
                    ctx.DemandSuccessful = true;
                    state.Stage = Stage.ActivatorPick;
                    return PromptActivator(g, target, activator, ctx, state);
                }

                ctx.PendingChoice = new SelectScoreCardRequest
                {
                    Prompt = $"Medicine: choose which of your age-{hi} cards to give up.",
                    PlayerIndex = target.Index,
                    EligibleCardIds = tied.ToArray(),
                    AllowNone = false,
                };
                state.Stage = Stage.DefenderPick;
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
            state.Stage = Stage.ActivatorPick;
            return PromptActivator(g, target, activator, ctx, state);
        }

        // Stage.ActivatorPick — resolving the activator's score-card pick.
        {
            var req = (SelectScoreCardRequest)ctx.PendingChoice!;
            ctx.PendingChoice = null;
            ctx.HandlerState = null;
            if (req.ChosenCardId is int id)
                Mechanics.TransferScoreToScore(g, activator, target, id);
            return true;
        }
    }

    private static bool PromptActivator(GameState g, PlayerState target, PlayerState activator, DogmaContext ctx, State state)
    {
        // "Lowest card in my score pile" means the activator's pile as it
        // stood before the exchange — the defender's card just received
        // does not count. Intersect with the current pile in case the
        // activator's score has shifted (defensive; shouldn't happen here).
        var eligible = state.ActivatorOriginalScorePile
            .Where(activator.ScorePile.Contains)
            .ToList();
        if (eligible.Count == 0) { ctx.HandlerState = null; return true; }

        int lo = eligible.Min(id => g.Cards[id].Age);
        var tied = eligible.Where(id => g.Cards[id].Age == lo).ToList();

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
