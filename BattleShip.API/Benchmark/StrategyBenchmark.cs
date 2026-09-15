using BattleShip.API.Strategies;
using BattleShip.Models;

namespace BattleShip.API.Benchmark;

/// <summary>
/// Entrée du duel d'IA : nombre de parties par stratégie et graine de départ.
/// </summary>
public sealed record BenchmarkInput(int Games, int Seed);

/// <summary>
/// Résultat agrégé d'une stratégie sur <see cref="Games"/> parties : nombre moyen de
/// coups jusqu'à la flotte coulée, ainsi que les extrêmes observés.
/// </summary>
public sealed record BenchmarkResult(
    string StrategyName, int Games, double AverageShots, int MinShots, int MaxShots);

/// <summary>
/// Duel d'IA : mesure le nombre de coups nécessaires à chaque niveau d'adversaire pour
/// couler une flotte, sur un nombre donné de parties à graine fixe (REVUE-IA.md, revue 3).
///
/// Reproduit volontairement la logique de construction de <see cref="ShotHistory"/> de
/// StrategyInvariantTests.HistoriqueDepuis plutôt que d'en faire dépendre le code de
/// production d'un fichier de test — voir le rapport de la tâche 11.
///
/// Discipline de reproductibilité : aucune source d'aléa n'est prise en dur
/// (jamais Random.Shared) et aucune structure non ordonnée (HashSet, dictionnaire...)
/// n'est énumérée dans la boucle de mesure. Pour l'indice de partie i, le placement de
/// flotte des trois stratégies est dérivé de la même graine (seed + i) : sinon la
/// comparaison mesurerait la chance du placement plutôt que le niveau de la stratégie.
/// La graine de la stratégie elle-même est dérivée séparément (seed + i + salt) pour ne
/// pas faire rejouer à la stratégie exactement la même suite de nombres que le placement,
/// sans que cela soit une exigence de correction — seule la déterminisme compte ici.
/// </summary>
public static class StrategyBenchmark
{
    private const int StrategyRandomSalt = 104_729;

    private static readonly IReadOnlyList<(string Name, Func<Random, IOpponentStrategy> Create)> Strategies =
    [
        ("Random", r => new RandomStrategy(r)),
        ("HuntTarget", r => new HuntTargetStrategy(r)),
        ("Density", r => new DensityStrategy(r)),
    ];

    public static IReadOnlyList<BenchmarkResult> Run(GameRules rules, int games, int seed)
    {
        var results = new List<BenchmarkResult>(Strategies.Count);

        foreach (var (name, create) in Strategies)
        {
            var shotsParPartie = new int[games];

            for (var i = 0; i < games; i++)
            {
                var placementSeed = unchecked(seed + i);
                var fleet = new FleetPlacer(new Random(placementSeed)).PlaceAll(rules).Value;
                var cible = new Board(rules.GridSize, fleet);
                var strategy = create(new Random(unchecked(placementSeed + StrategyRandomSalt)));

                shotsParPartie[i] = JoueUnePartie(rules, cible, strategy, name, i);
            }

            results.Add(new BenchmarkResult(
                StrategyName: name,
                Games: games,
                AverageShots: shotsParPartie.Average(),
                MinShots: shotsParPartie.Min(),
                MaxShots: shotsParPartie.Max()));
        }

        return results;
    }

    private static int JoueUnePartie(
        GameRules rules, Board cible, IOpponentStrategy strategy, string strategyName, int gameIndex)
    {
        var coups = new List<ShotRecord>();
        var coupsMax = rules.GridSize * rules.GridSize;

        while (!cible.AllSunk)
        {
            if (coups.Count >= coupsMax)
            {
                throw new InvalidOperationException(
                    $"{strategyName} (partie {gameIndex}) n'a pas coulé la flotte en " +
                    $"{coupsMax} coups : la stratégie ne progresse plus.");
            }

            var history = HistoriqueDepuis(rules, cible, coups);
            var coup = strategy.NextShot(history);
            var tir = cible.Fire(coup, Player.Opponent);

            if (!tir.IsOk)
            {
                throw new InvalidOperationException(
                    $"{strategyName} (partie {gameIndex}) a proposé un coup invalide " +
                    $"({coup.X}, {coup.Y}) : {tir.Error}.");
            }

            coups.Add(tir.Value);
        }

        return coups.Count;
    }

    private static ShotHistory HistoriqueDepuis(
        GameRules rules, Board cible, IReadOnlyList<ShotRecord> coups)
    {
        var coules = cible.Ships.Where(s => s.IsSunk).ToList();
        var restants = rules.Fleet
            .Where(t => coules.All(s => s.Name != t.Name))
            .ToList();

        return new ShotHistory(rules.GridSize, coups, restants, coules, rules.ShipsMayTouch);
    }
}
