using System.Text;
using Innovation.Core;
using Innovation.Core.Handlers;
using Xunit;

namespace Innovation.Tests;

/// <summary>
/// Tests for the four age-1 cards whose dogmas need no player choices:
/// Sailing (DrawAndMeldHandler), Mysticism, Metalworking, Domestication.
/// Each pair of tests covers handler behaviour directly plus the registered
/// wiring through <see cref="DogmaEngine"/>.
/// </summary>
public class Age1AutoHandlerTests
{
    static Age1AutoHandlerTests()
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
    }

    private static IReadOnlyList<Card> AllCards => CardDataLoader.LoadFromEmbeddedResource();

    /// <summary>Stocks every card into its age deck; hands empty.</summary>
    private static GameState FreshDecks(int players = 2)
    {
        var g = new GameState(AllCards, players);
        foreach (var c in AllCards) g.Decks[c.Age].Add(c.Id);
        g.Phase = GamePhase.Dogma;
        return g;
    }

    private static DogmaContext Ctx(int cardId, Icon featured = Icon.Castle) =>
        new(cardId, activatingPlayerIndex: 0, featuredIcon: featured);

    // ---------------- DrawAndMeldHandler (Sailing) ----------------

    [Fact]
    public void DrawAndMeld_MovesTopOfAgeDeck_IntoMatchingPile()
    {
        var g = FreshDecks();
        int age1Top = g.Decks[1][0];
        var expectedColor = g.Cards[age1Top].Color;

        var h = new DrawAndMeldHandler(count: 1, startingAge: 1);
        bool progressed = h.Execute(g, g.Players[0], Ctx(0));

        Assert.True(progressed);
        Assert.Empty(g.Players[0].Hand);                         // drawn card didn't stick in hand
        Assert.Equal(age1Top, g.Players[0].Stack(expectedColor).Top);
    }

    [Fact]
    public void Registered_Sailing_DrawsAndMeldsA1()
    {
        var g = FreshDecks();
        var registry = new CardRegistry(g.Cards);
        CardRegistrations.RegisterAll(registry, g.Cards);

        int sailingId = g.Cards.Single(c => c.Title == "Sailing").Id;
        // Sharer (player 1) resolves first per rulebook, so they take
        // deck[1][0]. The activator gets deck[1][1].
        int expected = g.Decks[1][1];
        var expectedColor = g.Cards[expected].Color;

        new DogmaEngine(g, registry).Execute(0, sailingId);

        Assert.Equal(expected, g.Players[0].Stack(expectedColor).Top);
    }

    // ---------------- Mysticism ----------------

    [Fact]
    public void Mysticism_DrawnColorMatchesBoard_MeldsAndDrawsAgain()
    {
        var g = FreshDecks();
        // Peek the top of age 1: whatever color that is, seed a pile of the
        // same color so the drawn card melds and triggers a second draw.
        int firstDraw = g.Decks[1][0];
        var color = g.Cards[firstDraw].Color;
        // Inject a sacrificial matching-color card directly onto the board.
        var sacrifice = g.Cards.First(c => c.Color == color && c.Id != firstDraw);
        g.Players[0].Stack(color).Meld(sacrifice.Id);

        int secondDraw = g.Decks[1][1];

        var h = new MysticismHandler();
        bool progressed = h.Execute(g, g.Players[0], Ctx(0));

        Assert.True(progressed);
        // firstDraw is melded (so top of its color pile), secondDraw sits in hand.
        Assert.Equal(firstDraw, g.Players[0].Stack(color).Top);
        Assert.Contains(secondDraw, g.Players[0].Hand);
    }

    [Fact]
    public void Mysticism_DrawnColorMissesBoard_StaysInHand()
    {
        var g = FreshDecks();
        int drawn = g.Decks[1][0];

        var h = new MysticismHandler();
        bool progressed = h.Execute(g, g.Players[0], Ctx(0));

        Assert.True(progressed);
        Assert.Contains(drawn, g.Players[0].Hand);
        // No pile created.
        foreach (var stack in g.Players[0].Stacks) Assert.True(stack.IsEmpty);
    }

    [Fact]
    public void Registered_Mysticism_ResolvesThroughEngine()
    {
        var g = FreshDecks();
        var registry = new CardRegistry(g.Cards);
        CardRegistrations.RegisterAll(registry, g.Cards);

        int mysticismId = g.Cards.Single(c => c.Title == "Mysticism").Id;
        // Sharer (player 1) resolves first and takes deck[1][0]; activator
        // then draws deck[1][1].
        int drawn = g.Decks[1][1];

        new DogmaEngine(g, registry).Execute(0, mysticismId);

        // Empty-board case: card goes to hand, no meld.
        Assert.Contains(drawn, g.Players[0].Hand);
    }

    // ---------------- Metalworking ----------------

    [Fact]
    public void Metalworking_ScoresCastleDraws_StopsAtFirstNonCastle()
    {
        // Build a custom age-1 deck: two Castle cards on top then one with no
        // Castle icon. The handler should score the first two, keep the third.
        var g = new GameState(AllCards, 2);
        g.Phase = GamePhase.Dogma;

        // Pick three deterministic cards by title (age 1 each):
        //   Metalworking has Castles in all slots,
        //   Mysticism has Castles in every icon slot,
        //   Agriculture has no Castle icon.
        int castleA = g.Cards.Single(c => c.Title == "Metalworking").Id;
        int castleB = g.Cards.Single(c => c.Title == "Mysticism").Id;
        int nonCastle = g.Cards.Single(c => c.Title == "Agriculture").Id;

        g.Decks[1].AddRange(new[] { castleA, castleB, nonCastle });

        var h = new MetalworkingHandler();
        bool progressed = h.Execute(g, g.Players[0], Ctx(0));

        Assert.True(progressed);
        // Two scored, one kept in hand, deck emptied of those three.
        Assert.Contains(castleA, g.Players[0].ScorePile);
        Assert.Contains(castleB, g.Players[0].ScorePile);
        Assert.Contains(nonCastle, g.Players[0].Hand);
        Assert.Empty(g.Decks[1]);
    }

    [Fact]
    public void Metalworking_FirstDrawNonCastle_KeepsItAndStops()
    {
        var g = new GameState(AllCards, 2);
        g.Phase = GamePhase.Dogma;

        int nonCastle = g.Cards.Single(c => c.Title == "Agriculture").Id;
        int nextUp    = g.Cards.Single(c => c.Title == "Metalworking").Id;
        g.Decks[1].AddRange(new[] { nonCastle, nextUp });

        var h = new MetalworkingHandler();
        h.Execute(g, g.Players[0], Ctx(0));

        Assert.Contains(nonCastle, g.Players[0].Hand);
        Assert.Empty(g.Players[0].ScorePile);
        // Loop exited before touching the second card.
        Assert.Equal(new[] { nextUp }, g.Decks[1]);
    }

    [Fact]
    public void Registered_Metalworking_WiredThroughEngine()
    {
        var g = FreshDecks();
        var registry = new CardRegistry(g.Cards);
        CardRegistrations.RegisterAll(registry, g.Cards);

        int metalId = g.Cards.Single(c => c.Title == "Metalworking").Id;
        int totalAge1Before = g.Decks[1].Count;

        new DogmaEngine(g, registry).Execute(0, metalId);

        // Handler should draw at least once; age-1 deck shrank.
        Assert.True(g.Decks[1].Count < totalAge1Before);
    }

    // ---------------- Domestication ----------------

    [Fact]
    public void Domestication_MeldsLowestAgeCardInHand_ThenDrawsA1()
    {
        var g = new GameState(AllCards, 2);
        g.Phase = GamePhase.Dogma;

        // Seed age 1 deck with a predictable next-draw card.
        int drawCard = g.Cards.Single(c => c.Title == "The Wheel").Id;
        g.Decks[1].Add(drawCard);

        // Hand: one age-3 card, one age-1 card. Lowest (age 1) should meld.
        int age3 = g.Cards.First(c => c.Age == 3).Id;
        int age1 = g.Cards.First(c => c.Age == 1 && c.Id != drawCard).Id;
        g.Players[0].Hand.Add(age3);
        g.Players[0].Hand.Add(age1);

        var age1Color = g.Cards[age1].Color;

        var h = new DomesticationHandler();
        bool progressed = h.Execute(g, g.Players[0], Ctx(0));

        Assert.True(progressed);
        Assert.Equal(age1, g.Players[0].Stack(age1Color).Top);   // melded
        Assert.Contains(age3, g.Players[0].Hand);                // untouched
        Assert.Contains(drawCard, g.Players[0].Hand);            // drawn
    }

    [Fact]
    public void Domestication_TiedLowestAges_PromptsPlayerToChoose()
    {
        var g = new GameState(AllCards, 2);
        g.Phase = GamePhase.Dogma;
        g.Decks[1].Add(g.Cards.Single(c => c.Title == "The Wheel").Id);

        var twoAge1s = g.Cards.Where(c => c.Age == 1).Take(2).ToList();
        g.Players[0].Hand.Add(twoAge1s[0].Id);
        g.Players[0].Hand.Add(twoAge1s[1].Id);

        var ctx = Ctx(0);
        var h = new DomesticationHandler();
        h.Execute(g, g.Players[0], ctx);

        Assert.True(ctx.Paused);
        var req = Assert.IsType<SelectHandCardRequest>(ctx.PendingChoice);
        Assert.Equal(new[] { twoAge1s[0].Id, twoAge1s[1].Id }.OrderBy(x => x),
                     req.EligibleCardIds.OrderBy(x => x));
        Assert.False(req.AllowNone);
    }

    [Fact]
    public void Domestication_EmptyHand_SkipsMeldAndStillDraws()
    {
        var g = FreshDecks();
        int expected = g.Decks[1][0];
        Assert.Empty(g.Players[0].Hand);

        var h = new DomesticationHandler();
        bool progressed = h.Execute(g, g.Players[0], Ctx(0));

        Assert.True(progressed);
        Assert.Contains(expected, g.Players[0].Hand);
        foreach (var stack in g.Players[0].Stacks) Assert.True(stack.IsEmpty);
    }

    [Fact]
    public void Registered_Domestication_ResolvesThroughEngine()
    {
        var g = FreshDecks();
        var registry = new CardRegistry(g.Cards);
        CardRegistrations.RegisterAll(registry, g.Cards);

        int domId = g.Cards.Single(c => c.Title == "Domestication").Id;
        // Seed a single hand card so it's the unambiguous lowest.
        var age2 = g.Cards.First(c => c.Age == 2);
        g.Players[0].Hand.Add(age2.Id);

        new DogmaEngine(g, registry).Execute(0, domId);

        // Key observable outcomes:
        //   • Player 0's age-2 card is melded on their board.
        //   • They drew at least one card. (The shared-bonus draw uses
        //     highest-top-card, which after melding age 2 is age 2, so the
        //     hand can mix age-1 and age-2 — only the Domestication draw
        //     itself is guaranteed to be age 1.)
        Assert.Equal(age2.Id, g.Players[0].Stack(age2.Color).Top);
        Assert.NotEmpty(g.Players[0].Hand);
        Assert.Contains(g.Players[0].Hand, id => g.Cards[id].Age == 1);
    }
}
