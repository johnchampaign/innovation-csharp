namespace Innovation.Core.Handlers;

/// <summary>
/// Machine Tools (age 6, Red/Factory) — non-demand: "Draw and score
/// a card of value equal to the highest card in your score pile."
///
/// If the score pile is empty the value floor is age 1 (deterministic
/// fallback matching VB6 behavior — an empty score pile yields
/// "highest" = 0, then DrawFromAge walks up from age 1).
/// </summary>
public sealed class MachineToolsHandler : IDogmaHandler
{
    public bool Execute(GameState g, PlayerState target, DogmaContext ctx)
    {
        int age = target.ScorePile.Count == 0
            ? 1
            : target.ScorePile.Max(id => g.Cards[id].Age);
        int id = Mechanics.DrawAndScore(g, target, Math.Max(age, 1));
        return id >= 0;
    }
}
