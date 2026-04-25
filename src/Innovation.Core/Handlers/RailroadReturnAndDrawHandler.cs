namespace Innovation.Core.Handlers;

/// <summary>
/// Railroad effect 1 (age 7, Purple/Clock): "Return all cards from your
/// hand, then draw three 6s." Auto — no choice.
/// </summary>
public sealed class RailroadReturnAndDrawHandler : IDogmaHandler
{
    public bool Execute(GameState g, PlayerState target, DogmaContext ctx)
    {
        var hand = target.Hand.ToList();
        foreach (var id in hand) Mechanics.Return(g, target, id);
        for (int i = 0; i < 3; i++)
        {
            if (g.IsGameOver) break;
            Mechanics.DrawFromAge(g, target, 6);
        }
        return true;
    }
}
