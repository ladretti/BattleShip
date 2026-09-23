using System.Net;
using System.Net.Http.Json;
using BattleShip.API.Stores;
using BattleShip.Models;
using BattleShip.Models.Contracts;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace BattleShip.Tests.Api;

public sealed class EventsEndpointTests
{
    private static (HttpClient Client, IGameStore Store) Seeded()
    {
        var store = new InMemoryGameStore();

        var factory = new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<IGameStore>();
                services.AddSingleton<IGameStore>(store);
            }));

        return (factory.CreateClient(), store);
    }

    private static Game PlayableGame()
    {
        var rules = GameRules.Default;
        var human = new FleetPlacer(new Random(41)).PlaceAll(rules).Value;
        var opponent = new FleetPlacer(new Random(42)).PlaceAll(rules).Value;
        return Game.Start(Guid.NewGuid(), rules, human, opponent);
    }

    [Fact]
    public async Task An_unknown_game_returns_404()
    {
        var (client, _) = Seeded();

        var response = await client.GetAsync($"/games/{Guid.NewGuid()}/events");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task A_negative_from_is_rejected()
    {
        var (client, store) = Seeded();
        var game = PlayableGame();
        store.Save(game);

        var response = await client.GetAsync($"/games/{game.Id}/events?from=-1");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task During_a_game_no_unsunk_opposing_ship_cell_crosses_the_wire()
    {
        var (client, store) = Seeded();
        var game = PlayableGame();
        var shot = game.PlayerFires(new Coordinate(0, 0));
        Assert.True(shot.IsOk);
        store.Save(game);

        Assert.NotEqual(GameStatus.Finished, game.Status);

        var events = await (await client.GetAsync($"/games/{game.Id}/events"))
            .Content.ReadFromJsonAsync<List<GameEventDto>>();

        Assert.NotNull(events);

        var created = events!.OfType<GameCreatedDto>().Single();
        Assert.Empty(created.OpponentShips);

        var exposedCells = events
            .SelectMany(e => e switch
            {
                GameCreatedDto c => c.OpponentShips.SelectMany(s => s.Cells),
                HumanFleetPlacedDto p => p.Ships.SelectMany(s => s.Cells),
                _ => []
            })
            .Select(c => new Coordinate(c.X, c.Y))
            .ToHashSet();

        var humanCells = game.HumanBoard.Ships.SelectMany(s => s.Cells).ToHashSet();

        Assert.Equal(humanCells, exposedCells);
    }

    [Fact]
    public async Task After_the_game_ends_the_full_journal_is_served()
    {
        var (client, store) = Seeded();
        var game = PlayableGame();

        foreach (var cell in game.OpponentBoard.Ships.SelectMany(s => s.Cells).ToList())
        {
            var shot = game.PlayerFires(cell);
            Assert.True(shot.IsOk);
            if (game.Status == GameStatus.Finished)
                break;
        }

        Assert.Equal(GameStatus.Finished, game.Status);
        store.Save(game);

        var events = await (await client.GetAsync($"/games/{game.Id}/events"))
            .Content.ReadFromJsonAsync<List<GameEventDto>>();

        Assert.NotNull(events);

        var created = events!.OfType<GameCreatedDto>().Single();

        Assert.Equal(game.OpponentBoard.Ships.Count, created.OpponentShips.Count);

        var exposedOpponentCells = created.OpponentShips
            .SelectMany(s => s.Cells)
            .Select(c => new Coordinate(c.X, c.Y))
            .ToHashSet();

        var trueOpponentCells = game.OpponentBoard.Ships
            .SelectMany(s => s.Cells)
            .ToHashSet();

        Assert.Equal(trueOpponentCells, exposedOpponentCells);
    }

    [Fact]
    public async Task A_from_beyond_the_journal_length_returns_an_empty_list()
    {
        var (client, store) = Seeded();
        var game = PlayableGame();
        store.Save(game);

        var response = await client.GetAsync($"/games/{game.Id}/events?from=9999");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var events = await response.Content.ReadFromJsonAsync<List<GameEventDto>>();

        Assert.NotNull(events);
        Assert.Empty(events!);
    }
}
