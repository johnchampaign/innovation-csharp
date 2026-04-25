namespace Innovation.Core;

/// <summary>
/// Drives dogma resolution: figures out which players are affected by each
/// effect, runs the handlers in VB6 order, and emits the shared-bonus draw.
///
/// Mirrors VB6 <c>perform_dogma_effect</c> / <c>perform_dogma_effect_by_level</c>
/// / <c>activate_card</c> (main.frm 3719–3809, 4251+):
///   • Demands: each other player with strictly fewer of the featured icon,
///     in turn order starting from the left of the active player.
///   • Shares: each other player with ≥ icons (clockwise from active), then
///     the active player last.
///   • If a non-active player progressed a share effect (<c>dogma_copied</c>),
///     the active player gets one bonus draw after all levels are done.
///
/// Handlers that need human input set <see cref="DogmaContext.Paused"/> and
/// return false; the engine stops iterating and the caller drives the UI,
/// then calls <see cref="Resume"/> once the choice has been applied.
/// </summary>
public sealed class DogmaEngine
{
    private readonly GameState _g;
    private readonly CardRegistry _registry;

    public DogmaEngine(GameState g, CardRegistry registry)
    {
        _g = g;
        _registry = registry;
    }

    /// <summary>
    /// Start a new dogma resolution. Returns the context; inspect
    /// <see cref="DogmaContext.IsComplete"/> to tell whether it ran to the
    /// end or paused partway.
    /// </summary>
    public DogmaContext Execute(int activatingPlayerIndex, int cardId)
    {
        var def = _registry.Get(cardId);
        var ctx = new DogmaContext(cardId, activatingPlayerIndex, def.FeaturedIcon);
        GameLog.Log($"Dogma: {GameLog.P(activatingPlayerIndex)} activates {GameLog.C(_g, cardId)} (featured {def.FeaturedIcon})");
        Resume(ctx);
        return ctx;
    }

    /// <summary>
    /// Continue a paused dogma. The caller is responsible for applying
    /// whatever choice the pause was waiting on and clearing
    /// <see cref="DogmaContext.Paused"/> before calling this.
    /// </summary>
    public void Resume(DogmaContext ctx)
    {
        if (ctx.IsComplete) return;
        if (ctx.Paused)
            throw new InvalidOperationException("Clear DogmaContext.Paused before resuming.");

        var def = _registry.Get(ctx.CardId);
        var activator = _g.Players[ctx.ActivatingPlayerIndex];

        // Drain any nested frames that paused mid-flight before returning to
        // the main level loop.
        RunNestedFrames(ctx);
        if (ctx.Paused) return;
        if (_g.IsGameOver) { ctx.IsComplete = true; return; }

        while (ctx.CurrentLevel < def.Effects.Count)
        {
            var effect = def.Effects[ctx.CurrentLevel];

            // Compute targets once per level so mid-level state changes don't
            // change who the level affects (matches VB6's pre-computed
            // affected_by_dogma matrix).
            if (ctx.CurrentTargetIndex == 0 && ctx.CurrentTargets.Count == 0)
            {
                int activatorIcons = IconCounter.Count(activator, def.FeaturedIcon, _g.Cards);
                ctx.CurrentTargets = ComputeTargets(ctx.ActivatingPlayerIndex, effect.IsDemand, activatorIcons, def.FeaturedIcon);
                var counts = string.Join(", ", _g.Players.Select(pl => $"{GameLog.P(pl)}={IconCounter.Count(pl, def.FeaturedIcon, _g.Cards)}"));
                var tgts = ctx.CurrentTargets.Count == 0 ? "(none)" : string.Join(",", ctx.CurrentTargets.Select(GameLog.P));
                GameLog.Log($"Effect {ctx.CurrentLevel + 1} ({(effect.IsDemand ? "demand" : "share/non-demand")}) — {def.FeaturedIcon} counts: {counts}; targets: {tgts}");
            }

            while (ctx.CurrentTargetIndex < ctx.CurrentTargets.Count)
            {
                var target = _g.Players[ctx.CurrentTargets[ctx.CurrentTargetIndex]];
                GameLog.Log($"  → handler {effect.Handler.GetType().Name} on {GameLog.P(target)}");
                bool progressed = effect.Handler.Execute(_g, target, ctx);
                GameLog.Log($"    = {(progressed ? "progressed" : "no-op")}{(ctx.Paused ? " (paused awaiting choice)" : "")}");

                if (ctx.Paused) return; // handler still has more to do with this target

                // Handler may have queued nested "execute another card for
                // self only" frames (Robotics, Self Service, …). Drain them
                // before advancing past this target.
                RunNestedFrames(ctx);
                if (ctx.Paused) return;

                if (progressed && !effect.IsDemand && target.Index != ctx.ActivatingPlayerIndex)
                    ctx.SharedBonus = true;

                ctx.CurrentTargetIndex++;

                if (_g.IsGameOver) { ctx.IsComplete = true; return; }
            }

            // Finished this level — advance.
            ctx.CurrentLevel++;
            ctx.CurrentTargetIndex = 0;
            ctx.CurrentTargets = Array.Empty<int>();
        }

        // All levels done. Shared-bonus draw fires once per dogma, not per
        // level (VB6 main.frm 3805).
        if (ctx.SharedBonus && !_g.IsGameOver)
        {
            Mechanics.Draw(_g, activator);
        }

        ctx.IsComplete = true;
    }

    /// <summary>
    /// Service pushed nested-execution frames ("execute card X's non-demand
    /// effects for player Y only"). Each frame carries its own handler-state
    /// slot so nested handlers don't clobber the caller's
    /// <see cref="DogmaContext.HandlerState"/>. Stops when the top frame
    /// pauses — the outer engine loop will be re-entered via
    /// <see cref="Resume"/> once the choice resolves.
    /// </summary>
    private void RunNestedFrames(DogmaContext ctx)
    {
        while (ctx.NestedFrames.Count > 0)
        {
            var frame = ctx.NestedFrames.Peek();
            var def = _registry.Get(frame.CardId);

            while (frame.EffectIndex < def.Effects.Count)
            {
                var effect = def.Effects[frame.EffectIndex];
                if (effect.IsDemand) { frame.EffectIndex++; continue; }

                var target = _g.Players[frame.TargetPlayerIndex];
                GameLog.Log($"  ↪ nested {GameLog.C(_g, frame.CardId)} effect {frame.EffectIndex + 1} on {GameLog.P(target)} (self-only)");

                // Swap HandlerState so the nested handler sees its own scratch.
                var savedState = ctx.HandlerState;
                ctx.HandlerState = frame.HandlerState;
                effect.Handler.Execute(_g, target, ctx);
                frame.HandlerState = ctx.HandlerState;
                ctx.HandlerState = savedState;

                if (ctx.Paused) return;
                if (_g.IsGameOver) { ctx.NestedFrames.Clear(); return; }

                frame.EffectIndex++;
            }

            ctx.NestedFrames.Pop();
        }
    }

    /// <summary>
    /// Player indexes the current effect will run against, in turn order.
    /// Demand: non-active players with strictly fewer featured icons.
    /// Share: active player first, then non-active with ≥ icons.
    /// </summary>
    private List<int> ComputeTargets(int activatingIdx, bool isDemand, int activatorIcons, Icon featured)
    {
        var list = new List<int>();
        int n = _g.Players.Length;

        if (isDemand)
        {
            for (int i = 1; i < n; i++)
            {
                int idx = (activatingIdx + i) % n;
                int count = IconCounter.Count(_g.Players[idx], featured, _g.Cards);
                if (count < activatorIcons) list.Add(idx);
            }
        }
        else
        {
            // Rulebook: "Before you execute an effect, each other player who is
            // eligible to share must also use it. Starting to your left and
            // going clockwise, each of them follows the effect's instructions.
            // Then you perform them." Sharers first, activator last.
            for (int i = 1; i < n; i++)
            {
                int idx = (activatingIdx + i) % n;
                int count = IconCounter.Count(_g.Players[idx], featured, _g.Cards);
                if (count >= activatorIcons) list.Add(idx);
            }
            list.Add(activatingIdx);
        }
        return list;
    }
}
