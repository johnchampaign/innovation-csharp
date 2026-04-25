using System.Text;
using Innovation.Core;
using Innovation.Core.Handlers;
using Xunit;

namespace Innovation.Tests;

/// <summary>
/// Tests for the pause/resume choice API (ChoiceRequest + DogmaContext
/// extensions) using Agriculture as the end-to-end example.
/// </summary>
public class AgricultureHandlerTests
{
    static AgricultureHandlerTests()
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

    private static DogmaContext Ctx(int playerIndex = 0) =>
        new(cardId: 0, activatingPlayerIndex: playerIndex, featuredIcon: Icon.Leaf);

    // ---------- Direct handler ----------

    [Fact]
    public void Agriculture_EmptyHand_NoOp()
    {
        var g = FreshDecks();
        Assert.Empty(g.Players[0].Hand);

        var ctx = Ctx();
        var h = new AgricultureHandler();
        bool progressed = h.Execute(g, g.Players[0], ctx);

        Assert.False(progressed);
        Assert.False(ctx.Paused);
        Assert.Null(ctx.PendingChoice);
        Assert.Empty(g.Players[0].Hand);
        Assert.Empty(g.Players[0].ScorePile);
    }

    [Fact]
    public void Agriculture_FirstCall_PausesWithChoice()
    {
        var g = FreshDecks();
        // Seed three hand cards (ages 1, 2, 3) — all should be legal picks.
        var age1 = g.Cards.First(c => c.Age == 1).Id;
        var age2 = g.Cards.First(c => c.Age == 2).Id;
        var age3 = g.Cards.First(c => c.Age == 3).Id;
        g.Players[0].Hand.AddRange(new[] { age1, age2, age3 });

        var ctx = Ctx();
        var h = new AgricultureHandler();
        bool progressed = h.Execute(g, g.Players[0], ctx);

        Assert.False(progressed);
        Assert.True(ctx.Paused);
        var req = Assert.IsType<SelectHandCardRequest>(ctx.PendingChoice);
        Assert.True(req.AllowNone);
        Assert.Equal(0, req.PlayerIndex);
        Assert.Equal(new[] { age1, age2, age3 }, req.EligibleCardIds);
    }

    [Fact]
    public void Agriculture_Declined_NoReturnOrDraw()
    {
        var g = FreshDecks();
        var age2 = g.Cards.First(c => c.Age == 2).Id;
        g.Players[0].Hand.Add(age2);

        var ctx = Ctx();
        var h = new AgricultureHandler();
        h.Execute(g, g.Players[0], ctx);   // pause with choice
        Assert.True(ctx.Paused);

        // "Decline" — ChosenCardId left null, AllowNone=true.
        ctx.Paused = false;
        bool progressed = h.Execute(g, g.Players[0], ctx);

        Assert.False(progressed);
        Assert.Contains(age2, g.Players[0].Hand);         // card still in hand
        Assert.Empty(g.Players[0].ScorePile);             // nothing scored
        Assert.Null(ctx.PendingChoice);                   // cleared
    }

    [Fact]
    public void Agriculture_ReturnsAndScores_AtAgePlusOne()
    {
        var g = FreshDecks();
        var age2 = g.Cards.First(c => c.Age == 2).Id;
        g.Players[0].Hand.Add(age2);

        // Capture the top of the age-3 deck — that's what the draw-and-score
        // should land on.
        int topOfAge3 = g.Decks[3][0];

        var ctx = Ctx();
        var h = new AgricultureHandler();
        h.Execute(g, g.Players[0], ctx);   // pause with choice

        var req = (SelectHandCardRequest)ctx.PendingChoice!;
        req.ChosenCardId = age2;
        ctx.Paused = false;
        bool progressed = h.Execute(g, g.Players[0], ctx);

        Assert.True(progressed);
        // The returned card sits at the bottom of age-2 deck now (not in hand).
        Assert.DoesNotContain(age2, g.Players[0].Hand);
        Assert.Equal(age2, g.Decks[2][^1]);
        // The age-3 draw ended up in the score pile, not the hand.
        Assert.Contains(topOfAge3, g.Players[0].ScorePile);
        Assert.DoesNotContain(topOfAge3, g.Players[0].Hand);
    }

    [Fact]
    public void Agriculture_ReturnAge10_EndsGameOnDrawOverflow()
    {
        var g = FreshDecks();
        var age10 = g.Cards.First(c => c.Age == 10).Id;
        g.Players[0].Hand.Add(age10);

        var ctx = Ctx();
        var h = new AgricultureHandler();
        h.Execute(g, g.Players[0], ctx);
        var req = (SelectHandCardRequest)ctx.PendingChoice!;
        req.ChosenCardId = age10;
        ctx.Paused = false;

        h.Execute(g, g.Players[0], ctx);

        // Drawing at "age 11" is impossible — game ends, no score.
        Assert.Equal(GamePhase.GameOver, g.Phase);
        Assert.Empty(g.Players[0].ScorePile);
    }

    // ---------- Through DogmaEngine ----------

    [Fact]
    public void Agriculture_ThroughEngine_PausesOnActivatorAndResumes()
    {
        var g = FreshDecks();
        var registry = new CardRegistry(g.Cards);
        CardRegistrations.RegisterAll(registry, g.Cards);

        int agriId = g.Cards.Single(c => c.Title == "Agriculture").Id;
        var age2 = g.Cards.First(c => c.Age == 2).Id;
        g.Players[0].Hand.Add(age2);

        var engine = new DogmaEngine(g, registry);
        var ctx = engine.Execute(activatingPlayerIndex: 0, cardId: agriId);

        // Paused on player 0's choice.
        Assert.True(ctx.Paused);
        Assert.False(ctx.IsComplete);
        var req = Assert.IsType<SelectHandCardRequest>(ctx.PendingChoice);
        Assert.Equal(0, req.PlayerIndex);

        // Activator returns age-2 → draw-and-score at age 3.
        int topOfAge3 = g.Decks[3][0];
        req.ChosenCardId = age2;
        ctx.Paused = false;
        engine.Resume(ctx);

        // Engine may pause again on player 1's share; if so, decline it.
        if (ctx.Paused)
        {
            var req2 = (SelectHandCardRequest)ctx.PendingChoice!;
            req2.ChosenCardId = null;
            ctx.Paused = false;
            engine.Resume(ctx);
        }

        Assert.True(ctx.IsComplete);
        Assert.Contains(topOfAge3, g.Players[0].ScorePile);
    }

    [Fact]
    public void Agriculture_ThroughEngine_ShareTargetAlsoPrompted()
    {
        // Both players start empty-board (0 Leaf icons) so both qualify for
        // Agriculture's share effect. If player 1's hand has a card, the
        // engine must pause again on them after player 0 finishes.
        var g = FreshDecks();
        var registry = new CardRegistry(g.Cards);
        CardRegistrations.RegisterAll(registry, g.Cards);

        int agriId = g.Cards.Single(c => c.Title == "Agriculture").Id;
        // Give player 0 an age-1, player 1 an age-2 — both will be offered a choice.
        var p0card = g.Cards.First(c => c.Age == 1).Id;
        var p1card = g.Cards.First(c => c.Age == 2 && c.Id != p0card).Id;
        g.Players[0].Hand.Add(p0card);
        g.Players[1].Hand.Add(p1card);

        var engine = new DogmaEngine(g, registry);
        var ctx = engine.Execute(0, agriId);

        // First pause: player 1 (sharer resolves first per rulebook).
        var req0 = (SelectHandCardRequest)ctx.PendingChoice!;
        Assert.Equal(1, req0.PlayerIndex);
        req0.ChosenCardId = null;     // decline
        ctx.Paused = false;
        engine.Resume(ctx);

        // Second pause: player 0 (activator last).
        Assert.True(ctx.Paused);
        var req1 = Assert.IsType<SelectHandCardRequest>(ctx.PendingChoice);
        Assert.Equal(0, req1.PlayerIndex);
        req1.ChosenCardId = null;
        ctx.Paused = false;
        engine.Resume(ctx);

        Assert.True(ctx.IsComplete);
    }
}
