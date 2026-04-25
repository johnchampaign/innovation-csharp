namespace Innovation.Core.Handlers;

/// <summary>
/// Fission (age 9, Red/Clock) — demand: "I demand you draw a 10! If it is
/// red, remove all hands, boards, and score piles from the game! If this
/// occurs, the dogma action is complete."
///
/// The apocalyptic branch wipes every player's hand/board/score-pile and
/// discards the drawn card. A sentinel on <see cref="DogmaContext.HandlerState"/>
/// signals the second effect to skip via <see cref="FissionReturnHandler"/>.
/// </summary>
public sealed class FissionDemandHandler : IDogmaHandler
{
    public static readonly object FissionWiped = new();

    public bool Execute(GameState g, PlayerState target, DogmaContext ctx)
    {
        int id = Mechanics.DrawFromAge(g, target, 10);
        if (id < 0 || g.IsGameOver) return true;

        if (g.Cards[id].Color != CardColor.Red)
        {
            ctx.DemandSuccessful = true;
            return true;
        }

        GameLog.Log("*** FISSION: red 10 drawn — removing all hands, boards, and score piles ***");
        foreach (var p in g.Players)
        {
            p.Hand.Clear();
            p.ScorePile.Clear();
            foreach (var s in p.Stacks) s.ClearForFission();
        }
        // The drawn card itself was in target.Hand before the clear; already gone.
        ctx.DemandSuccessful = true;
        ctx.HandlerState = FissionWiped;
        SpecialAchievements.CheckAll(g);
        return true;
    }
}
