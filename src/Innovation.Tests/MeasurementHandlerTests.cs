using System.Text;
using Innovation.Core;
using Innovation.Core.Handlers;
using Xunit;

namespace Innovation.Tests;

public class MeasurementHandlerTests
{
    static MeasurementHandlerTests()
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

    /// Pile with N cards on the given color, splayed right.
    private static void StackAndSplayRight(GameState g, PlayerState p, CardColor color, int n)
    {
        var ids = AllCards.Where(c => c.Color == color).Take(n).Select(c => c.Id).ToList();
        foreach (var id in ids)
        {
            g.Decks[g.Cards[id].Age].Remove(id);
            p.Hand.Add(id);
            Mechanics.Meld(g, p, id);
        }
        if (n >= 2) p.Stack(color).ApplySplay(Splay.Right);
    }

    [Fact]
    public void AlreadyRightSplayed_PileIsEligibleAndDrives_Draw()
    {
        var g = FreshDecks();
        var me = g.Players[0];

        // Set up a Yellow pile of 4 cards, splayed right.
        StackAndSplayRight(g, me, CardColor.Yellow, 4);
        Assert.Equal(Splay.Right, me.Stack(CardColor.Yellow).Splay);

        // Hand needs at least one card to satisfy the Phase 1 return.
        var ret = AllCards.First(c => c.Age == 1 && c.Color == CardColor.Blue).Id;
        g.Decks[1].Remove(ret);
        me.Hand.Add(ret);

        var ctx = new DogmaContext(0, 0, Icon.Lightbulb);
        var h = new MeasurementHandler();

        // Phase 1: return the blue.
        Assert.False(h.Execute(g, me, ctx));
        var hr = (SelectHandCardRequest)ctx.PendingChoice!;
        hr.ChosenCardId = ret;
        ctx.Paused = false;

        // Phase 2 prompt — Yellow MUST be in eligible despite being splayed right.
        h.Execute(g, me, ctx);
        var cr = (SelectColorRequest)ctx.PendingChoice!;
        Assert.Contains(CardColor.Yellow, cr.EligibleColors);

        int age4Before = g.Decks[4].Count;
        cr.ChosenColor = CardColor.Yellow;
        ctx.Paused = false;

        Assert.True(h.Execute(g, me, ctx));

        // Splay didn't change (already right) but the draw fired anyway:
        // pile size 4 → drew an age-4 card.
        Assert.Equal(Splay.Right, me.Stack(CardColor.Yellow).Splay);
        Assert.Equal(age4Before - 1, g.Decks[4].Count);
        Assert.Single(me.Hand);
        Assert.Equal(4, g.Cards[me.Hand[0]].Age);
    }

    [Fact]
    public void NotYetSplayed_DoesSplayAndDraws()
    {
        var g = FreshDecks();
        var me = g.Players[0];

        // Yellow pile of 3, NOT splayed.
        var ids = AllCards.Where(c => c.Color == CardColor.Yellow).Take(3).Select(c => c.Id).ToList();
        foreach (var id in ids)
        {
            g.Decks[g.Cards[id].Age].Remove(id);
            me.Hand.Add(id);
            Mechanics.Meld(g, me, id);
        }
        Assert.Equal(Splay.None, me.Stack(CardColor.Yellow).Splay);

        // Return card.
        var ret = AllCards.First(c => c.Age == 1 && c.Color == CardColor.Blue).Id;
        g.Decks[1].Remove(ret);
        me.Hand.Add(ret);

        var ctx = new DogmaContext(0, 0, Icon.Lightbulb);
        var h = new MeasurementHandler();
        Assert.False(h.Execute(g, me, ctx));
        ((SelectHandCardRequest)ctx.PendingChoice!).ChosenCardId = ret;
        ctx.Paused = false;

        h.Execute(g, me, ctx);
        var cr = (SelectColorRequest)ctx.PendingChoice!;
        cr.ChosenColor = CardColor.Yellow;
        ctx.Paused = false;

        Assert.True(h.Execute(g, me, ctx));
        Assert.Equal(Splay.Right, me.Stack(CardColor.Yellow).Splay);
        Assert.Single(me.Hand);
        Assert.Equal(3, g.Cards[me.Hand[0]].Age);
    }
}
