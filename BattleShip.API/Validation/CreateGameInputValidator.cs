using BattleShip.Models.Contracts;
using BattleShip.API.Strategies;
using FluentValidation;

namespace BattleShip.API.Validation;

public sealed class CreateGameInputValidator : AbstractValidator<CreateGameInput>
{
    public CreateGameInputValidator()
    {
        RuleFor(i => i.GridSize).InclusiveBetween(7, 20);

        RuleFor(i => i.Difficulty)
            .Must(difficulty => DifficultyLevels.All.Contains(difficulty, StringComparer.Ordinal))
            .WithMessage($"Difficulty must be one of: {string.Join(", ", DifficultyLevels.All)}.");
    }
}
