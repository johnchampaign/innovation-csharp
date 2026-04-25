namespace Innovation.Core.Handlers;

/// <summary>
/// Paper (age 3, Green/Lightbulb) — effect 2: "Draw a 4 for every color
/// you have splayed left."
/// </summary>
public sealed class PaperDrawPerSplayHandler : IDogmaHandler
{
    public bool Execute(GameState g, PlayerState target, DogmaContext ctx)
    {
        int n = 0;
        foreach (var s in target.Stacks)
            if (s.Splay == Splay.Left) n++;
        if (n == 0) return false;

        for (int i = 0; i < n; i++)
        {
            Mechanics.DrawFromAge(g, target, 4);
            if (g.IsGameOver) return true;
        }
        return true;
    }
}
