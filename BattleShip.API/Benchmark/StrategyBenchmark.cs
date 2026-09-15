using BattleShip.API.Strategies;
using BattleShip.Models;

namespace BattleShip.API.Benchmark;

/// <summary>
/// Input of the AI duel: number of games per strategy and starting seed.
/// </summary>
public sealed record BenchmarkInput(int Games, int Seed);

/// <summary>
/// Aggregated result of a strategy over <see cref="Games"/> games: average number of
/// shots until the fleet is sunk, along with the extremes observed.
/// </summary>
public sealed record BenchmarkResult(
    string StrategyName, int Games, double AverageShots, int MinShots, int MaxShots);

/// <summary>
/// AI duel: measures the number of shots each opponent level needs to sink a fleet,
/// over a given number of games with a fixed seed (REVUE-IA.md, Revue 3).
///
/// Deliberately reproduces the <see cref="ShotHistory"/> building logic of
/// StrategyInvariantTests.HistoryFrom rather than making production code depend on a
/// test file — see the task 11 report.
///
/// Reproducibility discipline: no source of randomness is hard-coded
/// (never Random.Shared) and no unordered structure (HashSet, dictionary...) is
/// enumerated in the measurement loop. For game index i, the fleet placement of the
/// three strategies is derived from the same seed (seed + i): otherwise the comparison
/// would measure the luck of the placement rather than the level of the strategy. The
/// seed of the strategy itself is derived separately (seed + i + salt) so that the
/// strategy does not replay exactly the same sequence of numbers as the placement,
/// without this being a correctness requirement — only determinism matters here.
/// </summary>
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
