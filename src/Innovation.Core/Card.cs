namespace Innovation.Core;

public sealed record Card(
    int Id,
    int Age,
    CardColor Color,
    string Title,
    Icon Top,
    Icon Left,
    Icon Middle,
    Icon Right,
    IconSlot HexagonSlot,
    string HexagonDescription,
    Icon DogmaIcon,
    IReadOnlyList<string> DogmaEffects);
