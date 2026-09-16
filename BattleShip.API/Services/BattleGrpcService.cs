using BattleShip.API.Contracts;
using BattleShip.Models.Contracts;
using BattleShip.API.Endpoints;
using BattleShip.API.Grpc;
using BattleShip.API.Strategies;
using BattleShip.Models;
using FluentValidation;
using Grpc.Core;

namespace BattleShip.API.Services;

/// <summary>
/// The gRPC-Web façade for firing (ADR 0005): the single exchange that satisfies the
/// subject's central gRPC-Web requirement. Binding and validating are, as everywhere
/// else in this codebase, two distinct responsibilities — <see cref="validator"/> is
/// resolved through DI but called explicitly, never implicitly.
///
/// "Touche = on rejoue" (ADR 0006) makes a single <c>Fire</c> call correspond to a
/// SEQUENCE of domain shots, not one: the player's own shot, then — only on a miss, and
/// only while the game stays InProgress — the opponent's counter-attack, replayed
/// through <see cref="IOpponentStrategy.NextShot"/> for as long as it keeps hitting. The
/// whole sequence runs inside a single <see cref="IGameStore.Mutate{T}"/> call, so it is
/// atomic with respect to any other request racing on the same game.
///
/// The opponent strategy never sees <see cref="Game.OpponentBoard"/>: the
/// <see cref="ShotHistory"/> built for it in <see cref="ShotHistoryFor"/> is derived only
/// from <see cref="Game.HumanBoard"/> and the opponent's own past shots
/// (<see cref="Game.History"/> filtered to <see cref="Player.Opponent"/>) — exactly what
/// <see cref="StrategyBenchmark.HistoryFrom"/> builds for the very same reason (ADR 0003:
/// a strategy that cheats is a strategy whose level cannot be measured).
/// </summary>
public sealed class BattleGrpcService(
    IGameStore store, IValidator<FireRequest> validator, IOpponentStrategyFactory strategies)
    : BattleService.BattleServiceBase
{
    public override async Task<FireResponse> Fire(FireRequest request, ServerCallContext context)
    {
        var check = await validator.ValidateAsync(request);
        if (!check.IsValid)
        {
            throw new RpcException(new Status(
                StatusCode.InvalidArgument,
                string.Join("; ", check.Errors.Select(e => e.ErrorMessage))));
        }

        var gameId = Guid.Parse(request.GameId);
        var at = new Coordinate(request.X, request.Y);

        var result = store.Mutate(gameId, game => FireSequence(game, at, strategies));

        return result.IsOk ? result.Value : throw ErrorMapping.ToRpcException(result.Error);
    }

    /// <summary>
    /// Runs entirely inside <see cref="IGameStore.Mutate{T}"/>'s per-game lock: the
    /// player's shot, then the opponent's chained counter-attack. A single thread reads
    /// and appends to <c>game.History</c> throughout, so nothing here races with itself —
    /// the concurrency guarantee comes from <see cref="IGameStore.Mutate{T}"/> excluding
    /// every OTHER call on the same game while this one runs.
    /// </summary>
    private static Result<FireResponse> FireSequence(
        Game game, Coordinate at, IOpponentStrategyFactory strategies)
    {
        var playerShot = game.PlayerFires(at);
        if (!playerShot.IsOk)
            return Result<FireResponse>.Fail(playerShot.Error);

        var opponentShots = new List<Shot>();

        if (playerShot.Value.Result == ShotResult.Miss && game.Status == GameStatus.InProgress)
        {
            var strategy = strategies.ForDifficulty(game.OpponentDifficulty);
            ShotResult lastOpponentResult;

            do
            {
                var history = ShotHistoryFor(game);
                var target = strategy.NextShot(history);
                var opponentShot = game.OpponentFires(target);

                if (!opponentShot.IsOk)
                {
                    // The strategy invariant (ADR 0003, StrategyInvariantTests) guarantees
                    // a legal cell (in bounds, never already shot): reaching this is a
                    // genuine anomaly in the strategy itself, not a business refusal a
                    // caller could act on — hence an exception rather than a Result,
                    // exactly as StrategyBenchmark.PlayGame treats the same impossible
                    // case.
                    throw new InvalidOperationException(
                        $"Opponent strategy '{strategy.Name}' proposed an invalid shot " +
                        $"({target.X}, {target.Y}): {opponentShot.Error}.");
                }

                opponentShots.Add(ToShot(opponentShot.Value));
                lastOpponentResult = opponentShot.Value.Result;
            }
            while (lastOpponentResult != ShotResult.Miss && game.Status == GameStatus.InProgress);
        }

        var response = new FireResponse
        {
            PlayerShot = ToShot(playerShot.Value),
            Status = game.Status.ToString(),
            CurrentPlayer = game.CurrentPlayer.ToString()
        };
        response.OpponentShots.AddRange(opponentShots);

        return Result<FireResponse>.Ok(response);
    }

    /// <summary>
    /// Builds the restricted view handed to the opponent's strategy: its own past shots
    /// and the human fleet's remaining/sunk ships, derived purely from
    /// <see cref="Game.HumanBoard"/> and <see cref="Game.History"/> — never
    /// <see cref="Game.OpponentBoard"/>. Deliberately mirrors
    /// <c>StrategyBenchmark.HistoryFrom</c> rather than sharing code with it (see the task
    /// 11 report referenced there): production code must not depend on a test file.
    /// </summary>
    private static ShotHistory ShotHistoryFor(Game game)
    {
        var sunk = game.HumanBoard.Ships.Where(s => s.IsSunk).ToList();
        var remaining = game.Rules.Fleet.Where(t => sunk.All(s => s.Name != t.Name)).ToList();
        var opponentShots = game.History.Where(s => s.By == Player.Opponent).ToList();

        return new ShotHistory(game.Rules.GridSize, opponentShots, remaining, sunk, game.Rules.ShipsMayTouch);
    }

    /// <summary>
    /// <see cref="DtoMappings.ToState"/> is reused verbatim for <see cref="Shot.Result"/>:
    /// the same "miss" | "hit" | "sunk" casing rule applies on both the HTTP DTO and the
    /// gRPC-Web contract (see <see cref="GameDto"/>'s doc comment), so it is written down
    /// once. <see cref="Shot.SunkShipName"/> is a plain proto3 <c>string</c> (no
    /// "optional"): it cannot carry <see langword="null"/>, hence the coalesce to
    /// <see cref="string.Empty"/> for an unsunk ship.
    /// </summary>
    private static Shot ToShot(ShotRecord record) => new()
    {
        X = record.At.X,
        Y = record.At.Y,
        Result = DtoMappings.ToState(record.Result),
        SunkShipName = record.SunkShipName ?? string.Empty
    };
}
