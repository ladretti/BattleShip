using BattleShip.Models;

namespace BattleShip.API.Strategies;

/// <summary>
/// Resolves the <see cref="IOpponentStrategy"/> that matches a difficulty string carried
/// over the wire. The three accepted values are a contract with the front end (task 16)
/// enforced at the HTTP boundary by <c>CreateGameInputValidator</c>, the only place
/// responsible for rejecting any other value with a 400 before it can reach
/// <see cref="ForDifficulty"/>.
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
        "Easy" => new RandomStrategy(random),
        "Normal" => new HuntTargetStrategy(random),
        "Hard" => new DensityStrategy(random),
        // CreateGameInputValidator already rejects any other value with a 400 before an
        // endpoint can reach this factory: getting here is an anomaly, not a business
        // refusal (ADR 0004), hence an exception rather than a Result.
        _ => throw new ArgumentOutOfRangeException(
            nameof(difficulty), difficulty, "Unknown difficulty level.")
    };
}
