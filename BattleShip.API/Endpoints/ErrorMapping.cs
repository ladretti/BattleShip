using BattleShip.Models;
using Grpc.Core;

namespace BattleShip.API.Endpoints;

public static class ErrorMapping
{
    public static StatusCode ToGrpcStatusCode(GameError error) => error switch
    {
        GameError.GameNotFound => StatusCode.NotFound,
        GameError.OutOfBounds => StatusCode.InvalidArgument,
        GameError.CellAlreadyShot => StatusCode.InvalidArgument,
        GameError.GameAlreadyFinished => StatusCode.FailedPrecondition,
        GameError.GameNotStarted => StatusCode.FailedPrecondition,
        GameError.NotYourTurn => StatusCode.FailedPrecondition,
        GameError.InvalidPlacement => StatusCode.InvalidArgument,
        _ => throw new ArgumentOutOfRangeException(
            nameof(error), error, "Unmapped GameError member: the ADR 0004 table must be extended.")
    };

    public static int ToHttpStatusCode(GameError error) => error switch
    {
        GameError.GameNotFound => StatusCodes.Status404NotFound,
        GameError.OutOfBounds => StatusCodes.Status400BadRequest,
        GameError.CellAlreadyShot => StatusCodes.Status409Conflict,
        GameError.GameAlreadyFinished => StatusCodes.Status409Conflict,
        GameError.GameNotStarted => StatusCodes.Status409Conflict,
        GameError.NotYourTurn => StatusCodes.Status409Conflict,
        GameError.InvalidPlacement => StatusCodes.Status400BadRequest,
        _ => throw new ArgumentOutOfRangeException(
            nameof(error), error, "Unmapped GameError member: the ADR 0004 table must be extended.")
    };

    public static RpcException ToRpcException(GameError error) =>
    new(new Status(ToGrpcStatusCode(error), error.ToString()));
}
