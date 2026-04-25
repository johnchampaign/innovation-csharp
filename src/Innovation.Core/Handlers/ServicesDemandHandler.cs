namespace Innovation.Core.Handlers;

/// <summary>
/// Services (age 9, Purple/Leaf) — demand: "Transfer all the highest cards
/// from your score pile to my hand! If you transferred any cards, then
/// transfer a top card from my board without a [Leaf] to your hand!"
/// </summary>
public sealed class ServicesDemandHandler : IDogmaHandler
{
    public bool Execute(GameState g, PlayerState target, DogmaContext ctx)
    {
        var activator = g.Players[ctx.ActivatingPlayerIndex];

        if (ctx.PendingChoice is SelectScoreCardRequest pick)
        {
            ctx.PendingChoice = null;
            if (pick.ChosenCardId is int chosen)
            {
                foreach (CardColor c in Enum.GetValues<CardColor>())
                {
                    var s = activator.Stack(c);
                    if (!s.IsEmpty && s.Top == chosen)
                    {
                        s.PopTop();
                        target.Hand.Add(chosen);
                        GameLog.Log($"Services: {GameLog.C(g, chosen)} moves {GameLog.P(activator)} board → {GameLog.P(target)} hand");
                        SpecialAchievements.CheckAll(g);
                        break;
                    }
                }
            }
            return true;
        }

        if (target.ScorePile.Count == 0) return false;

        int hi = target.ScorePile.Max(id => g.Cards[id].Age);
        var tied = target.ScorePile.Where(id => g.Cards[id].Age == hi).ToArray();
        foreach (var id in tied)
        {
            target.ScorePile.Remove(id);
            activator.Hand.Add(id);
            GameLog.Log($"Services: {GameLog.C(g, id)} {GameLog.P(target)} score → {GameLog.P(activator)} hand");
        }
        SpecialAchievements.CheckAll(g);
        ctx.DemandSuccessful = true;

        var eligible = new List<int>();
        foreach (CardColor c in Enum.GetValues<CardColor>())
        {
            var s = activator.Stack(c);
            if (s.IsEmpty) continue;
            if (Mechanics.HasIcon(g.Cards[s.Top], Icon.Leaf)) continue;
            eligible.Add(s.Top);
        }
        if (eligible.Count == 0) return true;

        ctx.PendingChoice = new SelectScoreCardRequest
        {
            Prompt = "Services: choose a top card without a [Leaf] from the activator's board to take into your hand.",
            PlayerIndex = target.Index,
            EligibleCardIds = eligible,
            AllowNone = false,
        };
        ctx.Paused = true;
        return true;
    }
}
