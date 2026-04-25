namespace Innovation.Core.Handlers;

/// <summary>
/// Fission (age 9, Red/Clock) — effect 2: "Return a top card other than
/// Fission from any player's board."
///
/// Activator-executed (target in share-loop). Skipped entirely if the first
/// effect wiped the game (signal via <see cref="FissionDemandHandler.FissionWiped"/>
/// on <see cref="DogmaContext.HandlerState"/>).
/// </summary>
public sealed class FissionReturnHandler : IDogmaHandler
{
    public bool Execute(GameState g, PlayerState target, DogmaContext ctx)
    {
        if (ReferenceEquals(ctx.HandlerState, FissionDemandHandler.FissionWiped))
        {
            ctx.HandlerState = null;
            return false;
        }

        if (ctx.PendingChoice is SelectHandCardRequest prior)
        {
            ctx.PendingChoice = null;
            if (prior.ChosenCardId is int chosen)
            {
                foreach (var pl in g.Players)
                {
                    foreach (CardColor c in Enum.GetValues<CardColor>())
                    {
                        var s = pl.Stack(c);
                        if (!s.IsEmpty && s.Top == chosen)
                        {
                            s.PopTop();
                            g.Decks[g.Cards[chosen].Age].Add(chosen);
                            GameLog.Log($"{GameLog.P(pl)} returns {GameLog.C(g, chosen)} from board (Fission)");
                            SpecialAchievements.CheckAll(g);
                            return true;
                        }
                    }
                }
            }
            return false;
        }

        var eligible = new List<int>();
        foreach (var pl in g.Players)
        {
            foreach (CardColor c in Enum.GetValues<CardColor>())
            {
                var s = pl.Stack(c);
                if (s.IsEmpty) continue;
                if (g.Cards[s.Top].Title == "Fission") continue;
                eligible.Add(s.Top);
            }
        }
        if (eligible.Count == 0) return false;

        ctx.PendingChoice = new SelectHandCardRequest
        {
            Prompt = "Fission: choose a top card (not Fission) from any player's board to return.",
            PlayerIndex = target.Index,
            EligibleCardIds = eligible,
            AllowNone = false,
        };
        ctx.Paused = true;
        return true;
    }
}
