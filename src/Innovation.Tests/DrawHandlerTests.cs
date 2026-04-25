using System.Text;
using Innovation.Core;
using Innovation.Core.Handlers;
using Xunit;

namespace Innovation.Tests;

public class DrawHandlerTests
{
    static DrawHandlerTests()
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

    [Fact]
    public void DrawHandler_Default_DrawsOneCard_FromHighestTopFloor()
    {
        var g = FreshDecks();
        int handBefore = g.Players[0].Hand.Count;

        var h = new DrawHandler();
        var ctx = new DogmaContext(cardId: 0, activatingPlayerIndex: 0, featuredIcon: Icon.Leaf);
        bool progressed = h.Execute(g, g.Players[0], ctx);

        Assert.True(progressed);
        Assert.Equal(handBefore + 1, g.Players[0].Hand.Count);
    }

    [Fact]
    public void DrawHandler_WithStartingAge_DrawsFromThatDeck()
    {
        var g = FreshDecks();
        int expected = g.Decks[3][0];

        var h = new DrawHandler(count: 1, startingAge: 3);
        var ctx = new DogmaContext(0, 0, Icon.Leaf);
        h.Execute(g, g.Players[0], ctx);

        Assert.Contains(expected, g.Players[0].Hand);
        Assert.Equal(3, g.Cards[expected].Age);
    }

    [Fact]
    public void DrawHandler_WithStartingAge_WalksUpWhenDeckEmpty()
    {
        var g = FreshDecks();
        g.Decks[2].Clear();
        int expected = g.Decks[3][0];

        var h = new DrawHandler(count: 1, startingAge: 2);
        var ctx = new DogmaContext(0, 0, Icon.Leaf);
        h.Execute(g, g.Players[0], ctx);

        Assert.Equal(3, g.Cards[expected].Age);
        Assert.Contains(expected, g.Players[0].Hand);
    }

    [Fact]
    public void DrawHandler_MultipleCount_DrawsAllOfThem()
    {
        var g = FreshDecks();
        int handBefore = g.Players[0].Hand.Count;

        var h = new DrawHandler(count: 3);
        var ctx = new DogmaContext(0, 0, Icon.Leaf);
        h.Execute(g, g.Players[0], ctx);

        Assert.Equal(handBefore + 3, g.Players[0].Hand.Count);
    }

    [Fact]
    public void DrawHandler_StopsEarlyWhenGameEnds()
    {
        var g = FreshDecks();
        for (int i = 0; i <= 10; i++) g.Decks[i].Clear();

        var h = new DrawHandler(count: 3);
        var ctx = new DogmaContext(0, 0, Icon.Leaf);
        bool progressed = h.Execute(g, g.Players[0], ctx);

        Assert.False(progressed);
        Assert.Equal(GamePhase.GameOver, g.Phase);
    }

    // ---------- CardRegistrations ----------

    [Fact]
    public void Registrations_Writing_DrawsA2()
    {
        var g = FreshDecks();
        var registry = new CardRegistry(g.Cards);
        CardRegistrations.RegisterAll(registry, g.Cards);

        int writingId = g.Cards.Single(c => c.Title == "Writing").Id;
        // Sharer (player 1) resolves first, taking deck[2][0]; activator
        // then draws deck[2][1].
        int expected = g.Decks[2][1];

        new DogmaEngine(g, registry).Execute(0, writingId);

        Assert.Contains(expected, g.Players[0].Hand);
        Assert.Equal(2, g.Cards[expected].Age);
    }

    [Fact]
    public void Registrations_TheWheel_DrawsTwo1s()
    {
        var g = FreshDecks();
        var registry = new CardRegistry(g.Cards);
        CardRegistrations.RegisterAll(registry, g.Cards);

        int wheelId = g.Cards.Single(c => c.Title == "The Wheel").Id;
        // Sharer (player 1) draws first (deck[1][0], [1][1]); activator then
        // draws deck[1][2] and [1][3].
        var expected0 = g.Decks[1][2];
        var expected1 = g.Decks[1][3];

        new DogmaEngine(g, registry).Execute(0, wheelId);

        Assert.Contains(expected0, g.Players[0].Hand);
        Assert.Contains(expected1, g.Players[0].Hand);
        Assert.All(new[] { expected0, expected1 }, id => Assert.Equal(1, g.Cards[id].Age));
    }
}
