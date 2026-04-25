namespace Innovation.WinForms;

/// <summary>
/// What kind of controller each seat gets. Phase 6.1 supports the
/// three we already have in Core plus Human (via
/// <see cref="Innovation.Core.Players.HumanController"/>).
/// </summary>
public enum SeatKind
{
    Human,
    Random,
    Greedy,
}

/// <summary>
/// Result of the new-game dialog. Two seats for now — 3+ player games
/// are engine-supported but the UI layout assumes a head-to-head.
/// </summary>
public sealed class NewGameConfig
{
    public SeatKind Player0 { get; init; } = SeatKind.Human;
    public SeatKind Player1 { get; init; } = SeatKind.Greedy;
    public int Seed { get; init; } = Environment.TickCount;
}
