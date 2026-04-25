namespace Innovation.Core.Handlers;

/// <summary>
/// Collaboration (age 9, Green/Crown) — demand: "I demand you draw two 9s
/// and reveal them. Transfer the card of my choice to my board, and meld
/// the other."
///
/// Target draws two 9s. Activator chooses which one transfers to their
/// board; the other is melded by the target.
/// </summary>
public sealed class CollaborationDemandHandler : IDogmaHandler
{
    private sealed class State
    {
        public int First;
        public int Second;
    }

    public bool Execute(GameState g, PlayerState target, DogmaContext ctx)
    {
        var activator = g.Players[ctx.ActivatingPlayerIndex];

        if (ctx.HandlerState is null && ctx.PendingChoice is null)
        {
            int a = Mechanics.DrawFromAge(g, target, 9);
            if (a < 0 || g.IsGameOver) return true;
            int b = Mechanics.DrawFromAge(g, target, 9);
            if (b < 0 || g.IsGameOver) return true;

            GameLog.Log($"Collaboration: drew {GameLog.C(g, a)} and {GameLog.C(g, b)}");

            var st = new State { First = a, Second = b };
            ctx.HandlerState = st;
            ctx.PendingChoice = new SelectHandCardRequest
            {
                Prompt = $"Collaboration ({GameLog.P(activator)}'s choice): which card transfers to your board?",
                PlayerIndex = activator.Index,
                EligibleCardIds = new[] { a, b },
                AllowNone = false,
            };
            ctx.Paused = true;
            return true;
        }

        var st2 = (State)ctx.HandlerState!;
        var req = (SelectHandCardRequest)ctx.PendingChoice!;
        ctx.PendingChoice = null;
        ctx.HandlerState = null;

        int toActivator = req.ChosenCardId ?? st2.First;
        int toMeld = toActivator == st2.First ? st2.Second : st2.First;

        // Move the chosen card from target's hand to activator's board.
        target.Hand.Remove(toActivator);
        activator.Stack(g.Cards[toActivator].Color).Meld(toActivator);
        GameLog.Log($"Collaboration: {GameLog.P(target)} transfers {GameLog.C(g, toActivator)} to {GameLog.P(activator)}'s board");
        SpecialAchievements.CheckAll(g);
        ctx.DemandSuccessful = true;
        if (g.IsGameOver) return true;

        Mechanics.Meld(g, target, toMeld);
        return true;
    }
}
