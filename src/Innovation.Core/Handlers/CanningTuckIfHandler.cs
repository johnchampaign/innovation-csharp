namespace Innovation.Core.Handlers;

/// <summary>
/// Canning (age 6, Yellow/Factory) — effect 1: "You may draw and tuck
/// a 6. If you do, score all your top cards without a [Factory]."
///
/// Non-demand: target is each share recipient. Prompt a yes/no; on yes,
/// draw-and-tuck a 6, then score every top card without a Factory icon.
/// </summary>
public sealed class CanningTuckIfHandler : IDogmaHandler
{
    public bool Execute(GameState g, PlayerState target, DogmaContext ctx)
    {
        if (ctx.PendingChoice is null)
        {
            ctx.PendingChoice = new YesNoChoiceRequest
            {
                Prompt = "Canning: draw and tuck a 6, then score all your top cards without a [Factory]?",
                PlayerIndex = target.Index,
            };
            ctx.Paused = true;
            return false;
        }

        var yn = (YesNoChoiceRequest)ctx.PendingChoice;
        ctx.PendingChoice = null;
        if (!yn.ChosenYes) return false;

        if (Mechanics.DrawAndTuck(g, target, 6) < 0 || g.IsGameOver) return true;

        foreach (CardColor c in Enum.GetValues<CardColor>())
        {
            var s = target.Stack(c);
            if (s.IsEmpty) continue;
            var top = g.Cards[s.Top];
            if (Mechanics.HasIcon(top, Icon.Factory)) continue;
            Mechanics.ScoreFromBoard(g, target, c, s.Top);
            if (g.IsGameOver) return true;
        }
        return true;
    }
}
