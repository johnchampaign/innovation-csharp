namespace Innovation.Core.Players;

/// <summary>
/// <see cref="IPlayerController"/> that routes every decision to a
/// human via an <see cref="IUserPromptSink"/>. Intentionally a thin
/// forwarder: the sink is where thread marshalling and UI blocking
/// lives, so the controller itself has no WinForms dependency and no
/// synchronization logic to test.
///
/// The controller exists as a distinct type (rather than using the
/// sink directly) so <see cref="GameRunner"/>'s seat-agnostic driver
/// treats human seats the same as AI seats — it doesn't need to know
/// which seats block on user input.
/// </summary>
public sealed class HumanController : IPlayerController
{
    private readonly IUserPromptSink _sink;

    public HumanController(IUserPromptSink sink)
    {
        _sink = sink ?? throw new ArgumentNullException(nameof(sink));
    }

    public int ChooseInitialMeld(GameState g, PlayerState self)
        => _sink.PromptInitialMeld(g, self);

    public PlayerAction ChooseAction(
        GameState g, PlayerState self, IReadOnlyList<PlayerAction> legal)
        => _sink.PromptAction(g, self, legal);

    public int? ChooseHandCard(GameState g, PlayerState self, SelectHandCardRequest req)
        => _sink.PromptHandCard(g, self, req);

    public IReadOnlyList<int> ChooseHandCardSubset(
        GameState g, PlayerState self, SelectHandCardSubsetRequest req)
        => _sink.PromptHandCardSubset(g, self, req);

    public bool ChooseYesNo(GameState g, PlayerState self, YesNoChoiceRequest req)
        => _sink.PromptYesNo(g, self, req);

    public CardColor? ChooseColor(GameState g, PlayerState self, SelectColorRequest req)
        => _sink.PromptColor(g, self, req);

    public int? ChooseScoreCard(GameState g, PlayerState self, SelectScoreCardRequest req)
        => _sink.PromptScoreCard(g, self, req);

    public IReadOnlyList<int> ChooseScoreCardSubset(GameState g, PlayerState self, SelectScoreCardSubsetRequest req)
        => _sink.PromptScoreCardSubset(g, self, req);

    public IReadOnlyList<int> ChooseStackOrder(GameState g, PlayerState self, SelectStackOrderRequest req)
        => _sink.PromptStackOrder(g, self, req);

    public IReadOnlyList<int> ChooseCardOrder(GameState g, PlayerState self, SelectCardOrderRequest req)
        => _sink.PromptCardOrder(g, self, req);

    public int? ChooseValue(GameState g, PlayerState self, SelectValueRequest req)
        => _sink.PromptValue(g, self, req);
}
