namespace Innovation.Core.Handlers;

/// <summary>
/// Anatomy (age 4, Yellow/Leaf) — demand: "I demand you return a card
/// from your score pile! If you do, return a top card of equal value
/// from your board!"
///
/// Two picks, both by the target. Score-pile return first; if anything
/// was returned, the target must then return a top card whose age
/// matches the returned card. If no eligible top card exists for leg
/// two, the demand still "succeeded" (leg one happened).
/// </summary>
public sealed class AnatomyDemandHandler : IDogmaHandler
{
    private sealed class AwaitingBoardReturn { public int RequiredAge; }

    public bool Execute(GameState g, PlayerState target, DogmaContext ctx)
    {
        // Phase 1: pick score-pile card to return.
        if (ctx.HandlerState is null && ctx.PendingChoice is null)
        {
            if (target.ScorePile.Count == 0) return false;

            ctx.HandlerState = "score";
            ctx.PendingChoice = new SelectScoreCardRequest
            {
                Prompt = "Anatomy: return a card from your score pile.",
                PlayerIndex = target.Index,
                EligibleCardIds = target.ScorePile.ToArray(),
                AllowNone = false,
            };
            ctx.Paused = true;
            return false;
        }

        if (ctx.HandlerState as string == "score")
        {
            var req = (SelectScoreCardRequest)ctx.PendingChoice!;
            ctx.PendingChoice = null;
            ctx.HandlerState = null;
            if (req.ChosenCardId is not int sid) return false;

            int age = g.Cards[sid].Age;
            target.ScorePile.Remove(sid);
            g.Decks[age].Add(sid);
            SpecialAchievements.CheckAll(g);
            ctx.DemandSuccessful = true;
            if (g.IsGameOver) return true;

            // Phase 2: top card of equal age.
            var eligible = new List<CardColor>();
            foreach (CardColor c in Enum.GetValues<CardColor>())
            {
                var s = target.Stack(c);
                if (s.IsEmpty) continue;
                if (g.Cards[s.Top].Age == age) eligible.Add(c);
            }
            if (eligible.Count == 0) return true;

            ctx.HandlerState = new AwaitingBoardReturn { RequiredAge = age };
            ctx.PendingChoice = new SelectColorRequest
            {
                Prompt = $"Anatomy: return a top card of value {age} from your board.",
                PlayerIndex = target.Index,
                EligibleColors = eligible,
            };
            ctx.Paused = true;
            return true;
        }

        var wait = (AwaitingBoardReturn)ctx.HandlerState!;
        var creq = (SelectColorRequest)ctx.PendingChoice!;
        ctx.PendingChoice = null;
        ctx.HandlerState = null;
        if (creq.ChosenColor is not CardColor color) return true;

        var stack = target.Stack(color);
        if (stack.IsEmpty) return true;
        int top = stack.PopTop();
        g.Decks[g.Cards[top].Age].Add(top);
        SpecialAchievements.CheckAll(g);
        return true;
    }
}
