using BattleShip.API.Strategies;
using BattleShip.Models;

namespace BattleShip.Tests.Opponent;

public sealed class DensityStrategyTests
{
    [Fact]
    public void On_an_empty_grid_it_aims_at_the_center_rather_than_a_corner()
    {
        var strategy = new DensityStrategy(new Random(1));
        var history = new ShotHistory(10, [], [new ShipTemplate("Battleship", 4)], [], false);

        var shot = strategy.NextShot(history);

        Assert.InRange(shot.X, 2, 7);
        Assert.InRange(shot.Y, 2, 7);
    }

    [Fact]
    public void It_extends_an_isolated_hit()
    {
        var strategy = new DensityStrategy(new Random(1));
        var history = new ShotHistory(
            10,
            [new ShotRecord(new Coordinate(4, 4), ShotResult.Hit, Player.Opponent, null)],
            [new ShipTemplate("Battleship", 4)], [], false);

        var shot = strategy.NextShot(history);

        Assert.Equal(1, Math.Abs(shot.X - 4) + Math.Abs(shot.Y - 4));
    }

    [Fact]
    public void Under_the_non_adjacency_rule_it_avoids_the_halo_of_a_sunk_ship()
    {
        var sunk = new Ship("Destroyer", 2,
            [new Coordinate(4, 4), new Coordinate(4, 5)]);
        var strategy = new DensityStrategy(new Random(1));
        var history = new ShotHistory(
            10,
            [new ShotRecord(new Coordinate(4, 4), ShotResult.Hit, Player.Opponent, null),
             new ShotRecord(new Coordinate(4, 5), ShotResult.Sunk, Player.Opponent, "Destroyer")],
            [new ShipTemplate("Battleship", 4)],
            [sunk],
            ShipsMayTouch: false);

        for (var i = 0; i < 20; i++)
        {
            var shot = new DensityStrategy(new Random(i + 1)).NextShot(history);
            var inTheHalo = sunk.Cells.Any(c =>
                Math.Abs(c.X - shot.X) <= 1 && Math.Abs(c.Y - shot.Y) <= 1);
            Assert.False(inTheHalo, $"shot {shot} inside the halo of the sunk ship");
        }
        _ = strategy;
    }
}
