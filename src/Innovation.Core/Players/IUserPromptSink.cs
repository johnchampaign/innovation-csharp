namespace Innovation.Core.Players;

/// <summary>
/// Contract between <see cref="HumanController"/> and whatever UI is
/// driving a human seat. Each method must block the calling thread
/// until the user has provided an answer, then return it. The UI layer
/// (typically a WinForms <c>GameForm</c>) is responsible for all the
/// threading details — marshalling onto the UI thread, disabling
/// illegal actions, blocking on a TaskCompletionSource, etc.
///
/// Split out from <see cref="HumanController"/> itself so Core has no
/// WinForms dependency and the controller is testable with a fake sink.
/// </summary>
public interface IUserPromptSink
{
    int PromptInitialMeld(GameState g, PlayerState self);

    PlayerAction PromptAction(
        GameState g, PlayerState self, IReadOnlyList<PlayerAction> legal);

    int? PromptHandCard(GameState g, PlayerState self, SelectHandCardRequest req);

    IReadOnlyList<int> PromptHandCardSubset(
        GameState g, PlayerState self, SelectHandCardSubsetRequest req);

    bool PromptYesNo(GameState g, PlayerState self, YesNoChoiceRequest req);

    CardColor? PromptColor(GameState g, PlayerState self, SelectColorRequest req);

    int? PromptScoreCard(GameState g, PlayerState self, SelectScoreCardRequest req);

    IReadOnlyList<int> PromptScoreCardSubset(
        GameState g, PlayerState self, SelectScoreCardSubsetRequest req);

    IReadOnlyList<int> PromptStackOrder(GameState g, PlayerState self, SelectStackOrderRequest req);

    IReadOnlyList<int> PromptCardOrder(GameState g, PlayerState self, SelectCardOrderRequest req);

    int? PromptValue(GameState g, PlayerState self, SelectValueRequest req);
}
