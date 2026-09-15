using BattleShip.Models;

namespace BattleShip.API.Strategies;

/// <summary>
/// Adversaire de niveau normal : chasse sur les cases de parité paire, puis
/// ratissage des voisins d'une touche non encore couverte par un navire coulé.
/// Sans état : reconstruit sa décision depuis ShotHistory à chaque appel, comme
/// RandomStrategy. L'aléa arrive par le constructeur, jamais via Random.Shared.
///
/// Toute énumération de candidats est bornée par la taille de la grille : aucune
/// boucle ne tourne « jusqu'à trouver ». Voir StrategyInvariantTests pour le
/// garde-fou qui ne détecte, lui, qu'une absence de progression entre deux appels.
/// </summary>
public sealed class HuntTargetStrategy(Random random) : IOpponentStrategy
{
    public string Name => "HuntTarget";

    public Coordinate NextShot(ShotHistory history)
    {
        var jouees = history.Shots.Select(s => s.At).ToHashSet();
        var couvertes = history.SunkShips.SelectMany(s => s.Cells).ToHashSet();

        var touches = history.Shots
            .Where(s => s.Result is ShotResult.Hit or ShotResult.Sunk)
            .Select(s => s.At)
            .Where(c => !couvertes.Contains(c))
            .ToList();

        if (touches.Count > 0)
        {
            var candidats = CandidatsDeRatissage(history.GridSize, touches, jouees);
            if (candidats.Count > 0)
                return candidats[random.Next(candidats.Count)];
        }

        return TirDeChasse(history.GridSize, jouees);
    }

    private Coordinate TirDeChasse(int gridSize, HashSet<Coordinate> jouees)
    {
        var paires = new List<Coordinate>();
        var toutes = new List<Coordinate>();

        for (var x = 0; x < gridSize; x++)
        {
            for (var y = 0; y < gridSize; y++)
            {
                var candidate = new Coordinate(x, y);
                if (jouees.Contains(candidate))
                    continue;

                toutes.Add(candidate);
                if ((x + y) % 2 == 0)
                    paires.Add(candidate);
            }
        }

        // Tout navire de taille >= 2 couvre forcément une case de parité paire : la
        // chasse peut ignorer l'autre moitié de la grille. Repli sur toutes les
        // cases non jouées si la grille paire est épuisée alors que la partie
        // continue.
        var reserve = paires.Count > 0 ? paires : toutes;
        return reserve[random.Next(reserve.Count)];
    }

    private static IReadOnlyList<Coordinate> CandidatsDeRatissage(
        int gridSize, IReadOnlyList<Coordinate> touches, HashSet<Coordinate> jouees)
    {
        // Si deux touches non couvertes sont alignées, prolonger l'alignement plutôt
        // que de proposer un voisin perpendiculaire.
        var prolongements = new List<Coordinate>();
        for (var i = 0; i < touches.Count; i++)
        {
            for (var j = i + 1; j < touches.Count; j++)
            {
                AjouterProlongements(touches[i], touches[j], gridSize, jouees, prolongements);
            }
        }

        if (prolongements.Count > 0)
            return prolongements;

        var voisins = new List<Coordinate>();
        foreach (var touche in touches)
        {
            foreach (var voisin in Voisins(touche, gridSize))
            {
                if (!jouees.Contains(voisin) && !voisins.Contains(voisin))
                    voisins.Add(voisin);
            }
        }

        return voisins;
    }

    private static void AjouterProlongements(
        Coordinate a, Coordinate b, int gridSize, HashSet<Coordinate> jouees, List<Coordinate> resultat)
    {
        if (a.X == b.X && Math.Abs(a.Y - b.Y) == 1)
        {
            AjouterSiValide(new Coordinate(a.X, Math.Min(a.Y, b.Y) - 1), gridSize, jouees, resultat);
            AjouterSiValide(new Coordinate(a.X, Math.Max(a.Y, b.Y) + 1), gridSize, jouees, resultat);
        }
        else if (a.Y == b.Y && Math.Abs(a.X - b.X) == 1)
        {
            AjouterSiValide(new Coordinate(Math.Min(a.X, b.X) - 1, a.Y), gridSize, jouees, resultat);
            AjouterSiValide(new Coordinate(Math.Max(a.X, b.X) + 1, a.Y), gridSize, jouees, resultat);
        }
    }

    private static void AjouterSiValide(
        Coordinate candidate, int gridSize, HashSet<Coordinate> jouees, List<Coordinate> resultat)
    {
        if (candidate.X < 0 || candidate.X >= gridSize || candidate.Y < 0 || candidate.Y >= gridSize)
            return;
        if (jouees.Contains(candidate))
            return;
        if (!resultat.Contains(candidate))
            resultat.Add(candidate);
    }

    private static IEnumerable<Coordinate> Voisins(Coordinate c, int gridSize)
    {
        if (c.X > 0) yield return new Coordinate(c.X - 1, c.Y);
        if (c.X < gridSize - 1) yield return new Coordinate(c.X + 1, c.Y);
        if (c.Y > 0) yield return new Coordinate(c.X, c.Y - 1);
        if (c.Y < gridSize - 1) yield return new Coordinate(c.X, c.Y + 1);
    }
}
