using BattleShip.Models;

namespace BattleShip.API.Strategies;

/// <summary>
/// Simplest opponent: shoots at random among the cells that have never been shot at.
/// Randomness arrives through the constructor, never through a hard-coded Random.Shared,
/// so that it stays reproducible under test (see FleetPlacer for the same discipline).
/// </summary>
public sealed class RandomStrategy(Random random) : IOpponentStrategy
{
    public string Name => "Random";

    public Coordinate NextShot(ShotHistory history)
    {
        var alreadyShot = history.Shots.Select(s => s.At).ToHashSet();

        var remaining = new List<Coordinate>();
        for (var x = 0; x < history.GridSize; x++)
        {
            for (var y = 0; y < history.GridSize; y++)
            {
                var candidate = new Coordinate(x, y);
                if (!alreadyShot.Contains(candidate))
                    remaining.Add(candidate);
            }
        }

        return remaining[random.Next(remaining.Count)];
    }
}
