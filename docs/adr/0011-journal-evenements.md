# ADR 0011 : Le journal d'événements comme source de vérité

## Statut et date
Accepté — 2026-09-17 — corrigé le 2026-09-18 (répartition `Board.Decide` / `Game.Fire`)

## Contexte

Un tir écrivait le même fait à trois endroits : `Board._receivedShots` et `Ship._hitCells` dans
`Board.Fire`, puis `Game._history` dans `Game.Fire`. Ces trois copies n'étaient synchronisées que
par discipline. Conséquence visible : le rejeu ne pouvait montrer que l'état final projeté en
arrière, jamais l'état « coulé » réel au tir *n*.

## Options envisagées

- **A. Un journal de `GameEvent` comme seule vérité**, les plateaux étant reconstruits par repli
  (`Fold`). L'état au tir *n* devient `Fold(events.Take(n))`, juste par construction.
- **B. Corriger le rejeu au cas par cas** : il faudrait recopier côté rejeu la logique
  touche/coulé de `Board`, donc un second moteur à garder synchrone.
- **C. Bus d'événements, projections asynchrones, CQRS/MediatR** : aucun besoin réel pour un seul
  agrégat en mémoire ; c'est la cérémonie que `CLAUDE.md` § 3 bis écarte.
- **D. Concurrence optimiste** (`Append(id, expectedVersion, events)`) : résout un problème déjà
  réglé par le verrou par partie de l'ADR 0002, au prix de boucles de réessai.

## Décision

Option A. Quatre événements : `GameCreated`, `HumanFleetPlaced`, `ShotFired` (avec son résultat),
`GameEnded`. Ils portent des `ShipSnapshot(Name, Size, Cells)` immuables, jamais des `Ship` : un
`Ship` est mutable, et le partager avec le plateau ferait changer un événement « déjà arrivé » à
chaque tir, `Fold` compris. `Fold` construit des `Ship` neufs à chaque repli.

Les deux registres de l'ADR 0004 restent séparés :

| Méthode | Rôle | Peut refuser ? |
|---|---|---|
| `Board.Decide` | valider un tir (hors grille, case déjà tirée) | oui, `Result<ShotOutcome>` |
| `Game.Fire` | vérifier le tour, appeler `Decide`, enregistrer `ShotFired` et éventuellement `GameEnded` | oui, `Result<ShotRecord>` |
| `Board.Apply` / `Game.Replay` | replier un événement déjà arrivé | jamais (exception si l'impossible arrive) |

`Board._receivedShots` et `Ship._hitCells` restent, mais deviennent ce que `Apply` remplit ;
seul `Game._history` est remplacé par `Game._events`. La surface publique du domaine ne change pas.
`IGameStore` gagne un seul membre, `ReadEvents(id, fromSequence)`, sous le même verrou que `Read`.
Il renvoie le journal complet ; la censure des navires adverses non coulés est faite par
l'endpoint HTTP, pas par le store.

Pas de bus, pas de projection asynchrone, pas de CQRS, pas de MediatR. La persistance sur disque
deviendrait simple avec un journal en ajout seul, mais reste au backlog par choix du binôme.

## Conséquences

- ADR 0001 précisé (même représentation, dérivée autrement), ADR 0002 étendu d'une lecture,
  ADR 0004 appliqué plus strictement, ADR 0008 inchangé (il permet de partager `Fold` avec le
  front), ADR 0007 amendé pour la reprise après rechargement (identifiant en `localStorage`).
- Les contrats HTTP existants gardent leur forme ; `GET /games/{id}/events?from={n}` est ajouté.
- Le rejeu reconstitue l'état « coulé » réel sans code dédié. « Redémarrer l'API perd les
  parties » reste une limite connue.
- Risque : muter un `HashSet` hors de `Apply` réintroduirait le doublon en silence. Seule la
  relecture protège contre ça.

## Vérification et réexamen

- Sérialisation polymorphe (`[JsonPolymorphic]` + `[JsonDerivedType]`) vérifiée avant d'écrire le
  contrat : sortie `count=2 first=ShotFiredDto second=GameEndedDto`.
- `A_ship_is_not_reported_sunk_before_the_shot_that_sank_it` échoue sur un repli qui réutilise les
  `Ship` du plateau et passe sur le repli livré. `/events` n'expose pendant la partie que les cases
  du joueur, et toute la flotte après `GameEnded` (`After_the_game_ends_the_full_journal_is_served`).
- `dotnet test` : 145 → 169 sans régression.
- À revoir si le store devenait multi-processus : la concurrence optimiste redeviendrait utile.

## Références

- Spec : `docs/superpowers/specs/2026-09-17-journal-evenements-design.md` (commit `81449d5`)
- Plan : `docs/superpowers/plans/2026-09-17-journal-evenements.md` ; commits `d488e8d` à `fa5422b`
- ADR 0001, 0002, 0004, 0007, 0008
