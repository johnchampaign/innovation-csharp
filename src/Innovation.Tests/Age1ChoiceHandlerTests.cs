using System.Text;
using Innovation.Core;
using Innovation.Core.Handlers;
using Xunit;

namespace Innovation.Tests;

/// <summary>
/// Tests for the three age-1 handlers that need player input: Pottery
/// (return subset + draw/score), Masonry (meld subset of castles), and
/// Code of Laws (multi-step tuck + optional splay). Covers direct handler
/// behaviour plus a sample of engine-level wiring.
/// </summary>
public class Age1ChoiceHandlerTests
{
    static Age1ChoiceHandlerTests()
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
    }

    private static IReadOnlyList<Card> AllCards => CardDataLoader.LoadFromEmbeddedResource();

    private static GameState FreshDecks(int players = 2)
    {
        var g = new GameState(AllCards, players);
        foreach (var c in AllCards) g.Decks[c.Age].Add(c.Id);
        foreach (var name in new[] { "Monument", "Empire", "World", "Wonder", "Universe" })
            g.AvailableSpecialAchievements.Add(name);
        g.Phase = GamePhase.Dogma;
        return g;
    }

    private static DogmaContext Ctx() => new(cardId: 0, activatingPlayerIndex: 0, featuredIcon: Icon.Leaf);

    // =========================================================================
    // Pottery (level 1: return 0-3, draw-and-score at count)
    // =========================================================================

    [Fact]
    public void Pottery_EmptyHand_NoOp()
    {
        var g = FreshDecks();
        var ctx = Ctx();

        bool progressed = new PotteryReturnAndScoreHandler().Execute(g, g.Players[0], ctx);

        Assert.False(progressed);
        Assert.Null(ctx.PendingChoice);
    }

    [Fact]
    public void Pottery_FirstCall_PausesWithSubsetRequest()
    {
        var g = FreshDecks();
        var age1 = g.Cards.First(c => c.Age == 1).Id;
        g.Players[0].Hand.Add(age1);

        var ctx = Ctx();
        new PotteryReturnAndScoreHandler().Execute(g, g.Players[0], ctx);

        var req = Assert.IsType<SelectHandCardSubsetRequest>(ctx.PendingChoice);
        Assert.Equal(0, req.MinCount);
        Assert.Equal(1, req.MaxCount);   // capped at hand size
        Assert.Contains(age1, req.EligibleCardIds);
    }

    [Fact]
    public void Pottery_Cap_Is3_EvenWithLargeHand()
    {
        var g = FreshDecks();
        for (int i = 0; i < 5; i++) g.Players[0].Hand.Add(g.Cards[i].Id);

        var ctx = Ctx();
        new PotteryReturnAndScoreHandler().Execute(g, g.Players[0], ctx);

        var req = (SelectHandCardSubsetRequest)ctx.PendingChoice!;
        Assert.Equal(3, req.MaxCount);
    }

    [Fact]
    public void Pottery_ReturnTwo_DrawAndScoresAge2()
    {
        var g = FreshDecks();
        // Put two distinct age-1 cards in hand.
        var handPair = g.Cards.Where(c => c.Age == 1).Take(2).Select(c => c.Id).ToList();
        g.Players[0].Hand.AddRange(handPair);
        // Remember what's on top of age-2.
        int topAge2 = g.Decks[2][0];

        var ctx = Ctx();
        var h = new PotteryReturnAndScoreHandler();
        h.Execute(g, g.Players[0], ctx);    // pause
        var req = (SelectHandCardSubsetRequest)ctx.PendingChoice!;
        req.ChosenCardIds = handPair;
        ctx.Paused = false;

        bool progressed = h.Execute(g, g.Players[0], ctx);

        Assert.True(progressed);
        // Hand empty; returned cards are at the bottom of age-1 deck.
        Assert.Empty(g.Players[0].Hand);
        Assert.Contains(handPair[0], g.Decks[1]);
        Assert.Contains(handPair[1], g.Decks[1]);
        // Age-2 draw was scored.
        Assert.Contains(topAge2, g.Players[0].ScorePile);
    }

    [Fact]
    public void Pottery_ReturnZero_NoProgressNoDraw()
    {
        var g = FreshDecks();
        var age1 = g.Cards.First(c => c.Age == 1).Id;
        g.Players[0].Hand.Add(age1);

        var ctx = Ctx();
        var h = new PotteryReturnAndScoreHandler();
        h.Execute(g, g.Players[0], ctx);
        var req = (SelectHandCardSubsetRequest)ctx.PendingChoice!;
        req.ChosenCardIds = Array.Empty<int>();
        ctx.Paused = false;

        bool progressed = h.Execute(g, g.Players[0], ctx);

        Assert.False(progressed);
        Assert.Contains(age1, g.Players[0].Hand);
        Assert.Empty(g.Players[0].ScorePile);
    }

    // =========================================================================
    // Masonry (meld subset of castles; 4+ ⇒ Monument)
    // =========================================================================

    [Fact]
    public void Masonry_NoCastlesInHand_NoOp()
    {
        var g = FreshDecks();
        // Agriculture (Yellow, no Castle icons) is a clean "no castle" card.
        var agri = g.Cards.Single(c => c.Title == "Agriculture").Id;
        g.Players[0].Hand.Add(agri);

        var ctx = Ctx();
        bool progressed = new MasonryHandler().Execute(g, g.Players[0], ctx);

        Assert.False(progressed);
        Assert.Null(ctx.PendingChoice);
    }

    [Fact]
    public void Masonry_OnlyCastleCards_AreEligible()
    {
        var g = FreshDecks();
        var agri = g.Cards.Single(c => c.Title == "Agriculture").Id;      // no Castle
        var metal = g.Cards.Single(c => c.Title == "Metalworking").Id;    // Castle
        var dom = g.Cards.Single(c => c.Title == "Domestication").Id;     // Castle
        g.Players[0].Hand.AddRange(new[] { agri, metal, dom });

        var ctx = Ctx();
        new MasonryHandler().Execute(g, g.Players[0], ctx);

        var req = (SelectHandCardSubsetRequest)ctx.PendingChoice!;
        Assert.Contains(metal, req.EligibleCardIds);
        Assert.Contains(dom, req.EligibleCardIds);
        Assert.DoesNotContain(agri, req.EligibleCardIds);
    }

    [Fact]
    public void Masonry_Meld4Castles_ClaimsMonument()
    {
        var g = FreshDecks();
        // Pick 4 castle-bearing cards.
        var castles = g.Cards
            .Where(c => c.Top == Icon.Castle || c.Left == Icon.Castle || c.Middle == Icon.Castle || c.Right == Icon.Castle)
            .Take(4)
            .Select(c => c.Id)
            .ToList();
        Assert.Equal(4, castles.Count);
        g.Players[0].Hand.AddRange(castles);

        var ctx = Ctx();
        var h = new MasonryHandler();
        h.Execute(g, g.Players[0], ctx);
        var req = (SelectHandCardSubsetRequest)ctx.PendingChoice!;
        req.ChosenCardIds = castles;
        ctx.Paused = false;

        bool progressed = h.Execute(g, g.Players[0], ctx);

        Assert.True(progressed);
        Assert.Contains("Monument", g.Players[0].SpecialAchievements);
        // All 4 are now melded.
        Assert.Empty(g.Players[0].Hand);
        Assert.Equal(4, g.Players[0].Stacks.Sum(s => s.Count));
    }

    [Fact]
    public void Masonry_Meld3Castles_DoesNotClaimMonument()
    {
        var g = FreshDecks();
        var castles = g.Cards
            .Where(c => c.Top == Icon.Castle || c.Left == Icon.Castle || c.Middle == Icon.Castle || c.Right == Icon.Castle)
            .Take(3)
            .Select(c => c.Id)
            .ToList();
        g.Players[0].Hand.AddRange(castles);

        var ctx = Ctx();
        var h = new MasonryHandler();
        h.Execute(g, g.Players[0], ctx);
        var req = (SelectHandCardSubsetRequest)ctx.PendingChoice!;
        req.ChosenCardIds = castles;
        ctx.Paused = false;

        h.Execute(g, g.Players[0], ctx);

        Assert.DoesNotContain("Monument", g.Players[0].SpecialAchievements);
        Assert.Contains("Monument", g.AvailableSpecialAchievements);
    }

    // =========================================================================
    // Code of Laws (tuck matching-color card, then optional splay-left)
    // =========================================================================

    [Fact]
    public void CodeOfLaws_NoMatchingColor_NoOp()
    {
        var g = FreshDecks();
        // Player has a Blue hand card but no piles.
        var blueCard = g.Cards.First(c => c.Color == CardColor.Blue).Id;
        g.Players[0].Hand.Add(blueCard);

        var ctx = Ctx();
        bool progressed = new CodeOfLawsHandler().Execute(g, g.Players[0], ctx);

        Assert.False(progressed);
        Assert.Null(ctx.PendingChoice);
    }

    [Fact]
    public void CodeOfLaws_FirstCall_OffersOnlyMatchingColors()
    {
        var g = FreshDecks();
        // Seed a Blue pile. Hand has a Blue card and a Red card.
        var seed = g.Cards.First(c => c.Color == CardColor.Blue);
        g.Players[0].Stack(CardColor.Blue).Meld(seed.Id);
        var blueHand = g.Cards.First(c => c.Color == CardColor.Blue && c.Id != seed.Id).Id;
        var redHand = g.Cards.First(c => c.Color == CardColor.Red).Id;
        g.Players[0].Hand.Add(blueHand);
        g.Players[0].Hand.Add(redHand);

        var ctx = Ctx();
        new CodeOfLawsHandler().Execute(g, g.Players[0], ctx);

        var req = Assert.IsType<SelectHandCardRequest>(ctx.PendingChoice);
        Assert.True(req.AllowNone);
        Assert.Contains(blueHand, req.EligibleCardIds);
        Assert.DoesNotContain(redHand, req.EligibleCardIds);
    }

    [Fact]
    public void CodeOfLaws_TuckAndSplayYes_AppliesBoth()
    {
        var g = FreshDecks();
        var seedA = g.Cards.Where(c => c.Color == CardColor.Blue).ElementAt(0);
        var seedB = g.Cards.Where(c => c.Color == CardColor.Blue).ElementAt(1);
        // Need two cards on the Blue pile so the post-tuck pile has 3 (splay needs ≥2).
        g.Players[0].Stack(CardColor.Blue).Meld(seedA.Id);
        g.Players[0].Stack(CardColor.Blue).Meld(seedB.Id);
        var blueHand = g.Cards.First(c => c.Color == CardColor.Blue && c.Id != seedA.Id && c.Id != seedB.Id).Id;
        g.Players[0].Hand.Add(blueHand);

        var ctx = Ctx();
        var h = new CodeOfLawsHandler();

        // Step 1 pause: pick the blue card.
        h.Execute(g, g.Players[0], ctx);
        var pick = (SelectHandCardRequest)ctx.PendingChoice!;
        pick.ChosenCardId = blueHand;
        ctx.Paused = false;

        // Resume into step 2: pause for yes/no.
        h.Execute(g, g.Players[0], ctx);
        Assert.Empty(g.Players[0].Hand);
        Assert.Contains(blueHand, g.Players[0].Stack(CardColor.Blue).Cards);
        var splayReq = Assert.IsType<YesNoChoiceRequest>(ctx.PendingChoice);
        splayReq.ChosenYes = true;
        ctx.Paused = false;

        // Final resume: applies the splay.
        h.Execute(g, g.Players[0], ctx);

        Assert.Equal(Splay.Left, g.Players[0].Stack(CardColor.Blue).Splay);
    }

    [Fact]
    public void CodeOfLaws_TuckAndSplayNo_LeavesPileUnsplayed()
    {
        var g = FreshDecks();
        var seed = g.Cards.First(c => c.Color == CardColor.Blue);
        g.Players[0].Stack(CardColor.Blue).Meld(seed.Id);
        var blueHand = g.Cards.First(c => c.Color == CardColor.Blue && c.Id != seed.Id).Id;
        g.Players[0].Hand.Add(blueHand);

        var ctx = Ctx();
        var h = new CodeOfLawsHandler();

        h.Execute(g, g.Players[0], ctx);
        ((SelectHandCardRequest)ctx.PendingChoice!).ChosenCardId = blueHand;
        ctx.Paused = false;

        h.Execute(g, g.Players[0], ctx);
        ((YesNoChoiceRequest)ctx.PendingChoice!).ChosenYes = false;
        ctx.Paused = false;

        h.Execute(g, g.Players[0], ctx);

        Assert.Equal(Splay.None, g.Players[0].Stack(CardColor.Blue).Splay);
        // Tuck still happened.
        Assert.Contains(blueHand, g.Players[0].Stack(CardColor.Blue).Cards);
    }

    [Fact]
    public void CodeOfLaws_DeclineFirstChoice_NoTuckNoSplay()
    {
        var g = FreshDecks();
        var seed = g.Cards.First(c => c.Color == CardColor.Blue);
        g.Players[0].Stack(CardColor.Blue).Meld(seed.Id);
        var blueHand = g.Cards.First(c => c.Color == CardColor.Blue && c.Id != seed.Id).Id;
        g.Players[0].Hand.Add(blueHand);

        var ctx = Ctx();
        var h = new CodeOfLawsHandler();

        h.Execute(g, g.Players[0], ctx);
        ((SelectHandCardRequest)ctx.PendingChoice!).ChosenCardId = null;   // decline
        ctx.Paused = false;

        bool progressed = h.Execute(g, g.Players[0], ctx);

        Assert.False(progressed);
        Assert.Contains(blueHand, g.Players[0].Hand);
        Assert.Null(ctx.HandlerState);
    }

    // =========================================================================
    // Engine smoke test across all three
    // =========================================================================

    [Fact]
    public void Registrations_PotteryAndMasonryAndCodeOfLaws_PauseViaEngine()
    {
        // End-to-end: each card should trigger its ChoiceRequest when played
        // through the DogmaEngine with a non-empty hand.
        foreach (var (title, expectedType) in new[]
        {
            ("Pottery",      typeof(SelectHandCardSubsetRequest)),
            ("Masonry",      typeof(SelectHandCardSubsetRequest)),
            ("Code of Laws", typeof(SelectHandCardRequest)),
        })
        {
            var g = FreshDecks();
            var registry = new CardRegistry(g.Cards);
            CardRegistrations.RegisterAll(registry, g.Cards);

            // Set up a hand that each card should find usable.
            int cardId = g.Cards.Single(c => c.Title == title).Id;

            if (title == "Code of Laws")
            {
                // Needs a matching-color pile.
                var seed = g.Cards.First(c => c.Color == CardColor.Blue);
                g.Players[0].Stack(CardColor.Blue).Meld(seed.Id);
                var hand = g.Cards.First(c => c.Color == CardColor.Blue && c.Id != seed.Id).Id;
                g.Players[0].Hand.Add(hand);
            }
            else if (title == "Masonry")
            {
                // Any card with a castle.
                var castle = g.Cards.First(c =>
                    c.Top == Icon.Castle || c.Left == Icon.Castle ||
                    c.Middle == Icon.Castle || c.Right == Icon.Castle).Id;
                g.Players[0].Hand.Add(castle);
            }
            else   // Pottery
            {
                g.Players[0].Hand.Add(g.Cards[0].Id);
            }

            var ctx = new DogmaEngine(g, registry).Execute(0, cardId);

            Assert.True(ctx.Paused);
            Assert.IsType(expectedType, ctx.PendingChoice);
            Assert.Equal(0, ctx.PendingChoice!.PlayerIndex);
        }
    }
}
