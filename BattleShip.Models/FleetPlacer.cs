namespace BattleShip.Models;

/// <summary>
/// Places a fleet at random on the grid by rejection sampling, from a source of
/// randomness injected through the constructor (never Random.Shared hard-coded: see
/// the ADR on randomness injection). The only rule that decides whether a placement
/// is valid is PlacementRules.Validate: this type reimplements neither overlap
/// detection nor adjacency detection.
/// </summary>
public sealed class FleetPlacer(Random random)
{
    private const int MaxAttemptsPerShip = 500;
    private const int MaxFullRestarts = 200;

    public Result<IReadOnlyList<Ship>> PlaceAll(GameRules rules)
    {
        for (var fullRestart = 0; fullRestart < MaxFullRestarts; fullRestart++)
        {
            var placements = TryPlaceFleet(rules);
            if (placements is null)
                continue;

            return PlacementRules.Validate(placements, rules);
        }

        return Result<IReadOnlyList<Ship>>.Fail(GameError.InvalidPlacement);
    }

    private List<ShipPlacement>? TryPlaceFleet(GameRules rules)
    {
        var placements = new List<ShipPlacement>(rules.Fleet.Count);

        foreach (var template in rules.Fleet)
        {
            var placement = TryPlaceShip(template, placements, rules);
            if (placement is null)
                return null;

            placements.Add(placement);
        }

        return placements;
    }

    private ShipPlacement? TryPlaceShip(
        ShipTemplate template, List<ShipPlacement> alreadyPlaced, GameRules rules)
    {
        // PlacementRules.Validate requires a complete fleet: we therefore describe to it
        // the partial fleet (ships already placed + candidate) that is being validated.
        List<ShipTemplate> partialFleet =
        [
            .. alreadyPlaced.Select(p => new ShipTemplate(p.Name, p.Size)),
            new ShipTemplate(template.Name, template.Size)
        ];
        var partialRules = rules with { Fleet = partialFleet };

        for (var attempt = 0; attempt < MaxAttemptsPerShip; attempt++)
        {
            var origin = new Coordinate(
                random.Next(rules.GridSize),
                random.Next(rules.GridSize));
            var orientation = random.Next(2) == 0 ? Orientation.Horizontal : Orientation.Vertical;
            var candidate = new ShipPlacement(template.Name, origin, orientation, template.Size);

            var validation = PlacementRules.Validate([.. alreadyPlaced, candidate], partialRules);
            if (validation.IsOk)
                return candidate;
        }

        return null;
    }
}
