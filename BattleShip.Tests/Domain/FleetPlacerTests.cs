using BattleShip.Models;

namespace BattleShip.Tests.Domain;

public sealed class FleetPlacerTests
{
    public static TheoryData<int> Seeds()
    {
        var data = new TheoryData<int>();
        for (var seed = 1; seed <= 50; seed++) data.Add(seed);
        return data;
    }

    [Theory]
    [MemberData(nameof(Seeds))]
    public void An_automatic_placement_always_respects_the_rules(int seed)
    {
        var placer = new FleetPlacer(new Random(seed));

        var result = placer.PlaceAll(GameRules.Default);

        Assert.True(result.IsOk);
        var cells = result.Value.SelectMany(s => s.Cells).ToList();

        // No cell outside the grid.
        Assert.All(cells, c =>
        {
            Assert.InRange(c.X, 0, GameRules.Default.GridSize - 1);
            Assert.InRange(c.Y, 0, GameRules.Default.GridSize - 1);
        });

        // No overlap.
        Assert.Equal(cells.Count, cells.Distinct().Count());

        // No adjacency between two distinct ships.
        foreach (var a in result.Value)
            foreach (var b in result.Value.Where(x => !ReferenceEquals(x, a)))
                foreach (var ca in a.Cells)
                    foreach (var cb in b.Cells)
                        Assert.False(
                            Math.Abs(ca.X - cb.X) <= 1 && Math.Abs(ca.Y - cb.Y) <= 1,
                            $"adjacent ships at {ca} and {cb} (seed {seed})");
    }

    [Fact]
    public void Two_identical_seeds_produce_the_same_placement()
    {
        var a = new FleetPlacer(new Random(12345)).PlaceAll(GameRules.Default);
        var b = new FleetPlacer(new Random(12345)).PlaceAll(GameRules.Default);

        Assert.Equal(
            a.Value.SelectMany(s => s.Cells).ToList(),
            b.Value.SelectMany(s => s.Cells).ToList());
    }

    [Fact]
    public void A_fleet_that_does_not_fit_in_the_grid_is_rejected_without_looping()
    {
        var impossible = new GameRules(
            GridSize: 3,
            Fleet: [new ShipTemplate("A", 3), new ShipTemplate("B", 3),
                    new ShipTemplate("C", 3)],
            ShipsMayTouch: false,
            ExtraTurnOnHit: true);

        var result = new FleetPlacer(new Random(1)).PlaceAll(impossible);

        Assert.False(result.IsOk);
        Assert.Equal(GameError.InvalidPlacement, result.Error);
    }
}
