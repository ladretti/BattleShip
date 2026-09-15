# CLAUDE.md — TP Bataille Navale (C# / ASP.NET Core)

Ce fichier cadre le travail de l'IA sur ce dépôt. Il est dérivé du support
`csharp-school/Cours C# ASP.NET - Bataille Navale - autonomie.pptx` (Christophe MOMMER,
HTS Learning) et des gabarits de `csharp-school/Ressources Bataille Navale/`.

**Règle d'or : produire du code n'est qu'une partie du travail. Toute session qui touche
au code doit aussi produire les livrables IA de la section « Livrables obligatoires »
(diapo 11 du support). Un commit de code sans trace de décision ni de vérification est
un travail incomplet.**

---

## 1. Le projet

Une bataille navale jouable du navigateur jusqu'au serveur.

Quatre projets dans une solution `BattleShip` :

| Projet              | Rôle                                       | Dépendances                |
|---------------------|--------------------------------------------|----------------------------|
| `BattleShip.API`    | Minimal API ASP.NET Core (.NET 10)         | → `BattleShip.Models`      |
| `BattleShip.App`    | Front Blazor WebAssembly                   | → `BattleShip.Models`      |
| `BattleShip.Models` | Modèles partagés + moteur de jeu           | **aucune**                 |
| `BattleShip.Tests`  | xUnit : tests métier et d'intégration      | → `BattleShip.API`         |

`BattleShip.Models` ne dépend d'aucun autre projet. Le moteur de jeu reste indépendant
de HTTP, de JSON et de gRPC : il doit être testable sans lancer de serveur.

### Contraintes imposées (non négociables)

- .NET 10 (LTS). `global.json` à la racine ; vérifier avec `dotnet --version`.
- API en **Minimal API** (pas de contrôleurs MVC).
- Front **Blazor WebAssembly**.
- Bibliothèque de modèles partagée.
- Partie complète jouable contre l'ordinateur (création → victoire).
- **FluentValidation** sur toutes les entrées serveur (HTTP *et* gRPC).
- **gRPC-Web** fonctionnel sur au moins un échange, avec une réponse *et* une erreur
  attendue démontrables depuis le navigateur.
- Tests métier **et** tests d'intégration.
- Livrables IA (section 5) et historique Git exploitable.

### Choix libres (à justifier en ADR)

Règles du jeu, taille de la grille, composition de la flotte, représentations internes,
organisation du code, stockage, algorithme de l'adversaire, interface et UX, extensions.

Le socle n'est qu'un point de départ : l'ambition et la pertinence du périmètre livré
sont évaluées. Proposer des extensions (adversaire élaboré, multijoueur, sauvegarde,
historique, statistiques, accessibilité, déploiement…) plutôt que s'en tenir au minimum.

---

## 1 bis. État actuel du dépôt et amorçage

Au 2026-09-15, **rien n'est encore échafaudé** : aucun `.slnx`, aucun `.csproj`, aucun
fichier source. Le dépôt contient uniquement `csharp-school/`, qui est un **clone du
dépôt amont du cours** (`github.com/christophe-mommer/csharp-school`) — ce sont les
ressources pédagogiques, pas le projet.

Deux points à traiter avant d'écrire du code :

- La racine `/home/luca/git/9-2-2-Env-aspnet` **n'est pas un dépôt Git**. Elle doit le
  devenir (l'historique est noté). Le `.git/` interne de `csharp-school/` entrera en
  conflit : l'exclure via `.gitignore`, le déplacer hors du dépôt rendu, ou en faire un
  sous-module — décision à consigner en ADR.
- SDK installé sur cette machine : **10.0.401** (conforme à `global.json`, qui épingle
  `10.0.100` en `rollForward: latestFeature`).

Amorçage (diapo 28) — copier d'abord `global.json` à la racine :

```bash
cp "csharp-school/Ressources Bataille Navale/global.json" .
dotnet --version                                  # doit afficher 10.x
dotnet new gitignore
dotnet new sln -n BattleShip
dotnet new webapi     -n BattleShip.API           # Minimal API par défaut
dotnet new blazorwasm -n BattleShip.App
dotnet new classlib   -n BattleShip.Models
dotnet new xunit      -n BattleShip.Tests
dotnet sln add BattleShip.API BattleShip.App BattleShip.Models BattleShip.Tests
dotnet add BattleShip.API   reference BattleShip.Models
dotnet add BattleShip.App   reference BattleShip.Models
dotnet add BattleShip.Tests reference BattleShip.API
dotnet build && dotnet test
```

Copier ensuite les gabarits de livrables à la racine (`PROMPTS.md`, `REVUE-IA.md`,
`docs/adr/0001-modele.md`, `api.http`) et compléter `CONTEXTE-IA.md`.

Pas de base de données imposée : l'état de partie est en mémoire par défaut. La
persistance est une piste de backlog, pas une contrainte du socle.

---

## 2. Conventions C# attendues

Le piège principal : la syntaxe ressemble à Java, **les conventions sont différentes**.

### Nommage

- `PascalCase` : types, méthodes, propriétés publiques, constantes, méthodes async.
- `camelCase` : paramètres, variables locales.
- `_camelCase` : champs privés.
- `IPascalCase` : interfaces.
- Suffixe `Async` sur les méthodes retournant `Task` / `Task<T>`.
- Noms de tests en français descriptif, style `Un_prix_negatif_est_refuse`.

### Propriétés, pas de getters/setters

```csharp
// NON — style Java
private string _name;
public string GetName() => _name;
public void SetName(string value) => _name = value;

// OUI — style C#
public string Name { get; set; }
public required string Label { get; init; }   // obligatoire à l'initialisation
public bool IsSunk => _hits >= Size;          // propriété calculée
```

### Types : choisir selon les données et les responsabilités

```csharp
// Record positionnel pour les DTO : égalité par valeur, concis.
public record ShotDto(int X, int Y);

// Classe pour les entités avec comportement et état mutable.
public sealed class Ship
{
    public required string Name { get; init; }
    public required int Size { get; init; }
    public bool IsSunk => Hits >= Size;
    public int Hits { get; private set; }
}
```

Attention : un `record` peut être mutable, et `init` ne rend pas immuable le *contenu*
d'une collection. Ne pas supposer l'immuabilité sans l'avoir établie.

### Le reste qui compte

- Lambdas avec `=>` (jamais `->`).
- Asynchrone : `async` / `await` et `Task<T>` (pas de `CompletableFuture`, pas de
  `.Result` ni `.Wait()` — ils bloquent et provoquent des interblocages).
- `var` uniquement quand le type est évident à la lecture ; le typage reste statique.
- Héritage et interfaces partagent `:` → `public sealed class A : B, IC`.
- **Nullable activé** : `string?` pour ce qui peut être nul, et traiter les
  avertissements du compilateur plutôt que les faire taire.
- `sealed` par défaut sur les classes non prévues pour l'héritage.
- **Constructeurs primaires** pour l'injection de dépendances :

```csharp
public sealed class GameEngine(IGameStore store, TimeProvider clock)
{
    public async Task<Game> LoadAsync(Guid id) =>
        await store.FindAsync(id) ?? throw new KeyNotFoundException("Partie inconnue");
}
```

### LINQ, collections, pattern matching

```csharp
var hits = shots.Where(s => s.IsHit).ToList();
var remaining = ships.Count(s => !s.IsSunk);
var target = cells.FirstOrDefault(c => c.State == CellState.Unknown);

var status = remaining switch
{
    0 => GameStatus.Finished,
    _ => GameStatus.InProgress
};

if (game is { Status: GameStatus.InProgress, CurrentPlayer: Player.Human })
{
    // ...
}
```

### C# 14 (facultatif, à choisir pour la lisibilité)

Mot-clé `field`, affectation null-conditionnelle `x?.Prop = v`, membres d'extension.
À n'utiliser que si cela rend le code plus clair, jamais pour faire moderne.

### Gestion des erreurs

Lever des exceptions explicites et typées côté domaine (`ArgumentOutOfRangeException`,
`InvalidOperationException`, `KeyNotFoundException`) ; les traduire en statuts HTTP ou
en `RpcException` à la frontière. Ne jamais avaler une exception silencieusement.

---

## 3. Conventions ASP.NET Core / Blazor / gRPC

### Minimal API

```csharp
app.MapGet("/games/{id:guid}", IResult (Guid id, IGameStore store) =>
    store.Find(id) is { } game
        ? Results.Ok(game.ToDto())
        : Results.NotFound());
```

- Contraintes de route (`{id:guid}`, `{x:int}`) plutôt que du parsing manuel.
- Attributs `[FromRoute]` / `[FromBody]` / `[FromQuery]` / `[FromServices]` quand la
  source d'un paramètre n'est pas évidente.
- Statuts cohérents : `200` succès, `201` création, `204` succès sans contenu,
  `400` requête invalide, `404` introuvable, `409` conflit avec l'état courant.
- `TypedResults` plutôt que `Results` quand le type de retour est connu.
- `builder.Services.AddOpenApi()` + `app.MapOpenApi()` en développement (le modèle
  .NET 10 n'embarque plus Swagger UI).
- Maintenir un fichier `api.http` versionné pour les essais manuels.

### Injection de dépendances — choisir la durée de vie

- `Singleton` : une instance pour l'application (ex. `TimeProvider.System`, un store
  en mémoire partagé).
- `Scoped` : une instance par requête HTTP.
- `Transient` : une instance à chaque résolution.

Justifier le choix quand il porte de l'état.

### FluentValidation

Un validateur par entrée, enregistré en `Scoped`, **appelé explicitement** dans
l'endpoint :

```csharp
public sealed class ShotInputValidator : AbstractValidator<ShotInput>
{
    public ShotInputValidator()
    {
        RuleFor(s => s.X).InclusiveBetween(0, 9);
        RuleFor(s => s.Y).InclusiveBetween(0, 9);
    }
}

app.MapPost("/games/{id:guid}/shots", async Task<IResult> (
    Guid id, ShotInput input, IValidator<ShotInput> validator) =>
{
    var check = await validator.ValidateAsync(input);
    if (!check.IsValid)
        return TypedResults.ValidationProblem(check.ToDictionary());
    // ...
});
```

La liaison des paramètres et leur validation sont **deux responsabilités distinctes**.
Les règles du jeu sont vérifiées **côté serveur**, jamais seulement dans le front.

### Blazor WebAssembly

- Un composant `.razor` = données + rendu + événements.
- `OnInitializedAsync` pour le chargement initial, `OnParametersSetAsync` pour les
  changements de paramètres, `OnAfterRenderAsync` après rendu.
- Toujours gérer les trois états : **chargement**, **succès**, **échec**. Un incident
  de communication ne doit pas rendre l'interface inutilisable.
- `HttpClient` avec `BaseAddress` configurée ; sérialisation via `System.Net.Http.Json`.
- **Secret du jeu** : le front ne doit jamais recevoir les positions adverses non
  découvertes. C'est une règle de conception du contrat, pas un détail d'affichage.

### CORS

L'API et le front sont servis sur des ports différents, donc des origines distinctes.
Déclarer explicitement les origines, méthodes et en-têtes autorisés, et **vérifier dans
le navigateur** (console F12, onglet Réseau). CORS ne remplace ni l'authentification ni
les autorisations.

### gRPC-Web

- Contrat dans `Protos/*.proto`, `csharp_namespace` défini, numéros de champs stables.
- API : `Grpc.AspNetCore`, `Grpc.AspNetCore.Web`, `<Protobuf ... GrpcServices="Server" />`.
- App : `Grpc.Net.Client`, `Grpc.Net.Client.Web`, `Google.Protobuf`, `Grpc.Tools`
  (`PrivateAssets="all"`), `GrpcServices="Client"`.
- Serveur : `AddGrpc()`, puis `app.UseGrpcWeb()` et
  `app.MapGrpcService<T>().EnableGrpcWeb()`.
- Client : `GrpcChannel.ForAddress(..., new GrpcChannelOptions { HttpHandler = new GrpcWebHandler(new HttpClientHandler()) })`.
- Valider aussi les requêtes gRPC ; traduire les échecs en
  `RpcException(new Status(StatusCode.InvalidArgument, ...))` / `StatusCode.NotFound`.

### Tests (xUnit)

- `[Fact]` pour un cas, `[Theory]` + `[InlineData]` pour des jeux de données.
- Tests **métier** sur `BattleShip.Models` (sans serveur) et tests **d'intégration** sur
  les endpoints.
- Un test doit pouvoir **échouer** : il doit distinguer une implémentation correcte
  d'une implémentation fautive. Tester les invariants et les cas de refus
  (chevauchement, débordement, case rejouée, coup après fin de partie), pas seulement
  le chemin nominal.
- Après une correction : vérifier que le test échouait **avant** et réussit **après**.

---

## 4. Commandes

```bash
dotnet --version                                  # doit afficher 10.x
dotnet build
dotnet test
dotnet run --project BattleShip.API
dotnet watch --project BattleShip.App
dotnet format                                     # avant de committer
dotnet dev-certs https --trust                    # certificat HTTPS local

# Diagnostic
dotnet build -v normal
dotnet restore
dotnet test --logger "console;verbosity=detailed"

# Vérifier un comportement en isolation (.NET 10, hors dossier de projet)
dotnet run --file essai.cs
```

`dotnet run --file` est l'outil privilégié pour **confronter une affirmation à une
exécution réelle** avant de l'intégrer.

---

## 5. Livrables obligatoires — à produire à CHAQUE session de travail

**C'est la partie que l'IA doit faire en plus du code.** Ces livrables sont notés
(diapos 11, 61, 62). Ne jamais terminer une session de travail sans les avoir mis à
jour. Ils vivent à la racine du dépôt.

### 5.1 `PROMPTS.md` — les échanges décisifs

Une entrée par échange **qui a compté** (pas tous les échanges). Après toute
contribution significative, ajouter :

```markdown
## <date> — <sujet de l'échange>

- Outil / modèle si connu : Claude Code (claude-opus-5)
- Contexte : besoin, contraintes et code concerné
- Prompt réellement utilisé : <la demande effective>
- Réponse et hypothèses résumées : <la proposition et ce qu'elle suppose vrai>
- Décision et justification : acceptée / adaptée / rejetée — pourquoi
- Scénario ou commande de vérification : <ce qui a été exécuté>
- Résultat attendu, puis résultat observé : <avant exécution, puis après>
- Erreur que ce contrôle pourrait détecter : <ce que le test discrimine>
- Preuves reproductibles et limites : <commit, test, ce qui reste non vérifié>
```

### 5.2 `docs/adr/NNNN-<intitule>.md` — les décisions d'architecture

Un ADR par **choix structurant** : représentation de la grille, stockage de l'état,
découpage du contrat d'API, choix de l'échange gRPC, algorithme de l'adversaire,
gestion de l'état côté Blazor… Statut mis à jour, historique conservé.

```markdown
# ADR NNNN : <intitulé de la décision>

## Statut et date
Proposé / Accepté / Remplacé par ADR NNNN — <date>

## Contexte
Besoin à satisfaire, contraintes et enjeu de la décision.

## Options envisagées
Alternatives crédibles, avantages, limites et critères de comparaison.

## Décision
Option retenue et raisons du choix dans ce contexte.

## Conséquences
Effets attendus, compromis, risques et travail induit.

## Vérification et réexamen
Éléments qui étayent le choix ; conditions qui conduiraient à le revoir.

## Références
Documentation, issue, expérience ou commit.
```

### 5.3 `REVUE-IA.md` — trois revues argumentées minimum

Une revue = une proposition de l'IA **acceptée, adaptée ou rejetée**, étayée par un
contrôle pertinent et reproductible. Une proposition *acceptée* exige elle aussi une
preuve.

```markdown
## Revue : <sujet précis du projet>

- Proposition et référence dans le dépôt : <fichier:ligne, commit>
- Hypothèse à vérifier : ce qui doit être vrai pour que ce soit acceptable
- Scénario, données ou commande : <l'expérience>
- Résultat attendu avant exécution : <énoncé AVANT de lancer>
- Erreur que ce contrôle pourrait détecter : <le pouvoir discriminant>
- Résultat réellement observé : <les faits>
- Décision et justification : acceptée / adaptée / rejetée
- Preuves reproductibles et liens vers les commits :
- Après correction éventuelle : résultat avant / après :
- Limites et points non vérifiés :
```

### 5.4 `README.md`

Noms du binôme, prérequis, **commandes exactes de lancement**, fonctionnalités livrées,
arbitrages du backlog (ce qui a été écarté et pourquoi), limites connues.
Critère : un autre binôme doit pouvoir lancer le projet **en suivant uniquement le
README**.

### 5.5 Historique Git

Commits qui identifient le travail effectué, en français, un sujet par commit.
L'historique fait partie de la note et doit rester lisible.

---

## 6. Discipline de vérification

> Compiler et réussir une démonstration ne prouvent pas tous les comportements.
> Le code généré est plausible ; c'est à nous d'établir qu'il répond au besoin.

Règles de travail pour l'IA sur ce dépôt :

1. **Nommer les hypothèses.** Avant de proposer du code, dire explicitement ce qu'il
   suppose vrai (version d'API, forme des données, invariant du moteur).
2. **Concevoir une expérience qui peut échouer.** Décrire le résultat attendu *avant*
   d'exécuter, puis exécuter, puis comparer. Un contrôle qui ne peut pas distinguer une
   implémentation correcte d'une fautive ne prouve rien.
3. **Vérifier contre la documentation .NET 10**, pas contre la mémoire. `dotnet new
   <modèle> --help` liste les options réelles ; `dotnet run --file` confronte une
   affirmation à une exécution.
4. **Distinguer trois registres** dans tout compte rendu : ce qui a été *constaté*, ce
   qui est *supposé*, ce qui *reste à vérifier*. Ne jamais annoncer « ça marche » sans
   la commande et sa sortie.
5. **Ne pas s'auto-déclarer correct.** Une affirmation de l'IA n'est pas une
   vérification. Une référence à une doc ne prouve pas que l'API est bien employée
   dans ce contexte.
6. **Rapporter fidèlement.** Si des tests échouent, le dire avec la sortie. Si une
   étape a été sautée, le dire.

### Protocole de blocage (diapo 22)

Lire l'erreur entière (fichier, ligne, code) → comparer obtenu vs attendu → isoler dans
un test ou un exemple minimal → consulter la documentation de la **bonne version** →
dans le navigateur, console F12 + onglet Réseau + logs serveur. Ne pas relancer la même
tentative sans diagnostic.

---

## 7. Ce que l'IA ne fait pas à la place du binôme

- Elle ne **décide pas** du périmètre, des règles du jeu ni des priorités : elle
  propose des options et leurs conséquences, le binôme tranche.
- Elle ne **remplit pas** les livrables avec du contenu inventé : PROMPTS.md,
  les ADR et REVUE-IA.md consignent des échanges, décisions et observations **réels**.
  Si une vérification n'a pas été exécutée, l'écrire comme non vérifiée.
- Elle ne **conclut pas** à la place du binôme : chaque membre doit pouvoir expliquer
  le fonctionnement, le périmètre choisi et les limites du projet.
- Elle **signale** le code qu'elle a généré, pour qu'il soit identifiable et compris.
- Aucun secret ni donnée personnelle dans les prompts : utiliser des données d'exemple.

---

## 8. Checklist avant de rendre (diapo 63)

- [ ] Le README suffit à lancer le projet avec les prérequis indiqués.
- [ ] Une partie complète se joue, y compris la fin et la création d'une autre partie.
- [ ] Un échange gRPC-Web **et une erreur attendue** sont démontrables.
- [ ] Les entrées HTTP et gRPC sont validées ; les règles sont vérifiées côté serveur.
- [ ] Les tests passent **et** détectent des règles violées, pas seulement le nominal.
- [ ] `PROMPTS.md`, les ADR et les trois revues IA sont à jour et reliés aux preuves.
- [ ] Les changements ont été relus ; les commits identifient le travail effectué.
- [ ] Chaque membre explique le fonctionnement, le périmètre et les limites.
- [ ] Le code généré automatiquement est identifié et son rôle est compris.

Remise : lien du dépôt + hash du commit à `contact@hts-learning.com` avant le début du
QCM (jour 5). Seul ce commit, poussé avant le QCM, est évalué.

---

## 9. Références

- Support : `csharp-school/Cours C# ASP.NET - Bataille Navale - autonomie.pptx`
- Référentiel texte : `csharp-school/Ressources Bataille Navale/Referentiel.md`
- Gabarits : `PROMPTS.md`, `REVUE-IA.md`, `docs/adr/0001-modele.md`, `global.json`,
  `api.http`, `Exemples/` dans `csharp-school/Ressources Bataille Navale/`
- [Blazor — cycle de vie](https://learn.microsoft.com/en-us/aspnet/core/blazor/components/lifecycle?view=aspnetcore-10.0)
- [gRPC-Web](https://learn.microsoft.com/en-us/aspnet/core/grpc/grpcweb?view=aspnetcore-10.0)
- [CORS](https://learn.microsoft.com/en-us/aspnet/core/security/cors?view=aspnetcore-10.0)
- [FluentValidation avec ASP.NET](https://docs.fluentvalidation.net/en/latest/aspnet.html)
