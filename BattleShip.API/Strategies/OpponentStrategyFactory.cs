using BattleShip.Models;
using BattleShip.Models.Contracts;

namespace BattleShip.API.Strategies;

/// <summary>
/// Resolves the <see cref="IOpponentStrategy"/> that matches a difficulty string carried
/// over the wire. See <see cref="DifficultyLevels"/> for the closed set of accepted
/// values, enforced at the HTTP boundary by <c>CreateGameInputValidator</c> before a
/// request can reach <see cref="ForDifficulty"/>.
/// </summary>
public interface IOpponentStrategyFactory
{
    IOpponentStrategy ForDifficulty(string difficulty);
}

/// <summary>
/// "Easy" → <see cref="RandomStrategy"/>, "Normal" → <see cref="HuntTargetStrategy"/>,
/// "Hard" → <see cref="DensityStrategy"/>. Randomness is injected through the
/// constructor, never through a hard-coded <c>Random.Shared</c> — same discipline as
/// <see cref="FleetPlacer"/> and every <see cref="IOpponentStrategy"/> implementation.
/// </summary>
public sealed class OpponentStrategyFactory(Random random) : IOpponentStrategyFactory
{
    public IOpponentStrategy ForDifficulty(string difficulty) => difficulty switch
    {
        DifficultyLevels.Easy => new RandomStrategy(random),
        DifficultyLevels.Normal => new HuntTargetStrategy(random),
        DifficultyLevels.Hard => new DensityStrategy(random),
        // CreateGameInputValidator already rejects any other value with a 400 before an
        // endpoint can reach this factory: getting here is an anomaly, not a business
        // refusal (ADR 0004), hence an exception rather than a Result.
        _ => throw new ArgumentOutOfRangeException(
            nameof(difficulty), difficulty, "Unknown difficulty level.")
    };
}
