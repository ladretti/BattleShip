using System.Net;
using System.Net.Http.Json;
using BattleShip.API.Grpc;
using BattleShip.Models.Contracts;
using Grpc.Net.Client;
using Grpc.Net.Client.Web;
using Microsoft.AspNetCore.Mvc.Testing;

namespace BattleShip.Tests.Api;

/// <summary>
/// GET /games/{id}/history — the ordered list of every shot of a game, both sides.
///
/// The harness (<see cref="Client"/>, <see cref="ReadyGame"/>) is deliberately a copy of
/// <c>FireGrpcTests</c>'s rather than a shared base class: each test class stands on its
/// own, and coupling two suites through inheritance for thirty lines buys less than it
/// costs.
///
/// The game is created on "Easy" and the human fleet is placed in known rows. The
/// OPPONENT's fleet, however, is placed at random by the server, so no test here may assume
/// the outcome of a given shot: a test that asserted "(5,5) is a miss" would pass or fail
/// depending on the run. Where the outcome matters — because a miss is what makes the
/// opponent answer at all ("touche = on rejoue", ADR 0006) — it is obtained by firing until
/// the server reports one, never by assuming it.
/// </summary>
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

        // The player fires first, and the endpoint must preserve that order — a history that
        // came back sorted by coordinate, or reversed, would still be "not empty".
        Assert.Equal("Human", history[0].By);
        Assert.Equal(5, history[0].X);
        Assert.Equal(5, history[0].Y);
    }

    [Fact]
    public async Task A_missed_shot_brings_the_opponent_into_the_history()
    {
        var id = await ReadyGame();
        var client = Client();

        // Fire until the server reports a miss. Which cell misses depends on where the
        // server placed the opponent's fleet, so it is discovered, never assumed.
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

        // The opponent only ever answers AFTER the player's miss, never before the first shot.
        Assert.Equal("Human", history[0].By);
    }

    [Fact]
    public async Task A_sunk_ship_is_named_in_the_history_and_nowhere_else()
    {
        var id = await ReadyGame();
        var client = Client();

        // The opponent's fleet is placed at random, so the only way to be sure something gets
        // sunk is to fire everywhere. The game stops accepting shots once it is over, which
        // is why the refusals are swallowed here rather than asserted on.
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
                    // Already shot, or the game has ended: neither is what this test measures.
                }
            }
        }

        var history = await factory.CreateClient()
            .GetFromJsonAsync<List<ShotDto>>($"/games/{id}/history");

        Assert.NotNull(history);
        var sunk = history.Where(s => s.SunkShipName is not null).ToList();
        Assert.NotEmpty(sunk);

        // A ship's name only ever appears on the shot that sank it — never on the hits that
        // preceded it, which would let a client know a ship was doomed one shot early.
        Assert.All(sunk, shot => Assert.Equal("sunk", shot.Result));

        // Grouped BY SIDE: both fleets carry the same five ship names, so "Carrier" legitimately
        // appears twice in a game where each side sank the other's. What must never happen is the
        // same side sinking the same ship twice.
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
