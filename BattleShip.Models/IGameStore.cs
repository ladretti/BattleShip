namespace BattleShip.Models;

public interface IGameStore
{
    Game? Find(Guid id);
    void Save(Game game);
    bool Remove(Guid id);

    Result<T> Mutate<T>(Guid id, Func<Game, Result<T>> change);

    Result<T> Read<T>(Guid id, Func<Game, T> projection);

    Result<IReadOnlyList<GameEvent>> ReadEvents(Guid id, int fromSequence);
}
