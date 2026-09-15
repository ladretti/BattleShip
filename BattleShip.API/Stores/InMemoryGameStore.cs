using System.Collections.Concurrent;
using BattleShip.Models;

namespace BattleShip.API.Stores;

/// <summary>
/// In-memory implementation of <see cref="IGameStore"/>, designed for a Singleton
/// registration (a single instance shared by the whole application).
///
/// The ConcurrentDictionary of games protects the dictionary itself — not the Game
/// objects it holds. Two concurrent requests on the same game (a double-click from the
/// player, a gRPC request crossing an HTTP request) would otherwise mutate the same
/// object in parallel. Hence the second dictionary, one lock per game identifier:
/// Mutate takes the lock of THE targeted game, never a global lock, so that two
/// distinct games never block one another.
/// </summary>
public sealed class InMemoryGameStore : IGameStore
{
    private readonly ConcurrentDictionary<Guid, Game> _games = new();

    /// <summary>
    /// One lock per game identifier, never a global lock: Mutate must only block the
    /// concurrent calls targeting THE SAME game.
    ///
    /// This dictionary never purges its entries, including when the corresponding game
    /// is removed by Remove. That is deliberate, not an oversight: purging the entry at
    /// Remove time would break the mutual exclusion it is supposed to guarantee. If a
    /// Mutate is already in flight with the existing lock object at the moment Remove
    /// would delete it, a Mutate arriving just afterwards would obtain, through GetOrAdd,
    /// a SECOND object for the same identifier — two distinct locks for the same game, so
    /// no mutual exclusion at all: exactly the race this task is meant to close. A safe
    /// purge would require reference counting (releasing the entry only once no Mutate
    /// holds it any more), which is out of proportion here.
    ///
    /// The unbounded growth that results is accepted knowingly: game Guids are never
    /// reused, and the entire state of the store is in memory for the lifetime of a
    /// server session (no persistence, restart = empty table). Do not "fix" this leak
    /// without revisiting this justification.
    /// </summary>
    private readonly ConcurrentDictionary<Guid, object> _locks = new();

    /// <summary>
    /// Returns the <see cref="Game"/> instance actually held by the store — not a copy.
    /// That instance stays mutable and live: it may change under the caller's feet if a
    /// concurrent mutation happens through <see cref="Mutate{T}"/>.
    ///
    /// Find is reserved for READS (typically a projection to a DTO on the way out of a
    /// state or history query endpoint). Calling a method that mutates the game obtained
    /// here — <c>store.Find(id)!.PlayerFires(...)</c> for instance — would entirely
    /// bypass the per-game lock that <see cref="Mutate{T}"/> enforces: two callers could
    /// then mutate the same object in parallel without any exclusion at all. Every
    /// mutation must go through <see cref="Mutate{T}"/>, never through the result of Find.
    /// </summary>
    public Game? Find(Guid id) => _games.TryGetValue(id, out var game) ? game : null;

    public void Save(Game game) => _games[game.Id] = game;

    public bool Remove(Guid id) => _games.TryRemove(id, out _);

    public Result<T> Mutate<T>(Guid id, Func<Game, Result<T>> change)
    {
        // The game is read UNDER the lock, not before taking it: were it read before, a
        // concurrent Save could replace the dictionary entry between the read and the
        // lock acquisition, and two calls would then mutate two different instances while
        // believing they share the same exclusion (the lock is keyed by Guid, not by
        // object reference).
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
        // Same lock as Mutate, keyed the same way (by Guid, obtained under the lock via
        // GetOrAdd — see Mutate's own remarks on why): a projection that enumerates
        // History/ReceivedShots must never run concurrently with a Mutate appending to
        // them, and this is the only way to guarantee that with a single per-game lock.
        var gameLock = _locks.GetOrAdd(id, static _ => new object());
        lock (gameLock)
        {
            if (!_games.TryGetValue(id, out var game))
                return Result<T>.Fail(GameError.GameNotFound);

            return Result<T>.Ok(projection(game));
        }
    }
}
