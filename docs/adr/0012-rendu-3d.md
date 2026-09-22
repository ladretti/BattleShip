# ADR 0012 : Un arrière-plan 3D décoratif, pas un jeu en 3D

## Statut et date
Accepté — 2026-09-22

## Contexte

Second des deux axes retenus le 2026-09-17 (le premier, le journal d'événements, est livré —
ADR 0011). Il s'agit d'ajouter une scène 3D réactive — mer, flotte du joueur, épaves, caméra qui
réagit aux impacts — derrière le jeu, alimentée par l'état que le front calcule déjà. C'est la
première dépendance front du projet : aucun `package.json`, aucune bibliothèque JS n'existait
avant cette tâche. Une dépendance est un ajout structurant au sens du `CLAUDE.md` § 3 bis et se
décide en ADR avant d'être codée — d'où cette tâche 0, avant la moindre ligne de code.

Deux enjeux dominent, dans l'ordre où ils ont fait échouer une option chacun :

- **Le secret.** Une scène 3D est un graphe d'objets lisible depuis la console du navigateur
  (`scene.children`). Y placer les cinq coques adverses à leurs vraies positions, même rendues
  invisibles, serait une fuite pire que celle évitée par l'ADR 0007 : elle ne passe par aucun
  endpoint, donc aucun test d'intégration ne la verrait.
- **L'accessibilité déjà acquise.** Le jeu livré à la tâche 21 (ADR 0009, 0010) repose sur un
  plateau `<button>` par case : navigation clavier, région `aria-live`, glyphes par état, contrastes
  mesurés trois fois. Un `<canvas>` n'a pas de DOM.

## Options envisagées

- **A. Rendu 3D du jeu lui-même** — remplacer la grille `<button>` par une scène interactive.
  Écarté d'emblée : la navigation clavier, la région live, les glyphes et les contrastes de la
  tâche 21 devraient être réimplémentés en double dans un médium qui ne porte pas nativement ces
  garanties. Aucun gain ne compense ce coût.
- **B. Arrière-plan 3D décoratif, alimenté par la même projection que le DOM.** Le canvas est
  `aria-hidden`, `pointer-events: none`, `z-index: -1` ; on ne clique pas dedans, on n'y navigue pas
  au clavier. Il ne porte aucune information que la grille HTML ne porte pas déjà, donc il ne peut
  pas régresser l'accessibilité existante — il n'y touche simplement pas.
- **C. Pas de 3D du tout.** Le moins de travail, mais ce n'est pas la demande validée en session de
  brainstorming du 2026-09-22 ; le socle et les extensions déjà livrées visent l'ambition, pas le
  minimum (CLAUDE.md § 1).

## Décision

Option B.

**Three.js est vendorisé dans `wwwroot/lib/`, pas chargé depuis un CDN.** Le `README.md` documente
déjà une dégradation hors-ligne pour Google Fonts ; on n'introduit pas un second point de défaillance
réseau pour un projet qui doit rester lançable hors ligne une fois construit.

**Le secret se filtre à la source, jamais à l'affichage.** La scène ne reçoit que ce que le joueur
sait déjà : la mer, sa flotte, les coques adverses **coulées** pendant la partie, et la flotte
adverse complète seulement après `GameEnded`. Aucun objet n'est créé, même invisible, pour un
navire adverse non coulé. Conséquence assumée : les navires adverses qui sombrent en cascade sont
une scène de fin de partie ou de rediffusion, pas de jeu courant — une limite du secret, à consigner
au `README.md`, pas de la technique.

**La projection descend dans `BattleShip.Models`, établi par spike et non supposé.** La première
rédaction la plaçait dans `BattleShip.App`, à côté de `Play.razor`. Un spike sur une copie jetable
du dépôt — ajouter un `ProjectReference` de `BattleShip.App` à `BattleShip.Tests` pour pouvoir
tester la fonction depuis là où elle vivrait — a produit :

```
error CS0433: Le type 'BattleService' existe dans 'BattleShip.API, Version=1.0.0.0' et
'BattleShip.App, Version=1.0.0.0'
```

Cause : `BattleShip.API` et `BattleShip.App` génèrent chacun leurs stubs gRPC depuis le même
`BattleShip.API/Protos/battle.proto`, donc les deux assemblies portent un type `BattleService`
distinct de même nom : une troisième assembly qui référence les deux ne compile pas. `Models` est
donc le seul endroit atteignable à la fois par `App` (pour appeler la fonction) et par `Tests`
(pour la vérifier) sans ce conflit. Rien ne s'y oppose : la fonction ne manipule que `GameDto`,
`GameEvent` et `GameFold`, déjà dans `Models` ; seul `ShipOutline`, un helper de mise en forme dont
la projection n'a pas besoin, appartenait à `App`.

**`Play.razor` est rebranché sur cette même fonction.** Le composant contient aujourd'hui trois
méthodes privées (`LiveOpponentHulls`, `RevealedOpponentHulls`, `CensoredOpponentHulls`) qui
répondent déjà à la question que pose la scène 3D : quels navires adverses sont coulés à l'instant
*n*. Le chantier précédent (R18, axe A) a montré ce que coûtent deux implémentations divergentes du
même calcul critique pour le secret. Rebrancher `Play.razor` sur la fonction partagée est donc une
suppression de duplication, pas un ajout de portée — aucun comportement visible ne change.

**Contrat d'interop, quatre fonctions, pas trois.** Le front a déjà un motif d'interop —
`battleshipSettings` (`read`/`write`/`remove`/`prefersReducedMotion`) dans `wwwroot/index.html`. On
en ajoute un second de la même forme :

```js
battleshipScene = {
  init(canvas),      // -> bool : false si WebGL indisponible
  update(state),      // état complet, idempotent, jamais un delta
  freeze(bool),        // gèle/dégèle la scène sous mouvement réduit
  dispose()
}
```

La spec (§ 3) n'en listait que trois (`init`/`update`/`dispose`) ; cette liste était illustrative,
pas fermée. L'exigence du § 4 — la scène se fige sous mouvement réduit — a besoin d'un point d'entrée
propre : `update` ne le porte pas sans coupler l'état du jeu au réglage d'affichage, et `init` ne
s'exécute qu'une fois. `freeze` s'appuie sur le réglage « Impacts complet ou calme » déjà posé par
l'ADR 0009 et sur `battleshipSettings.prefersReducedMotion()` déjà existant : aucun troisième
interrupteur n'est inventé.

## Conséquences

- **Rapport à l'ADR 0009** (trois apparences, réglage de mouvement) : non remplacé, on s'y branche.
  `freeze` consomme le même réglage calme/complet que la chorégraphie CSS des impacts.
- **Rapport à l'ADR 0010** (coques dessinées par une couche SVG superposée) : non remplacé, même
  principe appliqué à un autre médium — une couche décorative séparée du plateau interactif, jamais
  fusionnée avec lui.
- `BattleShip.Models` gagne une dépendance interne supplémentaire à zéro dépendance externe : la
  fonction de projection reste un domaine pur, testable sans navigateur ni serveur.
- La caméra reste fixe après l'initialisation (`position.set`, `lookAt`) ; seul `aspect` change au
  redimensionnement. L'effet livré sur impact est un halo pulsé (`state.impactMesh`), pas un
  mouvement de caméra — corrigé dans le `README.md` du 2026-09-22, qui l'annonçait à tort.
- `Play.razor` perd environ une quarantaine de lignes sur 467 en supprimant ses trois méthodes
  privées devenues redondantes.
- Aucun test automatisé ne couvre le rendu, la dégradation sans WebGL, le gel sous mouvement réduit
  ni l'absence de fuite de contexte WebGL — ces quatre points ne sont vérifiables qu'au navigateur.
  Créer un projet de tests front pour les couvrir serait un sous-système de plus pour vérifier du
  dessin ; écarté au même titre que dans la liste fermée du CLAUDE.md § 3 bis.

## Vérification et réexamen

À exécuter et consigner : les quatre tests du § 5 de la spec, sur la fonction de projection dans
`BattleShip.Tests` — (1) en cours de partie, `Wrecks` ne contient que des navires coulés ; (2)
l'union des cases de `Friendly` et `Wrecks` ne contient aucune case d'un navire adverse non coulé ;
(3) après `GameEnded`, `Revealed` est vrai et la flotte adverse complète apparaît ; (4) au curseur de
rejeu *n*, `Wrecks` est exactement l'ensemble des navires coulés à l'instant *n* — la régression R18
de l'axe A, reproduite dans un médium où aucun test n'aurait pu l'attraper sans ce test dédié. Les
tests 1 et 3 forment une paire : le premier seul est satisfait par une fonction qui ne rend jamais
rien, le second seul par une fonction qui ne censure jamais.

À constater au navigateur, hors automatisation : absence de rectangle noir sans WebGL (repli sur le
fond SVG actuel), gel effectif sous mouvement réduit, `dispose()` appelé à la sortie de page sans
contexte WebGL fuité après plusieurs parties.

**Condition de réexamen** : si le canvas devenait interactif (clic, survol, caméra libre à la
souris), l'arbitrage d'accessibilité de cet ADR serait à refaire entièrement — l'option A,
écartée ici, redeviendrait la question posée.

## Références

- Spec de conception : `docs/superpowers/specs/2026-09-22-rendu-3d-design.md`
- `CLAUDE.md` § 3 bis (liste fermée des patterns, ajout décidé en ADR avant d'être codé)
- ADR 0009 (trois apparences, réglage de mouvement) — on s'y branche
- ADR 0010 (coques dessinées par une couche superposée) — même principe, autre médium
- ADR 0011 (journal d'événements) — source de l'état que la scène consomme
- ADR 0007 (état côté Blazor, règle du secret) — appliquée ici à un second médium
- Axe A tâche 8 (`Play.razor`, trois méthodes de projection des coques adverses) — la logique réutilisée
