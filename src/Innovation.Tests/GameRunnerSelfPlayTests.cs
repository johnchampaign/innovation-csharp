using System.Text;
using Innovation.Core;
using Innovation.Core.Players;
using Xunit;

namespace Innovation.Tests;

/// <summary>
/// End-to-end smoke: two random controllers can play a full game to
/// completion without the engine deadlocking, crashing, or landing in an
/// inconsistent phase. These tests catch regressions where a new handler
/// mis-handles its pause/resume contract — they're the reason the
/// <see cref="GameRunner.SafetyStepLimit"/> exists.
///
/// Intentionally runs many games with different seeds: dogma paths are
/// combinatorial and a single seed may miss a rare latent bug.
/// </summary>
public class GameRunnerSelfPlayTests
{
    static GameRunnerSelfPlayTests()
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
    }

    private static IReadOnlyList<Card> AllCards => CardDataLoader.LoadFromEmbeddedResource();

    private static void PlayOne(int players, int seed)
    {
        var rng = new Random(seed);
        var g = GameSetup.Create(AllCards, players, rng);
        var controllers = Enumerable.Range(0, players)
            .Select(i => (IPlayerController)new RandomController(seed + 1_000 * (i + 1)))
            .ToList();
        var runner = new GameRunner(g, controllers);
        runner.CompleteInitialMeld();
        runner.RunToCompletion();

        Assert.True(g.IsGameOver, $"Game with seed {seed} ({players}p) didn't end.");
        Assert.NotEmpty(g.Winners);
        Assert.All(g.Winners, w => Assert.InRange(w, 0, players - 1));
    }

    [Theory]
    [InlineData(2, 0)]
    [InlineData(2, 1)]
    [InlineData(2, 2)]
    [InlineData(3, 100)]
    [InlineData(3, 101)]
    [InlineData(4, 200)]
    [InlineData(4, 201)]
    public void RandomSelfPlay_CompletesCleanly(int players, int seed) => PlayOne(players, seed);

    [Fact]
    public void RandomSelfPlay_ManyGames_AllTerminate()
    {
        // Hammer the engine: 30 full games across 2–4 players. The point is
        // to catch pause-loop bugs a single seed might hide. Keep the count
        // modest so the suite stays fast; raise if a regression slips
        // through in the future.
        for (int seed = 0; seed < 10; seed++)
        {
            PlayOne(2, seed + 300);
            PlayOne(3, seed + 400);
            PlayOne(4, seed + 500);
        }
    }

    [Fact]
    public void InitialMeld_AssignsStartingPlayer()
    {
        var g = GameSetup.Create(AllCards, 2, new Random(7));
        var controllers = new IPlayerController[]
        {
            new RandomController(7),
            new RandomController(8),
        };
        var runner = new GameRunner(g, controllers);
        runner.CompleteInitialMeld();

        // After the opening meld the active-player index is determined
        // alphabetically by the melded card titles — we just verify some
        // player is in seat and each player has something on their board.
        Assert.Equal(GamePhase.Action, g.Phase);
        Assert.Equal(1, g.ActionsRemaining);
        foreach (var p in g.Players)
            Assert.Contains(p.Stacks, s => !s.IsEmpty);
    }

    [Fact]
    public void OnStepCompleted_FiresAfterInitialMeldAndEachStep()
    {
        // The callback is how the UI (and, potentially, a headless game
        // recorder) learns about each state change without polling.
        // Contract: once per CompleteInitialMeld, then at least once per
        // top-level action or dogma resume. First actor arg is -1 for
        // the initial-meld fire; non-negative seat index otherwise.
        var g = GameSetup.Create(AllCards, 2, new Random(42));
        var fires = new List<(int actor, PlayerAction? action)>();
        var runner = new GameRunner(g, new IPlayerController[]
        {
            new RandomController(1), new RandomController(2),
        })
        {
            OnStepCompleted = (actor, action) => fires.Add((actor, action)),
        };

        runner.CompleteInitialMeld();
        Assert.Single(fires);
        Assert.Equal(-1, fires[0].actor);
        Assert.Null(fires[0].action);

        int beforeRun = fires.Count;
        runner.RunToCompletion();
        Assert.True(fires.Count > beforeRun + 10,
            $"expected many step callbacks during a full game, got {fires.Count - beforeRun}");

        // Every non-setup actor must be a valid seat index.
        foreach (var (actor, _) in fires.Skip(1))
            Assert.InRange(actor, 0, g.Players.Length - 1);

        // At least some callbacks should carry an action (not all dogma resumes).
        Assert.Contains(fires.Skip(1), f => f.action is not null);
    }

    [Fact]
    public void OnStepCompleted_NotSetOnTrialRunner_DoesNotFire()
    {
        // Sanity: GreedyController's internal look-ahead creates trial
        // GameRunners without assigning OnStepCompleted. If that ever
        // regressed to fire on trial clones, every candidate-action
        // evaluation would thrash the UI / log. This test doesn't reach
        // into the greedy controller — it just proves that omitting the
        // assignment means zero callbacks.
        var g = GameSetup.Create(AllCards, 2, new Random(5));
        int count = 0;
        var runner = new GameRunner(g, new IPlayerController[]
        {
            new RandomController(1), new RandomController(2),
        });
        // note: no OnStepCompleted assigned
        runner.CompleteInitialMeld();
        runner.RunToCompletion();
        _ = count; // keep the compiler from warning
        // If OnStepCompleted-null were ever dereferenced we'd NRE above,
        // so just reaching here is the assertion.
        Assert.True(g.IsGameOver);
    }
}
