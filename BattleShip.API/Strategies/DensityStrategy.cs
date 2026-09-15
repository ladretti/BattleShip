using BattleShip.Models;

namespace BattleShip.API.Strategies;

/// <summary>
/// Adversaire de niveau difficile : pour chaque navire encore à couler, énumère
/// tous les placements encore légaux compte tenu de ce qui est connu, compte
/// combien de placements passent par chaque case, puis vise la case au score
/// maximal. Aucune heuristique de voisinage : la seule concentration des
/// placements légaux fait converger la stratégie sur les navires entamés.
///
/// Sans état : reconstruit sa décision depuis ShotHistory à chaque appel, comme
/// RandomStrategy et HuntTargetStrategy. L'aléa arrive par le constructeur,
/// jamais via Random.Shared, et ne sert qu'à départager les cases ex æquo.
///
/// Toute énumération est bornée par navires restants x 2 orientations x
/// GridSize x GridSize origines : aucune boucle ne tourne « jusqu'à trouver ».
/// </summary>
public sealed class DensityStrategy(Random random) : IOpponentStrategy
{
    private const int PoidsToucheNonCoulee = 10;

    public string Name => "Density";

    public Coordinate NextShot(ShotHistory history)
    {
        var jouees = history.Shots.Select(s => s.At).ToHashSet();
        var manquees = history.Shots
            .Where(s => s.Result == ShotResult.Miss)
            .Select(s => s.At)
            .ToHashSet();
        var casesCoulees = history.SunkShips.SelectMany(s => s.Cells).ToHashSet();
        var touchesNonCoulees = history.Shots
            .Where(s => s.Result is ShotResult.Hit or ShotResult.Sunk)
            .Select(s => s.At)
            .Where(c => !casesCoulees.Contains(c))
            .ToHashSet();

        var couronneInterdite = history.ShipsMayTouch
            ? new HashSet<Coordinate>()
            : CouronneDe(history.SunkShips.SelectMany(s => s.Cells), history.GridSize);

        var scores = new Dictionary<Coordinate, int>();

        foreach (var navire in history.RemainingShips)
        {
            foreach (var placement in PlacementsPossibles(navire.Size, history.GridSize))
            {
                if (!EstLegal(placement, manquees, casesCoulees, couronneInterdite))
                    continue;

                var poids = placement.Any(touchesNonCoulees.Contains) ? PoidsToucheNonCoulee : 1;
                foreach (var cellule in placement)
                {
                    scores[cellule] = scores.GetValueOrDefault(cellule) + poids;
                }
            }
        }

        var candidatesPositifs = scores
            .Where(kv => !jouees.Contains(kv.Key) && kv.Value > 0)
            .ToList();

        if (candidatesPositifs.Count > 0)
        {
            var meilleur = candidatesPositifs.Max(kv => kv.Value);

            // Tri explicite : l'ordre d'énumération d'un Dictionary<,> n'est pas
            // garanti par la spécification .NET (seulement stable en pratique tant
            // qu'aucune suppression n'a lieu). Sans ce tri, le tirage aléatoire sur
            // `meilleures` porterait sur un ordre non contractuel : à graine égale,
            // un changement de runtime pourrait faire pointer random.Next() vers une
            // autre case, cassant silencieusement la reproductibilité que la tâche
            // 11 exige (l'invariant de légalité continuerait de passer sans le
            // détecter).
            var meilleures = candidatesPositifs
                .Where(kv => kv.Value == meilleur)
                .Select(kv => kv.Key)
                .OrderBy(c => c.Y).ThenBy(c => c.X)
                .ToList();
            return meilleures[random.Next(meilleures.Count)];
        }

        return TirDeRepli(history.GridSize, jouees);
    }

    private static bool EstLegal(
        IReadOnlyList<Coordinate> placement,
        HashSet<Coordinate> manquees,
        HashSet<Coordinate> casesCoulees,
        HashSet<Coordinate> couronneInterdite)
    {
        foreach (var cellule in placement)
        {
            if (manquees.Contains(cellule))
                return false;
            if (casesCoulees.Contains(cellule))
                return false;
            if (couronneInterdite.Contains(cellule))
                return false;
        }

        return true;
    }

    private static IEnumerable<IReadOnlyList<Coordinate>> PlacementsPossibles(int taille, int gridSize)
    {
        for (var x = 0; x < gridSize; x++)
        {
            for (var y = 0; y < gridSize; y++)
            {
                // Horizontal.
                if (x + taille <= gridSize)
                {
                    yield return Enumerable.Range(0, taille)
                        .Select(i => new Coordinate(x + i, y))
                        .ToList();
                }

                // Vertical. Un navire de taille 1 produirait le même placement que
                // l'horizontal : l'éviter ne changerait rien au score (même case,
                // même poids) mais évite un double comptage.
                if (taille > 1 && y + taille <= gridSize)
                {
                    yield return Enumerable.Range(0, taille)
                        .Select(i => new Coordinate(x, y + i))
                        .ToList();
                }
            }
        }
    }

    private static HashSet<Coordinate> CouronneDe(IEnumerable<Coordinate> cellules, int gridSize)
    {
        var couronne = new HashSet<Coordinate>();
        foreach (var cellule in cellules)
        {
            for (var dx = -1; dx <= 1; dx++)
            {
                for (var dy = -1; dy <= 1; dy++)
                {
                    var voisin = new Coordinate(cellule.X + dx, cellule.Y + dy);
                    if (voisin.X < 0 || voisin.X >= gridSize || voisin.Y < 0 || voisin.Y >= gridSize)
                        continue;
                    couronne.Add(voisin);
                }
            }
        }

        return couronne;
    }

    private Coordinate TirDeRepli(int gridSize, HashSet<Coordinate> jouees)
    {
        var restantes = new List<Coordinate>();
        for (var x = 0; x < gridSize; x++)
        {
            for (var y = 0; y < gridSize; y++)
            {
                var candidate = new Coordinate(x, y);
                if (!jouees.Contains(candidate))
                    restantes.Add(candidate);
            }
        }

        return restantes[random.Next(restantes.Count)];
    }
}
