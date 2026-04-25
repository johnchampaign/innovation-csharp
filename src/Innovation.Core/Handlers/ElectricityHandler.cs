namespace Innovation.Core.Handlers;

/// <summary>
/// Electricity (age 7, Green/Factory): "Return all your top cards without
/// a Factory, then draw an 8 for each card you returned." Auto — no choice.
/// </summary>
public sealed class ElectricityHandler : IDogmaHandler
{
    public bool Execute(GameState g, PlayerState target, DogmaContext ctx)
    {
        int returned = 0;
        foreach (var stack in target.Stacks)
        {
            if (stack.IsEmpty) continue;
            int topId = stack.Top;
            if (Mechanics.HasIcon(g.Cards[topId], Icon.Factory)) continue;
            stack.PopTop();
            g.Decks[g.Cards[topId].Age].Add(topId);
            GameLog.Log($"{GameLog.P(target)} returns top {GameLog.C(g, topId)}");
            returned++;
        }
        for (int i = 0; i < returned; i++) Mechanics.DrawFromAge(g, target, 8);
        return returned > 0;
    }
}
