namespace Innovation.Core.Handlers;

/// <summary>
/// The Pirate Code (age 5, Red/Crown) — non-demand: "If any card was
/// transferred due to the demand, score the lowest top card with a
/// [Crown] from your board."
///
/// "Lowest" = lowest-age crown-iconed top card. When multiple top cards
/// share the lowest age, the player picks which to score. Scored via
/// <see cref="Mechanics.ScoreFromBoard"/> (counts for Monument).
/// </summary>
public sealed class PirateCodeScoreIfDemandHandler : IDogmaHandler
{
    public bool Execute(GameState g, PlayerState target, DogmaContext ctx)
    {
        if (!ctx.DemandSuccessful) return false;

        // Resume after the player picked among tied colors.
        if (ctx.PendingChoice is SelectColorRequest answered)
        {
            ctx.PendingChoice = null;
            ctx.HandlerState = null;
            if (answered.ChosenColor is not CardColor pickedColor) return false;
            int pickedTop = target.Stack(pickedColor).Top;
            Mechanics.ScoreFromBoard(g, target, pickedColor, pickedTop);
            return true;
        }

        // Find every top card with a Crown, find the lowest age among them.
        int lowest = int.MaxValue;
        var atLowest = new List<CardColor>();
        foreach (CardColor c in Enum.GetValues<CardColor>())
        {
            var s = target.Stack(c);
            if (s.IsEmpty) continue;
            var card = g.Cards[s.Top];
            if (!Mechanics.HasIcon(card, Icon.Crown)) continue;
            if (card.Age < lowest) { lowest = card.Age; atLowest.Clear(); atLowest.Add(c); }
            else if (card.Age == lowest) atLowest.Add(c);
        }
        if (atLowest.Count == 0) return false;

        if (atLowest.Count == 1)
        {
            int top = target.Stack(atLowest[0]).Top;
            Mechanics.ScoreFromBoard(g, target, atLowest[0], top);
            return true;
        }

        // Multiple tops tied at the lowest age — the player picks.
        ctx.PendingChoice = new SelectColorRequest
        {
            Prompt = $"Pirate Code: choose which of your age-{lowest} top crown cards to score.",
            PlayerIndex = target.Index,
            EligibleColors = atLowest,
        };
        ctx.Paused = true;
        return false;
    }
}
