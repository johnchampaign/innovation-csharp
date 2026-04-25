namespace Innovation.Core.Handlers;

/// <summary>
/// Masonry (age 1, Yellow/Castle): "You may meld any number of cards from
/// your hand, each with a [Castle]. If you melded four or more cards, claim
/// the Monument achievement."
///
/// Mirrors VB6 main.frm 4407–4434. The "4+ melded ⇒ Monument" trigger is
/// Masonry-specific and separate from the per-turn
/// <see cref="PlayerState.ScoredThisTurn"/> / <see cref="PlayerState.TuckedThisTurn"/>
/// counters, so we claim Monument directly here rather than relying on the
/// mutation hooks inside <see cref="Mechanics"/>.
/// </summary>
public sealed class MasonryHandler : IDogmaHandler
{
    public bool Execute(GameState g, PlayerState target, DogmaContext ctx)
    {
        // Only castle-bearing cards are eligible (VB6 card_has_symbol(id, 2)).
        var eligible = target.Hand
            .Where(id => HasCastle(g.Cards[id]))
            .ToArray();

        if (eligible.Length == 0) return false;

        if (ctx.PendingChoice is null)
        {
            ctx.PendingChoice = new SelectHandCardSubsetRequest
            {
                Prompt = "Masonry: meld any number of castle-bearing cards. "
                       + "Melding four or more claims Monument.",
                PlayerIndex = target.Index,
                EligibleCardIds = eligible,
                MinCount = 0,
                MaxCount = eligible.Length,
            };
            ctx.Paused = true;
            return false;
        }

        var req = (SelectHandCardSubsetRequest)ctx.PendingChoice;
        ctx.PendingChoice = null;

        int melded = req.ChosenCardIds.Count;
        if (melded == 0) return false;

        foreach (var id in req.ChosenCardIds)
        {
            Mechanics.Meld(g, target, id);
            if (g.IsGameOver) return true;
        }

        if (melded >= 4)
            AchievementRules.ClaimSpecial(g, target, SpecialAchievements.Monument);

        return true;
    }

    private static bool HasCastle(Card c) =>
        c.Top == Icon.Castle || c.Left == Icon.Castle ||
        c.Middle == Icon.Castle || c.Right == Icon.Castle;
}
