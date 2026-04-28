namespace Innovation.Core.Handlers;

/// <summary>
/// Encyclopedia (age 6, Blue/Crown) — non-demand: "You may meld all
/// the highest cards in your score pile. If you meld one of the
/// highest, you must meld all of the highest."
///
/// Highest = highest age among score-pile cards. Yes/no on "meld them
/// all"; on yes, every score-pile card of that age moves into its
/// color pile via <see cref="Mechanics.Meld"/>. (Meld takes a hand
/// card; here we manually mirror: remove from score pile, add to
/// color stack, run special-achievement check.)
/// </summary>
public sealed class EncyclopediaHandler : IDogmaHandler
{
    public bool Execute(GameState g, PlayerState target, DogmaContext ctx)
    {
        if (target.ScorePile.Count == 0) return false;
        int highest = target.ScorePile.Max(id => g.Cards[id].Age);

        // Stage A: yes/no.
        if (ctx.PendingChoice is null && ctx.HandlerState is null)
        {
            ctx.PendingChoice = new YesNoChoiceRequest
            {
                Prompt = $"Encyclopedia: meld all {highest}s from your score pile?",
                PlayerIndex = target.Index,
            };
            ctx.Paused = true;
            return false;
        }

        if (ctx.PendingChoice is YesNoChoiceRequest yn)
        {
            ctx.PendingChoice = null;
            if (!yn.ChosenYes) return false;

            var toMeld = target.ScorePile.Where(id => g.Cards[id].Age == highest).ToArray();
            if (toMeld.Length == 0) return false;
            if (toMeld.Length == 1 || !Mechanics.OrderMatters(toMeld, id => g.Cards[id].Color))
            {
                foreach (var id in toMeld)
                {
                    MeldFromScore(g, target, id);
                    if (g.IsGameOver) return true;
                }
                return true;
            }

            // Two or more share a color — ask the player for the meld order.
            ctx.HandlerState = toMeld;
            ctx.PendingChoice = new SelectCardOrderRequest
            {
                Prompt = "Encyclopedia: choose the meld order for the highest cards (last melded ends up on top of its color).",
                PlayerIndex = target.Index,
                Action = "meld",
                CardIds = toMeld,
            };
            ctx.Paused = true;
            return false;
        }

        // Stage B: order resolved.
        var orderReq = (SelectCardOrderRequest)ctx.PendingChoice!;
        var input = (int[])ctx.HandlerState!;
        ctx.PendingChoice = null;
        ctx.HandlerState = null;
        var ordered = Mechanics.ValidateOrder(orderReq.ChosenOrder, input);
        // ChosenOrder is the final pile arrangement, top-first. Reverse so
        // the first listed melds last and ends up on top.
        for (int i = ordered.Count - 1; i >= 0; i--)
        {
            MeldFromScore(g, target, ordered[i]);
            if (g.IsGameOver) return true;
        }
        return ordered.Count > 0;
    }

    private static void MeldFromScore(GameState g, PlayerState target, int id)
    {
        target.ScorePile.Remove(id);
        var color = g.Cards[id].Color;
        target.Stack(color).Meld(id);
        GameLog.Log($"{GameLog.P(target)} melds (from score) {GameLog.C(g, id)}");
        SpecialAchievements.CheckAll(g);
    }
}
