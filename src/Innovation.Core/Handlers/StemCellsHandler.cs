namespace Innovation.Core.Handlers;

/// <summary>
/// Stem Cells (age 10, Yellow/Leaf): "You may score all cards from your
/// hand. If you score one, you must score them all."
/// </summary>
public sealed class StemCellsHandler : IDogmaHandler
{
    public bool Execute(GameState g, PlayerState target, DogmaContext ctx)
    {
        if (target.Hand.Count == 0) return false;

        if (ctx.PendingChoice is null)
        {
            ctx.PendingChoice = new YesNoChoiceRequest
            {
                Prompt = $"Stem Cells: score all {target.Hand.Count} cards from your hand?",
                PlayerIndex = target.Index,
            };
            ctx.Paused = true;
            return false;
        }

        var yn = (YesNoChoiceRequest)ctx.PendingChoice;
        ctx.PendingChoice = null;
        if (!yn.ChosenYes) return false;

        foreach (var id in target.Hand.ToArray())
        {
            Mechanics.Score(g, target, id);
            if (g.IsGameOver) return true;
        }
        return true;
    }
}
