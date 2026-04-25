namespace Innovation.Core.Handlers;

/// <summary>
/// "Draw and score N [age]s." Simple composition — used by A.I. effect 1,
/// Software effect 1, The Internet effect 2, and any future card with the
/// same shape.
/// </summary>
public sealed class DrawAndScoreHandler : IDogmaHandler
{
    public int Count { get; }
    public int StartingAge { get; }

    public DrawAndScoreHandler(int count, int startingAge)
    {
        if (count < 1) throw new ArgumentOutOfRangeException(nameof(count));
        Count = count;
        StartingAge = startingAge;
    }

    public bool Execute(GameState g, PlayerState target, DogmaContext ctx)
    {
        bool any = false;
        for (int i = 0; i < Count; i++)
        {
            int id = Mechanics.DrawAndScore(g, target, StartingAge);
            if (id >= 0) any = true;
            if (g.IsGameOver) break;
        }
        return any;
    }
}
