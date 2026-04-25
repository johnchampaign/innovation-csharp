using System.Text;
using Innovation.Core;
using Innovation.Core.Players;
using Xunit;

namespace Innovation.Tests;

/// <summary>
/// <see cref="HumanController"/> is deliberately a pure forwarder to
/// <see cref="IUserPromptSink"/>. These tests lock that in: every
/// method must delegate to the sink, return the sink's answer, and
/// pass the same request object through unmodified. If the controller
/// ever grows branching logic (e.g. "auto-confirm when only one legal
/// move") the sink tests flag the behavior change.
///
/// Threading is the sink's problem, not the controller's — so nothing
/// here touches <see cref="System.Windows.Forms"/> and the tests run
/// in a plain xUnit worker.
/// </summary>
public class HumanControllerTests
{
    static HumanControllerTests()
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
    }

    private static IReadOnlyList<Card> AllCards => CardDataLoader.LoadFromEmbeddedResource();

    private sealed class RecordingSink : IUserPromptSink
    {
        public int InitialMeldCalls;
        public int ActionCalls;
        public int HandCardCalls;
        public int SubsetCalls;
        public int YesNoCalls;
        public int ColorCalls;
        public int ScoreCardCalls;

        public int InitialMeldAnswer;
        public PlayerAction ActionAnswer = new DrawAction();
        public int? HandCardAnswer;
        public IReadOnlyList<int> SubsetAnswer = Array.Empty<int>();
        public bool YesNoAnswer;
        public CardColor? ColorAnswer;
        public int? ScoreCardAnswer;

        public SelectHandCardRequest? LastHandRequest;
        public SelectHandCardSubsetRequest? LastSubsetRequest;
        public YesNoChoiceRequest? LastYesNoRequest;
        public SelectColorRequest? LastColorRequest;
        public SelectScoreCardRequest? LastScoreCardRequest;

        public int PromptInitialMeld(GameState g, PlayerState self)
        {
            InitialMeldCalls++;
            return InitialMeldAnswer;
        }

        public PlayerAction PromptAction(GameState g, PlayerState self, IReadOnlyList<PlayerAction> legal)
        {
            ActionCalls++;
            return ActionAnswer;
        }

        public int? PromptHandCard(GameState g, PlayerState self, SelectHandCardRequest req)
        {
            HandCardCalls++;
            LastHandRequest = req;
            return HandCardAnswer;
        }

        public IReadOnlyList<int> PromptHandCardSubset(GameState g, PlayerState self, SelectHandCardSubsetRequest req)
        {
            SubsetCalls++;
            LastSubsetRequest = req;
            return SubsetAnswer;
        }

        public bool PromptYesNo(GameState g, PlayerState self, YesNoChoiceRequest req)
        {
            YesNoCalls++;
            LastYesNoRequest = req;
            return YesNoAnswer;
        }

        public CardColor? PromptColor(GameState g, PlayerState self, SelectColorRequest req)
        {
            ColorCalls++;
            LastColorRequest = req;
            return ColorAnswer;
        }

        public int? PromptScoreCard(GameState g, PlayerState self, SelectScoreCardRequest req)
        {
            ScoreCardCalls++;
            LastScoreCardRequest = req;
            return ScoreCardAnswer;
        }

        public IReadOnlyList<int> PromptStackOrder(GameState g, PlayerState self, SelectStackOrderRequest req)
            => req.CurrentOrder;

        public int? PromptValue(GameState g, PlayerState self, SelectValueRequest req) => null;
    }

    [Fact]
    public void Ctor_NullSink_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => new HumanController(null!));
    }

    [Fact]
    public void ChooseInitialMeld_Forwards()
    {
        var g = new GameState(AllCards, 2);
        var sink = new RecordingSink { InitialMeldAnswer = 42 };
        var hc = new HumanController(sink);
        int pick = hc.ChooseInitialMeld(g, g.Players[0]);
        Assert.Equal(42, pick);
        Assert.Equal(1, sink.InitialMeldCalls);
    }

    [Fact]
    public void ChooseAction_Forwards()
    {
        var g = new GameState(AllCards, 2);
        var expected = new MeldAction(3);
        var sink = new RecordingSink { ActionAnswer = expected };
        var hc = new HumanController(sink);
        var picked = hc.ChooseAction(g, g.Players[0], new[] { (PlayerAction)new DrawAction(), expected });
        Assert.Same(expected, picked);
        Assert.Equal(1, sink.ActionCalls);
    }

    [Fact]
    public void ChooseHandCard_PassesRequestAndReturnsAnswer()
    {
        var g = new GameState(AllCards, 2);
        var sink = new RecordingSink { HandCardAnswer = 7 };
        var hc = new HumanController(sink);
        var req = new SelectHandCardRequest
        {
            PlayerIndex = 0,
            EligibleCardIds = new[] { 1, 2, 7 },
            AllowNone = true,
            Prompt = "pick one",
        };
        var answer = hc.ChooseHandCard(g, g.Players[0], req);
        Assert.Equal(7, answer);
        Assert.Same(req, sink.LastHandRequest);
    }

    [Fact]
    public void ChooseHandCard_NullAnswerPropagates()
    {
        // AllowNone declines must round-trip as null, not as default(int).
        var g = new GameState(AllCards, 2);
        var sink = new RecordingSink { HandCardAnswer = null };
        var hc = new HumanController(sink);
        var req = new SelectHandCardRequest { AllowNone = true };
        Assert.Null(hc.ChooseHandCard(g, g.Players[0], req));
    }

    [Fact]
    public void ChooseHandCardSubset_Forwards()
    {
        var g = new GameState(AllCards, 2);
        var picks = new[] { 1, 2 };
        var sink = new RecordingSink { SubsetAnswer = picks };
        var hc = new HumanController(sink);
        var req = new SelectHandCardSubsetRequest
        {
            EligibleCardIds = new[] { 1, 2, 3 },
            MinCount = 1,
            MaxCount = 2,
        };
        var answer = hc.ChooseHandCardSubset(g, g.Players[0], req);
        Assert.Same(picks, answer);
        Assert.Same(req, sink.LastSubsetRequest);
    }

    [Fact]
    public void ChooseYesNo_Forwards()
    {
        var g = new GameState(AllCards, 2);
        var sink = new RecordingSink { YesNoAnswer = true };
        var hc = new HumanController(sink);
        var req = new YesNoChoiceRequest { Prompt = "splay?" };
        Assert.True(hc.ChooseYesNo(g, g.Players[0], req));
        Assert.Same(req, sink.LastYesNoRequest);
    }

    [Fact]
    public void ChooseColor_Forwards()
    {
        var g = new GameState(AllCards, 2);
        var sink = new RecordingSink { ColorAnswer = CardColor.Red };
        var hc = new HumanController(sink);
        var req = new SelectColorRequest
        {
            EligibleColors = new[] { CardColor.Red, CardColor.Blue },
        };
        Assert.Equal(CardColor.Red, hc.ChooseColor(g, g.Players[0], req));
        Assert.Same(req, sink.LastColorRequest);
    }

    [Fact]
    public void HumanController_DrivesFullGameViaFakeSink()
    {
        // End-to-end: a deterministic fake sink (always picks the first
        // legal option / first hand card / yes / first color) plugs into
        // GameRunner exactly like any AI controller and finishes a game.
        // This is the smoke test that the HumanController + IUserPromptSink
        // pair is sufficient to drive the engine, independent of any UI.
        var fake = new FirstChoiceSink();
        var g = GameSetup.Create(AllCards, 2, new Random(123));
        var runner = new GameRunner(g, new IPlayerController[]
        {
            new HumanController(fake),
            new RandomController(123),
        });
        runner.CompleteInitialMeld();
        runner.RunToCompletion();
        Assert.True(g.IsGameOver);
    }

    private sealed class FirstChoiceSink : IUserPromptSink
    {
        public int PromptInitialMeld(GameState g, PlayerState self) => self.Hand[0];
        public PlayerAction PromptAction(GameState g, PlayerState self, IReadOnlyList<PlayerAction> legal) => legal[0];
        public int? PromptHandCard(GameState g, PlayerState self, SelectHandCardRequest req)
            => req.EligibleCardIds.Count > 0 ? req.EligibleCardIds[0] : (req.AllowNone ? (int?)null : throw new InvalidOperationException());
        public IReadOnlyList<int> PromptHandCardSubset(GameState g, PlayerState self, SelectHandCardSubsetRequest req)
            => req.EligibleCardIds.Take(req.MinCount).ToList();
        public bool PromptYesNo(GameState g, PlayerState self, YesNoChoiceRequest req) => true;
        public CardColor? PromptColor(GameState g, PlayerState self, SelectColorRequest req)
            => req.EligibleColors.Count > 0 ? req.EligibleColors[0] : null;
        public int? PromptScoreCard(GameState g, PlayerState self, SelectScoreCardRequest req)
            => req.EligibleCardIds.Count > 0 ? req.EligibleCardIds[0] : (req.AllowNone ? (int?)null : throw new InvalidOperationException());
        public IReadOnlyList<int> PromptStackOrder(GameState g, PlayerState self, SelectStackOrderRequest req)
            => req.CurrentOrder;
        public int? PromptValue(GameState g, PlayerState self, SelectValueRequest req)
            => req.EligibleValues.Count > 0 ? req.EligibleValues[0] : (req.AllowNone ? (int?)null : throw new InvalidOperationException());
    }
}
