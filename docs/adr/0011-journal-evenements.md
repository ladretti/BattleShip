# ADR 0011 : Le journal d'événements comme source de vérité

## Statut et date
Accepté — 2026-09-17

## Contexte

Le moteur fait déjà, sans que ce soit une décision assumée, la moitié d'un event sourcing.
`Game._history` est une liste ordonnée de `ShotRecord`, et `DtoMappings.ToOwnBoardDto` en
dérive déjà les cases reçues. En parallèle, `Board._receivedShots` et `Ship._hitCells` portent
le **même fait** sous forme de `HashSet` mutables : un tir écrit donc le même fait à trois
endroits — `Board._receivedShots` et `Ship._hitCells` depuis `Board.Fire`, puis `Game._history`
depuis `Game.Fire` qui l'appelle — et deux lectures différentes puisent dans deux sources
différentes qui ne sont synchronisées que par discipline, pas par construction.

Cette duplication n'est pas seulement esthétique : elle est la cause du bug documenté au
`README.md`, « le rejeu ne rejoue que les coups : il ne reconstitue pas l'état "coulé"
intermédiaire ». Rejouer un historique de `ShotRecord` sans faire aussi rejouer les plateaux ne
peut produire que l'état **final** projeté en arrière, jamais l'état réel à l'instant *n*. Ce
n'est pas un manque de code, c'est le symptôme d'une forme de donnée inadaptée à ce qu'on lui
demande.

## Options envisagées

- **A. Un journal d'événements (`GameEvent`) comme unique source de vérité, `Board` et `Ship`
  réduits au rôle d'accumulateur du repli.** `Decide` valide et produit des événements,
  `Apply`/`Fold` les replie sans jamais pouvoir échouer. Un seul fait, une seule écriture ; l'état
  à l'instant *n* devient `Fold(events.Take(n))`, correct par construction. Coût : réécrire
  `Board.Fire` en `Decide` + `Apply`, sans changer sa surface publique.
- **B. Ne rien changer, corriger le rejeu au cas par cas.** Reconstituer l'état « coulé »
  intermédiaire en rejouant les tirs contre une copie du plateau final nécessiterait de dupliquer,
  côté rejeu, exactement la logique de touche/coulé que `Board` porte déjà — un second moteur à
  synchroniser avec le premier, l'inverse de ce qui est recherché.
- **C. Bus d'événements avec projections asynchrones (CQRS/MediatR).** Résoudrait la duplication
  et ouvrirait la voie à des abonnés multiples, mais aucun besoin réel ne le justifie ici : un seul
  agrégat, un seul lecteur (le repli), un store mono-processus en mémoire. C'est exactement la
  cérémonie que le `CLAUDE.md` § 3 bis (« Ce qui n'est pas retenu ») écarte pour `IRepository<T>`
  générique et `IGameService` : un ajout qui retire de la lisibilité sans contrepartie mesurable.
- **D. Concurrence optimiste (`Append(id, expectedVersion, events)`) en remplacement du verrou par
  partie de l'ADR 0002.** C'est l'option « canonique » de l'event sourcing en général, mais elle
  résout un problème — l'écriture concurrente — déjà résolu par le verrou indexé par `Guid`, testé
  et correct dans un store mono-processus en mémoire (`InMemoryGameStoreTests`). Elle ajouterait
  des boucles de réessai et une classe d'erreur supplémentaire pour un gain nul dans ce contexte.

## Décision

Option A. Le journal devient la source de vérité unique du domaine.

```csharp
public abstract record GameEvent(int Sequence);

public sealed record ShipSnapshot(string Name, int Size, IReadOnlyList<Coordinate> Cells);

public sealed record GameCreated(int Sequence, GameRules Rules,
    IReadOnlyList<ShipSnapshot> OpponentShips, string Difficulty) : GameEvent(Sequence);

public sealed record HumanFleetPlaced(int Sequence,
    IReadOnlyList<ShipSnapshot> Ships) : GameEvent(Sequence);

public sealed record ShotFired(int Sequence, Coordinate At, Player By,
    ShotResult Result, string? SunkShipName) : GameEvent(Sequence);

public sealed record GameEnded(int Sequence, Player Winner) : GameEvent(Sequence);
```

Les événements portent `ShipSnapshot`, jamais `Ship`. `Ship` est **mutable** (`_hitCells`,
rempli par `TryHit`) et `Board` ne copie que la liste qu'on lui donne, pas les objets
(`Ships { get; } = [.. ships]`) : un événement qui porterait des `Ship` partagerait ses
instances avec le plateau, et l'événement « déjà arrivé » se mettrait à jour rétroactivement à
chaque tir. Pire, `Fold` se contaminerait lui-même — replier un préfixe muterait les `Ship` du
journal, donc le repli suivant partirait d'un état déjà touché, et le rejeu deviendrait faux
sans qu'aucun test ne le voie (un test de totalité ne constate qu'une absence d'exception).
`ShipSnapshot(Name, Size, Cells)` est une donnée pure qui ne peut porter aucun état de touche ;
`Fold` construit des `Ship` neufs à partir des instantanés à chaque repli. C'est exactement le
geste de l'**ADR 0003** pour `ShotHistory` : l'invariant — ici, « un événement ne change
jamais » — tient dans le type, pas dans la discipline de qui écrit `Fold`.

Deux fonctions séparent strictement les deux registres de l'ADR 0004, réparties entre le plateau
et la partie :

| | rôle | peut refuser ? | registre d'erreur |
|---|---|---|---|
| `Board.Decide(Coordinate)` | valider un tir contre l'état du plateau visé (hors grille, case déjà tirée) | oui | `Result<ShotOutcome>` |
| `Game.Fire(...)` | vérifier le tour et l'état de la partie, puis dériver de la décision du plateau un ou deux `GameEvent` (`ShotFired`, et `GameEnded` si ce tir coule le dernier navire) | oui | `Result<ShotRecord>` ; le journal est alimenté par effet de bord |
| `Board.Apply` / `Game.Replay` | replier un événement déjà arrivé dans l'état | jamais | exception si l'impossible survient |

**Correction du 2026-09-18** : la version acceptée le 2026-09-17 décrivait un unique `Decide` de
niveau partie rendant `Result<IReadOnlyList<GameEvent>>`. Le code livré (`BattleShip.Models/Board.cs`,
`Game.cs`) sépare la validation (`Board.Decide`, par plateau) de la construction des événements
(`Game.Fire`, qui appelle `Decide` puis enregistre `ShotFired` et, le cas échéant, `GameEnded`). La
séparation refus métier / anomalie de l'ADR 0004 n'est pas remise en cause : seule la répartition
entre les deux méthodes était mal documentée. La spec de conception est corrigée au même moment.

`Apply` est **total** : un événement est un fait déjà arrivé, il ne se refuse pas — c'est
l'invariant central, et il est testable (repli de tout préfixe d'un journal valide). `ShotFired`
porte le **résultat** du tir, pas seulement la coordonnée visée : le repli n'a besoin de consulter
aucun plateau pour savoir si un coup a touché.

`Board._receivedShots` et `Ship._hitCells` sont **conservés**, mais changent de statut : de vérité
parallèle à `Game._history`, ils deviennent l'accumulateur que `Apply` remplit. Les supprimer
priverait `Ship.IsSunk` de son support et casserait la surface publique du domaine sans raison ;
seul `Game._history` (`List<ShotRecord>`) est remplacé, par `Game._events`. La surface publique de
`Board`, `Ship` et `Game` ne change pas.

### Ce que ce n'est pas

Cohérent avec le `CLAUDE.md` § 3 bis, quatre absences explicites :

- **Pas de bus d'événements** — aucun `IEventHandler`, aucune publication, aucun abonné.
- **Pas de projection asynchrone** — le repli est synchrone et calculé à la demande.
- **Pas de CQRS** — un seul modèle, un seul `Fold`, aucune séparation lecture/écriture.
- **Pas de MediatR** — `Decide` est une méthode du domaine, pas un message routé.

### Concurrence optimiste examinée, puis écartée

Le verrou par partie de l'ADR 0002 est correct et déjà testé
(`InMemoryGameStoreTests.Only_one_concurrent_shot_on_the_same_cell_succeeds`). Un
`expectedVersion` ajouterait des boucles de réessai et une classe d'erreur pour résoudre, moins
bien, un problème déjà résolu dans un store mono-processus. `IGameStore` n'est donc étendu que
d'un membre de **lecture** :

```csharp
public interface IGameStore
{
    // inchangés : Find, Save, Remove, Mutate, Read

    Result<IReadOnlyList<GameEvent>> ReadEvents(Guid id, int fromSequence);
}
```

`ReadEvents` s'implémente sous le même verrou que `Read` et renvoie le journal **complet**, non
censuré : la censure des positions adverses non coulées est une responsabilité de la façade HTTP
(§ 3 de la spec), pas du store. Le store sert la vérité ; l'endpoint décide de ce qui en sort.

### Persistance rendue triviale, volontairement non faite

Un journal append-only — où aucune ligne n'est jamais modifiée — se persiste sur disque en
quelques dizaines de lignes. Le binôme choisit néanmoins de garder cette spec sur un seul sujet :
la persistance reste au backlog. La limite connue « redémarrer l'API perd les parties » reste donc
vraie et reste écrite au `README.md`.

## Conséquences

- **Rapport aux ADR existants** : 0001 (représentation de l'état) est **précisé**, pas remplacé —
  la représentation par navires + tirs reçus ne change pas, seule sa dérivation change, de mutation
  parallèle à repli d'un journal. 0002 (`IGameStore`) est **étendu** d'un seul membre de lecture,
  son verrou par partie n'est pas touché. 0004 (`Result<T>` pour les refus) est **appliqué plus
  strictement** : `Decide`/`Apply` rend la séparation refus/anomalie visible dans la signature de
  deux fonctions distinctes plutôt que dans une seule méthode `Board.Fire` qui faisait les deux à
  la fois. 0008 (contrat partagé dans `Models`) ne change pas : c'est lui qui permet à `Fold`
  d'être partagé tel quel entre `BattleShip.API` et `BattleShip.App`. 0007 (état Blazor) n'est
  **pas** inchangé : sa condition de réexamen — « une reprise de partie après rechargement du
  navigateur entrait au périmètre : il faudrait alors persister l'identifiant de partie côté
  client » — est précisément ce que programme la spec § 5.3 (identifiant en `localStorage`,
  relu au démarrage). Cette reprise satisfait donc la condition de réexamen de l'ADR 0007 et
  devra y être consignée en amendement au moment où elle sera codée.
- Aucun contrat HTTP existant ne change de forme : `GET /games/{id}` devient `Fold(events)` mais
  rend le même DTO, `GET /games/{id}/history` devient une projection du journal filtrée sur
  `ShotFired`. Un nouveau `GET /games/{id}/events?from={n}` sert le flux incrémental censuré. Cette
  invariance de forme est ce qui rend le refactor vérifiable sans toucher aux tests existants.
- Les deux limites connues du `README.md` évoluent : « le rejeu ne reconstitue pas l'état coulé
  intermédiaire » disparaît sans code dédié (`Fold` sur un préfixe est correct par construction) ;
  « redémarrer l'API perd les parties » survit, assumé au backlog.
- Risque assumé : `Game._history` et les plateaux étaient deux vérités visiblement parallèles ;
  après ce refactor, le journal est la seule vérité et les plateaux en sont le produit — un
  contributeur qui muterait directement un `HashSet` hors de `Apply` réintroduirait la
  duplication en silence. La revue de code est la garde-fou, comme pour le risque équivalent noté
  à l'ADR 0002 sur `Mutate`.
- La sérialisation JSON d'événements polymorphes (discriminateur `[JsonPolymorphic]` /
  `[JsonDerivedType]`) est un risque technique distinct, à lever par un `dotnet run --file` isolé
  avant d'écrire le contrat HTTP, conformément au `CLAUDE.md` § 6. Repli si la vérification échoue :
  une union plate à un seul record avec champ `Type` et champs nullables.

## Vérification et réexamen

- À exécuter et consigner dans `REVUE-IA.md` (quatrième revue) : les cinq tests du § 6.2 de la
  spec — totalité de `Apply` sur tout préfixe d'un journal valide ; état des navires coulés correct
  à l'étape *n* d'une partie à graine fixe (ce test doit **échouer sur le code actuel** avant
  correction, exigence du `CLAUDE.md` § 6) ; `/events` ne fuite aucune coordonnée d'un navire
  adverse non coulé pendant la partie ; `/events` les contient après `GameEnded` ; `ReadEvents(from:
  n)` renvoie exactement la queue du journal à partir du rang `n` inclus.
- Preuve d'invariance déjà disponible : les tests actuels ne changent pas de forme, ni les
  contrats HTTP/gRPC ni la surface publique du domaine ne changeant — s'ils doivent être modifiés,
  c'est un signal d'alarme à comprendre avant de les toucher, pas un ajustement de routine.
- À réexaminer si une implémentation réellement asynchrone ou multi-processus du store apparaissait
  : la concurrence optimiste, écartée ici pour un store mono-processus en mémoire, redeviendrait à
  examiner.

## Références

- Spec de conception : `docs/superpowers/specs/2026-09-17-journal-evenements-design.md`
- `CLAUDE.md` § 3 bis (liste fermée des patterns, Repository, gestion des erreurs) et § 6
  (discipline de vérification)
- ADR 0001 (représentation de l'état) — précisé
- ADR 0002 (`IGameStore`, verrou par partie) — étendu d'un membre de lecture
- ADR 0004 (`Result<T>` pour les refus métier) — appliqué plus strictement
- ADR 0008 (contrat partagé dans `Models`) — inchangé, il rend le repli partageable entre
  serveur et front
- ADR 0007 (état côté Blazor) — non inchangé : sa condition de réexamen (persister
  l'identifiant de partie pour une reprise après rechargement) est atteinte par la spec § 5.3,
  à consigner en amendement
