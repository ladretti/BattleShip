using BattleShip.Models;

namespace BattleShip.Tests.Domain;

public sealed class ShipTests
{
    private static Ship Destroyer() => new("Destroyer", 2,
        [new Coordinate(0, 0), new Coordinate(0, 1)]);

    [Fact]
    public void A_new_ship_is_not_sunk()
    {
        Assert.False(Destroyer().IsSunk);
    }

    [Fact]
    public void A_ship_hit_on_all_its_cells_is_sunk()
    {
        var ship = Destroyer();

        Assert.True(ship.TryHit(new Coordinate(0, 0)));
        Assert.False(ship.IsSunk);
        Assert.True(ship.TryHit(new Coordinate(0, 1)));
        Assert.True(ship.IsSunk);
    }

    [Fact]
    public void A_shot_on_a_cell_outside_the_ship_does_not_hit_it()
    {
        var ship = Destroyer();

        Assert.False(ship.TryHit(new Coordinate(5, 5)));
        Assert.False(ship.IsSunk);
    }

    [Fact]
    public void Firing_again_on_an_already_hit_cell_does_not_sink_the_ship()
    {
        var ship = Destroyer();
        ship.TryHit(new Coordinate(0, 0));
        ship.TryHit(new Coordinate(0, 0));

        Assert.False(ship.IsSunk);
    }

    [Fact]
    public void Firing_again_on_an_already_hit_cell_does_not_count_as_a_new_hit()
    {
        var ship = Destroyer();

        Assert.True(ship.TryHit(new Coordinate(0, 0)));
        Assert.False(ship.TryHit(new Coordinate(0, 0)));
    }
}
