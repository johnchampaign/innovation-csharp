using System.Text;
using Innovation.Core;
using Innovation.Core.Handlers;
using Innovation.Core.Players;
using Xunit;

namespace Innovation.Tests;

public class ReformationHandlerTests
{
    static ReformationHandlerTests()
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
    }

    private static IReadOnlyList<Card> AllCards => CardDataLoader.LoadFromEmbeddedResource();

    private static GameState Fresh(int players = 2)
    {
        var g = new GameState(AllCards, players);
        foreach (var c in AllCards) g.Decks[c.Age].Add(c.Id);
        g.Phase = GamePhase.Dogma;
        return g;
    }

    private static int IdOf(string title) =>
        AllCards.Single(c => c.Title == title).Id;

    [Fact]
    public void Tuck_TwoCards_BothActuallyTucked()
    {
        var g = Fresh();
        var me = g.Players[0];
        // Give P1 enough leaves (board with several leaf-icon cards) and 2 hand cards.
        // Easiest: stuff some cards directly into stacks/hand.
        int invention = IdOf("Invention");
        int physics = IdOf("Physics");
        me.Hand.Add(invention);
        me.Hand.Add(physics);
        // Pretend P1 has lots of leaves by putting many leaf-bearing cards on board.
        // Reformation itself has Leaves; meld it + a few others.
        int reformation = IdOf("Reformation");
        int agriculture = IdOf("Agriculture");
        int pottery = IdOf("Pottery");
        me.Stack(g.Cards[reformation].Color).Meld(reformation);
        me.Stack(g.Cards[agriculture].Color).Meld(agriculture);
        me.Stack(g.Cards[pottery].Color).Meld(pottery);

        var ctx = new DogmaContext(cardId: reformation, activatingPlayerIndex: 0, featuredIcon: Icon.Leaf);
        var h = new ReformationTuckHandler();

        bool first = h.Execute(g, me, ctx);
        Assert.False(first);
        Assert.True(ctx.Paused);
        var req = (SelectHandCardSubsetRequest)ctx.PendingChoice!;
        // Simulate user picking BOTH cards.
        req.ChosenCardIds = new[] { invention, physics };
        ctx.Paused = false;

        bool second = h.Execute(g, me, ctx);
        Assert.True(second);
        Assert.Empty(me.Hand);
        Assert.Contains(invention, me.Stack(g.Cards[invention].Color).Cards);
        Assert.Contains(physics, me.Stack(g.Cards[physics].Color).Cards);
    }

    [Fact]
    public void Tuck_TwoCards_AfterFiveTucksThisTurn_StillTucksBoth()
    {
        var g = Fresh();
        var me = g.Players[0];
        int invention = IdOf("Invention");
        int physics = IdOf("Physics");
        me.Hand.Add(invention);
        me.Hand.Add(physics);
        int reformation = IdOf("Reformation");
        int agriculture = IdOf("Agriculture");
        int pottery = IdOf("Pottery");
        me.Stack(g.Cards[reformation].Color).Meld(reformation);
        me.Stack(g.Cards[agriculture].Color).Meld(agriculture);
        me.Stack(g.Cards[pottery].Color).Meld(pottery);
        me.TuckedThisTurn = 5; // First Reformation already tucked 5 this turn.

        var ctx = new DogmaContext(cardId: reformation, activatingPlayerIndex: 0, featuredIcon: Icon.Leaf);
        var h = new ReformationTuckHandler();

        Assert.False(h.Execute(g, me, ctx));
        var req = (SelectHandCardSubsetRequest)ctx.PendingChoice!;
        req.ChosenCardIds = new[] { invention, physics };
        ctx.Paused = false;

        Assert.True(h.Execute(g, me, ctx));
        Assert.Empty(me.Hand);
        Assert.Contains(invention, me.Stack(g.Cards[invention].Color).Cards);
        Assert.Contains(physics, me.Stack(g.Cards[physics].Color).Cards);
    }

    private sealed class ScriptedController : IPlayerController
    {
        public Queue<IReadOnlyList<int>> SubsetAnswers { get; } = new();
        public Queue<CardColor?> ColorAnswers { get; } = new();
        public Queue<PlayerAction> ActionAnswers { get; } = new();
        public int ChooseInitialMeld(GameState g, PlayerState self) => self.Hand[0];
        public PlayerAction ChooseAction(GameState g, PlayerState self, IReadOnlyList<PlayerAction> legal) => ActionAnswers.Dequeue();
        public int? ChooseHandCard(GameState g, PlayerState self, SelectHandCardRequest req) => null;
        public IReadOnlyList<int> ChooseHandCardSubset(GameState g, PlayerState self, SelectHandCardSubsetRequest req) => SubsetAnswers.Dequeue();
        public bool ChooseYesNo(GameState g, PlayerState self, YesNoChoiceRequest req) => false;
        public CardColor? ChooseColor(GameState g, PlayerState self, SelectColorRequest req) => ColorAnswers.Dequeue();
        public int? ChooseScoreCard(GameState g, PlayerState self, SelectScoreCardRequest req) => null;
        public IReadOnlyList<int> ChooseStackOrder(GameState g, PlayerState self, SelectStackOrderRequest req) => req.CurrentOrder.ToList();
        public int? ChooseValue(GameState g, PlayerState self, SelectValueRequest req) => null;
    }

    [Fact]
    public void TwoActivations_SecondTucksTwoAndSplays_ViaRunner()
    {
        var g = Fresh();
        var me = g.Players[0];
        int invention = IdOf("Invention");
        int physics = IdOf("Physics");
        int reformation = IdOf("Reformation");
        // Stack many leaf-bearing cards on Purple so Reformation is the top
        // AND there are 10+ leaves on board. Reformation is melded LAST so
        // it's on top of Purple.
        var leafyOthers = new[] { "Agriculture", "Pottery", "Translation", "Medicine", "Code of Laws", "Domestication", "Tools" }
            .Select(IdOf).ToArray();
        foreach (var id in leafyOthers)
            me.Stack(g.Cards[id].Color).Meld(id);
        me.Stack(g.Cards[reformation].Color).Meld(reformation);
        // Hand: 7 cards (5 to tuck first activation + Invention + Physics).
        var firstBatch = new[] { "Colonialism", "Enterprise", "Sailing", "Alchemy", "Navigation" }
            .Select(IdOf).ToArray();
        foreach (var id in firstBatch) me.Hand.Add(id);
        me.Hand.Add(invention);
        me.Hand.Add(physics);

        var ctrl = new ScriptedController();
        ctrl.ActionAnswers.Enqueue(new DogmaAction(g.Cards[reformation].Color));
        ctrl.SubsetAnswers.Enqueue(firstBatch);
        ctrl.ColorAnswers.Enqueue(CardColor.Purple);
        ctrl.ActionAnswers.Enqueue(new DogmaAction(g.Cards[reformation].Color));
        ctrl.SubsetAnswers.Enqueue(new[] { invention, physics });
        ctrl.ColorAnswers.Enqueue(CardColor.Yellow);

        var p2 = new ScriptedController();

        g.Phase = GamePhase.Action;
        g.ActivePlayer = 0;
        g.ActionsRemaining = 2;
        g.CurrentTurn = 27;

        var runner = new GameRunner(g, new IPlayerController[] { ctrl, p2 });
        // Drain steps until either turn passes or we run out of canned answers.
        for (int i = 0; i < 30 && g.ActivePlayer == 0 && !g.IsGameOver; i++)
            runner.Step();

        Assert.DoesNotContain(invention, me.Hand);
        Assert.DoesNotContain(physics, me.Hand);
        Assert.Contains(invention, me.Stack(g.Cards[invention].Color).Cards);
        Assert.Contains(physics, me.Stack(g.Cards[physics].Color).Cards);
        Assert.Equal(Splay.Right, me.Stack(CardColor.Yellow).Splay);
    }
}
