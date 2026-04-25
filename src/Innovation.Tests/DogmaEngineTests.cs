using System.Text;
using Innovation.Core;
using Xunit;

namespace Innovation.Tests;

public class DogmaEngineTests
{
    static DogmaEngineTests()
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
    }

    private static IReadOnlyList<Card> AllCards => CardDataLoader.LoadFromEmbeddedResource();

    private static GameState NewGame(int players = 3)
    {
        var g = new GameState(AllCards, players);
        foreach (var c in AllCards) g.Decks[c.Age].Add(c.Id);
        g.Phase = GamePhase.Dogma;
        g.ActivePlayer = 0;
        return g;
    }

    /// <summary>
    /// A test handler that records which player indexes it was called against
    /// and returns a configurable progress value. Used to verify engine
    /// iteration order, demand/share gating, and shared-bonus bookkeeping.
    /// </summary>
    private sealed class RecordingHandler : IDogmaHandler
    {
        public List<int> CalledOn { get; } = new();
        public bool ReturnProgress { get; set; }

        public bool Execute(GameState g, PlayerState target, DogmaContext ctx)
        {
            CalledOn.Add(target.Index);
            return ReturnProgress;
        }
    }

    /// <summary>
    /// Force a player's leaf count by placing a known card on their yellow
    /// pile. Agriculture's top slots give exactly 3 leaves; empty gives 0.
    /// Only the two values we need in these tests are supported — we want
    /// the test helper to fail loudly if someone broadens usage.
    /// </summary>
    private static void SetLeafCount(GameState g, PlayerState p, int leaves)
    {
        var yellow = p.Stack(CardColor.Yellow);
        while (!yellow.IsEmpty) yellow.PopTop();

        if (leaves == 0) return;
        if (leaves == 3)
        {
            int agriId = g.Cards.First(c => c.Title == "Agriculture").Id;
            yellow.Meld(agriId);
            return;
        }
        throw new ArgumentException($"SetLeafCount only supports 0 or 3 (got {leaves}).");
    }

    private static DogmaDefinition SingleEffect(bool isDemand, IDogmaHandler handler, Icon featured = Icon.Leaf)
        => new(featured, new[] { new DogmaEffect(isDemand, "test", handler) });

    // ---------- Share effect targeting ----------

    [Fact]
    public void Share_IncludesActivePlayer_AndPlayersWithEqualOrMoreIcons()
    {
        var g = NewGame(3);
        // Player 0 (active): 3 leaves, Player 1: 3 leaves, Player 2: 0 leaves.
        SetLeafCount(g, g.Players[0], 3);
        SetLeafCount(g, g.Players[1], 3);
        SetLeafCount(g, g.Players[2], 0);

        var handler = new RecordingHandler { ReturnProgress = false };
        var registry = new CardRegistry(g.Cards);
        int cardId = g.Cards[0].Id;
        registry.Register(cardId, SingleEffect(isDemand: false, handler));

        new DogmaEngine(g, registry).Execute(0, cardId);

        Assert.Equal(new[] { 1, 0 }, handler.CalledOn);  // sharer first, active last
    }

    [Fact]
    public void Share_ExcludesPlayersWithFewerIcons()
    {
        var g = NewGame(3);
        SetLeafCount(g, g.Players[0], 3);
        SetLeafCount(g, g.Players[1], 0);
        SetLeafCount(g, g.Players[2], 0);

        var handler = new RecordingHandler();
        var registry = new CardRegistry(g.Cards);
        int cardId = g.Cards[0].Id;
        registry.Register(cardId, SingleEffect(isDemand: false, handler));

        new DogmaEngine(g, registry).Execute(0, cardId);

        Assert.Equal(new[] { 0 }, handler.CalledOn);
    }

    // ---------- Demand effect targeting ----------

    [Fact]
    public void Demand_OnlyHitsPlayersWithStrictlyFewerIcons()
    {
        var g = NewGame(3);
        SetLeafCount(g, g.Players[0], 3);
        SetLeafCount(g, g.Players[1], 3);      // tied → not demanded
        SetLeafCount(g, g.Players[2], 0);      // fewer → demanded

        var handler = new RecordingHandler();
        var registry = new CardRegistry(g.Cards);
        int cardId = g.Cards[0].Id;
        registry.Register(cardId, SingleEffect(isDemand: true, handler));

        new DogmaEngine(g, registry).Execute(0, cardId);

        Assert.Equal(new[] { 2 }, handler.CalledOn);
    }

    [Fact]
    public void Demand_DoesNotIncludeActivePlayer()
    {
        var g = NewGame(3);
        SetLeafCount(g, g.Players[0], 3);
        SetLeafCount(g, g.Players[1], 0);
        SetLeafCount(g, g.Players[2], 0);

        var handler = new RecordingHandler();
        var registry = new CardRegistry(g.Cards);
        int cardId = g.Cards[0].Id;
        registry.Register(cardId, SingleEffect(isDemand: true, handler));

        new DogmaEngine(g, registry).Execute(0, cardId);

        Assert.DoesNotContain(0, handler.CalledOn);
    }

    // ---------- Turn order ----------

    [Fact]
    public void Share_IteratesFromLeftOfActivePlayer()
    {
        var g = NewGame(4);
        g.ActivePlayer = 1;
        for (int i = 0; i < 4; i++) SetLeafCount(g, g.Players[i], 3);  // everyone tied

        var handler = new RecordingHandler();
        var registry = new CardRegistry(g.Cards);
        int cardId = g.Cards[0].Id;
        registry.Register(cardId, SingleEffect(isDemand: false, handler));

        new DogmaEngine(g, registry).Execute(1, cardId);

        // Sharers clockwise from left of active, then active last: 2, 3, 0, 1.
        Assert.Equal(new[] { 2, 3, 0, 1 }, handler.CalledOn);
    }

    // ---------- Shared-bonus draw ----------

    [Fact]
    public void SharedBonus_Triggers_WhenNonActivePlayerMakesProgress()
    {
        var g = NewGame(2);
        SetLeafCount(g, g.Players[0], 3);
        SetLeafCount(g, g.Players[1], 3);

        var handler = new RecordingHandler { ReturnProgress = true };
        var registry = new CardRegistry(g.Cards);
        int cardId = g.Cards.First(c => c.Age == 1).Id;
        registry.Register(cardId, SingleEffect(isDemand: false, handler));

        int handBefore = g.Players[0].Hand.Count;
        var ctx = new DogmaEngine(g, registry).Execute(0, cardId);

        Assert.True(ctx.SharedBonus);
        // Active player drew one bonus card.
        Assert.Equal(handBefore + 1, g.Players[0].Hand.Count);
    }

    [Fact]
    public void SharedBonus_DoesNotTrigger_WhenOnlyActiveProgressed()
    {
        var g = NewGame(2);
        SetLeafCount(g, g.Players[0], 3);
        SetLeafCount(g, g.Players[1], 0);  // not a share target

        var handler = new RecordingHandler { ReturnProgress = true };
        var registry = new CardRegistry(g.Cards);
        int cardId = g.Cards[0].Id;
        registry.Register(cardId, SingleEffect(isDemand: false, handler));

        int handBefore = g.Players[0].Hand.Count;
        var ctx = new DogmaEngine(g, registry).Execute(0, cardId);

        Assert.False(ctx.SharedBonus);
        Assert.Equal(handBefore, g.Players[0].Hand.Count);
    }

    [Fact]
    public void SharedBonus_DoesNotTrigger_ForDemandProgress()
    {
        var g = NewGame(2);
        SetLeafCount(g, g.Players[0], 3);
        SetLeafCount(g, g.Players[1], 0);

        var handler = new RecordingHandler { ReturnProgress = true };
        var registry = new CardRegistry(g.Cards);
        int cardId = g.Cards[0].Id;
        registry.Register(cardId, SingleEffect(isDemand: true, handler));

        var ctx = new DogmaEngine(g, registry).Execute(0, cardId);

        Assert.False(ctx.SharedBonus);
    }

    // ---------- Multi-level effects ----------

    [Fact]
    public void MultiLevel_Dogma_ExecutesAllEffects_InOrder()
    {
        var g = NewGame(2);
        SetLeafCount(g, g.Players[0], 3);
        SetLeafCount(g, g.Players[1], 3);

        var a = new RecordingHandler();
        var b = new RecordingHandler();
        var def = new DogmaDefinition(Icon.Leaf, new[]
        {
            new DogmaEffect(false, "first",  a),
            new DogmaEffect(false, "second", b),
        });
        var registry = new CardRegistry(g.Cards);
        int cardId = g.Cards[0].Id;
        registry.Register(cardId, def);

        new DogmaEngine(g, registry).Execute(0, cardId);

        Assert.Equal(new[] { 1, 0 }, a.CalledOn);
        Assert.Equal(new[] { 1, 0 }, b.CalledOn);
    }

    // ---------- Placeholder ----------

    [Fact]
    public void Placeholder_Cards_ResolveAsNoOp()
    {
        var g = NewGame(2);
        SetLeafCount(g, g.Players[0], 3);
        SetLeafCount(g, g.Players[1], 3);

        var registry = new CardRegistry(g.Cards);   // nothing registered
        int cardId = g.Cards[0].Id;

        int handBefore = g.Players[0].Hand.Count;
        var ctx = new DogmaEngine(g, registry).Execute(0, cardId);

        Assert.True(ctx.IsComplete);
        Assert.False(ctx.SharedBonus);
        Assert.Equal(handBefore, g.Players[0].Hand.Count);  // no bonus draw
    }

    // ---------- Pause / resume ----------

    [Fact]
    public void Pause_StopsIteration_AndResumeCompletes()
    {
        var g = NewGame(2);
        SetLeafCount(g, g.Players[0], 3);
        SetLeafCount(g, g.Players[1], 3);

        var calls = new List<int>();
        var pauseOnce = new PauseOnceHandler(calls);
        var registry = new CardRegistry(g.Cards);
        int cardId = g.Cards[0].Id;
        registry.Register(cardId, SingleEffect(isDemand: false, pauseOnce));

        var engine = new DogmaEngine(g, registry);
        var ctx = engine.Execute(0, cardId);

        Assert.True(ctx.Paused);
        Assert.False(ctx.IsComplete);
        Assert.Empty(calls);  // handler paused before recording

        ctx.Paused = false;
        engine.Resume(ctx);

        Assert.True(ctx.IsComplete);
        // Sharer (player 1) runs first, then the active player.
        Assert.Equal(new[] { 1, 0 }, calls);
    }

    /// <summary>
    /// Pauses on its very first call without recording, then records on every
    /// subsequent call. Models a real "ask the user for input then act" flow.
    /// </summary>
    private sealed class PauseOnceHandler : IDogmaHandler
    {
        private readonly List<int> _calls;
        private bool _hasPaused;

        public PauseOnceHandler(List<int> calls) { _calls = calls; }

        public bool Execute(GameState g, PlayerState target, DogmaContext ctx)
        {
            if (!_hasPaused)
            {
                _hasPaused = true;
                ctx.Paused = true;
                return false;
            }
            _calls.Add(target.Index);
            return false;
        }
    }
}
