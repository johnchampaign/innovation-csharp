using System.Text;
using Innovation.Core;
using Xunit;

namespace Innovation.Tests;

public class TurnManagerTests
{
    static TurnManagerTests()
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
    }

    private static IReadOnlyList<Card> AllCards => CardDataLoader.LoadFromEmbeddedResource();

    private static (GameState g, TurnManager tm) NewGame(int players = 2, int seed = 42)
    {
        var g = GameSetup.Create(AllCards, players, new Random(seed));
        return (g, new TurnManager(g));
    }

    /// <summary>Pick the alphabetically-lowest-titled card from a hand.</summary>
    private static int LowestTitle(IReadOnlyList<int> hand, IReadOnlyList<Card> cards)
        => hand.OrderBy(id => cards[id].Title, StringComparer.Ordinal).First();

    private static IReadOnlyList<int> MeldLowestFromEach(GameState g)
    {
        return g.Players.Select(p => LowestTitle(p.Hand, g.Cards)).ToList();
    }

    // ---------- Setup ----------

    [Fact]
    public void Setup_Deals2CardsToEveryPlayer_AllAge1()
    {
        var (g, _) = NewGame(3);
        foreach (var p in g.Players)
        {
            Assert.Equal(2, p.Hand.Count);
            Assert.All(p.Hand, id => Assert.Equal(1, g.Cards[id].Age));
        }
    }

    [Fact]
    public void Setup_Age1To9_Decks_ReserveAchievementTile()
    {
        var (g, _) = NewGame(2);
        // Age 1 has 15 cards; ages 2–10 have 10. Age 1 loses 1 to the
        // achievement tile and 4 to the opening hands; ages 2–9 lose 1 each
        // (tile); age 10 has no tile reservation.
        Assert.Equal(15 - 1 - 4, g.Decks[1].Count);
        for (int age = 2; age <= 9; age++)
            Assert.Equal(10 - 1, g.Decks[age].Count);
        Assert.Equal(10, g.Decks[10].Count);
    }

    [Fact]
    public void Setup_AllNineAgeAchievements_Available()
    {
        var (g, _) = NewGame(4);
        Assert.Equal(9, g.AvailableAgeAchievements.Count);
        Assert.Equal(Enumerable.Range(1, 9), g.AvailableAgeAchievements.OrderBy(x => x));
    }

    [Fact]
    public void Setup_SpecialAchievements_AllFiveAvailable()
    {
        var (g, _) = NewGame(2);
        Assert.Equal(new[] { "Empire", "Monument", "Universe", "Wonder", "World" },
            g.AvailableSpecialAchievements.OrderBy(s => s).ToArray());
    }

    // ---------- Initial meld ----------

    [Fact]
    public void CompleteInitialMeld_StartsTurnOneWithOneAction()
    {
        var (g, tm) = NewGame(3);
        tm.CompleteInitialMeld(MeldLowestFromEach(g));

        Assert.Equal(GamePhase.Action, g.Phase);
        Assert.Equal(1, g.CurrentTurn);
        Assert.Equal(1, g.ActionsRemaining);
        // One card melded per player.
        foreach (var p in g.Players)
        {
            Assert.Single(p.Hand);
            Assert.Equal(1, p.Stacks.Sum(s => s.Count));
        }
    }

    [Fact]
    public void CompleteInitialMeld_StartingPlayer_IsLowestAlphabetically()
    {
        var (g, tm) = NewGame(3);
        var choices = MeldLowestFromEach(g);
        tm.CompleteInitialMeld(choices);

        string winningTitle = g.Cards[choices[g.ActivePlayer]].Title;
        foreach (var id in choices)
        {
            Assert.True(
                string.Compare(winningTitle, g.Cards[id].Title, StringComparison.Ordinal) <= 0,
                $"Starting-player title {winningTitle} is not ≤ competitor {g.Cards[id].Title}");
        }
    }

    [Fact]
    public void CompleteInitialMeld_RejectsWrongNumberOfChoices()
    {
        var (g, tm) = NewGame(3);
        Assert.Throws<ArgumentException>(() => tm.CompleteInitialMeld(new[] { g.Players[0].Hand[0] }));
    }

    [Fact]
    public void CompleteInitialMeld_RejectsCardNotInHand()
    {
        var (g, tm) = NewGame(2);
        int notHeld = Enumerable.Range(0, g.Cards.Count).First(id => g.Players.All(p => !p.Hand.Contains(id)));
        Assert.Throws<InvalidOperationException>(() => tm.CompleteInitialMeld(new[] { notHeld, g.Players[1].Hand[0] }));
    }

    // ---------- Turn advancement ----------

    [Fact]
    public void Draw_DecrementsActionsAndAdvancesTurn_In2PlayerGame()
    {
        var (g, tm) = NewGame(2);
        tm.CompleteInitialMeld(MeldLowestFromEach(g));
        int firstPlayer = g.ActivePlayer;

        tm.Apply(new DrawAction());                        // turn 1 (1 action) consumed

        Assert.Equal(2, g.CurrentTurn);
        Assert.Equal((firstPlayer + 1) % 2, g.ActivePlayer);
        Assert.Equal(2, g.ActionsRemaining);               // 2-player turn 2 gets 2 actions
    }

    [Fact]
    public void Turn2_In4PlayerGame_Gets1Action_MatchingVb6()
    {
        // Preserves the VB6 quirk: turn 2 in a 4-player game gets only 1
        // action, matching main.frm line 3356.
        var (g, tm) = NewGame(4);
        tm.CompleteInitialMeld(MeldLowestFromEach(g));

        tm.Apply(new DrawAction());                        // end of turn 1
        Assert.Equal(2, g.CurrentTurn);
        Assert.Equal(1, g.ActionsRemaining);

        tm.Apply(new DrawAction());                        // end of turn 2
        Assert.Equal(3, g.CurrentTurn);
        Assert.Equal(2, g.ActionsRemaining);               // back to 2 from turn 3 on
    }

    [Fact]
    public void TwoActions_PerTurn_InSteadyState()
    {
        var (g, tm) = NewGame(2);
        tm.CompleteInitialMeld(MeldLowestFromEach(g));
        tm.Apply(new DrawAction());                        // end of turn 1
        Assert.Equal(2, g.ActionsRemaining);

        tm.Apply(new DrawAction());
        Assert.Equal(1, g.ActionsRemaining);
        Assert.Equal(2, g.CurrentTurn);

        tm.Apply(new DrawAction());                        // ends turn 2
        Assert.Equal(3, g.CurrentTurn);
        Assert.Equal(2, g.ActionsRemaining);
    }

    // ---------- Action execution ----------

    [Fact]
    public void MeldAction_MovesCardToColorStack()
    {
        var (g, tm) = NewGame(2);
        tm.CompleteInitialMeld(MeldLowestFromEach(g));
        var p = g.Active;
        int card = p.Hand[0];
        var color = g.Cards[card].Color;

        tm.Apply(new MeldAction(card));

        Assert.DoesNotContain(card, p.Hand);
        Assert.Equal(card, p.Stack(color).Top);
    }

    [Fact]
    public void MeldAction_RejectsCardNotInHand()
    {
        var (g, tm) = NewGame(2);
        tm.CompleteInitialMeld(MeldLowestFromEach(g));
        int notHeld = Enumerable.Range(0, g.Cards.Count).First(id => g.Active.Hand.All(h => h != id));

        Assert.Throws<InvalidOperationException>(() => tm.Apply(new MeldAction(notHeld)));
    }

    [Fact]
    public void Apply_WhenNoActionsRemaining_Throws()
    {
        var (g, tm) = NewGame(2);
        tm.CompleteInitialMeld(MeldLowestFromEach(g));
        tm.Apply(new DrawAction());
        // Turn has advanced; new active player has 2 actions — consume both.
        tm.Apply(new DrawAction());
        tm.Apply(new DrawAction());
        // Turn advanced again, new player has 2 actions — still valid. Force
        // a run-out by stepping back using reflection? Instead: simulate
        // end by flipping ActionsRemaining to 0 directly to test the guard.
        g.ActionsRemaining = 0;
        Assert.Throws<InvalidOperationException>(() => tm.Apply(new DrawAction()));
    }

    [Fact]
    public void DogmaAction_OnEmptyColorPile_Throws()
    {
        var (g, tm) = NewGame(2);
        tm.CompleteInitialMeld(MeldLowestFromEach(g));

        // Find a color the active player has no pile of, and try to dogma it.
        var empty = Enum.GetValues<CardColor>()
            .First(c => g.Active.Stack(c).IsEmpty);

        Assert.Throws<InvalidOperationException>(() => tm.Apply(new DogmaAction(empty)));
    }

    [Fact]
    public void DogmaAction_OnRegisteredCard_ResolvesAndConsumesAction()
    {
        // Seed a 2-player game where we control what the active player's top
        // Blue card is, so the dogma action triggers Writing's effect.
        var (g, tm) = NewGame(2);
        tm.CompleteInitialMeld(MeldLowestFromEach(g));

        var p = g.Active;
        int writing = g.Cards.Single(c => c.Title == "Writing").Id;

        // Strip Writing out of every deck/hand, then drop it into player's
        // Blue pile as the top card.
        for (int age = 1; age <= 10; age++) g.Decks[age].Remove(writing);
        foreach (var pl in g.Players) pl.Hand.Remove(writing);
        p.Stack(CardColor.Blue).Meld(writing);

        int handBefore = p.Hand.Count;
        int firstPlayer = g.ActivePlayer;

        tm.Apply(new DogmaAction(CardColor.Blue));

        // Writing = "Draw a 2" → one card lands in the acting player's hand.
        Assert.Equal(handBefore + 1, p.Hand.Count);
        Assert.Equal(GamePhase.Action, g.Phase);
        Assert.True(tm.PendingDogma!.IsComplete);
        // Turn 1 was the active player's single action; after consuming it the
        // turn advances to the next player with 2 actions.
        Assert.Equal(2, g.CurrentTurn);
        Assert.Equal((firstPlayer + 1) % 2, g.ActivePlayer);
        Assert.Equal(2, g.ActionsRemaining);
    }

    [Fact]
    public void DogmaAction_UnregisteredCard_ResolvesAsNoOp()
    {
        var (g, tm) = NewGame(2);
        tm.CompleteInitialMeld(MeldLowestFromEach(g));

        // Force the active player's Blue pile to contain a card we know
        // isn't registered yet: Tools will arrive in Phase 4.3, but for now
        // its dogma falls through to PlaceholderHandler.
        var p = g.Active;
        int tools = g.Cards.Single(c => c.Title == "Tools").Id;
        for (int age = 1; age <= 10; age++) g.Decks[age].Remove(tools);
        foreach (var pl in g.Players) pl.Hand.Remove(tools);
        p.Stack(CardColor.Blue).Meld(tools);

        int handBefore = p.Hand.Count;
        int scoreBefore = p.Score(g.Cards);

        tm.Apply(new DogmaAction(CardColor.Blue));

        Assert.Equal(handBefore, p.Hand.Count);
        Assert.Equal(scoreBefore, p.Score(g.Cards));
        Assert.True(tm.PendingDogma!.IsComplete);
    }

    // ---------- Achievement claims ----------

    [Fact]
    public void CanClaim_RequiresScore5xAge_AndMatchingTopCard()
    {
        var (g, _) = NewGame(2);
        var p = g.Players[0];
        // Age-1 tile requires score ≥ 5 and top card age ≥ 1.
        Assert.False(AchievementRules.CanClaim(g, p, 1));

        // Give them score 5 (one age-5 card) and a top card age ≥ 1.
        int age5 = g.Cards.First(c => c.Age == 5).Id;
        int age1 = g.Cards.First(c => c.Age == 1).Id;
        p.ScorePile.Add(age5);
        p.Stack(g.Cards[age1].Color).Meld(age1);

        Assert.True(AchievementRules.CanClaim(g, p, 1));

        // But age-2 tile needs top card age ≥ 2 — still fails.
        Assert.False(AchievementRules.CanClaim(g, p, 2));
    }

    [Fact]
    public void Claim_RemovesTileFromPool_AndCreditsPlayer()
    {
        var (g, _) = NewGame(2);
        var p = g.Players[0];
        int age5 = g.Cards.First(c => c.Age == 5).Id;
        int age1 = g.Cards.First(c => c.Age == 1).Id;
        p.ScorePile.Add(age5);
        p.Stack(g.Cards[age1].Color).Meld(age1);

        bool ok = AchievementRules.Claim(g, p, 1);

        Assert.True(ok);
        Assert.DoesNotContain(1, g.AvailableAgeAchievements);
        Assert.Contains(1, p.AgeAchievements);
        Assert.Equal(1, p.AchievementCount);
    }

    [Fact]
    public void SixAchievements_In2Player_TriggersGameEnd()
    {
        var (g, _) = NewGame(2);
        for (int age = 1; age <= 6; age++) g.Players[0].AgeAchievements.Add(age);
        AchievementRules.CheckAchievementWin(g);

        Assert.Equal(GamePhase.GameOver, g.Phase);
        Assert.Equal(new[] { 0 }, g.Winners);
    }

    [Fact]
    public void FourAchievements_In4Player_TriggersGameEnd()
    {
        var (g, _) = NewGame(4);
        for (int age = 1; age <= 4; age++) g.Players[2].AgeAchievements.Add(age);
        AchievementRules.CheckAchievementWin(g);

        Assert.Equal(GamePhase.GameOver, g.Phase);
        Assert.Equal(new[] { 2 }, g.Winners);
    }
}
