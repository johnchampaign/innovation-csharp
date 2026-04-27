namespace Innovation.Core.Players;

/// <summary>
/// Decision-maker for one seat in a game. The <see cref="GameRunner"/> calls
/// the appropriate method whenever that seat needs to answer a question —
/// the top-level "what action do I take" query, plus every in-dogma
/// <see cref="ChoiceRequest"/> prompt.
///
/// Implementations may inspect the full <see cref="GameState"/>. The C#
/// port doesn't enforce hidden-information secrecy — humans and AIs share
/// this interface and the driver trusts their return values. If hidden-
/// info ever matters, a wrapper can project <see cref="GameState"/> into a
/// per-seat view before invoking.
/// </summary>
public interface IPlayerController
{
    /// <summary>
    /// Pick the card to meld from this player's opening hand. Called once
    /// per player at setup. Must return an id that appears in
    /// <see cref="PlayerState.Hand"/>.
    /// </summary>
    int ChooseInitialMeld(GameState g, PlayerState self);

    /// <summary>
    /// Pick one of the <paramref name="legal"/> actions for this turn.
    /// Must return one of the supplied options verbatim — the runner
    /// doesn't re-validate eligibility before handing it to
    /// <see cref="TurnManager"/>.
    /// </summary>
    PlayerAction ChooseAction(GameState g, PlayerState self, IReadOnlyList<PlayerAction> legal);

    /// <summary>
    /// Answer a pending "choose one card from your hand" prompt. Returning
    /// null is only legal when <see cref="SelectHandCardRequest.AllowNone"/>
    /// is true; handlers that require a pick may crash on null.
    /// </summary>
    int? ChooseHandCard(GameState g, PlayerState self, SelectHandCardRequest req);

    /// <summary>
    /// Answer a pending "choose N cards from your hand" prompt. The returned
    /// count must be within <c>[MinCount, MaxCount]</c> and every id must
    /// appear in <see cref="SelectHandCardSubsetRequest.EligibleCardIds"/>.
    /// </summary>
    IReadOnlyList<int> ChooseHandCardSubset(GameState g, PlayerState self, SelectHandCardSubsetRequest req);

    /// <summary>Answer a pending yes/no prompt.</summary>
    bool ChooseYesNo(GameState g, PlayerState self, YesNoChoiceRequest req);

    /// <summary>
    /// Answer a pending "choose one color" prompt. Returning null is only
    /// legal if the handler advertised the prompt as optional (most use-
    /// sites — City States etc. — require a pick).
    /// </summary>
    CardColor? ChooseColor(GameState g, PlayerState self, SelectColorRequest req);

    /// <summary>
    /// Answer a pending "choose one card from a score pile" prompt.
    /// Returning null is only legal when
    /// <see cref="SelectScoreCardRequest.AllowNone"/> is true.
    /// </summary>
    int? ChooseScoreCard(GameState g, PlayerState self, SelectScoreCardRequest req);

    /// <summary>
    /// Answer a pending "choose N cards from a score pile" prompt. Returned
    /// count must be within <c>[MinCount, MaxCount]</c> and every id must
    /// appear in <see cref="SelectScoreCardSubsetRequest.EligibleCardIds"/>.
    /// </summary>
    IReadOnlyList<int> ChooseScoreCardSubset(GameState g, PlayerState self, SelectScoreCardSubsetRequest req);

    /// <summary>
    /// Answer a pending "rearrange pile order" prompt. The returned list
    /// must be a permutation of <see cref="SelectStackOrderRequest.CurrentOrder"/>.
    /// Returning the same order is a valid no-op pick.
    /// </summary>
    IReadOnlyList<int> ChooseStackOrder(GameState g, PlayerState self, SelectStackOrderRequest req);

    /// <summary>
    /// Answer a pending "order these cards" prompt for a multi-meld /
    /// multi-tuck / multi-return step. Default is to keep the input order
    /// (the AI doesn't care; humans will see a reorder dialog).
    /// </summary>
    IReadOnlyList<int> ChooseCardOrder(GameState g, PlayerState self, SelectCardOrderRequest req);

    /// <summary>
    /// Answer a pending "choose a card value" prompt. Returning null is only
    /// legal when <see cref="SelectValueRequest.AllowNone"/> is true.
    /// </summary>
    int? ChooseValue(GameState g, PlayerState self, SelectValueRequest req);
}
