using System.Net;
using System.Net.Http.Json;
using System.Text;
using BattleShip.API.Contracts;
using Microsoft.AspNetCore.Mvc.Testing;

namespace BattleShip.Tests.Api;

public sealed class HttpEndpointsTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient _client;

    public HttpEndpointsTests(WebApplicationFactory<Program> factory)
        => _client = factory.CreateClient();

    [Fact]
    public async Task Creating_a_game_returns_201_and_its_identifier()
    {
        var response = await _client.PostAsJsonAsync("/games",
            new CreateGameInput(10, "Normal"));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var dto = await response.Content.ReadFromJsonAsync<GameDto>();
        Assert.NotEqual(Guid.Empty, dto!.Id);
    }

    [Fact]
    public async Task An_unknown_game_returns_404()
    {
        var response = await _client.GetAsync($"/games/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task An_out_of_range_grid_is_rejected_with_400()
    {
        var response = await _client.PostAsJsonAsync("/games",
            new CreateGameInput(2, "Normal"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task An_unknown_difficulty_level_is_rejected_with_400()
    {
        var response = await _client.PostAsJsonAsync("/games",
            new CreateGameInput(10, "Impossible"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task A_placement_with_adjacent_ships_is_rejected_with_400()
    {
        var create = await _client.PostAsJsonAsync("/games", new CreateGameInput(10, "Normal"));
        var game = await create.Content.ReadFromJsonAsync<GameDto>();

        // Carrier at (0,0) horizontal, Battleship stuck right below it.
        var placement = new PlacementInput(
        [
            new ShipPlacementInput("Carrier", 0, 0, "Horizontal"),
            new ShipPlacementInput("Battleship", 0, 1, "Horizontal"),
            new ShipPlacementInput("Cruiser", 0, 5, "Horizontal"),
            new ShipPlacementInput("Submarine", 0, 7, "Horizontal"),
            new ShipPlacementInput("Destroyer", 0, 9, "Horizontal")
        ]);

        var response = await _client.PostAsJsonAsync(
            $"/games/{game!.Id}/placement", placement);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("adjac", body, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task A_valid_placement_is_accepted_with_204()
    {
        var create = await _client.PostAsJsonAsync("/games", new CreateGameInput(10, "Normal"));
        var game = await create.Content.ReadFromJsonAsync<GameDto>();

        var placement = new PlacementInput(
        [
            new ShipPlacementInput("Carrier", 0, 0, "Horizontal"),
            new ShipPlacementInput("Battleship", 0, 2, "Horizontal"),
            new ShipPlacementInput("Cruiser", 0, 4, "Horizontal"),
            new ShipPlacementInput("Submarine", 0, 6, "Horizontal"),
            new ShipPlacementInput("Destroyer", 0, 8, "Horizontal")
        ]);

        var response = await _client.PostAsJsonAsync(
            $"/games/{game!.Id}/placement", placement);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }

    [Fact]
    public async Task The_created_game_keeps_the_requested_difficulty()
    {
        var response = await _client.PostAsJsonAsync("/games", new CreateGameInput(10, "Hard"));
        var dto = await response.Content.ReadFromJsonAsync<GameDto>();

        Assert.Equal("Hard", dto!.OpponentDifficulty);
    }

    // A grid this small cannot legally hold the default fleet under the non-adjacency
    // rule: FleetPlacer.PlaceAll fails deterministically (0/20 in direct measurement, see
    // the task 13 correction report), which used to surface as an unhandled 500 instead
    // of a 400 the caller could act on.
    [Fact]
    public async Task A_grid_size_too_small_for_the_fleet_is_rejected_with_400()
    {
        var response = await _client.PostAsJsonAsync("/games", new CreateGameInput(6, "Normal"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    // Enum.TryParse<Orientation>("2", out _) returns true (numeric representations are
    // accepted), which used to let "2" reach ShipPlacement.Cells()'s `_ => throw` and
    // surface as an unhandled 500.
    [Fact]
    public async Task A_numeric_orientation_is_rejected_with_400()
    {
        var create = await _client.PostAsJsonAsync("/games", new CreateGameInput(10, "Normal"));
        var game = await create.Content.ReadFromJsonAsync<GameDto>();

        var placement = new PlacementInput(
        [
            new ShipPlacementInput("Carrier", 0, 0, "2"),
            new ShipPlacementInput("Battleship", 0, 2, "Horizontal"),
            new ShipPlacementInput("Cruiser", 0, 4, "Horizontal"),
            new ShipPlacementInput("Submarine", 0, 6, "Horizontal"),
            new ShipPlacementInput("Destroyer", 0, 8, "Horizontal")
        ]);

        var response = await _client.PostAsJsonAsync($"/games/{game!.Id}/placement", placement);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task A_null_ships_list_is_rejected_with_400()
    {
        var create = await _client.PostAsJsonAsync("/games", new CreateGameInput(10, "Normal"));
        var game = await create.Content.ReadFromJsonAsync<GameDto>();

        var response = await _client.PostAsJsonAsync(
            $"/games/{game!.Id}/placement", new PlacementInput(null!));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task A_missing_ships_property_is_rejected_with_400()
    {
        var create = await _client.PostAsJsonAsync("/games", new CreateGameInput(10, "Normal"));
        var game = await create.Content.ReadFromJsonAsync<GameDto>();

        var response = await _client.PostAsync(
            $"/games/{game!.Id}/placement",
            new StringContent("{}", Encoding.UTF8, "application/json"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task A_null_entry_in_ships_is_rejected_with_400()
    {
        var create = await _client.PostAsJsonAsync("/games", new CreateGameInput(10, "Normal"));
        var game = await create.Content.ReadFromJsonAsync<GameDto>();

        const string body = """
            {
              "ships": [
                { "name": "Carrier", "x": 0, "y": 0, "orientation": "Horizontal" },
                null
              ]
            }
            """;

        var response = await _client.PostAsync(
            $"/games/{game!.Id}/placement",
            new StringContent(body, Encoding.UTF8, "application/json"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    // Regression guard for the "every mutation goes through IGameStore.Mutate" discipline
    // (documented in IGameStore, previously unguarded by any test): N concurrent
    // placements on the SAME newly created game must produce exactly one 204 (the first
    // to take the per-game lock, which moves the game out of Placing) and N-1 refusals.
    // Dedicated Threads + a Barrier, not Task.Run: a Task.Run-based race was measured to
    // start threads too gradually to reliably collide (see REVUE-IA.md, Revue 4, and
    // InMemoryGameStoreTests.Only_one_concurrent_shot_on_the_same_cell_succeeds, which
    // this test mirrors at the HTTP layer instead of calling IGameStore directly).
    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(5)]
    public async Task Only_one_concurrent_placement_on_the_same_game_succeeds(int execution)
    {
        _ = execution;   // the case is replayed: a race that passes once proves nothing
        var create = await _client.PostAsJsonAsync("/games", new CreateGameInput(10, "Normal"));
        var game = await create.Content.ReadFromJsonAsync<GameDto>();

        var placement = new PlacementInput(
        [
            new ShipPlacementInput("Carrier", 0, 0, "Horizontal"),
            new ShipPlacementInput("Battleship", 0, 2, "Horizontal"),
            new ShipPlacementInput("Cruiser", 0, 4, "Horizontal"),
            new ShipPlacementInput("Submarine", 0, 6, "Horizontal"),
            new ShipPlacementInput("Destroyer", 0, 8, "Horizontal")
        ]);

        const int concurrentRequests = 32;
        using var start = new Barrier(concurrentRequests);
        var statusCodes = new HttpStatusCode[concurrentRequests];
        var threads = new Thread[concurrentRequests];

        for (var i = 0; i < concurrentRequests; i++)
        {
            var index = i;
            threads[index] = new Thread(() =>
            {
                start.SignalAndWait();
                var response = _client.PostAsJsonAsync($"/games/{game!.Id}/placement", placement)
                    .GetAwaiter().GetResult();
                statusCodes[index] = response.StatusCode;
            });
        }

        foreach (var thread in threads)
            thread.Start();
        foreach (var thread in threads)
            thread.Join();

        Assert.Equal(1, statusCodes.Count(s => s == HttpStatusCode.NoContent));
        Assert.Equal(concurrentRequests - 1, statusCodes.Count(s => s == HttpStatusCode.BadRequest));
    }
}
