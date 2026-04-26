namespace Innovation.Core.Handlers;

/// <summary>
/// Currency (age 2, Green/Crown): "You may return any number of cards from
/// your hand. If you do, draw and score a 2 for every different value of
/// card you returned."
///
/// "Different value" means distinct ages — returning two 1s and a 3 yields
/// two draw-and-scores, not three. Cards go to the SCORE pile, not the
/// hand. Pattern follows Pottery: subset prompt → return each → derive
/// distinct-age count → draw-and-score that many 2s.
/// </summary>
public sealed class CurrencyHandler : IDogmaHandler
{
    public bool Execute(GameState g, PlayerState target, DogmaContext ctx)
    {
        if (target.Hand.Count == 0) return false;

        if (ctx.PendingChoice is null)
        {
            ctx.PendingChoice = new SelectHandCardSubsetRequest
            {
                Prompt = "Currency: return any number of cards from your hand. "
                       + "Draw and score a 2 for each distinct age among cards returned.",
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

        if (req.ChosenCardIds.Count == 0) return false;

        var distinctAges = new HashSet<int>();
        foreach (var id in req.ChosenCardIds)
        {
            distinctAges.Add(g.Cards[id].Age);
            Mechanics.Return(g, target, id);
        }

        for (int i = 0; i < distinctAges.Count; i++)
        {
            Mechanics.DrawAndScore(g, target, 2);
            if (g.IsGameOver) return true;
        }
        return true;
    }
}
