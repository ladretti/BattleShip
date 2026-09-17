namespace BattleShip.Models;

public abstract record GameEvent(int Sequence);

public sealed record GameCreated(
    int Sequence,
    GameRules Rules,
    IReadOnlyList<Ship> OpponentShips,
    string Difficulty) : GameEvent(Sequence);

public sealed record HumanFleetPlaced(
    int Sequence,
    IReadOnlyList<Ship> Ships) : GameEvent(Sequence);

public sealed record ShotFired(
    int Sequence,
    Coordinate At,
    Player By,
    ShotResult Result,
    string? SunkShipName) : GameEvent(Sequence);

public sealed record GameEnded(
    int Sequence,
    Player Winner) : GameEvent(Sequence);
