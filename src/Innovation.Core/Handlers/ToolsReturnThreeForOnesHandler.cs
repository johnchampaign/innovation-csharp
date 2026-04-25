namespace Innovation.Core.Handlers;

/// <summary>
/// Tools second effect (age 1, Blue/Lightbulb, non-demand): "You may
/// return a 3 from your hand. If you do, draw three 1s."
///
/// Mirrors VB6 main.frm 4553–4578 (AI path) and 8197–8204 (human phase).
/// Optional single-card return. Decline → no progress. Return an age-3 →
/// three age-1 draws (stopping early if the deck runs out).
///
/// Note: despite "three 1s", VB6 uses <c>draw_num(player, 1)</c> three
/// times, meaning "draw from age 1 (or higher if empty)" — same cascade
/// semantics as <see cref="Mechanics.DrawFromAge"/>.
/// </summary>
public sealed class ToolsReturnThreeForOnesHandler : IDogmaHandler
{
    public bool Execute(GameState g, PlayerState target, DogmaContext ctx)
    {
        if (ctx.PendingChoice is null)
        {
            var threes = target.Hand
                .Where(id => g.Cards[id].Age == 3)
                .ToArray();
            if (threes.Length == 0) return false;

            ctx.PendingChoice = new SelectHandCardRequest
            {
                Prompt = "Tools: return a 3 from your hand to draw three 1s.",
                PlayerIndex = target.Index,
                EligibleCardIds = threes,
                AllowNone = true,
            };
            ctx.Paused = true;
            return false;
        }

        var req = (SelectHandCardRequest)ctx.PendingChoice;
        ctx.PendingChoice = null;

        if (req.ChosenCardId is not int cardId) return false;   // declined

        Mechanics.Return(g, target, cardId);
        for (int i = 0; i < 3; i++)
        {
            int drawn = Mechanics.DrawFromAge(g, target, 1);
            if (drawn < 0 || g.IsGameOver) return true;
        }
        return true;
    }
}
