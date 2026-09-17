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
| Références API→Models, App→Models, Tests→API (15) | trois `.csproj` | `grep -h ProjectReference */*.csproj` | 4 références : `Tests` référence aussi `Models` en direct, en plus de `API` (écart non corrigé) |
| Pages du gabarit purgées (28) | `BattleShip.App/Pages/` | `ls BattleShip.App/Pages` | ni Counter ni Weather |
| Partie complète contre l'ordinateur (5, 63) | `FireGrpcTests.A_game_played_to_the_end_finishes_with_the_player_as_winner` ; `Play.razor` bouton *New game* | `dotnet test --filter A_game_played_to_the_end` | test d'intégration ajouté à la tâche 7 (à venir) |
| FluentValidation sur toutes les entrées serveur, HTTP et gRPC (40, 50) | `BattleShip.API/Validation/` (4 validateurs), appel explicite `ValidateAsync` dans chaque route et dans `BattleGrpcService.Fire` | `grep -rn ValidateAsync BattleShip.API` | 4 entrées, 4 appels explicites |
| gRPC-Web : réponse **et** erreur attendue depuis le navigateur (48) | `Protos/battle.proto`, `docs/demo/*.png`, `docs/demo/grpc-web-trace.md` | `ls docs/demo` | 3 captures + trace |
| Tests métier et d'intégration (6, 39) | `BattleShip.Tests/Domain`, `/Api`, `/Opponent` | `dotnet test` | 142 tests au 2026-09-17 (avant les tâches 6-7) |
| Livrables IA (11, 62) | `PROMPTS.md`, `docs/adr/`, `REVUE-IA.md`, `README.md` | `ls` | présents ; condensés le 2026-09-17 |
| Historique Git exploitable (62) | `git log` | `git log --format=%s \| grep -vcE '^(feat\|fix\|docs\|test\|chore\|refactor): '` | 1 écart : le commit initial `init` (limite) |

## Spécifications du jeu (diapos 36, 37, 38, 46)

| Exigence | Preuve dans le dépôt | Commande de contrôle | Constat |
|---|---|---|---|
| Identifier la fin de partie **et le gagnant** (36) | `Game.Winner`, `GameDto.Winner` | `dotnet test --filter The_shooter_who_sinks_the_last_ship_is_the_winner` | **corrigé le 2026-09-17** : le vainqueur était déduit par le front |
