# ADR 0001 : Représentation de l'état de jeu

## Statut et date
Accepté — 2026-09-15

## Contexte

Le moteur doit répondre à chaque coup : ce tir touche-t-il, le navire est-il coulé, la partie
est-elle terminée ? Il doit aussi produire une vue affichable, amputée de ce que le joueur n'a
pas le droit de connaître. Le piège classique de la bataille navale est de tenir **deux**
représentations — une matrice de cellules et une liste de navires — puis de les laisser diverger :
c'est de là que viennent les bugs de « coulé » et de fin de partie.

## Options envisagées

- **A. Navires + ensemble de tirs comme vérité, matrice calculée.** `Board` porte
  `IReadOnlyList<Ship>` et `HashSet<Coordinate> ReceivedShots` ; `ToDto()` projette à la demande.
  Une seule source de vérité. Tir en O(navires × taille), soit au plus 17 comparaisons.
  Inconvénient : pas d'indexation O(1).
- **B. Matrice `CellState[,]` comme vérité, plus la liste des navires.** Indexation O(1) et
  projection directe vers le front, mais deux structures à tenir cohérentes à chaque tir et des
  tests d'invariant de cohérence en plus des tests de règles.
- **C. Matrice de références `Ship?[,]`.** Cohérence occupation/navire par construction et O(1)
  au tir, mais l'état « touché » doit vivre ailleurs et la sérialisation devient délicate
  (références croisées).

Critère de comparaison retenu : **le nombre d'invariants qu'il faut maintenir à la main**. La
performance ne discrimine pas — à cette échelle, les trois options sont instantanées.

## Décision

Option A. `Coordinate` est un `readonly record struct (int X, int Y)` : égalité par valeur, donc
utilisable directement en clé de `HashSet`. `Board` détient les navires et l'ensemble des tirs
reçus ; toute matrice est une projection. La désynchronisation devient **impossible par
construction** plutôt qu'évitée par discipline.

`CellDto.State` porte quatre valeurs (`"miss"`, `"hit"`, `"sunk"`, `"ship"`). `"ship"` désigne une
case d'un navire du joueur non encore touchée ; elle n'apparaît **jamais** sous
`OpponentBoardDto`, qui ne projette que des navires entièrement coulés.

## Conséquences

- Aucun test d'invariant de cohérence à écrire : il n'y a rien à synchroniser.
- `ToDto()` est le seul point où la règle du secret s'applique, donc le seul point à tester pour
  elle (ADR 0005, spec § 3).
- Le placement écrit `Ship.Cells` une fois pour toutes ; le déroulement n'ajoute que des tirs. Les
  deux phases sont nettement séparées.
- Pas d'indexation O(1). Sur une grille très grande, un index dérivé serait ajouté **en cache**,
  sans jamais devenir la vérité.

## Vérification et réexamen

- Fin de partie : `GameTests` coule le dernier navire et vérifie le passage à `Finished`.
- Règle du secret : `BattleShip.Tests/Api/SecretTests.cs`,
  `The_dto_reveals_no_undiscovered_opposing_cell` (l'intersection entre les cases exposées par
  `dto.Opponent` et les cases non découvertes de la flotte adverse est vide) et
  `The_own_board_is_the_player_board_and_not_the_opponent_one`. Pouvoir discriminant mesuré par
  injection de fuite (retrait du filtre `Where(ship => ship.IsSunk)` dans `ToOpponentBoardDto`)
  et par inversion temporaire des deux plateaux : les tests échouent alors en nommant les
  coordonnées fuitées.
- À réexaminer si une grille de très grande taille entrait au backlog, ou si un profilage montrait
  que la recherche linéaire au tir devient mesurable.

## Références

- Spec de conception : `docs/superpowers/specs/2026-09-15-bataille-navale-design.md` § 2
- `CLAUDE.md` § 2 (conventions C#, `record` et `record struct`)
