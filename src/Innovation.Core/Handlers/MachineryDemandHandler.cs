namespace Innovation.Core.Handlers;

/// <summary>
/// Machinery (age 3, Yellow/Leaf) — demand: "I demand you exchange all
/// cards in your hand with all the highest cards in my hand!"
///
/// Target loses all hand cards; activator loses only the top-age slice
/// of their hand. Target receives that slice; activator receives the
/// target's whole hand. Transfer, not score or meld.
/// </summary>
public sealed class MachineryDemandHandler : IDogmaHandler
{
    public bool Execute(GameState g, PlayerState target, DogmaContext ctx)
    {
        var activator = g.Players[ctx.ActivatingPlayerIndex];

        var fromTarget = target.Hand.ToArray();
        int[] fromActivator;
        if (activator.Hand.Count == 0)
            fromActivator = Array.Empty<int>();
        else
        {
            int hi = activator.Hand.Max(id => g.Cards[id].Age);
            fromActivator = activator.Hand.Where(id => g.Cards[id].Age == hi).ToArray();
        }

        if (fromTarget.Length == 0 && fromActivator.Length == 0) return false;

        foreach (var id in fromTarget)
            Mechanics.TransferHandToHand(g, target, activator, id);
        foreach (var id in fromActivator)
            Mechanics.TransferHandToHand(g, activator, target, id);

        ctx.DemandSuccessful = true;
        return true;
    }
}
