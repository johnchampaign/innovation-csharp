namespace Innovation.Core.Players;

/// <summary>
/// Uniformly-random <see cref="IPlayerController"/>. Useful as a baseline
/// opponent and — more importantly — as an engine smoke test: two random
/// controllers playing against each other must always terminate cleanly.
/// A hang or exception in self-play means a handler has a pause/resume bug.
///
/// Uses <see cref="System.Random"/> per the project convention. Pass a
/// seed for reproducible runs in tests.
/// </summary>
public sealed class RandomController : IPlayerController
{
    private readonly Random _rng;

    public RandomController(Random rng) => _rng = rng;
    public RandomController(int seed) : this(new Random(seed)) { }
    public RandomController() : this(new Random()) { }

    public int ChooseInitialMeld(GameState g, PlayerState self)
        => self.Hand[_rng.Next(self.Hand.Count)];

    public PlayerAction ChooseAction(GameState g, PlayerState self, IReadOnlyList<PlayerAction> legal)
        => legal[_rng.Next(legal.Count)];

    public int? ChooseHandCard(GameState g, PlayerState self, SelectHandCardRequest req)
    {
        // When the prompt is optional, decline half the time. Keeps self-
        // play exploring both branches rather than always returning a card.
        if (req.AllowNone && _rng.Next(2) == 0) return null;
        if (req.EligibleCardIds.Count == 0) return null;
        return req.EligibleCardIds[_rng.Next(req.EligibleCardIds.Count)];
    }

    public IReadOnlyList<int> ChooseHandCardSubset(GameState g, PlayerState self, SelectHandCardSubsetRequest req)
    {
        // Pick a valid size uniformly at random, then sample without
        // replacement from the eligible pool.
        int maxTakeable = Math.Min(req.MaxCount, req.EligibleCardIds.Count);
        int minTakeable = Math.Min(req.MinCount, maxTakeable);
        int size = minTakeable + _rng.Next(maxTakeable - minTakeable + 1);

        var pool = req.EligibleCardIds.ToList();
        var result = new List<int>(size);
        for (int i = 0; i < size; i++)
        {
            int k = _rng.Next(pool.Count);
            result.Add(pool[k]);
            pool.RemoveAt(k);
        }
        return result;
    }

    public IReadOnlyList<int> ChooseCardOrder(GameState g, PlayerState self, SelectCardOrderRequest req)
        => req.CardIds.ToList();   // input order is acceptable

    public IReadOnlyList<int> ChooseScoreCardSubset(GameState g, PlayerState self, SelectScoreCardSubsetRequest req)
    {
        int maxTakeable = Math.Min(req.MaxCount, req.EligibleCardIds.Count);
        int minTakeable = Math.Min(req.MinCount, maxTakeable);
        int size = minTakeable + _rng.Next(maxTakeable - minTakeable + 1);
        var pool = req.EligibleCardIds.ToList();
        var result = new List<int>(size);
        for (int i = 0; i < size; i++)
        {
            int k = _rng.Next(pool.Count);
            result.Add(pool[k]);
            pool.RemoveAt(k);
        }
        return result;
    }

    public bool ChooseYesNo(GameState g, PlayerState self, YesNoChoiceRequest req)
        => _rng.Next(2) == 0;

    public CardColor? ChooseColor(GameState g, PlayerState self, SelectColorRequest req)
    {
        if (req.EligibleColors.Count == 0) return null;
        if (req.AllowNone && _rng.Next(2) == 0) return null;
        return req.EligibleColors[_rng.Next(req.EligibleColors.Count)];
    }

    public int? ChooseScoreCard(GameState g, PlayerState self, SelectScoreCardRequest req)
    {
        if (req.AllowNone && _rng.Next(2) == 0) return null;
        if (req.EligibleCardIds.Count == 0) return null;
        return req.EligibleCardIds[_rng.Next(req.EligibleCardIds.Count)];
    }

    public IReadOnlyList<int> ChooseStackOrder(GameState g, PlayerState self, SelectStackOrderRequest req)
    {
        // Random permutation of the current order.
        var list = req.CurrentOrder.ToList();
        for (int i = list.Count - 1; i > 0; i--)
        {
            int j = _rng.Next(i + 1);
            (list[i], list[j]) = (list[j], list[i]);
        }
        return list;
    }

    public int? ChooseValue(GameState g, PlayerState self, SelectValueRequest req)
    {
        if (req.AllowNone && _rng.Next(2) == 0) return null;
        if (req.EligibleValues.Count == 0) return null;
        return req.EligibleValues[_rng.Next(req.EligibleValues.Count)];
    }
}
