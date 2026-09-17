namespace BattleShip.Models;

public abstract record GameEvent(int Sequence);

public sealed record ShipSnapshot(string Name, int Size, IReadOnlyList<Coordinate> Cells)
{
    public static ShipSnapshot Of(Ship ship) => new(ship.Name, ship.Size, [.. ship.Cells]);

    public Ship ToShip() => new(Name, Size, Cells);
}

public sealed record GameCreated(
    int Sequence,
    GameRules Rules,
    IReadOnlyList<ShipSnapshot> OpponentShips,
    string Difficulty) : GameEvent(Sequence);

public sealed record HumanFleetPlaced(
    int Sequence,
    IReadOnlyList<ShipSnapshot> Ships) : GameEvent(Sequence);

public sealed record ShotFired(
    int Sequence,
    Coordinate At,
    Player By,
    ShotResult Result,
    string? SunkShipName) : GameEvent(Sequence);

public sealed record GameEnded(
    int Sequence,
    Player Winner) : GameEvent(Sequence);
