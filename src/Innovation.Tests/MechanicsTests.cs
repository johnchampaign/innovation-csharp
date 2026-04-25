using System.Text;
using Innovation.Core;
using Xunit;

namespace Innovation.Tests;

public class MechanicsTests
{
    static MechanicsTests()
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
    }

    private static IReadOnlyList<Card> AllCards => CardDataLoader.LoadFromEmbeddedResource();

    /// <summary>
    /// Build a state for tests. Decks are seeded with card IDs by their age
    /// (using the actual card catalog, not stubs) so we can exercise Draw
    /// realistically. 2 players by default.
    /// </summary>
    private static GameState NewGameState(int players = 2)
    {
        var cards = AllCards;
        var g = new GameState(cards, players);
        foreach (var c in cards) g.Decks[c.Age].Add(c.Id);
        g.Phase = GamePhase.Action;
        return g;
    }

    private static Card FindCard(IReadOnlyList<Card> cards, string title)
        => cards.Single(c => c.Title == title);

    // ---------- Draw ----------

    [Fact]
    public void Draw_WhenDeckEmpty_WalksUpToHigherAge()
    {
        var g = NewGameState();
        g.Decks[1].Clear();                  // age-1 deck is empty
        var expected = g.Decks[2][0];        // first card in age-2 should be drawn
        int drawn = Mechanics.Draw(g, g.Players[0]);

        Assert.Equal(expected, drawn);
        Assert.Contains(drawn, g.Players[0].Hand);
        Assert.Equal(2, g.Cards[drawn].Age);
    }

    [Fact]
    public void Draw_UsesHighestTopCardAgeAsFloor()
    {
        var g = NewGameState();
        // Put a Factory-age card (age 4) on player 0's board so their floor is 4.
        var aged4 = FindCard(g.Cards, "Colonialism"); // Age 4, Red
        g.Players[0].Stack(CardColor.Red).Meld(aged4.Id);
        g.Decks[aged4.Age].Remove(aged4.Id);

        int drawn = Mechanics.Draw(g, g.Players[0]);
        Assert.Equal(4, g.Cards[drawn].Age);
    }

    [Fact]
    public void Draw_WhenAge10PileEmpty_EndsGame_WithWinnerByScore()
    {
        var g = NewGameState();
        // Give player 1 the higher score pile.
        var card3 = g.Cards.First(c => c.Age == 3);
        var card5 = g.Cards.First(c => c.Age == 5);
        g.Players[0].ScorePile.Add(card3.Id);           // score 3
        g.Players[1].ScorePile.Add(card3.Id);
        g.Players[1].ScorePile.Add(card5.Id);           // score 8
        // Empty all decks so the draw definitely overflows.
        for (int i = 0; i <= 10; i++) g.Decks[i].Clear();

        int drawn = Mechanics.Draw(g, g.Players[0]);

        Assert.Equal(-1, drawn);
        Assert.Equal(GamePhase.GameOver, g.Phase);
        Assert.Single(g.Winners);
        Assert.Equal(1, g.Winners[0]);
        // Player 0 must NOT have received card 0 (Agriculture) — that was the bug.
        Assert.Empty(g.Players[0].Hand);
    }

    [Fact]
    public void Draw_WithTiedScores_UsesAchievementsAsTiebreaker()
    {
        var g = NewGameState();
        var card5 = g.Cards.First(c => c.Age == 5);
        g.Players[0].ScorePile.Add(card5.Id);
        g.Players[1].ScorePile.Add(card5.Id);
        g.Players[1].AgeAchievements.Add(1);  // tiebreak in favor of player 1
        for (int i = 0; i <= 10; i++) g.Decks[i].Clear();

        Mechanics.Draw(g, g.Players[0]);

        Assert.Single(g.Winners);
        Assert.Equal(1, g.Winners[0]);
    }

    [Fact]
    public void Draw_WithFullyTiedPlayers_YieldsMultipleWinners()
    {
        var g = NewGameState();
        for (int i = 0; i <= 10; i++) g.Decks[i].Clear();

        Mechanics.Draw(g, g.Players[0]);

        Assert.Equal(2, g.Winners.Count);
    }

    // ---------- Meld / Tuck / Score ----------

    [Fact]
    public void Meld_PlacesCardOnTopOfColorStack()
    {
        var g = NewGameState();
        var a = FindCard(g.Cards, "Agriculture");   // Yellow
        var b = FindCard(g.Cards, "Domestication"); // Yellow
        g.Players[0].Hand.AddRange(new[] { a.Id, b.Id });

        Mechanics.Meld(g, g.Players[0], a.Id);
        Mechanics.Meld(g, g.Players[0], b.Id);

        var yellow = g.Players[0].Stack(CardColor.Yellow);
        Assert.Equal(b.Id, yellow.Top);                          // b melded last → on top
        Assert.Equal(new[] { b.Id, a.Id }, yellow.Cards);
        Assert.Empty(g.Players[0].Hand);
    }

    [Fact]
    public void Tuck_PlacesCardOnBottomOfColorStack()
    {
        var g = NewGameState();
        var a = FindCard(g.Cards, "Agriculture");
        var b = FindCard(g.Cards, "Domestication");
        g.Players[0].Hand.AddRange(new[] { a.Id, b.Id });

        Mechanics.Meld(g, g.Players[0], a.Id);
        Mechanics.Tuck(g, g.Players[0], b.Id);

        var yellow = g.Players[0].Stack(CardColor.Yellow);
        Assert.Equal(a.Id, yellow.Top);                          // a still on top
        Assert.Equal(new[] { a.Id, b.Id }, yellow.Cards);
    }

    [Fact]
    public void Score_MovesCardFromHandToScorePile_AndUpdatesScore()
    {
        var g = NewGameState();
        var c = g.Cards.First(c => c.Age == 3);
        g.Players[0].Hand.Add(c.Id);

        Mechanics.Score(g, g.Players[0], c.Id);

        Assert.Empty(g.Players[0].Hand);
        Assert.Contains(c.Id, g.Players[0].ScorePile);
        Assert.Equal(3, g.Players[0].Score(g.Cards));
    }

    // ---------- Splay ----------

    [Fact]
    public void Splay_OnSingleCardPile_Fails()
    {
        var g = NewGameState();
        var a = FindCard(g.Cards, "Agriculture");
        g.Players[0].Hand.Add(a.Id);
        Mechanics.Meld(g, g.Players[0], a.Id);

        bool ok = Mechanics.Splay(g, g.Players[0], CardColor.Yellow, Innovation.Core.Splay.Left);

        Assert.False(ok);
        Assert.Equal(Innovation.Core.Splay.None, g.Players[0].Stack(CardColor.Yellow).Splay);
    }

    [Fact]
    public void Splay_SameDirection_IsNoOp_AndReturnsFalse()
    {
        var g = NewGameState();
        var a = FindCard(g.Cards, "Agriculture");
        var b = FindCard(g.Cards, "Domestication");
        g.Players[0].Hand.AddRange(new[] { a.Id, b.Id });
        Mechanics.Meld(g, g.Players[0], a.Id);
        Mechanics.Meld(g, g.Players[0], b.Id);

        Assert.True(Mechanics.Splay(g, g.Players[0], CardColor.Yellow, Innovation.Core.Splay.Left));
        Assert.False(Mechanics.Splay(g, g.Players[0], CardColor.Yellow, Innovation.Core.Splay.Left));
    }

    [Fact]
    public void Splay_ResetsWhenStackShrinksToOneCard()
    {
        var g = NewGameState();
        var a = FindCard(g.Cards, "Agriculture");
        var b = FindCard(g.Cards, "Domestication");
        g.Players[0].Hand.AddRange(new[] { a.Id, b.Id });
        Mechanics.Meld(g, g.Players[0], a.Id);
        Mechanics.Meld(g, g.Players[0], b.Id);
        Mechanics.Splay(g, g.Players[0], CardColor.Yellow, Innovation.Core.Splay.Right);

        g.Players[0].Stack(CardColor.Yellow).PopTop();      // pile now has 1

        Assert.Equal(Innovation.Core.Splay.None, g.Players[0].Stack(CardColor.Yellow).Splay);
    }

    // ---------- Icon counts ----------

    [Fact]
    public void IconCount_TopCardOnly_WhenUnsplayed()
    {
        var g = NewGameState();
        // Agriculture: slots are Top(x) Left(Leaf) Middle(Leaf) Right(Leaf) → 3 leaves.
        var a = FindCard(g.Cards, "Agriculture");
        g.Players[0].Hand.Add(a.Id);
        Mechanics.Meld(g, g.Players[0], a.Id);

        Assert.Equal(3, IconCounter.Count(g.Players[0], Icon.Leaf, g.Cards));
    }

    [Fact]
    public void IconCount_LeftSplay_RevealsRightSlotOfCoveredCards()
    {
        var g = NewGameState();
        // Two yellow cards; we want the covered card's Right slot icon to count.
        // Agriculture: T=x L=Leaf M=Leaf R=Leaf (3 leaves on top alone already)
        // Domestication: T=Castle L=Crown M=x R=Castle
        // Meld Agriculture first (becomes bottom), then Domestication on top.
        var bottom = FindCard(g.Cards, "Agriculture");
        var top = FindCard(g.Cards, "Domestication");
        g.Players[0].Hand.AddRange(new[] { bottom.Id, top.Id });
        Mechanics.Meld(g, g.Players[0], bottom.Id);
        Mechanics.Meld(g, g.Players[0], top.Id);

        // Before splay: only top card icons count.
        //   Domestication top slots: Castle, Crown, x, Castle → 2 Castles, 1 Crown, 0 Leaves.
        Assert.Equal(2, IconCounter.Count(g.Players[0], Icon.Castle, g.Cards));
        Assert.Equal(0, IconCounter.Count(g.Players[0], Icon.Leaf, g.Cards));

        // Splay left → covered Agriculture reveals its Right slot (Leaf).
        Assert.True(Mechanics.Splay(g, g.Players[0], CardColor.Yellow, Innovation.Core.Splay.Left));
        Assert.Equal(1, IconCounter.Count(g.Players[0], Icon.Leaf, g.Cards));
        Assert.Equal(2, IconCounter.Count(g.Players[0], Icon.Castle, g.Cards));
    }

    [Fact]
    public void IconCount_UpSplay_RevealsLeftMiddleRightOfCoveredCards()
    {
        var g = NewGameState();
        // Agriculture covered: Left=Leaf, Middle=Leaf, Right=Leaf → 3 leaves revealed.
        var bottom = FindCard(g.Cards, "Agriculture");
        var top = FindCard(g.Cards, "Domestication");
        g.Players[0].Hand.AddRange(new[] { bottom.Id, top.Id });
        Mechanics.Meld(g, g.Players[0], bottom.Id);
        Mechanics.Meld(g, g.Players[0], top.Id);

        Assert.True(Mechanics.Splay(g, g.Players[0], CardColor.Yellow, Innovation.Core.Splay.Up));
        // Top card icons: 2 Castle, 1 Crown. Covered reveals 3 Leaf.
        Assert.Equal(3, IconCounter.Count(g.Players[0], Icon.Leaf, g.Cards));
        Assert.Equal(2, IconCounter.Count(g.Players[0], Icon.Castle, g.Cards));
        Assert.Equal(1, IconCounter.Count(g.Players[0], Icon.Crown, g.Cards));
    }
}
