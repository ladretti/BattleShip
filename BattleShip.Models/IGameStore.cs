namespace BattleShip.Models;

/// <summary>
/// Abstraction for accessing game state, independent of any concrete storage
/// mechanism. It exists for two reasons: to let integration tests inject a store
/// pre-filled into a given state, and to make the store's lifetime an explicit
/// choice (a Singleton implementation carries shared state and must therefore handle
/// concurrent access).
///
/// <see cref="Mutate{T}"/> is the ONLY mutation point of a game that is already
/// stored. Any future change that would Find, then mutate the object obtained, then
/// Save, would bypass the per-game lock: two simultaneous requests on the same game
/// could then mutate the same object in parallel (skewed hit counter, two shots
/// accepted on the same cell).
/// </summary>
public interface IGameStore
{
    Game? Find(Guid id);
    void Save(Game game);
    bool Remove(Guid id);

    /// <summary>
    /// The only mutation point of a game that is already stored. Retrieves the game
    /// (fails with <see cref="GameError.GameNotFound"/> if it is absent), applies
    /// <paramref name="change"/> under the lock belonging to that game, then returns
    /// its result. Do not bypass this entry point with a Find followed by a direct
    /// mutation and a Save: that would break the per-game mutual exclusion
    /// guarantee.
    /// </summary>
    Result<T> Mutate<T>(Guid id, Func<Game, Result<T>> change);
}
