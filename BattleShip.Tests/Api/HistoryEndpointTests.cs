using System.Net;
using System.Net.Http.Json;
using BattleShip.API.Grpc;
using BattleShip.Models.Contracts;
using Grpc.Net.Client;
using Grpc.Net.Client.Web;
using Microsoft.AspNetCore.Mvc.Testing;

namespace BattleShip.Tests.Api;

public sealed class HistoryEndpointTests(WebApplicationFactory<Program> factory)
    : IClassFixture<WebApplicationFactory<Program>>
{
    private BattleService.BattleServiceClient Client()
    {
        var handler = new GrpcWebHandler(factory.Server.CreateHandler());
        return new BattleService.BattleServiceClient(
            GrpcChannel.ForAddress(factory.Server.BaseAddress,
                new GrpcChannelOptions { HttpHandler = handler }));
    }

    private async Task<Guid> ReadyGame()
    {
        var http = factory.CreateClient();
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
    public async Task The_history_reflects_the_shots_played_in_order()
    {
        var id = await ReadyGame();
        await Client().FireAsync(new FireRequest { GameId = id.ToString(), X = 5, Y = 5 });

        var history = await factory.CreateClient()
            .GetFromJsonAsync<List<ShotDto>>($"/games/{id}/history");

        Assert.NotNull(history);
        Assert.NotEmpty(history);

        Assert.Equal("Human", history[0].By);
        Assert.Equal(5, history[0].X);
        Assert.Equal(5, history[0].Y);
    }

    [Fact]
    public async Task A_missed_shot_brings_the_opponent_into_the_history()
    {
        var id = await ReadyGame();
        var client = Client();

        var shots = 0;
        string result;
        do
        {
            var reply = await client.FireAsync(new FireRequest { GameId = id.ToString(), X = shots % 10, Y = shots / 10 });
            result = reply.PlayerShot.Result;
            shots++;
        }
        while (result != "miss" && shots < 100);

        Assert.Equal("miss", result);

        var history = await factory.CreateClient()
            .GetFromJsonAsync<List<ShotDto>>($"/games/{id}/history");

        Assert.NotNull(history);
        Assert.Contains(history, shot => shot.By == "Opponent");
        Assert.Contains(history, shot => shot.By == "Human");
        Assert.All(history, shot => Assert.Contains(shot.Result, new[] { "miss", "hit", "sunk" }));

        Assert.Equal("Human", history[0].By);
    }

    [Fact]
    public async Task A_sunk_ship_is_named_in_the_history_and_nowhere_else()
    {
        var id = await ReadyGame();
        var client = Client();

        for (var y = 0; y < 10; y++)
        {
            for (var x = 0; x < 10; x++)
            {
                try
                {
                    await client.FireAsync(new FireRequest { GameId = id.ToString(), X = x, Y = y });
                }
                catch (Grpc.Core.RpcException)
                {
                }
            }
        }

        var history = await factory.CreateClient()
            .GetFromJsonAsync<List<ShotDto>>($"/games/{id}/history");

        Assert.NotNull(history);
        var sunk = history.Where(s => s.SunkShipName is not null).ToList();
        Assert.NotEmpty(sunk);

        Assert.All(sunk, shot => Assert.Equal("sunk", shot.Result));

        Assert.Equal(
            sunk.Select(s => (s.By, s.SunkShipName)).Distinct().Count(),
            sunk.Count);
    }

    [Fact]
    public async Task The_history_of_an_unknown_game_is_not_found()
    {
        var response = await factory.CreateClient().GetAsync($"/games/{Guid.NewGuid()}/history");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}
