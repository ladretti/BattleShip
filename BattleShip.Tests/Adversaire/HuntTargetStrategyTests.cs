using BattleShip.API.Strategies;
using BattleShip.Models;

namespace BattleShip.Tests.Adversaire;

public sealed class HuntTargetStrategyTests
{
    private static ShotHistory Historique(params ShotRecord[] coups) =>
        new(10, coups, [new ShipTemplate("Torpilleur", 2)], [], ShipsMayTouch: false);

    [Fact]
    public void En_phase_de_chasse_elle_ne_vise_que_les_cases_de_parite_paire()
    {
        var strategy = new HuntTargetStrategy(new Random(1));

        for (var i = 0; i < 40; i++)
        {
            var coup = strategy.NextShot(Historique());
            Assert.Equal(0, (coup.X + coup.Y) % 2);
        }
    }

    [Fact]
    public void Apres_une_touche_elle_vise_une_case_adjacente()
    {
        var strategy = new HuntTargetStrategy(new Random(1));
        var touche = new ShotRecord(new Coordinate(4, 4), ShotResult.Hit,
            Player.Opponent, null);

        var coup = strategy.NextShot(Historique(touche));

        var distance = Math.Abs(coup.X - 4) + Math.Abs(coup.Y - 4);
        Assert.Equal(1, distance);
    }

    [Fact]
    public void Apres_une_touche_dans_un_coin_elle_ne_sort_pas_de_la_grille()
    {
        var strategy = new HuntTargetStrategy(new Random(1));
        var touche = new ShotRecord(new Coordinate(0, 0), ShotResult.Hit,
            Player.Opponent, null);

        var coup = strategy.NextShot(Historique(touche));

        Assert.InRange(coup.X, 0, 9);
        Assert.InRange(coup.Y, 0, 9);
    }

    [Fact]
    public void Un_navire_coule_ne_declenche_plus_de_ratissage()
    {
        var strategy = new HuntTargetStrategy(new Random(1));
        var history = new ShotHistory(
            10,
            [new ShotRecord(new Coordinate(4, 4), ShotResult.Hit, Player.Opponent, null),
             new ShotRecord(new Coordinate(4, 5), ShotResult.Sunk, Player.Opponent, "Torpilleur")],
            [],
            [new Ship("Torpilleur", 2, [new Coordinate(4, 4), new Coordinate(4, 5)])],
            ShipsMayTouch: false);

        var coup = strategy.NextShot(history);

        Assert.Equal(0, (coup.X + coup.Y) % 2);   // retour en phase de chasse
    }

    [Fact]
    public void Deux_touches_alignees_font_prolonger_l_alignement()
    {
        // Deux touches verticalement adjacentes en (4,4) et (4,5) : le navire suit cet
        // axe. La stratégie doit viser une extrémité — (4,3) ou (4,6) — et jamais un
        // voisin perpendiculaire comme (3,4) ou (5,5), qui ne peut appartenir au navire.
        var history = Historique(
            new ShotRecord(new Coordinate(4, 4), ShotResult.Hit, Player.Opponent, null),
            new ShotRecord(new Coordinate(4, 5), ShotResult.Hit, Player.Opponent, null));

        for (var seed = 1; seed <= 20; seed++)
        {
            var coup = new HuntTargetStrategy(new Random(seed)).NextShot(history);

            Assert.Contains(coup, new[] { new Coordinate(4, 3), new Coordinate(4, 6) });
        }
    }
}
