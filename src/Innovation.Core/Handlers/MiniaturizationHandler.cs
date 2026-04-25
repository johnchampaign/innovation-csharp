namespace Innovation.Core.Handlers;

/// <summary>
/// Miniaturization (age 10, Red/Lightbulb): "You may return a card from
/// your hand. If you returned a 10, draw a 10 for every different value of
/// card in your score pile."
/// </summary>
public sealed class MiniaturizationHandler : IDogmaHandler
{
    public bool Execute(GameState g, PlayerState target, DogmaContext ctx)
    {
        if (target.Hand.Count == 0) return false;

        if (ctx.PendingChoice is null)
        {
            ctx.PendingChoice = new SelectHandCardRequest
            {
                Prompt = "Miniaturization: return a card. If it's a 10, draw a 10 for each distinct score-pile value.",
                PlayerIndex = target.Index,
                EligibleCardIds = target.Hand.ToArray(),
                AllowNone = true,
            };
            ctx.Paused = true;
            return false;
        }

        var req = (SelectHandCardRequest)ctx.PendingChoice;
        ctx.PendingChoice = null;
        if (req.ChosenCardId is not int id) return false;
        int age = g.Cards[id].Age;
        Mechanics.Return(g, target, id);
        if (age != 10 || g.IsGameOver) return true;

        int distinct = target.ScorePile.Select(c => g.Cards[c].Age).Distinct().Count();
        for (int i = 0; i < distinct; i++)
        {
            Mechanics.DrawFromAge(g, target, 10);
            if (g.IsGameOver) return true;
        }
        return true;
    }
}
