# Conception — Le journal d'événements du domaine

Date : 2026-09-17
Statut : validé par le binôme à l'issue de la session de brainstorming du 2026-09-17.
Source des décisions : `PROMPTS.md`, entrée « 2026-09-17 — Brainstorming du journal d'événements ».
Décision structurante attendue : **ADR 0011**, à écrire *avant* la première ligne de code.

Ce document décrit le premier des deux axes retenus pour étendre le projet au-delà du socle
livré le 2026-09-17. Le second axe — un rendu 3D en couche de présentation — fera l'objet
d'une spec distincte et **postérieure**, car il consomme ce que celui-ci établit.

---

## 1. Le constat qui motive la décision

Le moteur fait **déjà** de l'event sourcing à moitié, sans que ce soit une décision assumée.

`Game` tient `_history`, une liste ordonnée de `ShotRecord`. `DtoMappings.ToOwnBoardDto`
dérive déjà les cases reçues **depuis cet historique**. Mais en parallèle, `Board._receivedShots`
et `Ship._hitCells` stockent la même information sous forme mutable.

```
  Game.Fire(at)                          Game.cs
    |
    +--> Board.Fire(at)                  Board.cs
    |      |
    |      +--> _receivedShots.Add(at)      \  deux écritures
    |      +--> ship._hitCells.Add(at)      /  dans Board
    |
    +--> _history.Add(record)            Game.cs:85  <- la troisième

  Ship.IsSunk        <- dérivé de _hitCells
  ToOwnBoardDto      <- dérivé de _history     <-- déjà le repli
```

Un même tir écrit donc le même fait à **trois endroits** — deux depuis `Board.Fire`, un
depuis `Game.Fire` qui l'appelle — et deux lectures puisent dans deux sources différentes.
C'est cette duplication que le journal supprime.

**Le refactor n'ajoute pas une couche, il en retire une.** C'est l'argument central de
l'ADR 0011 : ici l'event sourcing est une simplification, pas une cérémonie.

### Les deux limites connues que cela résout

Extraites du `README.md` livré :

- « Le rejeu ne rejoue que les **coups** : il ne reconstitue pas l'état "coulé" intermédiaire. »
- « Le front ne conserve pas l'identifiant, donc **recharger la page perd la partie en cours**. »

La première n'est pas un manque de code : c'est le symptôme d'une forme de donnée inadaptée.
Avec un journal, l'état à l'instant *n* est `Fold(events.Take(n))` ; l'état « coulé »
intermédiaire est correct **par construction**, sans code dédié.

---

## 2. La forme du domaine

### 2.1 Les événements

Dans `BattleShip.Models`. Un événement enregistre **ce qui est arrivé**, jamais ce qui a été
demandé.

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

`ShotFired` porte le **résultat** du tir, pas seulement son intention. C'est ce qui permet au
repli de ne consulter aucun plateau pour savoir si un coup a touché.

### 2.1 bis Pourquoi `ShipSnapshot` et non `Ship`

C'est le piège le plus coûteux du design, et il a été trouvé en relecture après une première
rédaction fautive.

`Ship` est **mutable** : il porte `_hitCells`, que `TryHit` remplit. Et `Board` copie la
**liste** qu'on lui donne, pas les objets (`Ships { get; } = [.. ships]`). Un événement qui
porterait `IReadOnlyList<Ship>` partagerait donc ses instances avec le plateau :

```
  GameCreated(seq, rules, opponentShips, diff)
                             |
                             |  MÊMES instances
                             v
  Game.OpponentBoard.Ships --+
        |
        |  chaque tir appelle ship.TryHit() -> mute _hitCells
        v
  l'événement « déjà arrivé » change rétroactivement
```

Deux conséquences, la seconde étant fatale :

1. Le journal cesse d'être en ajout seul : un fait enregistré se modifie après coup.
2. **`Fold` se contamine lui-même.** Replier un préfixe mute les `Ship` du journal, donc le
   repli suivant démarre d'un état déjà touché. Le rejeu devient faux — et
   `Folding_every_prefix_never_throws` resterait **vert**, puisqu'il ne constate qu'une absence
   d'exception. Un test au vert sur un système cassé.

`ShipSnapshot` est une donnée pure : nom, taille, cases. Il **ne peut pas** porter d'état de
touche. `Fold` construit des `Ship` neufs à partir des instantanés, à chaque repli.

C'est exactement le geste de l'**ADR 0003** pour `ShotHistory` — « l'invariant tient dans le
type, pas dans la relecture ». Ici l'invariant « un événement ne change jamais » est porté par
le type, et non par la discipline de celui qui écrit `Fold`.

`Sequence` est un entier contigu à partir de 0, propre à une partie. Il sert le rang de
lecture incrémentale (§ 4.2) et le curseur de rejeu (§ 5.1).

### 2.2 Décider et appliquer

Deux fonctions, deux registres d'erreur. Cette séparation est l'application stricte de
l'**ADR 0004**, plus stricte qu'aujourd'hui.

| | rôle | peut refuser ? | registre d'erreur |
|---|---|---|---|
| `Decide(command)` | valider contre l'état courant, produire les événements | **oui** | `Result<IReadOnlyList<GameEvent>>` — case rejouée, hors grille, partie finie, placement invalide |
| `Apply(state, event)` | replier l'événement dans l'état | **jamais** | exception si l'impossible survient |

`Apply` est **total** : il ne peut pas échouer, parce qu'un événement est un fait déjà arrivé.
Cette totalité est l'invariant central du design, et elle est testable (§ 6, test 1).

Un tir produit un ou deux événements : `ShotFired`, suivi de `GameEnded` si ce tir a coulé le
dernier navire d'un camp. `Decide` renvoie donc une **liste**, jamais un événement seul.

### 2.3 Ce qui change : `Board` et `Ship` deviennent l'accumulateur du repli

C'est le point le plus facile à énoncer de travers, alors il est explicité.

Les `HashSet` mutés **ne disparaissent pas**. Les supprimer priverait `Ship.HitCells` et
`Ship.IsSunk` de leur support, casserait la surface publique du domaine, et détruirait le filet
de vérification du § 6.1. Ils changent de **statut**.

| élément | aujourd'hui | après |
|---|---|---|
| `Game._history` (`List<ShotRecord>`) | vérité **parallèle** aux plateaux | **remplacé** par `Game._events` |
| `Board._receivedShots` | vérité parallèle à `_history` | **conservé** — devient l'accumulateur du repli |
| `Ship._hitCells` | idem | **conservé** — idem |
| `Board.Fire(at, by)` | décide **et** mute | **scindé** : `Decide` pur, `Apply` total |
| surface publique de `Board`, `Ship`, `Game` | — | **inchangée** |

La duplication supprimée n'est donc pas celle des `HashSet`, c'est celle des **sources de
vérité**. Aujourd'hui `_history` et les plateaux sont deux vérités écrites en parallèle par la
même méthode, et deux lectures différentes puisent dans l'une ou l'autre. Après, il n'y a qu'une
seule vérité — le journal — et les plateaux en sont le **produit**, reconstructibles à tout
moment.

C'est précisément ce qui rend `Fold(events.Take(n))` possible : construire des plateaux neufs à
partir de `GameCreated` et `HumanFleetPlaced`, puis y rejouer les *n* premiers `ShotFired`.
L'état « coulé » à l'étape *n* est alors celui qu'avaient réellement les navires à l'étape *n*,
et non l'état final projeté en arrière — ce qui est exactement le bug documenté.

### 2.4 Ce qui n'est PAS fait

À écrire explicitement dans l'ADR 0011, car ce sont les confusions attendues à l'oral :

- **Pas de bus d'événements.** Aucun `IEventHandler`, aucune publication, aucun abonné.
- **Pas de projection asynchrone.** Le repli est synchrone et calculé à la demande.
- **Pas de CQRS.** Aucune séparation lecture/écriture : un seul modèle, un seul `Fold`.
- **Pas de MediatR.** `Decide` est une méthode du domaine, pas un message routé.

Ces quatre absences sont cohérentes avec la section « Ce qui n'est pas retenu » du `CLAUDE.md`.
Ce qui est retenu tient en deux mots : **des événements, et un `fold`**.

---

## 3. Le secret — le point le plus dangereux du design

`GameCreated` porte `OpponentShips`, donc **les positions de la flotte adverse**. Exposer un
endpoint `/events` sans précaution publierait au joueur exactement ce que la revue 1 de
`REVUE-IA.md` avait établi comme inviolable.

### 3.1 Censure par champ, pas par événement

La projection servie au client émet `GameCreated` avec `OpponentShips = []`. La taille de
grille, la flotte et la difficulté **ne sont pas secrètes** — elles figurent déjà dans
`GameDto`. Seules les *positions* le sont.

Retenir l'événement entier serait plus simple mais casserait le repli côté front (§ 5.2) :
sans `GameCreated`, le repli n'aurait ni taille de grille ni règles.

### 3.2 La frontière est temporelle

```
  JOURNAL (serveur, complet)          PROJECTION SERVIE AU JOUEUR
  ----------------------------        ------------------------------------
  GameCreated(rules, ships, diff) ->  GameCreated(rules, [], diff)
  HumanFleetPlaced(ships)         ->  HumanFleetPlaced(ships)
  ShotFired(3,4,Human,Hit)        ->  ShotFired(3,4,Human,Hit)
  ShotFired(7,1,Opponent,Miss)    ->  ShotFired(7,1,Opponent,Miss)
  GameEnded(Human)                ->  GameEnded(Human)
                                          |
                                  à partir d'ici, le journal
                                  COMPLET devient servable.
```

Pendant la partie, `OpponentShips` est vidé. Après `GameEnded`, le journal complet est servi.

Cette bascule n'est pas une concession : c'est ce qui rend la **révélation d'après-partie** une
fonctionnalité au lieu d'une fuite. Elle est aussi la condition de possibilité de la
rediffusion prévue par l'axe C.

### 3.3 Nuance sur les navires coulés

Un navire adverse **coulé** a ses positions déjà connues du joueur : elles se déduisent des
`ShotFired` de résultat `Hit` et `Sunk`. Le champ `OpponentShips` est donc vidé **intégralement**
pendant la partie, navires coulés compris — on ne cherche pas à n'y laisser que les coulés.
C'est plus simple, plus sûr, et ça ne retire rien au joueur, puisque l'information est
reconstituée par le repli des tirs.

Le test 3 (§ 6) est formulé en conséquence : il porte sur les navires **non coulés**.

---

## 4. Le store et l'API

### 4.1 L'ADR 0002 n'est pas remplacé

La tentation canonique serait de remplacer le verrou par partie par une concurrence optimiste
(`Append(id, expectedVersion, events)`).

**Elle est écartée, et l'ADR 0011 doit le dire.** Le verrou par partie de l'ADR 0002 est
correct, justifié et déjà testé (`InMemoryGameStoreTests`). La concurrence optimiste
ajouterait des boucles de réessai et une classe d'erreur supplémentaire pour résoudre, moins
bien, un problème qu'un verrou résout déjà dans un store mono-processus en mémoire. Ce serait
le pattern posé pour lui-même que le `CLAUDE.md` § 3 bis dit coûter des points.

Ce qui manque au contrat n'est pas une capacité d'écriture, mais une capacité de **lecture**.

### 4.2 Un seul membre ajouté

```csharp
public interface IGameStore
{
    // inchangés : Find, Save, Remove, Mutate, Read

    // Lecture incrémentale du journal à partir d'un rang.
    Result<IReadOnlyList<GameEvent>> ReadEvents(Guid id, int fromSequence);
}
```

`ReadEvents` s'implémente sous le même verrou que `Read`, et renvoie le journal **complet**
(non censuré) : la censure est une responsabilité de la façade HTTP, pas du store. Le store
sert la vérité ; l'endpoint décide de ce qui en sort.

### 4.3 Le contrat HTTP

| route | statut | rôle |
|---|---|---|
| `GET /games/{id}/events?from={n}` | **nouveau** | le flux censuré, incrémental |
| `GET /games/{id}/history` | conservé | devient une *projection* du journal, filtrée sur `ShotFired` |
| `GET /games/{id}` | conservé | devient `Fold(events)` — même DTO, même forme |

`from` est **inclusif** : `from=0` renvoie tout le journal, `from=n` renvoie les événements de
rang `n` compris jusqu'à la fin. Un client qui a déjà traité jusqu'au rang `k` redemande
donc `from=k+1`. Cette convention est à réaffirmer dans le test 5, faute de quoi un décalage
d'une unité passerait inaperçu.

`from` est validé par FluentValidation comme les autres entrées : entier, `>= 0`, avec défaut
`0` s'il est absent. Un `from` supérieur au rang maximal renvoie une liste vide et un `200`,
pas une erreur : demander la suite d'un journal à jour est un cas normal, pas un refus métier.

**Aucun contrat existant ne change de forme**, et la surface publique du domaine est préservée
(§ 2.3). C'est la propriété qui rend le refactor vérifiable (§ 6.1).

### 4.4 Risque technique à lever avant d'écrire le contrat

Les événements sont des records polymorphes. Leur sérialisation JSON demande un discriminateur
(`[JsonPolymorphic]` / `[JsonDerivedType]`), et le front Blazor WebAssembly sérialise **par
réflexion** — le `README.md` signale déjà que son comportement sous publication trimmée est
inconnu.

Conformément au `CLAUDE.md` § 6.3, ce point se tranche par un `dotnet run --file` isolé
**avant** d'écrire le contrat, et non sur une affirmation de mémoire.

Repli si la vérification échoue : une union plate, un seul record portant un champ `Type` et
les champs de toutes les variantes en nullable. Moins élégant, sûr, et décidé sur preuve.

---

## 5. Le front

### 5.1 Le repli est partagé, pas réimplémenté

`BattleShip.Models` ne dépend de rien et est référencé par `BattleShip.API` **et**
`BattleShip.App` (ADR 0008). Donc `Fold` vit dans `Models`, et le front rejoue la partie avec
**exactement le même code** que le serveur.

```
              BattleShip.Models
              +--------------------------+
              |  Fold(events) -> Game    |
              +--------------------------+
                 ^                    ^
                 |                    |
        BattleShip.API          BattleShip.App
        (journal complet)       (journal censuré)
                 |                    |
              état VRAI            état VU
```

Aucune seconde machine à états à maintenir, donc aucune dérive silencieuse possible entre ce
que le serveur calcule et ce que le front affiche.

`Fold` rend un **`Game`**, pas un type de vue dédié. Conséquence pratique vérifiée :
`BattleShip.App` référence `BattleShip.Models` et **rien d'autre**, donc le front lit directement
`game.HumanBoard.Ships` et `game.OpponentBoard.Ships.Where(s => s.IsSunk)` sur l'état replié.
Aucun besoin de déplacer `DtoMappings` hors de `BattleShip.API`, et aucune logique de projection
dupliquée côté front.

### 5.2 Une fonction, deux entrées, sortie correcte dans les deux cas

C'est ce que la censure par champ (§ 3.1) rend possible :

- **Pendant la partie** — le front replie un journal où `OpponentShips` est vide. Il obtient un
  état où la flotte adverse est inconnue : exactement ce qu'il doit afficher.
- **Après `GameEnded`** — le serveur sert le journal complet ; le même repli produit la vérité,
  positions adverses comprises.

La révélation de fin de partie n'est pas un mode d'affichage spécial : c'est le même repli sur
plus d'information.

### 5.3 Changements dans `BattleShip.App`

- **Curseur de rejeu** (`HistoryPanel`) : consomme `/events` et replie jusqu'au rang *n*. La
  limite « ne reconstitue pas l'état coulé intermédiaire » disparaît sans code dédié.
- **Reprise après rechargement** : l'identifiant de partie en `localStorage`, relu au
  démarrage, `GET /games/{id}`, effacé sur `404`. Indépendant du journal, mais plus aucune
  raison d'attendre.
- **`GameState`** garde sa forme actuelle. L'ADR 0007 reste valide : le flux d'événements
  alimente le rejeu, pas la boucle de jeu.

### 5.4 Ce qui n'est PAS fait

Remplacer le rafraîchissement de `GameDto` par un flux incrémental fonctionnerait, ne
résoudrait aucun problème existant, et ajouterait un chemin de synchronisation à déboguer.
Ce sera la bonne décision le jour où le temps réel multijoueur arrivera — pas avant.

---

## 6. Vérification

### 6.1 Le filet existant est la preuve principale

Ni les contrats HTTP/gRPC (§ 4.3) ni la surface publique du domaine (§ 2.3) ne changent de
forme. Donc **les tests actuels passent sans modification, ou le refactor est faux.** Si le
repli produit le même état que la mutation, c'est que les deux calculent la même chose.

Toute retouche à un test existant est un **signal d'alarme**, pas un ajustement. Si un test
doit changer, la cause est à comprendre et à consigner avant de le toucher.

### 6.2 Les cinq tests nouveaux

| # | ce qu'il affirme | l'erreur qu'il détecte |
|---|---|---|
| 1 | `Apply` est total : replier **tout préfixe** d'un journal valide ne lève jamais | un `Apply` qui suppose un ordre ou un état préalable |
| 2 | À l'étape *n* d'une partie enregistrée (graine fixe), la liste des navires coulés est exactement celle attendue | le bug de rejeu actuellement documenté |
| 3 | `/events` en cours de partie ne contient **aucune** coordonnée d'un navire adverse non coulé, sérialisation comprise | une fuite du secret par le nouveau canal |
| 4 | `/events` après `GameEnded` **contient** ces coordonnées | une censure trop large |
| 5 | `ReadEvents(from: n)` renvoie exactement la queue du journal | un décalage de rang |

Les tests 3 et 4 forment une **paire délibérée**. Chacun pris seul est satisfait par une
implémentation triviale et fausse : le test 3 passerait avec un endpoint qui ne renvoie rien,
le test 4 passerait avec un endpoint qui ne censure jamais. Ensemble, ils encadrent la
frontière temporelle du § 3.2.

Le test 2 doit **échouer sur le code actuel** avant d'être rendu vert. Cette vérification est
exigée par le `CLAUDE.md` § 6 et doit être consignée dans `REVUE-IA.md`.

Le test 1 s'appuie sur des parties engendrées à graine fixe, conformément à la section
« Injection de la source d'aléa » du `CLAUDE.md` : un contrôle non déterministe sur un
invariant de totalité passerait presque toujours sans rien prouver.

### 6.3 Ce qui reste non vérifié

- Le comportement de la sérialisation polymorphe sous publication trimmée reste inconnu, comme
  aujourd'hui pour le reste du front.
- La totalité de `Apply` est établie sur des journaux **engendrés par `Decide`**. Un journal
  forgé à la main hors de ces chemins n'est pas couvert, et n'a pas à l'être : seul `Decide`
  écrit dans le journal.

---

## 7. Ordre de travail

1. **ADR 0011 écrit avant la première ligne de code** — exigé par le `CLAUDE.md` § 3 bis pour
   tout pattern hors liste fermée. Contenu obligatoire : pourquoi ce n'est pas MediatR/CQRS
   (§ 2.4), pourquoi la concurrence optimiste est écartée (§ 4.1), et pourquoi la persistance
   est rendue triviale mais volontairement non faite (§ 8).
2. **Spike de sérialisation** en `dotnet run --file` (§ 4.4). Le contrat se décide sur cette
   sortie.
3. Événements, `Decide` et `Apply` dans `Models`, **en conservant la surface publique actuelle
   de `Game`**. Les tests restent verts en continu ; pas de grand saut.
4. Suppression de l'état dupliqué (§ 2.3).
5. `ReadEvents` dans `IGameStore` et `InMemoryGameStore` (§ 4.2), test 5.
6. `GET /games/{id}/events`, la projection censurée, les tests 3 et 4.
7. Front : rejeu par repli partagé, reprise par `localStorage` (§ 5.3).
8. Livrables : `PROMPTS.md`, une **quatrième revue** dans `REVUE-IA.md`, `README.md`,
   `docs/conformite.md`.

Les étapes 3 à 7 avancent en TDD, test rouge d'abord.

### La quatrième revue IA

Candidat retenu : **la censure par champ**.

- Hypothèse à vérifier : aucune position d'un navire adverse non coulé ne franchit le nouveau
  canal `/events` pendant la partie, et toutes la franchissent après `GameEnded`.
- Contrôle : la paire de tests 3 et 4, dont le pouvoir discriminant est justement d'interdire
  les deux implémentations triviales.

---

## 8. Hors périmètre, et pourquoi

- **Persistance sur disque.** Le journal la rend quasi gratuite — un fichier append-only, aucune
  ligne jamais modifiée. Elle reste néanmoins au backlog : la décision du binôme est de garder
  cette spec sur un seul sujet. La limite connue « redémarrer l'API perd les parties » survit
  donc, et doit rester écrite dans le `README.md`.
- **Temps réel multijoueur.** Le journal en est la brique naturelle (diffuser les événements à
  partir d'un rang), mais c'est un axe distinct, avec sa propre plomberie hors domaine.
- **Rendu 3D.** C'est l'axe C, qui aura sa propre spec et consommera le flux défini ici.
- **Concurrence optimiste.** Écartée, motivée au § 4.1.

---

## 9. Références

- `CLAUDE.md` § 3 bis (liste fermée des patterns), § 6 (discipline de vérification)
- ADR 0001 (représentation de l'état) — **précisé**, non remplacé : la représentation ne change
  pas, sa dérivation change
- ADR 0002 (`IGameStore`, verrou par partie) — **étendu** d'un membre, non remplacé
- ADR 0004 (`Result<T>` pour les refus) — appliqué plus strictement par la séparation
  `Decide` / `Apply`
- ADR 0007 (état côté Blazor) — inchangé
- ADR 0008 (le contrat partagé vit dans `Models`) — c'est lui qui rend le repli partagé possible
- `REVUE-IA.md` revue 1 (le secret) — étendue au nouveau canal
- `README.md` § Limites connues — deux entrées résolues, une conservée
