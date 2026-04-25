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

        if (ctx.PendingChoice is null)
        {
            ctx.PendingChoice = new YesNoChoiceRequest
            {
                Prompt = $"Encyclopedia: meld all {highest}s from your score pile?",
                PlayerIndex = target.Index,
            };
            ctx.Paused = true;
            return false;
        }

        var yn = (YesNoChoiceRequest)ctx.PendingChoice;
        ctx.PendingChoice = null;
        if (!yn.ChosenYes) return false;

        var toMeld = target.ScorePile.Where(id => g.Cards[id].Age == highest).ToArray();
        foreach (var id in toMeld)
        {
            target.ScorePile.Remove(id);
            var color = g.Cards[id].Color;
            target.Stack(color).Meld(id);
            GameLog.Log($"{GameLog.P(target)} melds (from score) {GameLog.C(g, id)}");
            SpecialAchievements.CheckAll(g);
        }
        return toMeld.Length > 0;
    }
}
