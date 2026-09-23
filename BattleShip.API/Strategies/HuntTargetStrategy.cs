using BattleShip.Models;

namespace BattleShip.API.Strategies;

public sealed class HuntTargetStrategy(Random random) : IOpponentStrategy
{
    public string Name => "HuntTarget";

    public Coordinate NextShot(ShotHistory history)
    {
        var alreadyShot = history.Shots.Select(s => s.At).ToHashSet();
        var sunkCells = history.SunkShips.SelectMany(s => s.Cells).ToHashSet();

        var hits = history.Shots
            .Where(s => s.Result is ShotResult.Hit or ShotResult.Sunk)
            .Select(s => s.At)
            .Where(c => !sunkCells.Contains(c))
            .ToList();

        if (hits.Count > 0)
        {
            var candidates = TargetCandidates(history.GridSize, hits, alreadyShot);
            if (candidates.Count > 0)
                return candidates[random.Next(candidates.Count)];
        }

        return HuntShot(history.GridSize, alreadyShot);
    }

    private Coordinate HuntShot(int gridSize, HashSet<Coordinate> alreadyShot)
    {
        var evenParity = new List<Coordinate>();
        var all = new List<Coordinate>();

        for (var x = 0; x < gridSize; x++)
        {
            for (var y = 0; y < gridSize; y++)
            {
                var candidate = new Coordinate(x, y);
                if (alreadyShot.Contains(candidate))
                    continue;

                all.Add(candidate);
                if ((x + y) % 2 == 0)
                    evenParity.Add(candidate);
            }
        }

        var pool = evenParity.Count > 0 ? evenParity : all;
        return pool[random.Next(pool.Count)];
    }

    private static IReadOnlyList<Coordinate> TargetCandidates(
        int gridSize, IReadOnlyList<Coordinate> hits, HashSet<Coordinate> alreadyShot)
    {
        var lineExtensions = new List<Coordinate>();
        for (var i = 0; i < hits.Count; i++)
        {
            for (var j = i + 1; j < hits.Count; j++)
            {
                AddLineExtensions(hits[i], hits[j], gridSize, alreadyShot, lineExtensions);
            }
        }

        if (lineExtensions.Count > 0)
            return lineExtensions;

        var neighbors = new List<Coordinate>();
        foreach (var hit in hits)
        {
            foreach (var neighbor in Neighbors(hit, gridSize))
            {
                if (!alreadyShot.Contains(neighbor) && !neighbors.Contains(neighbor))
                    neighbors.Add(neighbor);
            }
        }

        return neighbors;
    }

    private static void AddLineExtensions(
        Coordinate a, Coordinate b, int gridSize, HashSet<Coordinate> alreadyShot, List<Coordinate> result)
    {
        if (a.X == b.X && Math.Abs(a.Y - b.Y) == 1)
        {
            AddIfValid(new Coordinate(a.X, Math.Min(a.Y, b.Y) - 1), gridSize, alreadyShot, result);
            AddIfValid(new Coordinate(a.X, Math.Max(a.Y, b.Y) + 1), gridSize, alreadyShot, result);
        }
        else if (a.Y == b.Y && Math.Abs(a.X - b.X) == 1)
        {
            AddIfValid(new Coordinate(Math.Min(a.X, b.X) - 1, a.Y), gridSize, alreadyShot, result);
            AddIfValid(new Coordinate(Math.Max(a.X, b.X) + 1, a.Y), gridSize, alreadyShot, result);
        }
    }

    private static void AddIfValid(
        Coordinate candidate, int gridSize, HashSet<Coordinate> alreadyShot, List<Coordinate> result)
    {
        if (candidate.X < 0 || candidate.X >= gridSize || candidate.Y < 0 || candidate.Y >= gridSize)
            return;
        if (alreadyShot.Contains(candidate))
            return;
        if (!result.Contains(candidate))
            result.Add(candidate);
    }

    private static IEnumerable<Coordinate> Neighbors(Coordinate c, int gridSize)
    {
        if (c.X > 0) yield return new Coordinate(c.X - 1, c.Y);
        if (c.X < gridSize - 1) yield return new Coordinate(c.X + 1, c.Y);
        if (c.Y > 0) yield return new Coordinate(c.X, c.Y - 1);
        if (c.Y < gridSize - 1) yield return new Coordinate(c.X, c.Y + 1);
    }
}
