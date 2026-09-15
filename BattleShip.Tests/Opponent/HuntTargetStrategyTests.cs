using BattleShip.API.Strategies;
using BattleShip.Models;

namespace BattleShip.Tests.Opponent;

public sealed class HuntTargetStrategyTests
{
    private static ShotHistory History(params ShotRecord[] shots) =>
        new(10, shots, [new ShipTemplate("Destroyer", 2)], [], ShipsMayTouch: false);

    [Fact]
    public void In_the_hunt_phase_it_only_aims_at_even_parity_cells()
    {
        var strategy = new HuntTargetStrategy(new Random(1));

        for (var i = 0; i < 40; i++)
        {
            var shot = strategy.NextShot(History());
            Assert.Equal(0, (shot.X + shot.Y) % 2);
        }
    }

    [Fact]
    public void After_a_hit_it_aims_at_an_adjacent_cell()
    {
        var strategy = new HuntTargetStrategy(new Random(1));
        var hit = new ShotRecord(new Coordinate(4, 4), ShotResult.Hit,
            Player.Opponent, null);

        var shot = strategy.NextShot(History(hit));

        var distance = Math.Abs(shot.X - 4) + Math.Abs(shot.Y - 4);
        Assert.Equal(1, distance);
    }

    [Fact]
    public void After_a_hit_in_a_corner_it_does_not_leave_the_grid()
    {
        var strategy = new HuntTargetStrategy(new Random(1));
        var hit = new ShotRecord(new Coordinate(0, 0), ShotResult.Hit,
            Player.Opponent, null);

        var shot = strategy.NextShot(History(hit));

        Assert.InRange(shot.X, 0, 9);
        Assert.InRange(shot.Y, 0, 9);
    }

    [Fact]
    public void A_sunk_ship_no_longer_triggers_the_target_phase()
    {
        var strategy = new HuntTargetStrategy(new Random(1));
        var history = new ShotHistory(
            10,
            [new ShotRecord(new Coordinate(4, 4), ShotResult.Hit, Player.Opponent, null),
             new ShotRecord(new Coordinate(4, 5), ShotResult.Sunk, Player.Opponent, "Destroyer")],
            [],
            [new Ship("Destroyer", 2, [new Coordinate(4, 4), new Coordinate(4, 5)])],
            ShipsMayTouch: false);

        var shot = strategy.NextShot(history);

        Assert.Equal(0, (shot.X + shot.Y) % 2);   // back to the hunt phase
    }

    [Fact]
    public void Two_aligned_hits_make_it_extend_the_line()
    {
        // Two vertically adjacent hits at (4,4) and (4,5): the ship follows that
        // axis. The strategy must aim at one end — (4,3) or (4,6) — and never at a
        // perpendicular neighbor such as (3,4) or (5,5), which cannot belong to the ship.
        var history = History(
            new ShotRecord(new Coordinate(4, 4), ShotResult.Hit, Player.Opponent, null),
            new ShotRecord(new Coordinate(4, 5), ShotResult.Hit, Player.Opponent, null));

        for (var seed = 1; seed <= 20; seed++)
        {
            var shot = new HuntTargetStrategy(new Random(seed)).NextShot(history);

            Assert.Contains(shot, new[] { new Coordinate(4, 3), new Coordinate(4, 6) });
        }
    }
}
