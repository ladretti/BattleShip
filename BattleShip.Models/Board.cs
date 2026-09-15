namespace BattleShip.Models;

/// <summary>
/// Represents a player's grid: their fleet and the shots received. This is the only
/// source of truth — no cell matrix is built or cached here. A matrix view, should one
/// be needed (display, DTO), is computed on demand from Ships and ReceivedShots, never
/// by duplicating them.
/// </summary>
public sealed class Board(int gridSize, IReadOnlyList<Ship> ships)
{
    private readonly HashSet<Coordinate> _receivedShots = new();

    public int GridSize { get; } = gridSize;

    // Defensive copy: without it, Board would share the reference of the list passed
    // by the caller. If the caller mutates its own list (removing a ship, for
    // example), Fire and AllSunk would read that mutation on what is supposed to be
    // the board's central invariant.
    public IReadOnlyList<Ship> Ships { get; } = [.. ships];
    public IReadOnlySet<Coordinate> ReceivedShots => _receivedShots;

    public bool AllSunk => Ships.All(s => s.IsSunk);

    public Result<ShotRecord> Fire(Coordinate at, Player by)
    {
        if (IsOutOfBounds(at))
            return Result<ShotRecord>.Fail(GameError.OutOfBounds);

        if (!_receivedShots.Add(at))
            return Result<ShotRecord>.Fail(GameError.CellAlreadyShot);

        var hitShip = Ships.FirstOrDefault(s => s.TryHit(at));
        if (hitShip is null)
            return Result<ShotRecord>.Ok(new ShotRecord(at, ShotResult.Miss, by, null));

        var result = hitShip.IsSunk ? ShotResult.Sunk : ShotResult.Hit;
        var sunkShipName = hitShip.IsSunk ? hitShip.Name : null;
        return Result<ShotRecord>.Ok(new ShotRecord(at, result, by, sunkShipName));
    }

    private bool IsOutOfBounds(Coordinate at) =>
        at.X < 0 || at.X >= GridSize || at.Y < 0 || at.Y >= GridSize;
}
