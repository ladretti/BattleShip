using BattleShip.API.Strategies;
using BattleShip.Models;

namespace BattleShip.Tests.Opponent;

public sealed class StrategyInvariantTests
{
    public static TheoryData<string, Func<Random, IOpponentStrategy>> Strategies() => new()
    {
        { "Random", r => new RandomStrategy(r) },
        { "HuntTarget", r => new HuntTargetStrategy(r) },
        { "Density", r => new DensityStrategy(r) },
    };

    [Theory]
    [MemberData(nameof(Strategies))]
    public void A_strategy_never_proposes_an_invalid_shot(
        string name, Func<Random, IOpponentStrategy> factory)
    {
        var rules = GameRules.Default;

        for (var seed = 1; seed <= 20; seed++)
        {
            var strategy = factory(new Random(seed));
            var opponentBoard = new Board(rules.GridSize,
                new FleetPlacer(new Random(seed * 31)).PlaceAll(rules).Value);

            var alreadyShot = new HashSet<Coordinate>();
            var shots = new List<ShotRecord>();

            var maxShots = rules.GridSize * rules.GridSize;
            var shotsPlayed = 0;

            while (!opponentBoard.AllSunk)
            {
                Assert.True(++shotsPlayed <= maxShots,
                    $"{name} (seed {seed}) did not sink the fleet in {maxShots} shots: " +
                    "the strategy makes no more progress.");

                var history = HistoryFrom(rules, opponentBoard, shots);
                var shot = strategy.NextShot(history);

                Assert.InRange(shot.X, 0, rules.GridSize - 1);
                Assert.InRange(shot.Y, 0, rules.GridSize - 1);
                Assert.True(alreadyShot.Add(shot),
                    $"{name} (seed {seed}) replayed cell {shot}");

                var fired = opponentBoard.Fire(shot, Player.Opponent);
                Assert.True(fired.IsOk);
                shots.Add(fired.Value);
            }

            Assert.True(alreadyShot.Count <= rules.GridSize * rules.GridSize);
        }
    }

    internal static ShotHistory HistoryFrom(
        GameRules rules, Board opponentBoard, IReadOnlyList<ShotRecord> shots)
    {
        var sunk = opponentBoard.Ships.Where(s => s.IsSunk).ToList();
        var remaining = rules.Fleet
            .Where(t => sunk.All(s => s.Name != t.Name))
            .ToList();

        return new ShotHistory(rules.GridSize, shots, remaining, sunk, rules.ShipsMayTouch);
    }

    [Theory]
    [MemberData(nameof(Strategies))]
    public void A_strategy_replays_the_same_game_for_an_equal_seed(
        string name, Func<Random, IOpponentStrategy> factory)
    {
        var rules = GameRules.Default;

        for (var seed = 1; seed <= 5; seed++)
        {
            var first = ShotsPlayed(rules, factory(new Random(seed)), seed);
            var second = ShotsPlayed(rules, factory(new Random(seed)), seed);

            Assert.Equal(first, second);
            Assert.NotEmpty(first);
        }

        _ = name;
    }

    private static List<Coordinate> ShotsPlayed(
        GameRules rules, IOpponentStrategy strategy, int seed)
    {
        var opponentBoard = new Board(rules.GridSize,
            new FleetPlacer(new Random(seed * 31)).PlaceAll(rules).Value);
        var shots = new List<ShotRecord>();
        var shotsPlayed = new List<Coordinate>();

        var maxShots = rules.GridSize * rules.GridSize;
        while (!opponentBoard.AllSunk)
        {
            Assert.True(shotsPlayed.Count < maxShots, $"game not finished in {maxShots} shots");

            var shot = strategy.NextShot(HistoryFrom(rules, opponentBoard, shots));
            shotsPlayed.Add(shot);
            shots.Add(opponentBoard.Fire(shot, Player.Opponent).Value);
        }

        return shotsPlayed;
    }
}
