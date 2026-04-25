namespace Innovation.Core.Handlers;

/// <summary>
/// Bioengineering (age 10, Blue/Lightbulb) — effect 2: "If any player has
/// fewer than three [Leaf] icons on their board, the single player with
/// the most [Leaf] icons on their board wins."
/// </summary>
public sealed class BioengineeringWinHandler : IDogmaHandler
{
    public bool Execute(GameState g, PlayerState target, DogmaContext ctx)
    {
        var counts = g.Players.Select(p => (p, n: IconCounter.Count(p, Icon.Leaf, g.Cards))).ToArray();
        if (!counts.Any(x => x.n < 3)) return false;

        int max = counts.Max(x => x.n);
        var tops = counts.Where(x => x.n == max).ToArray();
        if (tops.Length != 1) return false;
        var winner = tops[0].p;
        GameLog.Log($"{GameLog.P(winner)} wins via Bioengineering — most [Leaf] icons ({max})");
        g.Winners.Clear();
        g.Winners.Add(winner.Index);
        g.Phase = GamePhase.GameOver;
        return true;
    }
}
