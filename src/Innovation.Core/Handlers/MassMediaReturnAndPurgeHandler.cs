namespace Innovation.Core.Handlers;

/// <summary>
/// Mass Media (age 8, Green/Lightbulb) — effect 1: "You may return a card
/// from your hand. If you do, choose a value, and return all cards of that
/// value from all score piles."
/// </summary>
public sealed class MassMediaReturnAndPurgeHandler : IDogmaHandler
{
    private enum Stage { PickHand, PickValue }

    public bool Execute(GameState g, PlayerState target, DogmaContext ctx)
    {
        var stage = (Stage?)ctx.HandlerState ?? Stage.PickHand;

        if (stage == Stage.PickHand)
        {
            if (target.Hand.Count == 0) return false;

            if (ctx.PendingChoice is null)
            {
                ctx.PendingChoice = new SelectHandCardRequest
                {
                    Prompt = "Mass Media: return a card from your hand (then purge a value from all score piles)?",
                    PlayerIndex = target.Index,
                    EligibleCardIds = target.Hand.ToArray(),
                    AllowNone = true,
                };
                ctx.HandlerState = Stage.PickHand;
                ctx.Paused = true;
                return false;
            }

            var r = (SelectHandCardRequest)ctx.PendingChoice;
            ctx.PendingChoice = null;
            if (r.ChosenCardId is not int returned) { ctx.HandlerState = null; return false; }

            Mechanics.Return(g, target, returned);
            if (g.IsGameOver) { ctx.HandlerState = null; return true; }

            var values = new HashSet<int>();
            foreach (var pl in g.Players)
                foreach (var id in pl.ScorePile)
                    values.Add(g.Cards[id].Age);
            if (values.Count == 0) { ctx.HandlerState = null; return true; }

            ctx.PendingChoice = new SelectValueRequest
            {
                Prompt = "Mass Media: choose a value. All cards of that value in every score pile are returned.",
                PlayerIndex = target.Index,
                EligibleValues = values.OrderBy(x => x).ToArray(),
                AllowNone = false,
            };
            ctx.HandlerState = Stage.PickValue;
            ctx.Paused = true;
            return true;
        }

        // Stage.PickValue
        var req = (SelectValueRequest)ctx.PendingChoice!;
        ctx.PendingChoice = null;
        ctx.HandlerState = null;
        if (req.ChosenValue is not int age) return true;

        foreach (var pl in g.Players)
        {
            var toReturn = pl.ScorePile.Where(id => g.Cards[id].Age == age).ToArray();
            foreach (var id in toReturn)
            {
                pl.ScorePile.Remove(id);
                g.Decks[g.Cards[id].Age].Add(id);
                GameLog.Log($"{GameLog.P(pl)} returns {GameLog.C(g, id)} from score pile (Mass Media)");
            }
        }
        SpecialAchievements.CheckAll(g);
        return true;
    }
}
