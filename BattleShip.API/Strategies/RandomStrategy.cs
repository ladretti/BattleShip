using BattleShip.Models;

namespace BattleShip.API.Strategies;

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
