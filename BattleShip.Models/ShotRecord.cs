namespace BattleShip.Models;

public sealed record ShotRecord(Coordinate At, ShotResult Result, Player By, string? SunkShipName);
