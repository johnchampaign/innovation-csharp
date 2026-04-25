namespace Innovation.Core.Handlers;

/// <summary>
/// Suburbia (age 9, Yellow/Leaf): "You may tuck any number of cards from
/// your hand. Draw and score a 1 for each card you tucked."
/// </summary>
public sealed class SuburbiaHandler : IDogmaHandler
{
    public bool Execute(GameState g, PlayerState target, DogmaContext ctx)
    {
        if (target.Hand.Count == 0) return false;

        if (ctx.PendingChoice is null)
        {
            ctx.PendingChoice = new SelectHandCardSubsetRequest
            {
                Prompt = "Suburbia: tuck any number of cards. Draw and score a 1 for each.",
                PlayerIndex = target.Index,
                EligibleCardIds = target.Hand.ToArray(),
                MinCount = 0,
                MaxCount = target.Hand.Count,
            };
            ctx.Paused = true;
            return false;
        }

        var req = (SelectHandCardSubsetRequest)ctx.PendingChoice;
        ctx.PendingChoice = null;
        int tucked = req.ChosenCardIds.Count;
        foreach (var id in req.ChosenCardIds)
            Mechanics.Tuck(g, target, id);

        for (int i = 0; i < tucked; i++)
        {
            Mechanics.DrawAndScore(g, target, 1);
            if (g.IsGameOver) return true;
        }
        return tucked > 0;
    }
}
