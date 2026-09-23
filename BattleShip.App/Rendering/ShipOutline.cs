using BattleShip.Models;
using BattleShip.Models.Contracts;

namespace BattleShip.App.Rendering;

public sealed record ShipOutline(
    string Name,
    Coordinate Origin,
    bool Vertical,
    int Size,
    bool IsSunk,
    IReadOnlyList<CellDto> Cells)
{
    public static ShipOutline? FromCells(string name, IReadOnlyList<CellDto> cells, bool isSunk)
    {
        if (cells.Count == 0)
            return null;

        var sameRow = cells.All(c => c.Y == cells[0].Y);
        var sameColumn = cells.All(c => c.X == cells[0].X);

        if (!sameRow && !sameColumn)
            return null;

        var vertical = !sameRow && sameColumn;
        var origin = new Coordinate(cells.Min(c => c.X), cells.Min(c => c.Y));

        return new ShipOutline(name, origin, vertical, cells.Count, isSunk, cells);
    }

    public IEnumerable<(Coordinate At, string State)> Damage =>
    Cells.Where(c => c.State is "hit" or "sunk")
         .Select(c => (new Coordinate(c.X, c.Y), c.State));

    public IEnumerable<Coordinate> Footprint =>
    Cells.Select(c => new Coordinate(c.X, c.Y));
}
