using BattleShip.API.Benchmark;
using FluentValidation;

namespace BattleShip.API.Validation;

public sealed class BenchmarkInputValidator : AbstractValidator<BenchmarkInput>
{
    public BenchmarkInputValidator()
    {
        RuleFor(i => i.Games).InclusiveBetween(1, 1000);
    }
}
