using System.Text;
using Innovation.Core;
using Innovation.Core.Handlers;
using Xunit;

namespace Innovation.Tests;

/// <summary>
/// Tests for Tools (two conditional-return effects) and Clothing (meld-a-
/// new-color + unique-color-score). These are the last two age-1 cards —
/// passing this suite completes Phase 4 age 1.
/// </summary>
public class ToolsAndClothingHandlerTests
{
    static ToolsAndClothingHandlerTests()
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

    private static DogmaContext Ctx() =>
        new(cardId: 0, activatingPlayerIndex: 0, featuredIcon: Icon.Lightbulb);

    // =========================================================================
    // Tools effect 1: return 3 to draw-and-meld a 3
    // =========================================================================

    [Fact]
    public void Tools1_HandBelowThree_NoOp()
    {
        var g = FreshDecks();
        g.Players[0].Hand.Add(g.Cards[0].Id);
        g.Players[0].Hand.Add(g.Cards[1].Id);

        var ctx = Ctx();
        bool progressed = new ToolsReturnThreeForMeldHandler().Execute(g, g.Players[0], ctx);

        Assert.False(progressed);
        Assert.Null(ctx.PendingChoice);
    }

    [Fact]
    public void Tools1_FirstCall_AsksYesNo()
    {
        var g = FreshDecks();
        for (int i = 0; i < 3; i++) g.Players[0].Hand.Add(g.Cards[i].Id);

        var ctx = Ctx();
        new ToolsReturnThreeForMeldHandler().Execute(g, g.Players[0], ctx);

        Assert.IsType<YesNoChoiceRequest>(ctx.PendingChoice);
    }

    [Fact]
    public void Tools1_DeclineYesNo_NoProgress()
    {
        var g = FreshDecks();
        for (int i = 0; i < 3; i++) g.Players[0].Hand.Add(g.Cards[i].Id);

        var ctx = Ctx();
        var h = new ToolsReturnThreeForMeldHandler();
        h.Execute(g, g.Players[0], ctx);
        ((YesNoChoiceRequest)ctx.PendingChoice!).ChosenYes = false;
        ctx.Paused = false;

        bool progressed = h.Execute(g, g.Players[0], ctx);

        Assert.False(progressed);
        Assert.Equal(3, g.Players[0].Hand.Count);   // nothing returned
    }

    [Fact]
    public void Tools1_YesThenPickThree_ReturnsAndMeldsAnAge3()
    {
        var g = FreshDecks();
        var hand = new[] { g.Cards[0].Id, g.Cards[1].Id, g.Cards[2].Id };
        g.Players[0].Hand.AddRange(hand);
        int topAge3 = g.Decks[3][0];

        var ctx = Ctx();
        var h = new ToolsReturnThreeForMeldHandler();

        // Step 1: yes/no
        h.Execute(g, g.Players[0], ctx);
        ((YesNoChoiceRequest)ctx.PendingChoice!).ChosenYes = true;
        ctx.Paused = false;

        // Step 2: subset
        h.Execute(g, g.Players[0], ctx);
        var subset = Assert.IsType<SelectHandCardSubsetRequest>(ctx.PendingChoice);
        Assert.Equal(3, subset.MinCount);
        Assert.Equal(3, subset.MaxCount);
        subset.ChosenCardIds = hand;
        ctx.Paused = false;

        bool progressed = h.Execute(g, g.Players[0], ctx);

        Assert.True(progressed);
        Assert.Empty(g.Players[0].Hand);
        // Three cards are back on age-1 deck.
        foreach (var id in hand) Assert.Contains(id, g.Decks[1]);
        // Age-3 top was melded onto the activator's board.
        var color3 = g.Cards[topAge3].Color;
        Assert.Equal(topAge3, g.Players[0].Stack(color3).Top);
    }

    // =========================================================================
    // Tools effect 2: return a 3 to draw three 1s
    // =========================================================================

    [Fact]
    public void Tools2_NoAge3InHand_NoOp()
    {
        var g = FreshDecks();
        // Hand of all age-1s.
        var ones = g.Cards.Where(c => c.Age == 1).Take(2).Select(c => c.Id).ToList();
        g.Players[0].Hand.AddRange(ones);

        var ctx = Ctx();
        bool progressed = new ToolsReturnThreeForOnesHandler().Execute(g, g.Players[0], ctx);

        Assert.False(progressed);
    }

    [Fact]
    public void Tools2_PickAge3_ReturnAndDrawThreeOnes()
    {
        var g = FreshDecks();
        int three = g.Cards.First(c => c.Age == 3).Id;
        g.Players[0].Hand.Add(three);

        var ctx = Ctx();
        var h = new ToolsReturnThreeForOnesHandler();
        h.Execute(g, g.Players[0], ctx);
        ((SelectHandCardRequest)ctx.PendingChoice!).ChosenCardId = three;
        ctx.Paused = false;

        bool progressed = h.Execute(g, g.Players[0], ctx);

        Assert.True(progressed);
        Assert.Contains(three, g.Decks[3]);
        Assert.Equal(3, g.Players[0].Hand.Count);
        Assert.All(g.Players[0].Hand, id => Assert.Equal(1, g.Cards[id].Age));
    }

    [Fact]
    public void Tools2_DeclinePick_NoProgress()
    {
        var g = FreshDecks();
        int three = g.Cards.First(c => c.Age == 3).Id;
        g.Players[0].Hand.Add(three);

        var ctx = Ctx();
        var h = new ToolsReturnThreeForOnesHandler();
        h.Execute(g, g.Players[0], ctx);
        ((SelectHandCardRequest)ctx.PendingChoice!).ChosenCardId = null;
        ctx.Paused = false;

        bool progressed = h.Execute(g, g.Players[0], ctx);

        Assert.False(progressed);
        Assert.Contains(three, g.Players[0].Hand);   // still there
    }

    // =========================================================================
    // Clothing effect 1: meld a card of a new-to-board color
    // =========================================================================

    [Fact]
    public void Clothing1_NoEligibleCard_NoOp()
    {
        var g = FreshDecks();
        // Seed every color on target's board.
        foreach (CardColor c in Enum.GetValues<CardColor>())
        {
            var card = g.Cards.First(card => card.Color == c);
            g.Players[0].Stack(c).Meld(card.Id);
        }
        // Add a hand card — its color is already on the board.
        g.Players[0].Hand.Add(g.Cards.Last(c => c.Color == CardColor.Blue).Id);

        var ctx = Ctx();
        bool progressed = new ClothingMeldDifferentColorHandler().Execute(g, g.Players[0], ctx);

        Assert.False(progressed);
    }

    [Fact]
    public void Clothing1_OffersOnlyNewColors()
    {
        var g = FreshDecks();
        // Seed only the Blue pile.
        var blueSeed = g.Cards.First(c => c.Color == CardColor.Blue);
        g.Players[0].Stack(CardColor.Blue).Meld(blueSeed.Id);

        // Hand has a Blue (already on board) and a Red (not on board).
        int blueHand = g.Cards.First(c => c.Color == CardColor.Blue && c.Id != blueSeed.Id).Id;
        int redHand = g.Cards.First(c => c.Color == CardColor.Red).Id;
        g.Players[0].Hand.AddRange(new[] { blueHand, redHand });

        var ctx = Ctx();
        new ClothingMeldDifferentColorHandler().Execute(g, g.Players[0], ctx);

        var req = Assert.IsType<SelectHandCardRequest>(ctx.PendingChoice);
        Assert.False(req.AllowNone);   // mandatory meld
        Assert.DoesNotContain(blueHand, req.EligibleCardIds);
        Assert.Contains(redHand, req.EligibleCardIds);
    }

    [Fact]
    public void Clothing1_MeldsPickedCard()
    {
        var g = FreshDecks();
        int red = g.Cards.First(c => c.Color == CardColor.Red).Id;
        g.Players[0].Hand.Add(red);

        var ctx = Ctx();
        var h = new ClothingMeldDifferentColorHandler();
        h.Execute(g, g.Players[0], ctx);
        ((SelectHandCardRequest)ctx.PendingChoice!).ChosenCardId = red;
        ctx.Paused = false;

        bool progressed = h.Execute(g, g.Players[0], ctx);

        Assert.True(progressed);
        Assert.Equal(red, g.Players[0].Stack(CardColor.Red).Top);
    }

    // =========================================================================
    // Clothing effect 2: draw-and-score a 1 per unique color
    // =========================================================================

    [Fact]
    public void Clothing2_NoUniqueColor_NoOp()
    {
        var g = FreshDecks();
        // Both players have Red.
        var reds = g.Cards.Where(c => c.Color == CardColor.Red).Take(2).ToList();
        g.Players[0].Stack(CardColor.Red).Meld(reds[0].Id);
        g.Players[1].Stack(CardColor.Red).Meld(reds[1].Id);

        var ctx = Ctx();
        bool progressed = new ClothingDrawAndScoreUniqueColorsHandler().Execute(g, g.Players[0], ctx);

        Assert.False(progressed);
        Assert.Empty(g.Players[0].ScorePile);
    }

    [Fact]
    public void Clothing2_TwoUniqueColors_ScoresTwoOnes()
    {
        var g = FreshDecks();
        // Target (p0) has Red and Green (both unique to them). Opponent has
        // Blue only.
        var r = g.Cards.First(c => c.Color == CardColor.Red);
        var gr = g.Cards.First(c => c.Color == CardColor.Green);
        var b = g.Cards.First(c => c.Color == CardColor.Blue);
        g.Players[0].Stack(CardColor.Red).Meld(r.Id);
        g.Players[0].Stack(CardColor.Green).Meld(gr.Id);
        g.Players[1].Stack(CardColor.Blue).Meld(b.Id);

        var ctx = Ctx();
        bool progressed = new ClothingDrawAndScoreUniqueColorsHandler().Execute(g, g.Players[0], ctx);

        Assert.True(progressed);
        // Two age-1 cards in score pile.
        Assert.Equal(2, g.Players[0].ScorePile.Count);
        Assert.All(g.Players[0].ScorePile, id => Assert.Equal(1, g.Cards[id].Age));
    }

    // =========================================================================
    // Engine-level smoke: both cards resolve (or pause) correctly via the
    // registry
    // =========================================================================

    [Fact]
    public void Tools_ViaEngine_FirstEffect_YesNoPauses()
    {
        var g = FreshDecks();
        for (int i = 0; i < 3; i++) g.Players[0].Hand.Add(g.Cards[i].Id);

        var registry = new CardRegistry(g.Cards);
        CardRegistrations.RegisterAll(registry, g.Cards);
        int tools = g.Cards.Single(c => c.Title == "Tools").Id;

        var ctx = new DogmaEngine(g, registry).Execute(0, tools);

        Assert.True(ctx.Paused);
        Assert.IsType<YesNoChoiceRequest>(ctx.PendingChoice);
        Assert.Equal(0, ctx.PendingChoice!.PlayerIndex);
    }

    [Fact]
    public void Clothing_ViaEngine_FirstEffect_PausesOnSelectHandCard()
    {
        var g = FreshDecks();
        int red = g.Cards.First(c => c.Color == CardColor.Red).Id;
        g.Players[0].Hand.Add(red);

        var registry = new CardRegistry(g.Cards);
        CardRegistrations.RegisterAll(registry, g.Cards);
        int clothing = g.Cards.Single(c => c.Title == "Clothing").Id;

        var ctx = new DogmaEngine(g, registry).Execute(0, clothing);

        Assert.True(ctx.Paused);
        Assert.IsType<SelectHandCardRequest>(ctx.PendingChoice);
    }
}
