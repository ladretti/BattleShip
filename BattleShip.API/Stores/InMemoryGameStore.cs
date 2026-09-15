using System.Collections.Concurrent;
using BattleShip.Models;

namespace BattleShip.API.Stores;

/// <summary>
/// Implémentation en mémoire de <see cref="IGameStore"/>, pensée pour un
/// enregistrement Singleton (une instance partagée par toute l'application).
///
/// Le ConcurrentDictionary des parties protège le dictionnaire lui-même — pas les
/// objets Game qu'il contient. Deux requêtes simultanées sur la même partie (un
/// double-clic du joueur, une requête gRPC qui croise une requête HTTP) muteraient
/// sinon le même objet en parallèle. D'où le second dictionnaire, un verrou par
/// identifiant de partie : Mutate prend le verrou de LA partie visée, jamais un
/// verrou global, afin que deux parties distinctes ne se bloquent jamais l'une
/// l'autre.
/// </summary>
public sealed class InMemoryGameStore : IGameStore
{
    private readonly ConcurrentDictionary<Guid, Game> _games = new();
    private readonly ConcurrentDictionary<Guid, object> _locks = new();

    public Game? Find(Guid id) => _games.TryGetValue(id, out var game) ? game : null;

    public void Save(Game game) => _games[game.Id] = game;

    public bool Remove(Guid id) => _games.TryRemove(id, out _);

    public Result<T> Mutate<T>(Guid id, Func<Game, Result<T>> change)
    {
        if (!_games.TryGetValue(id, out var game))
            return Result<T>.Fail(GameError.GameNotFound);

        var gameLock = _locks.GetOrAdd(id, static _ => new object());
        lock (gameLock)
        {
            return change(game);
        }
    }
}
