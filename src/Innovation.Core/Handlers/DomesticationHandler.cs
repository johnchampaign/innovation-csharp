namespace Innovation.Core.Handlers;

/// <summary>
/// Domestication (age 1, Yellow/Castle): "Meld the lowest card in your hand.
/// Draw a 1."
///
/// When multiple hand cards share the lowest age, the player chooses which
/// to meld (standard Innovation tiebreak for "lowest card" effects). If the
/// hand is empty, skip the meld and just draw.
/// </summary>
public sealed class DomesticationHandler : IDogmaHandler
{
    public bool Execute(GameState g, PlayerState target, DogmaContext ctx)
    {
        if (ctx.PendingChoice is null)
        {
            if (target.Hand.Count == 0)
            {
                int drawnOnly = Mechanics.DrawFromAge(g, target, 1);
                return drawnOnly >= 0;
            }

            int lowestAge = target.Hand.Min(id => g.Cards[id].Age);
            var eligible = target.Hand.Where(id => g.Cards[id].Age == lowestAge).ToArray();

            if (eligible.Length == 1)
            {
                Mechanics.Meld(g, target, eligible[0]);
                if (g.IsGameOver) return true;
                Mechanics.DrawFromAge(g, target, 1);
                return true;
            }

            ctx.PendingChoice = new SelectHandCardRequest
            {
                Prompt = "Domestication: choose a lowest-age card to meld.",
                PlayerIndex = target.Index,
                EligibleCardIds = eligible,
                AllowNone = false,
            };
            ctx.Paused = true;
            return false;
        }

        var req = (SelectHandCardRequest)ctx.PendingChoice;
        ctx.PendingChoice = null;
        if (req.ChosenCardId is not int chosen) return false;

        Mechanics.Meld(g, target, chosen);
        if (g.IsGameOver) return true;
        Mechanics.DrawFromAge(g, target, 1);
        return true;
    }
}
