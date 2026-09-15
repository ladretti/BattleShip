# ADR 0001 : Représentation de l'état de jeu

## Statut et date
Accepté — 2026-09-15

## Contexte

Le moteur doit répondre à trois questions à chaque coup : ce tir touche-t-il, le navire
touché est-il coulé, la partie est-elle terminée. Il doit aussi produire une vue affichable
du plateau, amputée de ce que le joueur n'a pas le droit de connaître.

Le piège classique de la bataille navale est de tenir **deux** représentations de l'état —
une matrice de cellules et une liste de navires — puis de les laisser diverger. C'est de là
que viennent les bugs de « coulé » et de fin de partie : une case marquée touchée alors que
le navire ne compte pas le coup, ou l'inverse.

## Options envisagées

**A. Navires + ensemble de tirs comme vérité, matrice calculée.**
`Board` porte `IReadOnlyList<Ship>` et `HashSet<Coordinate> ReceivedShots`. La matrice
affichable est produite à la demande par `ToDto()`. Une seule source de vérité. Le tir est en
O(nombre de navires × taille), soit au plus 17 comparaisons — négligeable. Inconvénient :
pas d'indexation O(1).

**B. Matrice `CellState[,]` comme vérité, plus la liste des navires.**
Indexation O(1) et projection directe vers le front. Mais deux structures à tenir cohérentes
à chaque tir, et il faudrait des tests d'invariant de cohérence en plus des tests de règles.

**C. Matrice de références `Ship?[,]`.**
Chaque case pointe vers le navire qui l'occupe. Résout la cohérence occupation/navire par
construction et donne un O(1) au tir. Mais l'état « touché » doit vivre ailleurs, et la
sérialisation devient délicate (références croisées entre la matrice et les navires).

Critère de comparaison retenu : **le nombre d'invariants qu'il faut maintenir à la main.**
La performance ne discrimine pas — à cette échelle, les trois options sont instantanées.

## Décision

Option A. `Coordinate` est un `readonly record struct (int X, int Y)` : égalité par valeur,
utilisable directement en clé de `HashSet`. `Board` détient les navires et l'ensemble des
tirs reçus ; toute matrice est une projection.

La désynchronisation devient **impossible par construction** plutôt qu'évitée par discipline.

## Conséquences

- Aucun test d'invariant de cohérence à écrire : il n'y a rien à synchroniser.
- `ToDto()` devient le seul point où la règle du secret s'applique, donc le seul point à
  tester pour elle (voir ADR 0005 et la spec § 3).
- Le placement écrit `Ship.Cells` une fois pour toutes ; l'état de jeu n'ajoute ensuite que
  des tirs. Le placement et le déroulement sont donc deux phases nettement séparées.
- Pas d'indexation O(1). Si un jour une grille très grande rendait cela sensible, un index
  dérivé pourrait être ajouté **en cache**, sans devenir la vérité.

## Vérification et réexamen

- Fin de partie : `GameTests` (tâche 4) contient un test qui coule le dernier navire et
  vérifie que la partie passe à `Finished`.

- Règle du secret (tâche 12) : `BattleShip.Tests/Api/SecretTests.cs`.
  `The_dto_reveals_no_undiscovered_opposing_cell` sérialise `Game.ToDto()` et vérifie que
  l'intersection entre les cases exposées par `dto.Opponent` (tirs + navires coulés) et les
  cases non découvertes de la flotte adverse est vide. Son pouvoir discriminant a été mesuré
  par injection de fuite (retrait du filtre `Where(ship => ship.IsSunk)` dans
  `ToOpponentBoardDto`) : le test échoue alors en identifiant précisément les coordonnées
  fuitées.
  Une première version de ce test cherchait une sous-chaîne `"x":N,"y":N"` dans le JSON du
  `GameDto` entier plutôt que dans le seul sous-arbre `Opponent` ; une revue a montré qu'elle
  produisait un faux positif dès que `Own.Ships` porte les vraies coordonnées de la flotte du
  joueur, les deux plateaux partageant le même espace 0..9 (deux coordonnées communes aux
  deux flottes sur les graines 1/2 du test, 186 paires de graines sur 200 en partageant au
  moins une). Le test a été repris pour porter sur le graphe d'objets de `dto.Opponent`
  uniquement — voir aussi `The_own_board_is_the_player_board_and_not_the_opponent_one`, qui
  détecte spécifiquement une inversion des deux plateaux dans `ToDto()` (pouvoir discriminant
  mesuré de la même façon, par inversion temporaire des arguments).

- Décision sur `CellDto.State` : l'énumération est passée de trois à quatre valeurs
  (`"miss"`, `"hit"`, `"sunk"`, `"ship"`). `"ship"` désigne une case d'un navire du joueur
  non encore touchée ; elle n'apparaît **jamais** sous `OpponentBoardDto`, puisque
  `SunkShipDto` ne projette que des navires déjà entièrement coulés. Cette quatrième valeur
  a été ajoutée après revue, et non de l'initiative unilatérale de l'IA lors de la tâche 12 —
  voir `BattleShip.API/Contracts/GameDto.cs` (doc XML de `CellDto`).

À réexaminer si une grille de très grande taille apparaissait au backlog, ou si un profilage
montrait que la recherche linéaire au tir devient mesurable.

## Références

- Spec de conception : `docs/superpowers/specs/2026-09-15-bataille-navale-design.md` § 2
- `CLAUDE.md` § 2 (conventions C#, `record` et `record struct`)
