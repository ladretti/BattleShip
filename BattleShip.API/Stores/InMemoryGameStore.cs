using System.Collections.Concurrent;
using BattleShip.Models;

namespace BattleShip.API.Stores;

public sealed class InMemoryGameStore : IGameStore
{
    private readonly ConcurrentDictionary<Guid, Game> _games = new();

    private readonly ConcurrentDictionary<Guid, object> _locks = new();

    public Game? Find(Guid id) => _games.TryGetValue(id, out var game) ? game : null;

    public void Save(Game game) => _games[game.Id] = game;

    public bool Remove(Guid id) => _games.TryRemove(id, out _);

    public Result<T> Mutate<T>(Guid id, Func<Game, Result<T>> change)
    {
        var gameLock = _locks.GetOrAdd(id, static _ => new object());
        lock (gameLock)
        {
            if (!_games.TryGetValue(id, out var game))
                return Result<T>.Fail(GameError.GameNotFound);

            return change(game);
        }
    }

    public Result<T> Read<T>(Guid id, Func<Game, T> projection)
    {
        var gameLock = _locks.GetOrAdd(id, static _ => new object());
        lock (gameLock)
        {
            if (!_games.TryGetValue(id, out var game))
                return Result<T>.Fail(GameError.GameNotFound);

            return Result<T>.Ok(projection(game));
        }
    }
}
