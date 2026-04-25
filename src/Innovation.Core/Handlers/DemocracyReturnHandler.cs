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
/// Counts persist across target visits via
/// <see cref="DogmaContext.HandlerState"/> as a <c>Dictionary&lt;int, int&gt;</c>
/// keyed by player index. The handler is two-stage per target:
///   1. Prompt the subset choice (or skip if hand is empty).
///   2. Apply the returns, record the count, evaluate the reward against
///      counts already recorded by earlier targets, and draw-score-an-8
///      if strictly greater than every previous count.
/// </summary>
public sealed class DemocracyReturnHandler : IDogmaHandler
{
    public bool Execute(GameState g, PlayerState target, DogmaContext ctx)
    {
        var counts = ctx.HandlerState as Dictionary<int, int>;
        if (counts is null)
        {
            counts = new Dictionary<int, int>();
            ctx.HandlerState = counts;
        }

        // Stage 1: ask for the subset (unless hand is empty).
        if (ctx.PendingChoice is null)
        {
            if (target.Hand.Count == 0)
            {
                counts[target.Index] = 0;
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

        // Stage 2: apply returns, record count, evaluate reward.
        var req = (SelectHandCardSubsetRequest)ctx.PendingChoice;
        ctx.PendingChoice = null;

        var picks = req.ChosenCardIds.ToArray();
        foreach (var id in picks) Mechanics.Return(g, target, id);
        counts[target.Index] = picks.Length;

        // Reward: strictly more cards returned than any *previously* recorded
        // target this dogma. Equal counts don't qualify (rules: "more than").
        // A target who returned 0 can't have returned more than anyone, so
        // skip the draw outright.
        if (picks.Length > 0)
        {
            bool moreThanAll = true;
            foreach (var (idx, c) in counts)
            {
                if (idx == target.Index) continue;
                if (c >= picks.Length) { moreThanAll = false; break; }
            }
            if (moreThanAll)
                Mechanics.DrawAndScore(g, target, 8);
        }

        return picks.Length > 0;
    }
}
