using System.Text;
using Innovation.Core;
using Innovation.Core.Handlers;
using Xunit;

namespace Innovation.Tests;

/// <summary>
/// Card-conservation invariants. At no point during a dogma should the
/// total number of card-id slots across all locations (hands, score piles,
/// achievements, board stacks, decks) lose or gain cards. The full set of
/// 105 ids must always be accounted for. Bugs that "vanish" cards (e.g.
/// removing from hand without putting on a stack) violate this.
///
/// These tests exercise the full dogma path for cards that have caused
/// such bugs in the past.
/// </summary>
public class CardConservationTests
{
    static CardConservationTests()
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

    /// <summary>
    /// Every card id must appear exactly once across all locations.
    /// Used as a post-condition check after any dogma effect.
    /// </summary>
    private static void AssertAllCardsAccountedFor(GameState g)
    {
        var seen = new HashSet<int>();
        var dupes = new List<int>();

        void Mark(int id)
        {
            if (!seen.Add(id)) dupes.Add(id);
        }

        for (int age = 1; age <= 10; age++)
            foreach (var id in g.Decks[age]) Mark(id);
        foreach (var p in g.Players)
        {
            foreach (var id in p.Hand) Mark(id);
            foreach (var id in p.ScorePile) Mark(id);
            foreach (var stack in p.Stacks)
                foreach (var id in stack.Cards) Mark(id);
        }
        // Note: AgeAchievements track age numbers, not card ids — achievement
        // tiles are anonymous (a card is removed from the deck during setup
        // but never reattached to a specific player's claim).

        Assert.Empty(dupes);
        Assert.Equal(g.Cards.Count, seen.Count);
    }

    [Fact]
    public void Classification_TwoMatchingColorCards_AllAccountedFor()
    {
        var g = FreshDecks();
        var me = g.Players[0];
        var opp = g.Players[1];

        // Player 1: two yellow cards in hand. One will be revealed; both will meld.
        var yellow1 = AllCards.First(c => c.Color == CardColor.Yellow && c.Age == 1).Id;
        var yellow2 = AllCards.First(c => c.Color == CardColor.Yellow && c.Age == 2).Id;
        me.Hand.AddRange(new[] { yellow1, yellow2 });

        // Opponent: one yellow card. Will transfer to me.
        var yellow3 = AllCards.First(c => c.Color == CardColor.Yellow && c.Age == 3).Id;
        opp.Hand.Add(yellow3);

        // Pull the test cards out of the decks so we don't double-count.
        g.Decks[1].Remove(yellow1);
        g.Decks[2].Remove(yellow2);
        g.Decks[3].Remove(yellow3);

        AssertAllCardsAccountedFor(g);

        var ctx = new DogmaContext(0, 0, Icon.Lightbulb);
        var h = new ClassificationHandler();

        // Stage 1: reveal a yellow card.
        Assert.False(h.Execute(g, me, ctx));
        var revealReq = (SelectHandCardRequest)ctx.PendingChoice!;
        revealReq.ChosenCardId = yellow1;
        ctx.Paused = false;

        // Stage 2: meld order pick (3 cards now after transfer).
        Assert.False(h.Execute(g, me, ctx));
        AssertAllCardsAccountedFor(g);   // mid-dogma invariant
        var orderReq = (SelectCardOrderRequest)ctx.PendingChoice!;
        Assert.Equal(3, orderReq.CardIds.Count);
        orderReq.ChosenOrder = orderReq.CardIds.ToList();
        ctx.Paused = false;

        // Stage 3: melds applied.
        Assert.True(h.Execute(g, me, ctx));

        AssertAllCardsAccountedFor(g);

        // All three yellow cards are on me's yellow stack.
        var yellowStack = me.Stack(CardColor.Yellow);
        Assert.Equal(3, yellowStack.Count);
        Assert.Contains(yellow1, yellowStack.Cards);
        Assert.Contains(yellow2, yellowStack.Cards);
        Assert.Contains(yellow3, yellowStack.Cards);
        Assert.Empty(me.Hand);
        Assert.Empty(opp.Hand);
    }

    private static void MoveDeckToHand(GameState g, PlayerState p, IEnumerable<int> ids)
    {
        foreach (var id in ids)
        {
            int age = g.Cards[id].Age;
            g.Decks[age].Remove(id);
            p.Hand.Add(id);
        }
    }

    [Fact]
    public void Masonry_ThreeMelds_NoVanishing()
    {
        var g = FreshDecks();
        var me = g.Players[0];
        var castles = AllCards.Where(c =>
            c.Top == Icon.Castle || c.Left == Icon.Castle ||
            c.Middle == Icon.Castle || c.Right == Icon.Castle).Take(3).Select(c => c.Id).ToList();
        MoveDeckToHand(g, me, castles);

        AssertAllCardsAccountedFor(g);

        var ctx = new DogmaContext(0, 0, Icon.Castle);
        var h = new MasonryHandler();
        Assert.False(h.Execute(g, me, ctx));
        var subset = (SelectHandCardSubsetRequest)ctx.PendingChoice!;
        subset.ChosenCardIds = castles;
        ctx.Paused = false;

        Assert.False(h.Execute(g, me, ctx));
        var orderReq = (SelectCardOrderRequest)ctx.PendingChoice!;
        orderReq.ChosenOrder = orderReq.CardIds.ToList();
        ctx.Paused = false;

        Assert.True(h.Execute(g, me, ctx));
        AssertAllCardsAccountedFor(g);
        Assert.Empty(me.Hand);
        Assert.Equal(3, me.Stacks.Sum(s => s.Count));
    }

    [Fact]
    public void Lighting_ThreeTucks_NoVanishing()
    {
        var g = FreshDecks();
        var me = g.Players[0];
        var hand = AllCards.Where(c => c.Age == 1).Take(3).Select(c => c.Id).ToList();
        MoveDeckToHand(g, me, hand);

        AssertAllCardsAccountedFor(g);

        var ctx = new DogmaContext(0, 0, Icon.Leaf);
        var h = new LightingHandler();
        Assert.False(h.Execute(g, me, ctx));
        var subset = (SelectHandCardSubsetRequest)ctx.PendingChoice!;
        subset.ChosenCardIds = hand;
        ctx.Paused = false;

        Assert.False(h.Execute(g, me, ctx));
        var orderReq = (SelectCardOrderRequest)ctx.PendingChoice!;
        orderReq.ChosenOrder = orderReq.CardIds.ToList();
        ctx.Paused = false;

        Assert.True(h.Execute(g, me, ctx));
        AssertAllCardsAccountedFor(g);
        Assert.Empty(me.Hand);
        Assert.Equal(3, me.Stacks.Sum(s => s.Count));
    }

    [Fact]
    public void Currency_TwoReturns_NoVanishing()
    {
        var g = FreshDecks();
        var me = g.Players[0];
        var hand = new[]
        {
            AllCards.First(c => c.Age == 1).Id,
            AllCards.First(c => c.Age == 2).Id,
        };
        MoveDeckToHand(g, me, hand);

        AssertAllCardsAccountedFor(g);

        var ctx = new DogmaContext(0, 0, Icon.Crown);
        var h = new CurrencyHandler();
        Assert.False(h.Execute(g, me, ctx));
        var subset = (SelectHandCardSubsetRequest)ctx.PendingChoice!;
        subset.ChosenCardIds = hand;
        ctx.Paused = false;

        Assert.False(h.Execute(g, me, ctx));
        var orderReq = (SelectCardOrderRequest)ctx.PendingChoice!;
        orderReq.ChosenOrder = orderReq.CardIds.ToList();
        ctx.Paused = false;

        Assert.True(h.Execute(g, me, ctx));
        AssertAllCardsAccountedFor(g);
    }

    [Fact]
    public void Industrialization_BatchedTucks_NoVanishing()
    {
        var g = FreshDecks();
        var me = g.Players[0];
        // Need 4 factories to draw-and-tuck twice. Stuff factory cards on
        // board and freeze the snapshot at activation.
        var factoryCards = AllCards.Where(c =>
            c.Top == Icon.Factory || c.Left == Icon.Factory ||
            c.Middle == Icon.Factory || c.Right == Icon.Factory).Take(4).ToList();
        MoveDeckToHand(g, me, factoryCards.Select(c => c.Id));
        foreach (var c in factoryCards)
            Mechanics.Meld(g, me, c.Id);

        AssertAllCardsAccountedFor(g);

        var ctx = new DogmaContext(0, 0, Icon.Factory);
        ctx.FrozenIconCounts = new[] { 4, 0 };   // direct test setup

        var h = new IndustrializationTuckHandler();
        Assert.False(h.Execute(g, me, ctx));
        var orderReq = (SelectCardOrderRequest)ctx.PendingChoice!;
        Assert.Equal(2, orderReq.CardIds.Count);
        orderReq.ChosenOrder = orderReq.CardIds.ToList();
        ctx.Paused = false;

        Assert.True(h.Execute(g, me, ctx));
        AssertAllCardsAccountedFor(g);
        // Two age-6s tucked under their respective color piles.
        int totalCardsOnBoard = me.Stacks.Sum(s => s.Count);
        Assert.Equal(factoryCards.Count + 2, totalCardsOnBoard);
    }

    [Fact]
    public void Classification_OneMatchingColor_NoVanishing()
    {
        var g = FreshDecks();
        var me = g.Players[0];

        var blue = AllCards.First(c => c.Color == CardColor.Blue && c.Age == 1).Id;
        me.Hand.Add(blue);
        g.Decks[1].Remove(blue);

        AssertAllCardsAccountedFor(g);

        var ctx = new DogmaContext(0, 0, Icon.Lightbulb);
        var h = new ClassificationHandler();
        Assert.False(h.Execute(g, me, ctx));
        var req = (SelectHandCardRequest)ctx.PendingChoice!;
        req.ChosenCardId = blue;
        ctx.Paused = false;

        // Single match → no order prompt; meld immediately.
        Assert.True(h.Execute(g, me, ctx));

        AssertAllCardsAccountedFor(g);
        Assert.Single(me.Stack(CardColor.Blue).Cards);
        Assert.Equal(blue, me.Stack(CardColor.Blue).Top);
        Assert.Empty(me.Hand);
    }
}
