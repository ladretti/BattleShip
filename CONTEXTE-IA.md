# Contexte du projet

## Contraintes du cours

- .NET 10 stable ; vérifier `global.json` et `dotnet --version`.
- API ASP.NET Core Minimal API, Blazor WebAssembly, modèles partagés, tests.
- FluentValidation sur les entrées serveur ; au moins un échange gRPC-Web fonctionnel depuis
  le navigateur.
- Règles vérifiées côté serveur ; informations adverses cachées préservées.
- Projet attendu au-delà du socle : ambition, pertinence et qualité des extensions évaluées.
- Résultat compris et défendable par chaque membre du binôme.

## Vision du projet, règles et expérience visée

Une bataille navale jouable du navigateur au serveur, dont l'intérêt principal est
**l'adversaire**. Trois niveaux réellement distincts — aléatoire, chasse/cible avec parité,
densité probabiliste — et une mesure chiffrée qui prouve qu'ils diffèrent.

Règles retenues : grille et flotte paramétrables (défaut 10×10, 5-4-3-3-2), navires interdits
de se toucher, « touche = on rejoue », placement manuel du joueur validé côté serveur.

Le placement manuel a été préféré au placement automatique en connaissance de son coût front :
il produit la meilleure démonstration de validation serveur du projet, puisqu'un placement
soumis peut violer trois règles distinctes.

## Backlog, priorités et périmètre retenu

Retenu, par ordre d'attaque : duel d'IA avec mesure du nombre de coups, historique et rejeu,
accessibilité. Écartés : persistance, multijoueur, déploiement — motifs dans `README.md`.

## Organisation du code et contrats

`BattleShip.Models` est un domaine pur sans dépendance : modèle, règles de placement, moteur,
contrats `IGameStore` et `IOpponentStrategy`. `BattleShip.API` porte les implémentations, les
validateurs et les deux façades. `BattleShip.App` porte les trois pages et le service
`GameState`. `BattleShip.Tests` référence `API`.

Deux invariants de conception gouvernent tout le reste :

- **Une seule source de vérité** — les navires et l'ensemble des tirs ; toute matrice est
  une projection calculée.
- **Le secret** — ni le front ni l'adversaire ne reçoivent les positions non découvertes.
  Pour l'adversaire, la garantie tient dans le type (`ShotHistory` ne référence pas le
  `Board`), pas dans la relecture.

## Commandes, ports et environnement

SDK 10.0.401. API sur `https://localhost:7050`, front sur `https://localhost:7073`.
Commandes de lancement et de vérification dans `README.md`.

## Conventions et méthode de collaboration

Conventions C# et ASP.NET : `CLAUDE.md` § 2 et § 3. Commits en français, un sujet par commit.
`dotnet format` avant chaque commit.

Méthode : toute décision structurante passe par un ADR **avant** d'être codée. Toute
contribution significative de l'IA est consignée dans `PROMPTS.md`, et toute proposition
acceptée doit être étayée par un contrôle reproductible dans `REVUE-IA.md`.

## Décisions structurantes et références des ADR

| ADR | Décision |
|---|---|
| 0001 | Représentation de l'état : navires + tirs comme vérité, grille calculée |
| 0002 | `IGameStore` en `Singleton` avec verrou par partie |
| 0003 | `IOpponentStrategy` et contrat `ShotHistory` — trois niveaux |
| 0004 | `Result<T>` pour les refus métier — **renverse** `CLAUDE.md` § Gestion des erreurs |
| 0005 | Le tir en gRPC-Web exclusif |
| 0006 | Règles du jeu |
| 0007 | `GameState` singleton côté Blazor |

## Vérifications réalisées et limites connues

**Réalisé** : l'état du dépôt a été confronté à l'exécution et `CLAUDE.md` § 1 bis s'est
révélé périmé sur trois points, constatés par exécution le 2026-09-15.

**Non vérifié à ce jour** :

- les nombres moyens de coups par stratégie — hypothèses issues de la littérature, sur une
  grille sans règle de non-adjacence (revue 2) ;
- l'absence d'exception levée pendant les tours de l'adversaire, qui fonde l'argument de
  l'ADR 0004 (revue 1) ;
- le montage `WebApplicationFactory` + `GrpcChannel`, principal risque technique du projet,
  traité en premier dans le plan d'implémentation.

## Arbitrages et évolution du périmètre

Deux recommandations de l'IA ont été délibérément écartées lors du brainstorming du
2026-09-15 : le placement automatique du joueur, et le tour strictement alterné. Les deux
écarts sont assumés et leurs conséquences sont consignées dans l'ADR 0006 — notamment le
caractère écrasant du niveau Difficile, accepté plutôt que corrigé.
