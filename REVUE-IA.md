# Revues de propositions IA

Quatre propositions de l'IA qu'on a vérifiées avant de les garder : une acceptée, une adaptée, une
rejetée, une corrigée. Pour chacune, on a écrit ce qu'on attendait avant de lancer la commande.

## Revue 1 — L'adversaire ne provoque jamais de refus du moteur — acceptée

**Proposition examinée** : dans l'ADR 0004 (commit `f379852`), l'IA disait qu'il était inutile de
mesurer le coût des exceptions pendant les tours de l'adversaire, parce que `DensityStrategy`
n'appelle jamais le moteur pour tester un coup.

**Hypothèse à vérifier** : deux choses. Aucune stratégie ne propose un coup que le moteur refuse, et
une stratégie n'a de toute façon aucun moyen d'atteindre le plateau adverse.

**Expérience** : un script `dotnet run --file refusals.cs` joue 200 parties par stratégie (graine
20260915) et compte les `Result` en échec sur `Board.Fire` pendant les tours adverses. Il parcourt
aussi par réflexion tous les types atteignables depuis `ShotHistory`. On attendait zéro refus et
aucun chemin vers `Board` ou `Game`. Ce contrôle détecterait une stratégie qui « essaie » ses coups
contre le moteur ou qui tire à nouveau sur une case déjà jouée.

**Observation** : 37 903 tirs d'adversaire (Random 19 139, HuntTarget 10 520, Density 8 244), zéro
refus ; 9 types visités depuis `ShotHistory`, aucun ne mène à `Board` ni à `Game`. Pour être sûrs
que le compteur marche, on a ajouté une stratégie volontairement fausse qui tire toujours en
`(0,0)` : elle remonte bien `CellAlreadyShot x200`.

**Décision** : acceptée. L'ADR 0004 s'appuie maintenant sur une mesure et pas seulement sur un
raisonnement.

**Preuves et limites** : `scratchpad/refusals.cs` (hors dépôt), commit `f379852`. Une seule graine,
un seul jeu de règles. La partie « aucun chemin » ne vaut que pour les types de `ShotHistory`, pas
pour un plateau qu'on passerait par constructeur.

## Revue 2 — Le test de concurrence du store peut-il échouer ? — adaptée

**Proposition examinée** : le test `Only_one_concurrent_shot_on_the_same_cell_succeeds` lance 32
tirs simultanés sur la même case et vérifie qu'un seul réussit (commits `883fe0b`, `37f9734`).

**Hypothèse à vérifier** : si on retire le verrou de `InMemoryGameStore.Mutate`, le test doit
échouer à chaque fois, sans rendre la suite lente.

**Expérience** : `lock (gameLock)` commenté, puis le test lancé 3 fois pour chaque version. On
attendait 3 échecs sur 3. Ce contrôle détecte un `Mutate` qui ne sérialise pas les tirs sur une
même partie.

**Observation** : trois versions essayées, dans cet ordre.
1. `Task.Run` seul : échec 2 fois sur 3 seulement. Les premières tâches finissent avant que les
   dernières démarrent.
2. `Barrier(32)` sur `Task.Run` : toujours 2 sur 3, et la suite passe de 0,86 s à 15-18 s (le pool
   de threads grossit trop lentement). Abandonné.
3. `Barrier(32)` sur 32 `Thread` dédiés : 3 échecs sur 3, 4 des 5 `[InlineData]` en échec à chaque
   fois, suite revenue à 842 ms.

**Décision** : adaptée, on garde la version 3 (commit `c604123`). Le test reste imparfait : un cas
sur cinq ne voit pas la course, donc il ne suffit pas seul. La vraie garantie reste la relecture de
`Mutate` (verrou par `Guid`, lecture sous verrou).

**Preuves et limites** : commits `883fe0b`, `44bd57c`, `c604123`. La fenêtre de course reste
probabiliste et on n'a testé qu'une case. Le même exercice sur la course `Read`/`Mutate` est
raconté dans l'ADR 0005 : le test par HTTP/gRPC ne pouvait pas échouer, il a été redescendu au
niveau du store (commit `aef815e`).

## Revue 3 — Recopier les DTO dans le front — rejetée

**Proposition examinée** : les DTO étaient dans `BattleShip.API`, que le front ne référence pas.
L'IA a proposé en premier de recopier les records côté front.

**Hypothèse à vérifier** : une copie n'est acceptable que si une différence entre les deux versions
se voit tôt, à la compilation, et pas seulement à l'écran.

**Expérience** : on a déplacé les DTO dans `BattleShip.Models/Contracts/`, puis renommé une
propriété (`sed -i 's/string OpponentDifficulty, /string Level, /'` sur `GameDto.cs`) et lancé
`dotnet build BattleShip.App`. On attendait une erreur de compilation. Avec une copie, le même
renommage aurait compilé des deux côtés et donné un niveau vide à l'écran, sans aucun message. On a
aussi vérifié par `curl` que `POST /games` renvoie bien un JSON que le front sait relire.

**Observation** : `error CS1061: 'GameDto' ne contient pas de définition pour
'OpponentDifficulty'`. Côté JSON : `201 Created`, 13 propriétés relues correctement sans aucun
attribut de sérialisation. Le renommage a été annulé ensuite (`git diff` vide).

**Décision** : rejetée. Les contrats sont partagés via `Models/Contracts` (ADR 0008). Front et API
sont compilés ensemble, la copie n'apportait aucune indépendance et ajoutait une panne silencieuse.

**Preuves et limites** : commits `fe16e5b` et `d037bcf`, ADR 0008. Rien n'empêche quelqu'un
d'ajouter un attribut JSON dans ces records et de faire entrer la sérialisation dans le domaine :
seule la relecture l'empêche. La publication *trimmée* n'a pas été testée.

## Revue 4 — Le vainqueur était calculé par le front — corrigée

**Proposition examinée** : dans `Play.razor` (commit `ed4cb56`), l'IA avait écrit
`PlayerWon(game) => game.Opponent.SunkShips.Count == game.Fleet.Count`. La victoire était donc une
règle calculée dans le navigateur.

**Hypothèse à vérifier** : le sujet (diapo 36) demande que le moteur désigne le gagnant, et
l'ADR 0007 dit que le front ne décide aucune règle. Il faut donc que `Game` porte le vainqueur.

**Expérience** : `grep -rn Winner BattleShip.Models/`. Si c'était conforme, on devait trouver au
moins une propriété. Ce contrôle détecte une règle de fin de partie qui n'existe que dans
l'interface.

**Observation** : aucune occurrence. La proposition ne respectait pas le sujet.

**Décision** : corrigée. `Game.Winner` et `GameDto.Winner` ont été ajoutés en TDD, et le front lit
simplement la valeur.

**Avant / après** : avant, aucun test ne parlait du vainqueur. Après,
`The_shooter_who_sinks_the_last_ship_is_the_winner` et
`A_game_played_to_the_end_finishes_with_the_player_as_winner` passent. Pour vérifier que le second
discrimine vraiment, on l'a relancé en attendant `"Opponent"` au lieu de `"Human"` : il échoue avec
`Expected: Opponent / Actual: Human`.

**Preuves et limites** : commits `5948009` et `0d873f9`. La réponse gRPC `FireResponse` ne contient
pas le vainqueur : la page relit l'état par HTTP après chaque tir.
