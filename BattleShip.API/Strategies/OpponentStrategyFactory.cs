using BattleShip.Models;
using BattleShip.Models.Contracts;

namespace BattleShip.API.Strategies;

public interface IOpponentStrategyFactory
{
    IOpponentStrategy ForDifficulty(string difficulty);
}

public sealed class OpponentStrategyFactory(Random random) : IOpponentStrategyFactory
{
    public IOpponentStrategy ForDifficulty(string difficulty) => difficulty switch
    {
        DifficultyLevels.Easy => new RandomStrategy(random),
        DifficultyLevels.Normal => new HuntTargetStrategy(random),
        DifficultyLevels.Hard => new DensityStrategy(random),

        _ => throw new ArgumentOutOfRangeException(
            nameof(difficulty), difficulty, "Unknown difficulty level.")
    };
}
