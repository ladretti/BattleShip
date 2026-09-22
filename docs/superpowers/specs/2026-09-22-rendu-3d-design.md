# Conception — Le rendu 3D réactif

Date : 2026-09-22
Statut : validé par le binôme à l'issue de la session de brainstorming du 2026-09-22.
Décision structurante attendue : **ADR 0012**, à écrire *avant* la première ligne de code.

Second des deux axes retenus le 2026-09-17. Le premier — le journal d'événements — est livré
(spec `2026-09-17-journal-evenements-design.md`). Celui-ci le consomme.

---

## 1. Ce qui est construit, et ce qui ne l'est pas

Un arrière-plan 3D **réactif** derrière le jeu : une mer, la flotte du joueur, les épaves, et
une réaction visible à la case impactée. Il est alimenté par l'état que le front calcule déjà.

*Amendé le 2026-09-22 — la rédaction initiale annonçait « une caméra qui réagit aux impacts ».
Le rendu livré est un **halo pulsé** à la case impactée ; la caméra reste fixe après
l'initialisation. Voir l'ADR 0012 § Conséquences et le `README.md`.*

**Le jeu reste le DOM.** Le canvas est décoratif : `aria-hidden`, `pointer-events: none`,
`z-index: -1`. On ne clique pas dedans, on n'y navigue pas au clavier, il ne porte aucune
information que la grille HTML ne porte pas.

C'est la contrainte qui a écarté un rendu 3D *du jeu lui-même* : un `<canvas>` n'a pas de DOM,
donc la navigation clavier de la grille, la région `aria-live`, les glyphes par état et les
contrastes mesurés — livrés à la tâche 21 et défendus par un ADR — devraient être
réimplémentés à côté, en double. Le canvas décoratif ne coûte aucune de ces garanties.

---

## 2. Le secret — le point le plus dangereux, comme à l'axe A

Une scène 3D est un **graphe d'objets lisible depuis la console du navigateur**. Y placer les
cinq coques adverses à leurs vraies positions en les rendant invisibles serait une fuite pire
que celle qu'a évitée l'axe A : elle ne passe par aucun endpoint, donc aucun test d'intégration
ne la verrait.

**Règle : la scène ne reçoit que ce que le joueur sait déjà.** Le filtrage est à la source, pas
à l'affichage.

```
  PENDANT LA PARTIE                    APRÈS GameEnded
  ─────────────────────                ────────────────
  mer + lumière                        idem
  la flotte du joueur                  idem
  coques adverses COULÉES seulement    + les survivantes révélées
  gerbes aux impacts joués             idem
  ─────────────────────                ────────────────
  aucun objet pour un navire           le journal complet est
  adverse non coulé                    servable (tâche 6 de l'axe A)
```

Cette liste est **exactement** celle que `Play.razor:OpponentHulls` calcule déjà en trois
branches depuis la tâche 8 de l'axe A. La scène consomme la même projection que le DOM, jamais
une seconde.

### Conséquence assumée sur l'ambition

Pendant une partie en cours, l'arrière-plan montre surtout la mer, la flotte du joueur et les
épaves. **Les navires adverses qui sombrent en cascade sont une scène de fin de partie ou de
rediffusion, pas de jeu courant.** C'est une limite du secret, pas de la technique, et elle
doit figurer au `README.md`.

---

## 3. Le contrat d'interop

Le front a déjà un motif d'interop : l'objet `battleshipSettings` de `wwwroot/index.html`
(`read` / `write` / `remove` / `prefersReducedMotion`). On en ajoute **un second de la même
forme**, pas une seconde manière de parler à JavaScript.

```js
battleshipScene = {
  init(canvas),      // -> bool : false si WebGL indisponible
  update(state),     // état complet, idempotent
  freeze(bool),      // gèle/dégèle la scène sous mouvement réduit
  dispose()
}
```

*Amendé le 2026-09-22 — la rédaction initiale n'en listait que trois (`init`/`update`/`dispose`) ;
`freeze` a été ajouté par un ruling de pré-vol et assumé par l'ADR 0012.*

Ce qui traverse la frontière, et rien d'autre :

```csharp
public sealed record SceneShip(string Name, int X, int Y, int Size, bool Vertical, bool Sunk);

public sealed record SceneState(
    int GridSize,
    IReadOnlyList<SceneShip> Friendly,
    IReadOnlyList<SceneShip> Wrecks,
    Coordinate? LastImpact,
    bool Revealed);
```

*Amendé le 2026-09-22 — `SceneShip` porte aussi `Name` : pour une épave, le nom a déjà été
annoncé au joueur, ce n'est pas une fuite du secret.*

`update` reçoit l'état **entier** à chaque changement, jamais un delta. Un graphe de scène
synchronisé par deltas dérive dès qu'un message se perd ; ici il n'y a rien à réconcilier, et
un `update` manqué se rattrape au suivant.

`Revealed` n'est pas un drapeau d'affichage : il dit que le journal servi est complet
(`GameEnded` reçu). C'est la même bascule temporelle que le § 3.2 de la spec de l'axe A.

### La scène suit le curseur de rejeu

`SceneState` est calculé à partir du **même instant** que le DOM : hors rejeu, l'état courant ;
en rejeu, l'état à la position du curseur. Une scène figée sur le présent pendant que la grille
remonte le temps afficherait deux vérités côte à côte — exactement le défaut R18 de l'axe A,
reproduit dans un médium où aucun test ne l'attraperait. D'où le test 4 du § 5.

---

## 4. Dépendance et dégradation

- **Three.js vendorisé** dans `wwwroot/lib/`, pas de CDN. Le `README.md` documente déjà une
  dégradation hors-ligne (Google Fonts) ; on n'en ajoute pas une seconde.
- **Pas de WebGL** (navigateur ancien, accélération désactivée) → `init` rend `false`, le canvas
  reste masqué, le fond SVG actuel s'affiche. **Jamais de rectangle noir.**
- **Mouvement réduit** → la scène se fige. `battleshipSettings.prefersReducedMotion()` existe
  déjà, et le réglage « Impacts complet ou calme » de l'ADR 0009 aussi : on s'y branche, on
  n'invente pas un troisième interrupteur.
- **Cycle de vie** → `dispose()` à la sortie de page, via `IAsyncDisposable` sur le composant.
  Un contexte WebGL fuité est un onglet qui rame au bout de trois parties.

---

## 5. Ce qui est testable, et ce qui ne l'est pas

Il n'existe pas de projet de tests front, et on n'en crée pas : ce serait un sous-système de
plus pour vérifier du dessin.

Mais **la partie dangereuse n'est pas le dessin, c'est ce qu'on envoie à la scène.** `SceneState`
est donc construit par une **fonction C# pure**, sans dépendance à Blazor ni à `IJSRuntime`, et
testée dans `BattleShip.Tests`.

### Où vit cette fonction — vérifié par spike, pas supposé

La première rédaction la plaçait dans `BattleShip.App`. **C'est infaisable** : `BattleShip.Tests`
ne référence pas `App`, et l'y ajouter casse la compilation —

```
error CS0433: Le type 'BattleService' existe dans 'BattleShip.API' et 'BattleShip.App'
```

les deux projets générant leurs stubs gRPC depuis le *même* `battle.proto`.

La fonction vit donc dans **`BattleShip.Models`**, ce que rien n'empêche : elle ne manipule que
`GameDto`, `GameEvent` et `GameFold`, tous déjà dans `Models`. Seul `ShipOutline` appartenait à
`App`, et ce n'est qu'un helper de mise en forme dont la projection n'a pas besoin.

**Conséquence retenue, et c'est une simplification** : `Play.razor` contient aujourd'hui trois
méthodes privées (`LiveOpponentHulls`, `RevealedOpponentHulls`, `CensoredOpponentHulls`) qui
répondent déjà à « quels navires adverses sont coulés à l'instant *n* ». La scène 3D pose la
même question. Deux implémentations du **même calcul critique pour le secret** dériveraient — le
chantier précédent a montré ce que ça coûte. `Play.razor` sera donc rebranché sur la fonction
partagée et allégé de ses trois méthodes ; aucun comportement visible ne change, et le fichier
de 467 lignes en perd une quarantaine.

| # | ce que le test affirme | l'erreur qu'il détecte |
|---|---|---|
| 1 | en cours de partie, `Wrecks` ne contient que des navires coulés | une fuite du secret dans la scène |
| 2 | en cours de partie, l'union des cases de `Friendly` et `Wrecks` ne contient **aucune** case d'un navire adverse non coulé | la même fuite, prise par les coordonnées |
| 3 | après `GameEnded`, `Revealed` est vrai et la flotte adverse complète apparaît | une censure qui ne se lève jamais |
| 4 | au curseur *n*, `Wrecks` est exactement l'ensemble des navires coulés à l'instant *n* | la régression R18 de l'axe A, reproduite en 3D |

Les tests 1 et 3 forment une **paire** : le premier seul est satisfait par une fonction qui ne
rend jamais rien, le second seul par une fonction qui ne censure jamais.

**Reste non vérifiable automatiquement** : le rendu lui-même, la dégradation sans WebGL, le gel
sous mouvement réduit, et l'absence de fuite de contexte WebGL. Ces quatre points se vérifient
au navigateur et seront rapportés comme tels — constaté, supposé, reste à vérifier.

---

## 6. Hors périmètre

- **Rendu 3D du jeu lui-même** — écarté au § 1, coût en accessibilité.
- **Interaction dans le canvas** (clic, survol, caméra libre à la souris) — le jeu est le DOM.
- **Projet de tests front** — on rend testable la fonction qui compte, pas le dessin.
- **Rediffusion cinématique complète** (caméra scénarisée sur tout le journal) — l'axe C livre
  une scène réactive ; une réalisation filmée est un axe D, s'il existe un jour.

---

## 7. Références

- Spec de l'axe A : `docs/superpowers/specs/2026-09-17-journal-evenements-design.md`
- ADR 0009 (trois apparences, jetons `[data-theme]`, réglage de mouvement) — on s'y branche
- ADR 0010 (coques dessinées par une couche superposée) — même principe, autre médium
- Axe A tâche 8 (`Play.razor:OpponentHulls`, trois branches) — la projection réutilisée
- `wwwroot/index.html`, objet `battleshipSettings` — le motif d'interop suivi
