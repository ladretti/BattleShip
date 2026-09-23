namespace BattleShip.Models;

using System.Collections.Frozen;

public sealed class Ship(string name, int size, IEnumerable<Coordinate> cells)
{
    public string Name { get; } = name;
    public int Size { get; } = size;
    public IReadOnlySet<Coordinate> Cells { get; } = cells.ToFrozenSet();

    private readonly HashSet<Coordinate> _hitCells = new();

    public IReadOnlySet<Coordinate> HitCells => _hitCells;

    public bool IsSunk => _hitCells.Count == Size;

    public bool TryHit(Coordinate at)
    {
        if (!Cells.Contains(at))
            return false;

        return _hitCells.Add(at);
    }
}
