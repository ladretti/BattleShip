namespace BattleShip.Models.Contracts;

public sealed record CreateGameInput(int GridSize, string Difficulty);

public sealed record PlacementInput(IReadOnlyList<ShipPlacementInput> Ships);

public sealed record ShipPlacementInput(string Name, int X, int Y, string Orientation);

public sealed record EventQuery(int From);
