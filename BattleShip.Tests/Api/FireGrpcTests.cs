using System.Net.Http.Json;
using BattleShip.API.Contracts;
using BattleShip.API.Grpc;
using Grpc.Core;
using Grpc.Net.Client;
using Grpc.Net.Client.Web;
using Microsoft.AspNetCore.Mvc.Testing;

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
}
