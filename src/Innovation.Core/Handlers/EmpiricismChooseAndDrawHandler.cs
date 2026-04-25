namespace Innovation.Core.Handlers;

/// <summary>
/// Empiricism (age 8, Purple/Lightbulb) — effect 1: "Choose two colors,
/// then draw and reveal a 9. If it is either of the colors you chose,
/// meld it and you may splay your cards of that color up."
///
/// Two sequential SelectColorRequests (can't select the same color twice),
/// then reveal a 9 and optionally splay up.
/// </summary>
public sealed class EmpiricismChooseAndDrawHandler : IDogmaHandler
{
    private enum Stage { PickFirst, PickSecond, AskSplay }

    private sealed class State
    {
        public Stage Stage;
        public CardColor First;
        public CardColor Second;
        public CardColor RevealedMatched;
    }

    public bool Execute(GameState g, PlayerState target, DogmaContext ctx)
    {
        var st = ctx.HandlerState as State ?? new State();

        if (st.Stage == Stage.PickFirst)
        {
            if (ctx.PendingChoice is null)
            {
                ctx.PendingChoice = new SelectColorRequest
                {
                    Prompt = "Empiricism: choose the first color.",
                    PlayerIndex = target.Index,
                    EligibleColors = Enum.GetValues<CardColor>().ToArray(),
                };
                ctx.HandlerState = st;
                ctx.Paused = true;
                return false;
            }

            var r = (SelectColorRequest)ctx.PendingChoice;
            ctx.PendingChoice = null;
            if (r.ChosenColor is not CardColor c1) { ctx.HandlerState = null; return false; }
            st.First = c1;
            st.Stage = Stage.PickSecond;
        }

        if (st.Stage == Stage.PickSecond)
        {
            if (ctx.PendingChoice is null)
            {
                var rest = Enum.GetValues<CardColor>().Where(c => c != st.First).ToArray();
                ctx.PendingChoice = new SelectColorRequest
                {
                    Prompt = $"Empiricism: choose the second color (first was {st.First}).",
                    PlayerIndex = target.Index,
                    EligibleColors = rest,
                };
                ctx.HandlerState = st;
                ctx.Paused = true;
                return false;
            }

            var r = (SelectColorRequest)ctx.PendingChoice;
            ctx.PendingChoice = null;
            if (r.ChosenColor is not CardColor c2) { ctx.HandlerState = null; return false; }
            st.Second = c2;

            int drawn = Mechanics.DrawFromAge(g, target, 9);
            if (drawn < 0 || g.IsGameOver) { ctx.HandlerState = null; return true; }

            var color = g.Cards[drawn].Color;
            GameLog.Log($"Empiricism: revealed {GameLog.C(g, drawn)} — chose {st.First}/{st.Second}");
            if (color == st.First || color == st.Second)
            {
                Mechanics.Meld(g, target, drawn);
                if (g.IsGameOver) { ctx.HandlerState = null; return true; }
                st.RevealedMatched = color;
                st.Stage = Stage.AskSplay;
            }
            else
            {
                ctx.HandlerState = null;
                return true;
            }
        }

        // Stage.AskSplay
        if (ctx.PendingChoice is null)
        {
            ctx.PendingChoice = new YesNoChoiceRequest
            {
                Prompt = $"Empiricism: splay your {st.RevealedMatched} cards up?",
                PlayerIndex = target.Index,
            };
            ctx.HandlerState = st;
            ctx.Paused = true;
            return false;
        }

        var yn = (YesNoChoiceRequest)ctx.PendingChoice;
        ctx.PendingChoice = null;
        ctx.HandlerState = null;
        if (yn.ChosenYes)
            Mechanics.Splay(g, target, st.RevealedMatched, Splay.Up);
        return true;
    }
}
