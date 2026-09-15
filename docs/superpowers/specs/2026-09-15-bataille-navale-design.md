# Conception — Bataille Navale (C# / ASP.NET Core)

Date : 2026-09-15
Statut : validé par le binôme à l'issue de la session de brainstorming du 2026-09-15.
Source des décisions : `PROMPTS.md`, entrée « 2026-09-15 — Brainstorming des choix de conception ».

Ce document fixe les cinq familles de choix laissées libres par le sujet. Chaque décision
structurante y renvoie vers son ADR, qui en porte la justification détaillée.

---

## 1. Règles du jeu

| Règle | Valeur retenue | ADR |
|---|---|---|
| Taille de grille | paramétrable, défaut **10×10** | 0006 |
| Flotte | paramétrable, défaut **5-4-3-3-2** (Porte-avions, Croiseur, Contre-torpilleur, Sous-marin, Torpilleur) | 0006 |
| Navires adjacents | **interdits** — deux navires ne peuvent pas se toucher, diagonales comprises | 0006 |
| Enchaînement des tours | **touche = on rejoue** ; le tour ne change qu'après un coup manqué | 0006 |
| Placement de la flotte du joueur | **manuel**, dans le navigateur, validé côté serveur | 0006 |
| Placement de la flotte adverse | automatique (`FleetPlacer` à `Random` injecté) | 0006 |
| Fin de partie | tous les navires d'un camp coulés | 0006 |

La grille et la flotte sont des **données** (`GameRules`), pas des constantes. Ce choix sert
d'abord les tests : une grille 3×3 avec un seul navire de taille 2 rend les cas limites
(saturation, fin de partie, débordement) atteignables en quelques coups au lieu de dix-sept.

### Conséquence assumée

Le niveau Difficile (densité probabiliste) combiné à « touche = on rejoue » enchaîne
typiquement 4 à 5 coups dès qu'il touche. Le joueur perdra presque systématiquement.
Ce n'est pas un défaut : c'est le résultat attendu de la combinaison de règles retenue.
Le niveau **Normal** est le mode jouable, le niveau **Difficile** est une démonstration de
l'algorithme. Cette limite doit figurer dans le `README.md`.

---

## 2. Représentation et organisation du code

### Principe directeur — une seule source de vérité

Le piège classique de la bataille navale est de tenir **deux** représentations de l'état :
une matrice de cellules et une liste de navires. Elles se désynchronisent, et c'est de là
que viennent les bugs de « coulé » et de fin de partie.

Ici, la vérité est portée par **les navires et l'ensemble des tirs reçus**. La matrice
affichable est *calculée* à la demande par `ToDto()`. L'inverse serait impossible : on ne
peut pas reconstituer les navires à partir d'une matrice partiellement découverte.

Le coût de calcul est nul à cette échelle : savoir si un tir touche, c'est parcourir cinq
navires d'au plus cinq cases.

### `BattleShip.Models` — domaine pur, aucune dépendance

| Type | Rôle |
|---|---|
| `Coordinate` | `readonly record struct (int X, int Y)` — égalité par valeur, utilisable en clé de `HashSet` |
| `ShipTemplate` | `(string Name, int Size)` — la composition de flotte est une donnée |
| `Ship` | `Name`, `Size`, `Cells` (posées au placement), `HitCells`, `IsSunk => HitCells.Count == Size` |
| `Board` | `IReadOnlyList<Ship> Ships`, `HashSet<Coordinate> ReceivedShots` — **la vérité** |
| `GameRules` | `GridSize`, `Fleet`, `ShipsMayTouch = false`, `ExtraTurnOnHit = true` |
| `Game` | agrégat : `Id`, `Rules`, `HumanBoard`, `OpponentBoard`, `CurrentPlayer`, `Status`, `History` |
| `ShotRecord` | `(Coordinate At, ShotResult Result, Player By, int Turn)` |
| `Result<T>` / `GameError` | issues métier normales (voir § 5) |
| `PlacementRules` | fonctions pures : bornes, chevauchement, adjacence |
| `FleetPlacer(Random random)` | placement aléatoire par tirage-rejet avec compteur de garde |
| `IOpponentStrategy` / `ShotHistory` | contrat de l'adversaire (voir § 4) |
| `IGameStore` | contrat du dépôt (voir § 3) |

`PlacementRules` est **partagé** par le placement automatique et par la validation du
placement manuel. Sans ce partage, la règle d'adjacence serait écrite deux fois et le
`FleetPlacer` finirait par produire des placements que le validateur refuserait.

### `BattleShip.API`

`InMemoryGameStore`, les trois `IOpponentStrategy`, les validateurs FluentValidation, les
extensions `ToDto()`, les endpoints HTTP et le service gRPC.

### `BattleShip.App`

Service `GameState` (singleton) et trois pages : nouvelle partie → placement → jeu.

### `BattleShip.Tests`

Tests métier sur `BattleShip.Models` et tests d'intégration sur `BattleShip.API`.

---

## 3. Stockage et concurrence

`IGameStore` en `Singleton`, implémenté par `InMemoryGameStore` sur `ConcurrentDictionary`.
L'interface appartient à `BattleShip.Models`, l'implémentation à `BattleShip.API`.

### Le trou que ferme l'ADR 0002

`ConcurrentDictionary` protège **le dictionnaire**, pas les `Game` qu'il contient. Deux
requêtes simultanées sur la même partie — un double-clic, ou une requête gRPC qui croise une
requête HTTP — mutent le même objet en parallèle : compteur de touches faussé, deux tirs
acceptés sur la même case.

Le store expose donc une **opération de mutation atomique** qui prend un verrou propre à la
partie :

```csharp
public interface IGameStore
{
    Game? Find(Guid id);
    void Save(Game game);
    bool Remove(Guid id);
    Result<T> Mutate<T>(Guid id, Func<Game, Result<T>> change);
}
```

`Mutate` sérialise les modifications d'une même partie sans sérialiser les parties entre
elles. Les signatures restent **synchrones** : l'état est en mémoire, rien n'est réellement
asynchrone, et rendre asynchrone ce qui ne l'est pas coûte sans rien apporter.

La persistance (SQLite / EF) reste en backlog, explicitement écartée du socle.

---

## 4. Algorithme de l'adversaire

Trois niveaux livrés, trois implémentations de `IOpponentStrategy` (ADR 0003).

| Niveau | Stratégie | Principe |
|---|---|---|
| Facile | `RandomStrategy` | tire au hasard parmi les cases non jouées |
| Normal | `HuntTargetStrategy` | chasse sur les cases de parité `(x+y)%2==0`, puis ratisse les voisines après une touche |
| Difficile | `DensityStrategy` | énumère tous les placements encore légaux de chaque navire non coulé, compte les passages par case, vise le maximum |

### Nombre moyen de coups — **hypothèse non vérifiée**

Valeurs issues de la littérature sur la grille 10×10 classique **sans** règle de
non-adjacence : aléatoire ≈ 96, chasse/cible ≈ 65, avec parité ≈ 60, densité ≈ 42.

Notre règle de non-adjacence **change ces valeurs** (elle donne plus d'information à la
densité). Ces chiffres n'ont pas été mesurés dans ce projet. La mesure est une tâche du
plan d'implémentation ; tant qu'elle n'est pas faite, aucun document ne doit présenter ces
nombres comme un résultat.

### Le contrat, et pourquoi il a cette forme

```csharp
public sealed record ShotHistory(
    int GridSize,
    IReadOnlyList<ShotRecord> Shots,
    IReadOnlyList<ShipTemplate> RemainingShips,
    IReadOnlyList<Ship> SunkShips,
    bool ShipsMayTouch);

public interface IOpponentStrategy
{
    string Name { get; }
    Coordinate NextShot(ShotHistory history);
}
```

`ShotHistory` ne porte **aucune référence au `Board` adverse**. L'invariant « l'adversaire ne
triche pas » devient une propriété du type plutôt qu'une affaire de relecture : la densité ne
*peut pas* consulter la flotte, même par erreur.

`RemainingShips` et `SunkShips` sont des informations légitimes — on les annonce aussi au
joueur humain. Sans elles, la densité serait impossible ; avec un accès au `Board`, elle
serait tricheuse et son niveau ne mesurerait plus rien.

### Source d'aléa

`FleetPlacer` et `RandomStrategy` reçoivent un `Random` par injection. Jamais
`Random.Shared` en dur : un test de placement non déterministe passe presque toujours et
échoue de loin en loin, sans moyen de reproduire l'échec.

---

## 5. Erreurs et frontières

Les refus métier passent par `Result<T>` ; les exceptions restent réservées à ce qui ne
devrait jamais arriver (ADR 0004, qui **renverse** le § Gestion des erreurs de `CLAUDE.md`).

| `GameError` | gRPC | HTTP |
|---|---|---|
| `GameNotFound` | `NotFound` | `404` |
| `OutOfBounds` | `InvalidArgument` | `400` |
| `CellAlreadyShot` | `InvalidArgument` | `409` |
| `GameAlreadyFinished` | `FailedPrecondition` | `409` |
| `NotYourTurn` | `FailedPrecondition` | `409` |
| `InvalidPlacement` | `InvalidArgument` | `400` |

La traduction vit **en un seul endroit par façade**. Les endpoints ne contiennent pas de
`catch` par type.

### Note sur l'argument de performance

Le § 3 bis de `CLAUDE.md` suggérait d'instruire le coût des exceptions « en boucle serrée
quand l'adversaire probabiliste évalue beaucoup de coups ». Cet argument **ne s'applique pas**
à cette conception : `DensityStrategy` énumère des placements dans sa propre structure de
travail et n'appelle jamais le moteur pour tester un coup ; l'invariant garantit qu'elle ne
propose jamais un coup invalide. Aucune exception n'est donc levée par tour d'IA. Le seul
émetteur de refus métier est le joueur humain, à raison de quelques-uns par partie.

Le choix se joue sur la lisibilité et sur la traduction vers les façades, pas sur la
performance. Aucun micro-benchmark n'est nécessaire, et en écrire un mesurerait un scénario
qui n'existe pas.

---

## 6. Contrat d'API

### gRPC-Web — le tir, exclusivement (ADR 0005)

```proto
service BattleService {
  rpc Fire (FireRequest) returns (FireResponse);
}
```

Le tir est le seul échange gRPC. Il coche les trois critères de la contrainte : appel
fréquent (démonstration facile), refus **naturels** (case déjà jouée, partie inconnue — pas
d'erreur fabriquée pour l'occasion), et charge utile qui illustre la règle du secret.

Avec « touche = on rejoue », `Fire` renvoie une **séquence** et non un coup : le résultat du
coup du joueur, puis la chaîne des coups de l'adversaire jusqu'à son premier manqué.

```
Fire(gameId, x, y)
  → FluentValidation (bornes, gameId)
  → store.Mutate(gameId, …)                  [verrou propre à la partie]
      → Result<ShotOutcome>
      → si touche et partie en cours : le tour reste au joueur, on s'arrête
      → sinon : l'adversaire joue tant qu'il touche, chaque coup enregistré
  → réponse : coup du joueur + chaîne adverse + état résultant
```

**Conséquence acceptée** : `api.http` ne peut pas couvrir le tir. La démonstration de
l'échange passe par le navigateur (console F12, onglet Réseau) et par les tests
d'intégration. Le scénario de démonstration doit être écrit pas à pas dans le `README.md`,
sinon la contrainte « démontrable depuis le navigateur » ne se prouve pas en soutenance.

### HTTP — le reste

| Verbe | Route | Rôle |
|---|---|---|
| `POST` | `/games` | crée une partie (règles + niveau de difficulté) → `201` |
| `GET` | `/games/{id:guid}` | état visible de la partie → `200` / `404` |
| `POST` | `/games/{id:guid}/placement` | soumet le placement manuel → `204` / `400` / `409` |
| `GET` | `/games/{id:guid}/history` | historique des coups → `200` / `404` |
| `POST` | `/benchmark` | duel d'IA, N parties à graine fixe → `200` |

Chaque entrée a son validateur FluentValidation, enregistré en `Scoped` et **appelé
explicitement** dans l'endpoint. Le validateur de placement est la meilleure démonstration du
projet : il refuse le débordement, le chevauchement **et** l'adjacence, et sa règle est la
même que celle du placement automatique.

### La règle du secret

Le DTO du plateau adverse ne transporte que les cases tirées avec leur résultat, et les
navires coulés avec leurs cases. **Jamais la flotte.**

---

## 7. Interface (Blazor WebAssembly)

Un service `GameState` enregistré en singleton détient la partie courante et notifie les
composants abonnés par un événement `OnChange` (ADR 0007). Le parcours traverse trois pages —
nouvelle partie, placement, jeu — et l'état doit survivre à la navigation ; sans ce service il
faudrait soit repasser par le serveur à chaque navigation, soit fusionner le tout dans un
seul `.razor` qui en ferait trop.

Les trois états — **chargement**, **succès**, **échec** — sont gérés sur chaque page. Une
coupure de communication ne doit pas rendre l'interface inutilisable.

Page de placement : sélection d'un navire, rotation, prévisualisation de validité, envoi au
serveur. La prévisualisation côté client est un **confort**, pas une garantie : la règle fait
foi côté serveur.

---

## 8. Plan de tests

### Métier (`BattleShip.Models`, sans serveur)

- **Invariant des stratégies** — `[Theory]` paramétré par les trois implémentations, graine
  fixe, parties jouées jusqu'au bout : jamais un coup hors grille, jamais deux fois la même
  case. Le même test couvrira toute stratégie ajoutée ensuite.
- **`FleetPlacer`** — sur N graines : ni chevauchement, ni débordement, ni adjacence.
- **Placement manuel** — un test de refus par motif : débordement, chevauchement, adjacence.
- **Fin de partie** — le dernier navire coulé termine la partie ; un tir après la fin est
  refusé.
- **Le secret** — sérialiser le DTO du plateau adverse et vérifier qu'aucune coordonnée d'un
  navire non touché n'y apparaît. Un `ToDto()` naïf qui sérialiserait `Board.Ships` fait
  échouer ce test immédiatement.

### Intégration (`BattleShip.API`)

- **gRPC via `WebApplicationFactory`** — un succès, un `InvalidArgument` (case déjà jouée),
  un `NotFound` (partie inconnue).
- **HTTP** — placement invalide → `400 ValidationProblem` avec le motif.
- **Concurrence** — N tâches tirent simultanément sur la même case : une seule réussit, les
  autres reçoivent `CellAlreadyShot`.

Chaque test doit pouvoir **échouer**. Après toute correction, vérifier que le test échouait
avant et réussit après.

---

## 9. Risques identifiés

| Risque | Parade |
|---|---|
| Le montage `WebApplicationFactory` + `GrpcChannel` ne fonctionne pas | **Spike au jour 1**, avant toute logique de tir. Si le montage résiste, tout le tir est intestable — à découvrir au jour 1, pas au jour 4 |
| Le placement manuel déborde le budget front | Le placement automatique existe déjà pour l'adversaire ; en repli, l'exposer au joueur avec un bouton « re-générer » et consigner l'arbitrage |
| Le tirage-rejet du `FleetPlacer` boucle avec la non-adjacence | Compteur de garde, puis relance complète du placement ; test sur N graines |
| `DensityStrategy` mal calibrée sur les touches non coulées | La mesure du duel d'IA (§ 10) rend la régression visible |

Le problème du `.git` de `csharp-school` évoqué au § 1 bis de `CLAUDE.md` **n'existe plus** :
le dépôt est `BattleShip/`, et `csharp-school/` est un répertoire frère, hors périmètre.
Aucun ADR n'est nécessaire.

---

## 10. Backlog

Retenu, par ordre d'attaque :

1. **Duel d'IA + mesure** — N parties d'une stratégie contre une autre, graine fixe,
   nombre moyen de coups. Quasi gratuit une fois le moteur paramétrable et les stratégies
   écrites, et c'est la preuve chiffrée que les trois niveaux diffèrent réellement. Alimente
   `REVUE-IA.md` avec un résultat mesuré plutôt qu'affirmé.
2. **Historique des coups + rejeu** — la liste ordonnée des tirs est déjà dans le modèle ;
   l'exposer donne un panneau d'historique et un rejeu pas à pas, utile aussi pour déboguer
   le comportement de l'adversaire.
3. **Accessibilité** — grille navigable au clavier, annonces des résultats de tir pour
   lecteur d'écran, contrastes vérifiés.

Écarté du socle, et pourquoi :

- **Persistance SQLite / EF** — travail d'infrastructure qui ne sert ni le moteur ni
  l'adversaire ; `IGameStore` rend le changement local le jour où il devient utile.
- **Multijoueur** — demande une gestion de sessions et de temps réel hors périmètre.
- **Déploiement** — sans valeur ajoutée pour l'évaluation par rapport au coût.

---

## 11. ADR associés

| ADR | Décision |
|---|---|
| 0001 | Représentation de l'état de jeu |
| 0002 | `IGameStore` et verrou par partie |
| 0003 | `IOpponentStrategy` et contrat `ShotHistory` |
| 0004 | `Result<T>` pour les refus métier — renverse `CLAUDE.md` |
| 0005 | Le tir en gRPC-Web exclusif |
| 0006 | Règles du jeu |
| 0007 | Gestion de l'état côté Blazor |
