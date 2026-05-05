using System.Text;
using Innovation.Core;
using Innovation.Core.Handlers;
using Xunit;

namespace Innovation.Tests;

public class CompassHandlerTests
{
    static CompassHandlerTests()
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
    }

    private static IReadOnlyList<Card> AllCards => CardDataLoader.LoadFromEmbeddedResource();

    private static GameState FreshDecks(int players = 2)
    {
        var g = new GameState(AllCards, players);
        foreach (var c in AllCards) g.Decks[c.Age].Add(c.Id);
        g.Phase = GamePhase.Dogma;
        return g;
    }

    /// <summary>
    /// Both legs of Compass are chosen by the demand target. The activator
    /// doesn't pick anything — they're a passive recipient of the leaf
    /// card and a passive donor of the non-leaf card.
    /// </summary>
    [Fact]
    public void Compass_BothLegsAskTheTarget()
    {
        var g = FreshDecks();
        var activator = g.Players[0];
        var target = g.Players[1];

        // Target board: a non-green Leaf-iconed card to give in leg 1.
        var targetLeaf = AllCards.First(c =>
            c.Color != CardColor.Green
            && (c.Top == Icon.Leaf || c.Left == Icon.Leaf || c.Middle == Icon.Leaf || c.Right == Icon.Leaf)).Id;
        target.Hand.Add(targetLeaf);
        Mechanics.Meld(g, target, targetLeaf);

        // Activator board: a non-Leaf card to be taken in leg 2.
        var activatorNonLeaf = AllCards.First(c =>
            c.Top != Icon.Leaf && c.Left != Icon.Leaf
            && c.Middle != Icon.Leaf && c.Right != Icon.Leaf).Id;
        activator.Hand.Add(activatorNonLeaf);
        Mechanics.Meld(g, activator, activatorNonLeaf);

        var ctx = new DogmaContext(0, 0, Icon.Crown);
        var h = new CompassDemandHandler();

        // Phase 1 prompt — addressed to the target (defender of the demand).
        Assert.False(h.Execute(g, target, ctx));
        var leg1 = (SelectColorRequest)ctx.PendingChoice!;
        Assert.Equal(target.Index, leg1.PlayerIndex);
        leg1.ChosenColor = g.Cards[targetLeaf].Color;
        ctx.Paused = false;

        // Phase 2 prompt — also addressed to the target. The activator
        // never gets a turn at picking.
        h.Execute(g, target, ctx);
        Assert.NotNull(ctx.PendingChoice);
        var leg2 = (SelectColorRequest)ctx.PendingChoice!;
        Assert.Equal(target.Index, leg2.PlayerIndex);
        leg2.ChosenColor = g.Cards[activatorNonLeaf].Color;
        ctx.Paused = false;

        Assert.True(h.Execute(g, target, ctx));

        // Leaf card moved target → activator; non-leaf moved activator → target.
        Assert.Contains(targetLeaf, activator.Stack(g.Cards[targetLeaf].Color).Cards);
        Assert.Contains(activatorNonLeaf, target.Stack(g.Cards[activatorNonLeaf].Color).Cards);
    }
}
