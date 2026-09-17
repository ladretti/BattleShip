using BattleShip.API.Contracts;
using BattleShip.Models.Contracts;
using BattleShip.Models;
using FluentValidation;

namespace BattleShip.API.Endpoints;

public static class GameEndpoints
{
    public static void MapGameEndpoints(this WebApplication app)
    {
        app.MapPost("/games", async Task<IResult> (
            CreateGameInput input, IValidator<CreateGameInput> validator,
            IGameStore store, Random random) =>
        {
            var check = await validator.ValidateAsync(input);
            if (!check.IsValid)
                return TypedResults.ValidationProblem(check.ToDictionary());

            var rules = GameRules.Default with { GridSize = input.GridSize };

            var opponentFleet = new FleetPlacer(random).PlaceAll(rules);
            if (!opponentFleet.IsOk)
            {
                throw new InvalidOperationException(
                    $"Could not place the opponent fleet: {opponentFleet.Error}.");
            }

            var game = Game.Create(Guid.NewGuid(), rules, opponentFleet.Value, input.Difficulty);
            store.Save(game);

            return TypedResults.Created($"/games/{game.Id}", game.ToDto());
        });

        app.MapGet("/games/{id:guid}", IResult (Guid id, IGameStore store) =>
        {
            var result = store.Read(id, game => game.ToDto());
            return result.IsOk ? TypedResults.Ok(result.Value) : TypedResults.NotFound();
        });

        app.MapPost("/games/{id:guid}/placement", async Task<IResult> (
            Guid id, PlacementInput input, IValidator<PlacementInput> validator, IGameStore store) =>
        {
            var check = await validator.ValidateAsync(input);
            if (!check.IsValid)
                return TypedResults.ValidationProblem(check.ToDictionary());

            var result = store.Mutate(id, game =>
            {
                var placements = ToShipPlacements(input, game.Rules);
                return placements.IsOk
                    ? game.PlaceHumanFleet(placements.Value)
                    : Result<bool>.Fail(placements.Error);
            });

            return result.IsOk ? TypedResults.NoContent() : ToProblem(result.Error);
        });

        app.MapGet("/games/{id:guid}/history", IResult (Guid id, IGameStore store) =>
        {
            var result = store.Read(id, game => game.History.ToDto());
            return result.IsOk ? TypedResults.Ok(result.Value) : TypedResults.NotFound();
        });

        app.MapGet("/games/{id:guid}/events", async Task<IResult> (
            Guid id, int? from, IValidator<EventQuery> validator, IGameStore store) =>
        {
            var query = new EventQuery(from ?? 0);
            var check = await validator.ValidateAsync(query);
            if (!check.IsValid)
                return TypedResults.ValidationProblem(check.ToDictionary());

            var status = store.Read(id, game => game.Status);
            if (!status.IsOk)
                return TypedResults.NotFound();

            var events = store.ReadEvents(id, query.From);
            if (!events.IsOk)
                return TypedResults.NotFound();

            return TypedResults.Ok(
                EventProjection.ForPlayer(events.Value, status.Value == GameStatus.Finished));
        });
    }

    private static Result<IReadOnlyList<ShipPlacement>> ToShipPlacements(PlacementInput input, GameRules rules)
    {
        var placements = new List<ShipPlacement>(input.Ships.Count);

        foreach (var ship in input.Ships)
        {
            var template = rules.Fleet.FirstOrDefault(t => t.Name == ship.Name);
            if (template is null)
                return Result<IReadOnlyList<ShipPlacement>>.Fail(GameError.InvalidPlacement);

            var orientation = Enum.Parse<Orientation>(ship.Orientation);
            placements.Add(new ShipPlacement(ship.Name, new Coordinate(ship.X, ship.Y), orientation, template.Size));
        }

        return Result<IReadOnlyList<ShipPlacement>>.Ok(placements);
    }

    private static IResult ToProblem(GameError error) => ErrorMapping.ToHttpStatusCode(error) switch
    {
        StatusCodes.Status404NotFound => TypedResults.NotFound(),
        _ => TypedResults.BadRequest(new
        {
            error = "Invalid placement: the fleet must be placed exactly once, inside " +
                    "the grid, without overlapping, and without any ship adjacent to " +
                    "another (diagonal touching included)."
        })
    };
}
