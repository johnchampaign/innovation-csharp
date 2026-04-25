using System.Text;
using Innovation.Core;
using Innovation.Core.Players;
using Xunit;

namespace Innovation.Tests;

/// <summary>
/// Tests for <see cref="GreedyController"/>. Covers: (a) a surgical
/// scenario where the locally-optimal choice is unambiguous, (b) self-
/// play sanity (greedy completes a full game), and (c) a statistical
/// head-to-head against <see cref="RandomController"/>. The last test
/// is the closest thing to a "real" AI regression guard — if greedy
/// ever stops beating random over many seeded games, the heuristic or
/// the search has regressed.
/// </summary>
public class GreedyControllerTests
{
    static GreedyControllerTests()
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
    }

    private static IReadOnlyList<Card> AllCards => CardDataLoader.LoadFromEmbeddedResource();

    [Fact]
    public void ChooseAction_PicksAchieveWhenObviouslyBest()
    {
        // Set up p0 so Achieve(1) is in the legal set and the +10_000
        // achievement bonus dwarfs every other option.
        var g = new GameState(AllCards, 2);
        foreach (var c in AllCards) g.Decks[c.Age].Add(c.Id);
        g.Phase = GamePhase.Action;
        g.ActionsRemaining = 1;

        // 5 age-1 in score + an age-1 top card = eligible for age-1 tile.
        var age1s = g.Cards.Where(c => c.Age == 1).Take(6).ToList();
        g.Players[0].ScorePile.AddRange(age1s.Take(5).Select(c => c.Id));
        var topper = age1s[5];
        g.Players[0].Stack(topper.Color).Meld(topper.Id);
        g.AvailableAgeAchievements.Clear();
        g.AvailableAgeAchievements.Add(1);

        var greedy = new GreedyController(seed: 1);
        var legal = LegalActions.Enumerate(g, g.Players[0]);
        Assert.Contains(legal, a => a is AchieveAction aa && aa.Age == 1);

        var picked = greedy.ChooseAction(g, g.Players[0], legal);
        Assert.IsType<AchieveAction>(picked);
        Assert.Equal(1, ((AchieveAction)picked).Age);
    }

    [Fact]
    public void ChooseAction_ReturnsLegalOption()
    {
        // Over several random-ish scenarios, every return value must be
        // in the legal list. Just a sanity guard against off-by-one or
        // stale-reference bugs.
        var rng = new Random(99);
        for (int trial = 0; trial < 5; trial++)
        {
            var g = GameSetup.Create(AllCards, 2, rng);
            var runner = new GameRunner(g, new IPlayerController[]
            {
                new RandomController(trial),
                new RandomController(trial + 1),
            });
            runner.CompleteInitialMeld();

            var legal = LegalActions.Enumerate(g, g.Active);
            var greedy = new GreedyController(seed: trial);
            var picked = greedy.ChooseAction(g, g.Active, legal);
            Assert.Contains(picked, legal);
        }
    }

    [Fact]
    public void SelfPlay_GreedyVsGreedy_Completes()
    {
        // Greedy on both sides just needs to terminate — the rollouts
        // inside each controller's look-ahead are independent, so the
        // decision loop doesn't loop.
        var g = GameSetup.Create(AllCards, 2, new Random(7));
        var runner = new GameRunner(g, new IPlayerController[]
        {
            new GreedyController(seed: 11),
            new GreedyController(seed: 22),
        });
        runner.CompleteInitialMeld();
        runner.RunToCompletion();

        Assert.True(g.IsGameOver);
        Assert.NotEmpty(g.Winners);
    }

    [Fact]
    public void SelfPlay_GreedyBeatsRandom_MostOfTheTime()
    {
        // Stochastic regression guard: greedy should win majority of
        // seeded head-to-head games. A threshold of 11/20 is a low bar
        // — if greedy is truly beating random handily (which it should,
        // given a 10_000-point achievement bonus drives its decisions)
        // this passes comfortably. Ties count as not-a-win for greedy
        // — they still flag asymmetry worth investigating.
        int greedyWins = 0;
        int games = 20;
        for (int seed = 0; seed < games; seed++)
        {
            var g = GameSetup.Create(AllCards, 2, new Random(seed));
            // Alternate seats so any starting-player bias cancels out.
            bool greedyIsP0 = seed % 2 == 0;
            var controllers = greedyIsP0
                ? new IPlayerController[] { new GreedyController(seed + 100), new RandomController(seed + 200) }
                : new IPlayerController[] { new RandomController(seed + 200), new GreedyController(seed + 100) };
            int greedyIndex = greedyIsP0 ? 0 : 1;

            var runner = new GameRunner(g, controllers);
            runner.CompleteInitialMeld();
            runner.RunToCompletion();

            if (g.Winners.Count == 1 && g.Winners[0] == greedyIndex)
                greedyWins++;
        }
        Assert.True(greedyWins >= 11,
            $"Greedy only won {greedyWins}/{games} vs Random — heuristic or search may have regressed.");
    }

    [Fact]
    public void ChooseInitialMeld_ReturnsCardFromHand()
    {
        var g = GameSetup.Create(AllCards, 2, new Random(3));
        var greedy = new GreedyController(seed: 3);
        int pick = greedy.ChooseInitialMeld(g, g.Players[0]);
        Assert.Contains(pick, g.Players[0].Hand);
    }
}
