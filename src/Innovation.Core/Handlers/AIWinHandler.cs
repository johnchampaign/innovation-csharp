namespace Innovation.Core.Handlers;

/// <summary>
/// A.I. (age 10, Purple/Lightbulb) — effect 2: "If Robotics and Software
/// are top cards on any board, the single player with the lowest score
/// wins."
/// </summary>
public sealed class AIWinHandler : IDogmaHandler
{
    public bool Execute(GameState g, PlayerState target, DogmaContext ctx)
    {
        bool roboticsTop = false, softwareTop = false;
        foreach (var p in g.Players)
        {
            foreach (var s in p.Stacks)
            {
                if (s.IsEmpty) continue;
                var t = g.Cards[s.Top].Title;
                if (t == "Robotics") roboticsTop = true;
                else if (t == "Software") softwareTop = true;
            }
        }
        if (!roboticsTop || !softwareTop) return false;

        int lo = g.Players.Min(p => p.Score(g.Cards));
        var lows = g.Players.Where(p => p.Score(g.Cards) == lo).ToArray();
        if (lows.Length != 1) return false;
        var winner = lows[0];
        GameLog.Log($"{GameLog.P(winner)} wins via A.I. — lowest score with Robotics+Software on table");
        g.Winners.Clear();
        g.Winners.Add(winner.Index);
        g.Phase = GamePhase.GameOver;
        return true;
    }
}
