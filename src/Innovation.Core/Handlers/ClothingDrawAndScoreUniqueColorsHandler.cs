namespace Innovation.Core.Handlers;

/// <summary>
/// Clothing second effect (age 1, Green/Crown, non-demand): "Draw and
/// score a 1 for every color present on your board not present on any
/// other player's board."
///
/// Mirrors VB6 main.frm 4337–4352. Walks the five colors; for each, if
/// <em>this</em> player has a pile of color C and <em>no other</em>
/// player has a pile of color C, draw an age-1 card and score it. If
/// the draw cascades off the top of the deck mid-loop, bail — the game
/// is over.
///
/// Returns true if at least one draw-and-score happened, so the engine
/// fires the shared-bonus draw for the activator when a share-eligible
/// player benefits.
/// </summary>
public sealed class ClothingDrawAndScoreUniqueColorsHandler : IDogmaHandler
{
    public bool Execute(GameState g, PlayerState target, DogmaContext ctx)
    {
        bool anyScored = false;

        foreach (CardColor color in Enum.GetValues<CardColor>())
        {
            // Target must have this color on their board.
            if (target.Stack(color).IsEmpty) continue;

            // No other player may have it.
            bool othersHaveIt = false;
            foreach (var other in g.Players)
            {
                if (other.Index == target.Index) continue;
                if (!other.Stack(color).IsEmpty) { othersHaveIt = true; break; }
            }
            if (othersHaveIt) continue;

            int drawn = Mechanics.DrawFromAge(g, target, 1);
            if (drawn < 0 || g.IsGameOver) return anyScored || drawn >= 0;
            Mechanics.Score(g, target, drawn);
            anyScored = true;
            if (g.IsGameOver) return true;
        }

        return anyScored;
    }
}
