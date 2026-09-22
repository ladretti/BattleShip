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
| Partie complète contre l'ordinateur (5, 63) | `FireGrpcTests.A_game_played_to_the_end_finishes_with_the_player_as_winner` ; `Play.razor` bouton *New game* | `dotnet test --filter A_game_played_to_the_end` | `FireGrpcTests.A_game_played_to_the_end_finishes_with_the_player_as_winner` au vert |
| FluentValidation sur toutes les entrées serveur, HTTP et gRPC (40, 50) | `BattleShip.API/Validation/` (5 validateurs, dont `EventQueryValidator`), appel explicite `ValidateAsync` dans chaque route et dans `BattleGrpcService.Fire` | `grep -rn ValidateAsync BattleShip.API` | 5 entrées, 5 appels explicites |
| gRPC-Web : réponse **et** erreur attendue depuis le navigateur (48) | `Protos/battle.proto`, `docs/demo/*.png`, `docs/demo/grpc-web-trace.md` | `ls docs/demo` | 3 captures + trace |
| Tests métier et d'intégration (6, 39) | `BattleShip.Tests/Domain`, `/Api`, `/Opponent` | `dotnet test` | 191 tests, 100 [Fact] et 10 [Theory] (2026-09-22) |
| Livrables IA (11, 62) | `PROMPTS.md`, `docs/adr/`, `REVUE-IA.md`, `README.md` | `ls` | présents ; condensés le 2026-09-17 |
| Historique Git exploitable (62) | `git log` | `git log --format=%s \| grep -vcE '^(feat\|fix\|docs\|test\|chore\|refactor): '` | 1 écart : le commit initial `init` (limite) |

## Spécifications du jeu (diapos 36, 37, 38, 46)

| Exigence | Preuve dans le dépôt | Commande de contrôle | Constat |
|---|---|---|---|
| Identifier la fin de partie **et le gagnant** (36) | `Game.Winner`, `GameDto.Winner` | `dotnet test --filter The_shooter_who_sinks_the_last_ship_is_the_winner` | **corrigé le 2026-09-17** : le vainqueur était déduit par le front |
| Partie complète de la création à la victoire, aucun coup après la fin (5, 38) | `FireGrpcTests.A_game_played_to_the_end_finishes_with_the_player_as_winner` | `dotnet test --filter A_game_played_to_the_end` | **ajouté le 2026-09-17** ; `FailedPrecondition` sur le tir suivant |
| Placement sans chevauchement ni débordement (36) | `PlacementRulesTests`, `FleetPlacerTests` (graine fixe) | `dotnet test --filter PlacementRules` | 8/8 tests verts |
| Un coup refusé ne modifie pas la partie ; case rejouée non comptée (36) | `GameTests.A_shot_on_an_already_shot_cell_is_rejected` | `dotnet test --filter already_shot` | `Result` en échec, historique inchangé |
| Positions adverses secrètes (36, 37) | `SecretTests` (3 tests), `ShotHistory` sans chemin vers `Board` | `dotnet test --filter SecretTests` | 3/3 ; revue 1 |
| Contrat explicite : créer, connaître l'état, faire évoluer (37) | `POST /games`, `GET /games/{id}`, `POST /games/{id}/placement`, `Fire` gRPC, `GET /games/{id}/history` | `grep -rn MapGet BattleShip.API; grep -rn MapPost BattleShip.API` | 5 opérations, ADR 0005 et 0008 |
| Aucun coup après la fin (38) | `GameTests.A_shot_after_the_end_of_the_game_is_rejected` ; `FireGrpcTests.A_game_played_to_the_end_finishes_with_the_player_as_winner` | `dotnet test --filter after_the_end` | `GameAlreadyFinished` → `FailedPrecondition` |
| Adversaire soumis aux mêmes règles de validité (38) | `StrategyInvariantTests.A_strategy_never_proposes_an_invalid_shot` (`[Theory]` sur les 3 stratégies) | `dotnet test --filter never_proposes` | 3/3 |
| Interface : créer, deux grilles, jouer, annoncer la fin, rejouer (46) | `NewGame.razor`, `Placement.razor`, `Play.razor` (bouton *New game*) | démonstration README | constaté dans le navigateur le 2026-09-16 |
| Incident de communication pris en charge (46) | `BattleApiClient.IsCommunicationFailure` (4 emplois), `BattleGrpcClient` (`RpcException`) | `grep -rn IsCommunicationFailure BattleShip.App` | 5 lignes (4 emplois + 1 définition), plus le `catch (RpcException` de `BattleGrpcClient.cs` |

## Bonnes pratiques du référentiel (diapos 4, 24-27, 31-35, 39, 43-45, 49-51)

| Pratique | Preuve | Commande | Constat |
|---|---|---|---|
| Propriétés, pas de getters/setters (4) | tout le domaine | `grep -rnE 'public \w+ (Get\|Set)[A-Z]\w*\(' --include='*.cs' BattleShip.API BattleShip.App BattleShip.Models \| grep -v /obj/ \| wc -l` | 0 |
| `_camelCase` privé, `PascalCase` public, `Async` sur les `Task` (4, CLAUDE.md § 2) | — | `grep -rnE '\bTask(<[^>]*>)?\s+[A-Z]\w*\(' --include='*.cs' --include='*.razor' BattleShip.API BattleShip.App BattleShip.Models \| grep -v /obj/ \| grep -v Async` | 1 ligne : `BattleGrpcService.Fire`, nom imposé par la base générée (limite) |
| Pas de `.Result` / `.Wait()` (4) | — | `grep -rnE '\.Wait\(\)\|GetAwaiter\(\)\.GetResult' --include='*.cs' --include='*.razor' BattleShip.API BattleShip.App BattleShip.Models \| grep -v /obj/ \| wc -l` | 0 ; 0 `Task.Result` : toutes les occurrences de `.Result` sont la propriété `Result` d'un tir |
| `Nullable` activé, 0 avertissement (24) | 4 `.csproj` | `grep -c '<Nullable>enable' BattleShip.*/*.csproj && dotnet build` | 4 fichiers à 1 ; 0 warning |
| `sealed` sauf statique (CLAUDE.md § 2) | — | `grep -rnE '^\s*public (static \|abstract \|partial )?class ' --include='*.cs' . \| grep -v /obj/ \| grep -v sealed \| grep -vE 'static class\|partial class Program'` | aucune ligne : 0 classe ouverte |
| Contraintes de route, `TypedResults`, statuts 201/204/400/404/409 (31, 34) | `GameEndpoints.cs`, `ErrorMapping.cs` | `grep -rnoE 'Map(Get\|Post)\("[^"]+"' --include='*.cs' BattleShip.API \| grep -v /obj/` ; `grep -rnoE '\b(TypedResults\|Results)\.' --include='*.cs' BattleShip.API \| grep -v /obj/ \| cut -d: -f3 \| sort \| uniq -c` | 6 routes (5 sur `/games`, dont 4 en `{id:guid}`, + `/benchmark`) ; 16 `TypedResults.`, 0 `Results.` (2026-09-22, corrigé — comptait 5/12 avant l'ajout de `GET /games/{id}/events`) |
| Traduction des refus en un seul endroit, sans `catch` par type (ADR 0004) | `ErrorMapping.cs` | `grep -rn catch --include='*.cs' BattleShip.API \| grep -v /obj/` | aucune ligne : 0 `catch` |
| OpenAPI en développement, `api.http` versionné (35) | `Program.cs`, `api.http` | `grep -nE 'AddOpenApi\|MapOpenApi' BattleShip.API/Program.cs && git ls-files api.http` | lignes 14 et 36 (`MapOpenApi` sous `IsDevelopment`) ; `api.http` versionné |
| Durées de vie DI choisies et justifiées (33) | `Program.cs` ×2, ADR 0002, ADR 0007, revue 5 | `grep -nE 'AddSingleton\|AddScoped\|AddTransient' BattleShip.API/Program.cs BattleShip.App/Program.cs` | API : 3 `Singleton` (store, aléa, fabrique) + 5 `Scoped` (validateurs) ; App : 9 `Scoped` (`GameSessionStorage` inclus) |
| Store partagé concurrent (ADR 0002) | `InMemoryGameStore` : `ConcurrentDictionary` + verrou par partie | `dotnet test --filter InMemoryGameStore` | 12/12 (dont 4 tests `ReadEvents`) ; revues 3 et 4 |
| Aléa injecté (CLAUDE.md § 3 bis) | `FleetPlacer(Random)`, stratégies | `grep -rn 'Random.Shared' --include='*.cs' BattleShip.Models BattleShip.API/Strategies BattleShip.API/Benchmark \| grep -v /obj/ \| wc -l` | 0 |
| `[Fact]` et `[Theory]`, tests de refus (39, 63) | `BattleShip.Tests` | `grep -rn '\[Fact\]' --include='*.cs' BattleShip.Tests \| wc -l` ; même commande avec `\[Theory\]` | 100 Fact, 10 Theory, ≥ 12 tests de refus (2026-09-22) |
| Trois états Blazor, `HttpClient.BaseAddress`, `System.Net.Http.Json` (43, 44) | `GameState.LoadState`, `BattleApiClient`, `Program.cs` du front | `grep -c LoadState BattleShip.App/Services/GameState.cs ; grep -n BaseAddress BattleShip.App/Program.cs` | 12 lignes `LoadState` (Idle / Loading / Ready / Failed, `GameState.cs` remanié pour la reprise de partie) ; `BaseAddress` ligne 23 |
| CORS : origines explicites, en-têtes gRPC-Web exposés (45) | `Program.cs` de l'API, `CorsTests` (5 tests) | `grep -nE 'WithOrigins\|AllowAnyOrigin\|WithExposedHeaders' BattleShip.API/Program.cs ; dotnet test --filter CorsTests` | 5/5 ; `WithOrigins` ligne 18 depuis la configuration ; `AllowAnyOrigin` absent |
| Contrat `.proto` avec `csharp_namespace`, validation gRPC, `RpcException` typée (49, 50) | `battle.proto`, `BattleGrpcService`, `ErrorMapping` | `grep -n csharp_namespace BattleShip.API/Protos/battle.proto` | ligne 2 : `BattleShip.API.Grpc` |
| Code en anglais, docs en français (CLAUDE.md § 2) | — | `grep -rnP '[éèàçùÉ]' --include='*.cs' --include='*.razor' --include='*.proto' BattleShip.API BattleShip.App BattleShip.Models BattleShip.Tests \| grep -v /obj/ \| wc -l` | 0 |
| `dotnet format` propre (21) | — | `dotnet format --verify-no-changes` | exit 0 |

## Journal d'événements (ADR 0011, 2026-09-18)

| Exigence | Preuve dans le dépôt | Commande de contrôle | Constat |
|---|---|---|---|
| Le secret adverse ne franchit pas le flux d'événements en cours de partie (36, 37) | `EventProjection.ForPlayer`, `EventsEndpointTests` (sonde de fuite structurelle) | `dotnet test --filter EventsEndpointTests` | 0 case adverse non coulée exposée mi-partie ; 168→169 tests, R17 inclus |
| La flotte adverse est révélée après `GameEnded`, jamais avant (36, 37) | `EventsEndpointTests.After_the_game_ends_the_full_journal_is_served` | `dotnet test --filter After_the_game_ends_the_full_journal_is_served` | égalité structurelle d'ensembles de cases (R16) ; non rejoué manuellement à l'écran (limite) |
| Le repli reconstitue l'état réel à l'instant *n*, pas l'état final projeté en arrière (ADR 0011) | `GameFoldTests.A_ship_is_not_reported_sunk_before_the_shot_that_sank_it` | `dotnet test --filter A_ship_is_not_reported_sunk_before_the_shot_that_sank_it` | HONNÊTE `True` / FAUTIVE `False` — seul test du plan qui discrimine (revue 9) |
| Un journal désérialisé malformé ou tronqué ne se replie pas silencieusement (ADR 0011 § 2.1 bis) | `Game.Replay`, garde de contiguïté des rangs (R12) | `dotnet test --filter GameFoldTests` | journal vide → `ArgumentException` (R11) ; rangs non contigus → `InvalidOperationException` |
| Reprise de partie après rechargement de page (extension, ADR 0007 amendé) | `GameState.InitializeAsync`, `battleshipSettings` (`wwwroot/index.html`) | `dotnet build BattleShip.App` puis vérification navigateur (rechargement sur `/play`) | flotte redessinée, 5 navires à 0 touche, constaté à l'écran le 2026-09-18 |

## Arrière-plan 3D (ADR 0012, 2026-09-22)

| Exigence | Preuve dans le dépôt | Commande de contrôle | Constat |
|---|---|---|---|
| Le secret ne franchit pas le graphe de scène 3D, y compris à tout curseur de rejeu (ADR 0012) | `ScenePlanTests` (paire de tests 1/3, plus les cas à curseur) | `dotnet test --filter FullyQualifiedName~ScenePlanTests` | 22/22 (2026-09-22) |
| La projection de scène (`ScenePlan.For`) vit dans `BattleShip.Models`, atteignable par `App` et `Tests` sans conflit `CS0433` (spike, ADR 0012) | `BattleShip.Models/Scene/ScenePlan.cs` | `find BattleShip.Models -iname ScenePlan.cs && grep -c Reference BattleShip.Models/BattleShip.Models.csproj` | fichier présent ; 0 référence (Models reste sans dépendance) |
| Contrat d'interop à quatre fonctions : `init`, `update`, `freeze`, `dispose` (ADR 0012) | `BattleShip.App/wwwroot/js/scene.js` | `grep -nE "^\s*(init\|update\|freeze\|dispose)\s*\(" BattleShip.App/wwwroot/js/scene.js` | 4 lignes (20, 93, 136, 138) |
| Dépendance JS vendorisée, aucun CDN (ADR 0012) | `BattleShip.App/wwwroot/lib/three.module.js` | `wc -c BattleShip.App/wwwroot/lib/three.module.js && grep -rn "cdn\." BattleShip.App/wwwroot/index.html BattleShip.App/wwwroot/js/scene.js` | 1 304 820 octets présents ; 0 référence CDN |
| Canvas décoratif : `aria-hidden`, ne vole aucun événement (ADR 0012) | `SceneCanvas.razor`, `wwwroot/css/app.css` | `grep -n aria-hidden BattleShip.App/Components/SceneCanvas.razor ; grep -n "z-index: -1" BattleShip.App/wwwroot/css/app.css` | `aria-hidden="true"` (l.4) ; `z-index: -1` + `pointer-events: none` (l.1147-1148) |

Non vérifié — consigné en limite (`README.md`) plutôt qu'en constat : la dégradation sans WebGL et
la révélation visuelle de fin de partie n'ont pas été jouées à l'écran ; le recul visuel des épaves
avec le curseur est déduit de `ScenePlanTests` et du rendu des navires vérifié au pixel (revue 11),
pas observé directement — aucun navire adverse n'a été coulé dans les parties mesurées.

## Limites assumées

- Le commit initial `init` ne suit pas la convention `type: sujet` ; l'historique n'est pas réécrit.
- `BattleGrpcService.Fire` n'a pas le suffixe `Async` : la signature est celle de `BattleServiceBase` générée par `Grpc.Tools`.
- Aucun lecteur d'écran réel n'a été essayé (README, « Limites connues »).
- Arrière-plan 3D : dégradation sans WebGL et révélation visuelle de fin de partie non vérifiées ; recul des
  épaves avec le curseur déduit, non observé à l'écran (voir section ci-dessus et `REVUE-IA.md`, revue 11).
