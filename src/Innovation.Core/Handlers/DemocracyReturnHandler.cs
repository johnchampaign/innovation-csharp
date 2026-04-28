namespace Innovation.Core.Handlers;

/// <summary>
/// Democracy (age 6, Purple/Lightbulb) — single share effect: "You may
/// return any number of cards from your hand. If you returned more cards
/// than any other player due to Democracy this dogma action, draw and
/// score an 8."
///
/// Per the share rules, each target runs the whole instruction (return +
/// reward) before the next target starts. The comparison "more than any
/// other player" evaluates against the counts AT THAT MOMENT — i.e. only
/// counts already recorded by previous targets in this dogma. That makes
/// it possible for both players to score: e.g. opp returns 1 first
/// (greater than activator's 0-so-far → opp scores), then activator
/// returns 2 (greater than opp's 1 → activator also scores).
///
/// Three sub-stages per target visit:
///   1. Subset prompt — pick which cards to return.
///   2. Order prompt — when 2+ cards picked, the player picks the return
///      order (last-returned ends up on top of its age deck).
///   3. Apply: return each card, record this target's count, evaluate
///      the "more than every prior target" reward.
///
/// Per-target cross-target counts persist via the <see cref="State.Counts"/>
/// dictionary in <see cref="DogmaContext.HandlerState"/>.
/// </summary>
public sealed class DemocracyReturnHandler : IDogmaHandler
{
    private sealed class State
    {
        public Dictionary<int, int> Counts = new();
        public int[]? PendingPicks;   // non-null while waiting for order answer
    }

    public bool Execute(GameState g, PlayerState target, DogmaContext ctx)
    {
        var state = ctx.HandlerState as State;
        if (state is null)
        {
            state = new State();
            ctx.HandlerState = state;
        }

        // Subset stage.
        if (state.PendingPicks is null && ctx.PendingChoice is null)
        {
            if (target.Hand.Count == 0)
            {
                state.Counts[target.Index] = 0;
                return false;
            }
            ctx.PendingChoice = new SelectHandCardSubsetRequest
            {
                Prompt = "Democracy: return any number of cards from your hand.",
                PlayerIndex = target.Index,
                EligibleCardIds = target.Hand.ToArray(),
                MinCount = 0,
                MaxCount = target.Hand.Count,
            };
            ctx.Paused = true;
            return false;
        }

        // Subset answered.
        if (state.PendingPicks is null && ctx.PendingChoice is SelectHandCardSubsetRequest subset)
        {
            ctx.PendingChoice = null;
            var picks = subset.ChosenCardIds.ToArray();
            if (picks.Length == 0)
            {
                state.Counts[target.Index] = 0;
                return false;
            }
            if (picks.Length == 1 || !Mechanics.OrderMatters(picks, id => g.Cards[id].Age))
            {
                return ApplyReturnsAndEvaluate(g, target, picks, state);
            }
            // 2+ picks with at least one duplicate age — ask order.
            state.PendingPicks = picks;
            ctx.PendingChoice = new SelectCardOrderRequest
            {
                Prompt = "Democracy: choose the return order.",
                PlayerIndex = target.Index,
                Action = "return",
                CardIds = picks,
            };
            ctx.Paused = true;
            return false;
        }

        // Order answered.
        var orderReq = (SelectCardOrderRequest)ctx.PendingChoice!;
        var input = state.PendingPicks!;
        ctx.PendingChoice = null;
        state.PendingPicks = null;
        var ordered = Mechanics.ValidateOrder(orderReq.ChosenOrder, input);
        return ApplyReturnsAndEvaluate(g, target, ordered, state);
    }

    private static bool ApplyReturnsAndEvaluate(GameState g, PlayerState target, IReadOnlyList<int> ids, State state)
    {
        // ids is final deck-arrangement top-first; reverse for application.
        for (int i = ids.Count - 1; i >= 0; i--)
            Mechanics.Return(g, target, ids[i]);
        state.Counts[target.Index] = ids.Count;

        // Reward: strictly more cards returned than any *previously* recorded
        // target this dogma. Equal counts don't qualify (rules: "more than").
        if (ids.Count > 0)
        {
            bool moreThanAll = true;
            foreach (var (idx, c) in state.Counts)
            {
                if (idx == target.Index) continue;
                if (c >= ids.Count) { moreThanAll = false; break; }
            }
            if (moreThanAll)
                Mechanics.DrawAndScore(g, target, 8);
        }
        return ids.Count > 0;
    }
}
