namespace Innovation.Core.Handlers;

/// <summary>
/// Genetics (age 9, Blue/Lightbulb): "Draw and meld a 10. Score all cards
/// beneath it."
/// </summary>
public sealed class GeneticsHandler : IDogmaHandler
{
    public bool Execute(GameState g, PlayerState target, DogmaContext ctx)
    {
        int id = Mechanics.DrawAndMeld(g, target, 10);
        if (id < 0 || g.IsGameOver) return true;

        var color = g.Cards[id].Color;
        var stack = target.Stack(color);

        // The melded card is now at the top. Everything else in the stack is
        // "beneath it" and gets scored.
        var beneath = new List<int>();
        for (int i = 1; i < stack.Count; i++) beneath.Add(stack.Cards[i]);

        foreach (var b in beneath)
        {
            Mechanics.ScoreFromBoard(g, target, color, b);
            if (g.IsGameOver) return true;
        }
        return true;
    }
}
