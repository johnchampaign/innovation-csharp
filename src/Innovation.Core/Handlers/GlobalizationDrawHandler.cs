namespace Innovation.Core.Handlers;

/// <summary>
/// Globalization (age 10, Yellow/Factory) — effect 2: "Draw and score a 6.
/// If no player has more [Leaf] icons than [Factory] icons on their board,
/// the single player with the most points wins."
/// </summary>
public sealed class GlobalizationDrawHandler : IDogmaHandler
{
    public bool Execute(GameState g, PlayerState target, DogmaContext ctx)
    {
        Mechanics.DrawAndScore(g, target, 6);
        if (g.IsGameOver) return true;

        foreach (var p in g.Players)
        {
            int leaf = IconCounter.Count(p, Icon.Leaf, g.Cards);
            int fac = IconCounter.Count(p, Icon.Factory, g.Cards);
            if (leaf > fac) return true;
        }

        int max = g.Players.Max(p => p.Score(g.Cards));
        var tops = g.Players.Where(p => p.Score(g.Cards) == max).ToArray();
        if (tops.Length != 1) return true;
        var winner = tops[0];
        GameLog.Log($"{GameLog.P(winner)} wins via Globalization — most points, no player Leaf>Factory");
        g.Winners.Clear();
        g.Winners.Add(winner.Index);
        g.Phase = GamePhase.GameOver;
        return true;
    }
}
