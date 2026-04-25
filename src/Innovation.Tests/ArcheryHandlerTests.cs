using System.Text;
using Innovation.Core;
using Innovation.Core.Handlers;
using Xunit;

namespace Innovation.Tests;

/// <summary>
/// Tests for the Archery demand handler and the
/// <see cref="Mechanics.TransferHandToHand"/> primitive. Covers:
/// direct handler two-step flow, tie-breaking, engine wiring (including
/// the "activator has no icon advantage → no targets" case), and the
/// demand semantics (no shared-bonus).
/// </summary>
public class ArcheryHandlerTests
{
    static ArcheryHandlerTests()
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

    /// <summary>Context with activator=player 0 (the "attacker").</summary>
    private static DogmaContext Ctx() =>
        new(cardId: 0, activatingPlayerIndex: 0, featuredIcon: Icon.Castle);

    // =========================================================================
    // Mechanics.TransferHandToHand primitive
    // =========================================================================

    [Fact]
    public void TransferHandToHand_MovesCardBetweenHands()
    {
        var g = FreshDecks();
        int card = g.Cards.First(c => c.Age == 2).Id;
        g.Players[1].Hand.Add(card);

        Mechanics.TransferHandToHand(g, g.Players[1], g.Players[0], card);

        Assert.DoesNotContain(card, g.Players[1].Hand);
        Assert.Contains(card, g.Players[0].Hand);
    }

    // =========================================================================
    // Archery direct handler: two-step (draw, then pause for pick)
    // =========================================================================

    [Fact]
    public void Archery_FirstCall_DrawsForTargetAndPausesWithTiedHighest()
    {
        var g = FreshDecks();
        // Target (p1) starts with one age-1 card.
        int age1Lo = g.Cards.First(c => c.Age == 1).Id;
        g.Players[1].Hand.Add(age1Lo);
        int topAge1 = g.Decks[1][0];

        var ctx = Ctx();
        bool progressed = new ArcheryHandler().Execute(g, g.Players[1], ctx);

        Assert.True(progressed);
        Assert.True(ctx.Paused);
        // Draw happened: target.Hand now has 2 cards.
        Assert.Equal(2, g.Players[1].Hand.Count);
        Assert.Contains(topAge1, g.Players[1].Hand);

        var req = Assert.IsType<SelectHandCardRequest>(ctx.PendingChoice);
        Assert.Equal(1, req.PlayerIndex);
        Assert.False(req.AllowNone);
        // Both cards are age 1 → both are tied-highest → both offered.
        Assert.Equal(2, req.EligibleCardIds.Count);
    }

    [Fact]
    public void Archery_PicksSingleHighest_TransfersToActivator()
    {
        var g = FreshDecks();
        // Target has one age-1 and one age-2; after draw (a 1) the age-2 is
        // still the unique highest.
        int age1 = g.Cards.First(c => c.Age == 1).Id;
        int age2 = g.Cards.First(c => c.Age == 2).Id;
        g.Players[1].Hand.Add(age1);
        g.Players[1].Hand.Add(age2);

        var ctx = Ctx();
        var h = new ArcheryHandler();
        h.Execute(g, g.Players[1], ctx);   // pause

        var req = (SelectHandCardRequest)ctx.PendingChoice!;
        Assert.Single(req.EligibleCardIds);
        Assert.Equal(age2, req.EligibleCardIds[0]);
        req.ChosenCardId = age2;
        ctx.Paused = false;

        bool progressed = h.Execute(g, g.Players[1], ctx);

        Assert.True(progressed);
        Assert.Contains(age2, g.Players[0].Hand);
        Assert.DoesNotContain(age2, g.Players[1].Hand);
        Assert.Null(ctx.HandlerState);
        Assert.Null(ctx.PendingChoice);
    }

    [Fact]
    public void Archery_TieAtHighest_TargetChoosesWhichToTransfer()
    {
        var g = FreshDecks();
        // Two age-2 cards and an age-1; the draw adds a 1, so ties remain at
        // age 2. Target should be able to pick either age-2.
        var twos = g.Cards.Where(c => c.Age == 2).Take(2).Select(c => c.Id).ToList();
        var age1 = g.Cards.First(c => c.Age == 1).Id;
        g.Players[1].Hand.AddRange(twos);
        g.Players[1].Hand.Add(age1);

        var ctx = Ctx();
        var h = new ArcheryHandler();
        h.Execute(g, g.Players[1], ctx);

        var req = (SelectHandCardRequest)ctx.PendingChoice!;
        Assert.Equal(2, req.EligibleCardIds.Count);
        Assert.Contains(twos[0], req.EligibleCardIds);
        Assert.Contains(twos[1], req.EligibleCardIds);
        Assert.DoesNotContain(age1, req.EligibleCardIds);

        // Target picks the second one.
        req.ChosenCardId = twos[1];
        ctx.Paused = false;
        h.Execute(g, g.Players[1], ctx);

        Assert.Contains(twos[1], g.Players[0].Hand);
        Assert.Contains(twos[0], g.Players[1].Hand);   // not taken
    }

    [Fact]
    public void Archery_DrawnCard_CanItselfBeTheHighest()
    {
        var g = FreshDecks();
        // Give target only an age-1 already in hand; empty out age-1 deck
        // except for one top card so we know exactly what they draw.
        // (The fresh deck has all age-1 cards; that's fine — they draw the
        // top, which is age 1, so the tie is between all their 1s.)
        var pre = g.Cards.First(c => c.Age == 1).Id;
        g.Players[1].Hand.Add(pre);
        int drawnExpected = g.Decks[1][0];

        var ctx = Ctx();
        new ArcheryHandler().Execute(g, g.Players[1], ctx);

        var req = (SelectHandCardRequest)ctx.PendingChoice!;
        Assert.Contains(drawnExpected, req.EligibleCardIds);
    }

    [Fact]
    public void Archery_EndsGameIfDrawRunsOut_ReturnsProgressNoPause()
    {
        var g = FreshDecks();
        // Empty every deck so the draw hits age 11.
        for (int a = 1; a <= 10; a++) g.Decks[a].Clear();

        var ctx = Ctx();
        bool progressed = new ArcheryHandler().Execute(g, g.Players[1], ctx);

        Assert.True(progressed);
        Assert.True(g.IsGameOver);
        Assert.False(ctx.Paused);
        Assert.Null(ctx.PendingChoice);
    }

    // =========================================================================
    // Engine wiring
    // =========================================================================

    [Fact]
    public void Archery_ViaEngine_PausesOnlyForDemandedPlayer()
    {
        var g = FreshDecks();
        // Player 0 gets the Castle advantage by melding a castle card.
        var castle = g.Cards.First(c =>
            (c.Top == Icon.Castle || c.Left == Icon.Castle ||
             c.Middle == Icon.Castle || c.Right == Icon.Castle));
        g.Players[0].Stack(castle.Color).Meld(castle.Id);

        // Target starts with one hand card.
        int targetCard = g.Cards.First(c => c.Age == 1 && c.Id != castle.Id).Id;
        g.Players[1].Hand.Add(targetCard);

        var registry = new CardRegistry(g.Cards);
        CardRegistrations.RegisterAll(registry, g.Cards);
        int archery = g.Cards.Single(c => c.Title == "Archery").Id;

        var ctx = new DogmaEngine(g, registry).Execute(0, archery);

        Assert.True(ctx.Paused);
        var req = Assert.IsType<SelectHandCardRequest>(ctx.PendingChoice);
        Assert.Equal(1, req.PlayerIndex);   // pausing on the demanded player
    }

    [Fact]
    public void Archery_ViaEngine_NoTargetsWhenActivatorHasNoIconAdvantage()
    {
        var g = FreshDecks();
        // Neither player has any castles — activator has 0, target has 0,
        // so no one is "strictly fewer" and the demand affects nobody.
        int targetCard = g.Cards.First(c => c.Age == 1).Id;
        g.Players[1].Hand.Add(targetCard);

        var registry = new CardRegistry(g.Cards);
        CardRegistrations.RegisterAll(registry, g.Cards);
        int archery = g.Cards.Single(c => c.Title == "Archery").Id;

        var ctx = new DogmaEngine(g, registry).Execute(0, archery);

        Assert.False(ctx.Paused);
        Assert.True(ctx.IsComplete);
        // No one was demanded, so target's hand is untouched.
        Assert.Contains(targetCard, g.Players[1].Hand);
    }

    [Fact]
    public void Archery_ViaEngine_DemandDoesNotTriggerSharedBonus()
    {
        var g = FreshDecks();
        // Set up the demand so it actually hits.
        var castle = g.Cards.First(c =>
            c.Top == Icon.Castle || c.Left == Icon.Castle ||
            c.Middle == Icon.Castle || c.Right == Icon.Castle);
        g.Players[0].Stack(castle.Color).Meld(castle.Id);
        int targetCard = g.Cards.First(c => c.Age == 1 && c.Id != castle.Id).Id;
        g.Players[1].Hand.Add(targetCard);

        var registry = new CardRegistry(g.Cards);
        CardRegistrations.RegisterAll(registry, g.Cards);
        int archery = g.Cards.Single(c => c.Title == "Archery").Id;

        var engine = new DogmaEngine(g, registry);
        var ctx = engine.Execute(0, archery);

        // Resolve the pause: target picks the only tied-highest card.
        // Caller only writes the choice + clears Paused — the handler is
        // responsible for clearing PendingChoice.
        var req = (SelectHandCardRequest)ctx.PendingChoice!;
        req.ChosenCardId = req.EligibleCardIds[0];
        ctx.Paused = false;
        engine.Resume(ctx);

        Assert.True(ctx.IsComplete);
        Assert.False(ctx.SharedBonus);   // demands never share
        // Activator got the target's card — that's one card in hand, nothing
        // extra from a shared-bonus draw.
        Assert.Single(g.Players[0].Hand);
    }
}
