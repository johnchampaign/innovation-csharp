using System.Text;
using Innovation.Core;
using Xunit;

namespace Innovation.Tests;

public class CardDataLoaderTests
{
    static CardDataLoaderTests()
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
    }

    private static IReadOnlyList<Card> Cards => CardDataLoader.LoadFromEmbeddedResource();

    [Fact]
    public void Loads105Cards()
    {
        Assert.Equal(105, Cards.Count);
    }

    [Fact]
    public void Card0_IsAgriculture()
    {
        // Internal IDs are 0-indexed to match the VB6 representation; the TSV
        // itself uses 1-based row IDs and Agriculture is row 1 / id 0.
        var c = Cards.Single(c => c.Id == 0);
        Assert.Equal("Agriculture", c.Title);
        Assert.Equal(1, c.Age);
        Assert.Equal(CardColor.Yellow, c.Color);
        Assert.Equal(IconSlot.Top, c.HexagonSlot);
        Assert.Equal(Icon.Leaf, c.Left);
        Assert.Equal(Icon.Leaf, c.Middle);
        Assert.Equal(Icon.Leaf, c.Right);
        Assert.Equal(Icon.Leaf, c.DogmaIcon);
        Assert.Single(c.DogmaEffects);
    }

    [Fact]
    public void Card45_Astronomy_HasTwoEffects_WithMultilineText()
    {
        var c = Cards.Single(c => c.Id == 45);
        Assert.Equal("Astronomy", c.Title);
        Assert.Equal(2, c.DogmaEffects.Count);
        // Second effect spans two source lines in the TSV.
        Assert.Contains("Universe", c.DogmaEffects[1]);
        Assert.Contains("achievement", c.DogmaEffects[1]);
    }

    [Fact]
    public void Card68_Evolution_HasMultilineEffect()
    {
        var c = Cards.Single(c => c.Id == 68);
        Assert.Equal("Evolution", c.Title);
        Assert.Single(c.DogmaEffects);
        Assert.Contains("draw and score an 8", c.DogmaEffects[0]);
        Assert.Contains("one higher", c.DogmaEffects[0]);
    }

    [Fact]
    public void Card42_Perspective_TolerantOfLIghtbulbTypo()
    {
        var c = Cards.Single(c => c.Id == 42);
        Assert.Equal("Perspective", c.Title);
        // The TSV has "[LIghtbulb]" (capital I) in the dogma text; the card
        // itself still parses cleanly, and the icon fields are unaffected.
        Assert.Equal(Icon.Lightbulb, c.DogmaIcon);
    }

    [Fact]
    public void AllCards_HaveValidAgeRange()
    {
        Assert.All(Cards, c => Assert.InRange(c.Age, 1, 10));
    }

    [Fact]
    public void AllCards_HaveExactlyThreeNonHexagonIcons()
    {
        foreach (var c in Cards)
        {
            var slots = new[] { c.Top, c.Left, c.Middle, c.Right };
            int noneCount = slots.Count(i => i == Icon.None);
            Assert.Equal(1, noneCount);
        }
    }

    [Fact]
    public void AllCards_HaveAtLeastOneDogmaEffect()
    {
        Assert.All(Cards, c => Assert.NotEmpty(c.DogmaEffects));
    }

    [Fact]
    public void Card30_Machinery_HasExpectedStructure()
    {
        var c = Cards.Single(c => c.Id == 30);
        Assert.Equal("Machinery", c.Title);
        Assert.Equal(CardColor.Yellow, c.Color);
        Assert.Equal(2, c.DogmaEffects.Count);
        Assert.Contains("splay your red cards left", c.DogmaEffects[1]);
    }

    [Fact]
    public void Card104_IsTheInternet()
    {
        var c = Cards.Single(c => c.Id == 104);
        Assert.Equal("The Internet", c.Title);
        Assert.Equal(10, c.Age);
        Assert.Equal(3, c.DogmaEffects.Count);
    }
}
