using System.Text;
using Innovation.Core;
using Innovation.Core.Handlers;
using Xunit;

namespace Innovation.Tests;

/// <summary>
/// Regression tests for cards whose text says "draw and score" or
/// "draw and meld" — drawn cards must land in the score pile / on the
/// board, never in the hand. (Currency v0.1.1 had a `DrawFromAge` call
/// that forgot the follow-up `Score`, so cards landed in the hand.)
///
/// Each test runs the relevant handler end-to-end and snapshots the
/// hand size before vs. after. The property under test: the hand must
/// not gain any of the just-drawn cards.
/// </summary>
public class DrawAndScoreLandingTests
{
    static DrawAndScoreLandingTests()
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

    private static int IdOf(string title) => AllCards.Single(c => c.Title == title).Id;

    private static DogmaContext Ctx(int player = 0, Icon icon = Icon.Lightbulb) =>
        new(cardId: 0, activatingPlayerIndex: player, featuredIcon: icon);

    // ---------------------------------------------------------------
    // Currency — "draw and score a 2 for every different value returned"
    // ---------------------------------------------------------------

    [Fact]
    public void Currency_DrawnCardsLandInScorePile_NotHand()
    {
        var g = FreshDecks();
        var me = g.Players[0];

        // Hand: three distinct ages → expect 3 draw-and-scores at age 2.
        int a1 = IdOf("Pottery");      // age 1
        int a2 = IdOf("Currency");     // age 2 (not melded; just in hand)
        int a3 = IdOf("Engineering");  // age 3
        me.Hand.AddRange(new[] { a1, a2, a3 });
        int handBefore = me.Hand.Count;
        int scoreBefore = me.ScorePile.Count;

        var ctx = Ctx();
        var h = new CurrencyHandler();

        // First call posts the subset prompt.
        Assert.False(h.Execute(g, me, ctx));
        var req = (SelectHandCardSubsetRequest)ctx.PendingChoice!;
        req.ChosenCardIds = new[] { a1, a2, a3 };
        ctx.Paused = false;

        // 2+ returns → handler asks for return order.
        Assert.False(h.Execute(g, me, ctx));
        var orderReq = (SelectCardOrderRequest)ctx.PendingChoice!;
        orderReq.ChosenOrder = orderReq.CardIds.ToList();
        ctx.Paused = false;

        bool progressed = h.Execute(g, me, ctx);
        Assert.True(progressed);

        // 3 cards returned, 3 cards drawn-and-scored (one per distinct age):
        // hand count = before − 3 returned. Score pile gained 3.
        Assert.Equal(handBefore - 3, me.Hand.Count);
        Assert.Equal(scoreBefore + 3, me.ScorePile.Count);
    }

    // ---------------------------------------------------------------
    // Agriculture — "draw and score a card of value one higher"
    // ---------------------------------------------------------------

    [Fact]
    public void Agriculture_DrawnCardLandsInScorePile_NotHand()
    {
        var g = FreshDecks();
        var me = g.Players[0];

        int a3 = AllCards.First(c => c.Age == 3).Id;
        me.Hand.Add(a3);

        var ctx = Ctx(icon: Icon.Leaf);
        var h = new AgricultureHandler();

        Assert.False(h.Execute(g, me, ctx));
        var req = (SelectHandCardRequest)ctx.PendingChoice!;
        req.ChosenCardId = a3;
        ctx.Paused = false;

        Assert.True(h.Execute(g, me, ctx));

        // Returned the age-3, then drew-and-scored a 4. Hand is empty now.
        Assert.Empty(me.Hand);
        Assert.Single(me.ScorePile);
        Assert.Equal(4, g.Cards[me.ScorePile[0]].Age);
    }

    // ---------------------------------------------------------------
    // Pottery — "draw and score a card of value equal to the count returned"
    // ---------------------------------------------------------------

    [Fact]
    public void Pottery_DrawnCardLandsInScorePile_NotHand()
    {
        var g = FreshDecks();
        var me = g.Players[0];

        int a = AllCards.Where(c => c.Age == 1).Skip(0).First().Id;
        int b = AllCards.Where(c => c.Age == 1).Skip(1).First().Id;
        int c = AllCards.Where(c => c.Age == 1).Skip(2).First().Id;
        me.Hand.AddRange(new[] { a, b, c });

        var ctx = Ctx(icon: Icon.Leaf);
        var h = new PotteryReturnAndScoreHandler();

        Assert.False(h.Execute(g, me, ctx));
        var req = (SelectHandCardSubsetRequest)ctx.PendingChoice!;
        req.ChosenCardIds = new[] { a, b, c };
        ctx.Paused = false;

        // 3 returns → handler asks for return order.
        Assert.False(h.Execute(g, me, ctx));
        var orderReq = (SelectCardOrderRequest)ctx.PendingChoice!;
        orderReq.ChosenOrder = orderReq.CardIds.ToList();
        ctx.Paused = false;

        Assert.True(h.Execute(g, me, ctx));

        // Three returned → drew-and-scored a 3. Hand is empty.
        Assert.Empty(me.Hand);
        Assert.Single(me.ScorePile);
        Assert.Equal(3, g.Cards[me.ScorePile[0]].Age);
    }

    // ---------------------------------------------------------------
    // Clothing effect 2 — "draw and score a 1 for each unique color"
    // ---------------------------------------------------------------

    [Fact]
    public void Clothing_DrawnCardsLandInScorePile_NotHand()
    {
        var g = FreshDecks();
        var me = g.Players[0];

        // Meld two cards of two different colors, neither of which the
        // opponent has. Expect 2 draw-and-scores.
        int yellow = AllCards.First(c => c.Color == CardColor.Yellow && c.Age == 1).Id;
        int red    = AllCards.First(c => c.Color == CardColor.Red    && c.Age == 1).Id;
        me.Hand.Add(yellow); Mechanics.Meld(g, me, yellow);
        me.Hand.Add(red);    Mechanics.Meld(g, me, red);

        int handBefore = me.Hand.Count;
        int scoreBefore = me.ScorePile.Count;

        var ctx = Ctx(icon: Icon.Leaf);
        bool progressed = new ClothingDrawAndScoreUniqueColorsHandler().Execute(g, me, ctx);

        Assert.True(progressed);
        // No cards added to hand by the draw-and-scores.
        Assert.Equal(handBefore, me.Hand.Count);
        // Two unique colors → two age-1 cards added to score pile.
        Assert.Equal(scoreBefore + 2, me.ScorePile.Count);
        Assert.All(me.ScorePile, id => Assert.Equal(1, g.Cards[id].Age));
    }

    // ---------------------------------------------------------------
    // Tools effect 1 — "return 3, draw and meld a 3"
    // ---------------------------------------------------------------

    [Fact]
    public void Tools1_DrawnCardLandsOnBoard_NotHand()
    {
        var g = FreshDecks();
        var me = g.Players[0];

        int a = AllCards.Where(c => c.Age == 1).Skip(0).First().Id;
        int b = AllCards.Where(c => c.Age == 1).Skip(1).First().Id;
        int c = AllCards.Where(c => c.Age == 1).Skip(2).First().Id;
        me.Hand.AddRange(new[] { a, b, c });

        var ctx = Ctx();
        var h = new ToolsReturnThreeForMeldHandler();

        // Stage 1: yes/no — "return three to draw and meld a 3?"
        Assert.False(h.Execute(g, me, ctx));
        var yn = (YesNoChoiceRequest)ctx.PendingChoice!;
        yn.ChosenYes = true;
        ctx.Paused = false;

        // Stage 2: pick the three.
        Assert.False(h.Execute(g, me, ctx));
        var req = (SelectHandCardSubsetRequest)ctx.PendingChoice!;
        req.ChosenCardIds = new[] { a, b, c };
        ctx.Paused = false;

        Assert.True(h.Execute(g, me, ctx));

        // Hand emptied (3 returned), nothing in hand from the draw-and-meld.
        Assert.Empty(me.Hand);
        // The drawn 3 was melded — exactly one age-3 card on the board.
        int boardAge3Count = 0;
        foreach (CardColor col in Enum.GetValues<CardColor>())
        {
            var s = me.Stack(col);
            if (!s.IsEmpty && g.Cards[s.Top].Age == 3) boardAge3Count++;
        }
        Assert.Equal(1, boardAge3Count);
        Assert.Empty(me.ScorePile);
    }
}
