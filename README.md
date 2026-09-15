# Bataille Navale — C# / ASP.NET Core

TP d'autonomie — Cours C# ASP.NET (HTS Learning, Christophe MOMMER).

> **État au 2026-09-15 : conception arrêtée, implémentation non commencée.**
> La solution est échafaudée et les décisions structurantes sont prises et documentées
> (`docs/adr/`, `docs/superpowers/specs/`). Aucune fonctionnalité de jeu n'est encore écrite.
> Les sections « Fonctionnalités livrées » et « Démonstration gRPC-Web » ci-dessous décrivent
> le **périmètre visé** et seront reprises au fur et à mesure des livraisons réelles.

## Binôme

- Luca Ceccarelli
- *(second membre — à compléter)*

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
| Flotte | paramétrable, défaut **5-4-3-3-2** |
| Navires adjacents | **interdits**, diagonales comprises |
| Enchaînement des tours | **touche = on rejoue** |
| Placement du joueur | **manuel**, validé côté serveur |
| Placement de l'adversaire | automatique |

Trois niveaux d'adversaire : **Facile** (aléatoire), **Normal** (chasse/cible avec parité),
**Difficile** (densité probabiliste).

## Fonctionnalités visées

- Partie complète contre l'ordinateur, de la création à la victoire.
- Placement manuel de la flotte dans le navigateur, validé côté serveur (débordement,
  chevauchement, adjacence).
- Trois niveaux d'adversaire remplaçables.
- Tir en gRPC-Web ; création, état, placement et historique en HTTP.
- FluentValidation sur toutes les entrées serveur, HTTP et gRPC.
- Tests métier et tests d'intégration.

## Démonstration gRPC-Web

À faire dans le navigateur, avec la console F12 ouverte sur l'onglet **Réseau** :

1. Créer une partie et placer sa flotte.
2. **Réponse attendue** — tirer sur une case vierge : l'appel `BattleService/Fire` apparaît
   dans l'onglet Réseau et renvoie le résultat du coup.
3. **Erreur attendue nº 1** — retirer sur la **même case** : `InvalidArgument`
   (`CellAlreadyShot`), visible dans la réponse gRPC-Web.
4. **Erreur attendue nº 2** — rejouer un `Fire` sur un identifiant de partie inexistant :
   `NotFound`.

## Arbitrages du backlog

**Retenu**, par ordre d'attaque :

1. **Duel d'IA + mesure** — N parties d'une stratégie contre une autre à graine fixe, nombre
   moyen de coups. C'est la preuve chiffrée que les trois niveaux diffèrent réellement.
2. **Historique des coups + rejeu** — la liste ordonnée des tirs est déjà dans le modèle.
3. **Accessibilité** — grille navigable au clavier, annonces pour lecteur d'écran, contrastes.

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
- Les nombres moyens de coups par niveau ne sont **pas encore mesurés** : les valeurs citées
  dans les ADR sont des hypothèses issues de la littérature, sur une grille sans règle de
  non-adjacence (`REVUE-IA.md`, revue 3).
- L'état de partie est **en mémoire** : redémarrer l'API perd les parties en cours.
- Le tir n'est pas testable depuis `api.http` (conséquence assumée de l'ADR 0005).

## Documentation

| Document | Contenu |
|---|---|
| `docs/superpowers/specs/2026-09-15-bataille-navale-design.md` | conception d'ensemble |
| `docs/superpowers/plans/` | plan d'implémentation découpé en tâches |
| `docs/adr/` | décisions d'architecture (0001 à 0007) |
| `PROMPTS.md` | échanges décisifs avec l'IA |
| `REVUE-IA.md` | revues argumentées des propositions de l'IA |
| `CONTEXTE-IA.md` | contexte du projet |
| `CLAUDE.md` | cadre de travail de l'IA sur ce dépôt |
