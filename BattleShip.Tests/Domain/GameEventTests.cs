using BattleShip.Models;

namespace BattleShip.Tests.Domain;

public sealed class GameEventTests
{
    [Fact]
    public void A_shot_fired_event_carries_the_outcome_and_not_only_the_intent()
    {
        var fired = new ShotFired(7, new Coordinate(3, 4), Player.Human, ShotResult.Sunk, "Destroyer");

        Assert.Equal(7, fired.Sequence);
        Assert.Equal(new Coordinate(3, 4), fired.At);
        Assert.Equal(Player.Human, fired.By);
        Assert.Equal(ShotResult.Sunk, fired.Result);
        Assert.Equal("Destroyer", fired.SunkShipName);
    }

    [Fact]
    public void Events_are_compared_by_value()
    {
        var first = new ShotFired(1, new Coordinate(0, 0), Player.Human, ShotResult.Miss, null);
        var second = new ShotFired(1, new Coordinate(0, 0), Player.Human, ShotResult.Miss, null);

        Assert.Equal(first, second);
    }

    [Fact]
    public void Every_event_is_a_game_event()
    {
        var rules = GameRules.Default;
        IReadOnlyList<ShipSnapshot> fleet =
            [.. new FleetPlacer(new Random(11)).PlaceAll(rules).Value.Select(ShipSnapshot.Of)];

        GameEvent[] events =
        [
            new GameCreated(0, rules, fleet, "Normal"),
            new HumanFleetPlaced(1, fleet),
            new ShotFired(2, new Coordinate(0, 0), Player.Human, ShotResult.Miss, null),
            new GameEnded(3, Player.Human)
        ];

        Assert.Equal([0, 1, 2, 3], events.Select(e => e.Sequence));
    }

    [Fact]
    public void A_snapshot_does_not_share_hit_state_with_the_ship_it_came_from()
    {
        var ship = new Ship("Destroyer", 2, [new Coordinate(0, 0), new Coordinate(0, 1)]);
        var snapshot = ShipSnapshot.Of(ship);

        ship.TryHit(new Coordinate(0, 0));

        var rebuilt = snapshot.ToShip();

        Assert.True(ship.HitCells.Count == 1);
        Assert.Empty(rebuilt.HitCells);
        Assert.Equal(ship.Cells, rebuilt.Cells);
    }

    [Fact]
    public void Two_ships_rebuilt_from_the_same_snapshot_are_independent()
    {
        var snapshot = new ShipSnapshot(
            "Destroyer", 2, [new Coordinate(0, 0), new Coordinate(0, 1)]);

        var first = snapshot.ToShip();
        var second = snapshot.ToShip();

        first.TryHit(new Coordinate(0, 0));

        Assert.Single(first.HitCells);
        Assert.Empty(second.HitCells);
    }
}
