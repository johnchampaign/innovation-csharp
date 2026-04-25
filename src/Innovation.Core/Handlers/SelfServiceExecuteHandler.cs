namespace Innovation.Core.Handlers;

/// <summary>
/// Self Service (age 10, Green/Crown) — effect 1: "Execute the non-demand
/// dogma effects of any other top card on your board for yourself only."
/// </summary>
public sealed class SelfServiceExecuteHandler : IDogmaHandler
{
    public bool Execute(GameState g, PlayerState target, DogmaContext ctx)
    {
        if (ctx.PendingChoice is SelectHandCardRequest prior)
        {
            ctx.PendingChoice = null;
            if (prior.ChosenCardId is int chosen)
                Mechanics.ExecuteSelfOnly(ctx, chosen, target);
            return true;
        }

        var eligible = new List<int>();
        foreach (var s in target.Stacks)
        {
            if (s.IsEmpty) continue;
            if (g.Cards[s.Top].Title == "Self Service") continue;
            eligible.Add(s.Top);
        }
        if (eligible.Count == 0) return false;

        ctx.PendingChoice = new SelectHandCardRequest
        {
            Prompt = "Self Service: choose another top card on your board to execute for yourself only.",
            PlayerIndex = target.Index,
            EligibleCardIds = eligible,
            AllowNone = false,
        };
        ctx.Paused = true;
        return true;
    }
}
