# Plan d'implémentation — Le journal d'événements du domaine

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Faire du journal d'événements la source de vérité unique de l'état de partie, pour que le rejeu à l'étape *n* soit correct par construction et que le flux d'événements soit servable au front sans fuite du secret.

**Architecture:** `Game` remplace sa liste de `ShotRecord` par une liste de `GameEvent`. Chaque commande se scinde en `Decide` (valide, peut refuser via `Result<T>`) et `Apply` (replie, ne peut jamais échouer). `Board` et `Ship` gardent leurs `HashSet` mutés, qui deviennent **l'accumulateur du repli** au lieu d'une vérité parallèle. `GameFold.Fold(id, events)` reconstruit un `Game` complet depuis zéro, ce qui rend le rejeu à l'étape *n* égal à `Fold(events.Take(n))`. Le front, qui référence `BattleShip.Models`, utilise **le même `Fold`** que le serveur.

**Tech Stack:** .NET 10, C# 14, xUnit, FluentValidation, ASP.NET Core Minimal API, Blazor WebAssembly, System.Text.Json.

**Spec:** `docs/superpowers/specs/2026-09-17-journal-evenements-design.md`

## Global Constraints

Valeurs reprises telles quelles du `CLAUDE.md` et de la spec. Elles s'appliquent à **toutes** les tâches.

- **Langue** : tout le code est en **anglais** — identifiants, littéraux, texte affiché. La documentation destinée aux humains (ADR, `PROMPTS.md`, `REVUE-IA.md`, `README.md`, spec, plan) et **les messages de commit** sont en **français**.
- **Aucun commentaire dans le code.** Décision du binôme du 2026-09-17. Ni `//`, ni `/* */`, ni documentation XML. Ce qu'un commentaire expliquerait va dans un ADR.
- **Nommage** : `PascalCase` types/méthodes/propriétés publiques ; `camelCase` paramètres/locales ; `_camelCase` champs privés ; `IPascalCase` interfaces ; suffixe `Async` sur tout `Task`/`Task<T>`.
- **Noms de tests** : anglais descriptif, mots séparés par des underscores, style `A_shot_outside_the_grid_is_rejected`.
- **Nullable activé** : traiter les avertissements, ne pas les faire taire.
- **`sealed` par défaut** sur les classes non prévues pour l'héritage.
- **Deux registres d'erreur, jamais mélangés** (ADR 0004) : refus métier → `Result<T>` + `GameError` ; anomalies → exceptions typées.
- **Aucun `.Result` ni `.Wait()`.**
- **`dotnet build` doit rester à zéro avertissement.**
- **`dotnet format`** avant chaque commit.
- **Aléa injecté** : jamais `Random.Shared` en dur dans le domaine ou les tests ; toujours `new Random(<graine fixe>)`.
- **Surface publique préservée** : aucune signature publique existante de `Game`, `Board`, `Ship`, `IGameStore`, ni aucun contrat HTTP/gRPC ne change de forme. C'est la condition du filet de vérification.
- **Les tests existants passent sans modification.** Si un test doit changer, **s'arrêter** et remonter la cause ; ce n'est pas un ajustement, c'est un signal.
- **`from` est inclusif** : `from=n` renvoie les événements de rang `n` compris jusqu'à la fin.

### Commandes de référence

```bash
dotnet build                                   # zéro avertissement attendu
dotnet test                                    # 145 tests au vert avant de commencer
dotnet test --filter "FullyQualifiedName~<Nom>"
dotnet format
```

---

## Structure des fichiers

| fichier | responsabilité |
|---|---|
| `BattleShip.Models/Events/GameEvent.cs` | **créé** — la hiérarchie des quatre événements |
| `BattleShip.Models/Events/GameFold.cs` | **créé** — `Fold(id, events)` : reconstruit un `Game` depuis un journal |
| `BattleShip.Models/Board.cs` | **modifié** — `Fire` scindé en `Decide` + `Apply` |
| `BattleShip.Models/Game.cs` | **modifié** — `_history` remplacé par `_events` ; `History` devient dérivée |
| `BattleShip.Models/IGameStore.cs` | **modifié** — `ReadEvents` ajouté |
| `BattleShip.Models/Contracts/GameEventDto.cs` | **créé** — le contrat de transport des événements |
| `BattleShip.API/Stores/InMemoryGameStore.cs` | **modifié** — implémente `ReadEvents` |
| `BattleShip.API/Contracts/EventProjection.cs` | **créé** — la censure par champ, un seul endroit |
| `BattleShip.API/Endpoints/GameEndpoints.cs` | **modifié** — `GET /games/{id}/events` |
| `BattleShip.API/Validation/EventQueryValidator.cs` | **créé** — validation de `from` |
| `BattleShip.App/Services/BattleApiClient.cs` | **modifié** — `GetEventsAsync` |
| `BattleShip.App/Services/GameSessionStorage.cs` | **créé** — l'identifiant de partie en `localStorage` |
| `BattleShip.App/Pages/Play.razor` | **modifié** — le rejeu passe par `Fold` |
| `BattleShip.Tests/Domain/GameEventTests.cs` | **créé** — totalité de `Apply`, régression du rejeu |
| `BattleShip.Tests/Domain/GameFoldTests.cs` | **créé** — équivalence repli / mutation |
| `BattleShip.Tests/Api/EventsEndpointTests.cs` | **créé** — la paire censure 3 / 4, et `from` |
| `docs/adr/0011-journal-evenements.md` | **créé** — la décision, **avant** tout code |

---

## Tâche 0 : l'ADR, avant la première ligne de code

Le `CLAUDE.md` § 3 bis ferme la liste des patterns : tout ajout **se décide et s'écrit en ADR avant d'être codé**. Cette tâche n'est pas de la paperasse à rattraper, c'est la porte d'entrée.

**Files:**
- Create: `docs/adr/0011-journal-evenements.md`

**Interfaces:**
- Consumes: la spec `docs/superpowers/specs/2026-09-17-journal-evenements-design.md`
- Produces: rien de codé ; toutes les tâches suivantes y renvoient

- [ ] **Étape 1 : lire le gabarit et un ADR existant**

Run: `cat docs/adr/0001-modele.md docs/adr/0004-result-refus-metier.md`

Le gabarit impose six sections : `Statut et date`, `Contexte`, `Options envisagées`, `Décision`, `Conséquences`, `Vérification et réexamen`, `Références`.

- [ ] **Étape 2 : écrire l'ADR**

Contenu obligatoire, chaque point étant une objection attendue à l'oral :

1. **Contexte** — le moteur fait déjà un demi-repli : `Game._history` existe, `DtoMappings.ToOwnBoardDto` en dérive déjà les cases reçues, pendant que `Board._receivedShots` et `Ship._hitCells` portent le même fait. `Board.Fire` écrit le même fait à trois endroits. Deux lectures puisent dans deux sources.
2. **Décision** — le journal devient la source de vérité unique ; `Board` et `Ship` deviennent l'accumulateur du repli (leurs `HashSet` sont **conservés**, ils changent de statut, pas d'existence).
3. **Ce que ce n'est pas** — pas de bus d'événements, pas de projection asynchrone, pas de CQRS, pas de MediatR. Cohérent avec la section « Ce qui n'est pas retenu » du `CLAUDE.md`.
4. **Concurrence optimiste examinée puis écartée** — le verrou par partie de l'ADR 0002 est correct et testé (`InMemoryGameStoreTests.Only_one_concurrent_shot_on_the_same_cell_succeeds`) ; `expectedVersion` ajouterait des boucles de réessai et une classe d'erreur pour résoudre moins bien un problème déjà résolu dans un store mono-processus. `IGameStore` n'est étendu que d'un membre de **lecture**.
5. **Persistance rendue triviale mais volontairement non faite** — un journal append-only se persiste en quelques dizaines de lignes ; le binôme a choisi de garder une spec sur un seul sujet. La limite connue « redémarrer l'API perd les parties » reste vraie et reste écrite au `README.md`.
6. **Rapport aux ADR existants** — 0001 *précisé* (la représentation ne change pas, sa dérivation change), 0002 *étendu* d'un membre, 0004 *appliqué plus strictement* par `Decide`/`Apply`, 0007 et 0008 inchangés.
7. **Vérification et réexamen** — renvoyer aux cinq tests du § 6.2 de la spec ; condition de réexamen : si une implémentation réellement asynchrone ou multi-processus du store apparaît, la concurrence optimiste redevient à examiner.

- [ ] **Étape 3 : commit**

```bash
dotnet format
git add docs/adr/0011-journal-evenements.md
git commit -m "docs: ADR 0011 — le journal d'événements comme source de vérité

Décidé avant tout code, comme le CLAUDE.md § 3 bis l'exige pour un
pattern hors liste fermée. Documente aussi les deux écarts : ni CQRS
ni bus d'événements, et concurrence optimiste écartée au profit du
verrou par partie de l'ADR 0002.

Co-Authored-By: Claude Opus 5 (1M context) <noreply@anthropic.com>"
```

---

## Tâche 1 : le spike de sérialisation — décider sur une exécution, pas de mémoire

Le `CLAUDE.md` § 6.3 interdit de trancher ce point sur une affirmation. Cette tâche produit **une réponse**, pas du code conservé.

**Files:**
- Create: `/tmp/claude-1000/-home-ladretti-Bureau-DOTNET-DOTNET/fe5385d0-09f6-4c07-b586-fbd0611d0690/scratchpad/polymorphic.cs` (jetable, hors dépôt)

**Interfaces:**
- Consumes: rien
- Produces: la décision de forme pour `GameEventDto` (tâche 5) — soit hiérarchie polymorphe, soit union plate

- [ ] **Étape 1 : écrire l'essai**

```csharp
#:property Nullable=enable

using System.Text.Json;
using System.Text.Json.Serialization;

[JsonPolymorphic(TypeDiscriminatorPropertyName = "type")]
[JsonDerivedType(typeof(ShotFiredDto), "shotFired")]
[JsonDerivedType(typeof(GameEndedDto), "gameEnded")]
public abstract record GameEventDto(int Sequence);

public sealed record ShotFiredDto(int Sequence, int X, int Y, string By, string Result, string? SunkShipName)
    : GameEventDto(Sequence);

public sealed record GameEndedDto(int Sequence, string Winner) : GameEventDto(Sequence);

var events = new List<GameEventDto>
{
    new ShotFiredDto(0, 3, 4, "Human", "hit", null),
    new GameEndedDto(1, "Human")
};

var json = JsonSerializer.Serialize(events);
Console.WriteLine(json);

var back = JsonSerializer.Deserialize<List<GameEventDto>>(json)!;
Console.WriteLine($"count={back.Count} first={back[0].GetType().Name} second={back[1].GetType().Name}");
```

- [ ] **Étape 2 : énoncer le résultat attendu AVANT d'exécuter**

Écrire dans `PROMPTS.md` (ou dans le presse-papier de la session) **avant** de lancer :

> Attendu : un JSON où chaque élément porte `"type":"shotFired"` / `"gameEnded"`, puis `count=2 first=ShotFiredDto second=GameEndedDto`.
> Ce contrôle détecte : une désérialisation qui rendrait la classe de base, ou qui lèverait faute de discriminateur.

- [ ] **Étape 3 : exécuter**

Run: `dotnet run --file /tmp/claude-1000/-home-ladretti-Bureau-DOTNET-DOTNET/fe5385d0-09f6-4c07-b586-fbd0611d0690/scratchpad/polymorphic.cs`

- [ ] **Étape 4 : comparer et décider**

- **Si observé = attendu** → la tâche 5 utilise la **hiérarchie polymorphe** ci-dessus.
- **Si divergence** → la tâche 5 utilise l'**union plate** de repli :

```csharp
public sealed record GameEventDto(
    int Sequence, string Type, int? X, int? Y, string? By,
    string? Result, string? SunkShipName, string? Winner,
    int? GridSize, IReadOnlyList<ShipDto>? Ships);
```

- [ ] **Étape 5 : vérifier côté Blazor WebAssembly**

Le spike ci-dessus tourne sur le **runtime serveur**. Le front est en WASM, avec sérialisation par réflexion. Confirmer en conditions réelles après la tâche 6, quand `/games/{id}/events` existe : ouvrir le front, appeler l'endpoint, et vérifier dans la console F12 que les objets désérialisés ont le bon type.

**Rien à committer** — c'est un spike, son produit est une décision. Ne pas conserver le fichier dans le dépôt.

---

## Tâche 2 : les événements

**Files:**
- Create: `BattleShip.Models/Events/GameEvent.cs`
- Test: `BattleShip.Tests/Domain/GameEventTests.cs`

**Interfaces:**
- Consumes: `GameRules`, `Ship`, `Coordinate`, `Player`, `ShotResult` (existants)
- Produces: `GameEvent`, `GameCreated`, `HumanFleetPlaced`, `ShotFired`, `GameEnded` — tous dans `BattleShip.Models`, tous avec `int Sequence` en **premier** paramètre positionnel

- [ ] **Étape 1 : écrire le test qui échoue**

```csharp
using BattleShip.Models;

namespace BattleShip.Tests.Domain;

public sealed class GameEventTests
{
    [Fact]
    public void A_shot_fired_event_carries_the_outcome_and_not_only_the_intent()
    {
        var fired = new ShotFired(7, new Coordinate(3, 4), Player.Human, ShotResult.Sunk, "Destroyer");

        Assert.Equal(7, fired.Sequence);
        Assert.Equal(new Coordinate(3, 4), fired.At);
        Assert.Equal(Player.Human, fired.By);
        Assert.Equal(ShotResult.Sunk, fired.Result);
        Assert.Equal("Destroyer", fired.SunkShipName);
    }

    [Fact]
    public void Events_are_compared_by_value()
    {
        var first = new ShotFired(1, new Coordinate(0, 0), Player.Human, ShotResult.Miss, null);
        var second = new ShotFired(1, new Coordinate(0, 0), Player.Human, ShotResult.Miss, null);

        Assert.Equal(first, second);
    }

    [Fact]
    public void Every_event_is_a_game_event()
    {
        var rules = GameRules.Default;
        IReadOnlyList<ShipSnapshot> fleet =
            [.. new FleetPlacer(new Random(11)).PlaceAll(rules).Value.Select(ShipSnapshot.Of)];

        GameEvent[] events =
        [
            new GameCreated(0, rules, fleet, "Normal"),
            new HumanFleetPlaced(1, fleet),
            new ShotFired(2, new Coordinate(0, 0), Player.Human, ShotResult.Miss, null),
            new GameEnded(3, Player.Human)
        ];

        Assert.Equal([0, 1, 2, 3], events.Select(e => e.Sequence));
    }

    [Fact]
    public void A_snapshot_does_not_share_hit_state_with_the_ship_it_came_from()
    {
        var ship = new Ship("Destroyer", 2, [new Coordinate(0, 0), new Coordinate(0, 1)]);
        var snapshot = ShipSnapshot.Of(ship);

        ship.TryHit(new Coordinate(0, 0));

        var rebuilt = snapshot.ToShip();

        Assert.True(ship.HitCells.Count == 1);
        Assert.Empty(rebuilt.HitCells);
        Assert.Equal(ship.Cells, rebuilt.Cells);
    }

    [Fact]
    public void Two_ships_rebuilt_from_the_same_snapshot_are_independent()
    {
        var snapshot = new ShipSnapshot(
            "Destroyer", 2, [new Coordinate(0, 0), new Coordinate(0, 1)]);

        var first = snapshot.ToShip();
        var second = snapshot.ToShip();

        first.TryHit(new Coordinate(0, 0));

        Assert.Single(first.HitCells);
        Assert.Empty(second.HitCells);
    }
}
```

**Ces deux derniers tests sont le cœur de la tâche**, pas un supplément. `Ship` est mutable et `Board` ne copie que la liste qu'on lui donne (`Ships { get; } = [.. ships]`), jamais les objets. Un événement qui porterait des `Ship` partagerait donc ses instances avec le plateau : l'événement « déjà arrivé » se mettrait à jour rétroactivement, et `Fold` se contaminerait lui-même — replier un préfixe salirait le journal, donc le repli suivant partirait d'un état déjà touché.

Le détail qui rend ce piège redoutable : le test `Folding_every_prefix_never_throws` de la tâche 4 resterait **vert**, puisqu'il ne constate qu'une absence d'exception. Ces deux tests-ci sont les seuls à discriminer.

`ShipSnapshot` est une donnée pure qui **ne peut pas** porter d'état de touche : l'invariant tient dans le type et non dans la vigilance de qui écrit `Fold`. C'est le geste de l'ADR 0003 pour `ShotHistory`.

- [ ] **Étape 2 : exécuter pour vérifier que ça échoue**

Run: `dotnet test --filter "FullyQualifiedName~GameEventTests"`
Expected: **échec de compilation** — `The type or namespace name 'ShotFired' could not be found`.

- [ ] **Étape 3 : écrire l'implémentation minimale**

```csharp
namespace BattleShip.Models;

public abstract record GameEvent(int Sequence);

public sealed record ShipSnapshot(string Name, int Size, IReadOnlyList<Coordinate> Cells)
{
    public static ShipSnapshot Of(Ship ship) => new(ship.Name, ship.Size, [.. ship.Cells]);

    public Ship ToShip() => new(Name, Size, Cells);
}

public sealed record GameCreated(
    int Sequence,
    GameRules Rules,
    IReadOnlyList<ShipSnapshot> OpponentShips,
    string Difficulty) : GameEvent(Sequence);

public sealed record HumanFleetPlaced(
    int Sequence,
    IReadOnlyList<ShipSnapshot> Ships) : GameEvent(Sequence);

public sealed record ShotFired(
    int Sequence,
    Coordinate At,
    Player By,
    ShotResult Result,
    string? SunkShipName) : GameEvent(Sequence);

public sealed record GameEnded(
    int Sequence,
    Player Winner) : GameEvent(Sequence);
```

- [ ] **Étape 4 : exécuter pour vérifier que ça passe**

Run: `dotnet test --filter "FullyQualifiedName~GameEventTests"`
Expected: **PASS**, 3 tests.

Puis: `dotnet test` → **145 tests toujours au vert** (rien n'a été touché).

- [ ] **Étape 5 : commit**

```bash
dotnet format
git add BattleShip.Models/Events/GameEvent.cs BattleShip.Tests/Domain/GameEventTests.cs
git commit -m "feat: ajoute les quatre événements du domaine

ShotFired porte le résultat du tir, pas seulement son intention : c'est
ce qui permettra au repli de ne consulter aucun plateau pour savoir si
un coup a touché.

Co-Authored-By: Claude Opus 5 (1M context) <noreply@anthropic.com>"
```

---

## Tâche 3 : scinder `Board.Fire` en `Decide` et `Apply`

C'est le cœur du refactor. `Decide` est **pur** — il ne mute rien et peut refuser. `Apply` est **total** — il mute et ne peut jamais échouer.

**Files:**
- Modify: `BattleShip.Models/Board.cs`
- Test: `BattleShip.Tests/Domain/GameEventTests.cs` (ajouts)

**Interfaces:**
- Consumes: `GameEvent` et ses variantes (tâche 2)
- Produces:
  - `Result<ShotOutcome> Board.Decide(Coordinate at)` où `ShotOutcome` est `public readonly record struct ShotOutcome(ShotResult Result, string? SunkShipName)`
  - `void Board.Apply(Coordinate at)` — total, idempotent sur une case déjà tirée
  - `Board.Fire(Coordinate at, Player by)` **conservé**, réécrit comme `Decide` puis `Apply`

- [ ] **Étape 1 : écrire les tests qui échouent**

Ajouter à `GameEventTests.cs` :

```csharp
    private static Board BoardWithOneDestroyer() =>
        new(5, [new Ship("Destroyer", 2, [new Coordinate(0, 0), new Coordinate(0, 1)])]);

    [Fact]
    public void Decide_does_not_mutate_the_board()
    {
        var board = BoardWithOneDestroyer();

        var first = board.Decide(new Coordinate(0, 0));
        var second = board.Decide(new Coordinate(0, 0));

        Assert.True(first.IsOk);
        Assert.True(second.IsOk);
        Assert.Equal(ShotResult.Hit, second.Value.Result);
        Assert.Empty(board.ReceivedShots);
    }

    [Fact]
    public void Decide_refuses_a_cell_outside_the_grid()
    {
        var board = BoardWithOneDestroyer();

        var result = board.Decide(new Coordinate(9, 9));

        Assert.False(result.IsOk);
        Assert.Equal(GameError.OutOfBounds, result.Error);
    }

    [Fact]
    public void Decide_refuses_a_cell_already_shot()
    {
        var board = BoardWithOneDestroyer();
        board.Apply(new Coordinate(4, 4));

        var result = board.Decide(new Coordinate(4, 4));

        Assert.False(result.IsOk);
        Assert.Equal(GameError.CellAlreadyShot, result.Error);
    }

    [Fact]
    public void Decide_reports_a_sinking_before_it_happens()
    {
        var board = BoardWithOneDestroyer();
        board.Apply(new Coordinate(0, 0));

        var result = board.Decide(new Coordinate(0, 1));

        Assert.True(result.IsOk);
        Assert.Equal(ShotResult.Sunk, result.Value.Result);
        Assert.Equal("Destroyer", result.Value.SunkShipName);
    }

    [Fact]
    public void Apply_never_throws_even_when_replayed_twice()
    {
        var board = BoardWithOneDestroyer();

        board.Apply(new Coordinate(0, 0));
        board.Apply(new Coordinate(0, 0));

        Assert.Single(board.ReceivedShots);
        Assert.Single(board.Ships[0].HitCells);
    }
```

- [ ] **Étape 2 : exécuter pour vérifier que ça échoue**

Run: `dotnet test --filter "FullyQualifiedName~GameEventTests"`
Expected: **échec de compilation** — `'Board' does not contain a definition for 'Decide'`.

- [ ] **Étape 3 : écrire l'implémentation**

Remplacer intégralement `BattleShip.Models/Board.cs` :

```csharp
namespace BattleShip.Models;

public readonly record struct ShotOutcome(ShotResult Result, string? SunkShipName);

public sealed class Board(int gridSize, IReadOnlyList<Ship> ships)
{
    private readonly HashSet<Coordinate> _receivedShots = new();

    public int GridSize { get; } = gridSize;

    public IReadOnlyList<Ship> Ships { get; } = [.. ships];
    public IReadOnlySet<Coordinate> ReceivedShots => _receivedShots;

    public bool AllSunk => Ships.All(s => s.IsSunk);

    public Result<ShotOutcome> Decide(Coordinate at)
    {
        if (IsOutOfBounds(at))
            return Result<ShotOutcome>.Fail(GameError.OutOfBounds);

        if (_receivedShots.Contains(at))
            return Result<ShotOutcome>.Fail(GameError.CellAlreadyShot);

        var target = Ships.FirstOrDefault(s => s.Cells.Contains(at));
        if (target is null)
            return Result<ShotOutcome>.Ok(new ShotOutcome(ShotResult.Miss, null));

        var sinks = target.HitCells.Count + 1 == target.Size;
        return Result<ShotOutcome>.Ok(sinks
            ? new ShotOutcome(ShotResult.Sunk, target.Name)
            : new ShotOutcome(ShotResult.Hit, null));
    }

    public void Apply(Coordinate at)
    {
        if (IsOutOfBounds(at))
            throw new ArgumentOutOfRangeException(
                nameof(at), at, "An applied event can never land outside the grid.");

        if (!_receivedShots.Add(at))
            return;

        Ships.FirstOrDefault(s => s.Cells.Contains(at))?.TryHit(at);
    }

    public Result<ShotRecord> Fire(Coordinate at, Player by)
    {
        var decision = Decide(at);
        if (!decision.IsOk)
            return Result<ShotRecord>.Fail(decision.Error);

        Apply(at);

        return Result<ShotRecord>.Ok(
            new ShotRecord(at, decision.Value.Result, by, decision.Value.SunkShipName));
    }

    private bool IsOutOfBounds(Coordinate at) =>
        at.X < 0 || at.X >= GridSize || at.Y < 0 || at.Y >= GridSize;
}
```

**Le point délicat, à ne pas manquer** : l'ancien `Fire` s'appuyait sur `Ships.FirstOrDefault(s => s.TryHit(at))` — un `FirstOrDefault` à **effet de bord**. `Decide` doit être pur, d'où `s.Cells.Contains(at)` et le calcul `HitCells.Count + 1 == Size` pour anticiper le naufrage sans muter.

- [ ] **Étape 4 : exécuter pour vérifier que ça passe**

Run: `dotnet test --filter "FullyQualifiedName~GameEventTests"`
Expected: **PASS**, 8 tests.

Run: `dotnet test`
Expected: **150 tests au vert** (145 existants + 5 nouveaux). Si un test existant échoue, **s'arrêter** : `Fire` n'est plus équivalent à sa version précédente.

- [ ] **Étape 5 : commit**

```bash
dotnet format
git add BattleShip.Models/Board.cs BattleShip.Tests/Domain/GameEventTests.cs
git commit -m "refactor: scinde Board.Fire en Decide pur et Apply total

Decide valide sans muter et peut refuser (Result<T>) ; Apply mute et ne
peut jamais échouer. Fire est conservé à l'identique, réécrit comme la
composition des deux : la surface publique ne bouge pas, donc les tests
existants restent le filet.

L'ancien Fire s'appuyait sur un FirstOrDefault à effet de bord, ce qui
interdisait toute décision pure.

Co-Authored-By: Claude Opus 5 (1M context) <noreply@anthropic.com>"
```

---

## Tâche 4 : `Game` tient un journal, et `GameFold` le replie

**Files:**
- Modify: `BattleShip.Models/Game.cs`
- Create: `BattleShip.Models/Events/GameFold.cs`
- Test: `BattleShip.Tests/Domain/GameFoldTests.cs`

**Interfaces:**
- Consumes: `GameEvent` (tâche 2), `Board.Decide`/`Board.Apply` (tâche 3)
- Produces:
  - `IReadOnlyList<GameEvent> Game.Events`
  - `IReadOnlyList<ShotRecord> Game.History` — **conservée**, désormais dérivée des `ShotFired`
  - `static Game GameFold.Fold(Guid id, IReadOnlyList<GameEvent> events)`

- [ ] **Étape 1 : écrire les tests qui échouent**

```csharp
using BattleShip.Models;

namespace BattleShip.Tests.Domain;

public sealed class GameFoldTests
{
    private static Game PlayedGame(int seed, int shots)
    {
        var rules = GameRules.Default;
        var human = new FleetPlacer(new Random(seed)).PlaceAll(rules).Value;
        var opponent = new FleetPlacer(new Random(seed + 1)).PlaceAll(rules).Value;
        var game = Game.Start(Guid.NewGuid(), rules, human, opponent);

        var fired = 0;
        for (var x = 0; x < rules.GridSize && fired < shots; x++)
        {
            for (var y = 0; y < rules.GridSize && fired < shots; y++)
            {
                var shooter = game.CurrentPlayer;
                var target = shooter == Player.Human ? game.OpponentBoard : game.HumanBoard;
                if (target.ReceivedShots.Contains(new Coordinate(x, y)))
                    continue;

                var result = shooter == Player.Human
                    ? game.PlayerFires(new Coordinate(x, y))
                    : game.OpponentFires(new Coordinate(x, y));

                if (result.IsOk)
                    fired++;

                if (game.Status == GameStatus.Finished)
                    return game;
            }
        }

        return game;
    }

    [Fact]
    public void The_history_is_derived_from_the_journal()
    {
        var game = PlayedGame(seed: 21, shots: 12);

        Assert.Equal(
            game.Events.OfType<ShotFired>().Count(),
            game.History.Count);
    }

    [Fact]
    public void Folding_the_whole_journal_reproduces_the_played_state()
    {
        var game = PlayedGame(seed: 21, shots: 12);

        var replayed = GameFold.Fold(game.Id, game.Events);

        Assert.Equal(game.Status, replayed.Status);
        Assert.Equal(game.CurrentPlayer, replayed.CurrentPlayer);
        Assert.Equal(game.Winner, replayed.Winner);
        Assert.Equal(game.HumanBoard.ReceivedShots, replayed.HumanBoard.ReceivedShots);
        Assert.Equal(game.OpponentBoard.ReceivedShots, replayed.OpponentBoard.ReceivedShots);
        Assert.Equal(
            game.OpponentBoard.Ships.Select(s => s.IsSunk),
            replayed.OpponentBoard.Ships.Select(s => s.IsSunk));
    }

    [Fact]
    public void Folding_the_same_prefix_twice_gives_the_same_state()
    {
        var game = PlayedGame(seed: 33, shots: 40);
        var prefix = game.Events.Take(game.Events.Count / 2).ToList();

        var first = GameFold.Fold(game.Id, prefix);
        var second = GameFold.Fold(game.Id, prefix);

        Assert.Equal(first.Status, second.Status);
        Assert.Equal(first.CurrentPlayer, second.CurrentPlayer);
        Assert.Equal(first.HumanBoard.ReceivedShots, second.HumanBoard.ReceivedShots);
        Assert.Equal(first.OpponentBoard.ReceivedShots, second.OpponentBoard.ReceivedShots);
        Assert.Equal(
            first.OpponentBoard.Ships.Select(s => s.HitCells.Count),
            second.OpponentBoard.Ships.Select(s => s.HitCells.Count));
        Assert.Equal(
            first.HumanBoard.Ships.Select(s => s.IsSunk),
            second.HumanBoard.Ships.Select(s => s.IsSunk));
    }

    [Fact]
    public void Folding_every_prefix_of_a_valid_journal_never_throws()
    {
        var game = PlayedGame(seed: 33, shots: 40);

        for (var n = 0; n <= game.Events.Count; n++)
        {
            var prefix = game.Events.Take(n).ToList();
            var exception = Record.Exception(() => GameFold.Fold(game.Id, prefix));
            Assert.Null(exception);
        }
    }

    [Fact]
    public void A_ship_is_not_reported_sunk_before_the_shot_that_sank_it()
    {
        var game = PlayedGame(seed: 33, shots: 60);

        var sinking = game.Events.OfType<ShotFired>()
            .FirstOrDefault(e => e.Result == ShotResult.Sunk);

        Assert.NotNull(sinking);

        var justBefore = GameFold.Fold(game.Id, [.. game.Events.Take(sinking.Sequence)]);
        var justAfter = GameFold.Fold(game.Id, [.. game.Events.Take(sinking.Sequence + 1)]);

        var board = sinking.By == Player.Human ? justBefore.OpponentBoard : justBefore.HumanBoard;
        var after = sinking.By == Player.Human ? justAfter.OpponentBoard : justAfter.HumanBoard;

        Assert.DoesNotContain(board.Ships, s => s.Name == sinking.SunkShipName && s.IsSunk);
        Assert.Contains(after.Ships, s => s.Name == sinking.SunkShipName && s.IsSunk);
    }
}
```

`A_ship_is_not_reported_sunk_before_the_shot_that_sank_it` **est le test 2 de la spec** — celui qui doit échouer sur le code actuel. Voir l'étape 2.

`Folding_the_same_prefix_twice_gives_the_same_state` **est le test décisif du design**, placé ici et non en tâche 2 parce que le risque vit dans `Fold`, pas dans `ShipSnapshot`. Si les événements portaient des `Ship` vivants, le premier repli muterait les navires du journal et le second partirait d'un état déjà touché : les deux appels rendraient des `HitCells` différents. C'est le seul contrôle du plan qui échouerait sur cette conception. Les tests de la tâche 2, eux, n'instancient aucun événement et ne discriminent pas ce risque — relecture du 2026-09-17.

Noter le contraste avec `Folding_every_prefix_never_throws`, juste en dessous : celui-ci ne constate qu'une absence d'exception et **resterait vert** sur le design fautif. Les deux sont utiles, mais un seul prouve.

- [ ] **Étape 2 : exécuter pour vérifier que ça échoue, et consigner pourquoi**

Run: `dotnet test --filter "FullyQualifiedName~GameFoldTests"`
Expected: **échec de compilation** — `GameFold` n'existe pas, `Game.Events` n'existe pas.

**Important pour la revue IA** : noter ici que la démonstration du bug historique est *structurelle*, pas ce test. Le bug vit dans `Play.razor:OpponentHulls`, qui affiche `game.Opponent.SunkShips` — l'état **final** — quel que soit le curseur. Le confirmer avant la tâche 8 :

Run: `grep -n "OpponentHulls" -A6 BattleShip.App/Pages/Play.razor`
Expected: la méthode ignore `_replayCursor`. C'est le constat à consigner dans `REVUE-IA.md`.

- [ ] **Étape 3 : modifier `Game`**

Dans `BattleShip.Models/Game.cs`, remplacer le champ et la propriété d'historique :

```csharp
    private readonly List<GameEvent> _events = [];

    public IReadOnlyList<GameEvent> Events => _events;

    public IReadOnlyList<ShotRecord> History =>
        [.. _events.OfType<ShotFired>()
            .Select(e => new ShotRecord(e.At, e.Result, e.By, e.SunkShipName))];
```

Supprimer `private readonly List<ShotRecord> _history = [];` et l'ancienne propriété `History`.

Dans `Game.Create` et `Game.Start`, enregistrer les événements d'ouverture. Ajouter une méthode privée :

```csharp
    private void Record(Func<int, GameEvent> build) => _events.Add(build(_events.Count));
```

`Game.Create` — après la construction, avant le `return` :

```csharp
        var game = new Game(id, rules, humanBoard, opponentBoard, GameStatus.Placing, opponentDifficulty);
        game.Record(seq => new GameCreated(seq, rules, Snapshots(opponentShips), opponentDifficulty));
        return game;
```

`Game.Start` — même principe, en enregistrant aussi le placement humain :

```csharp
        var game = new Game(id, rules, humanBoard, opponentBoard, GameStatus.InProgress, opponentDifficulty);
        game.Record(seq => new GameCreated(seq, rules, Snapshots(opponentShips), opponentDifficulty));
        game.Record(seq => new HumanFleetPlaced(seq, Snapshots(humanShips)));
        return game;
```

`PlaceHumanFleet` — après `Status = GameStatus.InProgress;` :

```csharp
        Record(seq => new HumanFleetPlaced(seq, Snapshots(validation.Value)));
```

Et la fabrique d'instantanés, privée à `Game` :

```csharp
    private static IReadOnlyList<ShipSnapshot> Snapshots(IReadOnlyList<Ship> ships) =>
        [.. ships.Select(ShipSnapshot.Of)];
```

**Ne jamais passer un `Ship` vivant à un événement** : l'événement partagerait l'instance avec le plateau et se modifierait rétroactivement à chaque tir (voir § 2.1 bis de la spec).

`Fire` — remplacer le corps après les gardes d'état :

```csharp
        var decision = target.Decide(at);
        if (!decision.IsOk)
            return Result<ShotRecord>.Fail(decision.Error);

        target.Apply(at);
        Record(seq => new ShotFired(seq, at, shooter, decision.Value.Result, decision.Value.SunkShipName));

        if (decision.Value.Result == ShotResult.Miss || !Rules.ExtraTurnOnHit)
            CurrentPlayer = shooter == Player.Human ? Player.Opponent : Player.Human;

        if (target.AllSunk)
        {
            Status = GameStatus.Finished;
            Winner = shooter;
            Record(seq => new GameEnded(seq, shooter));
        }

        return Result<ShotRecord>.Ok(
            new ShotRecord(at, decision.Value.Result, shooter, decision.Value.SunkShipName));
```

- [ ] **Étape 4 : écrire `GameFold`**

```csharp
namespace BattleShip.Models;

public static class GameFold
{
    public static Game Fold(Guid id, IReadOnlyList<GameEvent> events)
    {
        if (events.Count == 0)
            return Game.Create(id, GameRules.Default, [], "Normal");

        if (events[0] is not GameCreated created)
            throw new InvalidOperationException("A journal must open with a GameCreated event.");

        var game = Game.Create(
            id,
            created.Rules,
            [.. created.OpponentShips.Select(s => s.ToShip())],
            created.Difficulty);

        foreach (var next in events.Skip(1))
            game.Replay(next);

        return game;
    }
}
```

Ajouter à `Game` la méthode de repli, **interne au domaine** et totale :

```csharp
    internal void Replay(GameEvent next)
    {
        _events.Add(next);

        switch (next)
        {
            case HumanFleetPlaced placed:
                HumanBoard = new Board(Rules.GridSize, [.. placed.Ships.Select(s => s.ToShip())]);
                Status = GameStatus.InProgress;
                break;

            case ShotFired shot:
                var target = shot.By == Player.Human ? OpponentBoard : HumanBoard;
                target.Apply(shot.At);
                if (shot.Result == ShotResult.Miss || !Rules.ExtraTurnOnHit)
                    CurrentPlayer = shot.By == Player.Human ? Player.Opponent : Player.Human;
                break;

            case GameEnded ended:
                Status = GameStatus.Finished;
                Winner = ended.Winner;
                break;

            case GameCreated:
                throw new InvalidOperationException("GameCreated can only open a journal.");

            default:
                throw new ArgumentOutOfRangeException(
                    nameof(next), next, "Unknown event type.");
        }
    }
```

`Replay` est `internal` et **rien à ajouter au `.csproj`** : `GameFold` vit dans le même assembly que `Game`, donc il y accède directement, et aucun test de ce plan n'appelle `Replay` en direct.

**Attention au piège — c'est le point le plus subtil de la tâche** : `Replay` ajoute l'événement tel quel à `_events` (`_events.Add(next)`), mais n'appelle **jamais** `Record`. `Record` recalcule un rang depuis `_events.Count` et créerait un second événement ; `Add` préserve le rang d'origine.

Sans ce `Add`, le `Game` rendu par `Fold` n'aurait qu'un seul événement — son propre `GameCreated` — donc `Fold(events).Events != events` et `Fold(events).History` serait **vide**. Aucun test de ce plan ne l'attraperait, et la tâche 8 fonctionnerait par accident.

- [ ] **Étape 5 : exécuter pour vérifier que ça passe**

Run: `dotnet test --filter "FullyQualifiedName~GameFoldTests"`
Expected: **PASS**, 4 tests.

Run: `dotnet test`
Expected: **154 tests au vert**. Si `HttpEndpointsTests` ou `FireGrpcTests` échouent, la cause probable est l'ordre des rangs dans `Fire` — vérifier que `ShotFired` est enregistré **avant** `GameEnded`.

- [ ] **Étape 6 : commit**

```bash
dotnet format
git add BattleShip.Models/Game.cs BattleShip.Models/Events/GameFold.cs \
        BattleShip.Tests/Domain/GameFoldTests.cs
git commit -m "feat: le journal devient la source de vérité de Game

Game._history (List<ShotRecord>) est remplacé par Game._events. History
est conservée mais devient dérivée des ShotFired, donc la surface
publique ne bouge pas.

GameFold.Fold reconstruit un Game depuis un journal : le rejeu à
l'étape n devient Fold(events.Take(n)), et l'état « coulé »
intermédiaire est correct par construction.

Co-Authored-By: Claude Opus 5 (1M context) <noreply@anthropic.com>"
```

---

## Tâche 5 : le store expose le journal

**Files:**
- Modify: `BattleShip.Models/IGameStore.cs`
- Modify: `BattleShip.API/Stores/InMemoryGameStore.cs`
- Test: `BattleShip.Tests/Domain/InMemoryGameStoreTests.cs` (ajouts en fin de classe)

**Interfaces:**
- Consumes: `GameEvent` (tâche 2), `Game.Events` (tâche 4)
- Produces: `Result<IReadOnlyList<GameEvent>> IGameStore.ReadEvents(Guid id, int fromSequence)` — `fromSequence` **inclusif**

- [ ] **Étape 1 : écrire les tests qui échouent**

Ajouter à `InMemoryGameStoreTests` :

```csharp
    [Fact]
    public void ReadEvents_on_a_missing_game_returns_GameNotFound()
    {
        var store = new InMemoryGameStore();

        var result = store.ReadEvents(Guid.NewGuid(), 0);

        Assert.False(result.IsOk);
        Assert.Equal(GameError.GameNotFound, result.Error);
    }

    [Fact]
    public void ReadEvents_from_zero_returns_the_whole_journal()
    {
        var store = new InMemoryGameStore();
        var game = GameOnDefaultGrid();
        store.Save(game);
        store.Mutate(game.Id, g => g.PlayerFires(new Coordinate(0, 0)));

        var result = store.ReadEvents(game.Id, 0);

        Assert.True(result.IsOk);
        Assert.Equal(game.Events.Count, result.Value.Count);
        Assert.Equal(0, result.Value[0].Sequence);
    }

    [Fact]
    public void ReadEvents_from_n_is_inclusive_and_returns_exactly_the_tail()
    {
        var store = new InMemoryGameStore();
        var game = GameOnDefaultGrid();
        store.Save(game);
        store.Mutate(game.Id, g => g.PlayerFires(new Coordinate(0, 0)));
        store.Mutate(game.Id, g => g.PlayerFires(new Coordinate(1, 1)));

        var all = store.ReadEvents(game.Id, 0).Value;
        var tail = store.ReadEvents(game.Id, 2).Value;

        Assert.Equal(all.Count - 2, tail.Count);
        Assert.Equal(2, tail[0].Sequence);
    }

    [Fact]
    public void ReadEvents_past_the_end_returns_an_empty_list_and_not_an_error()
    {
        var store = new InMemoryGameStore();
        var game = GameOnDefaultGrid();
        store.Save(game);

        var result = store.ReadEvents(game.Id, 9_999);

        Assert.True(result.IsOk);
        Assert.Empty(result.Value);
    }
```

- [ ] **Étape 2 : exécuter pour vérifier que ça échoue**

Run: `dotnet test --filter "FullyQualifiedName~InMemoryGameStoreTests"`
Expected: **échec de compilation** — `'InMemoryGameStore' does not contain a definition for 'ReadEvents'`.

- [ ] **Étape 3 : écrire l'implémentation**

Dans `IGameStore.cs`, ajouter au contrat :

```csharp
    Result<IReadOnlyList<GameEvent>> ReadEvents(Guid id, int fromSequence);
```

Dans `InMemoryGameStore.cs`, sous le **même verrou** que `Read` :

```csharp
    public Result<IReadOnlyList<GameEvent>> ReadEvents(Guid id, int fromSequence)
    {
        var gameLock = _locks.GetOrAdd(id, static _ => new object());
        lock (gameLock)
        {
            if (!_games.TryGetValue(id, out var game))
                return Result<IReadOnlyList<GameEvent>>.Fail(GameError.GameNotFound);

            IReadOnlyList<GameEvent> tail =
                [.. game.Events.Where(e => e.Sequence >= fromSequence)];

            return Result<IReadOnlyList<GameEvent>>.Ok(tail);
        }
    }
```

Le store sert le journal **complet et non censuré** : la censure appartient à la façade HTTP (tâche 6), pas au store.

- [ ] **Étape 4 : exécuter pour vérifier que ça passe**

Run: `dotnet test --filter "FullyQualifiedName~InMemoryGameStoreTests"`
Expected: **PASS**.

Run: `dotnet test`
Expected: **158 tests au vert**.

- [ ] **Étape 5 : commit**

```bash
dotnet format
git add BattleShip.Models/IGameStore.cs BattleShip.API/Stores/InMemoryGameStore.cs \
        BattleShip.Tests/Domain/InMemoryGameStoreTests.cs
git commit -m "feat: IGameStore expose le journal en lecture incrémentale

Un seul membre ajouté, de lecture. Le verrou par partie de l'ADR 0002
est conservé : la concurrence optimiste a été examinée et écartée
(ADR 0011). fromSequence est inclusif.

Co-Authored-By: Claude Opus 5 (1M context) <noreply@anthropic.com>"
```

---

## Tâche 6 : l'endpoint `/events` et la censure du secret

La tâche la plus risquée du plan : c'est ici qu'une fuite du secret est possible.

**Files:**
- Create: `BattleShip.Models/Contracts/GameEventDto.cs`
- Create: `BattleShip.API/Contracts/EventProjection.cs`
- Create: `BattleShip.API/Validation/EventQueryValidator.cs`
- Modify: `BattleShip.API/Endpoints/GameEndpoints.cs`
- Modify: `BattleShip.API/Program.cs`
- Test: `BattleShip.Tests/Api/EventsEndpointTests.cs`

**Interfaces:**
- Consumes: `IGameStore.ReadEvents` (tâche 5), la forme décidée en tâche 1
- Produces: `GET /games/{id}/events?from={n}` → `200` + `IReadOnlyList<GameEventDto>`, ou `404`

- [ ] **Étape 1 : écrire les tests qui échouent**

**Deux contraintes du dépôt, vérifiées avant d'écrire ce plan, qui dictent la forme de ces tests :**

1. Il n'existe **pas** de route HTTP de tir. Le tir est **exclusivement en gRPC-Web** (ADR 0005). Les seules routes HTTP sont `/games`, `/games/{id}`, `/games/{id}/placement`, `/games/{id}/history`.
2. Un test piloté par HTTP **ne peut pas connaître la flotte adverse** — c'est précisément le secret qu'on vérifie. Il ne pourrait donc pas affirmer qu'une position n'a pas fuité.

D'où la forme retenue : **injecter un store pré-rempli**, ce qui est la justification littérale de l'ADR 0002 (« les tests d'intégration doivent pouvoir injecter un store pré-rempli »). Le test connaît alors la flotte et devient réellement discriminant.

```csharp
using System.Net;
using System.Net.Http.Json;
using BattleShip.API.Stores;
using BattleShip.Models;
using BattleShip.Models.Contracts;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

namespace BattleShip.Tests.Api;

public sealed class EventsEndpointTests
{
    private static (HttpClient Client, IGameStore Store) Seeded()
    {
        var store = new InMemoryGameStore();

        var factory = new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<IGameStore>();
                services.AddSingleton<IGameStore>(store);
            }));

        return (factory.CreateClient(), store);
    }

    private static Game PlayableGame()
    {
        var rules = GameRules.Default;
        var human = new FleetPlacer(new Random(41)).PlaceAll(rules).Value;
        var opponent = new FleetPlacer(new Random(42)).PlaceAll(rules).Value;
        return Game.Start(Guid.NewGuid(), rules, human, opponent);
    }

    [Fact]
    public async Task An_unknown_game_returns_404()
    {
        var (client, _) = Seeded();

        var response = await client.GetAsync($"/games/{Guid.NewGuid()}/events");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task A_negative_from_is_rejected()
    {
        var (client, store) = Seeded();
        var game = PlayableGame();
        store.Save(game);

        var response = await client.GetAsync($"/games/{game.Id}/events?from=-1");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task During_a_game_no_unsunk_opposing_ship_cell_crosses_the_wire()
    {
        var (client, store) = Seeded();
        var game = PlayableGame();
        game.PlayerFires(new Coordinate(0, 0));
        store.Save(game);

        Assert.NotEqual(GameStatus.Finished, game.Status);

        var events = await (await client.GetAsync($"/games/{game.Id}/events"))
            .Content.ReadFromJsonAsync<List<GameEventDto>>();

        Assert.NotNull(events);

        var created = events!.OfType<GameCreatedDto>().Single();
        Assert.Empty(created.OpponentShips);

        var exposedCells = events
            .SelectMany(e => e switch
            {
                GameCreatedDto c => c.OpponentShips.SelectMany(s => s.Cells),
                HumanFleetPlacedDto p => p.Ships.SelectMany(s => s.Cells),
                _ => []
            })
            .Select(c => new Coordinate(c.X, c.Y))
            .ToHashSet();

        var humanCells = game.HumanBoard.Ships.SelectMany(s => s.Cells).ToHashSet();

        Assert.Equal(humanCells, exposedCells);
    }

    [Fact]
    public async Task After_the_game_ends_the_full_journal_is_served()
    {
        var (client, store) = Seeded();
        var game = PlayableGame();

        foreach (var cell in game.OpponentBoard.Ships.SelectMany(s => s.Cells).ToList())
        {
            game.PlayerFires(cell);
            if (game.Status == GameStatus.Finished)
                break;
        }

        Assert.Equal(GameStatus.Finished, game.Status);
        store.Save(game);

        var events = await (await client.GetAsync($"/games/{game.Id}/events"))
            .Content.ReadFromJsonAsync<List<GameEventDto>>();

        Assert.NotNull(events);

        var created = events!.OfType<GameCreatedDto>().Single();

        Assert.Equal(game.OpponentBoard.Ships.Count, created.OpponentShips.Count);
    }
}
```

**Les deux derniers tests forment la paire délibérée du § 6.2 de la spec** : chacun seul est satisfait par une implémentation fausse. Le troisième passerait avec un endpoint qui ne renvoie rien ; le quatrième passerait avec un endpoint qui ne censure jamais. Ne jamais n'en garder qu'un.

`RemoveAll<T>` vient de `Microsoft.Extensions.DependencyInjection.Extensions`. Si le `using` manque, l'ajouter.

Le troisième test s'appuie sur « `touche = on rejoue` » (`GameRules.Default.ExtraTurnOnHit`) : après un tir en (0,0), c'est encore au joueur si ça a touché, et à l'adversaire sinon. Dans les deux cas la partie n'est pas finie, ce que l'assertion vérifie explicitement plutôt que de le supposer.

**Pourquoi le test n'inspecte pas la charge utile brute.** La tentation serait de chercher `"x":3,"y":4` dans le JSON. **Ce contrôle serait faux** : les deux flottes occupent le *même* espace de coordonnées 0–9, donc une case du navire du joueur — légitimement transmise par `HumanFleetPlaced` — coïncide très souvent avec une case secrète adverse. Le test signalerait des fuites inexistantes, et la première « correction » serait d'affaiblir l'assertion.

L'assertion retenue est structurelle et exacte : **l'ensemble des cases de navires exposées par le flux est exactement l'ensemble des cases du joueur.** Ni plus (aucune fuite), ni moins (le joueur voit bien sa propre flotte). Elle échoue si `OpponentShips` n'est pas vidé, et elle échoue aussi si la censure devient si large qu'elle emporte la flotte du joueur.

- [ ] **Étape 2 : exécuter pour vérifier que ça échoue**

Run: `dotnet test --filter "FullyQualifiedName~EventsEndpointTests"`
Expected: **échec de compilation** — `GameEventDto` n'existe pas.

- [ ] **Étape 3 : écrire le contrat de transport**

Dans `BattleShip.Models/Contracts/GameEventDto.cs`, **la forme décidée à la tâche 1**. Version polymorphe si le spike l'a validée :

```csharp
using System.Text.Json.Serialization;

namespace BattleShip.Models.Contracts;

[JsonPolymorphic(TypeDiscriminatorPropertyName = "type")]
[JsonDerivedType(typeof(GameCreatedDto), "gameCreated")]
[JsonDerivedType(typeof(HumanFleetPlacedDto), "humanFleetPlaced")]
[JsonDerivedType(typeof(ShotFiredDto), "shotFired")]
[JsonDerivedType(typeof(GameEndedDto), "gameEnded")]
public abstract record GameEventDto(int Sequence);

public sealed record GameCreatedDto(
    int Sequence, int GridSize, IReadOnlyList<ShipTemplateDto> Fleet,
    bool ShipsMayTouch, string Difficulty,
    IReadOnlyList<ShipDto> OpponentShips) : GameEventDto(Sequence);

public sealed record HumanFleetPlacedDto(
    int Sequence, IReadOnlyList<ShipDto> Ships) : GameEventDto(Sequence);

public sealed record ShotFiredDto(
    int Sequence, int X, int Y, string By,
    string Result, string? SunkShipName) : GameEventDto(Sequence);

public sealed record GameEndedDto(int Sequence, string Winner) : GameEventDto(Sequence);
```

- [ ] **Étape 4 : écrire la projection censurée**

`BattleShip.API/Contracts/EventProjection.cs` — **le seul endroit du dépôt où la censure est décidée** :

```csharp
using BattleShip.Models;
using BattleShip.Models.Contracts;

namespace BattleShip.API.Contracts;

public static class EventProjection
{
    public static IReadOnlyList<GameEventDto> ForPlayer(
        IReadOnlyList<GameEvent> events, bool gameIsOver) =>
        [.. events.Select(e => ToDto(e, gameIsOver))];

    private static GameEventDto ToDto(GameEvent source, bool gameIsOver) => source switch
    {
        GameCreated created => new GameCreatedDto(
            created.Sequence,
            created.Rules.GridSize,
            [.. created.Rules.Fleet.Select(t => new ShipTemplateDto(t.Name, t.Size))],
            created.Rules.ShipsMayTouch,
            created.Difficulty,
            gameIsOver ? ToShipDtos(created.OpponentShips) : []),

        HumanFleetPlaced placed => new HumanFleetPlacedDto(
            placed.Sequence, ToShipDtos(placed.Ships)),

        ShotFired shot => new ShotFiredDto(
            shot.Sequence, shot.At.X, shot.At.Y, shot.By.ToString(),
            DtoMappings.ToState(shot.Result), shot.SunkShipName),

        GameEnded ended => new GameEndedDto(ended.Sequence, ended.Winner.ToString()),

        _ => throw new ArgumentOutOfRangeException(
            nameof(source), source, "Unknown event type.")
    };

    private static IReadOnlyList<ShipDto> ToShipDtos(IReadOnlyList<ShipSnapshot> ships) =>
        [.. ships.Select(s => new ShipDto(
            s.Name, s.Size,
            [.. s.Cells.Select(c => new CellDto(c.X, c.Y, "ship"))],
            IsSunk: false))];
}
```

`IsSunk: false` n'est pas un raccourci : un instantané de placement décrit la flotte **au moment où elle est posée**, où rien n'est coulé. L'état « coulé » à un instant donné se lit sur le repli des `ShotFired`, pas sur l'instantané.

`DtoMappings.ToState` est déjà `internal` et vit dans le même assembly — aucune modification nécessaire.

- [ ] **Étape 5 : écrire le validateur**

`BattleShip.API/Validation/EventQueryValidator.cs` :

```csharp
using BattleShip.Models.Contracts;
using FluentValidation;

namespace BattleShip.API.Validation;

public sealed class EventQueryValidator : AbstractValidator<EventQuery>
{
    public EventQueryValidator()
    {
        RuleFor(q => q.From)
            .GreaterThanOrEqualTo(0)
            .WithMessage("The 'from' sequence must be zero or greater.");
    }
}
```

Ajouter dans `BattleShip.Models/Contracts/Inputs.cs` :

```csharp
public sealed record EventQuery(int From);
```

- [ ] **Étape 6 : brancher l'endpoint**

Dans `GameEndpoints.MapGameEndpoints`, après la route `/history` :

```csharp
        app.MapGet("/games/{id:guid}/events", async Task<IResult> (
            Guid id, int? from, IValidator<EventQuery> validator, IGameStore store) =>
        {
            var query = new EventQuery(from ?? 0);
            var check = await validator.ValidateAsync(query);
            if (!check.IsValid)
                return TypedResults.ValidationProblem(check.ToDictionary());

            var status = store.Read(id, game => game.Status);
            if (!status.IsOk)
                return TypedResults.NotFound();

            var events = store.ReadEvents(id, query.From);
            if (!events.IsOk)
                return TypedResults.NotFound();

            return TypedResults.Ok(
                EventProjection.ForPlayer(events.Value, status.Value == GameStatus.Finished));
        });
```

**L'ordre des deux lectures compte.** `Read` et `ReadEvents` prennent le verrou séparément, donc une partie peut se terminer entre les deux. Lire `Status` **en premier** est l'ordre conservateur : dans la fenêtre de course, on censure une partie tout juste finie au lieu de révéler la flotte d'une partie encore en cours. L'inverse serait une fuite du secret.

Dans `BattleShip.API/Program.cs`, à côté des validateurs existants :

```csharp
builder.Services.AddScoped<IValidator<EventQuery>, EventQueryValidator>();
```

Run: `grep -n "AddScoped<IValidator" BattleShip.API/Program.cs` pour placer la ligne au bon endroit et suivre la forme existante.

- [ ] **Étape 7 : exécuter pour vérifier que ça passe**

Run: `dotnet test --filter "FullyQualifiedName~EventsEndpointTests"`
Expected: **PASS**, 4 tests.

Run: `dotnet test`
Expected: **162 tests au vert**.

- [ ] **Étape 8 : vérifier la censure à la main, dans le navigateur**

Le test 3 vérifie l'absence de coordonnées dans la charge utile. Confirmer aussi de visu, comme le § 6 du `CLAUDE.md` le demande :

```bash
dotnet run --project BattleShip.API &
# créer une partie, placer la flotte, tirer un coup via api.http
curl -s https://localhost:<port>/games/<id>/events | jq '.[] | select(.type=="gameCreated")'
```

Expected: `"opponentShips": []` en cours de partie.

Ajouter les requêtes correspondantes à `api.http`, versionné comme l'exige le `CLAUDE.md` § 3.

- [ ] **Étape 9 : commit**

```bash
dotnet format
git add BattleShip.Models/Contracts/GameEventDto.cs BattleShip.Models/Contracts/Inputs.cs \
        BattleShip.API/Contracts/EventProjection.cs BattleShip.API/Validation/EventQueryValidator.cs \
        BattleShip.API/Endpoints/GameEndpoints.cs BattleShip.API/Program.cs \
        BattleShip.Tests/Api/EventsEndpointTests.cs api.http
git commit -m "feat: sert le journal censuré sur GET /games/{id}/events

La censure est par champ et non par événement : GameCreated est servi
avec OpponentShips vidé pendant la partie, et complet après GameEnded.
Retenir l'événement entier priverait le repli côté front de la taille
de grille et des règles.

Les deux tests de censure forment une paire : chacun seul est satisfait
par une implémentation fausse — l'un passerait avec un endpoint muet,
l'autre avec un endpoint qui ne censure jamais.

Co-Authored-By: Claude Opus 5 (1M context) <noreply@anthropic.com>"
```

---

## Tâche 7 : la reprise après rechargement de page

Indépendante du journal, mais elle tue une limite connue et n'a plus de raison d'attendre.

**Files:**
- Create: `BattleShip.App/Services/GameSessionStorage.cs`
- Modify: `BattleShip.App/Services/GameState.cs`
- Modify: `BattleShip.App/Program.cs`

**Interfaces:**
- Consumes: `BattleApiClient.GetGameAsync` (existant)
- Produces: `GameSessionStorage.SaveAsync(Guid)`, `GameSessionStorage.ReadAsync()`, `GameSessionStorage.ClearAsync()`

- [ ] **Étape 1 : lire l'état actuel**

Run: `cat BattleShip.App/Services/GameState.cs`
Run: `grep -rn "localStorage\|IJSRuntime" BattleShip.App/`

Repérer comment `AppearanceState` persiste déjà ses réglages — le `CLAUDE.md` demande de suivre les motifs existants. **Réutiliser la même approche d'interop**, ne pas en introduire une seconde.

- [ ] **Étape 2 : écrire le service**

```csharp
using Microsoft.JSInterop;

namespace BattleShip.App.Services;

public sealed class GameSessionStorage(IJSRuntime js)
{
    private const string Key = "battleship.currentGame";

    public async Task SaveAsync(Guid id)
    {
        try
        {
            await js.InvokeVoidAsync("localStorage.setItem", Key, id.ToString());
        }
        catch (JSException)
        {
        }
    }

    public async Task<Guid?> ReadAsync()
    {
        try
        {
            var stored = await js.InvokeAsync<string?>("localStorage.getItem", Key);
            return Guid.TryParse(stored, out var id) ? id : null;
        }
        catch (JSException)
        {
            return null;
        }
    }

    public async Task ClearAsync()
    {
        try
        {
            await js.InvokeVoidAsync("localStorage.removeItem", Key);
        }
        catch (JSException)
        {
        }
    }
}
```

Le `try`/`catch` sur `JSException` couvre le mode navigation privée et le stockage bloqué : l'absence de stockage ne doit jamais rendre l'interface inutilisable (`CLAUDE.md` § Blazor, les trois états).

- [ ] **Étape 3 : brancher dans `GameState`**

Appeler `SaveAsync` quand une partie devient courante, `ClearAsync` quand elle se termine ou disparaît. Au démarrage, `ReadAsync` puis `GetGameAsync` ; sur échec, `ClearAsync` et retour à l'accueil sans message d'erreur bloquant — une partie expirée est un cas normal, pas une panne.

Enregistrer dans `BattleShip.App/Program.cs`, à côté de `AppearanceState` :

```csharp
builder.Services.AddScoped<GameSessionStorage>();
```

- [ ] **Étape 4 : vérifier dans le navigateur**

Le `CLAUDE.md` § 6 exige un énoncé **avant** exécution.

> Attendu : créer une partie, tirer deux coups, `F5` → la partie revient avec ses deux coups. Puis redémarrer l'API et `F5` → retour à l'accueil sans page blanche ni erreur bloquante.
> Ce contrôle détecte : un identifiant non mémorisé, et un `404` traité comme une panne au lieu d'une partie expirée.

```bash
dotnet run --project BattleShip.API &
dotnet watch --project BattleShip.App
```

Console F12 + onglet Réseau, comme le § Protocole de blocage le demande.

- [ ] **Étape 5 : commit**

```bash
dotnet format
git add BattleShip.App/Services/GameSessionStorage.cs BattleShip.App/Services/GameState.cs \
        BattleShip.App/Program.cs
git commit -m "feat: la partie en cours survit à un rechargement de page

L'identifiant est mémorisé en localStorage et relu au démarrage. Une
partie disparue du serveur ramène à l'accueil sans message bloquant :
c'est un cas normal, pas une panne. L'échec du stockage (navigation
privée) est absorbé.

Co-Authored-By: Claude Opus 5 (1M context) <noreply@anthropic.com>"
```

---

## Tâche 8 : le rejeu passe par le repli partagé

Le bug documenté disparaît ici.

**Files:**
- Modify: `BattleShip.App/Services/BattleApiClient.cs`
- Modify: `BattleShip.App/Pages/Play.razor`

**Interfaces:**
- Consumes: `GET /games/{id}/events` (tâche 6), `GameFold.Fold` (tâche 4)
- Produces: rien pour les tâches suivantes

- [ ] **Étape 1 : constater le bug AVANT de le corriger**

Le `CLAUDE.md` § 6 impose de vérifier qu'un contrôle échoue avant de réussir.

> Attendu : jouer jusqu'à couler un navire adverse, puis ramener le curseur de rejeu **avant** le coup fatal. Le navire doit apparaître **intact** ; il apparaîtra **coulé**, parce que `OpponentHulls` lit `game.Opponent.SunkShips`, c'est-à-dire l'état final.

Reproduire dans le navigateur et **capturer la preuve** pour `REVUE-IA.md`.

- [ ] **Étape 2 : ajouter la lecture des événements au client**

Dans `BattleApiClient`, sur le modèle exact de `GetHistoryAsync` — mêmes `try`/`catch`, mêmes messages :

```csharp
    public async Task<ApiResult<IReadOnlyList<GameEventDto>>> GetEventsAsync(
    Guid id, int from = 0, CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await http.GetAsync($"games/{id}/events?from={from}", cancellationToken);

            if (response.StatusCode == HttpStatusCode.NotFound)
                return ApiResult<IReadOnlyList<GameEventDto>>.Fail(
                    $"Game {id} no longer exists on the server.");

            if (!response.IsSuccessStatusCode)
                return ApiResult<IReadOnlyList<GameEventDto>>.Fail(
                    await DescribeFailureAsync(response, cancellationToken));

            var events = await response.Content
                .ReadFromJsonAsync<List<GameEventDto>>(cancellationToken);

            return ApiResult<IReadOnlyList<GameEventDto>>.Ok(events ?? []);
        }
        catch (Exception exception) when (IsCommunicationFailure(exception))
        {
            return ApiResult<IReadOnlyList<GameEventDto>>.Fail(Describe(exception));
        }
    }
```

- [ ] **Étape 3 : replier côté front**

Dans `Play.razor`, remplacer le chargement de l'historique et les trois méthodes de rejeu.

Les DTO d'événements doivent redevenir des `GameEvent` du domaine pour être repliés. Ajouter une conversion **privée** dans `Play.razor` — elle ne dépend que de `BattleShip.Models`, seule référence du projet App :

```csharp
    private IReadOnlyList<GameEvent> _events = [];

    private Game? ReplayedGame(int cursor)
    {
        if (_events.Count == 0)
            return null;

        var shots = 0;
        var upTo = new List<GameEvent>();

        foreach (var next in _events)
        {
            if (next is ShotFired && shots++ >= cursor)
                break;

            upTo.Add(next);
        }

        return GameFold.Fold(State.Current!.Id, upTo);
    }
```

`OpponentHulls` — **c'est la correction du bug** — prend désormais le curseur en compte :

```csharp
    private IReadOnlyList<ShipOutline> OpponentHulls(GameDto game)
    {
        if (_replayCursor is { } cursor && ReplayedGame(cursor) is { } replayed)
        {
            return
            [
                .. replayed.OpponentBoard.Ships
                    .Where(s => s.IsSunk)
                    .Select(s => ShipOutline.FromCells(
                        s.Name,
                        [.. s.Cells.Select(c => new CellDto(c.X, c.Y, "sunk"))],
                        isSunk: true))
                    .OfType<ShipOutline>()
            ];
        }

        return
        [
            .. game.Opponent.SunkShips
                .Select(ship => ShipOutline.FromCells(ship.Name, ship.Cells, isSunk: true))
                .OfType<ShipOutline>()
        ];
    }
```

`OpponentHulls` cesse d'être `static` — adapter son appel dans le balisage.

`LoadHistoryAsync` devient :

```csharp
    private async Task LoadHistoryAsync()
    {
        if (State.Current is not { } game)
            return;

        var result = await Api.GetEventsAsync(game.Id);
        if (!result.IsOk)
            return;

        _events = [.. result.Value.Select(ToDomainEvent).OfType<GameEvent>()];
        _history = [.. _events.OfType<ShotFired>()
            .Select(e => new ShotDto(e.At.X, e.At.Y, StateOf(e.Result), e.By.ToString(), e.SunkShipName))];
    }

    private static GameEvent? ToDomainEvent(GameEventDto dto) => dto switch
    {
        GameCreatedDto created => new GameCreated(
            created.Sequence,
            GameRules.Default with
            {
                GridSize = created.GridSize,
                Fleet = [.. created.Fleet.Select(f => new ShipTemplate(f.Name, f.Size))],
                ShipsMayTouch = created.ShipsMayTouch
            },
            ToShips(created.OpponentShips),
            created.Difficulty),

        HumanFleetPlacedDto placed => new HumanFleetPlaced(placed.Sequence, ToShips(placed.Ships)),

        ShotFiredDto shot => new ShotFired(
            shot.Sequence, new Coordinate(shot.X, shot.Y),
            Enum.Parse<Player>(shot.By), ResultOf(shot.Result), shot.SunkShipName),

        GameEndedDto ended => new GameEnded(ended.Sequence, Enum.Parse<Player>(ended.Winner)),

        _ => null
    };

    private static IReadOnlyList<ShipSnapshot> ToShips(IReadOnlyList<ShipDto> ships) =>
        [.. ships.Select(s => new ShipSnapshot(
            s.Name, s.Size, [.. s.Cells.Select(c => new Coordinate(c.X, c.Y))]))];

    private static ShotResult ResultOf(string state) => state switch
    {
        "miss" => ShotResult.Miss,
        "hit" => ShotResult.Hit,
        "sunk" => ShotResult.Sunk,
        _ => throw new ArgumentOutOfRangeException(nameof(state), state, "Unknown shot state.")
    };

    private static string StateOf(ShotResult result) => result switch
    {
        ShotResult.Miss => "miss",
        ShotResult.Hit => "hit",
        ShotResult.Sunk => "sunk",
        _ => throw new ArgumentOutOfRangeException(nameof(result), result, "Unknown shot result.")
    };
```

**Piège attendu** : pendant la partie, `GameCreatedDto.OpponentShips` est **vide** (censure). `GameFold.Fold` construit alors un `OpponentBoard` sans navire, donc `replayed.OpponentBoard.Ships` est vide et `OpponentHulls` ne rend aucune coque en rejeu. **C'est le comportement correct** : pendant la partie, le joueur ne voit les coques adverses que par `game.Opponent.SunkShips`, et la branche de rejeu doit alors retomber sur les coulés connus. Traiter ce cas explicitement plutôt que de rendre une liste vide par accident — c'est le point à relire le plus attentivement de toute la tâche.

- [ ] **Étape 4 : vérifier la correction dans le navigateur**

> Attendu : le scénario de l'étape 1 rejoué montre maintenant le navire **intact** avant le coup fatal et **coulé** après. Et en fin de partie, la flotte adverse complète apparaît.
> Ce contrôle détecte : un curseur qui n'atteint pas le repli, et une censure qui n'aurait pas basculé à `GameEnded`.

```bash
dotnet run --project BattleShip.API &
dotnet watch --project BattleShip.App
```

- [ ] **Étape 5 : commit**

```bash
dotnet format
git add BattleShip.App/Services/BattleApiClient.cs BattleShip.App/Pages/Play.razor
git commit -m "fix: le rejeu reconstitue l'état coulé intermédiaire

Le front replie le journal avec GameFold, la même fonction que le
serveur — BattleShip.App référence Models, donc aucune seconde machine
à états à maintenir.

OpponentHulls lisait game.Opponent.SunkShips, c'est-à-dire l'état
final, quel que soit le curseur : un navire coulé au coup 50
apparaissait coulé au coup 10. Corrigé, et la limite connue tombe.

Co-Authored-By: Claude Opus 5 (1M context) <noreply@anthropic.com>"
```

---

## Tâche 9 : les livrables

Le `CLAUDE.md` est explicite : « un commit de code sans trace de décision ni de vérification est un travail incomplet ». Cette tâche n'est pas optionnelle.

**Files:**
- Modify: `PROMPTS.md`, `REVUE-IA.md`, `README.md`, `docs/conformite.md`

**Interfaces:**
- Consumes: les preuves accumulées aux tâches 1 à 8
- Produces: rien de codé

- [ ] **Étape 1 : la quatrième revue dans `REVUE-IA.md`**

Sujet : **la censure par champ**. Suivre le gabarit du § 5.3 du `CLAUDE.md`, en renseignant :

- *Proposition et référence* : `BattleShip.API/Contracts/EventProjection.cs`, commit de la tâche 6.
- *Hypothèse* : aucune position d'un navire adverse non coulé ne franchit `/events` pendant la partie ; toutes la franchissent après `GameEnded`.
- *Résultat attendu avant exécution* : à énoncer **avant** de lancer les tests.
- *Pouvoir discriminant* : chacun des deux tests, pris seul, est satisfait par une implémentation fausse — c'est la paire qui prouve.
- *Avant / après* : le rejeu montrait un navire coulé trop tôt (constat de la tâche 8, étape 1) ; il ne le montre plus (étape 4).
- *Limites* : la censure est vérifiée sur le canal HTTP `/events` ; le canal gRPC n'expose pas d'événements et n'est pas couvert par ces tests.

- [ ] **Étape 2 : l'entrée `PROMPTS.md`**

Une entrée pour l'implémentation, distincte de celle du brainstorming déjà écrite. Suivre le format condensé des entrées existantes (labels en gras, pas de liste à puces). Y consigner **le résultat réel du spike de la tâche 1**, attendu puis observé.

- [ ] **Étape 3 : le `README.md`**

- Retirer de « Limites connues » : le rejeu qui ne reconstitue pas l'état coulé, et le rechargement de page qui perd la partie.
- **Conserver** « redémarrer l'API perd les parties » : la persistance est restée hors périmètre.
- Ajouter à « Fonctionnalités livrées » : le flux d'événements, le rejeu fidèle, la révélation de la flotte adverse en fin de partie.
- Ajouter à « Arbitrages du backlog », côté écarté : persistance disque (rendue triviale, non faite) et concurrence optimiste (examinée, écartée).

- [ ] **Étape 4 : `docs/conformite.md`**

Ajouter une ligne par nouvelle exigence couverte, avec **une commande par exigence**, comme le reste du fichier.

- [ ] **Étape 5 : vérification finale**

```bash
dotnet build            # zéro avertissement
dotnet test             # tout au vert
dotnet format --verify-no-changes
grep -rn "//\|/\*" --include=*.cs BattleShip.Models BattleShip.API BattleShip.App | grep -v "^.*://"
```

La dernière commande doit ne rien rendre : **aucun commentaire** n'a été réintroduit.

- [ ] **Étape 6 : commit**

```bash
git add PROMPTS.md REVUE-IA.md README.md docs/conformite.md
git commit -m "docs: consigne l'implémentation du journal et la quatrième revue

La revue porte sur la censure par champ : sa force est la paire de
tests, chacun seul étant satisfait par une implémentation fausse.

Deux limites connues retirées du README, une conservée — la persistance
est restée hors périmètre par décision du binôme.

Co-Authored-By: Claude Opus 5 (1M context) <noreply@anthropic.com>"
```

---

## Auto-revue du plan

**1. Couverture de la spec**

| section de la spec | tâche |
|---|---|
| § 1 constat | 0 (ADR) |
| § 2.1 les événements | 2 |
| § 2.2 `Decide` / `Apply` | 3 |
| § 2.3 accumulateur du repli | 3, 4 |
| § 2.4 ce qui n'est pas fait | 0 (ADR) |
| § 3 censure par champ et frontière temporelle | 6 |
| § 4.1 ADR 0002 non remplacé | 0, 5 |
| § 4.2 `ReadEvents` | 5 |
| § 4.3 contrat HTTP | 6 |
| § 4.4 risque de sérialisation | 1 |
| § 5.1–5.2 repli partagé | 4, 8 |
| § 5.3 rejeu, reprise | 7, 8 |
| § 6.1 filet existant | étape « `dotnet test` » de chaque tâche |
| § 6.2 tests 1 à 5 | 4 (1, 2), 6 (3, 4), 5 (5) |
| § 7 ordre de travail | ordre des tâches 0 → 9 |

Aucune section sans tâche.

**2. Placeholders** — aucun `TBD`, aucun « gérer les erreurs comme il faut », aucun « comme la tâche N ». Le code des étapes est écrit en entier, y compris là où il se répète.

**3. Cohérence des types** — vérifiée de bout en bout : `ShotOutcome(Result, SunkShipName)` produit en tâche 3 et consommé en tâche 4 ; `GameEvent.Sequence` en premier paramètre partout ; `ReadEvents` rend `Result<IReadOnlyList<GameEvent>>` en tâche 5 et est consommé tel quel en tâche 6 ; `GameEventDto` et ses quatre dérivés définis en tâche 6 et consommés en tâche 8 ; `GameFold.Fold(Guid, IReadOnlyList<GameEvent>)` défini en tâche 4, appelé en tâche 8 avec la même signature.

**Deux points à relire avec une attention particulière pendant l'exécution**, identifiés comme les plus susceptibles de casser :

- **Tâche 3** — l'ancien `Fire` utilisait `Ships.FirstOrDefault(s => s.TryHit(at))`, un prédicat à effet de bord. `Decide` doit rester pur.
- **Tâche 8** — pendant la partie, `GameCreatedDto.OpponentShips` est vide par censure, donc le `Game` replié n'a pas de flotte adverse. La branche de rejeu doit retomber sur les coulés connus au lieu de rendre une liste vide.
