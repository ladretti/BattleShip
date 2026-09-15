# Bataille Navale — Plan d'implémentation

> **Pour les agents exécutants** : utiliser `superpowers:subagent-driven-development`
> (recommandé) ou `superpowers:executing-plans` pour exécuter ce plan tâche par tâche.
> Les étapes utilisent la syntaxe case à cocher (`- [ ]`).

**Objectif** : livrer une bataille navale jouable du navigateur au serveur, avec un adversaire
à trois niveaux dont la différence est mesurée, un tir en gRPC-Web et une validation serveur
complète.

**Architecture** : un domaine pur (`BattleShip.Models`) dont la vérité est portée par les
navires et l'ensemble des tirs — toute matrice est une projection calculée. `BattleShip.API`
porte les implémentations, deux façades (HTTP et gRPC-Web) et la validation.
`BattleShip.App` porte trois pages Blazor et un service `GameState`. Les refus métier passent
par `Result<T>`, jamais par des exceptions.

**Pile technique** : .NET 10, ASP.NET Core Minimal API, Blazor WebAssembly, gRPC-Web,
FluentValidation, xUnit.

**Spec** : `docs/superpowers/specs/2026-09-15-bataille-navale-design.md`

## Contraintes globales

Elles s'appliquent implicitement à **toutes** les tâches.

- **.NET 10** ; `global.json` épingle `10.0.100` en `rollForward: latestFeature`.
- **Minimal API**, jamais de contrôleurs MVC.
- `BattleShip.Models` **ne dépend d'aucun autre projet** — ni HTTP, ni JSON, ni gRPC.
- **Nullable activé** ; traiter les avertissements, ne pas les faire taire.
- `sealed` par défaut ; constructeurs primaires pour l'injection.
- **Aucun `Random.Shared` en dur** : la source d'aléa est injectée.
- **Refus métier → `Result<T>`** ; exceptions réservées aux anomalies (ADR 0004).
- **Le secret** : ni le front ni l'adversaire ne reçoivent les positions non découvertes.
- Noms de tests en **français descriptif** : `Un_tir_hors_grille_est_refuse`.
- `dotnet format` avant chaque commit ; messages de commit en français, un sujet par commit.
- Chaque commit se termine par la ligne de co-auteur en vigueur sur ce dépôt.

**Règles du jeu** (ADR 0006) : grille et flotte paramétrables, défaut 10×10 et 5-4-3-3-2 ;
navires interdits de se toucher, **diagonales comprises** ; **touche = on rejoue** ;
placement du joueur **manuel**, placement de l'adversaire automatique.

---

# Phase 0 — Amorçage et levée du risque technique

### Tâche 1 : Épingler le SDK, purger les gabarits, poser les dépendances

**Fichiers**
- Créer : `global.json` (copie), `api.http`
- Supprimer : `BattleShip.App/Pages/Counter.razor`, `BattleShip.App/Pages/Weather.razor`,
  `BattleShip.Models/Class1.cs`, `BattleShip.Tests/UnitTest1.cs`
- Modifier : `BattleShip.App/Layout/NavMenu.razor`, les quatre `.csproj`

**Interfaces**
- Produit : une solution qui compile et dont les tests passent, avec toutes les dépendances
  NuGet nécessaires aux tâches suivantes.

- [ ] **Étape 1 : Copier `global.json` et vérifier le SDK**

```bash
cp "../csharp-school/Ressources Bataille Navale/global.json" .
cat global.json
dotnet --version
```

Attendu : `global.json` présent à la racine, `dotnet --version` affiche une version 10.x.

- [ ] **Étape 2 : Supprimer les gabarits**

```bash
rm BattleShip.App/Pages/Counter.razor BattleShip.App/Pages/Weather.razor
rm BattleShip.Models/Class1.cs BattleShip.Tests/UnitTest1.cs
```

Retirer ensuite les entrées « Counter » et « Weather » de
`BattleShip.App/Layout/NavMenu.razor`.

- [ ] **Étape 3 : Ajouter les dépendances**

```bash
dotnet add BattleShip.API package FluentValidation
dotnet add BattleShip.API package FluentValidation.DependencyInjectionExtensions
dotnet add BattleShip.API package Grpc.AspNetCore
dotnet add BattleShip.API package Grpc.AspNetCore.Web

dotnet add BattleShip.App package Grpc.Net.Client
dotnet add BattleShip.App package Grpc.Net.Client.Web
dotnet add BattleShip.App package Google.Protobuf
dotnet add BattleShip.App package Grpc.Tools

dotnet add BattleShip.Tests package Microsoft.AspNetCore.Mvc.Testing
dotnet add BattleShip.Tests package Grpc.Net.Client
dotnet add BattleShip.Tests package Grpc.Net.Client.Web
```

> `Grpc.Net.Client.Web` est requis côté test : `GrpcWebHandler` n'est pas dans
> `Grpc.Net.Client`. Sans lui, le test de la tâche 2 échoue à la compilation (CS0234).

Éditer `BattleShip.App/BattleShip.App.csproj` pour que `Grpc.Tools` soit en
`PrivateAssets="all"`.

- [ ] **Étape 4 : Vérifier que tout compile**

Exécuter : `dotnet build && dotnet test`
Attendu : compilation sans erreur ; `dotnet test` signale zéro test (les gabarits ont été
supprimés), sans échec.

- [ ] **Étape 5 : Commit**

```bash
dotnet format
git add -A
git commit -m "chore: épingle le SDK, purge les gabarits et pose les dépendances"
```

---

### Tâche 2 : Lever le risque gRPC — spike de test d'intégration

> Cette tâche existe pour **échouer tôt si elle doit échouer**. Si le montage
> `WebApplicationFactory` + `GrpcChannel` ne fonctionne pas, tout le tir devient intestable
> et l'ADR 0005 doit être révisé au profit d'une façade HTTP. C'est pour cela qu'elle passe
> **avant** toute logique de jeu.

**Fichiers**
- Créer : `BattleShip.API/Protos/ping.proto`, `BattleShip.API/Services/PingService.cs`,
  `BattleShip.Tests/Integration/GrpcHarnessTests.cs`
- Modifier : `BattleShip.API/Program.cs`, `BattleShip.API/BattleShip.API.csproj`

**Interfaces**
- Produit : un montage de test gRPC réutilisable par la tâche 14.

- [ ] **Étape 1 : Écrire le contrat minimal**

`BattleShip.API/Protos/ping.proto` :

```proto
syntax = "proto3";
option csharp_namespace = "BattleShip.API.Grpc";
package battleship;

service PingService {
  rpc Ping (PingRequest) returns (PingReply);
}

message PingRequest { string name = 1; }
message PingReply   { string message = 1; }
```

Déclarer dans `BattleShip.API.csproj` :

```xml
<ItemGroup>
  <Protobuf Include="Protos\ping.proto" GrpcServices="Both" />
</ItemGroup>
```

> **`Both`, pas `Server`.** `GrpcServices="Server"` ne génère que la classe de base du service ;
> le **client** (`PingService.PingServiceClient`) ne serait pas généré et le test de l'étape 2
> ne compilerait pas. Avec `Both`, `BattleShip.Tests` obtient le client par sa référence de
> projet sur `BattleShip.API`, sans copie du `.proto` ni types dupliqués.

- [ ] **Étape 2 : Écrire le test d'intégration — il doit échouer**

`BattleShip.Tests/Integration/GrpcHarnessTests.cs` :

```csharp
using Grpc.Net.Client;
using Grpc.Net.Client.Web;
using Microsoft.AspNetCore.Mvc.Testing;
using BattleShip.API.Grpc;

namespace BattleShip.Tests.Integration;

public sealed class GrpcHarnessTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public GrpcHarnessTests(WebApplicationFactory<Program> factory) => _factory = factory;

    private GrpcChannel CreateChannel()
    {
        var handler = new GrpcWebHandler(_factory.Server.CreateHandler());
        return GrpcChannel.ForAddress(
            _factory.Server.BaseAddress,
            new GrpcChannelOptions { HttpHandler = handler });
    }

    [Fact]
    public async Task Un_appel_grpc_web_depuis_le_serveur_de_test_repond()
    {
        var client = new PingService.PingServiceClient(CreateChannel());

        var reply = await client.PingAsync(new PingRequest { Name = "test" });

        Assert.Equal("pong test", reply.Message);
    }
}
```

- [ ] **Étape 3 : Lancer le test et constater l'échec**

Exécuter : `dotnet test --filter Un_appel_grpc_web_depuis_le_serveur_de_test_repond`
Attendu : ÉCHEC — le service `PingService` n'existe pas encore.

- [ ] **Étape 4 : Implémenter le service et le câblage**

`BattleShip.API/Services/PingService.cs` :

```csharp
using Grpc.Core;
using BattleShip.API.Grpc;

namespace BattleShip.API.Services;

public sealed class PingGrpcService : PingService.PingServiceBase
{
    public override Task<PingReply> Ping(PingRequest request, ServerCallContext context) =>
        Task.FromResult(new PingReply { Message = $"pong {request.Name}" });
}
```

Dans `Program.cs`, avant `builder.Build()` : `builder.Services.AddGrpc();`
Après : `app.UseGrpcWeb();` puis
`app.MapGrpcService<PingGrpcService>().EnableGrpcWeb();`

Ajouter en fin de `Program.cs` : `public partial class Program;` — nécessaire pour que
`WebApplicationFactory<Program>` trouve le point d'entrée d'une Minimal API.

- [ ] **Étape 5 : Lancer le test et constater le succès**

Exécuter : `dotnet test --filter Un_appel_grpc_web_depuis_le_serveur_de_test_repond`
Attendu : SUCCÈS.

**Si l'étape échoue encore** : ne pas relancer à l'identique. Lire l'erreur entière, puis
signaler le blocage — l'ADR 0005 doit être révisé avant de continuer.

- [ ] **Étape 6 : Commit**

```bash
dotnet format
git add -A
git commit -m "test: valide le montage gRPC-Web sur WebApplicationFactory"
```

---

# Phase 1 — Le domaine

### Tâche 3 : Types de base et `Result<T>`

**Fichiers**
- Créer : `BattleShip.Models/Coordinate.cs`, `BattleShip.Models/ShipTemplate.cs`,
  `BattleShip.Models/Ship.cs`, `BattleShip.Models/Orientation.cs`,
  `BattleShip.Models/GameError.cs`, `BattleShip.Models/Result.cs`
- Tests : `BattleShip.Tests/Domaine/ShipTests.cs`, `BattleShip.Tests/Domaine/ResultTests.cs`

**Interfaces**
- Produit : `Coordinate(int X, int Y)`, `ShipTemplate(string Name, int Size)`,
  `Ship(string name, int size, IEnumerable<Coordinate> cells)` — constructeur positionnel
  acceptant une **`IEnumerable`** pour que les expressions de collection `[...]` des tests
  compilent — exposant `Name`, `Size`, `IReadOnlySet<Coordinate> Cells`,
  `IReadOnlySet<Coordinate> HitCells`, `bool IsSunk`, `bool TryHit(Coordinate at)` ;
  `Orientation { Horizontal, Vertical }` ;
  `GameError` ; `Result<T>` avec `Ok(T)`, `Fail(GameError)`, `IsOk`, `Value`, `Error`.

- [ ] **Étape 1 : Écrire les tests**

`BattleShip.Tests/Domaine/ShipTests.cs` :

```csharp
using BattleShip.Models;

namespace BattleShip.Tests.Domaine;

public sealed class ShipTests
{
    private static Ship Torpilleur() => new("Torpilleur", 2,
        [new Coordinate(0, 0), new Coordinate(0, 1)]);

    [Fact]
    public void Un_navire_neuf_n_est_pas_coule()
    {
        Assert.False(Torpilleur().IsSunk);
    }

    [Fact]
    public void Un_navire_touche_sur_toutes_ses_cases_est_coule()
    {
        var ship = Torpilleur();

        Assert.True(ship.TryHit(new Coordinate(0, 0)));
        Assert.False(ship.IsSunk);
        Assert.True(ship.TryHit(new Coordinate(0, 1)));
        Assert.True(ship.IsSunk);
    }

    [Fact]
    public void Un_tir_a_cote_ne_touche_pas_le_navire()
    {
        var ship = Torpilleur();

        Assert.False(ship.TryHit(new Coordinate(5, 5)));
        Assert.False(ship.IsSunk);
    }

    [Fact]
    public void Retirer_sur_une_case_deja_touchee_ne_coule_pas_le_navire()
    {
        var ship = Torpilleur();
        ship.TryHit(new Coordinate(0, 0));
        ship.TryHit(new Coordinate(0, 0));

        Assert.False(ship.IsSunk);
    }
}
```

> Le dernier test est celui qui discrimine : une implémentation qui incrémenterait un
> compteur `Hits` au lieu de mémoriser les cases touchées le fait échouer.

`BattleShip.Tests/Domaine/ResultTests.cs` :

```csharp
using BattleShip.Models;

namespace BattleShip.Tests.Domaine;

public sealed class ResultTests
{
    [Fact]
    public void Un_resultat_ok_porte_sa_valeur()
    {
        var result = Result<int>.Ok(42);

        Assert.True(result.IsOk);
        Assert.Equal(42, result.Value);
    }

    [Fact]
    public void Un_resultat_en_echec_porte_son_erreur()
    {
        var result = Result<int>.Fail(GameError.CellAlreadyShot);

        Assert.False(result.IsOk);
        Assert.Equal(GameError.CellAlreadyShot, result.Error);
    }

    [Fact]
    public void Lire_la_valeur_d_un_resultat_en_echec_est_une_anomalie()
    {
        var result = Result<int>.Fail(GameError.OutOfBounds);

        Assert.Throws<InvalidOperationException>(() => _ = result.Value);
    }
}
```

- [ ] **Étape 2 : Lancer les tests et constater l'échec**

Exécuter : `dotnet test --filter Domaine`
Attendu : ÉCHEC de compilation — aucun de ces types n'existe.

- [ ] **Étape 3 : Implémenter**

```csharp
// Coordinate.cs
namespace BattleShip.Models;

public readonly record struct Coordinate(int X, int Y);

// Orientation.cs
public enum Orientation { Horizontal, Vertical }

// ShipTemplate.cs
public sealed record ShipTemplate(string Name, int Size);

// GameError.cs
public enum GameError
{
    GameNotFound, OutOfBounds, CellAlreadyShot,
    GameAlreadyFinished, NotYourTurn, InvalidPlacement
}
```

`Ship.cs` : classe `sealed`, `Cells` en **`FrozenSet<Coordinate>`** construit depuis
l'`IEnumerable` reçu (`System.Collections.Frozen`, en boîte depuis .NET 8 — aucun paquet, donc
`BattleShip.Models` reste sans dépendance ; et un `FrozenSet` n'expose aucune méthode de
mutation, contrairement à un `HashSet` masqué derrière `IReadOnlySet` qu'un simple cast
suffirait à vider). `HitCells` en `HashSet<Coordinate>` privé exposé en lecture seule, avec
une doc XML disant que la mutation passe exclusivement par `TryHit`.
`IsSunk => HitCells.Count == Size`. `TryHit` ne renvoie `true` que pour une touche
**nouvelle** — convention `TryAdd` — afin qu'un appelant des tâches 6 ou 14 ne déclenche pas
deux fois un effet de bord sur le même dégât.

`Result.cs` : `readonly struct Result<T>` avec les fabriques `Ok` / `Fail` ; `Value` lève
`InvalidOperationException` si `IsOk` est faux (c'est une anomalie, pas un refus métier).

> **Discriminant explicite obligatoire.** Un `struct` a toujours un constructeur implicite
> sans paramètre : si `IsOk` se déduisait de `_error == null`, alors `default(Result<T>)`, un
> élément de tableau non initialisé ou un champ non assigné rapporteraient un **succès** avec
> `Value == default(T)`, sans qu'aucun `Ok(...)` n'ait été appelé. Porte donc un champ
> `bool _isOk` posé uniquement par les fabriques, fais lever `Value` **et** `Error` sur un
> `default`, et n'écris qu'**un seul** constructeur privé — deux constructeurs `Result(T)` et
> `Result(GameError)` entrent en collision de signatures dès que `T` vaut `GameError`.

- [ ] **Étape 4 : Lancer les tests et constater le succès**

Exécuter : `dotnet test --filter Domaine`
Attendu : 13 tests, tous au vert (5 sur `Ship`, 8 sur `Result`).

- [ ] **Étape 5 : Commit**

```bash
dotnet format
git add -A
git commit -m "feat: ajoute les types de base du domaine et Result<T>"
```

---

### Tâche 4 : `PlacementRules` — bornes, chevauchement, adjacence

> Ces règles sont **partagées** par le placement automatique (tâche 5) et par la validation du
> placement manuel (tâche 13). Les écrire deux fois produirait un `FleetPlacer` capable de
> générer des placements que le validateur refuserait.

**Fichiers**
- Créer : `BattleShip.Models/GameRules.cs`, `BattleShip.Models/PlacementRules.cs`,
  `BattleShip.Models/ShipPlacement.cs`
- Tests : `BattleShip.Tests/Domaine/PlacementRulesTests.cs`

**Interfaces**
- Consomme : `Coordinate`, `Orientation`, `ShipTemplate`, `GameError` (tâche 3).
- Produit :
  - `GameRules(int GridSize, IReadOnlyList<ShipTemplate> Fleet, bool ShipsMayTouch, bool ExtraTurnOnHit)`
    avec `GameRules.Default` = grille 10 et la flotte **exactement nommée** :
    `Porte-avions` 5, `Croiseur` 4, `Contre-torpilleur` 3, `Sous-marin` 3, `Torpilleur` 2 ;
    `ShipsMayTouch: false`, `ExtraTurnOnHit: true`. Ces noms sont codés en dur dans les tests
    des tâches 13 et 14 : ne pas les changer.
  - `ShipPlacement(string Name, Coordinate Origin, Orientation Orientation, int Size)`
    avec `IEnumerable<Coordinate> Cells()`
  - `static Result<IReadOnlyList<Ship>> PlacementRules.Validate(IReadOnlyList<ShipPlacement> placements, GameRules rules)`

- [ ] **Étape 1 : Écrire les tests**

`BattleShip.Tests/Domaine/PlacementRulesTests.cs` :

```csharp
using BattleShip.Models;

namespace BattleShip.Tests.Domaine;

public sealed class PlacementRulesTests
{
    private static readonly GameRules Petite =
        new(GridSize: 5, Fleet: [new ShipTemplate("Torpilleur", 2)],
            ShipsMayTouch: false, ExtraTurnOnHit: true);

    private static ShipPlacement Torpilleur(int x, int y, Orientation o = Orientation.Horizontal)
        => new("Torpilleur", new Coordinate(x, y), o, 2);

    [Fact]
    public void Un_placement_valide_est_accepte()
    {
        var result = PlacementRules.Validate([Torpilleur(0, 0)], Petite);

        Assert.True(result.IsOk);
        Assert.Single(result.Value);
    }

    [Fact]
    public void Un_navire_qui_deborde_la_grille_est_refuse()
    {
        var result = PlacementRules.Validate([Torpilleur(4, 0)], Petite);

        Assert.False(result.IsOk);
        Assert.Equal(GameError.InvalidPlacement, result.Error);
    }

    [Fact]
    public void Deux_navires_qui_se_chevauchent_sont_refuses()
    {
        var rules = Petite with
        {
            Fleet = [new ShipTemplate("A", 2), new ShipTemplate("B", 2)]
        };
        var placements = new[]
        {
            new ShipPlacement("A", new Coordinate(0, 0), Orientation.Horizontal, 2),
            new ShipPlacement("B", new Coordinate(1, 0), Orientation.Horizontal, 2)
        };

        var result = PlacementRules.Validate(placements, rules);

        Assert.False(result.IsOk);
    }

    [Theory]
    [InlineData(0, 1)]   // dessous
    [InlineData(2, 0)]   // bout à bout
    [InlineData(2, 1)]   // diagonale
    public void Deux_navires_qui_se_touchent_sont_refuses(int x, int y)
    {
        var rules = Petite with
        {
            Fleet = [new ShipTemplate("A", 2), new ShipTemplate("B", 2)]
        };
        var placements = new[]
        {
            new ShipPlacement("A", new Coordinate(0, 0), Orientation.Horizontal, 2),
            new ShipPlacement("B", new Coordinate(x, y), Orientation.Horizontal, 2)
        };

        var result = PlacementRules.Validate(placements, rules);

        Assert.False(result.IsOk);
    }

    [Fact]
    public void Deux_navires_qui_se_touchent_sont_acceptes_si_la_regle_l_autorise()
    {
        var rules = Petite with
        {
            Fleet = [new ShipTemplate("A", 2), new ShipTemplate("B", 2)],
            ShipsMayTouch = true
        };
        var placements = new[]
        {
            new ShipPlacement("A", new Coordinate(0, 0), Orientation.Horizontal, 2),
            new ShipPlacement("B", new Coordinate(0, 1), Orientation.Horizontal, 2)
        };

        var result = PlacementRules.Validate(placements, rules);

        Assert.True(result.IsOk);
    }

    [Fact]
    public void Une_flotte_incomplete_est_refusee()
    {
        var rules = Petite with
        {
            Fleet = [new ShipTemplate("A", 2), new ShipTemplate("B", 2)]
        };

        var result = PlacementRules.Validate(
            [new ShipPlacement("A", new Coordinate(0, 0), Orientation.Horizontal, 2)],
            rules);

        Assert.False(result.IsOk);
    }
}
```

> Le `[Theory]` sur l'adjacence est le test qui compte : le cas `(2, 0)` (bout à bout) et le
> cas `(2, 1)` (diagonale) font échouer une implémentation qui ne vérifierait que les quatre
> voisins orthogonaux.

- [ ] **Étape 2 : Lancer les tests et constater l'échec**

Exécuter : `dotnet test --filter PlacementRules`
Attendu : ÉCHEC de compilation — `GameRules`, `ShipPlacement` et `PlacementRules` n'existent pas.

- [ ] **Étape 3 : Implémenter**

`GameRules` est un `record` ; ajouter une propriété statique `Default`.
`ShipPlacement.Cells()` produit les `Size` coordonnées depuis `Origin` selon `Orientation`.

`PlacementRules.Validate` enchaîne quatre contrôles, dans cet ordre, et renvoie
`Result<IReadOnlyList<Ship>>.Fail(GameError.InvalidPlacement)` au premier échec :

1. la flotte soumise correspond exactement, en noms et en tailles, à `rules.Fleet` ;
2. toutes les cases sont dans `[0, GridSize[` ;
3. aucune case n'apparaît deux fois (chevauchement) ;
4. si `!rules.ShipsMayTouch`, aucune case d'un navire n'est dans le **voisinage de Moore**
   (les huit voisins) d'une case d'un autre navire.

En cas de succès, construire les `Ship` correspondants.

- [ ] **Étape 4 : Lancer les tests et constater le succès**

Exécuter : `dotnet test --filter PlacementRules`
Attendu : 8 cas, tous au vert.

- [ ] **Étape 5 : Commit**

```bash
dotnet format
git add -A
git commit -m "feat: ajoute les règles de placement partagées"
```

---

### Tâche 5 : `FleetPlacer` — placement automatique à aléa injecté

**Fichiers**
- Créer : `BattleShip.Models/FleetPlacer.cs`
- Tests : `BattleShip.Tests/Domaine/FleetPlacerTests.cs`

**Interfaces**
- Consomme : `PlacementRules.Validate`, `GameRules`, `ShipPlacement` (tâche 4).
- Produit : `FleetPlacer(Random random)` avec
  `Result<IReadOnlyList<Ship>> PlaceAll(GameRules rules)`.

- [ ] **Étape 1 : Écrire les tests**

`BattleShip.Tests/Domaine/FleetPlacerTests.cs` :

```csharp
using BattleShip.Models;

namespace BattleShip.Tests.Domaine;

public sealed class FleetPlacerTests
{
    public static TheoryData<int> Graines()
    {
        var data = new TheoryData<int>();
        for (var seed = 1; seed <= 50; seed++) data.Add(seed);
        return data;
    }

    [Theory]
    [MemberData(nameof(Graines))]
    public void Un_placement_automatique_respecte_toujours_les_regles(int seed)
    {
        var placer = new FleetPlacer(new Random(seed));

        var result = placer.PlaceAll(GameRules.Default);

        Assert.True(result.IsOk);
        var cells = result.Value.SelectMany(s => s.Cells).ToList();

        // Aucune case hors grille.
        Assert.All(cells, c =>
        {
            Assert.InRange(c.X, 0, GameRules.Default.GridSize - 1);
            Assert.InRange(c.Y, 0, GameRules.Default.GridSize - 1);
        });

        // Aucun chevauchement.
        Assert.Equal(cells.Count, cells.Distinct().Count());

        // Aucune adjacence entre deux navires distincts.
        foreach (var a in result.Value)
            foreach (var b in result.Value.Where(x => !ReferenceEquals(x, a)))
                foreach (var ca in a.Cells)
                    foreach (var cb in b.Cells)
                        Assert.False(
                            Math.Abs(ca.X - cb.X) <= 1 && Math.Abs(ca.Y - cb.Y) <= 1,
                            $"navires adjacents en {ca} et {cb} (graine {seed})");
    }

    [Fact]
    public void Deux_graines_identiques_produisent_le_meme_placement()
    {
        var a = new FleetPlacer(new Random(12345)).PlaceAll(GameRules.Default);
        var b = new FleetPlacer(new Random(12345)).PlaceAll(GameRules.Default);

        Assert.Equal(
            a.Value.SelectMany(s => s.Cells).ToList(),
            b.Value.SelectMany(s => s.Cells).ToList());
    }

    [Fact]
    public void Une_flotte_qui_ne_tient_pas_dans_la_grille_est_refusee_sans_boucler()
    {
        var impossible = new GameRules(
            GridSize: 3,
            Fleet: [new ShipTemplate("A", 3), new ShipTemplate("B", 3),
                    new ShipTemplate("C", 3)],
            ShipsMayTouch: false,
            ExtraTurnOnHit: true);

        var result = new FleetPlacer(new Random(1)).PlaceAll(impossible);

        Assert.False(result.IsOk);
        Assert.Equal(GameError.InvalidPlacement, result.Error);
    }
}
```

> Le dernier test est le garde-fou de terminaison : sans compteur de garde, le tirage-rejet
> boucle indéfiniment sur une flotte impossible et le test ne se termine jamais.

- [ ] **Étape 2 : Lancer les tests et constater l'échec**

Exécuter : `dotnet test --filter FleetPlacer`
Attendu : ÉCHEC de compilation — `FleetPlacer` n'existe pas.

- [ ] **Étape 3 : Implémenter**

`FleetPlacer(Random random)` en constructeur primaire. `PlaceAll` : pour chaque navire, tirer
origine et orientation, retenir le placement s'il reste compatible avec les précédents selon
`PlacementRules`. Deux compteurs de garde — un par navire, un pour le nombre de relances
complètes — puis `Result.Fail(GameError.InvalidPlacement)`. Terminer par un appel à
`PlacementRules.Validate` sur la flotte complète : c'est la même règle qui fait foi partout.

- [ ] **Étape 4 : Lancer les tests et constater le succès**

Exécuter : `dotnet test --filter FleetPlacer`
Attendu : 52 cas, tous au vert, en moins de quelques secondes.

- [ ] **Étape 5 : Commit**

```bash
dotnet format
git add -A
git commit -m "feat: ajoute le placement automatique à aléa injecté"
```

---

### Tâche 6 : `Board`, `Game` et le moteur de tir

**Fichiers**
- Créer : `BattleShip.Models/Board.cs`, `BattleShip.Models/ShotRecord.cs`,
  `BattleShip.Models/Game.cs`, `BattleShip.Models/Player.cs`,
  `BattleShip.Models/GameStatus.cs`, `BattleShip.Models/ShotResult.cs`
- Tests : `BattleShip.Tests/Domaine/GameTests.cs`

**Interfaces**
- Consomme : tâches 3 à 5.
- Produit :
  - `Player { Human, Opponent }`, `GameStatus { Placing, InProgress, Finished }`,
    `ShotResult { Miss, Hit, Sunk }`
  - `ShotRecord(Coordinate At, ShotResult Result, Player By, string? SunkShipName)`
  - `Board(int gridSize, IReadOnlyList<Ship> ships)` avec `ReceivedShots`,
    `bool AllSunk`, `Result<ShotRecord> Fire(Coordinate at, Player by)`
  - `Game` avec `Id`, `Rules`, `HumanBoard`, `OpponentBoard`, `CurrentPlayer`, `Status`,
    `IReadOnlyList<ShotRecord> History`, `Result<ShotRecord> PlayerFires(Coordinate at)`,
    et son symétrique `Result<ShotRecord> OpponentFires(Coordinate at)` — qui tire sur
    `HumanBoard`, exige `CurrentPlayer == Opponent`, rend la main au joueur sur un coup
    manqué, et passe en `Finished` si `HumanBoard.AllSunk`. **La tâche 14 n'a aucun autre
    moyen de faire jouer l'adversaire.**

- [ ] **Étape 1 : Écrire les tests**

`BattleShip.Tests/Domaine/GameTests.cs` :

```csharp
using BattleShip.Models;

namespace BattleShip.Tests.Domaine;

public sealed class GameTests
{
    // Grille 3x3 avec un seul torpilleur horizontal en (0,0)-(1,0).
    private static Game PartieMinuscule()
    {
        var rules = new GameRules(3, [new ShipTemplate("Torpilleur", 2)], false, true);
        var flotte = () => new List<Ship>
        {
            new("Torpilleur", 2, [new Coordinate(0, 0), new Coordinate(1, 0)])
        };
        return Game.Start(Guid.NewGuid(), rules, flotte(), flotte());
    }

    [Fact]
    public void Un_tir_hors_grille_est_refuse()
    {
        var game = PartieMinuscule();

        var result = game.PlayerFires(new Coordinate(3, 0));

        Assert.False(result.IsOk);
        Assert.Equal(GameError.OutOfBounds, result.Error);
    }

    [Fact]
    public void Un_tir_sur_une_case_deja_jouee_est_refuse()
    {
        // (0,0) TOUCHE, donc la main reste au joueur (ExtraTurnOnHit).
        // Sur un coup manqué le refus attendu serait NotYourTurn, pas CellAlreadyShot.
        var game = PartieMinuscule();
        game.PlayerFires(new Coordinate(0, 0));

        var result = game.PlayerFires(new Coordinate(0, 0));

        Assert.False(result.IsOk);
        Assert.Equal(GameError.CellAlreadyShot, result.Error);
    }

    [Fact]
    public void L_adversaire_ne_peut_pas_tirer_quand_c_est_au_joueur()
    {
        var game = PartieMinuscule();

        var result = game.OpponentFires(new Coordinate(0, 0));

        Assert.False(result.IsOk);
        Assert.Equal(GameError.NotYourTurn, result.Error);
    }

    [Fact]
    public void Un_coup_manque_de_l_adversaire_rend_la_main_au_joueur()
    {
        var game = PartieMinuscule();
        game.PlayerFires(new Coordinate(2, 2));   // manqué : la main passe

        var result = game.OpponentFires(new Coordinate(2, 2));

        Assert.True(result.IsOk);
        Assert.Equal(ShotResult.Miss, result.Value.Result);
        Assert.Equal(Player.Human, game.CurrentPlayer);
    }

    [Fact]
    public void Un_tir_sur_la_derniere_case_d_un_navire_le_coule()
    {
        var game = PartieMinuscule();
        game.PlayerFires(new Coordinate(0, 0));

        var result = game.PlayerFires(new Coordinate(1, 0));

        Assert.Equal(ShotResult.Sunk, result.Value.Result);
        Assert.Equal("Torpilleur", result.Value.SunkShipName);
    }

    [Fact]
    public void Couler_le_dernier_navire_termine_la_partie()
    {
        var game = PartieMinuscule();
        game.PlayerFires(new Coordinate(0, 0));
        game.PlayerFires(new Coordinate(1, 0));

        Assert.Equal(GameStatus.Finished, game.Status);
    }

    [Fact]
    public void Un_tir_apres_la_fin_de_partie_est_refuse()
    {
        var game = PartieMinuscule();
        game.PlayerFires(new Coordinate(0, 0));
        game.PlayerFires(new Coordinate(1, 0));

        var result = game.PlayerFires(new Coordinate(2, 2));

        Assert.False(result.IsOk);
        Assert.Equal(GameError.GameAlreadyFinished, result.Error);
    }

    [Fact]
    public void Une_touche_laisse_la_main_au_joueur()
    {
        var game = PartieMinuscule();

        game.PlayerFires(new Coordinate(0, 0));

        Assert.Equal(Player.Human, game.CurrentPlayer);
    }

    [Fact]
    public void Un_coup_manque_passe_la_main_a_l_adversaire()
    {
        var game = PartieMinuscule();

        game.PlayerFires(new Coordinate(2, 2));

        Assert.Equal(Player.Opponent, game.CurrentPlayer);
    }

    [Fact]
    public void Un_tir_du_joueur_quand_ce_n_est_pas_son_tour_est_refuse()
    {
        var game = PartieMinuscule();
        game.PlayerFires(new Coordinate(2, 2));   // manqué : la main passe

        var result = game.PlayerFires(new Coordinate(2, 1));

        Assert.False(result.IsOk);
        Assert.Equal(GameError.NotYourTurn, result.Error);
    }
}
```

> Les deux tests sur la main sont ceux qui distinguent la règle « touche = on rejoue » d'un
> tour strictement alterné.

- [ ] **Étape 2 : Lancer les tests et constater l'échec**

Exécuter : `dotnet test --filter GameTests`
Attendu : ÉCHEC de compilation.

- [ ] **Étape 3 : Implémenter**

`Board` détient `IReadOnlyList<Ship> Ships` et `HashSet<Coordinate> ReceivedShots` — **la
seule vérité**, aucune matrice. `Fire` contrôle les bornes puis la case déjà jouée, enregistre
le tir, cherche le navire touché et produit `Miss` / `Hit` / `Sunk`.

`Game.Start(Guid id, GameRules rules, IReadOnlyList<Ship> humanShips, IReadOnlyList<Ship> opponentShips)`
construit les deux `Board` et passe en `InProgress`. `PlayerFires` contrôle dans cet ordre :
`Status != Finished`, `CurrentPlayer == Human`, puis délègue à `OpponentBoard.Fire`. En cas de
succès : enregistrer dans `History`, passer la main si le coup est manqué ou si
`ExtraTurnOnHit` est faux, et passer en `Finished` si `OpponentBoard.AllSunk`.

`OpponentFires` est son symétrique exact : mêmes contrôles avec `CurrentPlayer == Opponent`,
tir sur `HumanBoard`, `Finished` si `HumanBoard.AllSunk`. Factoriser le corps commun dans une
méthode privée prenant le plateau cible et le joueur — ne pas dupliquer la logique de tour.

- [ ] **Étape 4 : Lancer les tests et constater le succès**

Exécuter : `dotnet test --filter GameTests`
Attendu : 10 tests au vert.

- [ ] **Étape 5 : Commit**

```bash
dotnet format
git add -A
git commit -m "feat: ajoute le moteur de tir et la machine à états de partie"
```

---

### Tâche 7 : `IGameStore` et verrou par partie

**Fichiers**
- Créer : `BattleShip.Models/IGameStore.cs`, `BattleShip.API/Stores/InMemoryGameStore.cs`
- Tests : `BattleShip.Tests/Domaine/InMemoryGameStoreTests.cs`

**Interfaces**
- Produit : `IGameStore` avec `Find`, `Save`, `Remove`, et
  `Result<T> Mutate<T>(Guid id, Func<Game, Result<T>> change)`.

- [ ] **Étape 1 : Écrire le test de concurrence**

`BattleShip.Tests/Domaine/InMemoryGameStoreTests.cs` :

```csharp
using BattleShip.API.Stores;
using BattleShip.Models;

namespace BattleShip.Tests.Domaine;

public sealed class InMemoryGameStoreTests
{
    private static Game PartieSurGrandeGrille()
    {
        var rules = GameRules.Default;
        var flotte = () => new FleetPlacer(new Random(7)).PlaceAll(rules).Value;
        return Game.Start(Guid.NewGuid(), rules, flotte(), flotte());
    }

    [Fact]
    public void Une_partie_absente_renvoie_GameNotFound()
    {
        var store = new InMemoryGameStore();

        var result = store.Mutate(Guid.NewGuid(), g => g.PlayerFires(new Coordinate(0, 0)));

        Assert.False(result.IsOk);
        Assert.Equal(GameError.GameNotFound, result.Error);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(5)]
    public async Task Un_seul_tir_simultane_sur_la_meme_case_reussit(int execution)
    {
        _ = execution;   // le cas est rejoué : une course qui passe une fois ne prouve rien
        var store = new InMemoryGameStore();
        var game = PartieSurGrandeGrille();
        store.Save(game);

        var cible = new Coordinate(0, 0);
        var tirs = Enumerable.Range(0, 32).Select(_ => Task.Run(() =>
            store.Mutate(game.Id, g => g.PlayerFires(cible))));

        var resultats = await Task.WhenAll(tirs);

        Assert.Equal(1, resultats.Count(r => r.IsOk));
        Assert.All(resultats.Where(r => !r.IsOk), r =>
            Assert.Contains(r.Error, new[]
            {
                GameError.CellAlreadyShot, GameError.NotYourTurn,
                GameError.GameAlreadyFinished
            }));
    }

    [Fact]
    public void Deux_parties_distinctes_ne_se_bloquent_pas()
    {
        var store = new InMemoryGameStore();
        var a = PartieSurGrandeGrille();
        var b = PartieSurGrandeGrille();
        store.Save(a);
        store.Save(b);

        var ra = store.Mutate(a.Id, g => g.PlayerFires(new Coordinate(0, 0)));
        var rb = store.Mutate(b.Id, g => g.PlayerFires(new Coordinate(0, 0)));

        Assert.True(ra.IsOk);
        Assert.True(rb.IsOk);
    }
}
```

- [ ] **Étape 2 : Lancer les tests et constater l'échec**

Exécuter : `dotnet test --filter InMemoryGameStore`
Attendu : ÉCHEC de compilation.

- [ ] **Étape 3 : Implémenter**

```csharp
// BattleShip.Models/IGameStore.cs
namespace BattleShip.Models;

public interface IGameStore
{
    Game? Find(Guid id);
    void Save(Game game);
    bool Remove(Guid id);
    Result<T> Mutate<T>(Guid id, Func<Game, Result<T>> change);
}
```

`InMemoryGameStore` : un `ConcurrentDictionary<Guid, Game>` pour les parties, un
`ConcurrentDictionary<Guid, object>` pour les verrous. `Mutate` récupère la partie (sinon
`GameNotFound`), prend le verrou de **cette** partie via `GetOrAdd`, applique `change`, puis
relâche.

- [ ] **Étape 4 : Lancer les tests et constater le succès**

Exécuter : `dotnet test --filter InMemoryGameStore`
Attendu : 7 cas au vert. **Relancer trois fois** — un test de course qui passe une seule fois
ne prouve rien.

- [ ] **Étape 5 : Commit**

```bash
dotnet format
git add -A
git commit -m "feat: ajoute le store en mémoire avec verrou par partie"
```

---

# Phase 2 — L'adversaire

### Tâche 8 : Contrat de stratégie, `RandomStrategy` et l'invariant partagé

> Le test d'invariant écrit ici est **paramétré par la liste des stratégies**. Les tâches 9 et
> 10 n'auront qu'à y ajouter une ligne : c'est ce qui rend l'ajout d'une stratégie bon marché.

**Fichiers**
- Créer : `BattleShip.Models/ShotHistory.cs`, `BattleShip.Models/IOpponentStrategy.cs`,
  `BattleShip.API/Strategies/RandomStrategy.cs`
- Tests : `BattleShip.Tests/Adversaire/StrategyInvariantTests.cs`

**Interfaces**
- Produit :
  - `ShotHistory(int GridSize, IReadOnlyList<ShotRecord> Shots, IReadOnlyList<ShipTemplate> RemainingShips, IReadOnlyList<Ship> SunkShips, bool ShipsMayTouch)`
  - `IOpponentStrategy` avec `string Name { get; }` et `Coordinate NextShot(ShotHistory history)`
  - `RandomStrategy(Random random)`

- [ ] **Étape 1 : Écrire le test d'invariant**

`BattleShip.Tests/Adversaire/StrategyInvariantTests.cs` :

```csharp
using BattleShip.API.Strategies;
using BattleShip.Models;

namespace BattleShip.Tests.Adversaire;

public sealed class StrategyInvariantTests
{
    public static TheoryData<string, Func<Random, IOpponentStrategy>> Strategies() => new()
    {
        { "Random", r => new RandomStrategy(r) },
        // Tâche 9  : { "HuntTarget", r => new HuntTargetStrategy(r) },
        // Tâche 10 : { "Density",    r => new DensityStrategy(r) },
    };

    [Theory]
    [MemberData(nameof(Strategies))]
    public void Une_strategie_ne_propose_jamais_un_coup_invalide(
        string nom, Func<Random, IOpponentStrategy> fabrique)
    {
        var rules = GameRules.Default;

        for (var seed = 1; seed <= 20; seed++)
        {
            var strategy = fabrique(new Random(seed));
            var cible = new Board(rules.GridSize,
                new FleetPlacer(new Random(seed * 31)).PlaceAll(rules).Value);

            var joues = new HashSet<Coordinate>();
            var coups = new List<ShotRecord>();

            while (!cible.AllSunk)
            {
                var history = HistoriqueDepuis(rules, cible, coups);
                var coup = strategy.NextShot(history);

                Assert.InRange(coup.X, 0, rules.GridSize - 1);
                Assert.InRange(coup.Y, 0, rules.GridSize - 1);
                Assert.True(joues.Add(coup),
                    $"{nom} (graine {seed}) a rejoué la case {coup}");

                var tir = cible.Fire(coup, Player.Opponent);
                Assert.True(tir.IsOk);
                coups.Add(tir.Value);
            }

            Assert.True(joues.Count <= rules.GridSize * rules.GridSize);
        }
    }

    internal static ShotHistory HistoriqueDepuis(
        GameRules rules, Board cible, IReadOnlyList<ShotRecord> coups)
    {
        var coules = cible.Ships.Where(s => s.IsSunk).ToList();
        var restants = rules.Fleet
            .Where(t => coules.All(s => s.Name != t.Name))
            .ToList();

        return new ShotHistory(rules.GridSize, coups, restants, coules, rules.ShipsMayTouch);
    }
}
```

> Ce test détecte les deux fautes les plus probables : ne pas filtrer les cases déjà jouées,
> et déborder la grille en ratissant depuis un bord (tâche 9). Il ne se termine pas si une
> stratégie ne progresse plus — c'est voulu, et le harnais xUnit le signalera en timeout.

- [ ] **Étape 2 : Lancer le test et constater l'échec**

Exécuter : `dotnet test --filter Adversaire`
Attendu : ÉCHEC de compilation.

- [ ] **Étape 3 : Implémenter**

`ShotHistory` en `sealed record`. `IOpponentStrategy` comme spécifié. `RandomStrategy(Random random)` :
construire l'ensemble des cases de la grille, retirer celles présentes dans `history.Shots`,
tirer au hasard parmi le reste.

Exposer `Board.Ships` en lecture seule (nécessaire au test, pas au front).

- [ ] **Étape 4 : Lancer le test et constater le succès**

Exécuter : `dotnet test --filter Adversaire`
Attendu : 1 cas au vert.

- [ ] **Étape 5 : Commit**

```bash
dotnet format
git add -A
git commit -m "feat: ajoute le contrat de stratégie et l'adversaire aléatoire"
```

---

### Tâche 9 : `HuntTargetStrategy` — chasse avec parité, puis ratissage

**Fichiers**
- Créer : `BattleShip.API/Strategies/HuntTargetStrategy.cs`
- Tests : `BattleShip.Tests/Adversaire/HuntTargetStrategyTests.cs`
- Modifier : `BattleShip.Tests/Adversaire/StrategyInvariantTests.cs` (décommenter la ligne)

**Interfaces**
- Consomme : `ShotHistory`, `IOpponentStrategy` (tâche 8).
- Produit : `HuntTargetStrategy(Random random)`.

- [ ] **Étape 1 : Écrire les tests de comportement**

`BattleShip.Tests/Adversaire/HuntTargetStrategyTests.cs` :

```csharp
using BattleShip.API.Strategies;
using BattleShip.Models;

namespace BattleShip.Tests.Adversaire;

public sealed class HuntTargetStrategyTests
{
    private static ShotHistory Historique(params ShotRecord[] coups) =>
        new(10, coups, [new ShipTemplate("Torpilleur", 2)], [], ShipsMayTouch: false);

    [Fact]
    public void En_phase_de_chasse_elle_ne_vise_que_les_cases_de_parite_paire()
    {
        var strategy = new HuntTargetStrategy(new Random(1));

        for (var i = 0; i < 40; i++)
        {
            var coup = strategy.NextShot(Historique());
            Assert.Equal(0, (coup.X + coup.Y) % 2);
        }
    }

    [Fact]
    public void Apres_une_touche_elle_vise_une_case_adjacente()
    {
        var strategy = new HuntTargetStrategy(new Random(1));
        var touche = new ShotRecord(new Coordinate(4, 4), ShotResult.Hit,
            Player.Opponent, null);

        var coup = strategy.NextShot(Historique(touche));

        var distance = Math.Abs(coup.X - 4) + Math.Abs(coup.Y - 4);
        Assert.Equal(1, distance);
    }

    [Fact]
    public void Apres_une_touche_dans_un_coin_elle_ne_sort_pas_de_la_grille()
    {
        var strategy = new HuntTargetStrategy(new Random(1));
        var touche = new ShotRecord(new Coordinate(0, 0), ShotResult.Hit,
            Player.Opponent, null);

        var coup = strategy.NextShot(Historique(touche));

        Assert.InRange(coup.X, 0, 9);
        Assert.InRange(coup.Y, 0, 9);
    }

    [Fact]
    public void Un_navire_coule_ne_declenche_plus_de_ratissage()
    {
        var strategy = new HuntTargetStrategy(new Random(1));
        var history = new ShotHistory(
            10,
            [new ShotRecord(new Coordinate(4, 4), ShotResult.Hit, Player.Opponent, null),
             new ShotRecord(new Coordinate(4, 5), ShotResult.Sunk, Player.Opponent, "Torpilleur")],
            [],
            [new Ship("Torpilleur", 2, [new Coordinate(4, 4), new Coordinate(4, 5)])],
            ShipsMayTouch: false);

        var coup = strategy.NextShot(history);

        Assert.Equal(0, (coup.X + coup.Y) % 2);   // retour en phase de chasse
    }
}
```

- [ ] **Étape 2 : Lancer les tests et constater l'échec**

Exécuter : `dotnet test --filter HuntTarget`
Attendu : ÉCHEC de compilation.

- [ ] **Étape 3 : Implémenter**

La stratégie est **sans état** : elle reconstruit sa décision depuis `history` à chaque appel,
ce qui la rend testable en isolation et immunisée contre une remise en jeu.

1. Collecter les cases touchées non encore couvertes par un navire coulé
   (`history.SunkShips`).
2. S'il en reste, produire les voisins orthogonaux, dans la grille et non déjà joués. Si deux
   touches sont alignées, privilégier la prolongation de l'alignement.
3. Sinon, phase de chasse : cases non jouées avec `(x + y) % 2 == 0`. Si cet ensemble est
   vide, se rabattre sur toutes les cases non jouées.

- [ ] **Étape 4 : Ajouter la stratégie à l'invariant partagé**

Décommenter dans `StrategyInvariantTests.Strategies()` :

```csharp
{ "HuntTarget", r => new HuntTargetStrategy(r) },
```

- [ ] **Étape 5 : Lancer tous les tests d'adversaire**

Exécuter : `dotnet test --filter Adversaire`
Attendu : les 4 tests de comportement **et** les 2 cas d'invariant au vert.

- [ ] **Étape 6 : Commit**

```bash
dotnet format
git add -A
git commit -m "feat: ajoute l'adversaire chasse-cible avec parité"
```

---

### Tâche 10 : `DensityStrategy` — densité probabiliste

**Fichiers**
- Créer : `BattleShip.API/Strategies/DensityStrategy.cs`
- Tests : `BattleShip.Tests/Adversaire/DensityStrategyTests.cs`
- Modifier : `BattleShip.Tests/Adversaire/StrategyInvariantTests.cs`

**Interfaces**
- Produit : `DensityStrategy(Random random)`.

- [ ] **Étape 1 : Écrire les tests de comportement**

`BattleShip.Tests/Adversaire/DensityStrategyTests.cs` :

```csharp
using BattleShip.API.Strategies;
using BattleShip.Models;

namespace BattleShip.Tests.Adversaire;

public sealed class DensityStrategyTests
{
    [Fact]
    public void Sur_une_grille_vierge_elle_vise_le_centre_plutot_qu_un_coin()
    {
        var strategy = new DensityStrategy(new Random(1));
        var history = new ShotHistory(10, [], [new ShipTemplate("Croiseur", 4)], [], false);

        var coup = strategy.NextShot(history);

        Assert.InRange(coup.X, 2, 7);
        Assert.InRange(coup.Y, 2, 7);
    }

    [Fact]
    public void Elle_prolonge_une_touche_isolee()
    {
        var strategy = new DensityStrategy(new Random(1));
        var history = new ShotHistory(
            10,
            [new ShotRecord(new Coordinate(4, 4), ShotResult.Hit, Player.Opponent, null)],
            [new ShipTemplate("Croiseur", 4)], [], false);

        var coup = strategy.NextShot(history);

        Assert.Equal(1, Math.Abs(coup.X - 4) + Math.Abs(coup.Y - 4));
    }

    [Fact]
    public void Sous_la_regle_de_non_adjacence_elle_evite_la_couronne_d_un_navire_coule()
    {
        var coule = new Ship("Torpilleur", 2,
            [new Coordinate(4, 4), new Coordinate(4, 5)]);
        var strategy = new DensityStrategy(new Random(1));
        var history = new ShotHistory(
            10,
            [new ShotRecord(new Coordinate(4, 4), ShotResult.Hit, Player.Opponent, null),
             new ShotRecord(new Coordinate(4, 5), ShotResult.Sunk, Player.Opponent, "Torpilleur")],
            [new ShipTemplate("Croiseur", 4)],
            [coule],
            ShipsMayTouch: false);

        for (var i = 0; i < 20; i++)
        {
            var coup = new DensityStrategy(new Random(i + 1)).NextShot(history);
            var dansLaCouronne = coule.Cells.Any(c =>
                Math.Abs(c.X - coup.X) <= 1 && Math.Abs(c.Y - coup.Y) <= 1);
            Assert.False(dansLaCouronne, $"coup {coup} dans la couronne du navire coulé");
        }
        _ = strategy;
    }
}
```

> Le troisième test est le plus discriminant : il échoue sur une densité qui ignorerait
> `ShipsMayTouch`, c'est-à-dire sur une implémentation générique recopiée sans tenir compte de
> notre ADR 0006.

- [ ] **Étape 2 : Lancer les tests et constater l'échec**

Exécuter : `dotnet test --filter Density`
Attendu : ÉCHEC de compilation.

- [ ] **Étape 3 : Implémenter**

Pour chaque `ShipTemplate` de `history.RemainingShips`, pour chaque origine et chaque
orientation, le placement est **retenu** s'il satisfait toutes les conditions suivantes :

- toutes ses cases sont dans la grille ;
- aucune de ses cases n'est une case tirée et **manquée** ;
- aucune de ses cases n'appartient à un navire de `history.SunkShips` ;
- si `!history.ShipsMayTouch`, aucune de ses cases n'est dans la couronne de Moore d'un navire
  coulé.

Pour chaque placement retenu, incrémenter le compteur de chacune de ses cases. **Pondération** :
si le placement couvre une case touchée mais non coulée, multiplier sa contribution par un
facteur élevé (10 suffit) — c'est ce qui fait converger la stratégie sur un navire entamé.

Ne conserver ensuite que les cases non encore jouées et renvoyer celle de score maximal ; en
cas d'égalité, départager avec `random`. Si aucune case n'a de score strictement positif, se
rabattre sur une case non jouée tirée au hasard.

- [ ] **Étape 4 : Ajouter la stratégie à l'invariant partagé**

```csharp
{ "Density", r => new DensityStrategy(r) },
```

- [ ] **Étape 5 : Lancer tous les tests d'adversaire**

Exécuter : `dotnet test --filter Adversaire`
Attendu : tout au vert, y compris les 3 cas d'invariant.

- [ ] **Étape 6 : Commit**

```bash
dotnet format
git add -A
git commit -m "feat: ajoute l'adversaire à densité probabiliste"
```

---

### Tâche 11 : Duel d'IA — mesurer, et clore la revue 3

> Cette tâche **clôt `REVUE-IA.md`, revue 3**, dont le résultat attendu a été énoncé le
> 2026-09-15 avant toute implémentation : ordre `Random > HuntTarget > Density`, et `Density`
> **sous 55 coups**.

**Fichiers**
- Créer : `BattleShip.API/Benchmark/StrategyBenchmark.cs`
- Tests : `BattleShip.Tests/Adversaire/StrategyBenchmarkTests.cs`
- Modifier : `REVUE-IA.md`, `docs/adr/0003-strategie-adversaire.md`, `README.md`

**Interfaces**
- Produit :
  - `record BenchmarkInput(int Games, int Seed)`
  - `record BenchmarkResult(string StrategyName, int Games, double AverageShots, int MinShots, int MaxShots)`
  - `static IReadOnlyList<BenchmarkResult> StrategyBenchmark.Run(GameRules rules, int games, int seed)`

- [ ] **Étape 1 : Écrire le test de mesure**

`BattleShip.Tests/Adversaire/StrategyBenchmarkTests.cs` :

```csharp
using BattleShip.API.Benchmark;
using BattleShip.Models;

namespace BattleShip.Tests.Adversaire;

public sealed class StrategyBenchmarkTests
{
    [Fact]
    public void Les_trois_niveaux_sont_ordonnes_par_efficacite()
    {
        var results = StrategyBenchmark.Run(GameRules.Default, games: 200, seed: 20260915)
            .ToDictionary(r => r.StrategyName);

        var aleatoire = results["Random"].AverageShots;
        var chasse = results["HuntTarget"].AverageShots;
        var densite = results["Density"].AverageShots;

        Assert.True(aleatoire > chasse,
            $"aléatoire {aleatoire:F1} devrait être pire que chasse/cible {chasse:F1}");
        Assert.True(chasse > densite,
            $"chasse/cible {chasse:F1} devrait être pire que densité {densite:F1}");
        Assert.True(densite < 55,
            $"densité mesurée à {densite:F1} coups, attendue sous 55");
    }

    [Fact]
    public void La_mesure_est_reproductible_a_graine_egale()
    {
        var a = StrategyBenchmark.Run(GameRules.Default, 50, 1);
        var b = StrategyBenchmark.Run(GameRules.Default, 50, 1);

        Assert.Equal(a.Select(r => r.AverageShots), b.Select(r => r.AverageShots));
    }
}
```

- [ ] **Étape 2 : Lancer le test et constater l'échec**

Exécuter : `dotnet test --filter StrategyBenchmark`
Attendu : ÉCHEC de compilation.

- [ ] **Étape 3 : Implémenter**

`StrategyBenchmark.Run` : pour chaque stratégie, jouer `games` parties. Chaque partie place
une flotte avec `new FleetPlacer(new Random(seed + i))` et fait tirer la stratégie jusqu'à
`AllSunk`, en comptant les coups. Construire `ShotHistory` à chaque tour comme dans le test
d'invariant (tâche 8).

- [ ] **Étape 4 : Lancer le test et relever les valeurs**

Exécuter : `dotnet test --filter StrategyBenchmark --logger "console;verbosity=detailed"`

**Noter les trois moyennes réellement observées.** Deux cas :

- **Attendu confirmé** (ordre respecté, densité < 55) : reporter les chiffres.
- **Attendu infirmé** : ne pas ajuster le seuil pour faire passer le test. Une stratégie est
  fautive — reprendre la tâche 9 ou 10, et consigner l'écart dans `REVUE-IA.md`. C'est
  précisément ce que cette revue devait détecter.

- [ ] **Étape 5 : Exposer l'endpoint HTTP**

Dans `Program.cs` :

```csharp
app.MapPost("/benchmark", (BenchmarkInput input) =>
    TypedResults.Ok(StrategyBenchmark.Run(GameRules.Default, input.Games, input.Seed)));
```

Avec un validateur FluentValidation : `Games` entre 1 et 1000, `Seed` quelconque.

- [ ] **Étape 6 : Clore la revue 3**

Dans `REVUE-IA.md` : retirer la mention *(ouverte)* du titre de la revue 3, renseigner
« Résultat réellement observé » avec les trois moyennes et la commande exacte, et compléter
« Décision et justification ». Mettre à jour le § « Nombre moyen de coups » de
`docs/adr/0003-strategie-adversaire.md` et la section « Limites connues » du `README.md` :
les chiffres cessent d'être une hypothèse.

- [ ] **Étape 7 : Commit**

```bash
dotnet format
git add -A
git commit -m "feat: mesure les trois niveaux d'adversaire et clôt la revue 3"
```

---

# Phase 3 — L'API

### Tâche 12 : DTO et règle du secret

**Fichiers**
- Créer : `BattleShip.API/Contracts/GameDto.cs`, `BattleShip.API/Contracts/DtoMappings.cs`
- Tests : `BattleShip.Tests/Api/SecretTests.cs`

**Interfaces**
- Produit :
  - `record CellDto(int X, int Y, string State)` — `State` ∈ `"miss"`, `"hit"`, `"sunk"`
  - `record OpponentBoardDto(int GridSize, IReadOnlyList<CellDto> Shots, IReadOnlyList<SunkShipDto> SunkShips)`
  - `record OwnBoardDto(int GridSize, IReadOnlyList<ShipDto> Ships, IReadOnlyList<CellDto> ReceivedShots)`
  - `record SunkShipDto(string Name, IReadOnlyList<CellDto> Cells)`
  - `record ShipDto(string Name, int Size, IReadOnlyList<CellDto> Cells, bool IsSunk)`
  - `record ShotDto(int X, int Y, string Result, string By, string? SunkShipName)` — utilisé
    par l'endpoint d'historique (tâche 20)
  - `record GameDto(Guid Id, string Status, string CurrentPlayer, OwnBoardDto Own, OpponentBoardDto Opponent)`
  - `static GameDto DtoMappings.ToDto(this Game game)`
  - `static IReadOnlyList<ShotDto> DtoMappings.ToDto(this IReadOnlyList<ShotRecord> history)`

- [ ] **Étape 1 : Écrire le test de fuite**

`BattleShip.Tests/Api/SecretTests.cs` :

```csharp
using System.Text.Json;
using BattleShip.API.Contracts;
using BattleShip.Models;

namespace BattleShip.Tests.Api;

public sealed class SecretTests
{
    [Fact]
    public void Le_dto_ne_revele_aucune_case_adverse_non_decouverte()
    {
        var rules = GameRules.Default;
        var flotteJoueur = new FleetPlacer(new Random(1)).PlaceAll(rules).Value;
        var flotteAdverse = new FleetPlacer(new Random(2)).PlaceAll(rules).Value;
        var game = Game.Start(Guid.NewGuid(), rules, flotteJoueur, flotteAdverse);

        game.PlayerFires(new Coordinate(0, 0));

        var json = JsonSerializer.Serialize(game.ToDto());

        var revelees = game.OpponentBoard.ReceivedShots;
        var secretes = flotteAdverse
            .SelectMany(s => s.Cells)
            .Where(c => !revelees.Contains(c))
            .ToList();

        Assert.NotEmpty(secretes);   // sinon le test ne prouverait rien

        foreach (var c in secretes)
            Assert.DoesNotContain($"\"x\":{c.X},\"y\":{c.Y}",
                json, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Le_dto_expose_la_flotte_du_joueur()
    {
        var rules = GameRules.Default;
        var flotteJoueur = new FleetPlacer(new Random(1)).PlaceAll(rules).Value;
        var game = Game.Start(Guid.NewGuid(), rules, flotteJoueur,
            new FleetPlacer(new Random(2)).PlaceAll(rules).Value);

        var dto = game.ToDto();

        Assert.Equal(rules.Fleet.Count, dto.Own.Ships.Count);
    }
}
```

> L'assertion `Assert.NotEmpty(secretes)` est là pour que le test ne puisse pas passer
> **vide** : sans elle, une boucle sur une liste vide prouverait la règle sans rien vérifier.

- [ ] **Étape 2 : Lancer les tests et constater l'échec**

Exécuter : `dotnet test --filter SecretTests`
Attendu : ÉCHEC de compilation.

- [ ] **Étape 3 : Implémenter**

Écrire les DTO et `ToDto()`. Le plateau **adverse** ne projette que `ReceivedShots` et les
navires coulés ; il ne parcourt **jamais** `Ships` pour les navires non coulés. Le plateau du
joueur projette sa flotte entière — c'est la sienne.

- [ ] **Étape 4 : Lancer les tests et constater le succès**

Exécuter : `dotnet test --filter SecretTests`
Attendu : 2 tests au vert.

**Contrôle supplémentaire** : modifier temporairement `ToDto()` pour sérialiser
`OpponentBoard.Ships`, relancer, vérifier que le premier test **échoue**, puis annuler la
modification. Un test du secret qui ne sait pas détecter une fuite ne sert à rien.

- [ ] **Étape 5 : Commit**

```bash
dotnet format
git add -A
git commit -m "feat: ajoute les DTO et fait respecter le secret du jeu"
```

---

### Tâche 13 : Endpoints HTTP et FluentValidation

**Fichiers**
- Créer : `BattleShip.API/Endpoints/GameEndpoints.cs`,
  `BattleShip.API/Validation/CreateGameInputValidator.cs`,
  `BattleShip.API/Validation/PlacementInputValidator.cs`,
  `BattleShip.API/Contracts/Inputs.cs`
- Modifier : `BattleShip.API/Program.cs`, `api.http`
- Tests : `BattleShip.Tests/Api/HttpEndpointsTests.cs`

**Interfaces**
- Consomme : `IGameStore`, `GameRules`, `FleetPlacer`, `PlacementRules`, `ToDto` (tâches 4 à 12).
- Produit :
  - `record CreateGameInput(int GridSize, string Difficulty)`
  - `record PlacementInput(IReadOnlyList<ShipPlacementInput> Ships)`
  - `record ShipPlacementInput(string Name, int X, int Y, string Orientation)`
  - `interface IOpponentStrategyFactory { IOpponentStrategy ForDifficulty(string difficulty); }`
    et son implémentation `OpponentStrategyFactory(Random random)`, qui mappe
    `"Facile"` → `RandomStrategy`, `"Normal"` → `HuntTargetStrategy`,
    `"Difficile"` → `DensityStrategy`, et lève `ArgumentOutOfRangeException` sinon
    (le validateur a déjà écarté les valeurs inconnues : arriver ici est une anomalie)
  - Routes : `POST /games`, `GET /games/{id:guid}`, `POST /games/{id:guid}/placement`,
    `GET /games/{id:guid}/history`

- [ ] **Étape 1 : Écrire les tests d'intégration**

`BattleShip.Tests/Api/HttpEndpointsTests.cs` :

```csharp
using System.Net;
using System.Net.Http.Json;
using BattleShip.API.Contracts;
using Microsoft.AspNetCore.Mvc.Testing;

namespace BattleShip.Tests.Api;

public sealed class HttpEndpointsTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient _client;

    public HttpEndpointsTests(WebApplicationFactory<Program> factory)
        => _client = factory.CreateClient();

    [Fact]
    public async Task Creer_une_partie_renvoie_201_et_son_identifiant()
    {
        var response = await _client.PostAsJsonAsync("/games",
            new CreateGameInput(10, "Normal"));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var dto = await response.Content.ReadFromJsonAsync<GameDto>();
        Assert.NotEqual(Guid.Empty, dto!.Id);
    }

    [Fact]
    public async Task Une_partie_inconnue_renvoie_404()
    {
        var response = await _client.GetAsync($"/games/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Une_grille_hors_bornes_est_refusee_en_400()
    {
        var response = await _client.PostAsJsonAsync("/games",
            new CreateGameInput(2, "Normal"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Un_niveau_de_difficulte_inconnu_est_refuse_en_400()
    {
        var response = await _client.PostAsJsonAsync("/games",
            new CreateGameInput(10, "Impossible"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Un_placement_avec_navires_adjacents_est_refuse_en_400()
    {
        var create = await _client.PostAsJsonAsync("/games", new CreateGameInput(10, "Normal"));
        var game = await create.Content.ReadFromJsonAsync<GameDto>();

        // Porte-avions en (0,0) horizontal, Croiseur collé juste en dessous.
        var placement = new PlacementInput(
        [
            new ShipPlacementInput("Porte-avions", 0, 0, "Horizontal"),
            new ShipPlacementInput("Croiseur", 0, 1, "Horizontal"),
            new ShipPlacementInput("Contre-torpilleur", 0, 5, "Horizontal"),
            new ShipPlacementInput("Sous-marin", 0, 7, "Horizontal"),
            new ShipPlacementInput("Torpilleur", 0, 9, "Horizontal")
        ]);

        var response = await _client.PostAsJsonAsync(
            $"/games/{game!.Id}/placement", placement);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("adjac", body, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Un_placement_valide_est_accepte_en_204()
    {
        var create = await _client.PostAsJsonAsync("/games", new CreateGameInput(10, "Normal"));
        var game = await create.Content.ReadFromJsonAsync<GameDto>();

        var placement = new PlacementInput(
        [
            new ShipPlacementInput("Porte-avions", 0, 0, "Horizontal"),
            new ShipPlacementInput("Croiseur", 0, 2, "Horizontal"),
            new ShipPlacementInput("Contre-torpilleur", 0, 4, "Horizontal"),
            new ShipPlacementInput("Sous-marin", 0, 6, "Horizontal"),
            new ShipPlacementInput("Torpilleur", 0, 8, "Horizontal")
        ]);

        var response = await _client.PostAsJsonAsync(
            $"/games/{game!.Id}/placement", placement);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }
}
```

- [ ] **Étape 2 : Lancer les tests et constater l'échec**

Exécuter : `dotnet test --filter HttpEndpoints`
Attendu : ÉCHEC — les routes n'existent pas.

- [ ] **Étape 3 : Implémenter**

Enregistrer dans `Program.cs` : `AddSingleton<IGameStore, InMemoryGameStore>()`,
`AddSingleton(_ => Random.Shared)`, les validateurs en `Scoped`, `AddOpenApi()`,
et les trois `IOpponentStrategy` derrière une fabrique indexée par le nom de difficulté.

Chaque endpoint appelle **explicitement** son validateur :

```csharp
app.MapPost("/games", async Task<IResult> (
    CreateGameInput input, IValidator<CreateGameInput> validator,
    IGameStore store, Random random) =>
{
    var check = await validator.ValidateAsync(input);
    if (!check.IsValid) return TypedResults.ValidationProblem(check.ToDictionary());
    // ...
    return TypedResults.Created($"/games/{game.Id}", game.ToDto());
});
```

`CreateGameInputValidator` : `GridSize` entre 5 et 20, `Difficulty` dans
`{ "Facile", "Normal", "Difficile" }`. `PlacementInputValidator` : un navire par
`ShipTemplate` de la flotte, `Orientation` analysable, coordonnées dans la grille ; la règle
d'adjacence vient de `PlacementRules.Validate`, **jamais réécrite dans le validateur**. Le
message d'erreur mentionne explicitement le mot « adjacents » (le test le vérifie).

Mettre à jour `api.http` avec un appel par route, en notant que **le tir n'y figure pas**
(ADR 0005).

- [ ] **Étape 4 : Lancer les tests et constater le succès**

Exécuter : `dotnet test --filter HttpEndpoints`
Attendu : 6 tests au vert.

- [ ] **Étape 5 : Commit**

```bash
dotnet format
git add -A
git commit -m "feat: ajoute les endpoints HTTP et leur validation"
```

---

### Tâche 14 : Le tir en gRPC-Web

**Fichiers**
- Créer : `BattleShip.API/Protos/battle.proto`,
  `BattleShip.API/Services/BattleGrpcService.cs`,
  `BattleShip.API/Validation/FireRequestValidator.cs`,
  `BattleShip.API/Endpoints/ErrorMapping.cs`
- Supprimer : `BattleShip.API/Protos/ping.proto`, `BattleShip.API/Services/PingService.cs`,
  `BattleShip.Tests/Integration/GrpcHarnessTests.cs`
- Tests : `BattleShip.Tests/Api/FireGrpcTests.cs`
- Modifier : `BattleShip.API/Program.cs`

**Interfaces**
- Consomme : le montage de test validé en tâche 2, `IGameStore.Mutate`, `Game.PlayerFires`,
  les stratégies, `ToDto`.
- Produit : `BattleService.Fire(FireRequest) → FireResponse` portant le coup du joueur, la
  chaîne des coups adverses et l'état résultant.

- [ ] **Étape 1 : Écrire le contrat**

`BattleShip.API/Protos/battle.proto` :

```proto
syntax = "proto3";
option csharp_namespace = "BattleShip.API.Grpc";
package battleship;

service BattleService {
  rpc Fire (FireRequest) returns (FireResponse);
}

message FireRequest {
  string game_id = 1;
  int32 x = 2;
  int32 y = 3;
}

message Shot {
  int32 x = 1;
  int32 y = 2;
  string result = 3;          // miss | hit | sunk
  string sunk_ship_name = 4;
}

message FireResponse {
  Shot player_shot = 1;
  repeated Shot opponent_shots = 2;
  string status = 3;          // InProgress | Finished
  string current_player = 4;
}
```

- [ ] **Étape 2 : Écrire les tests d'intégration**

`BattleShip.Tests/Api/FireGrpcTests.cs` :

```csharp
using System.Net.Http.Json;
using BattleShip.API.Contracts;
using BattleShip.API.Grpc;
using Grpc.Core;
using Grpc.Net.Client;
using Grpc.Net.Client.Web;
using Microsoft.AspNetCore.Mvc.Testing;

namespace BattleShip.Tests.Api;

public sealed class FireGrpcTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public FireGrpcTests(WebApplicationFactory<Program> factory) => _factory = factory;

    private BattleService.BattleServiceClient Client()
    {
        var handler = new GrpcWebHandler(_factory.Server.CreateHandler());
        return new BattleService.BattleServiceClient(
            GrpcChannel.ForAddress(_factory.Server.BaseAddress,
                new GrpcChannelOptions { HttpHandler = handler }));
    }

    private async Task<Guid> PartiePrete()
    {
        var http = _factory.CreateClient();
        var create = await http.PostAsJsonAsync("/games", new CreateGameInput(10, "Facile"));
        var game = await create.Content.ReadFromJsonAsync<GameDto>();

        var placement = new PlacementInput(
        [
            new ShipPlacementInput("Porte-avions", 0, 0, "Horizontal"),
            new ShipPlacementInput("Croiseur", 0, 2, "Horizontal"),
            new ShipPlacementInput("Contre-torpilleur", 0, 4, "Horizontal"),
            new ShipPlacementInput("Sous-marin", 0, 6, "Horizontal"),
            new ShipPlacementInput("Torpilleur", 0, 8, "Horizontal")
        ]);
        await http.PostAsJsonAsync($"/games/{game!.Id}/placement", placement);
        return game.Id;
    }

    [Fact]
    public async Task Un_tir_valide_renvoie_un_resultat()
    {
        var id = await PartiePrete();

        var reply = await Client().FireAsync(new FireRequest
        {
            GameId = id.ToString(), X = 5, Y = 5
        });

        Assert.Contains(reply.PlayerShot.Result, new[] { "miss", "hit", "sunk" });
    }

    [Fact]
    public async Task Rejouer_la_meme_case_renvoie_InvalidArgument()
    {
        var id = await PartiePrete();
        var client = Client();
        var premier = await client.FireAsync(new FireRequest
        {
            GameId = id.ToString(), X = 5, Y = 5
        });

        // Si le premier coup a touché, le joueur rejoue : la case reste refusée.
        var ex = await Assert.ThrowsAsync<RpcException>(() =>
            client.FireAsync(new FireRequest
            {
                GameId = id.ToString(), X = 5, Y = 5
            }).ResponseAsync);

        Assert.Equal(StatusCode.InvalidArgument, ex.StatusCode);
        _ = premier;
    }

    [Fact]
    public async Task Un_tir_hors_grille_renvoie_InvalidArgument()
    {
        var id = await PartiePrete();

        var ex = await Assert.ThrowsAsync<RpcException>(() =>
            Client().FireAsync(new FireRequest
            {
                GameId = id.ToString(), X = 99, Y = 0
            }).ResponseAsync);

        Assert.Equal(StatusCode.InvalidArgument, ex.StatusCode);
    }

    [Fact]
    public async Task Un_tir_sur_une_partie_inconnue_renvoie_NotFound()
    {
        var ex = await Assert.ThrowsAsync<RpcException>(() =>
            Client().FireAsync(new FireRequest
            {
                GameId = Guid.NewGuid().ToString(), X = 0, Y = 0
            }).ResponseAsync);

        Assert.Equal(StatusCode.NotFound, ex.StatusCode);
    }

    [Fact]
    public async Task Un_coup_manque_declenche_la_riposte_de_l_adversaire()
    {
        var id = await PartiePrete();
        var client = Client();

        FireResponse reply;
        var x = 5;
        do
        {
            reply = await client.FireAsync(new FireRequest
            {
                GameId = id.ToString(), X = x++, Y = 5
            });
        } while (reply.PlayerShot.Result != "miss" && x < 10);

        Assert.NotEmpty(reply.OpponentShots);
    }
}
```

- [ ] **Étape 3 : Lancer les tests et constater l'échec**

Exécuter : `dotnet test --filter FireGrpc`
Attendu : ÉCHEC — `BattleService` n'existe pas.

- [ ] **Étape 4 : Implémenter**

`ErrorMapping` traduit `GameError` vers `StatusCode` gRPC **en un seul endroit**, selon la
table de l'ADR 0004.

`BattleGrpcService(IGameStore store, IValidator<FireRequest> validator, IOpponentStrategyFactory strategies)` :

1. valider la requête ; si invalide,
   `throw new RpcException(new Status(StatusCode.InvalidArgument, …))` ;
2. `store.Mutate(gameId, game => …)` ; dans la lambda :
   - `game.PlayerFires(coordinate)` ; si échec, remonter le `GameError` ;
   - si le coup est manqué et la partie en cours, faire jouer l'adversaire **tant qu'il
     touche**, en construisant `ShotHistory` à chaque coup ;
3. traduire un éventuel `GameError` via `ErrorMapping`, sinon composer la `FireResponse`.

Câbler dans `Program.cs` : `app.MapGrpcService<BattleGrpcService>().EnableGrpcWeb();`

Déclarer le contrat dans `BattleShip.API.csproj` avec **`GrpcServices="Both"`** (même raison
qu'en tâche 2 : `BattleShip.Tests` a besoin du client et l'obtient par la référence de
projet). `BattleShip.App`, qui ne référence pas `BattleShip.API`, garde sa propre entrée
`GrpcServices="Client"` (tâche 18) — aucun conflit de types entre les deux assemblages.

- [ ] **Étape 5 : Supprimer le spike**

```bash
rm BattleShip.API/Protos/ping.proto BattleShip.API/Services/PingService.cs
rm BattleShip.Tests/Integration/GrpcHarnessTests.cs
```

Retirer l'entrée `<Protobuf Include="Protos\ping.proto" …/>` et le `MapGrpcService<PingGrpcService>()`.

- [ ] **Étape 6 : Lancer toute la suite**

Exécuter : `dotnet test`
Attendu : tout au vert, y compris les 5 tests gRPC.

- [ ] **Étape 7 : Commit**

```bash
dotnet format
git add -A
git commit -m "feat: ajoute le tir en gRPC-Web et la traduction des erreurs"
```

---

### Tâche 15 : CORS et OpenAPI

**Fichiers**
- Modifier : `BattleShip.API/Program.cs`, `BattleShip.API/appsettings.Development.json`

- [ ] **Étape 1 : Déclarer la politique CORS**

Origines autorisées lues depuis la configuration (`https://localhost:7073` et
`http://localhost:5210`), méthodes et en-têtes déclarés explicitement, et **exposition des
en-têtes gRPC-Web** — sans quoi le client ne peut pas lire le statut d'erreur :

```csharp
builder.Services.AddCors(options => options.AddPolicy("front", policy => policy
    .WithOrigins(builder.Configuration.GetSection("Cors:Origins").Get<string[]>() ?? [])
    .AllowAnyMethod()
    .AllowAnyHeader()
    .WithExposedHeaders("grpc-status", "grpc-message", "grpc-encoding",
                        "grpc-accept-encoding")));
```

Ordre dans le pipeline : `app.UseCors("front");` **avant** `app.UseGrpcWeb();`.

- [ ] **Étape 2 : Activer OpenAPI en développement**

```csharp
builder.Services.AddOpenApi();
// ...
if (app.Environment.IsDevelopment()) app.MapOpenApi();
```

- [ ] **Étape 3 : Vérifier depuis le navigateur**

Lancer l'API et le front dans deux terminaux, ouvrir <https://localhost:7073>, puis la console
F12 sur l'onglet **Réseau**.
Attendu : aucune erreur CORS ; la requête préliminaire `OPTIONS` répond `204`.

- [ ] **Étape 4 : Commit**

```bash
dotnet format
git add -A
git commit -m "feat: configure CORS pour le front et gRPC-Web"
```

---

# Phase 4 — Le front

### Tâche 16 : `GameState` et page de création

**Fichiers**
- Créer : `BattleShip.App/Services/GameState.cs`, `BattleShip.App/Services/BattleApiClient.cs`,
  `BattleShip.App/Pages/NouvellePartie.razor`
- Modifier : `BattleShip.App/Program.cs`, `BattleShip.App/Layout/NavMenu.razor`

**Interfaces**
- Produit :
  - `GameState` avec `GameDto? Current`, `LoadState State` (`Idle`, `Loading`, `Ready`, `Failed`),
    `string? ErrorMessage`, `event Action? OnChange`, et les méthodes
    `Task CreateAsync(int gridSize, string difficulty)`, `Task RefreshAsync()`
  - `BattleApiClient` encapsulant les appels HTTP

- [ ] **Étape 1 : Écrire le service et le client**

`GameState` notifie via `OnChange` à chaque transition. Les trois états — chargement, succès,
échec — sont représentés explicitement par `LoadState` : une coupure de l'API doit produire
`Failed` avec un message, jamais une exception non rattrapée.

`GameState` ne contient **aucune règle du jeu** : il ne fait que refléter ce que le serveur a
renvoyé (ADR 0007).

- [ ] **Étape 2 : Enregistrer dans `Program.cs`**

```csharp
builder.Services.AddScoped(_ => new HttpClient
{
    BaseAddress = new Uri(builder.Configuration["ApiBaseAddress"] ?? "https://localhost:7050")
});
builder.Services.AddSingleton<GameState>();
builder.Services.AddScoped<BattleApiClient>();
```

- [ ] **Étape 3 : Écrire la page**

`NouvellePartie.razor` en `@page "/"` : choix de la taille de grille et du niveau de
difficulté, bouton de création, puis redirection vers `/placement`. Les trois états sont
rendus — un indicateur pendant le chargement, un message d'erreur exploitable en cas d'échec.

- [ ] **Étape 4 : Vérifier dans le navigateur**

Lancer l'API et le front, créer une partie.
Attendu : redirection vers `/placement`, et l'onglet Réseau montre un `POST /games` en `201`.
Vérifier aussi qu'avec l'API **arrêtée**, la page affiche un message d'échec et reste
utilisable.

- [ ] **Étape 5 : Commit**

```bash
dotnet format
git add -A
git commit -m "feat: ajoute l'état partagé et la page de création de partie"
```

---

### Tâche 17 : Page de placement manuel

**Fichiers**
- Créer : `BattleShip.App/Pages/Placement.razor`,
  `BattleShip.App/Components/GrilleDePlacement.razor`
- Modifier : `BattleShip.App/Services/GameState.cs`

- [ ] **Étape 1 : Écrire le composant de grille**

`GrilleDePlacement.razor` reçoit en paramètres la taille de grille, la liste des navires à
poser et les placements déjà effectués. Il émet un `EventCallback<Coordinate>` au clic et un
`EventCallback` de rotation.

- [ ] **Étape 2 : Écrire la page**

`Placement.razor` en `@page "/placement"` : liste des navires restants, navire sélectionné,
orientation courante (bascule par bouton ou touche `R`), prévisualisation de la position
survolée en vert si elle semble valide, en rouge sinon.

**La prévisualisation est un confort, pas une garantie** : la règle fait foi côté serveur
(ADR 0007). Le bouton « Valider la flotte » envoie `POST /games/{id}/placement`, et un `400`
est affiché tel quel, avec le motif renvoyé par le serveur.

- [ ] **Étape 3 : S'abonner et se désabonner**

Le composant implémente `IDisposable` et se désabonne de `GameState.OnChange` dans `Dispose`.
Oublier ce désabonnement provoque une fuite et des rendus sur des composants détruits.

- [ ] **Étape 4 : Vérifier dans le navigateur**

Poser une flotte valide → redirection vers `/jeu`.
Poser deux navires **collés** et valider → le serveur répond `400`, la page affiche le motif
contenant « adjacents ». C'est la démonstration de la validation serveur.
Faire plusieurs allers-retours entre les pages et vérifier dans la console F12 qu'aucun
avertissement de rendu sur composant détruit n'apparaît.

- [ ] **Étape 5 : Commit**

```bash
dotnet format
git add -A
git commit -m "feat: ajoute la page de placement manuel de la flotte"
```

---

### Tâche 18 : Page de jeu et client gRPC-Web

**Fichiers**
- Créer : `BattleShip.App/Pages/Jeu.razor`, `BattleShip.App/Components/GrilleDeTir.razor`,
  `BattleShip.App/Services/BattleGrpcClient.cs`
- Modifier : `BattleShip.App/BattleShip.App.csproj`, `BattleShip.App/Program.cs`

- [ ] **Étape 1 : Référencer le contrat proto côté client**

```xml
<ItemGroup>
  <Protobuf Include="..\BattleShip.API\Protos\battle.proto"
            GrpcServices="Client" Link="Protos\battle.proto" />
</ItemGroup>
```

- [ ] **Étape 2 : Écrire le client gRPC-Web**

```csharp
var handler = new GrpcWebHandler(GrpcWebMode.GrpcWeb, new HttpClientHandler());
var channel = GrpcChannel.ForAddress(apiBaseAddress,
    new GrpcChannelOptions { HttpHandler = handler });
```

`BattleGrpcClient.FireAsync(Guid gameId, Coordinate at)` rattrape `RpcException` et la traduit
en message affichable, en distinguant `InvalidArgument`, `FailedPrecondition` et `NotFound`.

- [ ] **Étape 3 : Écrire la page**

`Jeu.razor` en `@page "/jeu"` : deux grilles côte à côte — la flotte du joueur avec les tirs
reçus, la grille adverse avec les tirs émis. Un clic sur une case adverse déclenche `Fire`.

La réponse porte une **séquence** : afficher le coup du joueur, puis dérouler les coups de
l'adversaire (une courte temporisation entre chacun les rend lisibles). Quand `status` vaut
`Finished`, afficher le résultat et un bouton « Nouvelle partie ».

- [ ] **Étape 4 : Vérifier dans le navigateur**

Jouer une partie complète jusqu'à la victoire ou la défaite, puis en créer une autre.
Attendu : le parcours complet fonctionne, et l'onglet Réseau montre les appels
`BattleService/Fire`.

- [ ] **Étape 5 : Commit**

```bash
dotnet format
git add -A
git commit -m "feat: ajoute la page de jeu et le client gRPC-Web"
```

---

### Tâche 19 : Démonstration navigateur et mise à jour des livrables

> Cette tâche produit la **preuve** exigée par le sujet : une réponse et une erreur gRPC-Web
> démontrables depuis le navigateur.

**Fichiers**
- Créer : `docs/demo/` (captures)
- Modifier : `README.md`, `PROMPTS.md`, `REVUE-IA.md`

- [ ] **Étape 1 : Dérouler le scénario du README**

Suivre **à la lettre** la section « Démonstration gRPC-Web » du `README.md`, console F12
ouverte sur l'onglet Réseau :

1. créer une partie, placer la flotte ;
2. tirer sur une case vierge → l'appel `BattleService/Fire` répond ;
3. retirer sur la **même case** → `InvalidArgument` / `CellAlreadyShot` ;
4. forcer un `Fire` sur un identifiant inexistant → `NotFound`.

- [ ] **Étape 2 : Capturer les preuves**

Enregistrer trois captures dans `docs/demo/` : la réponse réussie, l'erreur
`InvalidArgument`, l'erreur `NotFound`. Les référencer depuis le `README.md`.

- [ ] **Étape 3 : Corriger le README si le scénario ne se déroule pas tel quel**

Le critère est qu'un autre binôme puisse lancer et démontrer le projet **en suivant
uniquement le README**. Si une étape manque, c'est le README qu'il faut corriger.

- [ ] **Étape 4 : Écrire la revue IA manquante**

`REVUE-IA.md` doit compter **trois revues closes**. La revue 1 est close, la revue 3 l'est
depuis la tâche 11. Clore la revue 2 en mesurant réellement le nombre de refus métier émis
pendant les tours de l'adversaire, comme décrit dans son scénario. Ajouter une entrée
`PROMPTS.md` pour toute contribution significative de l'IA pendant l'implémentation.

- [ ] **Étape 5 : Mettre à jour le README**

Renseigner le second membre du binôme, retirer l'encart « implémentation non commencée »,
et déplacer « Fonctionnalités visées » en « Fonctionnalités livrées » en ne conservant que ce
qui est réellement livré.

- [ ] **Étape 6 : Commit**

```bash
git add -A
git commit -m "docs: ajoute les preuves de démonstration gRPC-Web"
```

---

# Phase 5 — Extensions du backlog

> À attaquer dans cet ordre, et seulement si les phases 0 à 4 sont closes et vertes.

### Tâche 20 : Historique des coups et rejeu

**Fichiers**
- Créer : `BattleShip.App/Components/PanneauHistorique.razor`
- Modifier : `BattleShip.App/Pages/Jeu.razor`

> La route `GET /games/{id:guid}/history` est **déjà implémentée par la tâche 13**. Cette
> tâche n'ajoute que son test et le panneau d'interface.
- Tests : `BattleShip.Tests/Api/HistoryEndpointTests.cs`

**Interfaces**
- Consomme : `ShotDto` et `DtoMappings.ToDto(IReadOnlyList<ShotRecord>)` (tâche 12), le
  montage `WebApplicationFactory<Program>` et les aides `Client()` / `PartiePrete()` de
  `FireGrpcTests` (tâche 14) — les recopier dans la nouvelle classe de test, qui est
  autonome.
- Produit : `GET /games/{id:guid}/history` → `200` avec la liste ordonnée, `404` si la partie
  est inconnue.

- [ ] **Étape 1 : Écrire le test**

```csharp
[Fact]
public async Task L_historique_reflete_les_coups_joues_dans_l_ordre()
{
    var id = await PartiePrete();
    await Client().FireAsync(new FireRequest { GameId = id.ToString(), X = 5, Y = 5 });

    var history = await _factory.CreateClient()
        .GetFromJsonAsync<List<ShotDto>>($"/games/{id}/history");

    Assert.NotEmpty(history);
    Assert.Equal(5, history![0].X);
    Assert.Equal(5, history[0].Y);
}
```

- [ ] **Étape 2 : Lancer, implémenter, relancer**

Exécuter : `dotnet test --filter HistoryEndpoint` → ÉCHEC, puis implémenter l'endpoint et le
panneau, puis SUCCÈS.

- [ ] **Étape 3 : Ajouter le rejeu**

Un curseur dans le panneau rejoue les coups jusqu'à l'index choisi, en rendu seul — il ne
modifie jamais l'état serveur.

- [ ] **Étape 4 : Commit**

```bash
dotnet format
git add -A
git commit -m "feat: ajoute l'historique des coups et le rejeu"
```

---

### Tâche 21 : Accessibilité

**Fichiers**
- Modifier : `BattleShip.App/Components/GrilleDeTir.razor`,
  `BattleShip.App/Components/GrilleDePlacement.razor`,
  `BattleShip.App/wwwroot/css/app.css`

- [ ] **Étape 1 : Rendre les grilles navigables au clavier**

Chaque case est un `<button>` avec un `aria-label` explicite
(« case B4, non jouée » / « case B4, touché »). Les flèches déplacent le focus,
`Entrée` tire.

- [ ] **Étape 2 : Annoncer les résultats**

Une région `aria-live="polite"` annonce le résultat de chaque coup, le joueur comme
l'adversaire.

- [ ] **Étape 3 : Vérifier les contrastes**

Contrôler que les états manqué / touché / coulé restent distinguables **sans la couleur**
(motif ou symbole en plus), et que les contrastes atteignent le ratio 4,5:1.

- [ ] **Étape 4 : Vérifier au clavier seul**

Parcourir tout le jeu — création, placement, partie — **sans souris**.
Attendu : chaque action est atteignable et le focus reste visible.

- [ ] **Étape 5 : Commit**

```bash
dotnet format
git add -A
git commit -m "feat: rend les grilles accessibles au clavier et aux lecteurs d'écran"
```

---

## Avant la remise

Reprendre la checklist du § 7 de `CLAUDE.md`, puis :

```bash
dotnet format --verify-no-changes
dotnet build
dotnet test
git log --oneline
```

Envoyer le lien du dépôt et le **hash du commit** poussé à `contact@hts-learning.com` avant le
début du QCM. Seul ce commit est évalué.
