using BattleShip.Models;

namespace BattleShip.Tests.Domaine;

public sealed class ShipTests
{
    private static Ship Torpilleur() => new("Torpilleur", 2,
        [new Coordinate(0, 0), new Coordinate(0, 1)]);

    [Fact]
    public void Un_navire_neuf_n_est_pas_coule()
    {
        Assert.False(Torpilleur().IsSunk);
    }

    [Fact]
    public void Un_navire_touche_sur_toutes_ses_cases_est_coule()
    {
        var ship = Torpilleur();

        Assert.True(ship.TryHit(new Coordinate(0, 0)));
        Assert.False(ship.IsSunk);
        Assert.True(ship.TryHit(new Coordinate(0, 1)));
        Assert.True(ship.IsSunk);
    }

    [Fact]
    public void Un_tir_a_cote_ne_touche_pas_le_navire()
    {
        var ship = Torpilleur();

        Assert.False(ship.TryHit(new Coordinate(5, 5)));
        Assert.False(ship.IsSunk);
    }

    [Fact]
    public void Retirer_sur_une_case_deja_touchee_ne_coule_pas_le_navire()
    {
        var ship = Torpilleur();
        ship.TryHit(new Coordinate(0, 0));
        ship.TryHit(new Coordinate(0, 0));

        Assert.False(ship.IsSunk);
    }

    [Fact]
    public void Retirer_sur_une_case_deja_touchee_ne_compte_pas_comme_une_nouvelle_touche()
    {
        var ship = Torpilleur();

        Assert.True(ship.TryHit(new Coordinate(0, 0)));
        Assert.False(ship.TryHit(new Coordinate(0, 0)));
    }
}
