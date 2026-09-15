using BattleShip.Models;
using Grpc.Core;

namespace BattleShip.API.Endpoints;

/// <summary>
/// Single source of truth translating a domain-level business refusal
/// (<see cref="GameError"/>) into each façade's own vocabulary: a gRPC
/// <see cref="StatusCode"/> and an HTTP status code. ADR 0004 fixes this table; it is
/// written down here ONCE and read by both <c>GameEndpoints</c> (HTTP) and
/// <c>BattleGrpcService</c> (gRPC) rather than kept as two independent switches that could
/// silently drift apart.
///
/// Both switches below enumerate all seven <see cref="GameError"/> members explicitly and
/// reserve <c>default</c> for a <c>throw</c>: a silent fallback would hand out an
/// arbitrary status to a member added later without anyone noticing it was never mapped,
/// which is exactly what ADR 0004's closed, exhaustive table rules out. This is not the
/// same discipline as translating a business refusal to an exception (forbidden by ADR
/// 0004) — <see cref="GameError"/> itself is never thrown; only reaching this method with
/// a value that is not a real member of the enum (a genuine programming anomaly) throws.
/// </summary>
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

    /// <summary>
    /// Builds the <see cref="RpcException"/> that carries <paramref name="error"/> across
    /// the gRPC boundary, per <see cref="ToGrpcStatusCode"/>. The only place a
    /// <see cref="GameError"/> is turned into something thrown: everywhere upstream of
    /// this call it stays a <see cref="Result{T}"/> failure, never an exception (ADR
    /// 0004) — this method exists solely because gRPC itself has no Result-shaped return
    /// channel and reports failures through <see cref="RpcException"/>.
    /// </summary>
    public static RpcException ToRpcException(GameError error) =>
        new(new Status(ToGrpcStatusCode(error), error.ToString()));
}
