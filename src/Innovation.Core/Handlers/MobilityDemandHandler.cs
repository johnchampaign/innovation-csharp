namespace Innovation.Core.Handlers;

/// <summary>
/// Mobility (age 8, Red/Factory) — demand: "I demand you transfer your two
/// highest non-red cards without a [Factory] from your board to my score
/// pile! If you transferred any cards, draw an 8!"
///
/// "Your cards" here means top cards on non-red stacks (one per color). Up
/// to 2 of the highest-age eligible tops are transferred. Ties at the
/// cutoff are broken by the defender.
/// </summary>
public sealed class MobilityDemandHandler : IDogmaHandler
{
    private enum Stage { Start, ResolveTie }

    private sealed class State
    {
        public Stage Stage;
        public List<CardColor> Auto = new(); // colors definitely moving
        public int Remaining;                // how many more to transfer
        public int TiedAge;
    }

    public bool Execute(GameState g, PlayerState target, DogmaContext ctx)
    {
        var activator = g.Players[ctx.ActivatingPlayerIndex];
        var st = ctx.HandlerState as State ?? new State();

        if (st.Stage == Stage.Start)
        {
            var eligible = new List<(CardColor color, int age, int id)>();
            foreach (CardColor c in Enum.GetValues<CardColor>())
            {
                if (c == CardColor.Red) continue;
                var s = target.Stack(c);
                if (s.IsEmpty) continue;
                var card = g.Cards[s.Top];
                if (Mechanics.HasIcon(card, Icon.Factory)) continue;
                eligible.Add((c, card.Age, s.Top));
            }
            if (eligible.Count == 0) return false;

            eligible.Sort((a, b) => b.age.CompareTo(a.age));

            // Take up to 2 highest; figure out forced vs tied at boundary.
            int take = Math.Min(2, eligible.Count);
            int cutoffAge = eligible[take - 1].age;
            var strictlyAbove = eligible.Where(e => e.age > cutoffAge).ToList();
            var atCutoff = eligible.Where(e => e.age == cutoffAge).ToList();

            st.Auto.AddRange(strictlyAbove.Select(e => e.color));
            int needFromTied = take - strictlyAbove.Count;

            if (atCutoff.Count == needFromTied)
            {
                st.Auto.AddRange(atCutoff.Select(e => e.color));
            }
            else
            {
                // Transfer the forced ones first, then prompt.
                foreach (var col in st.Auto)
                    Mechanics.TransferBoardToScore(g, target, activator, col);
                ctx.DemandSuccessful |= st.Auto.Count > 0;

                st.Remaining = needFromTied;
                st.TiedAge = cutoffAge;
                st.Stage = Stage.ResolveTie;

                ctx.PendingChoice = new SelectColorRequest
                {
                    Prompt = $"Mobility: choose a top non-red age-{cutoffAge} card (without [Factory]) to give up.",
                    PlayerIndex = target.Index,
                    EligibleColors = atCutoff.Select(e => e.color).ToArray(),
                };
                ctx.HandlerState = st;
                ctx.Paused = true;
                return ctx.DemandSuccessful;
            }

            // No ties to resolve — transfer all autos, then reward.
            foreach (var col in st.Auto)
                Mechanics.TransferBoardToScore(g, target, activator, col);
            if (st.Auto.Count > 0)
            {
                ctx.DemandSuccessful = true;
                if (!g.IsGameOver) Mechanics.DrawFromAge(g, target, 8);
            }
            ctx.HandlerState = null;
            return st.Auto.Count > 0;
        }

        // Stage.ResolveTie — one pick per iteration.
        var req = (SelectColorRequest)ctx.PendingChoice!;
        ctx.PendingChoice = null;
        if (req.ChosenColor is not CardColor pick) { ctx.HandlerState = null; return ctx.DemandSuccessful; }

        Mechanics.TransferBoardToScore(g, target, activator, pick);
        ctx.DemandSuccessful = true;
        st.Remaining--;

        if (st.Remaining > 0 && !g.IsGameOver)
        {
            // More to pick from remaining tops still at the cutoff age.
            var remainingColors = new List<CardColor>();
            foreach (CardColor c in Enum.GetValues<CardColor>())
            {
                if (c == CardColor.Red) continue;
                var s = target.Stack(c);
                if (s.IsEmpty) continue;
                var card = g.Cards[s.Top];
                if (Mechanics.HasIcon(card, Icon.Factory)) continue;
                if (card.Age == st.TiedAge) remainingColors.Add(c);
            }
            if (remainingColors.Count == 0)
            {
                if (!g.IsGameOver) Mechanics.DrawFromAge(g, target, 8);
                ctx.HandlerState = null;
                return true;
            }
            ctx.PendingChoice = new SelectColorRequest
            {
                Prompt = $"Mobility: choose another top non-red age-{st.TiedAge} card to give up.",
                PlayerIndex = target.Index,
                EligibleColors = remainingColors,
            };
            ctx.HandlerState = st;
            ctx.Paused = true;
            return true;
        }

        if (!g.IsGameOver) Mechanics.DrawFromAge(g, target, 8);
        ctx.HandlerState = null;
        return true;
    }
}
