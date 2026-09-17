# Plan : code sans commentaires, audit de conformité au sujet, livrables IA condensés

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** livrer un dépôt dont le code ne porte plus aucun commentaire, dont la conformité à
chaque exigence et bonne pratique du support de cours est vérifiée par une commande et
consignée, et dont les livrables IA (`PROMPTS.md`, `REVUE-IA.md`, ADR, `README.md`,
`CONTEXTE-IA.md`) tiennent en une lecture.

**Architecture:** trois phases séquentielles. (1) Suppression mécanique des commentaires —
Roslyn pour le C#, expressions régulières pour Razor/CSS/proto/http — avec build, tests et
`dotnet format` comme filet. (2) Audit : chaque exigence des diapos 6, 28, 36-38, 46, 48,
61-63 et chaque bonne pratique du référentiel devient une ligne « exigence → preuve →
commande → constat » dans `docs/conformite.md` ; les trois écarts déjà constatés (fichier
`.http` du gabarit, absence de vainqueur dans le moteur et le contrat, absence de test de
partie complète) sont corrigés en TDD. (3) Condensation des livrables selon des budgets de
lignes fixés à l'avance et vérifiés par `wc -l`.

**Tech Stack:** .NET SDK 10.0.401, C# 14, `dotnet run --file` + `Microsoft.CodeAnalysis.CSharp`
(Roslyn) pour le strip C#, Python 3.12 pour les autres fichiers, xUnit, `WebApplicationFactory`,
gRPC-Web.

**Spec:** le support `../csharp-school/Cours C# ASP.NET - Bataille Navale - autonomie.pptx`
(texte extrait dans `../csharp-school/Ressources Bataille Navale/Referentiel.md` pour les
diapos 18-64) et `CLAUDE.md` § 2, 3, 5, 6, 7. La demande du binôme du 2026-09-17 :
« retire tous les commentaires du code ; vérifie que tout ce qui est demandé et toutes les
bonnes pratiques du support sont respectés ; mets à jour et raccourcis les fichiers IA — s'ils
sont trop longs, on ne les lira pas ».

## Global Constraints

- SDK .NET 10 épinglé : `global.json` = `10.0.100` avec `rollForward: latestFeature` ; `dotnet --version` doit afficher `10.0.401`.
- **Code en anglais** (identifiants, littéraux, texte affiché) ; **documentation humaine et commits en français**, un sujet par commit, préfixes `feat|fix|docs|test|chore|refactor:`.
- **Aucun commentaire dans le code** à partir de la tâche 3 — y compris dans les tests ajoutés aux tâches 6 et 7 : ni `//`, ni `///`, ni `/* */`, ni `@* *@`.
- `dotnet build` : **0 avertissement**. `dotnet test` : **0 échec**. `dotnet format --verify-no-changes` : code de sortie **0** avant chaque commit.
- Refus métier en `Result<T>` (ADR 0004) ; exceptions réservées aux anomalies. Jamais `Random.Shared` en dur dans `BattleShip.Models` ou dans une stratégie.
- `BattleShip.Models` ne référence aucun projet ni aucun paquet NuGet.
- Budgets de lignes des livrables (vérifiés par `wc -l`) : `PROMPTS.md` ≤ 160, `REVUE-IA.md` ≤ 200, `README.md` ≤ 150, chaque ADR ≤ 70, `CONTEXTE-IA.md` ≤ 60, `docs/conformite.md` ≤ 70.
- Scripts jetables dans le scratchpad de session (`/tmp/claude-1000/-home-luca-git-9-2-2-Env-aspnet-BattleShip/005671b5-a351-454b-9d45-668bebb5e33b/scratchpad/`), **pas** dans le dépôt ; leur contenu intégral figure dans ce plan pour rester reproductible.
- État de départ constaté le 2026-09-17 : branche `feat/bataille-navale`, arbre propre, `dotnet build` 0 avertissement, `dotnet test` **142/142** en ~4 s, `dotnet format --verify-no-changes` exit 0.

---

## Structure des fichiers

**Créés**

| Fichier | Responsabilité |
|---|---|
| `docs/conformite.md` | grille « exigence du sujet → preuve → commande → constat », une ligne par exigence ; unique sortie de l'audit |
| `docs/superpowers/plans/2026-09-17-nettoyage-conformite-livrables.md` | ce plan |
| scratchpad `strip-comments.cs` | retire les trivia de commentaire de tous les `.cs` (Roslyn) |
| scratchpad `strip-others.py` | retire les commentaires des `.razor`, `.css`, `.proto`, `.http` |

**Modifiés**

| Fichier | Changement |
|---|---|
| tous les `*.cs`, `*.razor` (56 fichiers), `wwwroot/css/app.css`, `Protos/battle.proto`, `api.http` | commentaires retirés |
| `BattleShip.Models/Game.cs` | `Player? Winner`, renseigné au moment où `Status` passe à `Finished` |
| `BattleShip.Models/Contracts/GameDto.cs` | `string? Winner` en dernier paramètre de `GameDto` |
| `BattleShip.API/Contracts/DtoMappings.cs` | projette `Winner` |
| `BattleShip.App/Pages/Play.razor` | `PlayerWon` lit `game.Winner` au lieu de déduire la victoire du nombre d'épaves |
| `BattleShip.Tests/Domain/GameTests.cs` | 2 tests : vainqueur désigné, pas de vainqueur avant la fin |
| `BattleShip.Tests/Api/FireGrpcTests.cs` | 1 test : partie complète jusqu'à `Finished` via gRPC-Web, vainqueur exposé, tir suivant refusé |
| `api.http` | adresse alignée sur le profil `http` par défaut, commentaires retirés |
| `PROMPTS.md`, `REVUE-IA.md`, `docs/adr/0001` à `0010`, `README.md`, `CONTEXTE-IA.md`, `CLAUDE.md` § 1 bis | condensés / actualisés |

**Supprimés**

| Fichier | Raison |
|---|---|
| `BattleShip.API/BattleShip.API.http` | reliquat du gabarit `webapi` : il interroge `/weatherforecast/`, route qui n'existe pas ; le fichier d'essais du projet est `api.http` à la racine |

---

## Phase 1 — Retirer les commentaires

### Tâche 1 : strip des fichiers C# par Roslyn

**Files:**
- Create: scratchpad `strip-comments.cs`
- Modify: tous les `BattleShip.*/**/*.cs` hors `bin/`, `obj/`

**Interfaces:**
- Produces: le script, invoqué par `dotnet run --file strip-comments.cs <racine-du-dépôt>`, imprime `removed N comment trivia in M files`.

- [ ] **Étape 1 : mesurer l'état de départ (le contrôle doit pouvoir échouer)**

```bash
cd /home/luca/git/9-2-2-Env-aspnet/BattleShip
grep -rnE '^\s*(//|///)|/\*' --include='*.cs' BattleShip.API BattleShip.App BattleShip.Models BattleShip.Tests | grep -v '/obj/' | wc -l
```

Attendu : un nombre **strictement positif** (ordre de grandeur : 1 200 lignes). Noter la valeur.

- [ ] **Étape 2 : écrire le script**

Fichier `strip-comments.cs` dans le scratchpad (hors du dépôt : un `.csproj` dans le dossier courant ferait lancer le projet au lieu du fichier, diapo 23) :

```csharp
#:package Microsoft.CodeAnalysis.CSharp@5.0.0
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

if (args.Length != 1)
    throw new ArgumentException("usage: dotnet run --file strip-comments.cs <repository-root>");

var root = Path.GetFullPath(args[0]);
var separator = Path.DirectorySeparatorChar;
var files = Directory.EnumerateFiles(root, "*.cs", SearchOption.AllDirectories)
    .Where(f => !f.Contains($"{separator}obj{separator}") && !f.Contains($"{separator}bin{separator}"))
    .OrderBy(f => f)
    .ToList();

var removedTrivia = 0;
var touchedFiles = 0;

foreach (var file in files)
{
    var text = File.ReadAllText(file);
    var tree = CSharpSyntaxTree.ParseText(text, new CSharpParseOptions(LanguageVersion.Preview));
    var node = tree.GetRoot();

    var comments = node.DescendantTrivia(descendIntoTrivia: false)
        .Where(t => t.IsKind(SyntaxKind.SingleLineCommentTrivia)
                 || t.IsKind(SyntaxKind.MultiLineCommentTrivia)
                 || t.IsKind(SyntaxKind.SingleLineDocumentationCommentTrivia)
                 || t.IsKind(SyntaxKind.MultiLineDocumentationCommentTrivia))
        .ToList();

    if (comments.Count == 0)
        continue;

    var stripped = node.ReplaceTrivia(comments, (_, _) => default).ToFullString();
    File.WriteAllText(file, Tidy(stripped));
    removedTrivia += comments.Count;
    touchedFiles++;
}

Console.WriteLine($"removed {removedTrivia} comment trivia in {touchedFiles} files");

static string Tidy(string source)
{
    var lines = source.Split('\n').Select(l => l.TrimEnd('\r', ' ', '\t')).ToList();
    var output = new List<string>(lines.Count);

    foreach (var line in lines)
    {
        var previous = output.Count > 0 ? output[^1] : null;
        if (line.Length == 0 && previous is not null && (previous.Length == 0 || previous.TrimEnd().EndsWith('{')))
            continue;
        if (line.TrimStart().StartsWith('}') && previous is { Length: 0 })
            output.RemoveAt(output.Count - 1);
        output.Add(line);
    }

    var result = string.Join('\n', output);
    return result.EndsWith('\n') ? result : result + "\n";
}
```

Hypothèses nommées : (a) le paquet `Microsoft.CodeAnalysis.CSharp` 5.0.0 est publié sur NuGet
(livré avec le SDK .NET 10) — si la restauration échoue avec `NU1102`, remplacer la première
ligne par `#:package Microsoft.CodeAnalysis.CSharp@4.14.0` ; (b) `ToFullString()` d'un arbre
Roslyn restitue le fichier à l'identique hors trivia retirés, y compris en présence d'erreurs
de syntaxe ; (c) `DescendantTrivia(descendIntoTrivia: false)` renvoie chaque commentaire
de documentation comme **un** trivia structuré, pas ses jetons internes.

- [ ] **Étape 3 : exécuter**

```bash
cd /tmp/claude-1000/-home-luca-git-9-2-2-Env-aspnet-BattleShip/005671b5-a351-454b-9d45-668bebb5e33b/scratchpad
dotnet run --file strip-comments.cs /home/luca/git/9-2-2-Env-aspnet/BattleShip
```

Attendu : `removed N comment trivia in M files` avec N > 0 et M ≈ 50.

- [ ] **Étape 4 : vérifier qu'il ne reste rien**

```bash
cd /home/luca/git/9-2-2-Env-aspnet/BattleShip
grep -rnE '^\s*(//|///)|/\*' --include='*.cs' BattleShip.API BattleShip.App BattleShip.Models BattleShip.Tests | grep -v '/obj/' | wc -l
grep -rnE '\s//\s' --include='*.cs' BattleShip.API BattleShip.App BattleShip.Models BattleShip.Tests | grep -v '/obj/'
```

Attendu : `0`, puis aucune ligne. Si la seconde commande renvoie une ligne, c'est une URL dans
une chaîne (`"https://..."` : celles de `CorsTests.cs` sont attendues et **ne sont pas** des
commentaires) — ne rien toucher.

- [ ] **Étape 5 : compiler**

```bash
dotnet build 2>&1 | tail -3
```

Attendu : `0 Warning(s)`, `0 Error(s)`. Une erreur ici signifie que Roslyn a retiré autre chose
qu'un commentaire : `git diff <fichier>` puis corriger à la main.

---

### Tâche 2 : strip des fichiers Razor, CSS, proto et http

**Files:**
- Create: scratchpad `strip-others.py`
- Modify: `BattleShip.App/**/*.razor`, `BattleShip.App/wwwroot/css/app.css`, `BattleShip.API/Protos/battle.proto`, `api.http`

- [ ] **Étape 1 : mesurer l'état de départ**

```bash
cd /home/luca/git/9-2-2-Env-aspnet/BattleShip
grep -rnE '@\*|^\s*//' --include='*.razor' BattleShip.App | wc -l
grep -c '/\*' BattleShip.App/wwwroot/css/app.css
grep -c '//' BattleShip.API/Protos/battle.proto
grep -cE '^# ' api.http
```

Attendu : quatre nombres strictement positifs (ordre de grandeur : 270, 54, 2, 9).

- [ ] **Étape 2 : vérifier les hypothèses du regex (le contrôle doit pouvoir échouer)**

```bash
grep -rnE '"[^"]*//[^"]*"' --include='*.razor' BattleShip.App | wc -l
grep -rnE '\S\s+//\s' --include='*.razor' BattleShip.App | grep -v http | wc -l
grep -rn '<!--' --include='*.razor' BattleShip.App | wc -l
grep -nE 'url\(|content:' BattleShip.App/wwwroot/css/app.css | grep -c '/\*'
```

Attendu : `0` quatre fois. Constaté le 2026-09-17 : `0 0 0 0`. Ces zéros garantissent que
supprimer les **lignes entières** commençant par `//` ou `///` et les blocs `@* *@` / `/* */`
ne touche ni une chaîne, ni un commentaire de fin de ligne, ni un `url()`. Si l'un des quatre
n'est plus à zéro, traiter ces lignes à la main avant l'étape 4.

- [ ] **Étape 3 : écrire le script**

Fichier `strip-others.py` dans le scratchpad :

```python
import pathlib
import re
import sys

root = pathlib.Path(sys.argv[1])


def tidy(text: str) -> str:
    lines = [line.rstrip() for line in text.split("\n")]
    output: list[str] = []
    for line in lines:
        previous = output[-1] if output else None
        if line == "" and previous is not None and (previous == "" or previous.endswith("{")):
            continue
        if line.lstrip().startswith("}") and previous == "":
            output.pop()
        output.append(line)
    result = "\n".join(output)
    return result if result.endswith("\n") else result + "\n"


def rewrite(path: pathlib.Path, *patterns: tuple[str, int]) -> int:
    text = path.read_text(encoding="utf-8")
    original = text
    for pattern, flags in patterns:
        text = re.sub(pattern, "", text, flags=flags)
    if text != original:
        path.write_text(tidy(text), encoding="utf-8")
        return 1
    return 0


def skip(path: pathlib.Path) -> bool:
    return any(part in ("bin", "obj", "lib") for part in path.parts)


touched = 0
for razor in root.rglob("*.razor"):
    if not skip(razor):
        touched += rewrite(razor, (r"@\*.*?\*@", re.S), (r"^[ \t]*///?.*\n", re.M))
for css in root.rglob("*.css"):
    if not skip(css):
        touched += rewrite(css, (r"/\*.*?\*/", re.S))
for proto in root.rglob("*.proto"):
    if not skip(proto):
        touched += rewrite(proto, (r"[ \t]*//.*$", re.M))
touched += rewrite(root / "api.http", (r"^#(?!##).*\n", re.M))
print(f"rewrote {touched} files")
```

- [ ] **Étape 4 : exécuter**

```bash
python3 /tmp/claude-1000/-home-luca-git-9-2-2-Env-aspnet-BattleShip/005671b5-a351-454b-9d45-668bebb5e33b/scratchpad/strip-others.py /home/luca/git/9-2-2-Env-aspnet/BattleShip
cd /home/luca/git/9-2-2-Env-aspnet/BattleShip && sed -i '/^### Note on firing$/d' api.http
```

Attendu : `rewrote N files` avec N ≈ 15. Le `sed` retire le séparateur `### Note on firing`,
dont la section ne contenait que des commentaires (le fait qu'il documentait — le tir n'a pas
de route HTTP — est dans le README et l'ADR 0005).

- [ ] **Étape 5 : vérifier qu'il ne reste rien**

```bash
cd /home/luca/git/9-2-2-Env-aspnet/BattleShip
grep -rnE '@\*|^\s*//' --include='*.razor' BattleShip.App | wc -l
grep -c '/\*' BattleShip.App/wwwroot/css/app.css
grep -c '//' BattleShip.API/Protos/battle.proto
grep -cE '^# ' api.http
grep -c '^###' api.http
```

Attendu : `0 0 0 0` puis `5` séparateurs `###` (six au départ, moins la section
commentaire-seule). Le `###` est la syntaxe de séparation des requêtes `.http`, pas un
commentaire.

- [ ] **Étape 6 : contrôle visuel de trois fichiers**

```bash
sed -n 1,30p BattleShip.App/Components/FiringGrid.razor
sed -n 1,25p BattleShip.App/wwwroot/css/app.css
cat BattleShip.API/Protos/battle.proto
```

Attendu : du balisage, du CSS et du proto lisibles, sans ligne vide en double, sans ligne vide
juste après `{`.

---

### Tâche 3 : filet de sécurité et commit

**Files:**
- Modify: (aucun nouveau) — `dotnet format` peut réécrire des espaces

- [ ] **Étape 1 : reformater**

```bash
cd /home/luca/git/9-2-2-Env-aspnet/BattleShip
dotnet format
dotnet format --verify-no-changes ; echo "format exit=$?"
```

Attendu : `format exit=0`.

- [ ] **Étape 2 : compiler et tester**

```bash
dotnet build 2>&1 | grep -E 'Warning\(s\)|Error\(s\)'
dotnet test 2>&1 | tail -2
```

Attendu : `0 Warning(s)`, `0 Error(s)`, `Passed! - Failed: 0, Passed: 142`. Les tests sont le
contrôle qui discrimine « seuls des commentaires ont disparu » de « du code a disparu » : un
strip qui aurait mangé une ligne de code casse la compilation ou un test.

- [ ] **Étape 3 : relire le diff en volume**

```bash
git diff --stat | tail -1
git diff -- BattleShip.Models/Game.cs | grep -E '^[-+]' | grep -vE '^[-+]\s*(///|//|\*|/\*)' | grep -vE '^(\+\+\+|---)' | grep -vE '^[-+]\s*$'
```

Attendu : première commande ≈ 60 fichiers, ≈ 1 500 suppressions, ≈ 0 ajout (hors espaces) ;
seconde commande **vide** — sur `Game.cs`, aucune ligne ajoutée ou retirée qui ne soit un
commentaire ou une ligne vide.

- [ ] **Étape 4 : committer**

```bash
git add -A
git commit -m "refactor: retire tous les commentaires du code

Le code doit se lire seul ; les décisions qu'expliquaient les commentaires
vivent dans docs/adr/ et REVUE-IA.md. Build sans avertissement, 142/142 tests.

Co-Authored-By: Claude Fable 5.1 <noreply@anthropic.com>"
```

---

## Phase 2 — Audit de conformité au sujet

### Tâche 4 : contraintes du socle (diapos 6, 28, 15) — contrôles mécaniques

**Files:**
- Create: `docs/conformite.md`

**Interfaces:**
- Produces: `docs/conformite.md` avec un tableau à quatre colonnes `Exigence (diapo) | Preuve dans le dépôt | Commande de contrôle | Constat 2026-09-17`, section « Contraintes du socle » ; les tâches 5 à 8 y ajoutent des lignes.

- [ ] **Étape 1 : exécuter la batterie et noter chaque constat**

```bash
cd /home/luca/git/9-2-2-Env-aspnet/BattleShip
echo "--- .NET 10 épinglé (diapo 19, 28)"; cat global.json; dotnet --version
echo "--- Minimal API, pas de contrôleurs (diapo 6)"; grep -rnE 'AddControllers|ControllerBase|\[ApiController\]' BattleShip.API --include='*.cs' | grep -v /obj/ | wc -l
echo "--- Blazor WebAssembly (diapo 6)"; head -1 BattleShip.App/BattleShip.App.csproj
echo "--- Models sans dépendance (diapo 28)"; grep -cE 'ProjectReference|PackageReference' BattleShip.Models/BattleShip.Models.csproj
echo "--- Références inter-projets (diapo 15)"; grep -h ProjectReference BattleShip.API/*.csproj BattleShip.App/*.csproj BattleShip.Tests/*.csproj
echo "--- Pages du gabarit purgées"; ls BattleShip.App/Pages
echo "--- .gitignore .NET (diapo 19)"; head -1 .gitignore
echo "--- FluentValidation : un validateur par entrée, appel explicite (diapos 34, 40, 50)"; ls BattleShip.API/Validation; grep -rn 'ValidateAsync' BattleShip.API --include='*.cs' | grep -v /obj/ | wc -l
echo "--- gRPC-Web monté (diapo 51)"; grep -nE 'AddGrpc|UseGrpcWeb|EnableGrpcWeb' BattleShip.API/Program.cs
echo "--- Preuves gRPC-Web navigateur (diapo 48)"; ls docs/demo
echo "--- Tests métier et d'intégration (diapo 6)"; ls BattleShip.Tests/Domain BattleShip.Tests/Api BattleShip.Tests/Opponent
echo "--- Livrables IA (diapo 62)"; ls PROMPTS.md REVUE-IA.md README.md CONTEXTE-IA.md docs/adr
echo "--- Historique Git lisible (diapo 62)"; git log --format=%s | grep -vcE '^(feat|fix|docs|test|chore|refactor): '
```

Attendu, ligne par ligne :
`10.0.100` / `10.0.401` · `0` · `Sdk="Microsoft.NET.Sdk.BlazorWebAssembly"` · `0` · trois
`ProjectReference` (API→Models, App→Models, Tests→API) · `NewGame.razor NotFound.razor
Placement.razor Play.razor` (ni `Counter` ni `Weather`) · `## Ignore Visual Studio...` ·
4 validateurs et **4** appels `ValidateAsync` (un par entrée : création, placement, benchmark,
tir gRPC) · trois lignes · 3 PNG + `grpc-web-trace.md` ·
trois dossiers non vides · tous présents · `1` (le seul écart : le commit initial `init`,
historique — non réécrit, consigné comme limite).

- [ ] **Étape 2 : créer `docs/conformite.md`**

```markdown
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
| Références API→Models, App→Models, Tests→API (15) | trois `.csproj` | `grep -h ProjectReference */*.csproj` | conformes |
| Pages du gabarit purgées (28) | `BattleShip.App/Pages/` | `ls BattleShip.App/Pages` | ni Counter ni Weather |
| Partie complète contre l'ordinateur (5, 63) | `FireGrpcTests.A_game_played_to_the_end_finishes_with_the_player_as_winner` ; `Play.razor` bouton *New game* | `dotnet test --filter A_game_played_to_the_end` | voir tâche 7 |
| FluentValidation sur toutes les entrées serveur, HTTP et gRPC (40, 50) | `BattleShip.API/Validation/` (4 validateurs), appel explicite `ValidateAsync` dans chaque route et dans `BattleGrpcService.Fire` | `grep -rn ValidateAsync BattleShip.API` | 4 entrées, 4 appels explicites |
| gRPC-Web : réponse **et** erreur attendue depuis le navigateur (48) | `Protos/battle.proto`, `docs/demo/*.png`, `docs/demo/grpc-web-trace.md` | `ls docs/demo` | 3 captures + trace |
| Tests métier et d'intégration (6, 39) | `BattleShip.Tests/Domain`, `/Api`, `/Opponent` | `dotnet test` | voir tâche 8 pour le total |
| Livrables IA (11, 62) | `PROMPTS.md`, `docs/adr/`, `REVUE-IA.md`, `README.md` | `ls` | présents ; condensés le 2026-09-17 |
| Historique Git exploitable (62) | `git log` | `git log --format=%s \| grep -vcE '^(feat\|fix\|docs\|test\|chore\|refactor): '` | 1 écart : le commit initial `init` (limite) |
```

- [ ] **Étape 3 : ne pas committer encore** — la tâche 8 complète et committe le fichier.

---

### Tâche 5 : corriger deux écarts de forme trouvés par l'audit

**Files:**
- Delete: `BattleShip.API/BattleShip.API.http`
- Modify: `api.http`

- [ ] **Étape 1 : constater l'écart**

```bash
cd /home/luca/git/9-2-2-Env-aspnet/BattleShip
cat BattleShip.API/BattleShip.API.http
grep -rn weatherforecast BattleShip.API --include='*.cs' | grep -v /obj/ | wc -l
head -1 api.http
grep -n '"applicationUrl"' BattleShip.API/Properties/launchSettings.json
```

Attendu : le fichier interroge `/weatherforecast/` ; `0` route de ce nom dans le code ; `api.http`
vise `https://localhost:7050` alors que `dotnet run` sans option prend le **premier** profil,
`http` (`http://localhost:5184`) — le README le dit, `api.http` le contredit.

- [ ] **Étape 2 : corriger**

```bash
git rm -q BattleShip.API/BattleShip.API.http
sed -i '1s|.*|@api = http://localhost:5184|' api.http
sed -i '1a @apiHttps = https://localhost:7050' api.http
head -3 api.http
```

Attendu :

```
@api = http://localhost:5184
@apiHttps = https://localhost:7050

```

- [ ] **Étape 3 : vérifier que le build ignore bien le fichier supprimé et committer**

```bash
dotnet build BattleShip.API 2>&1 | grep -E 'Warning\(s\)|Error\(s\)'
git add -A
git commit -m "chore: supprime le fichier .http du gabarit et aligne api.http sur le profil http

Co-Authored-By: Claude Fable 5.1 <noreply@anthropic.com>"
```

---

### Tâche 6 : le moteur désigne le vainqueur (diapo 36)

La diapo 36 exige de « résoudre les tirs et identifier la fin de partie **ainsi que le
gagnant** ». Constaté le 2026-09-17 : `Game` ne porte aucun vainqueur, `GameDto` non plus, et
`Play.razor` le **déduit** côté client (`Opponent.SunkShips.Count == Fleet.Count`) — une règle
de jeu décidée par le front, ce que l'ADR 0007 interdit.

**Files:**
- Modify: `BattleShip.Models/Game.cs`
- Modify: `BattleShip.Models/Contracts/GameDto.cs:89-91`
- Modify: `BattleShip.API/Contracts/DtoMappings.cs:15-24`
- Modify: `BattleShip.App/Pages/Play.razor` (méthode `PlayerWon`)
- Test: `BattleShip.Tests/Domain/GameTests.cs`

**Interfaces:**
- Produces: `Game.Winner : Player?` (nul tant que `Status != Finished`) ; `GameDto.Winner : string?` (`"Human"`, `"Opponent"` ou `null`), **dernier** paramètre positionnel du record.
- Consumes: `Game.Fire` (privé), `Board.AllSunk`, `Player`.

- [ ] **Étape 1 : écrire les deux tests qui échouent**

Ajouter à `GameTests` (le fichier possède déjà `TinyGame()` : grille 3×3, un `Destroyer` en
(0,0)-(1,0) sur chaque plateau, `ExtraTurnOnHit` actif) :

```csharp
    [Fact]
    public void An_unfinished_game_has_no_winner()
    {
        var game = TinyGame();

        game.PlayerFires(new Coordinate(0, 0));

        Assert.Equal(GameStatus.InProgress, game.Status);
        Assert.Null(game.Winner);
    }

    [Fact]
    public void The_shooter_who_sinks_the_last_ship_is_the_winner()
    {
        var game = TinyGame();
        game.PlayerFires(new Coordinate(0, 0));

        game.PlayerFires(new Coordinate(1, 0));

        Assert.Equal(GameStatus.Finished, game.Status);
        Assert.Equal(Player.Human, game.Winner);
    }
```

- [ ] **Étape 2 : constater l'échec de compilation**

```bash
cd /home/luca/git/9-2-2-Env-aspnet/BattleShip
dotnet test --filter "FullyQualifiedName~GameTests" 2>&1 | grep -E 'error CS|Passed!|Failed!' | head -3
```

Attendu : `error CS1061: 'Game' does not contain a definition for 'Winner'`.

- [ ] **Étape 3 : implémenter dans `Game.cs`**

Ajouter la propriété sous `Status` :

```csharp
    public GameStatus Status { get; private set; }
    public Player? Winner { get; private set; }
```

et remplacer, dans `Fire`, le bloc de fin de partie :

```csharp
        if (target.AllSunk)
        {
            Status = GameStatus.Finished;
            Winner = shooter;
        }
```

- [ ] **Étape 4 : constater le passage au vert**

```bash
dotnet test --filter "FullyQualifiedName~GameTests" 2>&1 | tail -1
```

Attendu : `Passed! - Failed: 0, Passed: 13` (11 `[Fact]` existants + 2 ; le fichier n'a
aucun `[Theory]`, donc le compte est exact).

- [ ] **Étape 5 : exposer dans le contrat**

`GameDto.cs`, record `GameDto` :

```csharp
public sealed record GameDto(
    Guid Id, string Status, string CurrentPlayer, OwnBoardDto Own, OpponentBoardDto Opponent,
    string OpponentDifficulty, IReadOnlyList<ShipTemplateDto> Fleet, bool ShipsMayTouch,
    string? Winner);
```

`DtoMappings.ToDto(this Game game)` :

```csharp
    public static GameDto ToDto(this Game game) =>
        new(
            game.Id,
            game.Status.ToString(),
            game.CurrentPlayer.ToString(),
            ToOwnBoardDto(game.HumanBoard, game.History),
            ToOpponentBoardDto(game.OpponentBoard, game.History),
            game.OpponentDifficulty,
            [.. game.Rules.Fleet.Select(t => new ShipTemplateDto(t.Name, t.Size))],
            game.Rules.ShipsMayTouch,
            game.Winner?.ToString());
```

Vérifier qu'aucun autre appelant ne construit un `GameDto` positionnel :

```bash
grep -rn 'new GameDto(' --include='*.cs' --include='*.razor' . | grep -v /obj/
```

Attendu : uniquement la ligne de `DtoMappings.cs`. Toute autre occurrence doit recevoir un
neuvième argument.

- [ ] **Étape 6 : le front lit le vainqueur au lieu de le déduire**

`Play.razor`, remplacer :

```csharp
    private static bool PlayerWon(GameDto game) => game.Opponent.SunkShips.Count == game.Fleet.Count;
```

par :

```csharp
    private static bool PlayerWon(GameDto game) => game.Winner == "Human";
```

- [ ] **Étape 7 : build complet, suite complète, format**

```bash
dotnet build 2>&1 | grep -E 'Warning\(s\)|Error\(s\)'
dotnet test 2>&1 | tail -1
dotnet format --verify-no-changes ; echo "format exit=$?"
```

Attendu : `0 Warning(s)` ; `Passed! - Failed: 0, Passed: 144` ; `format exit=0`.

- [ ] **Étape 8 : ajouter la ligne d'audit et committer**

Dans `docs/conformite.md`, section « Spécifications 1 à 4 » (créée ici, complétée à la tâche 8) :

```markdown
## Spécifications du jeu (diapos 36, 37, 38, 46)

| Exigence | Preuve dans le dépôt | Commande de contrôle | Constat |
|---|---|---|---|
| Identifier la fin de partie **et le gagnant** (36) | `Game.Winner`, `GameDto.Winner` | `dotnet test --filter The_shooter_who_sinks_the_last_ship_is_the_winner` | **corrigé le 2026-09-17** : le vainqueur était déduit par le front |
```

```bash
git add -A
git commit -m "feat: le moteur désigne le vainqueur et le contrat l'expose

Diapo 36 : identifier la fin de partie et le gagnant. Play.razor le déduisait
du nombre d'épaves ; c'est désormais une donnée du serveur.

Co-Authored-By: Claude Fable 5.1 <noreply@anthropic.com>"
```

---

### Tâche 7 : une partie complète jouée jusqu'au bout par l'API (diapos 5, 38, 63)

Constaté le 2026-09-17 : aucun test d'intégration ne mène une partie à `Finished`
(`grep -rl Finished BattleShip.Tests/Api/` est vide). Seuls le test métier
`Sinking_the_last_ship_finishes_the_game` et la démonstration navigateur le couvrent. Le test
ci-dessous exerce exactement la justification de l'ADR 0002 : lire le store injecté pour
connaître la flotte adverse **côté test**, puis la couler par gRPC-Web.

**Files:**
- Test: `BattleShip.Tests/Api/FireGrpcTests.cs`

**Interfaces:**
- Consumes: `FireGrpcTests.ReadyGame()` (existant : crée une partie `Easy` 10×10 et pose la flotte en lignes 0, 2, 4, 6, 8), `FireGrpcTests.Client()` (existant), `IGameStore.Read<T>`, `GameDto.Winner` (tâche 6).

- [ ] **Étape 1 : écrire le test qui échoue**

Ajouter en tête de fichier `using BattleShip.Models;` et
`using Microsoft.Extensions.DependencyInjection;`, puis le test :

```csharp
    [Fact]
    public async Task A_game_played_to_the_end_finishes_with_the_player_as_winner()
    {
        var id = await ReadyGame();
        var store = _factory.Services.GetRequiredService<IGameStore>();
        var targets = store.Read(id, game => game.OpponentBoard.Ships.SelectMany(s => s.Cells).ToList()).Value;
        var client = Client();
        FireResponse? last = null;

        foreach (var cell in targets)
        {
            last = await client.FireAsync(new FireRequest { GameId = id.ToString(), X = cell.X, Y = cell.Y });
        }

        Assert.Equal("Finished", last!.Status);
        var final = await _factory.CreateClient().GetFromJsonAsync<GameDto>($"/games/{id}");
        Assert.Equal("Finished", final!.Status);
        Assert.Equal("Human", final.Winner);
        Assert.Equal(final.Fleet.Count, final.Opponent.SunkShips.Count);

        var refused = await Assert.ThrowsAsync<RpcException>(() =>
            client.FireAsync(new FireRequest { GameId = id.ToString(), X = 9, Y = 9 }).ResponseAsync);
        Assert.Equal(StatusCode.FailedPrecondition, refused.StatusCode);
    }
```

Hypothèse nommée : avec `ExtraTurnOnHit` (règle par défaut), chaque tir de cette boucle est une
touche, donc le tour ne passe **jamais** à l'adversaire et la partie se termine sur le dernier
tir du joueur, quel que soit le placement aléatoire de la flotte adverse. Le test est
déterministe sans graine.

- [ ] **Étape 2 : constater l'échec pour la bonne raison**

Le test doit pouvoir échouer. Avant de le lancer sur le code réel, le lancer une fois avec
`Assert.Equal("Opponent", final.Winner)` à la place de `"Human"` :

```bash
cd /home/luca/git/9-2-2-Env-aspnet/BattleShip
dotnet test --filter A_game_played_to_the_end 2>&1 | grep -E 'Expected|Actual|Passed!|Failed!'
```

Attendu : `Failed!` avec `Expected: Opponent / Actual: Human`. Rétablir `"Human"`.

- [ ] **Étape 3 : passage au vert**

```bash
dotnet test --filter A_game_played_to_the_end 2>&1 | tail -1
dotnet test 2>&1 | tail -1
```

Attendu : `Passed! - Failed: 0, Passed: 1` puis `Passed! - Failed: 0, Passed: 145`.

- [ ] **Étape 4 : format, ligne d'audit, commit**

```bash
dotnet format --verify-no-changes ; echo "format exit=$?"
```

Ajouter à `docs/conformite.md`, section « Spécifications du jeu » :

```markdown
| Partie complète de la création à la victoire, aucun coup après la fin (5, 38) | `FireGrpcTests.A_game_played_to_the_end_finishes_with_the_player_as_winner` | `dotnet test --filter A_game_played_to_the_end` | **ajouté le 2026-09-17** ; `FailedPrecondition` sur le tir suivant |
```

```bash
git add -A
git commit -m "test: joue une partie complète jusqu'à la victoire par gRPC-Web

Co-Authored-By: Claude Fable 5.1 <noreply@anthropic.com>"
```

---

### Tâche 8 : bonnes pratiques du référentiel (diapos 4, 24-27, 32-35, 39, 43-46, 61)

**Files:**
- Modify: `docs/conformite.md`

- [ ] **Étape 1 : exécuter la batterie**

```bash
cd /home/luca/git/9-2-2-Env-aspnet/BattleShip
echo "--- Propriétés, pas de getters/setters Java (diapo 4)"; grep -rnE 'public \w+ (Get|Set)[A-Z]\w*\(' --include='*.cs' BattleShip.API BattleShip.App BattleShip.Models | grep -v /obj/ | wc -l
echo "--- Champs privés en _camelCase"; grep -rnE '^\s*private (static )?(readonly )?[A-Za-z][A-Za-z0-9<>\[\]?,. ]* [a-z]\w*\s*(=|;)' --include='*.cs' --include='*.razor' BattleShip.API BattleShip.App BattleShip.Models BattleShip.Tests | grep -v /obj/ | grep -v 'const ' | wc -l
echo "--- Suffixe Async sur les Task (diapo 4)"; grep -rnE '\bTask(<[^>]*>)?\s+[A-Z]\w*\(' --include='*.cs' --include='*.razor' BattleShip.API BattleShip.App BattleShip.Models | grep -v /obj/ | grep -v Async
echo "--- Pas de .Result / .Wait() bloquants (diapo 4)"; grep -rnE '\.Result;|\.Wait\(\)|\.GetAwaiter\(\)\.GetResult' --include='*.cs' --include='*.razor' BattleShip.API BattleShip.App BattleShip.Models | grep -v /obj/ | wc -l
echo "--- Nullable activé (diapo 24)"; grep -c '<Nullable>enable' BattleShip.*/*.csproj
echo "--- sealed par défaut"; grep -rnE '^\s*public (static |abstract |partial )?class ' --include='*.cs' . | grep -v /obj/ | grep -v sealed | grep -vE 'static class|partial class Program'
echo "--- Contraintes de route (diapo 31)"; grep -rnoE 'Map(Get|Post)\("[^"]+"' BattleShip.API --include='*.cs' | grep -v /obj/
echo "--- TypedResults (diapo 34)"; grep -rnoE '\b(TypedResults|Results)\.' --include='*.cs' BattleShip.API | grep -v /obj/ | cut -d: -f3 | sort | uniq -c
echo "--- Codes HTTP 201/204/400/404/409 (diapo 34)"; grep -nE 'Status(201|204|400|404|409)|Created|NoContent|BadRequest|NotFound|Conflict' BattleShip.API/Endpoints/*.cs | wc -l
echo "--- Aucun catch par type dans les façades (ADR 0004)"; grep -rn 'catch' BattleShip.API --include='*.cs' | grep -v /obj/ | wc -l
echo "--- OpenAPI en développement (diapo 35)"; grep -nE 'AddOpenApi|MapOpenApi' BattleShip.API/Program.cs
echo "--- Fichier .http versionné (diapo 35)"; git ls-files api.http
echo "--- Durées de vie DI justifiées (diapo 33)"; grep -nE 'AddSingleton|AddScoped|AddTransient' BattleShip.API/Program.cs BattleShip.App/Program.cs
echo "--- Store concurrent (ADR 0002)"; grep -n 'ConcurrentDictionary' BattleShip.API/Stores/InMemoryGameStore.cs | wc -l
echo "--- Aléa injecté, jamais Random.Shared en dur (CLAUDE.md § 3 bis)"; grep -rn 'Random.Shared' --include='*.cs' BattleShip.Models BattleShip.API/Strategies BattleShip.API/Benchmark | grep -v /obj/ | wc -l
echo "--- Fact et Theory (diapo 39)"; echo "Fact: $(grep -rh '\[Fact\]' BattleShip.Tests | wc -l)  Theory: $(grep -rh '\[Theory\]' BattleShip.Tests | wc -l)"
echo "--- Tests de refus, pas seulement nominal (diapo 63)"; grep -rhoE 'public (async )?(void|Task) \w*(rejected|refused|Invalid|NotFound|404|400|never)\w*' BattleShip.Tests | wc -l
echo "--- Trois états Blazor : chargement, succès, échec (diapo 43)"; grep -rn 'IsCommunicationFailure' BattleShip.App/Services/BattleApiClient.cs | wc -l; grep -rn 'catch (RpcException' BattleShip.App/Services/BattleGrpcClient.cs | wc -l
echo "--- HttpClient avec BaseAddress (diapo 44)"; grep -n 'BaseAddress' BattleShip.App/Program.cs | wc -l
echo "--- CORS : origines explicites, jamais AllowAnyOrigin (diapo 45)"; grep -nE 'WithOrigins|AllowAnyOrigin|WithExposedHeaders' BattleShip.API/Program.cs
echo "--- Secret du jeu (diapo 36, 37)"; grep -rhoE 'public (async )?(void|Task) \w+' BattleShip.Tests/Api/SecretTests.cs | wc -l
echo "--- Adversaire soumis aux mêmes règles (diapo 38)"; grep -n 'A_strategy_never_proposes_an_invalid_shot' BattleShip.Tests/Opponent/StrategyInvariantTests.cs | wc -l
echo "--- gRPC : csharp_namespace, validation, RpcException typée (diapos 49, 50)"; grep -n 'csharp_namespace' BattleShip.API/Protos/battle.proto; grep -nE 'ValidateAsync|RpcException|InvalidArgument' BattleShip.API/Services/BattleGrpcService.cs BattleShip.API/Endpoints/ErrorMapping.cs | wc -l
echo "--- Code en anglais (CLAUDE.md § 2)"; grep -rnP '[éèàçùÉ]' --include='*.cs' --include='*.razor' --include='*.proto' BattleShip.API BattleShip.App BattleShip.Models BattleShip.Tests | grep -v /obj/ | wc -l
echo "--- dotnet format"; dotnet format --verify-no-changes >/dev/null 2>&1; echo "exit=$?"
```

Attendu, dans l'ordre : `0` · `0` · une seule ligne, `BattleGrpcService.Fire` (surcharge d'une
méthode générée par `Grpc.Tools` : le nom est imposé par la base — **exception justifiée**, à
consigner) · `0` · `4` · rien · 5 routes dont trois avec `{id:guid}` · uniquement `TypedResults.`
· ≥ 6 · `0` · deux lignes · `api.http` · Singleton pour `IGameStore`, `Random`,
`IOpponentStrategyFactory` ; Scoped pour les validateurs et les services du front · ≥ 1 · `0` ·
`Fact: 72 Theory: 9` (après tâches 6-7) · ≥ 12 · `4` et `1` · `1` · `WithOrigins` et
`WithExposedHeaders` présents, `AllowAnyOrigin` **absent** · `3` · `1` · `csharp_namespace`
présent et ≥ 4 · `0` (le mot français de `Play.razor:151` était dans un commentaire, disparu à
la tâche 2) · `exit=0`.

Tout écart entre attendu et constaté est **rapporté tel quel** dans le tableau (CLAUDE.md § 6,
règle 6), pas corrigé en silence : une correction supplémentaire ferait l'objet d'une tâche
distincte ajoutée à ce plan.

- [ ] **Étape 2 : compléter `docs/conformite.md`**

Ajouter les sections suivantes (une ligne par contrôle ci-dessus, dans le même format
à quatre colonnes ; les constats sont ceux réellement observés à l'étape 1) :

```markdown
## Spécifications du jeu (diapos 36, 37, 38, 46) — suite

| Exigence | Preuve dans le dépôt | Commande de contrôle | Constat |
|---|---|---|---|
| Placement sans chevauchement ni débordement (36) | `PlacementRulesTests`, `FleetPlacerTests` (graine fixe) | `dotnet test --filter PlacementRules` | 6 tests de refus + 2 de placement |
| Un coup refusé ne modifie pas la partie ; case rejouée non comptée (36) | `GameTests.A_shot_on_an_already_shot_cell_is_rejected` | `dotnet test --filter already_shot` | `Result` en échec, historique inchangé |
| Positions adverses secrètes (36, 37) | `SecretTests` (3 tests), `ShotHistory` sans chemin vers `Board` | `dotnet test --filter SecretTests` | 3/3 ; revue 2 |
| Contrat explicite : créer, connaître l'état, faire évoluer (37) | `POST /games`, `GET /games/{id}`, `POST /games/{id}/placement`, `Fire` gRPC, `GET /games/{id}/history` | `grep -rn 'Map(Get\|Post)' BattleShip.API` | 5 opérations, ADR 0005 et 0008 |
| Aucun coup après la fin (38) | `GameTests.A_shot_after_the_end_of_the_game_is_rejected` ; `FireGrpcTests` (tâche 7) | `dotnet test --filter after_the_end` | `GameAlreadyFinished` → `FailedPrecondition` |
| Adversaire soumis aux mêmes règles de validité (38) | `StrategyInvariantTests.A_strategy_never_proposes_an_invalid_shot` (`[Theory]` sur les 3 stratégies) | `dotnet test --filter never_proposes` | 3/3 |
| Interface : créer, deux grilles, jouer, annoncer la fin, rejouer (46) | `NewGame.razor`, `Placement.razor`, `Play.razor` (bouton *New game*) | démonstration README | constaté dans le navigateur le 2026-09-16 |
| Incident de communication pris en charge (46) | `BattleApiClient.IsCommunicationFailure` (4 points), `BattleGrpcClient` (`RpcException`) | `grep -rn IsCommunicationFailure BattleShip.App` | 4 + 1 |

## Bonnes pratiques du référentiel (diapos 4, 24-27, 31-35, 39, 43-45, 49-51)

| Pratique | Preuve | Commande | Constat |
|---|---|---|---|
| Propriétés, pas de getters/setters (4) | tout le domaine | `grep -rnE 'public \w+ (Get\|Set)[A-Z]'` | 0 |
| `_camelCase` privé, `PascalCase` public, `Async` sur les `Task` (4, CLAUDE.md § 2) | — | greps de la tâche 8 | 0 écart ; exception : `BattleGrpcService.Fire`, nom imposé par la base générée |
| Pas de `.Result` / `.Wait()` (4) | — | grep | 0 |
| `Nullable` activé, 0 avertissement (24) | 4 `.csproj` | `dotnet build` | 4/4, 0 warning |
| `sealed` sauf statique (CLAUDE.md § 2) | — | grep | 0 classe ouverte |
| Contraintes de route, `TypedResults`, statuts 201/204/400/404/409 (31, 34) | `GameEndpoints.cs`, `ErrorMapping.cs` | grep | `{id:guid}` ×3 ; 100 % `TypedResults` |
| Traduction des refus en un seul endroit, sans `catch` par type (ADR 0004) | `ErrorMapping.cs` | `grep -rn catch BattleShip.API` | 0 `catch` |
| OpenAPI en développement, `api.http` versionné (35) | `Program.cs`, `api.http` | grep, `git ls-files` | présents |
| Durées de vie DI choisies et justifiées (33) | `Program.cs` ×2, ADR 0002, ADR 0007, revue 6 | grep | Singleton pour l'état partagé, Scoped ailleurs |
| Store partagé concurrent (ADR 0002) | `InMemoryGameStore` : `ConcurrentDictionary` + verrou par partie | `dotnet test --filter InMemoryGameStore` | revues 4 et 5 |
| Aléa injecté (CLAUDE.md § 3 bis) | `FleetPlacer(Random)`, stratégies | grep `Random.Shared` hors `Program.cs` | 0 |
| `[Fact]` et `[Theory]`, tests de refus (39, 63) | `BattleShip.Tests` | `dotnet test` | 72 Fact, 9 Theory, ≥ 12 tests de refus |
| Trois états Blazor, `HttpClient.BaseAddress`, `System.Net.Http.Json` (43, 44) | `BattleApiClient`, `Program.cs` du front | grep | conformes |
| CORS : origines explicites, en-têtes gRPC-Web exposés (45) | `Program.cs` de l'API, `CorsTests` (3 tests) | `dotnet test --filter CorsTests` | `WithOrigins` depuis la configuration ; `AllowAnyOrigin` absent |
| Contrat `.proto` avec `csharp_namespace`, validation gRPC, `RpcException` typée (49, 50) | `battle.proto`, `BattleGrpcService`, `ErrorMapping` | grep | conformes |
| Code en anglais, docs en français (CLAUDE.md § 2) | — | grep accents | 0 dans le code |
| `dotnet format` propre (21) | — | `dotnet format --verify-no-changes` | exit 0 |

## Limites assumées

- Le commit initial `init` ne suit pas la convention `type: sujet` ; l'historique n'est pas réécrit.
- `BattleGrpcService.Fire` n'a pas le suffixe `Async` : la signature est celle de `BattleServiceBase` générée par `Grpc.Tools`.
- Aucun lecteur d'écran réel n'a été essayé (README, « Limites connues »).
```

- [ ] **Étape 3 : vérifier le budget et committer**

```bash
wc -l docs/conformite.md
git add docs/conformite.md
git commit -m "docs: ajoute la grille de conformité au sujet, une commande par exigence

Co-Authored-By: Claude Fable 5.1 <noreply@anthropic.com>"
```

Attendu : ≤ 70 lignes. Si le fichier dépasse, fusionner des lignes de même diapo plutôt que
raccourcir les commandes.

---

## Phase 3 — Condenser les livrables IA

Règle commune aux tâches 9 à 12 : **une entrée = une décision + sa preuve**. On coupe les
récits d'itérations, les répétitions entre fichiers (un fait vit dans un seul document, les
autres y renvoient), les métaphores et les justifications de justifications. On garde
systématiquement : la décision, le fait mesuré (chiffre, message d'erreur exact, commit), la
commande rejouable, la limite. Format des entrées : celui des diapos 56 à 58 (champs en gras,
une à trois lignes par champ), plus compact que les listes à puces actuelles.

### Tâche 9 : `PROMPTS.md` de 639 à ≤ 160 lignes

**Files:**
- Modify: `PROMPTS.md`

- [ ] **Étape 1 : mesurer**

```bash
cd /home/luca/git/9-2-2-Env-aspnet/BattleShip && wc -l PROMPTS.md && grep -c '^## ' PROMPTS.md
```

Attendu : `639`, `10` entrées.

- [ ] **Étape 2 : réécrire le fichier avec exactement 8 entrées**

En-tête (3 lignes) : titre, une phrase « une entrée par échange qui a compté ; outil : Claude
Code, claude-opus-5 sauf mention contraire », et la référence à `REVUE-IA.md` pour les
contrôles détaillés. Puis, dans l'ordre, chaque entrée au format :

```markdown
## 2026-09-15 — <sujet>
**Contexte** : …  **Prompt** : « … »
**Réponse** : … (hypothèses : …)
**Décision** : acceptée / adaptée / rejetée — …
**Vérification** : `commande` → attendu : … ; observé : …
**Preuve / limite** : commit `xxxxxxx` ; …
```

Faits à conserver **obligatoirement** par entrée (tout le reste peut être coupé) :

1. **Brainstorming des cinq familles de choix** — 9 questions guidées ; adaptée : suivie sur
   modèle, verrou par partie, trois niveaux, `Result<T>`, `GameState`, backlog ; **écartée** sur
   placement manuel et « touche = on rejoue » (ADR 0006) ; chiffres ≈ 96/65/60/42 = littérature
   sans non-adjacence, non mesurés alors ; preuves `935a8b7`, `f379852`.
2. **Tâche 14 — tir gRPC-Web, `ErrorMapping` unique, test de course** (fusion des entrées
   « tâche 14 » et « correction du coordinateur ») — `Fire` renvoie une séquence ; contrôle
   discriminant `NotFound` → `Unknown` observé `Expected: NotFound / Actual: Unknown` ; test de
   course HTTP/gRPC resté vert malgré 224 chevauchements mesurés, redescendu au store : 5/5 en
   échec sans verrou (`ArgumentException: Destination array is not long enough`), 5/5 au vert
   avec ; suite 133/133 en ~4 s ; commits `1a8f5dc`, `aef815e` ; renvoi revue 5.
3. **Tâche 16 — état Blazor et contrat partagé** — question posée au binôme au lieu de trancher ;
   DTO déplacés dans `Models/Contracts` (ADR 0008) ; durées de vie du plan rejetées (dépendance
   captive, revue 6) ; `Adopt(GameDto)` retiré (YAGNI) ; navigateur : `OPTIONS 204`, `POST 201`,
   API arrêtée → page utilisable ; commits `fe16e5b`, `d037bcf`.
4. **Tâches 17-19 — placement, jeu, démonstration** — prévisualisation par
   `PlacementRules.Validate` **sans bloquer le clic** (sinon le refus serveur est indémontrable) ;
   `Enum.TryParse<GameError>` sur `Status.Detail` ; défaut de prévisualisation trouvé par le
   pilotage, pas la relecture ; victoire en 96 tirs, 116 appels `Fire`, erreurs en **HTTP 200**
   avec `grpc-status` 3 puis 5 ; 37 903 tirs, 0 refus (revue 2) ; commits `0e5ec13`, `ed4cb56` ;
   limite : Chrome lancé avec `--ignore-certificate-errors`.
5. **Tâches 20-21 — historique, rejeu, accessibilité** — deux hypothèses fausses des tests
   d'historique corrigées (case (5,5) crue vide, unicité des noms par camp) ; pouvoir
   discriminant établi en inversant l'historique ; focus perdu après tir (`activeElement` =
   `<body>`), corrigé en deux fois ; contrastes calculés 3,13:1 → 6,59:1 et 1,53:1 → 3,02:1 ;
   rejeu = 0 requête ; commits `224bb39`, `9f5d151` ; limite : aucun lecteur d'écran réel.
6. **Refonte — trois apparences** — diagnostic (symétrie sans hiérarchie, pas de coordonnées,
   pas de temps fort) ; un jeu de jetons par `[data-theme]`, jamais le balisage ni les glyphes
   (ADR 0009) ; Bootstrap retiré ; 5 contrastes en échec corrigés ; douzième ligne fantôme
   (`gridTemplateRows` 12 → 11) ; commit `499d046` ; limite : aucun test automatisé.
7. **Coques dessinées** — couche SVG superposée au gabarit identique (ADR 0010) ; dégâts hors du
   SVG ; 53/53 contrôles de géométrie, culture `fr-FR` prouvée discriminante (`"1,5"`) ;
   prévisualisation qui validait la flotte entière corrigée par paires ; commit `c06774e`.
8. **Page d'accueil : démonstration rejetée, illustration retenue** (fusion des deux dernières
   entrées) — première proposition (partie auto-jouée) **rejetée** par le binôme ; remplacée par
   la scène `SeaBattle` en SVG thémée ; vérifié : couleurs par `getComputedStyle` dans les trois
   apparences, mode calme = image figée, 0 élément focalisable ; commits `31b1407`, `31073e1` ;
   leçon : ne pas juger un thème sur un JPEG compressé.

- [ ] **Étape 3 : vérifier**

```bash
wc -l PROMPTS.md && grep -c '^## ' PROMPTS.md && grep -c '`[0-9a-f]\{7\}`' PROMPTS.md
```

Attendu : `≤ 160`, `8`, `≥ 12` références de commit. Chaque hash cité doit exister :

```bash
grep -oE '`[0-9a-f]{7}`' PROMPTS.md | tr -d '`' | sort -u | while read h; do git cat-file -e "$h^{commit}" 2>/dev/null || echo "INCONNU $h"; done
```

Attendu : aucune ligne `INCONNU`.

- [ ] **Étape 4 : committer**

```bash
git add PROMPTS.md
git commit -m "docs: condense PROMPTS.md en huit échanges décisifs

Co-Authored-By: Claude Fable 5.1 <noreply@anthropic.com>"
```

---

### Tâche 10 : `REVUE-IA.md` de 746 à ≤ 200 lignes

**Files:**
- Modify: `REVUE-IA.md`

- [ ] **Étape 1 : mesurer**

```bash
cd /home/luca/git/9-2-2-Env-aspnet/BattleShip && wc -l REVUE-IA.md && grep -c '^## Revue' REVUE-IA.md
```

Attendu : `746`, `7`.

- [ ] **Étape 2 : réécrire avec 6 revues, numérotées 1 à 6**

La revue 1 actuelle (état du dépôt décrit par `CLAUDE.md` § 1 bis) est **supprimée** : elle
porte sur un fichier de cadrage périmé, pas sur une proposition de code, et sa conséquence
(corriger § 1 bis) est réalisée à la tâche 12. Les six autres sont renumérotées et réécrites au
format de la diapo 58 :

```markdown
## Revue N — <sujet> — acceptée | adaptée | rejetée
**Proposition** : … (`fichier`, commit `xxxxxxx`)
**Hypothèse à vérifier** : …
**Expérience** : `commande` — attendu avant exécution : … — erreur détectable : …
**Observation** : … (faits, chiffres, message exact)
**Décision** : …
**Preuves et limites** : …
```

Faits à conserver obligatoirement :

1. **Aucune exception par tour d'adversaire (ADR 0004) — acceptée** — deux moitiés : (a) refus
   comptés sur 200 parties × 3 stratégies, graine 20260915 : `Random 19139`, `HuntTarget 10520`,
   `Density 8244`, **37 903 tirs, 0 refus** ; (b) parcours par réflexion : 9 types visités depuis
   `ShotHistory`, aucun chemin vers `Board` ni `Game` ; pouvoir discriminant prouvé par la
   stratégie fautive `AlwaysSameCell` → `CellAlreadyShot x200` ; limite : graine et règles
   uniques, (b) n'exclut pas un plateau passé par constructeur.
2. **Les chiffres des stratégies — confirmée** — attendu énoncé le 2026-09-15 : ordre
   `Random > HuntTarget > Density`, `Density < 55` ; mesuré (200 parties) : **95,69 / 52,60 /
   41,22**, min-max 80-100 / 29-67 / 26-58 ; `dotnet test --filter StrategyBenchmark` ;
   limite : une seule flotte, une seule grille, `ExtraTurnOnHit` non isolé.
3. **Pouvoir discriminant du test de concurrence du store — adaptée** — trois versions du départ
   des 32 tirs, verrou commenté, 3 exécutions chacune : `Task.Run` seul détecte 2/3 (0,86 s) ;
   `Barrier` sur `Task.Run` détecte 2/3 mais **15-18 s** (injection du pool ~1 thread/500 ms) ;
   `Barrier` sur `Thread` dédiés détecte **3/3** avec 4/5 `InlineData` en échec, 842 ms ;
   commits `883fe0b`, `44bd57c`, `c604123` ; limite : 1/5 `InlineData` n'a pas détecté, test
   probabiliste.
4. **Test de course `Read` vs `Mutate` — adaptée** — au niveau HTTP/gRPC : 4 conceptions vertes
   malgré 224 chevauchements instrumentés, coût 4-6 s → supprimé ; au niveau store : verrou de
   `Read` retiré → **5/5 en échec** (`ArgumentException: Destination array is not long enough`,
   5 à 14 occurrences), rétabli → 5/5 au vert en 380-460 ms ; réserve : l'exception vient de
   `HashSet<Coordinate>.ToList()` (`ReceivedShots`), pas de `List<T>` (`History`), qui reste
   verte 3/3 jusqu'à 4 800 tirs ; commit `aef815e`.
5. **Durées de vie de `GameState` — adaptée** — `BuildServiceProvider(validateScopes: true)` sur
   les vrais types : plan (`Singleton` sur client `Scoped`) → `Cannot consume scoped service
   'System.Net.Http.HttpClient' from singleton 'GameState'` ; livré (tout `Scoped`) → résolu ;
   suite 136/136 dans les deux cas, donc **les tests ne voient pas ce défaut** ; commit `d037bcf`.
6. **Recopier les DTO côté front — rejetée** — (1) relecture du corps réel de `POST /games`
   avec `JsonSerializerDefaults.Web` : 13/13 propriétés ; (2) renommage
   `OpponentDifficulty → Level` puis `dotnet build BattleShip.App` → `error CS1061` ; sous copie
   le même renommage compilerait ; commits `fe16e5b`, `d037bcf` ; ADR 0008 ; limite : rien
   n'empêche un attribut `System.Text.Json` d'entrer dans `Models`.

En-tête : titre, une phrase rappelant qu'une proposition acceptée exige aussi une preuve, et le
bilan « six revues, toutes closes : 2 acceptées, 3 adaptées, 1 rejetée ».

- [ ] **Étape 3 : vérifier**

```bash
wc -l REVUE-IA.md && grep -c '^## Revue' REVUE-IA.md
grep -oE '`[0-9a-f]{7}`' REVUE-IA.md | tr -d '`' | sort -u | while read h; do git cat-file -e "$h^{commit}" 2>/dev/null || echo "INCONNU $h"; done
grep -rn 'revue [1-7]\|Revue [1-7]' README.md CONTEXTE-IA.md docs/adr/*.md PROMPTS.md CLAUDE.md
```

Attendu : `≤ 200`, `6`, aucun `INCONNU`. La troisième commande liste les renvois à corriger
avec la nouvelle numérotation (ancienne 2→1, 3→2, 4→3, 5→4, 6→5, 7→6 ; ancienne 1 → renvoi
supprimé) — les corriger **dans cette tâche** pour que le dépôt reste cohérent au commit.

- [ ] **Étape 4 : committer**

```bash
git add -A
git commit -m "docs: condense REVUE-IA.md en six revues et renumérote les renvois

Co-Authored-By: Claude Fable 5.1 <noreply@anthropic.com>"
```

---

### Tâche 11 : ADR 0001 à 0010, chacun ≤ 70 lignes

**Files:**
- Modify: `docs/adr/0001-modele.md` … `docs/adr/0010-coques.md`

- [ ] **Étape 1 : mesurer**

```bash
cd /home/luca/git/9-2-2-Env-aspnet/BattleShip && wc -l docs/adr/*.md
```

Attendu : de 66 à 115 lignes ; six ADR au-dessus de 70.

- [ ] **Étape 2 : réécrire chaque ADR en gardant les sept sections de la diapo 57**

Budget par section : Statut 1 ligne · Contexte ≤ 6 · Options ≤ 15 (une ligne par option :
nom, avantage, limite) · Décision ≤ 15 (code conservé s'il est le contrat) · Conséquences ≤ 8
· Vérification et réexamen ≤ 8 · Références ≤ 4.

Coupes précises :

- **0003** : la sous-section « Nombre moyen de coups — mesuré » devient trois lignes (tableau
  des moyennes + renvoi `REVUE-IA.md` revue 2) ; supprimer la comparaison à la littérature.
- **0004** : garder la table `GameError → HTTP / gRPC` ; renvoyer à la revue 1 pour la mesure
  « 0 exception par tour » au lieu de la raconter.
- **0005** : garder le choix « le tir en gRPC-Web exclusif » et ses deux conséquences (pas de
  route HTTP, `api.http` ne couvre pas le tir) ; couper la narration du montage
  `WebApplicationFactory`.
- **0007** : ajouter une ligne « Réexamen 2026-09-16 : durée de vie `Scoped`, revue 5 » et
  couper le reste du réexamen.
- **0008** : garder les deux options (copie / partage) et le garde-fou « aucun paquet dans
  `Models` » ; renvoyer à la revue 6.
- **0009** et **0010** : « Vérification » réduite aux chiffres (contrastes, 53/53, 490×490) sans
  le récit des défauts trouvés, qui est dans `PROMPTS.md`.
- **0001**, **0002**, **0006** : appliquer le budget ; le contenu est déjà proche.

Chaque ADR conserve son statut `Accepté — <date>` et gagne, si absent, une ligne
`## Références` vers la revue ou le test qui l'étaye.

- [ ] **Étape 3 : vérifier**

```bash
wc -l docs/adr/*.md | awk '$1 > 70 && $2 != "total" {print "TROP LONG", $0}'
for f in docs/adr/*.md; do for s in "Statut" "Contexte" "Options" "Décision" "Conséquences" "Vérification" "Références"; do grep -q "^## .*$s" "$f" || echo "MANQUE $s dans $f"; done; done
```

Attendu : aucune ligne `TROP LONG`, aucune ligne `MANQUE`.

- [ ] **Étape 4 : committer**

```bash
git add docs/adr
git commit -m "docs: resserre les dix ADR sur les sept sections du gabarit

Co-Authored-By: Claude Fable 5.1 <noreply@anthropic.com>"
```

---

### Tâche 12 : `README.md` ≤ 150, `CONTEXTE-IA.md` ≤ 60, `CLAUDE.md` § 1 bis à jour

**Files:**
- Modify: `README.md`, `CONTEXTE-IA.md`, `CLAUDE.md`

- [ ] **Étape 1 : mesurer**

```bash
cd /home/luca/git/9-2-2-Env-aspnet/BattleShip && wc -l README.md CONTEXTE-IA.md && dotnet test 2>&1 | grep -oE 'Passed: +[0-9]+'
```

Attendu : `253`, `91`, `Passed: 145`.

- [ ] **Étape 2 : réécrire `README.md` selon ce sommaire et ces budgets**

| Section | Budget | Contenu obligatoire |
|---|---|---|
| Titre + une phrase d'état | 4 | « socle et trois extensions livrés ; partie complète jouable » |
| Binôme | 3 | Luca Ceccarelli, Irwin Ladrette |
| Prérequis | 6 | SDK .NET 10 (`dotnet --version`), navigateur ; certificat approuvé **si** HTTPS |
| Lancer | 18 | deux terminaux ; bloc `http` (5184 / 5210) ; bloc `https` (7050 / 7073) ; **une** note : ne pas mélanger les schémas, un « CORS (null) » = serveur non joint |
| Vérifier | 6 | `dotnet build`, `dotnet test` (nombre lu à l'étape 1), `dotnet format`, `api.http` (le tir passe par gRPC-Web) |
| Règles retenues | 10 | le tableau actuel (grille, flotte, adjacence, touche = on rejoue, placement) + trois niveaux avec leurs moyennes mesurées |
| Fonctionnalités livrées | 18 | une ligne par fonctionnalité : partie complète et nouvelle partie, placement manuel validé serveur, trois niveaux, tir gRPC-Web, FluentValidation partout, trois états UI, secret, historique + rejeu, trois apparences + mode calme, coques SVG, page d'accueil illustrée, clavier + `aria-live` + glyphes |
| Démonstration gRPC-Web | 16 | les 4 étapes et la remarque « erreur en HTTP 200, statut dans les trailers » ; tableau des 4 preuves de `docs/demo/` |
| Arbitrages du backlog | 12 | retenu (3) / écarté (4) avec un motif d'une ligne chacun |
| Limites connues | 20 | Difficile écrasant ; état en mémoire, rechargement perd la partie ; rebuild pendant `dotnet run` casse le front ; Google Fonts ; a11y : pas de lecteur d'écran réel, flèches ne bloquent pas le défilement ; rejeu = coups seuls ; trimming non éprouvé |
| Documentation | 10 | tableau : `docs/conformite.md` (**nouveau**), spec, plans, `docs/demo/`, ADR, `PROMPTS.md`, `REVUE-IA.md`, `CONTEXTE-IA.md`, `CLAUDE.md` |

Supprimer : les paragraphes d'explication sur le certificat « found mais not trusted », la
digression sur la nomenclature des navires (remplacée par « noms canoniques du jeu, tailles
5-4-3-3-2 »), les descriptions littéraires des apparences et de la page d'accueil.

- [ ] **Étape 3 : réécrire `CONTEXTE-IA.md`**

Conserver les huit rubriques du gabarit du cours (`../csharp-school/Ressources Bataille
Navale/CONTEXTE-IA.md`), ≤ 6 lignes chacune. Corrections de fond obligatoires :

- « Vérifications réalisées et limites connues » : les trois points listés comme **non vérifiés**
  (moyennes des stratégies, exceptions par tour, montage gRPC de test) sont **tous vérifiés
  depuis** — les remplacer par : « mesurés : 95,7 / 52,6 / 41,2 coups (revue 2) ; 0 exception
  par tour (revue 1) ; gRPC-Web monté et testé (`FireGrpcTests`) ; non vérifié : lecteur d'écran
  réel, publication trimmée ».
- Tableau des ADR : ajouter 0008 (contrat partagé), 0009 (trois apparences), 0010 (coques).
- « Commandes, ports » : `http` 5184/5210 par défaut, `https` 7050/7073.
- Ajouter une ligne « Code sans commentaires depuis le 2026-09-17 : les décisions vivent dans
  les ADR et `REVUE-IA.md` ».

- [ ] **Étape 4 : actualiser `CLAUDE.md` § 1 bis**

Remplacer toute la section « 1 bis. État actuel du dépôt » (de `## 1 bis.` jusqu'à la ligne
`---` qui précède `## 2.`) par :

```markdown
## 1 bis. État actuel du dépôt

Au 2026-09-17, **le socle et trois extensions sont livrés** : partie complète jouable dans le
navigateur, tir en gRPC-Web avec réponse et erreurs démontrables, trois niveaux d'adversaire
mesurés, historique et rejeu, accessibilité clavier, trois apparences. `global.json` épingle le
SDK 10 ; `dotnet build` sans avertissement ; `dotnet test` au vert.

Le code ne porte **aucun commentaire** (décision du binôme, 2026-09-17) : ce qu'un commentaire
aurait expliqué se trouve dans `docs/adr/`, `REVUE-IA.md` ou `docs/conformite.md`. Ne pas en
réintroduire.

Conformité au sujet : `docs/conformite.md` (une commande par exigence). Conception :
`docs/superpowers/specs/2026-09-15-bataille-navale-design.md` et `docs/adr/0001` à `0010`.

L'état de partie est en mémoire ; la persistance reste du backlog.

---
```

- [ ] **Étape 5 : vérifier**

```bash
wc -l README.md CONTEXTE-IA.md
grep -n 'Counter.razor\|reste à faire\|Reste à faire' CLAUDE.md | wc -l
grep -n 'conformite.md' README.md CLAUDE.md | wc -l
grep -oE 'Passed: +[0-9]+|[0-9]+ tests' README.md
```

Attendu : `≤ 150`, `≤ 60` ; `0` ; `≥ 2` ; le nombre de tests du README égal à celui lu à
l'étape 1.

- [ ] **Étape 6 : committer**

```bash
git add README.md CONTEXTE-IA.md CLAUDE.md
git commit -m "docs: allège le README, actualise CONTEXTE-IA.md et l'état du dépôt dans CLAUDE.md

Co-Authored-By: Claude Fable 5.1 <noreply@anthropic.com>"
```

---

### Tâche 13 : consigner cette session et clore

**Files:**
- Modify: `PROMPTS.md`, `REVUE-IA.md`

- [ ] **Étape 1 : ajouter la neuvième entrée de `PROMPTS.md`**

```markdown
## 2026-09-17 — Code sans commentaires, audit de conformité, livrables condensés
**Contexte** : 142/142 tests, ~1 500 lignes de commentaires, livrables de 639 et 746 lignes.
**Prompt** : « retire tous les commentaires du code (je n'aime pas les commentaires typiques
de l'IA) ; vérifie que tout ce qui est demandé dans le support et toutes ses bonnes pratiques
sont respectés ; mets à jour les fichiers IA et raccourcis-les — trop longs, on ne les lira pas ».
**Réponse** : strip par Roslyn (trivia de commentaire seuls) + regex pour Razor/CSS/proto ;
audit diapo par diapo consigné dans `docs/conformite.md` ; trois écarts trouvés : fichier `.http`
du gabarit, **aucun vainqueur** dans le moteur ni le contrat (diapo 36, déduit par le front),
aucun test d'intégration ne menant une partie à `Finished`.
**Décision** : acceptée — commentaires retirés y compris la documentation XML ; `Game.Winner`
et `GameDto.Winner` ajoutés en TDD ; test de partie complète par gRPC-Web ajouté.
**Vérification** : `dotnet build` (0 warning), `dotnet test` (142 → 145), greps de résidus à 0,
`docs/conformite.md` rejouable ligne à ligne.
**Preuve / limite** : commits de la journée ; limite : les greps de conventions détectent les
formes, pas l'intention — la lisibilité sans commentaire n'est établie que par relecture.
```

- [ ] **Étape 2 : ajouter la revue 7 à `REVUE-IA.md`**

```markdown
## Revue 7 — Le vainqueur déduit par le front — rejetée puis corrigée
**Proposition** : `Play.razor`, `PlayerWon(game) => game.Opponent.SunkShips.Count == game.Fleet.Count`
(commit `ed4cb56`) : la victoire est une règle calculée côté client.
**Hypothèse à vérifier** : la diapo 36 exige que le moteur identifie le gagnant ; l'ADR 0007
exige que le front ne décide aucune règle. Les deux sont violées si `Game` ne porte pas de vainqueur.
**Expérience** : `grep -n Winner BattleShip.Models/Game.cs BattleShip.Models/Contracts/GameDto.cs`
— attendu si conforme : au moins une propriété ; erreur détectable : une règle de fin de partie
qui n'existe que dans l'interface.
**Observation** : 0 occurrence hors commentaire. Après correction :
`The_shooter_who_sinks_the_last_ship_is_the_winner` et
`A_game_played_to_the_end_finishes_with_the_player_as_winner` au vert ; le second, lancé avec
`"Opponent"` à la place de `"Human"`, échoue (`Expected: Opponent / Actual: Human`) — il discrimine.
**Décision** : corrigée — `Game.Winner : Player?`, `GameDto.Winner : string?`, front qui lit la donnée.
**Preuves et limites** : commits de la tâche 6 et 7 ; limite : `FireResponse` (gRPC) ne porte
pas le vainqueur, la page relit l'état par HTTP après chaque échange.
```

Mettre à jour la ligne de bilan de l'en-tête : « sept revues : 2 acceptées, 3 adaptées,
1 rejetée, 1 corrigée ».

- [ ] **Étape 3 : contrôle final complet**

```bash
cd /home/luca/git/9-2-2-Env-aspnet/BattleShip
dotnet build 2>&1 | grep -E 'Warning\(s\)|Error\(s\)'
dotnet test 2>&1 | tail -1
dotnet format --verify-no-changes ; echo "format exit=$?"
grep -rnE '^\s*(//|///)|/\*|@\*' --include='*.cs' --include='*.razor' --include='*.css' --include='*.proto' BattleShip.API BattleShip.App BattleShip.Models BattleShip.Tests | grep -v '/obj/' | grep -v '/lib/' | wc -l
wc -l PROMPTS.md REVUE-IA.md README.md CONTEXTE-IA.md docs/conformite.md docs/adr/*.md
git status --short | wc -l
```

Attendu : `0 Warning(s)` · `Passed! - Failed: 0, Passed: 145` · `format exit=0` · `0` ·
tous les budgets tenus (`PROMPTS` ≤ 160, `REVUE` ≤ 200, `README` ≤ 150, `CONTEXTE` ≤ 60,
`conformite` ≤ 70, chaque ADR ≤ 70) · puis `2` fichiers modifiés (les deux livrables).

- [ ] **Étape 4 : committer**

```bash
git add PROMPTS.md REVUE-IA.md
git commit -m "docs: consigne la session du 17 septembre et la revue du vainqueur

Co-Authored-By: Claude Fable 5.1 <noreply@anthropic.com>"
git log --oneline -9
```

Attendu : neuf commits depuis `31073e1`, tous au format `type: sujet` en français.

---

## Auto-revue du plan

**Couverture de la demande.** (1) « Retirer tous les commentaires » → tâches 1-3, y compris
XML, Razor, CSS, proto et `.http` ; hypothèse explicite : la documentation XML `///` est un
commentaire et part aussi. (2) « Vérifier ce qui est demandé » → tâche 4 (contraintes, diapos
6/15/19/28) et tâches 6-7 (spécifications 1-4, diapos 36-38/46) ; « vérifier les bonnes
pratiques » → tâche 8 (diapos 4, 24-27, 31-35, 39, 43-45, 49-51 et CLAUDE.md § 2-3). Les trois
écarts constatés pendant la préparation du plan ont chacun une tâche de correction (5, 6, 7).
(3) « Mettre à jour et raccourcir les fichiers IA » → tâches 9-12, avec des budgets vérifiés par
`wc -l`, et tâche 13 pour que la session elle-même laisse une trace (CLAUDE.md § 5).

**Cohérence des types.** `Game.Winner : Player?` (tâche 6) → `game.Winner?.ToString()` dans
`DtoMappings` → `GameDto.Winner : string?` en neuvième position → `final.Winner == "Human"`
dans le test de la tâche 7 et `game.Winner == "Human"` dans `Play.razor`. `IGameStore.Read<T>`
renvoie `Result<T>`, d'où `.Value` dans la tâche 7. `FireResponse.Status` est une `string`
(`"InProgress" | "Finished"`, proto).

**Ordre.** Les commentaires partent avant l'audit, pour que les greps de conventions (accents,
`Random.Shared`) ne soient pas pollués par du texte de commentaire ; les corrections de code
précèdent la condensation des livrables, pour que README et `CONTEXTE-IA.md` décrivent l'état
final (nombre de tests, `docs/conformite.md`).

**Ce que le plan ne fait pas.** Il ne réécrit pas l'historique Git (commit `init`), ne touche
ni à `docs/superpowers/` ni à `.superpowers/sdd/` (traces de travail, pas livrables), et
n'ajoute pas `winner` au message `FireResponse` du contrat gRPC — la page relit l'état par HTTP
après chaque échange, ce qui suffit ; l'ajouter reste possible sans casser le contrat
(nouveau numéro de champ).
