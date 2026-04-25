namespace Innovation.Core.Handlers;

/// <summary>
/// Tools first effect (age 1, Blue/Lightbulb, non-demand): "You may return
/// three cards from your hand. If you do, draw and meld a 3."
///
/// Mirrors VB6 main.frm 4530–4552 (AI path) and 8187–8195 (human phase).
/// All-or-nothing: the player either returns exactly 3 hand cards (and gets
/// a free age-3 meld) or returns 0. Two-step:
///   1. Yes/no — "return 3 cards?" (requires hand ≥ 3).
///   2. If yes, pick exactly 3 to return.
///
/// <see cref="DogmaContext.HandlerState"/> carries a step marker so we know
/// whether we're coming back from step 1 or step 2.
/// </summary>
public sealed class ToolsReturnThreeForMeldHandler : IDogmaHandler
{
    private enum Step { AskedYesNo, AskedSubset }

    public bool Execute(GameState g, PlayerState target, DogmaContext ctx)
    {
        // Cold entry.
        if (ctx.HandlerState is null && ctx.PendingChoice is null)
        {
            if (target.Hand.Count < 3) return false;

            ctx.HandlerState = Step.AskedYesNo;
            ctx.PendingChoice = new YesNoChoiceRequest
            {
                Prompt = "Tools: return three cards from your hand to draw and meld a 3?",
                PlayerIndex = target.Index,
            };
            ctx.Paused = true;
            return false;
        }

        var step = (Step)ctx.HandlerState!;

        if (step == Step.AskedYesNo)
        {
            var yn = (YesNoChoiceRequest)ctx.PendingChoice!;
            ctx.PendingChoice = null;

            if (!yn.ChosenYes)
            {
                ctx.HandlerState = null;
                return false;
            }

            // Ask which three.
            ctx.HandlerState = Step.AskedSubset;
            ctx.PendingChoice = new SelectHandCardSubsetRequest
            {
                Prompt = "Tools: pick the three cards to return.",
                PlayerIndex = target.Index,
                EligibleCardIds = target.Hand.ToArray(),
                MinCount = 3,
                MaxCount = 3,
            };
            ctx.Paused = true;
            return false;
        }

        // Step.AskedSubset — apply the returns, then draw-and-meld a 3.
        var req = (SelectHandCardSubsetRequest)ctx.PendingChoice!;
        ctx.PendingChoice = null;
        ctx.HandlerState = null;

        if (req.ChosenCardIds.Count != 3) return false;   // defensive

        foreach (var id in req.ChosenCardIds)
            Mechanics.Return(g, target, id);

        int drawn = Mechanics.DrawFromAge(g, target, 3);
        if (drawn < 0 || g.IsGameOver) return true;
        Mechanics.Meld(g, target, drawn);
        return true;
    }
}
