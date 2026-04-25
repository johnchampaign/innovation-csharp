namespace Innovation.Core.Handlers;

/// <summary>
/// The Pirate Code (age 5, Red/Crown) — non-demand: "If any card was
/// transferred due to the demand, score the lowest top card with a
/// [Crown] from your board."
///
/// "Lowest" = lowest-age crown-iconed top card on the active player's
/// board. Ties broken by color enum order. Scored via
/// <see cref="Mechanics.ScoreFromBoard"/> (counts for Monument).
/// </summary>
public sealed class PirateCodeScoreIfDemandHandler : IDogmaHandler
{
    public bool Execute(GameState g, PlayerState target, DogmaContext ctx)
    {
        if (!ctx.DemandSuccessful) return false;

        CardColor? bestColor = null;
        int bestAge = int.MaxValue;
        foreach (CardColor c in Enum.GetValues<CardColor>())
        {
            var s = target.Stack(c);
            if (s.IsEmpty) continue;
            var card = g.Cards[s.Top];
            if (!Mechanics.HasIcon(card, Icon.Crown)) continue;
            if (card.Age < bestAge) { bestAge = card.Age; bestColor = c; }
        }
        if (bestColor is not CardColor color) return false;
        int top = target.Stack(color).Top;
        Mechanics.ScoreFromBoard(g, target, color, top);
        return true;
    }
}
