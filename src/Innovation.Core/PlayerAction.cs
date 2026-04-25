namespace Innovation.Core;

/// <summary>
/// One of the four actions a player can take on their turn. Mirrors the
/// VB6 set of top-level choices: Draw, Meld, Achieve, Dogma.
/// </summary>
public abstract record PlayerAction;

public sealed record DrawAction : PlayerAction;

public sealed record MeldAction(int CardId) : PlayerAction;

/// <summary>Claim an age achievement tile (age 1–9).</summary>
public sealed record AchieveAction(int Age) : PlayerAction;

/// <summary>Activate the top card of one of the player's color piles.</summary>
public sealed record DogmaAction(CardColor Color) : PlayerAction;
