using BattleShip.API.Strategies;
using BattleShip.Models;

namespace BattleShip.Tests.Adversaire;

public sealed class StrategyInvariantTests
{
    public static TheoryData<string, Func<Random, IOpponentStrategy>> Strategies() => new()
    {
        { "Random", r => new RandomStrategy(r) },
        // Tâche 9  : { "HuntTarget", r => new HuntTargetStrategy(r) },
        // Tâche 10 : { "Density",    r => new DensityStrategy(r) },
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

            while (!cible.AllSunk)
            {
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
