namespace BattleShip.Models;

public static class PlacementRules
{
    private static readonly (int Dx, int Dy)[] MooreOffsets =
    [
        (-1, -1), (0, -1), (1, -1),
        (-1, 0), (1, 0),
        (-1, 1), (0, 1), (1, 1)
    ];

    public static Result<IReadOnlyList<Ship>> Validate(IReadOnlyList<ShipPlacement> placements, GameRules rules)
    {
        if (!FleetMatches(placements, rules.Fleet))
            return Result<IReadOnlyList<Ship>>.Fail(GameError.InvalidPlacement);

        var placementCells = placements
            .Select(p => (Placement: p, Cells: p.Cells().ToList()))
            .ToList();

        if (placementCells.Any(pc => pc.Cells.Any(c => IsOutOfBounds(c, rules.GridSize))))
            return Result<IReadOnlyList<Ship>>.Fail(GameError.InvalidPlacement);

        var allCells = placementCells.SelectMany(pc => pc.Cells).ToList();
        if (allCells.Count != allCells.Distinct().Count())
            return Result<IReadOnlyList<Ship>>.Fail(GameError.InvalidPlacement);

        if (!rules.ShipsMayTouch && HasAdjacentShips(placementCells))
            return Result<IReadOnlyList<Ship>>.Fail(GameError.InvalidPlacement);

        IReadOnlyList<Ship> ships = placementCells
            .Select(pc => new Ship(pc.Placement.Name, pc.Placement.Size, pc.Cells))
            .ToList();

        return Result<IReadOnlyList<Ship>>.Ok(ships);
    }

    private static bool FleetMatches(IReadOnlyList<ShipPlacement> placements, IReadOnlyList<ShipTemplate> fleet)
    {
        var expected = fleet
            .Select(t => (t.Name, t.Size))
            .OrderBy(t => t.Name, StringComparer.Ordinal)
            .ThenBy(t => t.Size)
            .ToList();

        var actual = placements
            .Select(p => (p.Name, p.Size))
            .OrderBy(p => p.Name, StringComparer.Ordinal)
            .ThenBy(p => p.Size)
            .ToList();

        return expected.SequenceEqual(actual);
    }

    private static bool IsOutOfBounds(Coordinate cell, int gridSize) =>
        cell.X < 0 || cell.X >= gridSize || cell.Y < 0 || cell.Y >= gridSize;

    private static bool HasAdjacentShips(List<(ShipPlacement Placement, List<Coordinate> Cells)> placementCells)
    {
        for (var i = 0; i < placementCells.Count; i++)
        {
            for (var j = i + 1; j < placementCells.Count; j++)
            {
                if (AreAdjacent(placementCells[i].Cells, placementCells[j].Cells))
                    return true;
            }
        }

        return false;
    }

    private static bool AreAdjacent(List<Coordinate> a, List<Coordinate> b)
    {
        var bSet = b.ToHashSet();

        foreach (var cell in a)
        {
            foreach (var (dx, dy) in MooreOffsets)
            {
                if (bSet.Contains(new Coordinate(cell.X + dx, cell.Y + dy)))
                    return true;
            }
        }

        return false;
    }
}
