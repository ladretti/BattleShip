# ADR 0004 : `Result<T>` pour les refus métier

## Statut et date
Accepté — 2026-09-15. **Renverse** la règle du § Gestion des erreurs de `CLAUDE.md`, qui
prescrivait des exceptions typées pour les refus métier.

## Contexte

Les refus métier de ce domaine — case déjà jouée, coup hors grille, coup après la fin de la
partie, placement invalide — sont des cas **normaux et fréquents**, pas des anomalies. Ils
font partie du déroulement attendu d'une partie.

`CLAUDE.md` laissait la question ouverte et fixait les exceptions typées comme règle
provisoire, en suggérant d'instruire le coût des exceptions « en boucle serrée quand
l'adversaire probabiliste évalue beaucoup de coups ».

## Options envisagées

**A. `Result<T>` pour les refus métier, exceptions pour les bugs.**
Le compilateur force l'appelant à traiter le refus. La traduction vers les façades se fait
par filtrage sur le type d'échec, en un seul point. Coût : écrire un petit `Result<T>` maison
et renverser explicitement la règle en vigueur.

**B. Exceptions typées (statu quo).**
Aucun type à inventer, cohérent avec le reste de `CLAUDE.md`. Mais rien ne force l'appelant à
traiter un refus, et la frontière HTTP/gRPC se remplit de blocs `catch` par type.

**C. `Result<T>` partout, y compris pour les cas anormaux.**
Cohérent, mais bruyant là où il n'y a rien à récupérer, et plus de cérémonie que le projet
n'en demande.

## Décision

Option A. `Result<T>` porte soit une valeur, soit un `GameError`. Les exceptions restent
réservées à ce qui ne devrait jamais arriver.

```csharp
public enum GameError
{
    GameNotFound, OutOfBounds, CellAlreadyShot,
    GameAlreadyFinished, GameNotStarted, NotYourTurn, InvalidPlacement
}
```

**Les deux styles ne se mélangent pas** : aucun refus métier ne lève d'exception, aucune
anomalie ne renvoie de `Result`.

## L'argument de performance ne s'applique pas — et c'est un résultat, pas une omission

Le § 3 bis de `CLAUDE.md` proposait de **mesurer** le coût des exceptions dans la boucle de
l'adversaire probabiliste. Cette mesure n'a pas lieu d'être dans la conception retenue :

- `DensityStrategy` énumère des **placements de navires** dans sa propre structure de
  travail. Elle n'appelle jamais le moteur pour tester un coup.
- L'invariant de l'ADR 0003 garantit qu'une stratégie ne propose **jamais** un coup invalide.
  Aucune exception n'est donc levée par tour d'adversaire.
- Le seul émetteur de refus métier est le joueur humain, à raison de quelques-uns par partie.

Écrire un micro-benchmark ici mesurerait un scénario qui n'existe pas. L'hypothèse a été
examinée et écartée sur la structure du code, pas sur un chiffre — c'est plus solide qu'un
chiffre sans rapport avec l'exécution réelle.

Le choix se joue donc sur la lisibilité et sur la traduction vers les façades.

## Conséquences

- `CLAUDE.md` § Gestion des erreurs doit être mis à jour, sinon les deux documents se
  contredisent.
- `IGameStore.Mutate` renvoie un `Result<T>` (ADR 0002) : la section critique reste courte et
  sans exception.
- La traduction `GameError` → statut vit en un seul endroit par façade :

  | `GameError` | gRPC | HTTP |
  |---|---|---|
  | `GameNotFound` | `NotFound` | `404` |
  | `OutOfBounds` | `InvalidArgument` | `400` |
  | `CellAlreadyShot` | `InvalidArgument` | `409` |
  | `GameAlreadyFinished` | `FailedPrecondition` | `409` |
  | `GameNotStarted` | `FailedPrecondition` | `409` |
  | `NotYourTurn` | `FailedPrecondition` | `409` |
  | `InvalidPlacement` | `InvalidArgument` | `400` |

- Les endpoints ne contiennent aucun `catch` par type.
- Risque : un `Result` ignoré est silencieux là où une exception non rattrapée serait bruyante.
  La parade est le test, pas la discipline.

## Vérification et réexamen

**Constaté** (2026-09-16, `REVUE-IA.md` revue 1) : sur 200 parties par stratégie et pour les
trois niveaux — **37 903 tirs d'adversaire** —, le moteur n'a émis **aucun** refus métier
pendant un tour d'adversaire. L'affirmation « aucune exception n'est levée par tour
d'adversaire » n'est donc plus une analyse structurelle mais une mesure, et le micro-benchmark
envisagé mesurerait bien un scénario qui n'existe pas.

Le contrôle a été prouvé capable d'échouer : une stratégie délibérément fautive injectée dans
le même harnais produit 200 refus `CellAlreadyShot`.

**Constaté** aussi, par réflexion sur le graphe de types : aucun chemin ne mène de
`ShotHistory` — le seul argument de `IOpponentStrategy.NextShot` — vers `Board` ou `Game`. Une
stratégie ne peut donc pas interroger le moteur pour évaluer un coup candidat, faute d'y avoir
accès.

**Reste à vérifier** :

- Un test par `GameError` vérifiant le statut produit de chaque côté de la frontière.
- Un test qui rejoue une case déjà jouée et vérifie qu'aucune exception ne traverse la
  frontière.

À réexaminer si un `Result` ignoré causait un bug en pratique : il faudrait alors soit un
analyseur, soit revenir aux exceptions.

## Références

- Spec de conception § 5
- `CLAUDE.md` § Gestion des erreurs et § 3 bis (décision en suspens)
