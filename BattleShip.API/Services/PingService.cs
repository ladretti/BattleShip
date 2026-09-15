using Grpc.Core;
using BattleShip.API.Grpc;

namespace BattleShip.API.Services;

public sealed class PingGrpcService : PingService.PingServiceBase
{
    public override Task<PingReply> Ping(PingRequest request, ServerCallContext context) =>
        Task.FromResult(new PingReply { Message = $"pong {request.Name}" });
}
