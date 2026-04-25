namespace Innovation.Core.Handlers;

/// <summary>
/// Robotics (age 10, Red/Factory): "Score your top green card. Draw and meld
/// a 10, then execute its non-demand dogma effects for yourself only."
/// </summary>
public sealed class RoboticsHandler : IDogmaHandler
{
    public bool Execute(GameState g, PlayerState target, DogmaContext ctx)
    {
        var green = target.Stack(CardColor.Green);
        if (!green.IsEmpty)
            Mechanics.ScoreFromBoard(g, target, CardColor.Green, green.Top);
        if (g.IsGameOver) return true;

        int id = Mechanics.DrawAndMeld(g, target, 10);
        if (id < 0 || g.IsGameOver) return true;
        Mechanics.ExecuteSelfOnly(ctx, id, target);
        return true;
    }
}
