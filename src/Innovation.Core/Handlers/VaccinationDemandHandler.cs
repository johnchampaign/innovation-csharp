namespace Innovation.Core.Handlers;

/// <summary>
/// Vaccination (age 6, Yellow/Leaf) — demand: "I demand you return
/// all the lowest cards in your score pile! If you returned any,
/// draw and meld a 6!"
///
/// Lowest = lowest age in target's score pile. Returning goes under
/// the deck via <see cref="Mechanics.Return"/>-equivalent path (score
/// pile → deck bottom). On success the target (the demand's victim)
/// draws and melds a 6; they also set <see cref="DogmaContext.DemandSuccessful"/>
/// for <see cref="VaccinationDrawIfDemandHandler"/> to give the
/// activator a 7.
/// </summary>
public sealed class VaccinationDemandHandler : IDogmaHandler
{
    public bool Execute(GameState g, PlayerState target, DogmaContext ctx)
    {
        if (target.ScorePile.Count == 0) return false;
        int lowest = target.ScorePile.Min(id => g.Cards[id].Age);
        var lows = target.ScorePile.Where(id => g.Cards[id].Age == lowest).ToArray();
        if (lows.Length == 0) return false;

        foreach (var id in lows)
        {
            target.ScorePile.Remove(id);
            int age = g.Cards[id].Age;
            g.Decks[age].Add(id);
            GameLog.Log($"{GameLog.P(target)} returns (from score) {GameLog.C(g, id)}");
        }
        SpecialAchievements.CheckAll(g);
        ctx.DemandSuccessful = true;
        if (g.IsGameOver) return true;

        Mechanics.DrawAndMeld(g, target, 6);
        return true;
    }
}
