namespace BattleShip.Models;

/// <summary>
/// Place une flotte au hasard sur la grille par tirage-rejet, à partir d'une source
/// d'aléa injectée par le constructeur (jamais Random.Shared en dur : voir ADR sur
/// l'injection de l'aléa). La seule règle qui décide de la validité d'un placement
/// est PlacementRules.Validate : ce type ne réimplémente ni la détection de
/// chevauchement, ni celle d'adjacence.
/// </summary>
public sealed class FleetPlacer(Random random)
{
    private const int MaxTentativesParNavire = 500;
    private const int MaxRelancesCompletes = 200;

    public Result<IReadOnlyList<Ship>> PlaceAll(GameRules rules)
    {
        for (var relance = 0; relance < MaxRelancesCompletes; relance++)
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
        // PlacementRules.Validate exige une flotte complète : on lui décrit donc la
        // flotte partielle (navires déjà posés + candidat) qu'on est en train de valider.
        List<ShipTemplate> partialFleet =
        [
            .. alreadyPlaced.Select(p => new ShipTemplate(p.Name, p.Size)),
            new ShipTemplate(template.Name, template.Size)
        ];
        var partialRules = rules with { Fleet = partialFleet };

        for (var tentative = 0; tentative < MaxTentativesParNavire; tentative++)
        {
            var origin = new Coordinate(
                random.Next(rules.GridSize),
                random.Next(rules.GridSize));
            var orientation = random.Next(2) == 0 ? Orientation.Horizontal : Orientation.Vertical;
            var candidate = new ShipPlacement(template.Name, origin, orientation, template.Size);

            var attempt = PlacementRules.Validate([.. alreadyPlaced, candidate], partialRules);
            if (attempt.IsOk)
                return candidate;
        }

        return null;
    }
}
