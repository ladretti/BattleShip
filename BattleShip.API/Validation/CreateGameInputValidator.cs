using BattleShip.API.Contracts;
using FluentValidation;

namespace BattleShip.API.Validation;

/// <summary>
/// Validates POST /games. <see cref="KnownDifficulties"/> is the wire contract with the
/// front end (task 16): any other value is rejected here, with 400, before it can ever
/// reach <c>OpponentStrategyFactory.ForDifficulty</c>, which treats an unknown value as an
/// anomaly rather than a business refusal.
/// </summary>
public sealed class CreateGameInputValidator : AbstractValidator<CreateGameInput>
{
    private static readonly string[] KnownDifficulties = ["Easy", "Normal", "Hard"];

    public CreateGameInputValidator()
    {
        RuleFor(i => i.GridSize).InclusiveBetween(5, 20);

        RuleFor(i => i.Difficulty)
            .Must(difficulty => KnownDifficulties.Contains(difficulty, StringComparer.Ordinal))
            .WithMessage($"Difficulty must be one of: {string.Join(", ", KnownDifficulties)}.");
    }
}
