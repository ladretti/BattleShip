# ADR 0002 : `IGameStore` et verrou par partie

## Statut et date
Accepté — 2026-09-15

## Contexte

L'état des parties est en mémoire. Deux besoins s'y attachent.

**Testabilité.** Les tests d'intégration doivent pouvoir injecter un store pré-rempli. Sans
abstraction, amener un endpoint sur une partie dans un état donné oblige à passer par
d'autres endpoints : le test ne mesure plus ce qu'il prétend mesurer et échoue pour des
causes étrangères.

**Concurrence.** Le store porte de l'état partagé et vit en `Singleton`. `ConcurrentDictionary`
protège **le dictionnaire**, pas les `Game` qu'il contient. Deux requêtes simultanées sur la
même partie — un double-clic, ou une requête gRPC qui croise une requête HTTP — mutent le
même objet en parallèle : compteur de touches faussé, deux tirs acceptés sur la même case.

La justification « on pourra changer de base de données » est **écartée** : c'est du YAGNI,
l'état est en mémoire et la persistance est du backlog.

## Options envisagées

**A. `Game` mutable, verrou propre à chaque partie dans le store.**
Le store expose une opération de mutation atomique qui prend un verrou indexé par `Guid`.
Simple à écrire et à lire, testable directement. Les parties ne se bloquent pas entre elles.

**B. `Game` immuable, remplacement atomique par `ConcurrentDictionary.TryUpdate`.**
Chaque coup produit un nouveau `Game` ; le store fait un compare-and-swap et réessaie ou
refuse en cas de conflit. Plus élégant, et l'historique de partie devient presque gratuit.
Coût : tout le moteur doit être écrit en style fonctionnel dès le départ, ce qui pèse sur un
domaine où le placement et les tirs sont naturellement des mutations.

**C. Ne rien faire et documenter la course.**
Défendable pour un TP mono-joueur où une seule fenêtre joue. Mais le jury voit un `Singleton`
partagé et posera la question ; l'occasion de traiter le sujet proprement serait manquée.

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

L'**interface** appartient à `BattleShip.Models`, l'**implémentation** à `BattleShip.API`.
`Models` reste ainsi un domaine pur sans dépendance, et `BattleShip.Tests` — qui référence
`API` — peut malgré tout exercer l'implémentation.

Les signatures restent **synchrones** : l'état est en mémoire, rien n'est réellement
asynchrone. Passer à `Task<Game?>` n'aurait d'intérêt que si une implémentation réellement
asynchrone apparaissait.

## Conséquences

- Toute modification d'une partie passe par `Mutate`. Un endpoint qui ferait
  `Find` puis muterait puis `Save` contournerait le verrou — c'est le principal risque de
  régression, à surveiller en revue de code.
- Les parties sont indépendantes : le verrou est indexé par `Guid`, pas global.
- `Mutate` renvoie un `Result<T>` (ADR 0004), donc la section critique reste courte et sans
  exception.
- `Find` reste exposé pour les lectures pures, où une vue légèrement décalée est sans
  conséquence.

## Vérification et réexamen

À vérifier lors de l'implémentation, non encore fait :

- Test de concurrence : N tâches tirent simultanément sur la même case d'une même partie ;
  exactement une doit réussir, les autres doivent recevoir `CellAlreadyShot`. Ce test échoue
  si le verrou est absent ou mal indexé — c'est son pouvoir discriminant. Il doit être
  exécuté plusieurs fois, un test de course qui passe une fois ne prouve rien.
- Test d'injection d'un store pré-rempli dans `WebApplicationFactory`.

À réexaminer si la persistance quittait le backlog : les signatures synchrones devraient
alors être reconsidérées.

## Références

- Spec de conception § 3
- `CLAUDE.md` § 3 bis (Repository) et § Injection de dépendances
