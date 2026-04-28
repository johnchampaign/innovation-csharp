namespace Innovation.Core;

/// <summary>
/// Core game actions that mutate <see cref="GameState"/>. Mirrors the VB6
/// subs in main.frm (draw, meld, tuck, score, splay, end_game_10).
///
/// <para><b>Invariant — action cost lives one level up.</b>
/// Every method here is "free": it mutates state, but never touches
/// <c>ActionsRemaining</c> and never decides whether the current player's
/// turn ends. Only <see cref="TurnManager.Apply"/> decrements the action
/// counter. That way the same <see cref="Draw"/> primitive serves both a
/// top-level <c>DrawAction</c> (one action spent) and The Wheel's
/// "draw two 1s" dogma (no actions spent, however many draws). This
/// scales to recursive/chained dogma: Metalworking-style self-repeat,
/// or a draw-and-meld that triggers another card's on-meld effect,
/// composes cleanly because none of it re-enters the action-cost layer.</para>
///
/// <para><b>Invariant — handlers are flat.</b>
/// Dogma handlers compose <c>Mechanics.*</c> calls. Handlers never call
/// other handlers. If Card X's effect "does what Card Y's dogma does,"
/// factor that body into a <c>Mechanics.*</c> helper and call it from
/// both handlers — otherwise cascading dogmas form hidden graphs and
/// debugging becomes archaeology.</para>
/// </summary>
public static class Mechanics
{
    /// <summary>True if any of a card's four icon slots displays <paramref name="icon"/>.</summary>
    public static bool HasIcon(Card c, Icon icon) =>
        c.Top == icon || c.Left == icon || c.Middle == icon || c.Right == icon;

    /// <summary>
    /// Highest age among the top cards of a player's board.
    /// Matches VB6 highest_top_card().
    /// </summary>
    public static int HighestTopCardAge(GameState g, PlayerState p)
    {
        int max = 0;
        foreach (var stack in p.Stacks)
        {
            if (stack.IsEmpty) continue;
            int age = g.Cards[stack.Top].Age;
            if (age > max) max = age;
        }
        return max;
    }

    /// <summary>
    /// Find the age to draw for this player: start from their highest top card
    /// (or age 1 if their board is empty), then walk up until a non-empty deck
    /// is found or we run off the top.
    /// Returns 11 when there's nothing left — the caller should end the game.
    /// Matches VB6 find_next_draw().
    /// </summary>
    public static int FindNextDrawAge(GameState g, PlayerState p)
    {
        int age = HighestTopCardAge(g, p);
        if (age == 0) age = 1;
        while (age <= 10 && g.Decks[age].Count == 0)
            age++;
        return age; // 11 means no card available
    }

    /// <summary>
    /// Draw a card for a player. Returns the card ID drawn, or -1 if the
    /// draw ran off the top of the deck (in which case the game is now over).
    ///
    /// This fixes VB6 bug #1: when the age-11 case hits, the original code
    /// pushed card 0 (Agriculture) into the player's hand as a "hack" and
    /// returned 0 to callers, causing cascading bogus draws inside looping
    /// dogmas. Here we end the game cleanly and return -1; callers must
    /// check for game over.
    /// </summary>
    public static int Draw(GameState g, PlayerState p) =>
        DrawFromAge(g, p, FindNextDrawAge(g, p));

    /// <summary>
    /// Draw a card starting from <paramref name="startingAge"/>. If that deck
    /// is empty, walk up (per VB6 <c>draw</c> sub) until a non-empty deck is
    /// found. Returns -1 and ends the game if no deck 1..10 has any cards
    /// left.
    /// </summary>
    public static int DrawFromAge(GameState g, PlayerState p, int startingAge)
    {
        int age = startingAge;
        while (age <= 10 && g.Decks[age].Count == 0) age++;
        return DrawFromAgeOrHigher(g, p, age);
    }

    private static int DrawFromAgeOrHigher(GameState g, PlayerState p, int age)
    {
        if (age > 10)
        {
            GameLog.Log($"{GameLog.P(p)} draw from age>10 — game ends");
            EndGameOnEmptyDeck(g, p);
            return -1;
        }
        var deck = g.Decks[age];
        int cardId = deck[0];
        deck.RemoveAt(0);
        p.Hand.Add(cardId);
        GameLog.Log($"{GameLog.P(p)} draws {GameLog.C(g, cardId)} (hand={p.Hand.Count})");
        return cardId;
    }

    public static void Meld(GameState g, PlayerState p, int cardId)
    {
        p.Hand.Remove(cardId);
        var color = g.Cards[cardId].Color;
        p.Stack(color).Meld(cardId);
        GameLog.Log($"{GameLog.P(p)} melds {GameLog.C(g, cardId)}");
        SpecialAchievements.CheckAll(g);
    }

    /// <summary>
    /// Return a card from a player's hand to the bottom of its age deck.
    /// Per Innovation rules, returned cards go under the deck — the next
    /// draw from that age pulls the original top, not the just-returned
    /// card. (VB6 <c>return_from_hand</c> pushed to the top; that was a
    /// faithfulness bug, corrected here.)
    /// </summary>
    public static void Return(GameState g, PlayerState p, int cardId)
    {
        p.Hand.Remove(cardId);
        int age = g.Cards[cardId].Age;
        g.Decks[age].Add(cardId);
        GameLog.Log($"{GameLog.P(p)} returns {GameLog.C(g, cardId)}");
    }

    public static void Tuck(GameState g, PlayerState p, int cardId)
    {
        p.Hand.Remove(cardId);
        var color = g.Cards[cardId].Color;
        p.Stack(color).Tuck(cardId);
        p.TuckedThisTurn++;
        GameLog.Log($"{GameLog.P(p)} tucks {GameLog.C(g, cardId)}");
        if (p.TuckedThisTurn >= 6)
            AchievementRules.ClaimSpecial(g, p, "Monument");
        SpecialAchievements.CheckAll(g);
    }

    public static void Score(GameState g, PlayerState p, int cardId)
    {
        p.Hand.Remove(cardId);
        p.ScorePile.Add(cardId);
        p.ScoredThisTurn++;
        GameLog.Log($"{GameLog.P(p)} scores {GameLog.C(g, cardId)}");
        if (p.ScoredThisTurn >= 6)
            AchievementRules.ClaimSpecial(g, p, "Monument");
        // Score doesn't affect icon count, but a score pile change can still
        // matter for future hooks; keep the check to match VB6 uniformity.
        SpecialAchievements.CheckAll(g);
    }

    /// <summary>
    /// Move a card from one player's hand to another's hand. Mirrors VB6
    /// <c>transfer_card_in_hand</c> (main.frm 4024): remove from source hand,
    /// push to destination hand. No icon/achievement recompute is needed
    /// (hand contents don't contribute to either, and neither does VB6 call
    /// <c>check_for_achievements</c> here).
    ///
    /// Used by Archery's demand ("transfer the highest card in your hand to
    /// my hand"). The caller is responsible for selecting <paramref name="cardId"/>
    /// — this method doesn't enforce "highest" or any other rule.
    /// </summary>
    public static void TransferHandToHand(GameState g, PlayerState from, PlayerState to, int cardId)
    {
        from.Hand.Remove(cardId);
        to.Hand.Add(cardId);
        GameLog.Log($"transfer hand→hand {GameLog.C(g, cardId)}: {GameLog.P(from)} → {GameLog.P(to)}");
    }

    /// <summary>
    /// Move a card from one player's hand to another's score pile. Mirrors
    /// VB6 <c>transfer_hand_to_score</c> (main.frm 4033). Used by Oars's
    /// demand ("transfer a card with a [Crown] from your hand to my score
    /// pile").
    ///
    /// Monument cares about cards a player *scored* this turn, not cards
    /// that happened to land in their score pile. A transfer isn't a score
    /// — Canal Building's hand↔score swap is the canonical example: you
    /// can't Monument off it. So <see cref="PlayerState.ScoredThisTurn"/>
    /// does not bump here. (Shared dogmas like Metalworking are different
    /// — the opponent executes an actual Score on their own turn-in-effect,
    /// which goes through <see cref="Score"/> and does count.)
    /// </summary>
    public static void TransferHandToScore(GameState g, PlayerState from, PlayerState to, int cardId)
    {
        from.Hand.Remove(cardId);
        to.ScorePile.Add(cardId);
        GameLog.Log($"transfer hand→score {GameLog.C(g, cardId)}: {GameLog.P(from)} → {GameLog.P(to)} score");
        SpecialAchievements.CheckAll(g);
    }

    /// <summary>
    /// Move the top card of a source player's color pile onto the top of the
    /// destination player's same-color pile. Mirrors VB6 <c>transfer_card_on_board</c>
    /// (main.frm 4056). Used by City States (top Castle-card transfer).
    ///
    /// Returns true if a card actually moved. A transfer from an empty pile
    /// is a no-op (VB6 returns silently via <c>If card = -1 Then Exit Sub</c>).
    ///
    /// Side effects that differ from <see cref="Meld"/>:
    ///   • Source pile may shrink below 2, which resets its splay
    ///     (<see cref="ColorStack.PopTop"/>).
    ///   • Neither player's per-turn counters change (this isn't a meld by
    ///     the receiving player, it's a transfer).
    ///   • Board icons change on both sides, so re-check special achievements.
    /// </summary>
    public static bool TransferBoardToBoard(GameState g, PlayerState from, PlayerState to, CardColor color)
    {
        var src = from.Stack(color);
        if (src.IsEmpty) return false;

        int cardId = src.PopTop();
        to.Stack(color).Meld(cardId);
        GameLog.Log($"transfer board→board {GameLog.C(g, cardId)}: {GameLog.P(from)} → {GameLog.P(to)}");
        SpecialAchievements.CheckAll(g);
        return true;
    }

    /// <summary>
    /// Move a card from one player's score pile to another's score pile.
    /// Used by Mapmaking's demand ("transfer a 1 from your score pile to
    /// my score pile") and Optics's conditional transfer.
    ///
    /// Not a score — see <see cref="TransferHandToScore"/>. Neither
    /// player's per-turn counters change; the destination's score total
    /// did, so re-check specials.
    /// </summary>
    public static void TransferScoreToScore(GameState g, PlayerState from, PlayerState to, int cardId)
    {
        from.ScorePile.Remove(cardId);
        to.ScorePile.Add(cardId);
        GameLog.Log($"transfer score→score {GameLog.C(g, cardId)}: {GameLog.P(from)} → {GameLog.P(to)}");
        SpecialAchievements.CheckAll(g);
    }

    /// <summary>
    /// Move the top card of a source player's color pile into the
    /// destination player's score pile. Used by Monotheism's demand.
    /// Not a score — no Monument bump. The source pile may shrink below
    /// 2 and reset its splay.
    ///
    /// Returns the moved card id, or -1 if the source pile was empty.
    /// </summary>
    public static int TransferBoardToScore(GameState g, PlayerState from, PlayerState to, CardColor color)
    {
        var src = from.Stack(color);
        if (src.IsEmpty) return -1;
        int cardId = src.PopTop();
        to.ScorePile.Add(cardId);
        GameLog.Log($"transfer board→score {GameLog.C(g, cardId)}: {GameLog.P(from)} → {GameLog.P(to)} score");
        SpecialAchievements.CheckAll(g);
        return cardId;
    }

    /// <summary>
    /// Score a card that's already on the board (top or any covered card
    /// of a stack). The card moves into the owner's score pile; this
    /// counts as a score (bumps ScoredThisTurn, may fire Monument). Used
    /// by Coal (effect 3), Steam Engine (bottom-yellow), and Pirate Code
    /// (effect 2 lowest-Crown top).
    /// </summary>
    public static void ScoreFromBoard(GameState g, PlayerState p, CardColor color, int cardId)
    {
        var stack = p.Stack(color);
        if (stack.Top == cardId) stack.PopTop();
        else stack.RemoveCoveredCard(cardId);
        p.ScorePile.Add(cardId);
        p.ScoredThisTurn++;
        GameLog.Log($"{GameLog.P(p)} scores from board {GameLog.C(g, cardId)}");
        if (p.ScoredThisTurn >= 6)
            AchievementRules.ClaimSpecial(g, p, "Monument");
        SpecialAchievements.CheckAll(g);
    }

    /// <summary>
    /// Move a card from a player's score pile back to their hand. Not a
    /// score — no Monument bump. Used by Canal Building's hand↔score
    /// exchange.
    /// </summary>
    public static void TransferScoreToHand(GameState g, PlayerState p, int cardId)
    {
        p.ScorePile.Remove(cardId);
        p.Hand.Add(cardId);
        GameLog.Log($"{GameLog.P(p)} moves {GameLog.C(g, cardId)} from score→hand");
        SpecialAchievements.CheckAll(g);
    }

    /// <summary>
    /// Draw a card starting from <paramref name="startingAge"/>, then
    /// immediately score it. Returns the drawn card id, or -1 if the
    /// draw overflowed the age-10 deck (game ended). Mirrors the common
    /// VB6 "draw_and_score" pattern (Agriculture, Pottery).
    /// </summary>
    public static int DrawAndScore(GameState g, PlayerState p, int startingAge)
    {
        int id = DrawFromAge(g, p, startingAge);
        if (id < 0) return -1;
        Score(g, p, id);
        return id;
    }

    /// <summary>
    /// Draw a card starting from <paramref name="startingAge"/>, then
    /// immediately meld it. Returns the drawn card id, or -1 on deck
    /// overflow. Used by Sailing, Tools, and Mathematics.
    /// </summary>
    public static int DrawAndMeld(GameState g, PlayerState p, int startingAge)
    {
        int id = DrawFromAge(g, p, startingAge);
        if (id < 0) return -1;
        Meld(g, p, id);
        return id;
    }

    /// <summary>
    /// Draw a card starting from <paramref name="startingAge"/>, then
    /// immediately tuck it. Returns the drawn card id, or -1 on deck
    /// overflow. Used by Monotheism.
    /// </summary>
    public static int DrawAndTuck(GameState g, PlayerState p, int startingAge)
    {
        int id = DrawFromAge(g, p, startingAge);
        if (id < 0) return -1;
        Tuck(g, p, id);
        return id;
    }

    /// <summary>
    /// Attempt to splay a color pile. Returns true if the splay changed.
    /// A pile with fewer than 2 cards, or already in the requested direction,
    /// returns false. Mirrors VB6 can_splay + splay().
    /// </summary>
    public static bool Splay(GameState g, PlayerState p, CardColor color, Splay direction)
    {
        bool changed = p.Stack(color).ApplySplay(direction);
        if (changed)
        {
            GameLog.Log($"{GameLog.P(p)} splays {color} {direction}");
            SpecialAchievements.CheckAll(g);
        }
        return changed;
    }

    /// <summary>
    /// Queue "execute <paramref name="cardId"/>'s non-demand dogma effects for
    /// <paramref name="target"/> only" onto the current dogma resolution.
    /// The engine drains the queue after the caller's handler returns.
    /// Used by Robotics, Computers, Satellites effect 3, Software effect 2,
    /// and Self Service.
    /// </summary>
    public static void ExecuteSelfOnly(DogmaContext ctx, int cardId, PlayerState target)
    {
        ctx.NestedFrames.Push(new NestedDogmaFrame
        {
            CardId = cardId,
            TargetPlayerIndex = target.Index,
        });
    }

    /// <summary>
    /// Ends the game because a player tried to draw beyond age 10.
    /// Winners = the players with the highest <c>10*score + achievements</c>.
    /// Mirrors VB6 end_game_10 (main.frm line 7719).
    /// </summary>
    public static void EndGameOnEmptyDeck(GameState g, PlayerState _triggeringPlayer)
    {
        if (g.IsGameOver) return;

        int best = int.MinValue;
        foreach (var p in g.Players)
        {
            int s = g.FinalScore(p);
            if (s > best) best = s;
        }
        g.Winners.Clear();
        foreach (var p in g.Players)
            if (g.FinalScore(p) == best) g.Winners.Add(p.Index);
        g.Phase = GamePhase.GameOver;
    }

    /// <summary>
    /// Validate a controller's response to a <see cref="SelectCardOrderRequest"/>.
    /// Returns <paramref name="chosen"/> if it's a valid permutation of
    /// <paramref name="input"/>; otherwise falls back to <paramref name="input"/>.
    /// Used by handlers that meld / tuck / return multiple cards in player-
    /// chosen order.
    /// </summary>
    public static IReadOnlyList<int> ValidateOrder(IReadOnlyList<int> chosen, IReadOnlyList<int> input)
    {
        if (chosen.Count == input.Count
            && chosen.Distinct().Count() == chosen.Count
            && chosen.All(input.Contains))
            return chosen;
        return input;
    }

    /// <summary>
    /// True iff at least two ids in <paramref name="ids"/> share a destination
    /// key. The order of operations only matters when multiple cards land
    /// in the same destination — for returns that's the age deck, for
    /// melds/tucks that's the color stack. When every destination has at
    /// most one card, all orderings are equivalent and the handler should
    /// skip the player's order prompt entirely.
    /// </summary>
    public static bool OrderMatters<T>(IEnumerable<int> ids, Func<int, T> destination)
        where T : notnull
    {
        var seen = new HashSet<T>();
        foreach (var id in ids)
        {
            if (!seen.Add(destination(id))) return true;
        }
        return false;
    }
}
