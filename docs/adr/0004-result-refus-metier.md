# ADR 0004 : `Result<T>` pour les refus métier

## Statut et date
Accepté — 2026-09-15. **Renverse** le § Gestion des erreurs de `CLAUDE.md` (exceptions typées).

## Contexte

Les refus métier — case déjà jouée, coup hors grille, coup après la fin, placement invalide — sont
des cas **normaux et fréquents**, pas des anomalies. `CLAUDE.md` fixait les exceptions typées comme
règle provisoire et suggérait d'instruire leur coût dans la boucle de l'adversaire probabiliste.

## Options envisagées

- **A. `Result<T>` pour les refus, exceptions pour les bugs.** Le compilateur force l'appelant à
  traiter le refus et la traduction tient en un point ; coût : un `Result<T>` maison à écrire.
- **B. Exceptions typées (statu quo).** Aucun type à inventer, cohérent avec `CLAUDE.md` ; mais
  rien ne force l'appelant à traiter un refus et la frontière HTTP/gRPC se remplit de `catch`.
- **C. `Result<T>` partout, cas anormaux compris.** Cohérent, mais bruyant là où rien n'est
  récupérable.

## Décision

Option A. `Result<T>` porte une valeur ou un `GameError` ; les exceptions restent réservées à ce qui
ne devrait jamais arriver.

```csharp
public enum GameError
{
    GameNotFound, OutOfBounds, CellAlreadyShot,
    GameAlreadyFinished, GameNotStarted, NotYourTurn, InvalidPlacement
}
```

**Les deux styles ne se mélangent pas** : aucun refus métier ne lève d'exception, aucune anomalie
ne renvoie de `Result`. L'argument de performance ne s'applique pas — `DensityStrategy` énumère des
placements sans appeler le moteur et l'invariant de l'ADR 0003 interdit un coup invalide : le
micro-benchmark mesurerait un scénario inexistant. Le choix se joue donc sur la lisibilité.

## Conséquences

- `CLAUDE.md` § Gestion des erreurs a été mis à jour : les deux documents ne se contredisent plus.
- `IGameStore.Mutate` renvoie un `Result<T>` (ADR 0002) : section critique courte et sans exception.
- La traduction `GameError` → statut vit en un seul endroit par façade ; aucun `catch` par type
  dans les endpoints :

  | `GameError` | gRPC | HTTP |
  |---|---|---|
  | `GameNotFound` | `NotFound` | `404` |
  | `OutOfBounds` | `InvalidArgument` | `400` |
  | `CellAlreadyShot` | `InvalidArgument` | `409` |
  | `GameAlreadyFinished` | `FailedPrecondition` | `409` |
  | `GameNotStarted` | `FailedPrecondition` | `409` |
  | `NotYourTurn` | `FailedPrecondition` | `409` |
  | `InvalidPlacement` | `InvalidArgument` | `400` |

- Risque : un `Result` ignoré est silencieux là où une exception serait bruyante — parade : le test.

## Vérification et réexamen

- **Constaté** : sur 37 903 tirs d'adversaire (200 parties × trois niveaux), **aucun** refus métier
  pendant un tour ; contrôle prouvé capable d'échouer, une stratégie fautive injectée produit 200
  refus `CellAlreadyShot` (`REVUE-IA.md`, revue 1). Aucun chemin ne mène de `ShotHistory` vers
  `Board` ni `Game` : une stratégie ne peut pas interroger le moteur.
- **Reste à vérifier** : un test par `GameError` sur le statut produit de chaque côté de la
  frontière, et un test rejouant une case déjà jouée sans qu'aucune exception ne la traverse.
- À réexaminer si un `Result` ignoré causait un bug : il faudrait un analyseur, ou les exceptions.

## Références

- Spec de conception § 5 ; `CLAUDE.md` § Gestion des erreurs et § 3 bis
