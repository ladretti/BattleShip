using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Json;
using BattleShip.API.Contracts;
using BattleShip.API.Grpc;
using BattleShip.Models;
using Grpc.Core;
using Grpc.Net.Client;
using Grpc.Net.Client.Web;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace BattleShip.Tests.Api;

public sealed class FireGrpcTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public FireGrpcTests(WebApplicationFactory<Program> factory) => _factory = factory;

    private BattleService.BattleServiceClient Client()
    {
        var handler = new GrpcWebHandler(_factory.Server.CreateHandler());
        return new BattleService.BattleServiceClient(
            GrpcChannel.ForAddress(_factory.Server.BaseAddress,
                new GrpcChannelOptions { HttpHandler = handler }));
    }

    private async Task<Guid> ReadyGame()
    {
        var http = _factory.CreateClient();
        var create = await http.PostAsJsonAsync("/games", new CreateGameInput(10, "Easy"));
        var game = await create.Content.ReadFromJsonAsync<GameDto>();

        var placement = new PlacementInput(
        [
            new ShipPlacementInput("Carrier", 0, 0, "Horizontal"),
            new ShipPlacementInput("Battleship", 0, 2, "Horizontal"),
            new ShipPlacementInput("Cruiser", 0, 4, "Horizontal"),
            new ShipPlacementInput("Submarine", 0, 6, "Horizontal"),
            new ShipPlacementInput("Destroyer", 0, 8, "Horizontal")
        ]);
        await http.PostAsJsonAsync($"/games/{game!.Id}/placement", placement);
        return game.Id;
    }

    [Fact]
    public async Task A_valid_shot_returns_a_result()
    {
        var id = await ReadyGame();

        var reply = await Client().FireAsync(new FireRequest
        {
            GameId = id.ToString(),
            X = 5,
            Y = 5
        });

        Assert.Contains(reply.PlayerShot.Result, new[] { "miss", "hit", "sunk" });
    }

    [Fact]
    public async Task Replaying_the_same_cell_returns_InvalidArgument()
    {
        var id = await ReadyGame();
        var client = Client();
        var first = await client.FireAsync(new FireRequest
        {
            GameId = id.ToString(),
            X = 5,
            Y = 5
        });

        // If the first shot was a hit, the player fires again: the cell is still rejected.
        var ex = await Assert.ThrowsAsync<RpcException>(() =>
            client.FireAsync(new FireRequest
            {
                GameId = id.ToString(),
                X = 5,
                Y = 5
            }).ResponseAsync);

        Assert.Equal(StatusCode.InvalidArgument, ex.StatusCode);
        _ = first;
    }

    [Fact]
    public async Task A_shot_outside_the_grid_returns_InvalidArgument()
    {
        var id = await ReadyGame();

        var ex = await Assert.ThrowsAsync<RpcException>(() =>
            Client().FireAsync(new FireRequest
            {
                GameId = id.ToString(),
                X = 99,
                Y = 0
            }).ResponseAsync);

        Assert.Equal(StatusCode.InvalidArgument, ex.StatusCode);
    }

    [Fact]
    public async Task A_shot_on_an_unknown_game_returns_NotFound()
    {
        var ex = await Assert.ThrowsAsync<RpcException>(() =>
            Client().FireAsync(new FireRequest
            {
                GameId = Guid.NewGuid().ToString(),
                X = 0,
                Y = 0
            }).ResponseAsync);

        Assert.Equal(StatusCode.NotFound, ex.StatusCode);
    }

    [Fact]
    public async Task A_missed_shot_triggers_the_opponent_s_counterattack()
    {
        var id = await ReadyGame();
        var client = Client();

        FireResponse reply;
        var x = 5;
        do
        {
            reply = await client.FireAsync(new FireRequest
            {
                GameId = id.ToString(),
                X = x++,
                Y = 5
            });
        } while (reply.PlayerShot.Result != "miss" && x < 10);

        Assert.NotEmpty(reply.OpponentShots);
    }

    /// <summary>
    /// Additional discriminant check (task 14, not part of the 5 tests given by the
    /// brief): the first occasion in the project where a real mutation (gRPC
    /// <c>Fire</c>, through <see cref="IGameStore.Mutate{T}"/>) can race against a
    /// concurrent read that enumerates the same game's state (HTTP
    /// <c>GET /games/{id}</c>, through <see cref="IGameStore.Read{T}"/>). Task 13 left
    /// this unverified for lack of any concurrent mutation at the time.
    ///
    /// <b>Known limitation, reported per CLAUDE.md §6 rather than hidden:</b> several
    /// escalating designs of this test (kept only in the task 14 report, not here — a
    /// ~100-shot/32-writer version, this same design with a 60x60 then a 300x300 board,
    /// a 2000-writer stampede, and a GC-latency-tuned variant) were run against the real
    /// <c>Fire</c>/<c>GET</c> façades with the GET handler deliberately reverted to
    /// <c>Find</c>. Every one of them passed — including this exact version — DESPITE
    /// instrumented proof (a throwaway build that timestamped every domain
    /// <c>_history.Add</c> and every <c>history.Where(...).Select(...).ToList()</c> call)
    /// that the two genuinely overlap in wall-clock time under load (dozens to hundreds of
    /// confirmed overlaps per run). Two isolated, no-ASP.NET, no-gRPC repros (kept in the
    /// task 14 report) confirm the underlying mechanism is real and reproducible on this
    /// machine: a tight loop of <c>List&lt;T&gt;.Add</c> against a concurrent
    /// <c>Where().Select().ToList()</c> throws
    /// <c>System.InvalidOperationException: Collection was modified; enumeration
    /// operation may not execute.</c> reliably (8/8 and 4069/11357 occurrences measured).
    /// Going through the real gRPC/HTTP pipeline on both sides, on this specific machine,
    /// evidently dilutes the number of attempts-per-second on each side far enough that
    /// the same underlying race — confirmed to overlap at the wall-clock level — was not
    /// observed to land at the precise instruction granularity <c>List&lt;T&gt;</c>'s
    /// enumerator needs, within a runtime budget suitable for an automated test. This
    /// test is kept for what it DOES verify — no 500, no thrown corruption exception,
    /// under real, heavily-contended, verified-overlapping concurrent <c>Fire</c>/<c>GET</c>
    /// traffic against the CORRECT <c>Read</c>-based implementation — but, as delivered,
    /// it should NOT be relied upon as the sole guarantee against a future regression back
    /// to <c>Find</c>: that guarantee still rests primarily on the code review of
    /// <see cref="IGameStore.Read{T}"/> itself (same lock, same key, as
    /// <see cref="IGameStore.Mutate{T}"/> — see that interface's own remarks).
    ///
    /// The design keeps two reinforcements over a naive version, since both measurably
    /// helped (even though neither closed the gap above):
    /// <list type="bullet">
    /// <item><b>A wide per-read window.</b> The shared <c>Game</c> is seeded directly
    /// through the domain and saved into the very <see cref="IGameStore"/> instance the
    /// running server resolves via DI (exactly the reason <see cref="IGameStore"/>'s own
    /// doc comment gives for the abstraction: tests inject a store pre-filled into a
    /// given state) with <see cref="PrefillPairs"/> * 2 prior shots, so every read's
    /// <c>history.Where(...).Select(...).ToList()</c> walks thousands of elements instead
    /// of a handful.</item>
    /// <item><b>A simultaneous release burst</b> of <see cref="FireThreadCount"/> single
    /// <c>Fire</c> calls (mirroring the shape of the codebase's other proven races,
    /// <c>Only_one_concurrent_shot_on_the_same_cell_succeeds</c> and
    /// <c>Only_one_concurrent_placement_on_the_same_game_succeeds</c>), rather than a
    /// smaller number of threads each firing many calls sequentially — a large sequential
    /// volume was measured to risk the opponent's own real counter-fire (entitled to land
    /// anywhere not yet shot) accidentally sinking the human fleet before the volume
    /// completed, which stops all further writes once <c>Game.Status</c> flips to
    /// <see cref="GameStatus.Finished"/>. <see cref="ReadThreadCount"/> read threads spin
    /// real <c>GET</c> calls continuously — not a fixed count — for as long as any fire
    /// thread is still working.</item>
    /// </list>
    ///
    /// Dedicated <see cref="Thread"/>s released by a single <see cref="Barrier"/> are used
    /// rather than <c>Task.Run</c>, per the measurement in REVUE-IA.md, Revue 4 (a
    /// thread-pool ramp-up misses races far more often).
    ///
    /// Expected BEFORE running (stated up front, per CLAUDE.md §6): no request — gRPC or
    /// HTTP — ever surfaces as an HTTP 500, and no exception (thrown on either the gRPC or
    /// the HTTP client side) carries the message "Collection was modified".
    /// </summary>
    [Fact]
    public async Task Concurrent_fires_and_reads_on_the_same_game_never_return_500_or_corrupt_state()
    {
        var (id, freshOpponentCells) = SeedLargeHistoryGame();
        var http = _factory.CreateClient();

        Assert.True(
            freshOpponentCells.Count >= FireThreadCount,
            "Not enough fresh cells left on the board for the burst phase.");

        using var start = new Barrier(FireThreadCount + ReadThreadCount);
        var exceptions = new ConcurrentBag<Exception>();
        var saw500 = false;
        var lockObj = new object();
        var stillFiring = FireThreadCount;
        var threads = new List<Thread>(FireThreadCount + ReadThreadCount);

        for (var t = 0; t < FireThreadCount; t++)
        {
            var at = freshOpponentCells[t];
            var fireClient = Client();
            threads.Add(new Thread(() =>
            {
                start.SignalAndWait();
                try
                {
                    fireClient.FireAsync(new FireRequest
                    {
                        GameId = id.ToString(),
                        X = at.X,
                        Y = at.Y
                    }).ResponseAsync.GetAwaiter().GetResult();
                }
                catch (RpcException ex) when (ex.StatusCode is StatusCode.InvalidArgument
                    or StatusCode.FailedPrecondition or StatusCode.NotFound)
                {
                    // Expected business refusal — not the failure under test.
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

        for (var i = 0; i < ReadThreadCount; i++)
        {
            threads.Add(new Thread(() =>
            {
                start.SignalAndWait();
                while (Volatile.Read(ref stillFiring) > 0)
                {
                    try
                    {
                        var response = http.GetAsync($"/games/{id}").GetAwaiter().GetResult();
                        if (response.StatusCode == HttpStatusCode.InternalServerError)
                        {
                            lock (lockObj) { saw500 = true; }
                        }
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

        Assert.False(saw500, "A GET on the game returned 500 during a concurrent Fire.");
        Assert.DoesNotContain(exceptions, ex => ex.Message.Contains("Collection was modified"));
        Assert.Empty(exceptions);
    }

    private const int FireThreadCount = 300;
    private const int ReadThreadCount = 64;
    private const int PrefillPairs = 1_500;

    /// <summary>
    /// Builds a game directly through the domain — bypassing HTTP/gRPC entirely for the
    /// setup, per <see cref="IGameStore"/>'s own justification for the abstraction — with
    /// <see cref="PrefillPairs"/> alternating human/opponent MISSES already recorded in
    /// <c>Game.History</c>, then saves it into the exact <see cref="IGameStore"/> instance
    /// the running server resolves via DI.
    ///
    /// A 300x300 grid (90,000 cells) is deliberately much bigger than the 7-20 range
    /// <c>CreateGameInputValidator</c> allows over HTTP — legitimate here since this
    /// helper builds the <c>Game</c> straight from the domain, which enforces no such
    /// bound. The size matters for a reason a first, smaller attempt (60x60) exposed
    /// empirically, not hypothetically: with the default fleet's 17 cells a larger but
    /// still small fraction of a 60x60 board, the opponent's own real counter-fire during
    /// the race phase (chosen by the real <see cref="IOpponentStrategy"/>, entitled to
    /// land anywhere not yet shot) accumulated enough accidental hits on the human fleet
    /// to end the game before the intended volume of <c>Fire</c> calls completed. Once
    /// <c>Game.Status</c> flips to <see cref="GameStatus.Finished"/>, every further
    /// <c>Fire</c> fails fast without touching <c>History</c> at all, collapsing the write
    /// volume this test depends on. At 90,000 cells the same 17-cell fleet is a ~0.02%
    /// target, so the fleet realistically survives the whole burst. This is a property of
    /// the fixture, not of production code — the fix belongs here, not in the domain.
    ///
    /// Returns the game id and the remaining, never-yet-fired cells of
    /// <c>OpponentBoard</c> — the ones a subsequent real gRPC <c>Fire</c> call (always "as
    /// the human player", i.e. targeting <c>OpponentBoard</c>) can still legally target.
    /// </summary>
    private (Guid Id, IReadOnlyList<Coordinate> FreshOpponentCells) SeedLargeHistoryGame()
    {
        const int gridSize = 300;

        var rules = GameRules.Default with { GridSize = gridSize };
        var random = new Random(20260915);
        var humanShips = new FleetPlacer(random).PlaceAll(rules).Value;
        var opponentShips = new FleetPlacer(random).PlaceAll(rules).Value;

        var game = Game.Start(Guid.NewGuid(), rules, humanShips, opponentShips, "Easy");

        var humanShipCells = humanShips.SelectMany(s => s.Cells).ToHashSet();
        var opponentShipCells = opponentShips.SelectMany(s => s.Cells).ToHashSet();

        var allCells = Enumerable.Range(0, gridSize * gridSize)
            .Select(i => new Coordinate(i % gridSize, i / gridSize))
            .ToList();
        var safeOpponentCells = allCells.Where(c => !opponentShipCells.Contains(c)).ToList();
        var safeHumanCells = allCells.Where(c => !humanShipCells.Contains(c)).ToList();

        for (var i = 0; i < PrefillPairs; i++)
        {
            var playerShot = game.PlayerFires(safeOpponentCells[i]);
            if (!playerShot.IsOk || playerShot.Value.Result != ShotResult.Miss)
            {
                throw new InvalidOperationException(
                    $"Pre-fill assumption broken: player shot {i} was not a plain miss ({playerShot.IsOk}, {(playerShot.IsOk ? playerShot.Value.Result : null)}).");
            }

            var opponentShot = game.OpponentFires(safeHumanCells[i]);
            if (!opponentShot.IsOk || opponentShot.Value.Result != ShotResult.Miss)
            {
                throw new InvalidOperationException(
                    $"Pre-fill assumption broken: opponent shot {i} was not a plain miss ({opponentShot.IsOk}, {(opponentShot.IsOk ? opponentShot.Value.Result : null)}).");
            }
        }

        Assert.Equal(PrefillPairs * 2, game.History.Count);
        Assert.Equal(GameStatus.InProgress, game.Status);

        _ = _factory.Server; // force host startup before resolving the singleton below
        var store = _factory.Services.GetRequiredService<IGameStore>();
        store.Save(game);

        var freshOpponentCells = safeOpponentCells.Skip(PrefillPairs).ToList();
        return (game.Id, freshOpponentCells);
    }
}
