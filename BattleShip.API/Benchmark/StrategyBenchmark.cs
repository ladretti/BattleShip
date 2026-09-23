using BattleShip.API.Strategies;
using BattleShip.Models;

namespace BattleShip.API.Benchmark;

public sealed record BenchmarkInput(int Games, int Seed);

public sealed record BenchmarkResult(
    string StrategyName, int Games, double AverageShots, int MinShots, int MaxShots);

public static class StrategyBenchmark
{
    private const int StrategyRandomSalt = 104_729;

    private static readonly IReadOnlyList<(string Name, Func<Random, IOpponentStrategy> Create)> Strategies =
    [
        ("Random", r => new RandomStrategy(r)),
        ("HuntTarget", r => new HuntTargetStrategy(r)),
        ("Density", r => new DensityStrategy(r)),
    ];

    public static IReadOnlyList<BenchmarkResult> Run(GameRules rules, int games, int seed)
    {
        var results = new List<BenchmarkResult>(Strategies.Count);

        foreach (var (name, create) in Strategies)
        {
            var shotsPerGame = new int[games];

            for (var i = 0; i < games; i++)
            {
                var placementSeed = unchecked(seed + i);
                var fleet = new FleetPlacer(new Random(placementSeed)).PlaceAll(rules).Value;
                var opponentBoard = new Board(rules.GridSize, fleet);
                var strategy = create(new Random(unchecked(placementSeed + StrategyRandomSalt)));

                shotsPerGame[i] = PlayGame(rules, opponentBoard, strategy, name, i);
            }

            results.Add(new BenchmarkResult(
                StrategyName: name,
                Games: games,
                AverageShots: shotsPerGame.Average(),
                MinShots: shotsPerGame.Min(),
                MaxShots: shotsPerGame.Max()));
        }

        return results;
    }

    private static int PlayGame(
        GameRules rules, Board opponentBoard, IOpponentStrategy strategy, string strategyName, int gameIndex)
    {
        var shots = new List<ShotRecord>();
        var maxShots = rules.GridSize * rules.GridSize;

        while (!opponentBoard.AllSunk)
        {
            if (shots.Count >= maxShots)
            {
                throw new InvalidOperationException(
                    $"{strategyName} (game {gameIndex}) did not sink the fleet in " +
                    $"{maxShots} shots: the strategy is no longer making progress.");
            }

            var history = HistoryFrom(rules, opponentBoard, shots);
            var shot = strategy.NextShot(history);
            var fired = opponentBoard.Fire(shot, Player.Opponent);

            if (!fired.IsOk)
            {
                throw new InvalidOperationException(
                    $"{strategyName} (game {gameIndex}) proposed an invalid shot " +
                    $"({shot.X}, {shot.Y}): {fired.Error}.");
            }

            shots.Add(fired.Value);
        }

        return shots.Count;
    }

    private static ShotHistory HistoryFrom(
        GameRules rules, Board opponentBoard, IReadOnlyList<ShotRecord> shots)
    {
        var sunk = opponentBoard.Ships.Where(s => s.IsSunk).ToList();
        var remaining = rules.Fleet
            .Where(t => sunk.All(s => s.Name != t.Name))
            .ToList();

        return new ShotHistory(rules.GridSize, shots, remaining, sunk, rules.ShipsMayTouch);
    }
}
