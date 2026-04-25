using System.Text;
using Innovation.Core;
using Xunit;

namespace Innovation.Tests;

public class GameStateCodecTests
{
    static GameStateCodecTests()
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
    }

    private static IReadOnlyList<Card> AllCards => CardDataLoader.LoadFromEmbeddedResource();

    [Fact]
    public void RoundTrip_FreshGame_PreservesState()
    {
        var g = GameSetup.Create(AllCards, 3, new Random(42));
        var code = GameStateCodec.Encode(g);
        var g2 = GameStateCodec.Decode(code, AllCards);

        Assert.Equal(GameStateCodec.Encode(g), GameStateCodec.Encode(g2));
    }

    [Fact]
    public void RoundTrip_MidGame_PreservesState()
    {
        var g = GameSetup.Create(AllCards, 2, new Random(7));
        var tm = new TurnManager(g);
        tm.CompleteInitialMeld(new[] { g.Players[0].Hand[0], g.Players[1].Hand[0] });
        tm.Apply(new DrawAction());

        var code = GameStateCodec.Encode(g);
        var g2 = GameStateCodec.Decode(code, AllCards);
        Assert.Equal(GameStateCodec.Encode(g), GameStateCodec.Encode(g2));
    }

    [Fact]
    public void Encode_IsCompact()
    {
        var g = GameSetup.Create(AllCards, 4, new Random(1));
        var code = GameStateCodec.Encode(g);
        // Well under 1KB for a fresh 4p game.
        Assert.True(code.Length < 700, $"code too large: {code.Length}");
    }
}
