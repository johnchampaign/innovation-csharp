namespace Innovation.Core.Handlers;

/// <summary>
/// Globalization (age 10, Yellow/Factory) — demand: "Return a top card with
/// a [Leaf] on your board!"
/// </summary>
public sealed class GlobalizationDemandHandler : IDogmaHandler
{
    public bool Execute(GameState g, PlayerState target, DogmaContext ctx)
    {
        if (ctx.PendingChoice is SelectScoreCardRequest prior)
        {
            ctx.PendingChoice = null;
            if (prior.ChosenCardId is int chosen)
            {
                foreach (CardColor c in Enum.GetValues<CardColor>())
                {
                    var s = target.Stack(c);
                    if (!s.IsEmpty && s.Top == chosen)
                    {
                        s.PopTop();
                        g.Decks[g.Cards[chosen].Age].Add(chosen);
                        GameLog.Log($"{GameLog.P(target)} returns {GameLog.C(g, chosen)} from board (Globalization)");
                        SpecialAchievements.CheckAll(g);
                        ctx.DemandSuccessful = true;
                        return true;
                    }
                }
            }
            return false;
        }

        var eligible = new List<int>();
        foreach (CardColor c in Enum.GetValues<CardColor>())
        {
            var s = target.Stack(c);
            if (s.IsEmpty) continue;
            if (!Mechanics.HasIcon(g.Cards[s.Top], Icon.Leaf)) continue;
            eligible.Add(s.Top);
        }
        if (eligible.Count == 0) return false;

        ctx.PendingChoice = new SelectScoreCardRequest
        {
            Prompt = "Globalization: return a top [Leaf] card from your board.",
            PlayerIndex = target.Index,
            EligibleCardIds = eligible,
            AllowNone = false,
        };
        ctx.Paused = true;
        return true;
    }
}
