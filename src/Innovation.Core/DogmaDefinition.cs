namespace Innovation.Core;

/// <summary>
/// Declarative description of a card's dogma. Separated from the card data
/// (which is authored in TSV) because the effect logic is hand-coded per
/// card in C#.
/// </summary>
public sealed record DogmaDefinition(Icon FeaturedIcon, IReadOnlyList<DogmaEffect> Effects);

/// <summary>
/// One effect slot. A card has 1–3 effects. Whether an effect is a demand
/// is derived from its dogma text ("I demand...") in the original VB6
/// (main.frm 7544) and stored here for convenience.
/// </summary>
public sealed record DogmaEffect(bool IsDemand, string Text, IDogmaHandler Handler);

public interface IDogmaHandler
{
    /// <summary>
    /// Execute the effect for <paramref name="target"/>. Return true if the
    /// effect progressed (transferred a card, drew a card, scored, etc.) —
    /// this powers the "shared bonus" draw when a non-active player
    /// executes a non-demand effect and progresses.
    ///
    /// If the effect requires human input, set <see cref="DogmaContext.Paused"/>
    /// and return false; the engine will stop iterating and wait for
    /// external resumption.
    /// </summary>
    bool Execute(GameState g, PlayerState target, DogmaContext ctx);
}
