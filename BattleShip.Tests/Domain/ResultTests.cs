using BattleShip.Models;

namespace BattleShip.Tests.Domain;

public sealed class ResultTests
{
    [Fact]
    public void An_ok_result_carries_its_value()
    {
        var result = Result<int>.Ok(42);

        Assert.True(result.IsOk);
        Assert.Equal(42, result.Value);
    }

    [Fact]
    public void A_failed_result_carries_its_error()
    {
        var result = Result<int>.Fail(GameError.CellAlreadyShot);

        Assert.False(result.IsOk);
        Assert.Equal(GameError.CellAlreadyShot, result.Error);
    }

    [Fact]
    public void Reading_the_value_of_a_failed_result_is_an_anomaly()
    {
        var result = Result<int>.Fail(GameError.OutOfBounds);

        Assert.Throws<InvalidOperationException>(() => _ = result.Value);
    }

    [Fact]
    public void A_default_result_is_not_a_success()
    {
        Result<int> result = default;

        Assert.False(result.IsOk);
    }

    [Fact]
    public void Reading_the_value_of_a_default_result_is_an_anomaly()
    {
        Result<int> result = default;

        Assert.Throws<InvalidOperationException>(() => _ = result.Value);
    }

    [Fact]
    public void Reading_the_error_of_a_default_result_is_an_anomaly()
    {
        Result<int> result = default;

        Assert.Throws<InvalidOperationException>(() => _ = result.Error);
    }

    [Fact]
    public void Reading_the_error_of_a_successful_result_is_an_anomaly()
    {
        var result = Result<int>.Ok(1);

        Assert.Throws<InvalidOperationException>(() => _ = result.Error);
    }

    [Fact]
    public void An_uninitialized_array_of_results_contains_no_success()
    {
        var results = new Result<string>[3];

        Assert.All(results, r => Assert.False(r.IsOk));
    }
}
