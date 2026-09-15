using BattleShip.API.Benchmark;
using FluentValidation;

namespace BattleShip.API.Validation;

/// <summary>
/// Validates the input of the AI duel exposed on POST /benchmark. Capped at 1000 games
/// per strategy: beyond that, the duration of the HTTP request becomes unreasonable
/// (three strategies played sequentially, each one until the fleet is sunk).
/// </summary>
public sealed class BenchmarkInputValidator : AbstractValidator<BenchmarkInput>
{
    public BenchmarkInputValidator()
    {
        RuleFor(i => i.Games).InclusiveBetween(1, 1000);
    }
}
