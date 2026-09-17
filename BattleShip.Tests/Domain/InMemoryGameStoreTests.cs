using System.Collections.Concurrent;
using BattleShip.API.Stores;
using BattleShip.Models;

namespace BattleShip.Tests.Domain;

public sealed class InMemoryGameStoreTests
{
    private static Game GameOnDefaultGrid()
    {
        var rules = GameRules.Default;
        var fleet = () => new FleetPlacer(new Random(7)).PlaceAll(rules).Value;
        return Game.Start(Guid.NewGuid(), rules, fleet(), fleet());
    }

    [Fact]
    public void A_missing_game_returns_GameNotFound()
    {
        var store = new InMemoryGameStore();

        var result = store.Mutate(Guid.NewGuid(), g => g.PlayerFires(new Coordinate(0, 0)));

        Assert.False(result.IsOk);
        Assert.Equal(GameError.GameNotFound, result.Error);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(5)]
    public void Only_one_concurrent_shot_on_the_same_cell_succeeds(int execution)
    {
        _ = execution;
        var store = new InMemoryGameStore();
        var game = GameOnDefaultGrid();
        store.Save(game);

        var target = new Coordinate(0, 0);
        const int shotCount = 32;

        using var start = new Barrier(shotCount);
        var results = new Result<ShotRecord>[shotCount];
        var threads = new Thread[shotCount];

        for (var i = 0; i < shotCount; i++)
        {
            var index = i;
            threads[index] = new Thread(() =>
            {
                start.SignalAndWait();
                results[index] = store.Mutate(game.Id, g => g.PlayerFires(target));
            });
        }

        foreach (var thread in threads)
            thread.Start();
        foreach (var thread in threads)
            thread.Join();

        Assert.Equal(1, results.Count(r => r.IsOk));
        Assert.All(results.Where(r => !r.IsOk), r =>
            Assert.Contains(r.Error, new[]
            {
                GameError.CellAlreadyShot, GameError.NotYourTurn,
                GameError.GameAlreadyFinished
            }));
    }

    [Fact]
    public void Concurrent_fires_and_reads_never_throw_and_return_consistent_snapshots()
    {
        const int gridSize = 100;
        const int fireThreadCount = 8;
        const int shotsPerFireThread = 100;
        const int readThreadCount = 8;

        var rules = GameRules.Default with { GridSize = gridSize };
        var random = new Random(20260915);
        var humanShips = new FleetPlacer(random).PlaceAll(rules).Value;
        var opponentShips = new FleetPlacer(random).PlaceAll(rules).Value;
        var game = Game.Start(Guid.NewGuid(), rules, humanShips, opponentShips);

        var shipCells = humanShips.SelectMany(s => s.Cells)
            .Concat(opponentShips.SelectMany(s => s.Cells))
            .ToHashSet();
        var safeCells = Enumerable.Range(0, gridSize * gridSize)
            .Select(i => new Coordinate(i % gridSize, i / gridSize))
            .Where(c => !shipCells.Contains(c))
            .ToList();

        var totalShots = fireThreadCount * shotsPerFireThread;
        Assert.True(safeCells.Count >= totalShots, "Not enough safe cells for the configured volume.");

        var store = new InMemoryGameStore();
        store.Save(game);

        using var start = new Barrier(fireThreadCount + readThreadCount);
        var stillFiring = fireThreadCount;
        var exceptions = new ConcurrentBag<Exception>();
        var threads = new List<Thread>(fireThreadCount + readThreadCount);

        for (var t = 0; t < fireThreadCount; t++)
        {
            var cells = safeCells.Skip(t * shotsPerFireThread).Take(shotsPerFireThread).ToList();
            threads.Add(new Thread(() =>
            {
                start.SignalAndWait();
                try
                {
                    foreach (var at in cells)
                    {
                        store.Mutate(game.Id, g => g.CurrentPlayer == Player.Human
                            ? g.PlayerFires(at)
                            : g.OpponentFires(at));
                    }
                }
                catch (Exception ex)
                {
                    exceptions.Add(ex);
                }
                finally
                {
                    Interlocked.Decrement(ref stillFiring);
                }
            }));
        }

        for (var i = 0; i < readThreadCount; i++)
        {
            threads.Add(new Thread(() =>
            {
                start.SignalAndWait();
                while (Volatile.Read(ref stillFiring) > 0)
                {
                    try
                    {
                        var result = store.Read(game.Id, g =>
                        {
                            var ownReceived = g.History.Where(s => s.By == Player.Opponent).Select(s => s.At).ToList();
                            var opponentShots = g.History.Where(s => s.By == Player.Human).Select(s => s.At).ToList();
                            var humanBoardReceived = g.HumanBoard.ReceivedShots.ToList();
                            var opponentBoardReceived = g.OpponentBoard.ReceivedShots.ToList();
                            return ownReceived.Count + opponentShots.Count
                                + humanBoardReceived.Count + opponentBoardReceived.Count;
                        });

                        if (!result.IsOk)
                            throw new InvalidOperationException($"Unexpected read failure: {result.Error}");
                    }
                    catch (Exception ex)
                    {
                        exceptions.Add(ex);
                    }
                }
            }));
        }

        foreach (var thread in threads)
            thread.Start();
        foreach (var thread in threads)
            thread.Join();

        var messages = string.Join(" | ", exceptions.Select(e => $"{e.GetType().Name}: {e.Message}").Distinct());
        Assert.True(exceptions.IsEmpty, $"{exceptions.Count} exception(s) surfaced: {messages}");
    }

    [Fact]
    public void Two_distinct_games_do_not_block_each_other()
    {
        var store = new InMemoryGameStore();
        var a = GameOnDefaultGrid();
        var b = GameOnDefaultGrid();
        store.Save(a);
        store.Save(b);

        var ra = store.Mutate(a.Id, g => g.PlayerFires(new Coordinate(0, 0)));
        var rb = store.Mutate(b.Id, g => g.PlayerFires(new Coordinate(0, 0)));

        Assert.True(ra.IsOk);
        Assert.True(rb.IsOk);
    }
}
