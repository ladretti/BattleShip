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

    /// <summary>
    /// Un verrou par identifiant de partie, jamais un verrou global : Mutate ne doit
    /// bloquer que les appels concurrents visant LA MÊME partie.
    ///
    /// Ce dictionnaire ne purge jamais ses entrées, y compris quand la partie
    /// correspondante est retirée par Remove. C'est délibéré, pas un oubli : purger
    /// l'entrée au moment du Remove romprait l'exclusion mutuelle qu'elle est censée
    /// garantir. Si un Mutate est déjà en vol avec l'objet de verrou existant au
    /// moment où Remove le supprimerait, un Mutate qui arriverait juste après
    /// obtiendrait, via GetOrAdd, un SECOND objet pour le même identifiant — deux
    /// verrous distincts pour la même partie, donc plus d'exclusion mutuelle du tout :
    /// exactement la course que cette tâche a pour but de fermer. Une purge sûre
    /// demanderait un comptage de références (ne libérer l'entrée que lorsque plus
    /// aucun Mutate ne la détient), ce qui est disproportionné ici.
    ///
    /// La croissance non bornée qui en résulte est acceptée en connaissance de cause :
    /// les Guid de parties ne sont jamais réutilisés, et l'état complet du store est
    /// en mémoire pour la durée d'une session serveur (pas de persistance, redémarrage
    /// = table vide). Ne pas « corriger » cette fuite sans revoir cette justification.
    /// </summary>
    private readonly ConcurrentDictionary<Guid, object> _locks = new();

    /// <summary>
    /// Renvoie l'instance de <see cref="Game"/> réellement détenue par le store — pas
    /// une copie. Cette instance reste mutable et vivante : elle peut changer sous les
    /// pieds de l'appelant si une mutation concurrente survient via <see cref="Mutate{T}"/>.
    ///
    /// Find est réservé aux LECTURES (typiquement une projection vers un DTO en sortie
    /// d'un endpoint de consultation d'état ou d'historique). Appeler une méthode qui
    /// mute la partie obtenue ici — <c>store.Find(id)!.PlayerFires(...)</c> par exemple —
    /// contournerait intégralement le verrou par partie que <see cref="Mutate{T}"/>
    /// fait respecter : deux appelants pourraient alors muter le même objet en
    /// parallèle sans aucune exclusion. Toute mutation doit passer par
    /// <see cref="Mutate{T}"/>, jamais par le résultat de Find.
    /// </summary>
    public Game? Find(Guid id) => _games.TryGetValue(id, out var game) ? game : null;

    public void Save(Game game) => _games[game.Id] = game;

    public bool Remove(Guid id) => _games.TryRemove(id, out _);

    public Result<T> Mutate<T>(Guid id, Func<Game, Result<T>> change)
    {
        // La lecture de la partie se fait SOUS le verrou, pas avant de le prendre :
        // si elle avait lieu avant, un Save concurrent pourrait remplacer l'entrée du
        // dictionnaire entre la lecture et la prise du verrou, et deux appels
        // muteraient alors deux instances différentes tout en croyant partager la
        // même exclusion (le verrou est indexé par Guid, pas par référence d'objet).
        var gameLock = _locks.GetOrAdd(id, static _ => new object());
        lock (gameLock)
        {
            if (!_games.TryGetValue(id, out var game))
                return Result<T>.Fail(GameError.GameNotFound);

            return change(game);
        }
    }
}
