namespace Innovation.Core;

/// <summary>Mirrors the VB6 <c>phase</c> global state machine.</summary>
public enum GamePhase
{
    NewGame,
    Action,       // player choosing an action
    Dogma,        // resolving a dogma effect
    GameOver,
}

/// <summary>
/// Root game state. Mirrors the VB6 globals declared in AIFunctions.bas:
/// num_players, active_player, deck(), hand()/board()/score_pile()/splayed()
/// per player, plus achievement bookkeeping.
/// </summary>
public sealed class GameState
{
    public IReadOnlyList<Card> Cards { get; }

    public PlayerState[] Players { get; }

    /// <summary>
    /// Decks indexed 1..10 by age. Index 0 is unused (so ages match the
    /// game's 1-indexed naming). Card IDs, top of deck first.
    /// </summary>
    public List<int>[] Decks { get; }

    public int ActivePlayer { get; set; }
    public int CurrentTurn { get; set; }
    public int ActionsRemaining { get; set; }

    public GamePhase Phase { get; set; } = GamePhase.NewGame;

    /// <summary>Age-tile achievements still available in the pool.</summary>
    public List<int> AvailableAgeAchievements { get; } = new();

    /// <summary>Special achievements still available.</summary>
    public HashSet<string> AvailableSpecialAchievements { get; } = new(StringComparer.Ordinal);

    /// <summary>Set when the game ends; indexes of winning players.</summary>
    public List<int> Winners { get; } = new();

    public bool IsGameOver => Phase == GamePhase.GameOver;

    public GameState(IReadOnlyList<Card> cards, int numPlayers)
    {
        if (numPlayers < 2 || numPlayers > 4)
            throw new ArgumentOutOfRangeException(nameof(numPlayers), "Innovation supports 2–4 players.");
        Cards = cards;
        Players = Enumerable.Range(0, numPlayers).Select(i => new PlayerState(i)).ToArray();
        Decks = new List<int>[11];
        for (int i = 0; i <= 10; i++) Decks[i] = new List<int>();
    }

    public PlayerState Active => Players[ActivePlayer];

    /// <summary>
    /// Sum of ages in score pile, ×10, plus achievement count. Matches VB6
    /// end_game_10 formula (main.frm 7729).
    /// </summary>
    public int FinalScore(PlayerState p) => 10 * p.Score(Cards) + p.AchievementCount;

    /// <summary>
    /// Independent copy — used by AI controllers to try a move and score
    /// the resulting position without touching the live game. The
    /// immutable <see cref="Cards"/> catalog is shared (same reference);
    /// every mutable collection (decks, hands, boards, score piles,
    /// achievement pools, winners) is copied. Mirrors the purpose of VB6
    /// AIFunctions.bas copy_game_state / restore_game_state (lines 289–545),
    /// but returns a separate <see cref="GameState"/> instance rather than
    /// serializing into a shared Integer buffer.
    /// </summary>
    public GameState DeepClone()
    {
        var copy = new GameState(Cards, Players.Length);

        for (int i = 0; i < Players.Length; i++)
            copy.Players[i] = Players[i].DeepClone();

        for (int age = 0; age <= 10; age++)
        {
            copy.Decks[age].Clear();
            copy.Decks[age].AddRange(Decks[age]);
        }

        copy.ActivePlayer = ActivePlayer;
        copy.CurrentTurn = CurrentTurn;
        copy.ActionsRemaining = ActionsRemaining;
        copy.Phase = Phase;

        copy.AvailableAgeAchievements.Clear();
        copy.AvailableAgeAchievements.AddRange(AvailableAgeAchievements);

        copy.AvailableSpecialAchievements.Clear();
        foreach (var s in AvailableSpecialAchievements)
            copy.AvailableSpecialAchievements.Add(s);

        copy.Winners.Clear();
        copy.Winners.AddRange(Winners);

        return copy;
    }
}
