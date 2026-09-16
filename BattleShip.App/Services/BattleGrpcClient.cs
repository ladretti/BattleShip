using BattleShip.API.Grpc;
using BattleShip.Models;
using Grpc.Core;

namespace BattleShip.App.Services;

/// <summary>
/// The only gRPC-Web call the front makes: firing (ADR 0005). Everything else is HTTP, in
/// <see cref="BattleApiClient"/>.
///
/// The namespace of the generated types is <c>BattleShip.API.Grpc</c> — it comes from
/// <c>csharp_namespace</c> in <c>battle.proto</c>, which this project compiles as a client
/// rather than copying. A namespace named after the server on the client side looks odd for
/// a second; it is the price of having exactly one contract file, and it is cheaper than two.
///
/// Like <see cref="BattleApiClient"/>, this returns a failed <see cref="ApiResult{T}"/>
/// instead of letting <see cref="RpcException"/> escape: a refused shot is an ordinary,
/// frequent event in this game — the cell was already fired at, the game is over — and the
/// page must show it, not crash on it.
/// </summary>
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

    /// <summary>
    /// Turns the server's status into a sentence the player can act on.
    ///
    /// <para>The three statuses the server actually produces for a refusal come from
    /// <c>ErrorMapping.ToGrpcStatusCode</c>: <see cref="StatusCode.InvalidArgument"/>,
    /// <see cref="StatusCode.FailedPrecondition"/> and <see cref="StatusCode.NotFound"/>.
    /// Anything else means the call never reached the game logic — the API is down, CORS
    /// blocked the response, the channel failed — and says so instead of pretending the
    /// server refused something.</para>
    ///
    /// <para><see cref="Status.Detail"/> carries <c>GameError.ToString()</c> verbatim, so it
    /// is parsed back into the shared <see cref="GameError"/> enum rather than matched
    /// against strings typed here. A member added to that enum tomorrow lands in the
    /// <c>_ =></c> arm with its own name shown, not in a wrong message.</para>
    /// </summary>
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
