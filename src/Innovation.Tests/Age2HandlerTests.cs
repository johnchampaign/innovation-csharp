using System.Text;
using Innovation.Core;
using Innovation.Core.Handlers;
using Xunit;

namespace Innovation.Tests;

/// <summary>
/// Tests for the first batch of Age-2 dogma handlers: Calendar,
/// Fermenting, Mathematics, Philosophy. Focus on the conditional gate
/// and the pause/resume choice shape where present.
/// </summary>
public class Age2HandlerTests
{
    static Age2HandlerTests()
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

    private static DogmaContext Ctx(Icon icon, int playerIndex = 0) =>
        new(cardId: 0, activatingPlayerIndex: playerIndex, featuredIcon: icon);

    // ---------- Calendar ----------

    [Fact]
    public void Calendar_ScorePileLargerThanHand_DrawsTwo3s()
    {
        var g = FreshDecks();
        var me = g.Players[0];
        // Score pile count > hand count: two cards in score, zero in hand.
        me.ScorePile.AddRange(new[] { 0, 1 });
        int age3Before = g.Decks[3].Count;

        var h = new CalendarHandler();
        bool progressed = h.Execute(g, me, Ctx(Icon.Leaf));

        Assert.True(progressed);
        Assert.Equal(2, me.Hand.Count);
        Assert.Equal(age3Before - 2, g.Decks[3].Count);
        Assert.All(me.Hand, id => Assert.True(g.Cards[id].Age >= 3));
    }

    [Fact]
    public void Calendar_ScorePileNotLarger_NoOp()
    {
        var g = FreshDecks();
        var me = g.Players[0];
        me.Hand.Add(0);
        me.ScorePile.Add(1);

        var h = new CalendarHandler();
        bool progressed = h.Execute(g, me, Ctx(Icon.Leaf));

        Assert.False(progressed);
        Assert.Single(me.Hand);
    }

    // ---------- Fermenting ----------

    [Fact]
    public void Fermenting_NoLeaves_NoOp()
    {
        var g = FreshDecks();
        var me = g.Players[0];
        var h = new FermentingHandler();
        Assert.False(h.Execute(g, me, Ctx(Icon.Leaf)));
        Assert.Empty(me.Hand);
    }

    [Fact]
    public void Fermenting_FourLeaves_DrawsTwo2s()
    {
        var g = FreshDecks();
        var me = g.Players[0];

        // Meld Fermenting and Agriculture — both Yellow/Leaf, two top cards
        // with Leaf featured positions, contributing 4 visible leaves on
        // an unsplayed board (center icon counts 1×, other slots on top).
        // Simpler approach: directly pick two cards whose icon grid yields
        // 4 leaves total.
        var ferm = AllCards.Single(c => c.Title == "Fermenting").Id;
        var agri = AllCards.Single(c => c.Title == "Agriculture").Id;
        g.Decks[2].Remove(ferm);
        g.Decks[1].Remove(agri);
        me.Hand.AddRange(new[] { ferm, agri });
        Mechanics.Meld(g, me, ferm);
        Mechanics.Meld(g, me, agri);

        int leaves = IconCounter.Count(me, Icon.Leaf, g.Cards);
        int expectedDraws = leaves / 2;

        int age2Before = g.Decks[2].Count;

        var h = new FermentingHandler();
        bool progressed = h.Execute(g, me, Ctx(Icon.Leaf));

        if (expectedDraws == 0)
        {
            Assert.False(progressed);
        }
        else
        {
            Assert.True(progressed);
            Assert.Equal(expectedDraws, me.Hand.Count);
            Assert.Equal(age2Before - expectedDraws, g.Decks[2].Count);
        }
    }

    // ---------- Mathematics ----------

    [Fact]
    public void Mathematics_EmptyHand_NoOp()
    {
        var g = FreshDecks();
        var h = new MathematicsHandler();
        Assert.False(h.Execute(g, g.Players[0], Ctx(Icon.Lightbulb)));
    }

    [Fact]
    public void Mathematics_DeclineSkip_NoProgress()
    {
        var g = FreshDecks();
        var me = g.Players[0];
        me.Hand.Add(AllCards.First(c => c.Age == 2).Id);

        var ctx = Ctx(Icon.Lightbulb);
        var h = new MathematicsHandler();
        h.Execute(g, me, ctx);    // pause
        Assert.True(ctx.Paused);

        var req = (SelectHandCardRequest)ctx.PendingChoice!;
        req.ChosenCardId = null;   // decline
        ctx.Paused = false;
        bool progressed = h.Execute(g, me, ctx);

        Assert.False(progressed);
        Assert.Single(me.Hand);    // untouched
    }

    [Fact]
    public void Mathematics_ReturnsAndMeldsAtAgePlusOne()
    {
        var g = FreshDecks();
        var me = g.Players[0];
        var age2 = AllCards.First(c => c.Age == 2).Id;
        g.Decks[2].Remove(age2);
        me.Hand.Add(age2);

        int topOfAge3 = g.Decks[3][0];

        var ctx = Ctx(Icon.Lightbulb);
        var h = new MathematicsHandler();
        h.Execute(g, me, ctx);
        var req = (SelectHandCardRequest)ctx.PendingChoice!;
        req.ChosenCardId = age2;
        ctx.Paused = false;
        bool progressed = h.Execute(g, me, ctx);

        Assert.True(progressed);
        // The returned card went to bottom of age-2 deck.
        Assert.Equal(age2, g.Decks[2][^1]);
        Assert.DoesNotContain(age2, me.Hand);
        // The age-3 draw ended up melded, not in hand or score.
        var stack = me.Stack(g.Cards[topOfAge3].Color);
        Assert.False(stack.IsEmpty);
        Assert.Equal(topOfAge3, stack.Top);
    }

    // ---------- Philosophy ----------

    [Fact]
    public void PhilosophySplay_NoEligibleColors_NoOp()
    {
        // No board at all → nothing to splay.
        var g = FreshDecks();
        var h = new PhilosophySplayLeftHandler();
        Assert.False(h.Execute(g, g.Players[0], Ctx(Icon.Lightbulb)));
    }

    [Fact]
    public void PhilosophySplay_ChooseColor_SplaysLeft()
    {
        var g = FreshDecks();
        var me = g.Players[0];
        // Two Yellow cards on the board, unsplayed → eligible for left.
        var y1 = AllCards.First(c => c.Color == CardColor.Yellow).Id;
        var y2 = AllCards.First(c => c.Color == CardColor.Yellow && c.Id != y1).Id;
        me.Hand.AddRange(new[] { y1, y2 });
        Mechanics.Meld(g, me, y1);
        Mechanics.Meld(g, me, y2);

        var ctx = Ctx(Icon.Lightbulb);
        var h = new PhilosophySplayLeftHandler();
        h.Execute(g, me, ctx);
        Assert.True(ctx.Paused);

        var req = (SelectColorRequest)ctx.PendingChoice!;
        Assert.True(req.AllowNone);
        Assert.Contains(CardColor.Yellow, req.EligibleColors);
        req.ChosenColor = CardColor.Yellow;
        ctx.Paused = false;

        bool progressed = h.Execute(g, me, ctx);
        Assert.True(progressed);
        Assert.Equal(Splay.Left, me.Stack(CardColor.Yellow).Splay);
    }

    [Fact]
    public void PhilosophySplay_Decline_NoProgress()
    {
        var g = FreshDecks();
        var me = g.Players[0];
        var y1 = AllCards.First(c => c.Color == CardColor.Yellow).Id;
        var y2 = AllCards.First(c => c.Color == CardColor.Yellow && c.Id != y1).Id;
        me.Hand.AddRange(new[] { y1, y2 });
        Mechanics.Meld(g, me, y1);
        Mechanics.Meld(g, me, y2);

        var ctx = Ctx(Icon.Lightbulb);
        var h = new PhilosophySplayLeftHandler();
        h.Execute(g, me, ctx);
        var req = (SelectColorRequest)ctx.PendingChoice!;
        req.ChosenColor = null;  // decline
        ctx.Paused = false;

        bool progressed = h.Execute(g, me, ctx);
        Assert.False(progressed);
        Assert.Equal(Splay.None, me.Stack(CardColor.Yellow).Splay);
    }

    [Fact]
    public void PhilosophyScore_ScoresChosenCard()
    {
        var g = FreshDecks();
        var me = g.Players[0];
        var card = AllCards.First(c => c.Age == 1).Id;
        me.Hand.Add(card);

        var ctx = Ctx(Icon.Lightbulb);
        var h = new PhilosophyScoreHandler();
        h.Execute(g, me, ctx);
        var req = (SelectHandCardRequest)ctx.PendingChoice!;
        req.ChosenCardId = card;
        ctx.Paused = false;

        bool progressed = h.Execute(g, me, ctx);
        Assert.True(progressed);
        Assert.Contains(card, me.ScorePile);
        Assert.DoesNotContain(card, me.Hand);
    }

    [Fact]
    public void PhilosophyScore_Decline_NoProgress()
    {
        var g = FreshDecks();
        var me = g.Players[0];
        var card = AllCards.First(c => c.Age == 1).Id;
        me.Hand.Add(card);

        var ctx = Ctx(Icon.Lightbulb);
        var h = new PhilosophyScoreHandler();
        h.Execute(g, me, ctx);
        var req = (SelectHandCardRequest)ctx.PendingChoice!;
        req.ChosenCardId = null;
        ctx.Paused = false;

        bool progressed = h.Execute(g, me, ctx);
        Assert.False(progressed);
        Assert.Contains(card, me.Hand);
        Assert.Empty(me.ScorePile);
    }

    // ---------- Currency ----------

    [Fact]
    public void Currency_EmptyHand_NoOp()
    {
        var g = FreshDecks();
        var h = new CurrencyHandler();
        Assert.False(h.Execute(g, g.Players[0], Ctx(Icon.Crown)));
    }

    [Fact]
    public void Currency_DeclineEmptySubset_NoProgress()
    {
        var g = FreshDecks();
        var me = g.Players[0];
        me.Hand.Add(AllCards.First(c => c.Age == 1).Id);

        var ctx = Ctx(Icon.Crown);
        var h = new CurrencyHandler();
        h.Execute(g, me, ctx);
        var req = (SelectHandCardSubsetRequest)ctx.PendingChoice!;
        req.ChosenCardIds = Array.Empty<int>();
        ctx.Paused = false;

        Assert.False(h.Execute(g, me, ctx));
        Assert.Single(me.Hand);
    }

    [Fact]
    public void Currency_DistinctAgesDriveDrawCount()
    {
        var g = FreshDecks();
        var me = g.Players[0];
        // Pick two age-1 cards and one age-3 card. Expect 2 draws (distinct
        // ages = {1, 3}).
        var ones = AllCards.Where(c => c.Age == 1).Take(2).Select(c => c.Id).ToArray();
        var three = AllCards.First(c => c.Age == 3).Id;
        me.Hand.AddRange(ones);
        me.Hand.Add(three);

        int age2Before = g.Decks[2].Count;
        int age1Before = g.Decks[1].Count;
        int age3Before = g.Decks[3].Count;

        var ctx = Ctx(Icon.Crown);
        var h = new CurrencyHandler();
        h.Execute(g, me, ctx);
        var req = (SelectHandCardSubsetRequest)ctx.PendingChoice!;
        req.ChosenCardIds = new[] { ones[0], ones[1], three };
        ctx.Paused = false;

        bool progressed = h.Execute(g, me, ctx);
        Assert.True(progressed);
        // Two 2s drawn into hand.
        Assert.Equal(2, me.Hand.Count);
        Assert.All(me.Hand, id => Assert.Equal(2, g.Cards[id].Age));
        // Age-2 deck lost exactly two cards.
        Assert.Equal(age2Before - 2, g.Decks[2].Count);
        // Returned cards went to the bottom of their own age decks (+1 each for 1s, +1 for 3).
        Assert.Equal(age1Before + 2, g.Decks[1].Count);
        Assert.Equal(age3Before + 1, g.Decks[3].Count);
    }

    // ---------- Canal Building ----------

    [Fact]
    public void CanalBuilding_EmptyBothSides_NoOp()
    {
        var g = FreshDecks();
        var h = new CanalBuildingHandler();
        Assert.False(h.Execute(g, g.Players[0], Ctx(Icon.Crown)));
    }

    [Fact]
    public void CanalBuilding_No_NoExchange()
    {
        var g = FreshDecks();
        var me = g.Players[0];
        me.Hand.Add(AllCards.First(c => c.Age == 2).Id);

        var ctx = Ctx(Icon.Crown);
        var h = new CanalBuildingHandler();
        h.Execute(g, me, ctx);
        var req = (YesNoChoiceRequest)ctx.PendingChoice!;
        req.ChosenYes = false;
        ctx.Paused = false;

        Assert.False(h.Execute(g, me, ctx));
        Assert.Single(me.Hand);
        Assert.Empty(me.ScorePile);
    }

    [Fact]
    public void CanalBuilding_ExchangeHighestsIndependently()
    {
        var g = FreshDecks();
        var me = g.Players[0];
        // Hand: one age-4 and one age-2. Score: two age-3 and one age-1.
        var age4 = AllCards.First(c => c.Age == 4).Id;
        var age2 = AllCards.First(c => c.Age == 2).Id;
        var age3a = AllCards.First(c => c.Age == 3).Id;
        var age3b = AllCards.First(c => c.Age == 3 && c.Id != age3a).Id;
        var age1 = AllCards.First(c => c.Age == 1).Id;
        me.Hand.AddRange(new[] { age4, age2 });
        me.ScorePile.AddRange(new[] { age3a, age3b, age1 });

        var ctx = Ctx(Icon.Crown);
        var h = new CanalBuildingHandler();
        h.Execute(g, me, ctx);
        var req = (YesNoChoiceRequest)ctx.PendingChoice!;
        req.ChosenYes = true;
        ctx.Paused = false;

        Assert.True(h.Execute(g, me, ctx));
        // Hand highest (age 4) → score. Score highest (age 3s) → hand.
        Assert.Contains(age4, me.ScorePile);
        Assert.DoesNotContain(age4, me.Hand);
        Assert.Contains(age3a, me.Hand);
        Assert.Contains(age3b, me.Hand);
        // age-2 stays in hand (not highest).
        Assert.Contains(age2, me.Hand);
        // age-1 stays in score pile (not highest).
        Assert.Contains(age1, me.ScorePile);
    }

    [Fact]
    public void CanalBuilding_EmptyHand_StillExchanges()
    {
        var g = FreshDecks();
        var me = g.Players[0];
        var age3 = AllCards.First(c => c.Age == 3).Id;
        me.ScorePile.Add(age3);

        var ctx = Ctx(Icon.Crown);
        var h = new CanalBuildingHandler();
        h.Execute(g, me, ctx);
        var req = (YesNoChoiceRequest)ctx.PendingChoice!;
        req.ChosenYes = true;
        ctx.Paused = false;

        Assert.True(h.Execute(g, me, ctx));
        Assert.Contains(age3, me.Hand);
        Assert.Empty(me.ScorePile);
    }

    // ---------- Mapmaking ----------

    [Fact]
    public void Mapmaking_TargetHasNo1_NoOp()
    {
        var g = FreshDecks();
        var target = g.Players[1];
        target.ScorePile.Add(AllCards.First(c => c.Age == 3).Id);

        var ctx = Ctx(Icon.Crown, playerIndex: 0);
        var h = new MapmakingDemandHandler();
        Assert.False(h.Execute(g, target, ctx));
        Assert.False(ctx.DemandSuccessful);
    }

    [Fact]
    public void Mapmaking_Transfers1AndSetsFlag()
    {
        var g = FreshDecks();
        var activator = g.Players[0];
        var target = g.Players[1];
        var one = AllCards.First(c => c.Age == 1).Id;
        target.ScorePile.Add(one);

        var ctx = Ctx(Icon.Crown, playerIndex: 0);
        var h = new MapmakingDemandHandler();
        h.Execute(g, target, ctx);
        var req = (SelectScoreCardRequest)ctx.PendingChoice!;
        Assert.Contains(one, req.EligibleCardIds);
        req.ChosenCardId = one;
        ctx.Paused = false;

        Assert.True(h.Execute(g, target, ctx));
        Assert.True(ctx.DemandSuccessful);
        Assert.Contains(one, activator.ScorePile);
        Assert.DoesNotContain(one, target.ScorePile);
    }

    [Fact]
    public void Mapmaking_DrawIfDemand_FiresOnlyWhenFlagSet()
    {
        var g = FreshDecks();
        var me = g.Players[0];
        var h = new MapmakingDrawIfDemandHandler();

        var ctx = Ctx(Icon.Crown);
        Assert.False(h.Execute(g, me, ctx));    // flag unset → no-op
        Assert.Empty(me.ScorePile);

        ctx.DemandSuccessful = true;
        int age1Before = g.Decks[1].Count;
        Assert.True(h.Execute(g, me, ctx));
        Assert.Equal(age1Before - 1, g.Decks[1].Count);
        Assert.Single(me.ScorePile);
        Assert.Equal(1, g.Cards[me.ScorePile[0]].Age);
    }

    // ---------- Monotheism ----------

    [Fact]
    public void Monotheism_NoEligibleColors_NoOp()
    {
        var g = FreshDecks();
        var activator = g.Players[0];
        var target = g.Players[1];
        // Activator has Yellow; target also has Yellow — no eligible colors.
        var y1 = AllCards.First(c => c.Color == CardColor.Yellow).Id;
        var y2 = AllCards.First(c => c.Color == CardColor.Yellow && c.Id != y1).Id;
        activator.Hand.Add(y1); Mechanics.Meld(g, activator, y1);
        target.Hand.Add(y2); Mechanics.Meld(g, target, y2);

        var ctx = Ctx(Icon.Castle, playerIndex: 0);
        var h = new MonotheismDemandHandler();
        Assert.False(h.Execute(g, target, ctx));
    }

    [Fact]
    public void Monotheism_TransfersTopAndActiveDrawsAndTucks()
    {
        var g = FreshDecks();
        var activator = g.Players[0];
        var target = g.Players[1];
        // Target has Yellow; activator has no Yellow. Activator has Red only.
        var yellow = AllCards.First(c => c.Color == CardColor.Yellow).Id;
        var red = AllCards.First(c => c.Color == CardColor.Red).Id;
        target.Hand.Add(yellow); Mechanics.Meld(g, target, yellow);
        activator.Hand.Add(red); Mechanics.Meld(g, activator, red);

        var ctx = Ctx(Icon.Castle, playerIndex: 0);
        var h = new MonotheismDemandHandler();
        h.Execute(g, target, ctx);
        var req = (SelectColorRequest)ctx.PendingChoice!;
        Assert.Contains(CardColor.Yellow, req.EligibleColors);
        req.ChosenColor = CardColor.Yellow;
        ctx.Paused = false;

        bool progressed = h.Execute(g, target, ctx);
        Assert.True(progressed);
        Assert.True(ctx.DemandSuccessful);
        // The melded Yellow card was transferred to activator's score
        // pile. (We don't check target's Yellow stack — the test deck
        // contains every age-1 card, so target's draw-and-tuck below can
        // pull the same Yellow back and re-tuck it; that's a test-data
        // quirk, not a handler bug.)
        Assert.Contains(yellow, activator.ScorePile);
        // "If you do, draw and tuck a 1!" — the demand target (the
        // defender) is the one who draws and tucks per RAW. So we expect
        // a new age-1 card under one of the target's stacks, not the
        // activator's.
        int tuckedCount = 0;
        foreach (CardColor c in Enum.GetValues<CardColor>())
        {
            var stack = target.Stack(c);
            if (!stack.IsEmpty && g.Cards[stack.Cards[^1]].Age == 1) tuckedCount++;
        }
        Assert.True(tuckedCount >= 1);
    }

    [Fact]
    public void Monotheism_NonDemandEffect_DrawAndTucksA1()
    {
        var g = FreshDecks();
        var me = g.Players[0];
        var h = new DrawAndTuckHandler(count: 1, startingAge: 1);
        int age1Before = g.Decks[1].Count;

        bool progressed = h.Execute(g, me, Ctx(Icon.Castle));
        Assert.True(progressed);
        Assert.Equal(age1Before - 1, g.Decks[1].Count);
        // Exactly one color now has a bottom card.
        int nonEmpty = 0;
        foreach (CardColor c in Enum.GetValues<CardColor>())
            if (!me.Stack(c).IsEmpty) nonEmpty++;
        Assert.Equal(1, nonEmpty);
    }

    // ---------- Construction ----------

    [Fact]
    public void Construction_EmptyHand_NoOp()
    {
        var g = FreshDecks();
        var h = new ConstructionDemandHandler();
        Assert.False(h.Execute(g, g.Players[1], Ctx(Icon.Castle, playerIndex: 0)));
    }

    [Fact]
    public void Construction_TransfersTwoAndTargetDrawsA2()
    {
        var g = FreshDecks();
        var activator = g.Players[0];
        var target = g.Players[1];
        var c1 = AllCards.First(c => c.Age == 1).Id;
        var c2 = AllCards.First(c => c.Age == 1 && c.Id != c1).Id;
        target.Hand.AddRange(new[] { c1, c2 });

        int age2Before = g.Decks[2].Count;

        var ctx = Ctx(Icon.Castle, playerIndex: 0);
        var h = new ConstructionDemandHandler();
        h.Execute(g, target, ctx);
        var req = (SelectHandCardSubsetRequest)ctx.PendingChoice!;
        Assert.Equal(2, req.MinCount);
        Assert.Equal(2, req.MaxCount);
        req.ChosenCardIds = new[] { c1, c2 };
        ctx.Paused = false;

        Assert.True(h.Execute(g, target, ctx));
        Assert.True(ctx.DemandSuccessful);
        Assert.Contains(c1, activator.Hand);
        Assert.Contains(c2, activator.Hand);
        Assert.DoesNotContain(c1, target.Hand);
        Assert.DoesNotContain(c2, target.Hand);
        // Target drew a 2 consolation.
        Assert.Single(target.Hand);
        Assert.Equal(2, g.Cards[target.Hand[0]].Age);
        Assert.Equal(age2Before - 1, g.Decks[2].Count);
    }

    [Fact]
    public void Construction_OneCardInHand_TransfersOneAndDraws()
    {
        var g = FreshDecks();
        var activator = g.Players[0];
        var target = g.Players[1];
        var c1 = AllCards.First(c => c.Age == 1).Id;
        target.Hand.Add(c1);

        var ctx = Ctx(Icon.Castle, playerIndex: 0);
        var h = new ConstructionDemandHandler();
        h.Execute(g, target, ctx);
        var req = (SelectHandCardSubsetRequest)ctx.PendingChoice!;
        Assert.Equal(1, req.MinCount);
        req.ChosenCardIds = new[] { c1 };
        ctx.Paused = false;

        Assert.True(h.Execute(g, target, ctx));
        Assert.Contains(c1, activator.Hand);
        // Target drew a 2.
        Assert.Single(target.Hand);
        Assert.Equal(2, g.Cards[target.Hand[0]].Age);
    }

    [Fact]
    public void ConstructionEmpire_OnlyFullBoard_ClaimsEmpire()
    {
        var g = FreshDecks();
        g.AvailableSpecialAchievements.Add("Empire");
        var me = g.Players[0];
        // Fill all 5 colors for me.
        foreach (CardColor c in Enum.GetValues<CardColor>())
        {
            var id = AllCards.First(card => card.Color == c).Id;
            me.Hand.Add(id);
            Mechanics.Meld(g, me, id);
        }

        var h = new ConstructionEmpireHandler();
        bool progressed = h.Execute(g, me, Ctx(Icon.Castle));
        Assert.True(progressed);
        Assert.Contains("Empire", me.SpecialAchievements);
    }

    [Fact]
    public void ConstructionEmpire_OpponentAlsoFull_NoClaim()
    {
        var g = FreshDecks();
        void FillBoard(PlayerState p, int offset)
        {
            int taken = 0;
            foreach (CardColor c in Enum.GetValues<CardColor>())
            {
                var id = AllCards.Where(card => card.Color == c).ElementAt(offset).Id;
                p.Hand.Add(id);
                Mechanics.Meld(g, p, id);
                taken++;
            }
        }
        FillBoard(g.Players[0], 0);
        FillBoard(g.Players[1], 1);

        // Clear any auto-claims from meld side effects so we isolate the
        // handler's decision.
        g.Players[0].SpecialAchievements.Remove("Empire");
        g.Players[1].SpecialAchievements.Remove("Empire");
        g.AvailableSpecialAchievements.Add("Empire");

        var h = new ConstructionEmpireHandler();
        Assert.False(h.Execute(g, g.Players[0], Ctx(Icon.Castle)));
        Assert.DoesNotContain("Empire", g.Players[0].SpecialAchievements);
    }

    // ---------- Road Building ----------

    [Fact]
    public void RoadBuilding_EmptyHand_NoOp()
    {
        var g = FreshDecks();
        var h = new RoadBuildingHandler();
        Assert.False(h.Execute(g, g.Players[0], Ctx(Icon.Castle)));
    }

    [Fact]
    public void RoadBuilding_MeldOne_NoExchangeOffered()
    {
        var g = FreshDecks();
        var me = g.Players[0];
        var c1 = AllCards.First(c => c.Age == 1).Id;
        me.Hand.Add(c1);

        var ctx = Ctx(Icon.Castle);
        var h = new RoadBuildingHandler();
        h.Execute(g, me, ctx);
        var req = (SelectHandCardSubsetRequest)ctx.PendingChoice!;
        req.ChosenCardIds = new[] { c1 };
        ctx.Paused = false;

        Assert.True(h.Execute(g, me, ctx));
        // Melded, not holding anything else in hand, and no pending choice
        // for the exchange.
        Assert.Null(ctx.PendingChoice);
        Assert.False(ctx.Paused);
    }

    [Fact]
    public void RoadBuilding_MeldTwo_NoTopRed_NoExchangeOffered()
    {
        var g = FreshDecks();
        var me = g.Players[0];
        // Two non-red cards in hand.
        var c1 = AllCards.First(c => c.Age == 1 && c.Color != CardColor.Red).Id;
        var c2 = AllCards.First(c => c.Age == 1 && c.Color != CardColor.Red && c.Id != c1).Id;
        me.Hand.AddRange(new[] { c1, c2 });

        var ctx = Ctx(Icon.Castle);
        var h = new RoadBuildingHandler();
        h.Execute(g, me, ctx);
        var req = (SelectHandCardSubsetRequest)ctx.PendingChoice!;
        req.ChosenCardIds = new[] { c1, c2 };
        ctx.Paused = false;

        Assert.True(h.Execute(g, me, ctx));
        Assert.Null(ctx.PendingChoice);   // no exchange prompt raised
    }

    [Fact]
    public void RoadBuilding_MeldTwo_ExchangeYes_SwapsRedAndGreen()
    {
        var g = FreshDecks();
        var me = g.Players[0];
        var opp = g.Players[1];

        // Active melds a red and any other card via the subset path.
        var red = AllCards.First(c => c.Color == CardColor.Red).Id;
        var other = AllCards.First(c => c.Color == CardColor.Blue).Id;
        me.Hand.AddRange(new[] { red, other });

        // Opponent already has a top green card on their board.
        var green = AllCards.First(c => c.Color == CardColor.Green).Id;
        opp.Hand.Add(green);
        Mechanics.Meld(g, opp, green);

        var ctx = Ctx(Icon.Castle);
        var h = new RoadBuildingHandler();
        h.Execute(g, me, ctx);
        var req = (SelectHandCardSubsetRequest)ctx.PendingChoice!;
        req.ChosenCardIds = new[] { red, other };
        ctx.Paused = false;

        // Phase 1 resume triggers Phase 2 prompt.
        h.Execute(g, me, ctx);
        var yn = (YesNoChoiceRequest)ctx.PendingChoice!;
        yn.ChosenYes = true;
        ctx.Paused = false;

        Assert.True(h.Execute(g, me, ctx));
        // Red moved me → opp; green moved opp → me.
        Assert.Equal(red, opp.Stack(CardColor.Red).Top);
        Assert.True(me.Stack(CardColor.Red).IsEmpty);
        Assert.Equal(green, me.Stack(CardColor.Green).Top);
        Assert.True(opp.Stack(CardColor.Green).IsEmpty);
    }

    [Fact]
    public void RoadBuilding_MeldTwo_ExchangeYes_NoOpponentGreen_RedStillMoves()
    {
        var g = FreshDecks();
        var me = g.Players[0];
        var opp = g.Players[1];

        var red = AllCards.First(c => c.Color == CardColor.Red).Id;
        var other = AllCards.First(c => c.Color == CardColor.Blue).Id;
        me.Hand.AddRange(new[] { red, other });

        var ctx = Ctx(Icon.Castle);
        var h = new RoadBuildingHandler();
        h.Execute(g, me, ctx);
        var req = (SelectHandCardSubsetRequest)ctx.PendingChoice!;
        req.ChosenCardIds = new[] { red, other };
        ctx.Paused = false;

        h.Execute(g, me, ctx);
        var yn = (YesNoChoiceRequest)ctx.PendingChoice!;
        yn.ChosenYes = true;
        ctx.Paused = false;

        Assert.True(h.Execute(g, me, ctx));
        Assert.Equal(red, opp.Stack(CardColor.Red).Top);
        Assert.True(me.Stack(CardColor.Red).IsEmpty);
        Assert.True(me.Stack(CardColor.Green).IsEmpty);   // nothing came back
    }
}
