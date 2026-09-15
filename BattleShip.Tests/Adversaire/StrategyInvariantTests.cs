using BattleShip.API.Strategies;
using BattleShip.Models;

namespace BattleShip.Tests.Adversaire;

public sealed class StrategyInvariantTests
{
    public static TheoryData<string, Func<Random, IOpponentStrategy>> Strategies() => new()
    {
        { "Random", r => new RandomStrategy(r) },
        { "HuntTarget", r => new HuntTargetStrategy(r) },
        { "Density", r => new DensityStrategy(r) },
    };

    [Theory]
    [MemberData(nameof(Strategies))]
    public void Une_strategie_ne_propose_jamais_un_coup_invalide(
        string nom, Func<Random, IOpponentStrategy> fabrique)
    {
        var rules = GameRules.Default;

        for (var seed = 1; seed <= 20; seed++)
        {
            var strategy = fabrique(new Random(seed));
            var cible = new Board(rules.GridSize,
                new FleetPlacer(new Random(seed * 31)).PlaceAll(rules).Value);

            var joues = new HashSet<Coordinate>();
            var coups = new List<ShotRecord>();

            // Garde-fou : une partie ne peut pas dépasser GridSize * GridSize coups,
            // puisque chaque coup consomme une case distincte — c'est précisément ce
            // que cet invariant garantit. Au-delà, la stratégie ne progresse plus.
            // xUnit n'impose aucune limite de temps par défaut (aucun Timeout n'est
            // posé sur ce [Theory]) : sans ce garde-fou, une stratégie qui cesse de
            // progresser bloquerait la suite entière sans jamais produire d'échec.
            //
            // Ce garde-fou ne protège PAS contre une boucle infinie à l'intérieur
            // d'un seul appel à NextShot : l'assertion ci-dessous ne serait alors
            // jamais atteinte. La vraie parade contre ce second cas est l'absence de
            // boucle non bornée dans NextShot, côté implémentation des stratégies.
            var coupsMax = rules.GridSize * rules.GridSize;
            var coupsJoues = 0;

            while (!cible.AllSunk)
            {
                Assert.True(++coupsJoues <= coupsMax,
                    $"{nom} (graine {seed}) n'a pas coulé la flotte en {coupsMax} coups : " +
                    "la stratégie ne progresse plus.");

                var history = HistoriqueDepuis(rules, cible, coups);
                var coup = strategy.NextShot(history);

                Assert.InRange(coup.X, 0, rules.GridSize - 1);
                Assert.InRange(coup.Y, 0, rules.GridSize - 1);
                Assert.True(joues.Add(coup),
                    $"{nom} (graine {seed}) a rejoué la case {coup}");

                var tir = cible.Fire(coup, Player.Opponent);
                Assert.True(tir.IsOk);
                coups.Add(tir.Value);
            }

            Assert.True(joues.Count <= rules.GridSize * rules.GridSize);
        }
    }

    internal static ShotHistory HistoriqueDepuis(
        GameRules rules, Board cible, IReadOnlyList<ShotRecord> coups)
    {
        var coules = cible.Ships.Where(s => s.IsSunk).ToList();
        var restants = rules.Fleet
            .Where(t => coules.All(s => s.Name != t.Name))
            .ToList();

        return new ShotHistory(rules.GridSize, coups, restants, coules, rules.ShipsMayTouch);
    }
}
