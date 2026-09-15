using BattleShip.API.Strategies;
using BattleShip.Models;

namespace BattleShip.Tests.Adversaire;

public sealed class DensityStrategyTests
{
    [Fact]
    public void Sur_une_grille_vierge_elle_vise_le_centre_plutot_qu_un_coin()
    {
        var strategy = new DensityStrategy(new Random(1));
        var history = new ShotHistory(10, [], [new ShipTemplate("Croiseur", 4)], [], false);

        var coup = strategy.NextShot(history);

        Assert.InRange(coup.X, 2, 7);
        Assert.InRange(coup.Y, 2, 7);
    }

    [Fact]
    public void Elle_prolonge_une_touche_isolee()
    {
        var strategy = new DensityStrategy(new Random(1));
        var history = new ShotHistory(
            10,
            [new ShotRecord(new Coordinate(4, 4), ShotResult.Hit, Player.Opponent, null)],
            [new ShipTemplate("Croiseur", 4)], [], false);

        var coup = strategy.NextShot(history);

        Assert.Equal(1, Math.Abs(coup.X - 4) + Math.Abs(coup.Y - 4));
    }

    [Fact]
    public void Sous_la_regle_de_non_adjacence_elle_evite_la_couronne_d_un_navire_coule()
    {
        var coule = new Ship("Torpilleur", 2,
            [new Coordinate(4, 4), new Coordinate(4, 5)]);
        var strategy = new DensityStrategy(new Random(1));
        var history = new ShotHistory(
            10,
            [new ShotRecord(new Coordinate(4, 4), ShotResult.Hit, Player.Opponent, null),
             new ShotRecord(new Coordinate(4, 5), ShotResult.Sunk, Player.Opponent, "Torpilleur")],
            [new ShipTemplate("Croiseur", 4)],
            [coule],
            ShipsMayTouch: false);

        for (var i = 0; i < 20; i++)
        {
            var coup = new DensityStrategy(new Random(i + 1)).NextShot(history);
            var dansLaCouronne = coule.Cells.Any(c =>
                Math.Abs(c.X - coup.X) <= 1 && Math.Abs(c.Y - coup.Y) <= 1);
            Assert.False(dansLaCouronne, $"coup {coup} dans la couronne du navire coulé");
        }
        _ = strategy;
    }
}
