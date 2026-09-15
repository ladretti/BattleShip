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
        _ = execution;   // the case is replayed: a race that passes once proves nothing
        var store = new InMemoryGameStore();
        var game = GameOnDefaultGrid();
        store.Save(game);

        var target = new Coordinate(0, 0);
        const int shotCount = 32;
        // Task.Run goes through the thread pool, which only injects new threads
        // sparingly (about one every 500 ms beyond the initial threshold): a Barrier
        // placed on top of it was measured at 15-18 s per execution for an unchanged
        // detection rate (see REVUE-IA.md, Revue 4). Dedicated Threads start
        // immediately, without that progressive injection: the barrier releases within
        // a few milliseconds and the 32 shots really do enter Mutate together. The
        // number of participants MUST match the number of threads exactly: otherwise
        // SignalAndWait blocks indefinitely instead of failing the test.
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
