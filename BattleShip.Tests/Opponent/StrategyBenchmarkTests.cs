using BattleShip.API.Benchmark;
using BattleShip.Models;

namespace BattleShip.Tests.Opponent;

public sealed class StrategyBenchmarkTests
{
    [Fact]
    public void The_three_levels_are_ordered_by_efficiency()
    {
        var results = StrategyBenchmark.Run(GameRules.Default, games: 200, seed: 20260915)
            .ToDictionary(r => r.StrategyName);

        var random = results["Random"].AverageShots;
        var huntTarget = results["HuntTarget"].AverageShots;
        var density = results["Density"].AverageShots;

        Assert.True(random > huntTarget,
            $"random {random:F1} should be worse than hunt/target {huntTarget:F1}");
        Assert.True(huntTarget > density,
            $"hunt/target {huntTarget:F1} should be worse than density {density:F1}");
        Assert.True(density < 55,
            $"density measured at {density:F1} shots, expected under 55");
    }

    [Fact]
    public void The_measurement_is_reproducible_for_an_equal_seed()
    {
        var a = StrategyBenchmark.Run(GameRules.Default, 50, 1);
        var b = StrategyBenchmark.Run(GameRules.Default, 50, 1);

        Assert.Equal(a.Select(r => r.AverageShots), b.Select(r => r.AverageShots));
    }
}
