# ADR 0002 : `IGameStore` et verrou par partie

## Statut et date
Accepté — 2026-09-15

## Contexte

L'état des parties est en mémoire, et deux besoins s'y attachent. **Testabilité** : les tests
d'intégration doivent pouvoir injecter un store pré-rempli, sinon amener un endpoint sur une
partie dans un état donné oblige à passer par d'autres endpoints et le test échoue pour des
causes étrangères. **Concurrence** : le store vit en `Singleton` et `ConcurrentDictionary`
protège **le dictionnaire**, pas les `Game` qu'il contient — deux requêtes simultanées sur la même
partie mutent le même objet (compteur faussé, deux tirs sur la même case). La justification « on
pourra changer de base » est **écartée** : c'est du YAGNI, la persistance est du backlog.

## Options envisagées

- **A. `Game` mutable, verrou propre à chaque partie dans le store.** Une mutation atomique prend
  un verrou indexé par `Guid` : simple à lire, testable, parties indépendantes entre elles.
- **B. `Game` immuable, remplacement atomique par `ConcurrentDictionary.TryUpdate`.** Plus
  élégant, mais tout le moteur doit être écrit en style fonctionnel dès le départ.
- **C. Ne rien faire et documenter la course.** Défendable pour un TP mono-joueur, mais le jury
  voit un `Singleton` partagé et posera la question.

## Décision

Option A.

```csharp
public interface IGameStore
{
    Game? Find(Guid id);
    void Save(Game game);
    bool Remove(Guid id);
    Result<T> Mutate<T>(Guid id, Func<Game, Result<T>> change);
    Result<T> Read<T>(Guid id, Func<Game, T> projection);
}
```

L'**interface** appartient à `BattleShip.Models`, l'**implémentation** à `BattleShip.API` : `Models`
reste un domaine pur et `BattleShip.Tests`, qui référence `API`, exerce malgré tout
l'implémentation. Signatures **synchrones** : rien n'est asynchrone dans un store en mémoire.

## Conséquences

- Toute modification passe par `Mutate`. Un endpoint qui ferait `Find` puis muterait puis `Save`
  contournerait le verrou : principal risque de régression, à surveiller en revue de code.
- Les parties sont indépendantes : le verrou est indexé par `Guid`, pas global.
- `Mutate` renvoie un `Result<T>` (ADR 0004) : section critique courte et sans exception.
- `Find` est réservé à un test d'existence ou à la lecture d'un scalaire unique. **Toute
  projection qui énumère `Game.History` ou `Board.ReceivedShots` passe par `Read<T>`**, qui prend
  le même verrou par partie que `Mutate` — sans quoi l'énumération court contre une mutation
  concurrente (`ArgumentException` reproduite 5 fois sur 5, `REVUE-IA.md`, revue 4).
- Le dictionnaire de verrous n'est **jamais purgé**, pas même par `Remove` : délibéré. Purger
  l'entrée laisserait un `GetOrAdd` concurrent distribuer un **second** objet de verrou pour le
  même identifiant — deux verrous pour une partie, donc plus d'exclusion mutuelle. La croissance
  est acceptée : les `Guid` ne sont jamais réutilisés et le store ne vit que le temps du processus.

## Vérification et réexamen

- Test de concurrence : N tâches tirent simultanément sur la même case ; exactement une réussit,
  les autres reçoivent `CellAlreadyShot`. Il échoue si le verrou est absent ou mal indexé, et doit
  être rejoué plusieurs fois — une course qui passe une fois ne prouve rien (`REVUE-IA.md`,
  revue 3). La course `Read` / `Mutate` est mesurée au niveau du store, verrou retiré puis rétabli
  (revue 4 ; détail dans l'ADR 0005). Un store pré-rempli est injecté dans `WebApplicationFactory`.
- À réexaminer si la persistance quittait le backlog : signatures synchrones à reconsidérer.

## Références

- Spec de conception § 3 ; `CLAUDE.md` § 3 bis (Repository) et § Injection de dépendances
