using BattleShip.API.Contracts;
using BattleShip.Models.Contracts;
using BattleShip.API.Endpoints;
using BattleShip.API.Grpc;
using BattleShip.API.Strategies;
using BattleShip.Models;
using FluentValidation;
using Grpc.Core;

namespace BattleShip.API.Services;

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

    private static ShotHistory ShotHistoryFor(Game game)
    {
        var sunk = game.HumanBoard.Ships.Where(s => s.IsSunk).ToList();
        var remaining = game.Rules.Fleet.Where(t => sunk.All(s => s.Name != t.Name)).ToList();
        var opponentShots = game.History.Where(s => s.By == Player.Opponent).ToList();

        return new ShotHistory(game.Rules.GridSize, opponentShots, remaining, sunk, game.Rules.ShipsMayTouch);
    }

    private static Shot ToShot(ShotRecord record) => new()
    {
        X = record.At.X,
        Y = record.At.Y,
        Result = DtoMappings.ToState(record.Result),
        SunkShipName = record.SunkShipName ?? string.Empty
    };
}
