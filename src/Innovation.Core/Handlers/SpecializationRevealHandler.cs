namespace Innovation.Core.Handlers;

/// <summary>
/// Specialization (age 9, Purple/Factory) — effect 1: "Reveal a card from
/// your hand. Take into your hand the top card of that color from all other
/// players' boards."
/// </summary>
public sealed class SpecializationRevealHandler : IDogmaHandler
{
    public bool Execute(GameState g, PlayerState target, DogmaContext ctx)
    {
        if (target.Hand.Count == 0) return false;

        if (ctx.PendingChoice is null)
        {
            ctx.PendingChoice = new SelectHandCardRequest
            {
                Prompt = "Specialization: reveal a card from your hand — take its color's top card from every other player.",
                PlayerIndex = target.Index,
                EligibleCardIds = target.Hand.ToArray(),
                AllowNone = false,
            };
            ctx.Paused = true;
            return false;
        }

        var req = (SelectHandCardRequest)ctx.PendingChoice;
        ctx.PendingChoice = null;
        if (req.ChosenCardId is not int id) return false;

        var color = g.Cards[id].Color;
        GameLog.Log($"{GameLog.P(target)} reveals {GameLog.C(g, id)} (color={color})");

        foreach (var p in g.Players)
        {
            if (p.Index == target.Index) continue;
            var s = p.Stack(color);
            if (s.IsEmpty) continue;
            int top = s.PopTop();
            target.Hand.Add(top);
            GameLog.Log($"Specialization: {GameLog.C(g, top)} from {GameLog.P(p)} board → {GameLog.P(target)} hand");
        }
        SpecialAchievements.CheckAll(g);
        return true;
    }
}
