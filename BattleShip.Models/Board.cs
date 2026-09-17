namespace BattleShip.Models;

public readonly record struct ShotOutcome(ShotResult Result, string? SunkShipName);

public sealed class Board(int gridSize, IReadOnlyList<Ship> ships)
{
    private readonly HashSet<Coordinate> _receivedShots = new();

    public int GridSize { get; } = gridSize;

    public IReadOnlyList<Ship> Ships { get; } = [.. ships];
    public IReadOnlySet<Coordinate> ReceivedShots => _receivedShots;

    public bool AllSunk => Ships.All(s => s.IsSunk);

    public Result<ShotOutcome> Decide(Coordinate at)
    {
        if (IsOutOfBounds(at))
            return Result<ShotOutcome>.Fail(GameError.OutOfBounds);

        if (_receivedShots.Contains(at))
            return Result<ShotOutcome>.Fail(GameError.CellAlreadyShot);

        var target = Ships.FirstOrDefault(s => s.Cells.Contains(at));
        if (target is null)
            return Result<ShotOutcome>.Ok(new ShotOutcome(ShotResult.Miss, null));

        var sinks = target.HitCells.Count + 1 == target.Size;
        return Result<ShotOutcome>.Ok(sinks
            ? new ShotOutcome(ShotResult.Sunk, target.Name)
            : new ShotOutcome(ShotResult.Hit, null));
    }

    public void Apply(Coordinate at)
    {
        if (IsOutOfBounds(at))
            throw new ArgumentOutOfRangeException(
                nameof(at), at, "An applied event can never land outside the grid.");

        if (!_receivedShots.Add(at))
            return;

        Ships.FirstOrDefault(s => s.Cells.Contains(at))?.TryHit(at);
    }

    public Result<ShotRecord> Fire(Coordinate at, Player by)
    {
        var decision = Decide(at);
        if (!decision.IsOk)
            return Result<ShotRecord>.Fail(decision.Error);

        Apply(at);

        return Result<ShotRecord>.Ok(
            new ShotRecord(at, decision.Value.Result, by, decision.Value.SunkShipName));
    }

    private bool IsOutOfBounds(Coordinate at) =>
        at.X < 0 || at.X >= GridSize || at.Y < 0 || at.Y >= GridSize;
}
