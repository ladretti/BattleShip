# Conformité au sujet — constat du 2026-09-17

Une ligne par exigence du support (numéro de diapo entre parenthèses). La commande se rejoue
telle quelle depuis la racine du dépôt. « Constaté » = exécuté ce jour ; « limite » = écart
assumé, non corrigé.

## Contraintes du socle (diapos 6, 15, 19, 28)

| Exigence | Preuve dans le dépôt | Commande de contrôle | Constat |
|---|---|---|---|
| .NET 10 LTS épinglé (19, 28) | `global.json` | `cat global.json && dotnet --version` | 10.0.100 / latestFeature → SDK 10.0.401 |
| Minimal API, pas de contrôleurs (6) | `BattleShip.API/Endpoints/GameEndpoints.cs` | `grep -rn AddControllers BattleShip.API` | aucun contrôleur |
| Front Blazor WebAssembly (6) | `BattleShip.App.csproj` | `head -1 BattleShip.App/BattleShip.App.csproj` | `Microsoft.NET.Sdk.BlazorWebAssembly` |
| Bibliothèque de modèles sans dépendance (28) | `BattleShip.Models.csproj` | `grep -c Reference BattleShip.Models/BattleShip.Models.csproj` | 0 référence |
| Références API→Models, App→Models, Tests→API (15) | trois `.csproj` | `grep -h ProjectReference */*.csproj` | 4 références : Tests référence API et, en direct, Models (les tests métier compilent contre le domaine) |
| Pages du gabarit purgées (28) | `BattleShip.App/Pages/` | `ls BattleShip.App/Pages` | ni Counter ni Weather |
| Partie complète contre l'ordinateur (5, 63) | `FireGrpcTests.A_game_played_to_the_end_finishes_with_the_player_as_winner` ; `Play.razor` bouton *New game* | `dotnet test --filter A_game_played_to_the_end` | `FireGrpcTests.A_game_played_to_the_end_finishes_with_the_player_as_winner au vert` |
| FluentValidation sur toutes les entrées serveur, HTTP et gRPC (40, 50) | `BattleShip.API/Validation/` (4 validateurs), appel explicite `ValidateAsync` dans chaque route et dans `BattleGrpcService.Fire` | `grep -rn ValidateAsync BattleShip.API` | 4 entrées, 4 appels explicites |
| gRPC-Web : réponse **et** erreur attendue depuis le navigateur (48) | `Protos/battle.proto`, `docs/demo/*.png`, `docs/demo/grpc-web-trace.md` | `ls docs/demo` | 3 captures + trace |
| Tests métier et d'intégration (6, 39) | `BattleShip.Tests/Domain`, `/Api`, `/Opponent` | `dotnet test` | 145 tests, 72 [Fact] et 8 [Theory] |
| Livrables IA (11, 62) | `PROMPTS.md`, `docs/adr/`, `REVUE-IA.md`, `README.md` | `ls` | présents ; condensés le 2026-09-17 |
| Historique Git exploitable (62) | `git log` | `git log --format=%s \| grep -vcE '^(feat\|fix\|docs\|test\|chore\|refactor): '` | 1 écart : le commit initial `init` (limite) |

## Spécifications du jeu (diapos 36, 37, 38, 46)

| Exigence | Preuve dans le dépôt | Commande de contrôle | Constat |
|---|---|---|---|
| Identifier la fin de partie **et le gagnant** (36) | `Game.Winner`, `GameDto.Winner` | `dotnet test --filter The_shooter_who_sinks_the_last_ship_is_the_winner` | **corrigé le 2026-09-17** : le vainqueur était déduit par le front |
| Partie complète de la création à la victoire, aucun coup après la fin (5, 38) | `FireGrpcTests.A_game_played_to_the_end_finishes_with_the_player_as_winner` | `dotnet test --filter A_game_played_to_the_end` | **ajouté le 2026-09-17** ; `FailedPrecondition` sur le tir suivant |

## Spécifications du jeu (diapos 36, 37, 38, 46) — suite

| Exigence | Preuve dans le dépôt | Commande de contrôle | Constat |
|---|---|---|---|
| Placement sans chevauchement ni débordement (36) | `PlacementRulesTests`, `FleetPlacerTests` (graine fixe) | `dotnet test --filter PlacementRules` | 8/8 tests verts |
| Un coup refusé ne modifie pas la partie ; case rejouée non comptée (36) | `GameTests.A_shot_on_an_already_shot_cell_is_rejected` | `dotnet test --filter already_shot` | `Result` en échec, historique inchangé |
| Positions adverses secrètes (36, 37) | `SecretTests` (3 tests), `ShotHistory` sans chemin vers `Board` | `dotnet test --filter SecretTests` | 3/3 ; revue 1 |
| Contrat explicite : créer, connaître l'état, faire évoluer (37) | `POST /games`, `GET /games/{id}`, `POST /games/{id}/placement`, `Fire` gRPC, `GET /games/{id}/history` | `grep -rn MapGet BattleShip.API; grep -rn MapPost BattleShip.API` | 5 opérations, ADR 0005 et 0008 |
| Aucun coup après la fin (38) | `GameTests.A_shot_after_the_end_of_the_game_is_rejected` ; `FireGrpcTests` (tâche 7) | `dotnet test --filter after_the_end` | `GameAlreadyFinished` → `FailedPrecondition` |
| Adversaire soumis aux mêmes règles de validité (38) | `StrategyInvariantTests.A_strategy_never_proposes_an_invalid_shot` (`[Theory]` sur les 3 stratégies) | `dotnet test --filter never_proposes` | 3/3 |
| Interface : créer, deux grilles, jouer, annoncer la fin, rejouer (46) | `NewGame.razor`, `Placement.razor`, `Play.razor` (bouton *New game*) | démonstration README | constaté dans le navigateur le 2026-09-16 |
| Incident de communication pris en charge (46) | `BattleApiClient.IsCommunicationFailure` (4 emplois), `BattleGrpcClient` (`RpcException`) | `grep -rn IsCommunicationFailure BattleShip.App` | 5 lignes (4 emplois + 1 définition) + 1 |

## Bonnes pratiques du référentiel (diapos 4, 24-27, 31-35, 39, 43-45, 49-51)

| Pratique | Preuve | Commande | Constat |
|---|---|---|---|
| Propriétés, pas de getters/setters (4) | tout le domaine | `grep -rnE 'public \w+ Get[A-Z]'` puis `grep -rnE 'public \w+ Set[A-Z]'` | 0 |
| `_camelCase` privé, `PascalCase` public, `Async` sur les `Task` (4, CLAUDE.md § 2) | — | greps de la tâche 8 | 0 écart ; exception : `BattleGrpcService.Fire`, nom imposé par la base générée |
| Pas de `.Result` / `.Wait()` (4) | — | grep | 1 faux positif (`Shot.Result`, pas `Task.Result`) ; 0 réel |
| `Nullable` activé, 0 avertissement (24) | 4 `.csproj` | `dotnet build` | 4/4, 0 warning |
| `sealed` sauf statique (CLAUDE.md § 2) | — | grep | 0 classe ouverte |
| Contraintes de route, `TypedResults`, statuts 201/204/400/404/409 (31, 34) | `GameEndpoints.cs`, `ErrorMapping.cs` | grep | `{id:guid}` ×3 ; 100 % `TypedResults` |
| Traduction des refus en un seul endroit, sans `catch` par type (ADR 0004) | `ErrorMapping.cs` | `grep -rn catch BattleShip.API` | 0 `catch` |
| OpenAPI en développement, `api.http` versionné (35) | `Program.cs`, `api.http` | grep, `git ls-files` | présents |
| Durées de vie DI choisies et justifiées (33) | `Program.cs` ×2, ADR 0002, ADR 0007, revue 5 | grep | Singleton pour l'état partagé, Scoped ailleurs |
| Store partagé concurrent (ADR 0002) | `InMemoryGameStore` : `ConcurrentDictionary` + verrou par partie | `dotnet test --filter InMemoryGameStore` | 8/8 ; revues 3 et 4 |
| Aléa injecté (CLAUDE.md § 3 bis) | `FleetPlacer(Random)`, stratégies | grep `Random.Shared` hors `Program.cs` | 0 |
| `[Fact]` et `[Theory]`, tests de refus (39, 63) | `BattleShip.Tests` | `dotnet test` | 72 Fact, 8 Theory, ≥ 12 tests de refus |
| Trois états Blazor, `HttpClient.BaseAddress`, `System.Net.Http.Json` (43, 44) | `BattleApiClient`, `Program.cs` du front | grep | conformes |
| CORS : origines explicites, en-têtes gRPC-Web exposés (45) | `Program.cs` de l'API, `CorsTests` (5 tests) | `dotnet test --filter CorsTests` | 5/5 ; `WithOrigins` depuis la configuration ; `AllowAnyOrigin` absent |
| Contrat `.proto` avec `csharp_namespace`, validation gRPC, `RpcException` typée (49, 50) | `battle.proto`, `BattleGrpcService`, `ErrorMapping` | grep | conformes |
| Code en anglais, docs en français (CLAUDE.md § 2) | — | grep accents | 0 dans le code |
| `dotnet format` propre (21) | — | `dotnet format --verify-no-changes` | exit 0 |

## Limites assumées

- Le commit initial `init` ne suit pas la convention `type: sujet` ; l'historique n'est pas réécrit.
- `BattleGrpcService.Fire` n'a pas le suffixe `Async` : la signature est celle de `BattleServiceBase` générée par `Grpc.Tools`.
- Aucun lecteur d'écran réel n'a été essayé (README, « Limites connues »).
