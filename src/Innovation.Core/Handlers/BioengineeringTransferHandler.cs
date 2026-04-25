namespace Innovation.Core.Handlers;

/// <summary>
/// Bioengineering (age 10, Blue/Lightbulb) — effect 1: "Transfer a top card
/// with a [Leaf] from any other player's board to your score pile."
/// </summary>
public sealed class BioengineeringTransferHandler : IDogmaHandler
{
    public bool Execute(GameState g, PlayerState target, DogmaContext ctx)
    {
        if (ctx.PendingChoice is SelectScoreCardRequest prior)
        {
            ctx.PendingChoice = null;
            if (prior.ChosenCardId is int chosen)
            {
                foreach (var p in g.Players)
                {
                    if (p.Index == target.Index) continue;
                    foreach (CardColor c in Enum.GetValues<CardColor>())
                    {
                        var s = p.Stack(c);
                        if (!s.IsEmpty && s.Top == chosen)
                        {
                            s.PopTop();
                            target.ScorePile.Add(chosen);
                            GameLog.Log($"Bioengineering: {GameLog.C(g, chosen)} from {GameLog.P(p)} board → {GameLog.P(target)} score");
                            SpecialAchievements.CheckAll(g);
                            return true;
                        }
                    }
                }
            }
            return false;
        }

        var eligible = new List<int>();
        foreach (var p in g.Players)
        {
            if (p.Index == target.Index) continue;
            foreach (CardColor c in Enum.GetValues<CardColor>())
            {
                var s = p.Stack(c);
                if (s.IsEmpty) continue;
                if (!Mechanics.HasIcon(g.Cards[s.Top], Icon.Leaf)) continue;
                eligible.Add(s.Top);
            }
        }
        if (eligible.Count == 0) return false;

        ctx.PendingChoice = new SelectScoreCardRequest
        {
            Prompt = "Bioengineering: transfer a top [Leaf] card from another player's board to your score pile.",
            PlayerIndex = target.Index,
            EligibleCardIds = eligible,
            AllowNone = false,
        };
        ctx.Paused = true;
        return true;
    }
}
