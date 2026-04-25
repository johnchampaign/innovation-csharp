namespace Innovation.Core;

public sealed class PlayerState
{
    public int Index { get; }

    public List<int> Hand { get; } = new();
    public List<int> ScorePile { get; } = new();

    /// <summary>Five color stacks, indexed by <see cref="CardColor"/>.</summary>
    public ColorStack[] Stacks { get; } =
        Enumerable.Range(0, 5).Select(_ => new ColorStack()).ToArray();

    /// <summary>Achievement tile ages this player has claimed (1–9).</summary>
    public List<int> AgeAchievements { get; } = new();

    /// <summary>Special achievements claimed (Monument, Empire, etc.).</summary>
    public List<string> SpecialAchievements { get; } = new();

    /// <summary>
    /// Cards scored during the current turn. Reset by
    /// <see cref="TurnManager.AdvanceTurn"/>. Used for the Monument special
    /// achievement (6+ scores OR 6+ tucks in one turn). Mirrors VB6
    /// <c>scored_this_turn</c>.
    /// </summary>
    public int ScoredThisTurn { get; set; }

    /// <summary>Cards tucked during the current turn. Mirrors VB6 <c>tucked_this_turn</c>.</summary>
    public int TuckedThisTurn { get; set; }

    public PlayerState(int index) { Index = index; }

    /// <summary>Total ages of all cards in the score pile. Mirrors VB6 scores().</summary>
    public int Score(IReadOnlyList<Card> cards) => ScorePile.Sum(id => cards[id].Age);

    /// <summary>Number of achievements (age + special). Mirrors VB6 vps().</summary>
    public int AchievementCount => AgeAchievements.Count + SpecialAchievements.Count;

    public ColorStack Stack(CardColor c) => Stacks[(int)c];

    /// <summary>
    /// Independent copy — used by <see cref="GameState.DeepClone"/> for AI
    /// look-ahead. All mutable collections are copied; the clone shares no
    /// list references with the original.
    /// </summary>
    public PlayerState DeepClone()
    {
        var copy = new PlayerState(Index);
        copy.Hand.AddRange(Hand);
        copy.ScorePile.AddRange(ScorePile);
        for (int i = 0; i < Stacks.Length; i++)
            copy.Stacks[i] = Stacks[i].DeepClone();
        copy.AgeAchievements.AddRange(AgeAchievements);
        copy.SpecialAchievements.AddRange(SpecialAchievements);
        copy.ScoredThisTurn = ScoredThisTurn;
        copy.TuckedThisTurn = TuckedThisTurn;
        return copy;
    }
}
