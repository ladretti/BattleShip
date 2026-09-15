using BattleShip.Models;

namespace BattleShip.Tests.Domaine;

public sealed class PlacementRulesTests
{
    private static readonly GameRules Petite =
        new(GridSize: 5, Fleet: [new ShipTemplate("Torpilleur", 2)],
            ShipsMayTouch: false, ExtraTurnOnHit: true);

    private static ShipPlacement Torpilleur(int x, int y, Orientation o = Orientation.Horizontal)
        => new("Torpilleur", new Coordinate(x, y), o, 2);

    [Fact]
    public void Un_placement_valide_est_accepte()
    {
        var result = PlacementRules.Validate([Torpilleur(0, 0)], Petite);

        Assert.True(result.IsOk);
        Assert.Single(result.Value);
    }

    [Fact]
    public void Un_navire_qui_deborde_la_grille_est_refuse()
    {
        var result = PlacementRules.Validate([Torpilleur(4, 0)], Petite);

        Assert.False(result.IsOk);
        Assert.Equal(GameError.InvalidPlacement, result.Error);
    }

    [Fact]
    public void Deux_navires_qui_se_chevauchent_sont_refuses()
    {
        var rules = Petite with
        {
            Fleet = [new ShipTemplate("A", 2), new ShipTemplate("B", 2)]
        };
        var placements = new[]
        {
            new ShipPlacement("A", new Coordinate(0, 0), Orientation.Horizontal, 2),
            new ShipPlacement("B", new Coordinate(1, 0), Orientation.Horizontal, 2)
        };

        var result = PlacementRules.Validate(placements, rules);

        Assert.False(result.IsOk);
    }

    [Theory]
    [InlineData(0, 1)]   // dessous
    [InlineData(2, 0)]   // bout à bout
    [InlineData(2, 1)]   // diagonale
    public void Deux_navires_qui_se_touchent_sont_refuses(int x, int y)
    {
        var rules = Petite with
        {
            Fleet = [new ShipTemplate("A", 2), new ShipTemplate("B", 2)]
        };
        var placements = new[]
        {
            new ShipPlacement("A", new Coordinate(0, 0), Orientation.Horizontal, 2),
            new ShipPlacement("B", new Coordinate(x, y), Orientation.Horizontal, 2)
        };

        var result = PlacementRules.Validate(placements, rules);

        Assert.False(result.IsOk);
    }

    [Fact]
    public void Deux_navires_qui_se_touchent_sont_acceptes_si_la_regle_l_autorise()
    {
        var rules = Petite with
        {
            Fleet = [new ShipTemplate("A", 2), new ShipTemplate("B", 2)],
            ShipsMayTouch = true
        };
        var placements = new[]
        {
            new ShipPlacement("A", new Coordinate(0, 0), Orientation.Horizontal, 2),
            new ShipPlacement("B", new Coordinate(0, 1), Orientation.Horizontal, 2)
        };

        var result = PlacementRules.Validate(placements, rules);

        Assert.True(result.IsOk);
    }

    [Fact]
    public void Une_flotte_incomplete_est_refusee()
    {
        var rules = Petite with
        {
            Fleet = [new ShipTemplate("A", 2), new ShipTemplate("B", 2)]
        };

        var result = PlacementRules.Validate(
            [new ShipPlacement("A", new Coordinate(0, 0), Orientation.Horizontal, 2)],
            rules);

        Assert.False(result.IsOk);
    }
}
