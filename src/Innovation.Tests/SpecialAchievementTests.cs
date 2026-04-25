using System.Text;
using Innovation.Core;
using Xunit;

namespace Innovation.Tests;

public class SpecialAchievementTests
{
    static SpecialAchievementTests()
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
    }

    private static IReadOnlyList<Card> AllCards => CardDataLoader.LoadFromEmbeddedResource();

    /// <summary>
    /// A barebones game state with the full card catalog but no decks and no
    /// opening hands. Adds all five special-achievement tiles to the
    /// available pool (matching <see cref="GameSetup.Create"/>).
    /// </summary>
    private static GameState Bare(int players = 2)
    {
        var g = new GameState(AllCards, players);
        foreach (var name in new[] { "Monument", "Empire", "World", "Wonder", "Universe" })
            g.AvailableSpecialAchievements.Add(name);
        g.Phase = GamePhase.Action;
        return g;
    }

    /// <summary>Drop a card into a player's color stack directly, top-of-pile.</summary>
    private static void MeldDirect(PlayerState p, int cardId, CardColor color)
        => p.Stack(color).Meld(cardId);

    // ---------- World ----------

    [Fact]
    public void World_Triggers_At12Clocks()
    {
        var g = Bare();
        var p = g.Players[0];
        GivePlayerIcons(g, p, Icon.Clock, target: 12);

        // Sanity: the helper really produced 12+ clocks.
        Assert.True(IconCounter.Count(p, Icon.Clock, g.Cards) >= 12);

        SpecialAchievements.CheckAll(g);

        Assert.Contains("World", p.SpecialAchievements);
        Assert.DoesNotContain("World", g.AvailableSpecialAchievements);
    }

    [Fact]
    public void World_DoesNotTrigger_Below12Clocks()
    {
        var g = Bare();
        // Empty boards → 0 clocks.
        SpecialAchievements.CheckAll(g);
        foreach (var p in g.Players)
            Assert.DoesNotContain("World", p.SpecialAchievements);
        Assert.Contains("World", g.AvailableSpecialAchievements);
    }

    // ---------- Empire ----------

    [Fact]
    public void Empire_Triggers_With3OfEveryIcon()
    {
        var g = Bare();
        var p = g.Players[0];

        // Brute force: give the player 3+ of every icon by stacking each
        // color's pile with its highest-contributor card for each icon in turn.
        GivePlayerIcons(g, p, Icon.Leaf,     target: 3);
        GivePlayerIcons(g, p, Icon.Castle,   target: 3);
        GivePlayerIcons(g, p, Icon.Lightbulb,target: 3);
        GivePlayerIcons(g, p, Icon.Crown,    target: 3);
        GivePlayerIcons(g, p, Icon.Factory,  target: 3);
        GivePlayerIcons(g, p, Icon.Clock,    target: 3);

        foreach (var icon in new[]
            { Icon.Leaf, Icon.Castle, Icon.Lightbulb, Icon.Crown, Icon.Factory, Icon.Clock })
            Assert.True(IconCounter.Count(p, icon, g.Cards) >= 3, $"Icon {icon}");

        SpecialAchievements.CheckAll(g);

        Assert.Contains("Empire", p.SpecialAchievements);
    }

    [Fact]
    public void Empire_DoesNotTrigger_When1IconTypeShort()
    {
        var g = Bare();
        var p = g.Players[0];
        // Agriculture on top: 3 leaves but nothing else — missing 5 icon types.
        int agri = g.Cards.Single(c => c.Title == "Agriculture").Id;
        MeldDirect(p, agri, CardColor.Yellow);

        SpecialAchievements.CheckAll(g);

        Assert.DoesNotContain("Empire", p.SpecialAchievements);
    }

    // ---------- Universe ----------

    [Fact]
    public void Universe_Triggers_With5Piles_AllTopAge8Plus()
    {
        var g = Bare();
        var p = g.Players[0];
        // Find one age-8+ card of each color.
        var pickedByColor = Enum.GetValues<CardColor>()
            .Select(color => g.Cards.First(c => c.Color == color && c.Age >= 8))
            .ToList();
        foreach (var c in pickedByColor)
            MeldDirect(p, c.Id, c.Color);

        SpecialAchievements.CheckAll(g);

        Assert.Contains("Universe", p.SpecialAchievements);
    }

    [Fact]
    public void Universe_DoesNotTrigger_WithAge7TopCard()
    {
        var g = Bare();
        var p = g.Players[0];

        // 4 age-8+ cards + 1 age-7 card on the fifth color.
        var colors = Enum.GetValues<CardColor>().ToList();
        for (int i = 0; i < 4; i++)
        {
            var c = g.Cards.First(c => c.Color == colors[i] && c.Age >= 8);
            MeldDirect(p, c.Id, c.Color);
        }
        var age7 = g.Cards.First(c => c.Color == colors[4] && c.Age == 7);
        MeldDirect(p, age7.Id, age7.Color);

        SpecialAchievements.CheckAll(g);

        Assert.DoesNotContain("Universe", p.SpecialAchievements);
    }

    // ---------- Wonder ----------

    [Fact]
    public void Wonder_Triggers_With5Piles_AllSplayedUpOrRight()
    {
        var g = Bare();
        var p = g.Players[0];
        // Meld two cards in each color (need 2+ to splay), then splay.
        foreach (var color in Enum.GetValues<CardColor>())
        {
            var two = g.Cards.Where(c => c.Color == color).Take(2).ToList();
            MeldDirect(p, two[0].Id, color);
            MeldDirect(p, two[1].Id, color);
            p.Stack(color).ApplySplay(Innovation.Core.Splay.Right);
        }

        SpecialAchievements.CheckAll(g);

        Assert.Contains("Wonder", p.SpecialAchievements);
    }

    [Fact]
    public void Wonder_DoesNotTrigger_With_LeftSplay()
    {
        var g = Bare();
        var p = g.Players[0];
        foreach (var color in Enum.GetValues<CardColor>())
        {
            var two = g.Cards.Where(c => c.Color == color).Take(2).ToList();
            MeldDirect(p, two[0].Id, color);
            MeldDirect(p, two[1].Id, color);
        }
        // Splay 4 right, 1 left.
        var colors = Enum.GetValues<CardColor>().ToList();
        for (int i = 0; i < 4; i++) p.Stack(colors[i]).ApplySplay(Innovation.Core.Splay.Right);
        p.Stack(colors[4]).ApplySplay(Innovation.Core.Splay.Left);

        SpecialAchievements.CheckAll(g);

        Assert.DoesNotContain("Wonder", p.SpecialAchievements);
    }

    // ---------- Player iteration (tiebreaker) ----------

    [Fact]
    public void WhenTwoPlayersEligible_ActivePlayerClaimsFirst()
    {
        var g = Bare(players: 3);
        g.ActivePlayer = 2;
        // Players 0 and 2 both eligible for World — active player 2 should win.
        GivePlayerIcons(g, g.Players[0], Icon.Clock, target: 12);
        GivePlayerIcons(g, g.Players[2], Icon.Clock, target: 12);

        SpecialAchievements.CheckAll(g);

        Assert.Contains("World", g.Players[2].SpecialAchievements);
        Assert.DoesNotContain("World", g.Players[0].SpecialAchievements);
    }

    // ---------- Integration: Mechanics hooks ----------

    [Fact]
    public void Meld_AutoTriggers_WorldAchievement()
    {
        // Arrange a player who's one meld away from 12 clocks, then use
        // Mechanics.Meld to push them over. World should land without any
        // manual SpecialAchievements.CheckAll call.
        var g = Bare();
        var p = g.Players[0];
        GivePlayerIcons(g, p, Icon.Clock, target: 11);

        // Find a clocky card still in the catalog (not already on a board).
        var clockCard = g.Cards
            .Where(c => !IsCardOnAnyBoard(g, c.Id))
            .OrderByDescending(c => CountAllClocks(c))
            .First(c => CountAllClocks(c) >= 1);
        p.Hand.Add(clockCard.Id);
        int clocksBefore = IconCounter.Count(p, Icon.Clock, g.Cards);

        Mechanics.Meld(g, p, clockCard.Id);

        int clocksAfter = IconCounter.Count(p, Icon.Clock, g.Cards);
        // Only assert when the meld actually pushed count ≥12 (guaranteed by
        // setup — the card has ≥1 clock and starting count was 11).
        Assert.True(clocksAfter >= 12, $"Precondition: {clocksAfter} clocks after meld");
        Assert.Contains("World", p.SpecialAchievements);
    }

    [Fact]
    public void Splay_AutoTriggers_WonderAchievement()
    {
        // Every color has 2 cards, splayed Right except one that's unsplayed.
        // Mechanics.Splay on the last color should claim Wonder automatically.
        var g = Bare();
        var p = g.Players[0];
        foreach (var color in Enum.GetValues<CardColor>())
        {
            var two = g.Cards.Where(c => c.Color == color).Take(2).ToList();
            MeldDirect(p, two[0].Id, color);
            MeldDirect(p, two[1].Id, color);
        }
        var colors = Enum.GetValues<CardColor>().ToList();
        for (int i = 0; i < 4; i++) p.Stack(colors[i]).ApplySplay(Innovation.Core.Splay.Right);
        // 5th color unsplayed; splay it via Mechanics to trigger the hook.
        Assert.DoesNotContain("Wonder", p.SpecialAchievements);

        Mechanics.Splay(g, p, colors[4], Innovation.Core.Splay.Right);

        Assert.Contains("Wonder", p.SpecialAchievements);
    }

    private static int CountAllClocks(Card c) =>
        (c.Top == Icon.Clock ? 1 : 0) + CoveredIconCount(c, Icon.Clock);

    // ---------- Board builder ----------

    /// <summary>
    /// Give the player at least <paramref name="target"/> visible instances
    /// of <paramref name="icon"/> by stacking cards onto an under-used color
    /// pile and splaying up. Greedy across the whole catalog; throws if the
    /// target is unreachable (shouldn't happen with real card data for
    /// target ≤ 12).
    /// </summary>
    private static void GivePlayerIcons(GameState g, PlayerState p, Icon icon, int target)
    {
        if (IconCounter.Count(p, icon, g.Cards) >= target) return;

        // Rank every card by how many copies of the target icon it contributes
        // when sitting covered under an Up splay (L+M+R) — that's the pile
        // bulk contribution. Top card adds Top slot too, but we optimise the
        // covered cards and just let the last-melded card be whatever lands
        // on top.
        var available = g.Cards
            .Where(c => !IsCardOnAnyBoard(g, c.Id))
            .OrderByDescending(c => CoveredIconCount(c, icon))
            .ToList();

        // Try each color in turn; each color's pile can be up to 5 cards
        // (but we're not really restricted, so just stack as many as needed).
        foreach (var color in Enum.GetValues<CardColor>())
        {
            var pile = p.Stack(color);
            foreach (var c in available.Where(c => c.Color == color).ToList())
            {
                pile.Meld(c.Id);
                pile.ApplySplay(Innovation.Core.Splay.Up);
                available.Remove(c);
                if (IconCounter.Count(p, icon, g.Cards) >= target) return;
            }
        }

        throw new InvalidOperationException(
            $"Could not build board with {target} {icon}s (got {IconCounter.Count(p, icon, g.Cards)}).");
    }

    private static int CoveredIconCount(Card c, Icon icon)
    {
        int n = 0;
        if (c.Left   == icon) n++;
        if (c.Middle == icon) n++;
        if (c.Right  == icon) n++;
        return n;
    }

    private static bool IsCardOnAnyBoard(GameState g, int cardId)
    {
        foreach (var pl in g.Players)
            foreach (var stack in pl.Stacks)
                if (stack.Cards.Contains(cardId)) return true;
        return false;
    }
}
