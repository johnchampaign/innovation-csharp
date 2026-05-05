using System.Text;
using Innovation.Core;
using Innovation.Core.Handlers;
using Xunit;

namespace Innovation.Tests;

public class PirateCodeScoreIfDemandTests
{
    static PirateCodeScoreIfDemandTests()
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
    }

    private static IReadOnlyList<Card> AllCards => CardDataLoader.LoadFromEmbeddedResource();

    private static GameState FreshDecks(int players = 2)
    {
        var g = new GameState(AllCards, players);
        foreach (var c in AllCards) g.Decks[c.Age].Add(c.Id);
        g.Phase = GamePhase.Dogma;
        return g;
    }

    private static int IdOf(string title) => AllCards.Single(c => c.Title == title).Id;

    [Fact]
    public void Ties_AtLowestAge_PromptThePlayer()
    {
        var g = FreshDecks();
        var me = g.Players[0];

        // Both Pirate Code (Red, age 5, Crown) and Societies (Purple, age 5,
        // Crown) on top. Lowest tied at age 5 across two colors.
        int pirate = IdOf("The Pirate Code");
        int societies = IdOf("Societies");
        me.Hand.Add(pirate); Mechanics.Meld(g, me, pirate);
        me.Hand.Add(societies); Mechanics.Meld(g, me, societies);

        var ctx = new DogmaContext(0, 0, Icon.Crown) { DemandSuccessful = true };
        var h = new PirateCodeScoreIfDemandHandler();

        // First call posts the tie-break prompt.
        Assert.False(h.Execute(g, me, ctx));
        var req = (SelectColorRequest)ctx.PendingChoice!;
        Assert.Contains(CardColor.Red, req.EligibleColors);
        Assert.Contains(CardColor.Purple, req.EligibleColors);

        // Player chooses Societies.
        req.ChosenColor = CardColor.Purple;
        ctx.Paused = false;

        Assert.True(h.Execute(g, me, ctx));

        // Societies is in the score pile; Pirate Code stays on the board.
        Assert.Contains(societies, me.ScorePile);
        Assert.DoesNotContain(pirate, me.ScorePile);
        Assert.Equal(pirate, me.Stack(CardColor.Red).Top);
    }

    [Fact]
    public void NoTie_LowestUnique_AutoScores()
    {
        var g = FreshDecks();
        var me = g.Players[0];

        // Pirate Code (Red, age 5) is the only crown-iconed top card.
        int pirate = IdOf("The Pirate Code");
        me.Hand.Add(pirate); Mechanics.Meld(g, me, pirate);

        var ctx = new DogmaContext(0, 0, Icon.Crown) { DemandSuccessful = true };
        var h = new PirateCodeScoreIfDemandHandler();
        Assert.True(h.Execute(g, me, ctx));
        Assert.Contains(pirate, me.ScorePile);
    }

    [Fact]
    public void DemandUnsuccessful_NoOp()
    {
        var g = FreshDecks();
        var me = g.Players[0];

        int pirate = IdOf("The Pirate Code");
        me.Hand.Add(pirate); Mechanics.Meld(g, me, pirate);

        var ctx = new DogmaContext(0, 0, Icon.Crown) { DemandSuccessful = false };
        var h = new PirateCodeScoreIfDemandHandler();
        Assert.False(h.Execute(g, me, ctx));
        Assert.Empty(me.ScorePile);
    }
}
