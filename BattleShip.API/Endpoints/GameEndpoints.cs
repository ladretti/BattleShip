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
///
/// Both GET routes read through <see cref="IGameStore.Read{T}"/>, not
/// <see cref="IGameStore.Find"/>: both project a collection (<c>ToDto()</c> walks
/// <c>Game.History</c> and each board's ships/received shots), and <see cref="IGameStore"/>'s
/// own remarks reserve <c>Find</c> for reads that never enumerate. See
/// <see cref="IGameStore.Read{T}"/>'s doc comment for why this matters as soon as a
/// concurrent mutation exists (task 14's gRPC <c>Fire</c>).
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
            // CreateGameInputValidator already checked.
            var opponentFleet = new FleetPlacer(random).PlaceAll(rules);
            if (!opponentFleet.IsOk)
            {
                // FleetPlacer.PlaceAll fails deterministically for a grid too small to
                // legally hold the fleet under the non-adjacency rule (verified by direct
                // execution for every GridSize from 5 to 10 — see the task 13 correction
                // report): 0/20 successes at 5 and 6, 20/20 at 7 and above.
                // CreateGameInputValidator's lower bound of 7 exists precisely to keep
                // this branch unreachable through the validated range; reaching it despite
                // that would be a genuine anomaly in the placement algorithm itself, not a
                // business refusal a caller could act on, hence an exception rather than a
                // 4xx.
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
    }

    /// <summary>
    /// Looks up each ship's size from <paramref name="rules"/>.Fleet by name — never from
    /// <c>GameRules.Default</c> directly, unlike <c>PlacementInputValidator</c>, which can
    /// only check against the default fleet since it has no access to a specific game.
    /// The two coincide today (no route lets a game be created with anything but the
    /// default fleet), but this lookup uses FirstOrDefault and fails cleanly with
    /// InvalidPlacement rather than throwing if a name the validator accepted is somehow
    /// absent from the targeted game's actual fleet — a defense against that invariant
    /// breaking later (e.g. a per-game custom fleet), not a currently reachable path.
    /// </summary>
    private static Result<IReadOnlyList<ShipPlacement>> ToShipPlacements(PlacementInput input, GameRules rules)
    {
        var placements = new List<ShipPlacement>(input.Ships.Count);

        foreach (var ship in input.Ships)
        {
            var template = rules.Fleet.FirstOrDefault(t => t.Name == ship.Name);
            if (template is null)
                return Result<IReadOnlyList<ShipPlacement>>.Fail(GameError.InvalidPlacement);

            // Enum.Parse (not TryParse) is safe here: PlacementInputValidator already
            // rejected any Orientation other than the two literal names "Horizontal" and
            // "Vertical" before this method can run.
            var orientation = Enum.Parse<Orientation>(ship.Orientation);
            placements.Add(new ShipPlacement(ship.Name, new Coordinate(ship.X, ship.Y), orientation, template.Size));
        }

        return Result<IReadOnlyList<ShipPlacement>>.Ok(placements);
    }

    /// <summary>
    /// Translates a placement refusal to its HTTP status, read from
    /// <see cref="ErrorMapping.ToHttpStatusCode"/> — the single ADR 0004 table shared with
    /// <c>BattleGrpcService</c>'s gRPC façade, not a second copy of it. PlaceHumanFleet's
    /// and ToShipPlacements's only failure cause — an illegal placement (overlap, out of
    /// bounds, wrong fleet, adjacency, an unrecognized ship name), or a placement
    /// submitted outside the Placing phase — all surface as the same InvalidPlacement
    /// error (see Game.PlaceHumanFleet's own remarks), so this message spells out every
    /// possible reason, including the word "adjacent" that HttpEndpointsTests checks for,
    /// without claiming to know which one actually applied.
    /// </summary>
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
