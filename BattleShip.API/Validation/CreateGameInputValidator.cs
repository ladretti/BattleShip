using BattleShip.Models.Contracts;
using BattleShip.API.Strategies;
using FluentValidation;

namespace BattleShip.API.Validation;

/// <summary>
/// Validates POST /games. <see cref="DifficultyLevels.All"/> is the single source of
/// truth for the accepted difficulty names, shared with <c>OpponentStrategyFactory</c> so
/// the two cannot silently drift apart: any value outside it is rejected here, with 400,
/// before it can ever reach <c>OpponentStrategyFactory.ForDifficulty</c>, which treats an
/// unknown value as an anomaly rather than a business refusal.
///
/// The lower bound of 7 on <c>GridSize</c> is not an arbitrary margin below the default
/// fleet's longest ship (5): it was verified empirically (see the task 13 correction
/// report) that the default fleet, under the non-adjacency rule, admits ZERO legal
/// placement on a 5x5 or 6x6 grid — <see cref="BattleShip.Models.FleetPlacer.PlaceAll"/>
/// fails deterministically, not occasionally, for those sizes — while a 7x7 grid already
/// admits at least one. The bound depends on the fleet's total footprint plus the halo
/// each ship's non-adjacency rule reserves around it, not on any single ship's length.
/// </summary>
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
