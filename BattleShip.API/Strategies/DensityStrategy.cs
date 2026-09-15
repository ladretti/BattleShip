using BattleShip.Models;

namespace BattleShip.API.Strategies;

/// <summary>
/// Hard level opponent: for each ship still to be sunk, enumerates every
/// placement still legal given what is known, counts how many placements go
/// through each cell, then aims at the cell with the highest score. No
/// neighborhood heuristic: the concentration of legal placements alone makes the
/// strategy converge on the ships already hit.
///
/// Stateless: rebuilds its decision from ShotHistory on every call, just like
/// RandomStrategy and HuntTargetStrategy. Randomness arrives through the
/// constructor, never through Random.Shared, and only serves to break ties
/// between cells with equal scores.
///
/// Every enumeration is bounded by remaining ships x 2 orientations x
/// GridSize x GridSize origins: no loop runs "until it finds something".
/// </summary>
public sealed class DensityStrategy(Random random) : IOpponentStrategy
{
    private const int UnsunkHitWeight = 10;

    public string Name => "Density";

    public Coordinate NextShot(ShotHistory history)
    {
        var alreadyShot = history.Shots.Select(s => s.At).ToHashSet();
        var missed = history.Shots
            .Where(s => s.Result == ShotResult.Miss)
            .Select(s => s.At)
            .ToHashSet();
        var sunkCells = history.SunkShips.SelectMany(s => s.Cells).ToHashSet();
        var unsunkHits = history.Shots
            .Where(s => s.Result is ShotResult.Hit or ShotResult.Sunk)
            .Select(s => s.At)
            .Where(c => !sunkCells.Contains(c))
            .ToHashSet();

        var blockedHalo = history.ShipsMayTouch
            ? new HashSet<Coordinate>()
            : HaloOf(history.SunkShips.SelectMany(s => s.Cells), history.GridSize);

        var scores = new Dictionary<Coordinate, int>();

        foreach (var ship in history.RemainingShips)
        {
            foreach (var placement in PossiblePlacements(ship.Size, history.GridSize))
            {
                if (!IsLegal(placement, missed, sunkCells, blockedHalo))
                    continue;

                var weight = placement.Any(unsunkHits.Contains) ? UnsunkHitWeight : 1;
                foreach (var cell in placement)
                {
                    scores[cell] = scores.GetValueOrDefault(cell) + weight;
                }
            }
        }

        var positiveCandidates = scores
            .Where(kv => !alreadyShot.Contains(kv.Key) && kv.Value > 0)
            .ToList();

        if (positiveCandidates.Count > 0)
        {
            var best = positiveCandidates.Max(kv => kv.Value);

            // Explicit sort: the enumeration order of a Dictionary<,> is not
            // guaranteed by the .NET specification (only stable in practice as long
            // as no removal takes place). Without this sort, the random draw over
            // `bestCells` would rest on a non-contractual order: for the same seed,
            // a runtime change could make random.Next() point at another cell,
            // silently breaking the reproducibility that task 11 requires (the
            // legality invariant would keep passing without detecting it).
            var bestCells = positiveCandidates
                .Where(kv => kv.Value == best)
                .Select(kv => kv.Key)
                .OrderBy(c => c.Y).ThenBy(c => c.X)
                .ToList();
            return bestCells[random.Next(bestCells.Count)];
        }

        return FallbackShot(history.GridSize, alreadyShot);
    }

    private static bool IsLegal(
        IReadOnlyList<Coordinate> placement,
        HashSet<Coordinate> missed,
        HashSet<Coordinate> sunkCells,
        HashSet<Coordinate> blockedHalo)
    {
        foreach (var cell in placement)
        {
            if (missed.Contains(cell))
                return false;
            if (sunkCells.Contains(cell))
                return false;
            if (blockedHalo.Contains(cell))
                return false;
        }

        return true;
    }

    private static IEnumerable<IReadOnlyList<Coordinate>> PossiblePlacements(int size, int gridSize)
    {
        for (var x = 0; x < gridSize; x++)
        {
            for (var y = 0; y < gridSize; y++)
            {
                // Horizontal.
                if (x + size <= gridSize)
                {
                    yield return Enumerable.Range(0, size)
                        .Select(i => new Coordinate(x + i, y))
                        .ToList();
                }

                // Vertical. A ship of size 1 would produce the same placement as the
                // horizontal one: skipping it would change nothing to the score (same
                // cell, same weight) but avoids counting it twice.
                if (size > 1 && y + size <= gridSize)
                {
                    yield return Enumerable.Range(0, size)
                        .Select(i => new Coordinate(x, y + i))
                        .ToList();
                }
            }
        }
    }

    private static HashSet<Coordinate> HaloOf(IEnumerable<Coordinate> cells, int gridSize)
    {
        var halo = new HashSet<Coordinate>();
        foreach (var cell in cells)
        {
            for (var dx = -1; dx <= 1; dx++)
            {
                for (var dy = -1; dy <= 1; dy++)
                {
                    var neighbor = new Coordinate(cell.X + dx, cell.Y + dy);
                    if (neighbor.X < 0 || neighbor.X >= gridSize || neighbor.Y < 0 || neighbor.Y >= gridSize)
                        continue;
                    halo.Add(neighbor);
                }
            }
        }

        return halo;
    }

    private Coordinate FallbackShot(int gridSize, HashSet<Coordinate> alreadyShot)
    {
        var remaining = new List<Coordinate>();
        for (var x = 0; x < gridSize; x++)
        {
            for (var y = 0; y < gridSize; y++)
            {
                var candidate = new Coordinate(x, y);
                if (!alreadyShot.Contains(candidate))
                    remaining.Add(candidate);
            }
        }

        return remaining[random.Next(remaining.Count)];
    }
}
