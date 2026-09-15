using BattleShip.Models;

namespace BattleShip.Tests.Domain;

public sealed class PlacementRulesTests
{
    private static readonly GameRules Small =
        new(GridSize: 5, Fleet: [new ShipTemplate("Destroyer", 2)],
            ShipsMayTouch: false, ExtraTurnOnHit: true);

    private static ShipPlacement Destroyer(int x, int y, Orientation o = Orientation.Horizontal)
        => new("Destroyer", new Coordinate(x, y), o, 2);

    [Fact]
    public void A_valid_placement_is_accepted()
    {
        var result = PlacementRules.Validate([Destroyer(0, 0)], Small);

        Assert.True(result.IsOk);
        Assert.Single(result.Value);
    }

    [Fact]
    public void A_ship_that_overflows_the_grid_is_rejected()
    {
        var result = PlacementRules.Validate([Destroyer(4, 0)], Small);

        Assert.False(result.IsOk);
        Assert.Equal(GameError.InvalidPlacement, result.Error);
    }

    [Fact]
    public void Two_overlapping_ships_are_rejected()
    {
        var rules = Small with
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
    [InlineData(0, 1)]   // below
    [InlineData(2, 0)]   // end to end
    [InlineData(2, 1)]   // diagonal
    public void Two_touching_ships_are_rejected(int x, int y)
    {
        var rules = Small with
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
    public void Two_touching_ships_are_accepted_if_the_rule_allows_it()
    {
        var rules = Small with
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
    public void An_incomplete_fleet_is_rejected()
    {
        var rules = Small with
        {
            Fleet = [new ShipTemplate("A", 2), new ShipTemplate("B", 2)]
        };

        var result = PlacementRules.Validate(
            [new ShipPlacement("A", new Coordinate(0, 0), Orientation.Horizontal, 2)],
            rules);

        Assert.False(result.IsOk);
    }
}
