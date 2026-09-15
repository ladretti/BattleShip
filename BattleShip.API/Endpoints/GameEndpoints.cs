using BattleShip.API.Contracts;
using BattleShip.Models;
using FluentValidation;

namespace BattleShip.API.Endpoints;

/// <summary>
/// The four HTTP routes that create a game, read its state, place the human fleet, and
/// read its shot history. Firing has no HTTP route: it goes exclusively through
/// gRPC-Web (ADR 0005, task 14) and was never planned as an HTTP endpoint.
///
/// Binding a request body and validating it are two distinct responsibilities: every
/// route below resolves its FluentValidation validator through DI but calls it
/// explicitly, never implicitly.
/// </summary>
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

            // The opponent's fleet is placed automatically, up front: the player never
            // chooses it, so there is nothing to validate here beyond what
            // CreateGameInputValidator already checked (GridSize in [5, 20]).
            var opponentFleet = new FleetPlacer(random).PlaceAll(rules);
            if (!opponentFleet.IsOk)
            {
                // FleetPlacer.PlaceAll only fails if rejection sampling is exhausted for a
                // grid/fleet combination that cannot legally hold the fleet at all.
                // CreateGameInputValidator's [5, 20] range keeps every grid this large
                // enough for the default fleet (longest ship: 5) — reaching this branch
                // would be an anomaly in the placement algorithm, not a business refusal
                // a caller could act on, hence an exception rather than a 4xx.
                throw new InvalidOperationException(
                    $"Could not place the opponent fleet: {opponentFleet.Error}.");
            }

            var game = Game.Create(Guid.NewGuid(), rules, opponentFleet.Value);
            store.Save(game);

            return TypedResults.Created($"/games/{game.Id}", game.ToDto());
        });

        app.MapGet("/games/{id:guid}", IResult (Guid id, IGameStore store) =>
            store.Find(id) is { } game
                ? TypedResults.Ok(game.ToDto())
                : TypedResults.NotFound());

        app.MapPost("/games/{id:guid}/placement", async Task<IResult> (
            Guid id, PlacementInput input, IValidator<PlacementInput> validator, IGameStore store) =>
        {
            var check = await validator.ValidateAsync(input);
            if (!check.IsValid)
                return TypedResults.ValidationProblem(check.ToDictionary());

            var result = store.Mutate(id, game =>
                game.PlaceHumanFleet(ToShipPlacements(input, game.Rules)));

            return result.IsOk ? TypedResults.NoContent() : ToProblem(result.Error);
        });

        app.MapGet("/games/{id:guid}/history", IResult (Guid id, IGameStore store) =>
            store.Find(id) is { } game
                ? TypedResults.Ok(game.History.ToDto())
                : TypedResults.NotFound());
    }

    private static IReadOnlyList<ShipPlacement> ToShipPlacements(PlacementInput input, GameRules rules) =>
        [.. input.Ships.Select(s => ToShipPlacement(s, rules))];

    private static ShipPlacement ToShipPlacement(ShipPlacementInput input, GameRules rules)
    {
        var template = rules.Fleet.First(t => t.Name == input.Name);
        var orientation = Enum.Parse<Orientation>(input.Orientation);
        return new ShipPlacement(input.Name, new Coordinate(input.X, input.Y), orientation, template.Size);
    }

    /// <summary>
    /// Translates a placement refusal to its HTTP status, per the ADR 0004 table
    /// (GameNotFound → 404, InvalidPlacement → 400). PlaceHumanFleet's only two failure
    /// causes — an illegal placement (overlap, out of bounds, wrong fleet, adjacency) and
    /// a placement submitted outside the Placing phase — both surface as the same
    /// InvalidPlacement error (see Game.PlaceHumanFleet's own remarks), so this message
    /// spells out every possible reason, including the word "adjacent" that
    /// HttpEndpointsTests checks for, without claiming to know which one actually applied.
    /// </summary>
    private static IResult ToProblem(GameError error) => error switch
    {
        GameError.GameNotFound => TypedResults.NotFound(),
        _ => TypedResults.BadRequest(new
        {
            error = "Invalid placement: the fleet must be placed exactly once, inside " +
                    "the grid, without overlapping, and without any ship adjacent to " +
                    "another (diagonal touching included)."
        })
    };
}
