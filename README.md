# Bataille Navale — C# / ASP.NET Core

TP d'autonomie C# ASP.NET (HTS Learning, Christophe MOMMER). **État au 2026-09-22 : le socle, les trois
extensions du 2026-09-17, le journal d'événements (ADR 0011) et l'arrière-plan 3D réactif (ADR 0012) sont
livrés ; une partie complète se joue dans le navigateur, de la création à la victoire, avec rejeu fidèle et
reprise après rechargement.**

## Binôme

- Luca Ceccarelli
- Irwin Ladrette

## Prérequis

- **.NET SDK 10.x** — vérifier avec `dotnet --version` ; version épinglée par `global.json`.
- Un navigateur récent : la démonstration gRPC-Web se lit dans la console F12.
- **Pour le profil HTTPS uniquement** : certificat de développement *approuvé* — `dotnet dev-certs https --check --trust`.

## Lancer le projet

L'API et le front sont deux applications distinctes, à lancer dans **deux terminaux**. Chaque projet a deux profils,
`http` et `https` ; `dotnet run` sans option prend le premier, `http`. **Prendre le même profil des deux côtés.**

```bash
# HTTP (profil par défaut, aucun certificat) — terminal 1, puis terminal 2
dotnet run --project BattleShip.API                          # http://localhost:5184
dotnet run --project BattleShip.App                          # http://localhost:5210  ← ouvrir

# HTTPS (certificat approuvé requis) — terminal 1, puis terminal 2
dotnet run --project BattleShip.API --launch-profile https   # https://localhost:7050
dotnet run --project BattleShip.App --launch-profile https   # https://localhost:7073  ← ouvrir
```

> **Ne pas mélanger les deux schémas.** Une page https ne peut pas appeler une API http, et le front prend l'adresse
> de l'API d'après son propre schéma (`wwwroot/appsettings.json`, section `Api`). Un « échec CORS » avec un code
> d'état **`(null)`** n'est pas un refus CORS — un refus renvoie un code : `(null)` veut dire qu'aucune réponse n'est
> arrivée (serveur non démarré, mauvais profil, certificat non approuvé). L'API autorise explicitement les deux
> origines du front (`Cors:Origins`) et expose les en-têtes gRPC-Web.

## Vérifier

```bash
dotnet build      # 0 avertissement
dotnet test       # 191 tests
dotnet format     # avant tout commit
```

Essais manuels des endpoints HTTP : `api.http` à la racine — `@api = http://localhost:5184`,
`@apiHttps = https://localhost:7050`. **Le tir n'y figure pas** : il passe par gRPC-Web (ADR 0005).

## Règles du jeu retenues

| Règle | Valeur |
|---|---|
| Grille | paramétrable, défaut **10×10** |
| Flotte | paramétrable, défaut **5-4-3-3-2** — noms canoniques du jeu : `Carrier`, `Battleship`, `Cruiser`, `Submarine`, `Destroyer` |
| Navires adjacents | **interdits**, diagonales comprises |
| Enchaînement des tours | **touche = on rejoue** |
| Placement | joueur **manuel**, validé côté serveur ; adversaire automatique |
| Niveaux d'adversaire | **Facile** aléatoire, **Normal** chasse/cible, **Difficile** densité — 95,7 / 52,6 / 41,2 coups en moyenne (mesurés, revue 2) |

## Fonctionnalités livrées

- **Partie complète** contre l'ordinateur, de la création à la fin — **le serveur désigne le vainqueur**
  (`GameDto.Winner`) — puis nouvelle partie sans recharger la page.
- **Placement manuel** de la flotte : prévisualisation verte/rouge côté front, refus motivé côté serveur (débordement,
  chevauchement, adjacence).
- **Trois niveaux d'adversaire** remplaçables, choisis à la création.
- **Tir en gRPC-Web** (`BattleService/Fire`) ; création, état, placement et historique en HTTP.
- **FluentValidation** sur toutes les entrées serveur, HTTP et gRPC, appelée explicitement.
- Les **trois états** — chargement, succès, échec — sur chaque page : couper l'API affiche un motif exploitable et
  laisse l'interface utilisable.
- **Le secret** : aucune position adverse non découverte n'atteint le front, et l'adversaire ne reçoit aucun chemin
  vers la grille du joueur (`ShotHistory`, revue 1).
- **Historique et rejeu** : coups ordonnés des deux camps et curseur de rejeu, rendu seul, sans requête ni effet sur
  l'état serveur.
- **Trois apparences** commutables (*Hydrographic*, *Steel*, *Tabletop*) et un réglage **Impacts complet ou calme**,
  mémorisés entre deux visites (ADR 0009).
- **Journal d'événements** (`GET /games/{id}/events`) comme source de vérité unique du moteur (ADR 0011) : un
  seul fait écrit une seule fois, plutôt que trois écritures dupliquées à chaque tir.
- **Arrière-plan 3D réactif** (ADR 0012) : mer, flotte du joueur et épaves rendues avec Three.js, vendorisé
  dans `wwwroot/lib/` — aucun prérequis nouveau, rien à installer. Halo pulsé à la case impactée, scène
  figée sous mouvement réduit.
- **Rejeu fidèle** : le curseur reconstitue l'état « coulé » réellement atteint à chaque instant, y compris en
  cours de partie sur un journal censuré — plus seulement la liste des coups.
- **Révélation de la flotte adverse dans le journal** : `GET /games/{id}/events` expose les positions
  adverses une fois `GameEnded` reçu, jamais avant. Limite d'écran : le rejeu ne dessine que les
  navires adverses coulés (coques révélées par une touche) ; un navire adverse survivant, y compris
  après une partie perdue, n'est jamais dessiné.
- **Reprise après rechargement de page** : l'identifiant de la partie en cours survit à un rechargement
  (`localStorage`), avec repli propre si la partie n'existe plus côté serveur.
- **Coques dessinées en SVG** par géométrie, franchissant les gouttières entre les cases ; dégâts marqués sur la
  coque, épave conservée (ADR 0010).
- **Page d'accueil illustrée** en SVG, `aria-hidden`, figée en mode calme et sous `prefers-reduced-motion`.
- **Accessibilité** : les deux grilles au clavier seul (flèches, `Début`/`Fin`, `Entrée`, `R`), région `aria-live`, un
  **glyphe** par état en plus de la couleur, contrastes mesurés.

## Démonstration gRPC-Web

Dans le navigateur, console F12 ouverte sur l'onglet **Réseau**, filtre `Fire`.

1. Créer une partie (`/`), poser les cinq navires, puis **Confirm fleet**.
2. **Réponse attendue** — cliquer une case vierge de « Their waters » : un `POST /battleship.BattleService/Fire` en
   `HTTP 200`, `application/grpc-web` ; le coup s'affiche, puis la riposte adverse se déroule.
3. **Erreur attendue nº 1** — recliquer **la même case** : `grpc-status: 3` (`InvalidArgument`),
   `grpc-message: CellAlreadyShot`.
4. **Erreur attendue nº 2** — déplier **gRPC-Web diagnostics** en bas de la page de jeu, puis **Fire at an unknown
   game** : `grpc-status: 5` (`NotFound`), `grpc-message: GameNotFound`.

> Une erreur gRPC-Web revient en **`HTTP 200`** : le statut voyage dans les *trailers* (`grpc-status`,
> `grpc-message`), pas dans le code HTTP — d'où l'exposition explicite de ces en-têtes par CORS.

| Preuve | Contenu |
|---|---|
| `docs/demo/01-fire-success.png` | le tir accepté |
| `docs/demo/02-invalid-argument.png` | `InvalidArgument` / `CellAlreadyShot` |
| `docs/demo/03-not-found.png` | `NotFound` / `GameNotFound` |
| `docs/demo/grpc-web-trace.md` | la trace réseau des trois appels et leurs `grpc-status` |

## Arbitrages du backlog

**Retenu** — duel d'IA mesuré (`BattleShip.API/Benchmark/StrategyBenchmark.cs`, preuve chiffrée que les trois niveaux
diffèrent) ; historique et rejeu (`GET /games/{id}/history` et le panneau de rejeu) ; accessibilité (clavier,
annonces, glyphes, contrastes).

**Écarté** :

- **Persistance SQLite / EF** — infrastructure qui ne sert ni le moteur ni l'adversaire ; `IGameStore` rend le
  changement local le jour où il devient utile. Le journal d'événements (ADR 0011) **la rendrait directe** :
  un flux d'ajout seul se sérialise simplement, mais seule la sérialisation des DTO a été éprouvée, pas celle
  des événements de domaine — écartée quand même, par décision du binôme, faute de besoin réel.
- **Concurrence optimiste** (`Append(id, expectedVersion, events)`, la forme « canonique » de l'event sourcing)
  — examinée puis écartée à l'ADR 0011 : elle résout un problème déjà résolu par le verrou par partie de
  l'ADR 0002, testé et correct dans un store mono-processus en mémoire.
- **Multijoueur** (sessions et temps réel) et **déploiement** — hors périmètre, sans valeur ajoutée pour l'évaluation.
- **Placement automatique du joueur** — le placement manuel démontre mieux la validation serveur.

## Limites connues

- **Le niveau Difficile est écrasant** : la densité probabiliste combinée à « touche = on rejoue » enchaîne 4 à 5 coups
  dès qu'elle touche. **Normal est le mode jouable, Difficile une démonstration d'algorithme** (ADR 0006).
- Les moyennes (200 parties par stratégie, graine 20260915) ne valent que pour cette flotte et cette grille ;
  `Density` reste sous le seuil de 55 coups fixé avant mesure (revue 2).
- **État en mémoire** : redémarrer l'API perd les parties — la persistance est restée hors périmètre par
  décision du binôme (arbitrages du backlog ci-dessus), alors que le journal d'événements la rendrait triviale.
- **`localStorage` est partagé par ORIGINE, pas par onglet** : un second onglet restaure la même partie que le
  premier et les deux pilotent le même identifiant en parallèle — désynchronisation d'affichage dès que l'un des
  deux tire. `sessionStorage` isolerait par onglet au prix de ne pas survivre à la fermeture de l'onglet ;
  arbitrage non tranché par le binôme.
- **Reconstruire pendant que `dotnet run` tourne casse le front** : les empreintes de `_framework/` changent, la page
  reste blanche avec un `404` sur `dotnet.<hash>.js`. Remède : relancer `dotnet run --project BattleShip.App`.
- **Polices Google Fonts** : sans réseau, la pile de secours s'applique et les trois apparences se ressemblent.
- **Accessibilité** : les contrastes sont calculés, mais **aucun lecteur d'écran réel n'a été essayé** ; les flèches
  ne suppriment pas le défilement de la page ; la grille du joueur est lue case par case, pas parcourue.
- La révélation de la flotte adverse en fin de partie est couverte par un test d'intégration
  (`After_the_game_ends_the_full_journal_is_served`) mais **n'a pas été jouée à l'écran** — terminer une partie
  manuellement demande une vingtaine de tirs.
- La sérialisation JSON du front repose sur la réflexion ; son comportement sous **publication trimmée** est inconnu.
- **Arrière-plan 3D, secret** : pendant une partie en cours, l'arrière-plan ne montre que la mer, la flotte du
  joueur et les épaves — le secret l'interdit autrement ; la cascade de naufrages est une scène de fin de
  partie ou de rejeu.
- **Arrière-plan 3D, recul des épaves** : le recul visuel des épaves avec le curseur de rejeu est **déduit,
  non observé** — aucun navire adverse n'a été coulé dans les parties mesurées à l'écran. Il est couvert par
  les tests de `ScenePlanTests` et par le rendu identique des navires, vérifié au pixel — composition de deux
  parties prouvées, pas une observation directe.
- **Arrière-plan 3D, non vérifié** : la dégradation sans WebGL et la révélation visuelle de fin de partie
  n'ont pas été vérifiées.
- Un test préexistant, `FireGrpcTests.A_missed_shot_triggers_the_opponent_s_counterattack`, a été signalé une
  fois en échec puis **non reproduit** (3 exécutions sur 3 au vert en isolation) — à surveiller, pas à
  déclarer cassé.

## Code généré

- **Protobuf** : `Grpc.Tools` génère dans `obj/`, depuis `Protos/battle.proto`, `BattleService`,
  `BattleServiceBase`, `FireRequest`, `FireResponse` et `Shot` ; seul le `.proto` est versionné.
- **`public partial class Program;`** (fin de `BattleShip.API/Program.cs`) : accroche de `WebApplicationFactory<Program>`
  pour les tests d'intégration, pas du code métier.
- **Gabarits** : `Program.cs`, `Properties/launchSettings.json` et `.gitignore` sortent de `dotnet new webapi`,
  `dotnet new blazorwasm` et `dotnet new gitignore`, puis ont été adaptés.

## Documentation

| Document | Contenu |
|---|---|
| `docs/conformite.md` | une ligne par exigence du sujet, avec sa commande de contrôle |
| `docs/superpowers/specs/2026-09-15-bataille-navale-design.md` | conception d'ensemble |
| `docs/superpowers/plans/` | plan d'implémentation découpé en tâches |
| `docs/demo/` | preuves de la démonstration gRPC-Web |
| `docs/adr/` | décisions d'architecture (0001 à 0012) |
| `PROMPTS.md` | échanges décisifs avec l'IA |
| `REVUE-IA.md` | onze revues argumentées des propositions de l'IA |
| `CONTEXTE-IA.md` | contexte du projet |
| `CLAUDE.md` | cadre de travail de l'IA sur ce dépôt |
