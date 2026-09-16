using BattleShip.Models;
using BattleShip.Models.Contracts;

namespace BattleShip.App.Rendering;

/// <summary>
/// One ship, as the overlay needs it: where it starts, which way it runs, how long it is,
/// and which of its cells have been hit.
///
/// <para>Nothing new crosses the wire for this. Orientation and extent are <b>derived</b>
/// from the cells the server already sends — a ship whose cells share a row runs across, one
/// whose cells share a column runs down. The secrecy rule is untouched for the same reason:
/// on the opponent's board only <c>SunkShipDto</c> carries cells, so an afloat opposing ship
/// has nothing to derive a hull from and simply is not drawn.</para>
/// </summary>
public sealed record ShipOutline(
    string Name,
    Coordinate Origin,
    bool Vertical,
    int Size,
    bool IsSunk,
    IReadOnlyList<CellDto> Cells)
{
    /// <summary>
    /// Reads a hull off the cells of a ship. Returns <c>null</c> for anything it cannot make
    /// a hull of — no cells at all, or cells that are neither a row nor a column. That is not
    /// expected from this server, and drawing a wrong hull would be worse than drawing none.
    /// </summary>
    public static ShipOutline? FromCells(string name, IReadOnlyList<CellDto> cells, bool isSunk)
    {
        if (cells.Count == 0)
            return null;

        var sameRow = cells.All(c => c.Y == cells[0].Y);
        var sameColumn = cells.All(c => c.X == cells[0].X);

        // A single cell is both; treating it as horizontal keeps the drawing code from
        // needing a third case.
        if (!sameRow && !sameColumn)
            return null;

        var vertical = !sameRow && sameColumn;
        var origin = new Coordinate(cells.Min(c => c.X), cells.Min(c => c.Y));

        return new ShipOutline(name, origin, vertical, cells.Count, isSunk, cells);
    }

    /// <summary>The cells that carry a visible scar, with the state that decides which one.</summary>
    public IEnumerable<(Coordinate At, string State)> Damage =>
        Cells.Where(c => c.State is "hit" or "sunk")
             .Select(c => (new Coordinate(c.X, c.Y), c.State));

    /// <summary>Every cell this hull covers, so the board knows not to paint under it.</summary>
    public IEnumerable<Coordinate> Footprint =>
        Cells.Select(c => new Coordinate(c.X, c.Y));
}
