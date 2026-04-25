namespace Innovation.Core.Handlers;

/// <summary>
/// Lighting (age 7, Purple/Leaf): "You may tuck up to three cards from
/// your hand. If you do, draw and score a 7 for every different value of
/// card you tucked."
/// </summary>
public sealed class LightingHandler : IDogmaHandler
{
    public bool Execute(GameState g, PlayerState target, DogmaContext ctx)
    {
        if (ctx.PendingChoice is null)
        {
            if (target.Hand.Count == 0) return false;
            ctx.PendingChoice = new SelectHandCardSubsetRequest
            {
                Prompt = "Lighting: tuck up to 3 cards; draw and score a 7 for each distinct age tucked.",
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

        var distinct = new HashSet<int>();
        foreach (var id in req.ChosenCardIds)
        {
            distinct.Add(g.Cards[id].Age);
            Mechanics.Tuck(g, target, id);
        }
        foreach (var _ in distinct)
        {
            if (g.IsGameOver) break;
            Mechanics.DrawAndScore(g, target, 7);
        }
        return true;
    }
}
