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

        for (var i = 0; i < rules.Fleet.Count; i++)
        {
            // PlacementRules.Validate exige que la flotte fournie corresponde exactement
            // à rules.Fleet : on la restreint donc aux navires déjà posés (0..i) pour
            // pouvoir soumettre chaque candidat sans réimplémenter la règle d'adjacence.
            var partialRules = rules with { Fleet = [.. rules.Fleet.Take(i + 1)] };
            var placement = TryPlaceShip(rules.Fleet[i], placements, partialRules);
            if (placement is null)
                return null;

            placements.Add(placement);
        }

        return placements;
    }

    private ShipPlacement? TryPlaceShip(
        ShipTemplate template, List<ShipPlacement> alreadyPlaced, GameRules partialRules)
    {
        for (var tentative = 0; tentative < MaxTentativesParNavire; tentative++)
        {
            var origin = new Coordinate(
                random.Next(partialRules.GridSize),
                random.Next(partialRules.GridSize));
            var orientation = random.Next(2) == 0 ? Orientation.Horizontal : Orientation.Vertical;
            var candidate = new ShipPlacement(template.Name, origin, orientation, template.Size);

            var attempt = PlacementRules.Validate([.. alreadyPlaced, candidate], partialRules);
            if (attempt.IsOk)
                return candidate;
        }

        return null;
    }
}
