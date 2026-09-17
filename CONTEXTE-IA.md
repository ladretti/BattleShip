# Contexte du projet

## Contraintes du cours

.NET 10 stable, Minimal API, Blazor WebAssembly, modèles partagés, tests métier et d'intégration,
FluentValidation sur les entrées serveur, un échange gRPC-Web fonctionnel depuis le navigateur, règles
vérifiées côté serveur, informations adverses préservées, projet attendu au-delà du socle.

## Vision du projet, règles et expérience visée

Une bataille navale jouable du navigateur au serveur, dont l'intérêt principal est **l'adversaire** : trois
niveaux distincts — aléatoire, chasse/cible avec parité, densité probabiliste — et une mesure chiffrée qui le
prouve. Règles : grille et flotte paramétrables (10×10, 5-4-3-3-2), navires interdits de se toucher, « touche
= on rejoue », placement manuel du joueur validé côté serveur.

## Backlog, priorités et périmètre retenu

Retenu, par ordre d'attaque : duel d'IA avec mesure du nombre de coups, historique et rejeu, accessibilité.
Écartés : persistance, multijoueur, déploiement — motifs dans `README.md`.

## Organisation du code et contrats

`BattleShip.Models` est un domaine pur sans dépendance : modèle, règles de placement, moteur, contrats
`IGameStore` et `IOpponentStrategy`, DTO partagés (`Models/Contracts`, ADR 0008). `BattleShip.API` porte les
implémentations, les validateurs et les deux façades ; `BattleShip.App` les pages et `GameState`. Deux
invariants gouvernent le reste : **une seule source de vérité** (navires + tirs) et **le secret** (pour
l'adversaire, la garantie tient dans le type `ShotHistory`).

## Commandes, ports et environnement

SDK 10.0.100 `latestFeature` (`global.json`), soit 10.0.401 en pratique. Profil par défaut `http` : API
`http://localhost:5184`, front `http://localhost:5210` ; profil `https` : `7050` et `7073`. Ne pas mélanger
les schémas. Lancement, vérification et démonstration gRPC-Web : `README.md`.

## Conventions et méthode de collaboration

Conventions C# et ASP.NET : `CLAUDE.md` § 2 et § 3. Commits en français, un sujet par commit, `dotnet format`
avant chaque commit. **Code sans commentaires depuis le 2026-09-17** : les décisions vivent dans les ADR et
`REVUE-IA.md`. Toute décision structurante passe par un ADR **avant** d'être codée ; les échanges décisifs
vont dans `PROMPTS.md`, les propositions acceptées dans `REVUE-IA.md` avec leur contrôle.

## Décisions structurantes et références des ADR

`docs/adr/` — **0001** navires + tirs comme vérité, grille calculée · **0002** `IGameStore` `Singleton` à
verrou par partie · **0003** `IOpponentStrategy` et `ShotHistory`, trois niveaux · **0004** `Result<T>` pour
les refus métier (**renverse** `CLAUDE.md` § Gestion des erreurs) · **0005** tir en gRPC-Web exclusif ·
**0006** règles du jeu · **0007** `GameState` (réexamen : `Scoped`, revue 5) · **0008** DTO partagés (revue 6)
· **0009** trois apparences · **0010** coques SVG.

## Vérifications réalisées et limites connues

**Mesurés** : 95,7 / 52,6 / 41,2 coups par stratégie (revue 2) ; 0 exception par tour d'adversaire (revue 1) ;
gRPC-Web monté et testé (`FireGrpcTests`) ; store concurrent (revues 3 et 4). Exigence par exigence :
`docs/conformite.md`. **Non vérifié** : lecteur d'écran réel, publication trimmée.

## Arbitrages et évolution du périmètre

Deux recommandations de l'IA écartées au brainstorming du 2026-09-15 : le placement automatique du joueur et
le tour strictement alterné. Conséquences dans l'ADR 0006, dont le caractère écrasant du niveau Difficile,
accepté plutôt que corrigé.
