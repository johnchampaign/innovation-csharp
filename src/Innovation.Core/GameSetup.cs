namespace Innovation.Core;

/// <summary>
/// Creates a fresh <see cref="GameState"/> ready for turn 1. Mirrors the VB6
/// <c>initialize_game</c> + <c>launch_game</c> sequence:
///   1. Build the 10 age decks from the card catalog.
///   2. Shuffle each deck.
///   3. Reserve one card per age 1–9 as an achievement tile (removed from
///      the deck; the card's age becomes claimable).
///   4. Deal 2 cards to every player as their starting hand.
/// The caller is responsible for running the initial meld (lowest-title-
/// alphabetically goes first) via <see cref="TurnManager.CompleteInitialMeld"/>.
/// </summary>
public static class GameSetup
{
    public static GameState Create(IReadOnlyList<Card> cards, int numPlayers, Random? rng = null)
    {
        rng ??= new Random();
        var g = new GameState(cards, numPlayers);

        // Populate decks: each card goes into the deck of its age (1..10).
        foreach (var c in cards) g.Decks[c.Age].Add(c.Id);

        // Shuffle each deck in place (Fisher–Yates).
        for (int age = 1; age <= 10; age++)
            Shuffle(g.Decks[age], rng);

        // Reserve one card from each age 1..9 as the achievement tile. The
        // tile isn't a specific card face-up; the VB6 code simply drops the
        // top card of that deck to shorten it by one. Ages 1..9 are the only
        // claimable age achievements.
        for (int age = 1; age <= 9; age++)
        {
            if (g.Decks[age].Count > 0) g.Decks[age].RemoveAt(0);
            g.AvailableAgeAchievements.Add(age);
        }

        // Deal 2 cards to every player from the age-1 deck.
        foreach (var p in g.Players)
        {
            for (int k = 0; k < 2; k++)
            {
                if (g.Decks[1].Count == 0) break;
                int id = g.Decks[1][0];
                g.Decks[1].RemoveAt(0);
                p.Hand.Add(id);
            }
        }

        // Special achievements available at game start.
        foreach (var name in new[] { "Monument", "Empire", "World", "Wonder", "Universe" })
            g.AvailableSpecialAchievements.Add(name);

        g.Phase = GamePhase.Action;
        g.ActivePlayer = 0;
        g.CurrentTurn = 1;
        g.ActionsRemaining = 1;  // set properly by CompleteInitialMeld

        return g;
    }

    private static void Shuffle<T>(IList<T> list, Random rng)
    {
        for (int i = list.Count - 1; i > 0; i--)
        {
            int j = rng.Next(i + 1);
            (list[i], list[j]) = (list[j], list[i]);
        }
    }
}
