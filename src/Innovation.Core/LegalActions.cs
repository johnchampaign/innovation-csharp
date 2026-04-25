namespace Innovation.Core;

/// <summary>
/// Enumerates the top-level <see cref="PlayerAction"/>s a player may take
/// on their turn. Mirrors the four buckets the VB6 AI builds inside
/// <c>pick_ai_move</c> (AIFunctions.bas lines 130–154):
///   • Achieve: any age-tile the player is currently eligible for.
///   • Draw: always legal.
///   • Dogma: one per non-empty color pile on the player's board.
///   • Meld: one per card in hand.
///
/// Only age-1 through age-9 achievements are enumerated; no tile exists
/// for age 10 (its deck-exhaustion trigger is a separate end-game path).
/// </summary>
public static class LegalActions
{
    public static IReadOnlyList<PlayerAction> Enumerate(GameState g, PlayerState p)
    {
        var list = new List<PlayerAction>();

        // Achievements first — matches the VB6 AI's enumeration order, which
        // doesn't matter to the engine but keeps test assertions predictable.
        for (int age = 1; age <= 9; age++)
            if (AchievementRules.CanClaim(g, p, age))
                list.Add(new AchieveAction(age));

        // Draw is always legal. Drawing off the top of the age-10 deck is
        // a valid (game-ending) action, so there's no gate here.
        list.Add(new DrawAction());

        // Dogma: one action per non-empty stack. The color identifies the
        // pile; the top card of that pile is what actually activates.
        foreach (CardColor c in Enum.GetValues<CardColor>())
            if (!p.Stack(c).IsEmpty)
                list.Add(new DogmaAction(c));

        // Meld: one action per distinct card in hand.
        foreach (var id in p.Hand)
            list.Add(new MeldAction(id));

        return list;
    }
}
