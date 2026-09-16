# Bataille Navale — C# / ASP.NET Core

TP d'autonomie — Cours C# ASP.NET (HTS Learning, Christophe MOMMER).

> **État au 2026-09-16 : le socle et les trois extensions du backlog sont livrés.**
> Une partie complète se joue dans le navigateur, de la création à la victoire, au clavier
> comme à la souris. La démonstration gRPC-Web ci-dessous a été déroulée et ses preuves sont
> dans `docs/demo/`. Ce qui reste ouvert est dit, sans détour, en « Limites connues ».

## Binôme

- Luca Ceccarelli
- Irwin Ladrette

## Prérequis

- **.NET SDK 10.x**. Vérifier avec `dotnet --version` ; la version épinglée est dans
  `global.json` à la racine.
- Un navigateur récent (la démonstration gRPC-Web passe par la console F12).
- Certificat HTTPS de développement approuvé :

  ```bash
  dotnet dev-certs https --trust
  ```

## Lancer le projet

L'API et le front sont deux applications distinctes, à lancer dans **deux terminaux**.

```bash
# Terminal 1 — l'API
dotnet run --project BattleShip.API
#   https://localhost:7050   (http://localhost:5184)

# Terminal 2 — le front Blazor WebAssembly
dotnet run --project BattleShip.App
#   https://localhost:7073   (http://localhost:5210)
```

Ouvrir ensuite <https://localhost:7073>.

> Ne pas lancer `dotnet build` pendant que ces deux commandes tournent : le front servirait
> un `index.html` pointant sur des ressources qui n'existent plus. Voir « Limites connues ».

L'API et le front étant sur des origines distinctes, CORS est configuré côté API pour
autoriser explicitement l'origine du front, ainsi que les en-têtes gRPC-Web.

## Vérifier

```bash
dotnet build
dotnet test
dotnet format          # avant tout commit
```

Essais manuels des endpoints HTTP : `api.http` à la racine.
**`api.http` ne couvre pas le tir**, qui passe exclusivement par gRPC-Web (ADR 0005).

## Règles du jeu retenues

| Règle | Valeur |
|---|---|
| Grille | paramétrable, défaut **10×10** |
| Flotte | paramétrable, défaut **5-4-3-3-2** : `Carrier` 5, `Battleship` 4, `Cruiser` 3, `Submarine` 3, `Destroyer` 2 |
| Navires adjacents | **interdits**, diagonales comprises |
| Enchaînement des tours | **touche = on rejoue** |
| Placement du joueur | **manuel**, validé côté serveur |
| Placement de l'adversaire | automatique |

Les noms de la flotte suivent la nomenclature canonique du jeu *Battleship* ; ce n'est **pas une
traduction** des noms français (`Cruiser` fait 3 cases, pas 4 comme le « Croiseur » ; `Destroyer`
en fait 2). Seuls les noms changent : les tailles restent 5-4-3-3-2.

Trois niveaux d'adversaire : **Facile** (aléatoire), **Normal** (chasse/cible avec parité),
**Difficile** (densité probabiliste).

## Fonctionnalités livrées

- **Partie complète contre l'ordinateur**, de la création à la victoire ou la défaite, puis
  création d'une autre partie sans recharger la page.
- **Placement manuel** de la flotte dans le navigateur. La prévisualisation verte/rouge
  appelle `PlacementRules.Validate`, la règle même qu'exécute le serveur — mais elle ne
  **bloque pas** le clic : c'est le serveur qui refuse (débordement, chevauchement,
  adjacence), et son motif est affiché tel quel.
- **Trois niveaux d'adversaire** remplaçables, choisis à la création.
- **Tir en gRPC-Web** (`BattleService/Fire`) ; création, état, placement et historique en
  HTTP. Le tir n'a **aucune** route HTTP, par décision (ADR 0005).
- **FluentValidation** sur toutes les entrées serveur, HTTP et gRPC, appelée explicitement.
- Les trois états — chargement, succès, échec — rendus sur chaque page : couper l'API affiche
  un motif exploitable et laisse l'interface utilisable.
- **Le secret** : le front ne reçoit jamais la position d'un navire adverse encore à flot, et
  l'adversaire ne voit jamais la grille du joueur — le type `ShotHistory` qu'il reçoit ne
  porte aucun chemin vers un `Board` (vérifié par réflexion, `REVUE-IA.md` revue 2).
- **Historique et rejeu** : la liste ordonnée des coups des deux camps, et un curseur qui
  rejoue la partie. Le rejeu est un **rendu seul** — il n'émet aucune requête et ne touche
  jamais l'état serveur.
- **Accessibilité** : les deux grilles se jouent au clavier seul (tabindex roving, flèches,
  `Début`/`Fin`, `Entrée` pour agir, `R` pour pivoter), une région `aria-live` annonce chaque
  coup, chaque état porte un **glyphe** en plus de sa couleur, et les contrastes sont mesurés.
- Tests métier et tests d'intégration : **140 tests**, `dotnet test`.

## Démonstration gRPC-Web

À faire dans le navigateur, avec la console F12 ouverte sur l'onglet **Réseau**. Filtrer sur
`Fire` pour ne garder que les appels gRPC-Web.

1. Créer une partie (`/`), poser les cinq navires en cliquant sur la grille, puis
   **Validate fleet**.
2. **Réponse attendue** — cliquer une case vierge de « Their waters » : un
   `POST /battleship.BattleService/Fire` apparaît, en `HTTP 200`, `Content-Type:
   application/grpc-web`. Le coup s'affiche sur la grille, puis la riposte adverse se déroule
   coup par coup sur « Your fleet ».
3. **Erreur attendue nº 1** — cliquer **la même case** : la page affiche « You have already
   fired at that cell », et l'appel revient avec `grpc-status: 3` (`InvalidArgument`),
   `grpc-message: CellAlreadyShot`.
4. **Erreur attendue nº 2** — déplier **gRPC-Web diagnostics** en bas de la page de jeu, puis
   **Fire at an unknown game** : `grpc-status: 5` (`NotFound`),
   `grpc-message: GameNotFound`.

> **Point à comprendre pour lire l'onglet Réseau** : une erreur gRPC-Web revient en
> **`HTTP 200`**. Le statut voyage dans les *trailers* (`grpc-status`, `grpc-message`), pas
> dans le code HTTP. C'est pour cela que la politique CORS de l'API expose explicitement ces
> en-têtes — sans cela le navigateur les masquerait et le client ne pourrait pas distinguer
> les deux erreurs ci-dessus l'une de l'autre.

### Preuves

Le scénario a été déroulé et capturé dans `docs/demo/` :

| Preuve | Contenu |
|---|---|
| `docs/demo/01-fire-success.png` | le tir accepté |
| `docs/demo/02-invalid-argument.png` | `InvalidArgument` / `CellAlreadyShot` |
| `docs/demo/03-not-found.png` | `NotFound` / `GameNotFound` |
| `docs/demo/grpc-web-trace.md` | la trace réseau des trois appels, avec les `grpc-status` |

## Arbitrages du backlog

**Retenu**, par ordre d'attaque :

1. **Duel d'IA + mesure** — **livré et mesuré** : N parties par stratégie à graine fixe, nombre
   moyen de coups (`BattleShip.API/Benchmark/StrategyBenchmark.cs`). C'est la preuve chiffrée que
   les trois niveaux diffèrent réellement ; les chiffres sont en « Limites connues ».
2. **Historique des coups + rejeu** — **livré** : `GET /games/{id}/history` et le panneau de
   rejeu de la page de jeu, curseur compris.
3. **Accessibilité** — **livré pour l'essentiel** : clavier, annonces, glyphes, contrastes
   mesurés. Voir « Limites connues » pour ce qui reste.

**Écarté du socle**, et pourquoi :

- **Persistance SQLite / EF** — travail d'infrastructure qui ne sert ni le moteur ni
  l'adversaire. `IGameStore` rend le changement local le jour où il devient utile.
- **Multijoueur** — gestion de sessions et de temps réel hors périmètre.
- **Déploiement** — coût sans valeur ajoutée pour l'évaluation.
- **Placement automatique du joueur** — écarté au profit du placement manuel, qui offre une
  bien meilleure démonstration de la validation côté serveur.

## Limites connues

- **Le niveau Difficile est écrasant.** `DensityStrategy` combinée à « touche = on rejoue »
  enchaîne typiquement 4 à 5 coups dès qu'elle touche ; le joueur perdra presque
  systématiquement. C'est le résultat attendu de la combinaison de règles, pas un défaut.
  **Le niveau Normal est le mode jouable ; le niveau Difficile est une démonstration de
  l'algorithme.**
- Les nombres moyens de coups par niveau sont **mesurés** (200 parties par stratégie, graine
  20260915, `GameRules.Default` avec non-adjacence) : `Random` 95,7, `HuntTarget` 52,6,
  `Density` 41,2 coups. L'ordre attendu est confirmé et `Density` reste sous le seuil de 55
  coups fixé avant mesure (`docs/adr/0003-strategie-adversaire.md`, `REVUE-IA.md` revue 3).
  Ces chiffres ne portent que sur cette composition de flotte et cette taille de grille.
- L'état de partie est **en mémoire** : redémarrer l'API perd les parties en cours. Le front
  ne conserve pas non plus l'identifiant de partie : **recharger la page perd la partie en
  cours** (ADR 0007, réexamen).
- Le tir n'est pas testable depuis `api.http` (conséquence assumée de l'ADR 0005).
- **Reconstruire pendant que `dotnet run` tourne casse le front.** Les empreintes des fichiers
  de `_framework/` changent, l'`index.html` servi pointe alors sur des ressources absentes et
  la page reste blanche avec un `404` sur `dotnet.<hash>.js` en console. Remède : arrêter puis
  relancer `dotnet run --project BattleShip.App`. Constaté pendant la mise au point de la
  démonstration.
- **Accessibilité — ce qui reste.** Les contrastes ont été calculés (rapport WCAG 2.1) mais
  **aucun lecteur d'écran réel n'a été essayé** : les annonces sont vérifiées au niveau du DOM,
  pas à l'oreille. Les flèches ne suppriment pas le défilement de la page (Blazor ne permet pas
  de `preventDefault` conditionnel sans interop JS, et l'annuler pour toutes les touches
  emprisonnerait `Tab`). La grille du joueur n'est pas focalisable : elle est lue par son
  `aria-label` case par case, pas parcourue.
- Le rejeu ne rejoue que les **coups** : il ne reconstitue pas l'état « coulé » des navires
  intermédiaires, chaque case gardant le résultat que le serveur a donné à ce coup-là.
- La sérialisation JSON du front repose sur la réflexion. Elle est vérifiée en développement ;
  son comportement sous **publication trimmée** (`dotnet publish` en Release) n'a pas été
  éprouvé.

## Documentation

| Document | Contenu |
|---|---|
| `docs/superpowers/specs/2026-09-15-bataille-navale-design.md` | conception d'ensemble |
| `docs/superpowers/plans/` | plan d'implémentation découpé en tâches |
| `docs/demo/` | preuves de la démonstration gRPC-Web (captures + trace réseau) |
| `docs/adr/` | décisions d'architecture (0001 à 0008) |
| `PROMPTS.md` | échanges décisifs avec l'IA |
| `REVUE-IA.md` | revues argumentées des propositions de l'IA |
| `CONTEXTE-IA.md` | contexte du projet |
| `CLAUDE.md` | cadre de travail de l'IA sur ce dépôt |
