using System.Text;
using Innovation.Core;
using Innovation.Core.Handlers;
using Xunit;

namespace Innovation.Tests;

/// <summary>
/// Regression tests for the Machinery effect-2 handler's "did I progress?"
/// reporting. The handler returns true iff at least one of (score a castle,
/// splay red) actually happened. A sharer who declined the splay and had
/// no castle to score must report no-op so the activator's share-bonus
/// draw doesn't fire spuriously.
/// </summary>
public class MachineryShareBonusTests
{
    static MachineryShareBonusTests()
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
    }

    private static IReadOnlyList<Card> AllCards => CardDataLoader.LoadFromEmbeddedResource();

    private static GameState Fresh(int players = 2)
    {
        var g = new GameState(AllCards, players);
        foreach (var c in AllCards) g.Decks[c.Age].Add(c.Id);
        g.Phase = GamePhase.Dogma;
        return g;
    }

    [Fact]
    public void NoCastleNoSplay_ReturnsFalse()
    {
        // No castle in hand; no red pile to splay either. Handler should
        // return false — sharer did nothing.
        var g = Fresh();
        var me = g.Players[0];
        // Hand: a non-castle card (Library doesn't exist here; pick any
        // age-1 without Castle). Most age-1s have Castle though; use an
        // age-1 with no castle icon if any. Fallback: leave hand empty.
        // Easier: leave hand empty entirely; handler skips score step.

        var ctx = new DogmaContext(cardId: 0, activatingPlayerIndex: 1, featuredIcon: Icon.Leaf);
        var h = new MachineryScoreCastleSplayHandler();
        bool progressed = h.Execute(g, me, ctx);

        Assert.False(progressed);
        Assert.False(ctx.Paused);
    }

    [Fact]
    public void NoCastleSplayDeclined_ReturnsFalse()
    {
        var g = Fresh();
        var me = g.Players[0];

        // Empty hand → score step skipped. Splay possible: stuff 2 red
        // cards onto board so splay-offer fires.
        var reds = AllCards.Where(c => c.Color == CardColor.Red).Take(2).Select(c => c.Id).ToList();
        foreach (var id in reds)
        {
            g.Decks[g.Cards[id].Age].Remove(id);
            me.Stack(CardColor.Red).Meld(id);
        }

        var ctx = new DogmaContext(cardId: 0, activatingPlayerIndex: 1, featuredIcon: Icon.Leaf);
        var h = new MachineryScoreCastleSplayHandler();

        // Phase A: empty hand → handler offers splay yes/no, pauses.
        bool firstProgressed = h.Execute(g, me, ctx);
        // While paused, the engine ignores the bool, but document the
        // expected value anyway.
        Assert.False(firstProgressed);
        Assert.True(ctx.Paused);
        var yn = (YesNoChoiceRequest)ctx.PendingChoice!;
        yn.ChosenYes = false;   // sharer declines splay
        ctx.Paused = false;

        bool progressed = h.Execute(g, me, ctx);
        Assert.False(progressed);   // declined splay + no score = no-op
    }

    [Fact]
    public void NoCastleSplayAccepted_ReturnsTrue()
    {
        var g = Fresh();
        var me = g.Players[0];

        var reds = AllCards.Where(c => c.Color == CardColor.Red).Take(2).Select(c => c.Id).ToList();
        foreach (var id in reds)
        {
            g.Decks[g.Cards[id].Age].Remove(id);
            me.Stack(CardColor.Red).Meld(id);
        }

        var ctx = new DogmaContext(cardId: 0, activatingPlayerIndex: 1, featuredIcon: Icon.Leaf);
        var h = new MachineryScoreCastleSplayHandler();
        h.Execute(g, me, ctx);
        ((YesNoChoiceRequest)ctx.PendingChoice!).ChosenYes = true;
        ctx.Paused = false;

        Assert.True(h.Execute(g, me, ctx));
        Assert.Equal(Splay.Left, me.Stack(CardColor.Red).Splay);
    }
}
