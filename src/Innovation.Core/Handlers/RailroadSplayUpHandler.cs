namespace Innovation.Core.Handlers;

/// <summary>
/// Railroad effect 2 (age 7, Purple/Clock): "You may splay up any one
/// color of your cards currently splayed right."
/// </summary>
public sealed class RailroadSplayUpHandler : IDogmaHandler
{
    public bool Execute(GameState g, PlayerState target, DogmaContext ctx)
    {
        if (ctx.PendingChoice is null)
        {
            var eligible = new List<CardColor>();
            for (int i = 0; i < target.Stacks.Length; i++)
                if (target.Stacks[i].Splay == Splay.Right) eligible.Add((CardColor)i);
            if (eligible.Count == 0) return false;
            ctx.PendingChoice = new SelectColorRequest
            {
                Prompt = "Railroad: splay one of your right-splayed colors up.",
                PlayerIndex = target.Index,
                EligibleColors = eligible,
                AllowNone = true,
            };
            ctx.Paused = true;
            return false;
        }

        var req = (SelectColorRequest)ctx.PendingChoice;
        ctx.PendingChoice = null;
        if (req.ChosenColor is not CardColor col) return false;
        return Mechanics.Splay(g, target, col, Splay.Up);
    }
}
