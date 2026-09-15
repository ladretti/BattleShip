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
///
/// The symmetric rule holds for reads: any read that ENUMERATES part of a game's state
/// (<see cref="Game.History"/>, a board's ship or received-shot collections — anything
/// projected to a DTO) must go through <see cref="Read{T}"/>, not <see cref="Find"/>.
/// <see cref="Game.History"/> is a plain <c>List</c> and a board's received-shot set is a
/// plain <c>HashSet</c>; enumerating either one concurrently with a
/// <see cref="Mutate{T}"/> that is appending/inserting into it throws
/// <see cref="InvalidOperationException"/> ("Collection was modified"), which would
/// surface as an unhandled 500 at an HTTP or gRPC boundary. Nothing mutates a stored game
/// concurrently yet (no route fires a shot), so this cannot be observed today — but the
/// day one does (a gRPC <c>Fire</c> call mutating <see cref="Game.History"/> on every
/// shot, several in a row when the opponent chains hits), a read of the same game racing
/// against it will. <see cref="Find"/> remains reserved for uses that only test existence
/// or read a single scalar property (<c>Guid</c>, an enum, ...) — never one that walks a
/// collection.
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

    /// <summary>
    /// The read-side counterpart of <see cref="Mutate{T}"/>: retrieves the game (fails
    /// with <see cref="GameError.GameNotFound"/> if it is absent), applies
    /// <paramref name="projection"/> under the SAME per-game lock <see cref="Mutate{T}"/>
    /// uses, then returns its result. Required for any projection that enumerates part of
    /// the game's state — see the interface's own remarks for why a plain
    /// <see cref="Find"/> is not safe for that purpose once a game can be mutated
    /// concurrently with a read of it.
    /// </summary>
    Result<T> Read<T>(Guid id, Func<Game, T> projection);
}
