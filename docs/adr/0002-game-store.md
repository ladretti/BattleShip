# ADR 0002 : `IGameStore` et verrou par partie

## Statut et date
Accepté — 2026-09-15

## Contexte

L'état des parties est en mémoire, et deux besoins s'y attachent. **Testabilité** : les tests
d'intégration doivent pouvoir injecter un store pré-rempli, sinon amener un endpoint sur une
partie dans un état donné oblige à passer par d'autres endpoints et le test échoue pour des causes
étrangères. **Concurrence** : le store vit en `Singleton` et `ConcurrentDictionary` protège **le
dictionnaire**, pas les `Game` qu'il contient — deux requêtes simultanées sur la même partie
mutent le même objet (compteur de touches faussé, deux tirs acceptés sur la même case). La
justification « on pourra changer de base » est **écartée** : c'est du YAGNI, la persistance est
du backlog.

## Options envisagées

- **A. `Game` mutable, verrou propre à chaque partie dans le store.** Une opération de mutation
  atomique prend un verrou indexé par `Guid` : simple à lire, testable, et les parties ne se
  bloquent pas entre elles.
- **B. `Game` immuable, remplacement atomique par `ConcurrentDictionary.TryUpdate`.** Plus
  élégant, historique de partie presque gratuit ; mais tout le moteur doit être écrit en style
  fonctionnel dès le départ, là où placement et tirs sont naturellement des mutations.
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
}
```

L'**interface** appartient à `BattleShip.Models`, l'**implémentation** à `BattleShip.API` :
`Models` reste un domaine pur sans dépendance, et `BattleShip.Tests` — qui référence `API` — peut
malgré tout exercer l'implémentation. Les signatures restent **synchrones** : rien n'est réellement
asynchrone dans un store en mémoire.

## Conséquences

- Toute modification passe par `Mutate`. Un endpoint qui ferait `Find` puis muterait puis `Save`
  contournerait le verrou : principal risque de régression, à surveiller en revue de code.
- Les parties sont indépendantes : le verrou est indexé par `Guid`, pas global.
- `Mutate` renvoie un `Result<T>` (ADR 0004) : section critique courte et sans exception.
- `Find` reste exposé pour les lectures pures, où une vue légèrement décalée est sans conséquence.

## Vérification et réexamen

- Test de concurrence : N tâches tirent simultanément sur la même case ; exactement une réussit,
  les autres reçoivent `CellAlreadyShot`. Il échoue si le verrou est absent ou mal indexé, et doit
  être exécuté plusieurs fois — une course qui passe une fois ne prouve rien (`REVUE-IA.md`,
  revue 3). La course `Read` / `Mutate` a été mesurée au niveau du store, verrou retiré puis
  rétabli (`REVUE-IA.md`, revue 4 ; détail dans l'ADR 0005).
- Test d'injection d'un store pré-rempli dans `WebApplicationFactory`.
- À réexaminer si la persistance quittait le backlog : les signatures synchrones seraient alors à
  reconsidérer.

## Références

- Spec de conception § 3
- `CLAUDE.md` § 3 bis (Repository) et § Injection de dépendances
