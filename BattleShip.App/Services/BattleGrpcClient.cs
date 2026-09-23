using BattleShip.API.Grpc;
using BattleShip.Models;
using Grpc.Core;

namespace BattleShip.App.Services;

public sealed class BattleGrpcClient(BattleService.BattleServiceClient client)
{
    public async Task<ApiResult<FireResponse>> FireAsync(
        Guid gameId, Coordinate at, CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await client.FireAsync(
                new FireRequest { GameId = gameId.ToString(), X = at.X, Y = at.Y },
                cancellationToken: cancellationToken);

            return ApiResult<FireResponse>.Ok(response);
        }
        catch (RpcException exception)
        {
            return ApiResult<FireResponse>.Fail(Describe(exception));
        }
    }

    private static string Describe(RpcException exception)
    {
        var detail = exception.Status.Detail;

        if (Enum.TryParse<GameError>(detail, out var error))
            return Explain(error);

        return exception.StatusCode switch
        {
            StatusCode.InvalidArgument => $"The server rejected this shot: {detail}",
            StatusCode.NotFound => "That game no longer exists on the server.",
            StatusCode.FailedPrecondition => $"This shot cannot be played right now: {detail}",
            StatusCode.Unavailable =>
                "Could not reach the server. Check that BattleShip.API is running and that " +
                "its certificate is trusted.",
            _ => $"The shot failed ({exception.StatusCode}): {detail}"
        };
    }

    private static string Explain(GameError error) => error switch
    {
        GameError.CellAlreadyShot => "You have already fired at that cell. Pick another one.",
        GameError.OutOfBounds => "That cell is outside the grid.",
        GameError.GameAlreadyFinished => "This game is over. Start a new one to keep playing.",
        GameError.GameNotStarted => "The fleet is not placed yet.",
        GameError.NotYourTurn => "It is not your turn.",
        GameError.GameNotFound => "That game no longer exists on the server.",
        GameError.InvalidPlacement => "That placement was refused.",
        _ => $"The server refused this shot ({error})."
    };
}
