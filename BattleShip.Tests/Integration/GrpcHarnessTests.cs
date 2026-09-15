using Grpc.Net.Client;
using Grpc.Net.Client.Web;
using Microsoft.AspNetCore.Mvc.Testing;
using BattleShip.API.Grpc;

namespace BattleShip.Tests.Integration;

public sealed class GrpcHarnessTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public GrpcHarnessTests(WebApplicationFactory<Program> factory) => _factory = factory;

    private GrpcChannel CreateChannel()
    {
        var handler = new GrpcWebHandler(_factory.Server.CreateHandler());
        return GrpcChannel.ForAddress(
            _factory.Server.BaseAddress,
            new GrpcChannelOptions { HttpHandler = handler });
    }

    [Fact]
    public async Task A_grpc_web_call_from_the_test_server_responds()
    {
        var client = new PingService.PingServiceClient(CreateChannel());

        var reply = await client.PingAsync(new PingRequest { Name = "test" });

        Assert.Equal("pong test", reply.Message);
    }
}
