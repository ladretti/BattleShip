namespace BattleShip.Models;

/// <summary>
/// Abstraction d'accès à l'état des parties, indépendante de tout mécanisme de
/// stockage concret. Existe pour deux raisons : permettre aux tests d'intégration
/// d'injecter un store pré-rempli dans un état donné, et matérialiser la durée de
/// vie du store comme un choix explicite (une implémentation Singleton porte de
/// l'état partagé et doit donc gérer l'accès concurrent).
///
/// <see cref="Mutate{T}"/> est le SEUL point de mutation d'une partie déjà
/// enregistrée. Toute évolution future qui ferait Find, puis muterait l'objet
/// obtenu, puis Save, contournerait le verrou par partie : deux requêtes
/// simultanées sur la même partie pourraient alors muter le même objet en
/// parallèle (compteur de touches faussé, deux tirs acceptés sur la même case).
/// </summary>
public interface IGameStore
{
    Game? Find(Guid id);
    void Save(Game game);
    bool Remove(Guid id);

    /// <summary>
    /// Seul point de mutation d'une partie déjà enregistrée. Récupère la partie
    /// (échoue avec <see cref="GameError.GameNotFound"/> si elle est absente),
    /// applique <paramref name="change"/> sous le verrou propre à cette partie,
    /// puis renvoie son résultat. Ne pas contourner ce point d'entrée par un
    /// Find suivi d'une mutation directe et d'un Save : cela romprait la garantie
    /// d'exclusion mutuelle par partie.
    /// </summary>
    Result<T> Mutate<T>(Guid id, Func<Game, Result<T>> change);
}
