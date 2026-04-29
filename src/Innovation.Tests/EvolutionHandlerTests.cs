using System.Text;
using Innovation.Core;
using Innovation.Core.Handlers;
using Xunit;

namespace Innovation.Tests;

public class EvolutionHandlerTests
{
    static EvolutionHandlerTests()
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

    [Fact]
    public void Evolution_BranchB_HighestIs10_DrawsAge11_EndsGame()
    {
        var g = FreshDecks();
        var me = g.Players[0];
        var ten = AllCards.First(c => c.Age == 10).Id;
        me.ScorePile.Add(ten);
        g.Decks[10].Remove(ten);

        var ctx = new DogmaContext(0, 0, Icon.Lightbulb);
        var h = new EvolutionHandler();

        Assert.False(h.Execute(g, me, ctx));
        var yn = (YesNoChoiceRequest)ctx.PendingChoice!;
        yn.ChosenYes = false;   // branch B: draw one higher than highest
        ctx.Paused = false;

        h.Execute(g, me, ctx);

        Assert.True(g.IsGameOver, "drawing past age 10 should end the game");
        Assert.Contains(0, g.Winners);
    }

    [Fact]
    public void Evolution_BranchB_HighestIs5_DrawsAge6()
    {
        var g = FreshDecks();
        var me = g.Players[0];
        var five = AllCards.First(c => c.Age == 5).Id;
        me.ScorePile.Add(five);
        g.Decks[5].Remove(five);

        int age6Before = g.Decks[6].Count;

        var ctx = new DogmaContext(0, 0, Icon.Lightbulb);
        var h = new EvolutionHandler();
        Assert.False(h.Execute(g, me, ctx));
        var yn = (YesNoChoiceRequest)ctx.PendingChoice!;
        yn.ChosenYes = false;
        ctx.Paused = false;

        h.Execute(g, me, ctx);

        Assert.False(g.IsGameOver);
        Assert.Equal(age6Before - 1, g.Decks[6].Count);
        Assert.Single(me.Hand);
        Assert.Equal(6, g.Cards[me.Hand[0]].Age);
    }
}
