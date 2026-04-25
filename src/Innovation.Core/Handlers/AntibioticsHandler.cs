namespace Innovation.Core.Handlers;

/// <summary>
/// Antibiotics (age 8, Yellow/Leaf): "You may return up to three cards from
/// your hand. For every different value of card that you returned, draw
/// two 8s."
/// </summary>
public sealed class AntibioticsHandler : IDogmaHandler
{
    public bool Execute(GameState g, PlayerState target, DogmaContext ctx)
    {
        if (target.Hand.Count == 0) return false;

        if (ctx.PendingChoice is null)
        {
            ctx.PendingChoice = new SelectHandCardSubsetRequest
            {
                Prompt = "Antibiotics: return up to three cards. Draw two 8s for each distinct age returned.",
                PlayerIndex = target.Index,
                EligibleCardIds = target.Hand.ToArray(),
                MinCount = 0,
                MaxCount = Math.Min(3, target.Hand.Count),
            };
            ctx.Paused = true;
            return false;
        }

        var req = (SelectHandCardSubsetRequest)ctx.PendingChoice;
        ctx.PendingChoice = null;

        if (req.ChosenCardIds.Count == 0) return false;

        var distinctAges = new HashSet<int>();
        foreach (var id in req.ChosenCardIds)
        {
            distinctAges.Add(g.Cards[id].Age);
            Mechanics.Return(g, target, id);
        }

        int draws = distinctAges.Count * 2;
        for (int i = 0; i < draws; i++)
        {
            Mechanics.DrawFromAge(g, target, 8);
            if (g.IsGameOver) return true;
        }
        return true;
    }
}
