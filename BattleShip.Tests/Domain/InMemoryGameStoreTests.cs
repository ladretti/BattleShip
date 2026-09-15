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

    /// <summary>
    /// Direct store-level replacement for an earlier HTTP/gRPC-level version of this race
    /// (task 14): that version cost 4-6 s per run and, despite instrumented proof that
    /// <c>Game.Fire</c>'s <c>_history.Add</c> and <c>DtoMappings</c>'s
    /// <c>history.Where(...).Select(...).ToList()</c> genuinely overlapped in wall-clock
    /// time under load, never observed the corruption it was meant to catch — the real
    /// gRPC/HTTP pipeline on both sides evidently dilutes the attempt rate far below what
    /// the isolated mechanism needs (confirmed separately, outside this codebase, to
    /// reproduce reliably: see the task 14 report). A test that costs seconds and proves
    /// nothing is worse than no test — it manufactures false confidence — so it is dropped
    /// in favor of this one, which calls <see cref="IGameStore.Mutate{T}"/> and
    /// <see cref="IGameStore.Read{T}"/> directly: no network, no serialization, nothing
    /// standing between the write and the read but the lock under test. This version DOES
    /// have proven discriminant power (see below) and costs well under half a second.
    ///
    /// <see cref="Game.PlayerFires"/> only succeeds on <see cref="Player.Human"/>'s turn,
    /// and a shot always hands the turn to the other side — a fixed target coordinate would
    /// make every fire thread but the first fail with <see cref="GameError.NotYourTurn"/>,
    /// collapsing the write volume this test depends on (exactly what sank an earlier,
    /// discarded attempt at this same test — see the task 14 report). Firing as whichever
    /// player <see cref="Game.CurrentPlayer"/> actually is, decided INSIDE the
    /// <see cref="IGameStore.Mutate{T}"/> lambda (so under the same lock that resolves the
    /// race for real), keeps every fire legal. Both boards' fleets are built on a 100x100
    /// grid and every fired coordinate is pre-filtered to avoid EITHER fleet's cells, so
    /// every shot is a genuine miss: the game never reaches
    /// <see cref="GameStatus.Finished"/> and the turn keeps alternating for the whole
    /// volume, regardless of which thread's <c>Mutate</c> call happens to run when.
    ///
    /// The read side enumerates exactly what <c>DtoMappings.ToOwnBoardDto</c>/
    /// <c>ToOpponentBoardDto</c> enumerate in production — <c>Game.History</c> filtered by
    /// shooter, twice — plus both boards' <c>ReceivedShots</c> (a <c>HashSet</c>, mutated by
    /// the very same <c>Board.Fire</c> call): four live collections, all still being
    /// appended to while read threads spin.
    ///
    /// <b>Discriminant check, done and reproducible (task 14 report has the full log):</b>
    /// with <see cref="IGameStore.Read{T}"/>'s lock temporarily removed, this test failed
    /// 5/5 runs, each with several <c>ArgumentException: Destination array is not long
    /// enough to copy all the items in the collection</c> (occasionally a
    /// <c>NullReferenceException</c> alongside it) — both symptoms of
    /// <c>HashSet&lt;Coordinate&gt;.ToList()</c> racing a concurrent
    /// <c>_receivedShots.Add</c> in <c>Board.Fire</c>. This is NOT the
    /// <c>InvalidOperationException: "Collection was modified"</c> a <c>List&lt;T&gt;</c>
    /// enumerator gives: at this same volume, the <c>History</c>-only half of the
    /// projection (dropping the two <c>ReceivedShots</c> lines) stayed green 3/3 runs even
    /// with the lock removed, and needed a materially larger volume to be worth retrying —
    /// not attempted further once the combined projection above already gave a clean,
    /// fast, reproducible failure. <see cref="IGameStore"/>'s own doc comment names both
    /// collections as at risk; this test exercises both, and it is the <c>HashSet</c> side
    /// that turned out to break first.
    /// </summary>
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

        // Same discipline as Only_one_concurrent_shot_on_the_same_cell_succeeds: dedicated
        // Threads, not Task.Run (REVUE-IA.md, Revue 4), and a Barrier whose participant
        // count matches the thread count exactly — otherwise SignalAndWait blocks forever
        // instead of failing the test.
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

                        // No assertion on the exact count: it is read concurrently with
                        // writes and legitimately differs between two calls. A consistent
                        // snapshot here means only "the projection completed without
                        // throwing and the game was found" — result.IsOk.
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
