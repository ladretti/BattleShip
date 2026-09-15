namespace BattleShip.Models;

using System.Collections.Frozen;

public sealed class Ship(string name, int size, IEnumerable<Coordinate> cells)
{
    public string Name { get; } = name;
    public int Size { get; } = size;
    public IReadOnlySet<Coordinate> Cells { get; } = cells.ToFrozenSet();

    private readonly HashSet<Coordinate> _hitCells = new();

    /// <summary>
    /// The subset of the ship's cells that have been hit. Every mutation of this set
    /// goes exclusively through TryHit(), which guarantees that only valid cells are
    /// added and that a cell can only be hit once.
    ///
    /// Careful: IReadOnlySet is a view over a mutable HashSet. A cast to HashSet
    /// would make it possible to bypass TryHit() and to violate the invariants. This
    /// risk is accepted and confined to the domain (BattleShip.Models). Unlike Cells,
    /// which is a genuinely immutable FrozenSet, this asymmetry reflects a decision:
    /// HitCells needs frequent mutations during play, whereas Cells is stable from
    /// the ship's creation.
    /// </summary>
    public IReadOnlySet<Coordinate> HitCells => _hitCells;

    public bool IsSunk => _hitCells.Count == Size;

    public bool TryHit(Coordinate at)
    {
        if (!Cells.Contains(at))
            return false;

        return _hitCells.Add(at);
    }
}
