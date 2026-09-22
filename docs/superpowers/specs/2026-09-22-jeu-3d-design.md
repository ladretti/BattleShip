# Conception — Le socle 3D jouable

Date : 2026-09-22
Statut : validé par le binôme le 2026-09-22.
Décision structurante attendue : **ADR 0013**, qui **remplace l'ADR 0012**, à écrire avant tout code.

Premier des quatre sous-chantiers du passage au jeu en 3D. Les trois autres — flottes en volume,
placement en 3D, rejeu et fin de partie en 3D — auront leurs propres specs et ne commencent qu'après
celui-ci, qui est le seul bloquant.

---

## 1. Ce qui change, et ce qui ne change pas

**Change** : la couche de rendu du plateau. Le jeu se joue désormais **dans une scène 3D** —
mer, deux plateaux posés dessus, grille projetée, survol et tir à la souris.

**Ne change pas** : le serveur, le moteur, `ScenePlan`, le contrat gRPC, le secret. Le tir passe
par le même `FireAsync` qu'aujourd'hui. Les 191 tests restent le filet.

**Est jeté** : `wwwroot/js/scene.js`. Il dessine un décor de 167 lignes, sans notion de grille ni
d'interaction. Repartir dessus coûterait plus que repartir propre.

### L'ADR 0012 est remplacé, pas amendé

Sa décision centrale était : « le canvas est décoratif, le jeu reste le DOM », justifiée par le coût
en accessibilité d'un jeu rendu en `<canvas>`. Le binôme a tranché l'inverse. L'ADR 0013 doit donc
**reprendre cet argument et dire pourquoi il ne s'applique plus** — voir § 2.

---

## 2. L'accessibilité — l'objection de l'ADR 0012 et sa réponse

L'ADR 0012 écartait un jeu en 3D parce qu'un `<canvas>` n'a pas de DOM : la navigation clavier de
la grille, la région `aria-live`, les glyphes par état et les contrastes mesurés (livrés à la
tâche 21, défendus par un ADR) auraient dû être réimplémentés en double.

**La réponse est de ne pas les réimplémenter du tout.**

```
  ce qui SE VOIT                   ce qui S'ANNONCE
  ──────────────                   ────────────────
  la scène 3D                      les 100 boutons de grille, inchangés
  survol, clic, caméra             aria-label, tabindex, flèches, aria-live
  raycasting souris                visuellement effacés, toujours focalisables
                    ↑                        ↓
                    focus DOM ──▶ éclaire la case 3D correspondante
```

Les boutons existent toujours, avec leurs attributs. Ils ne sont simplement plus ce qu'on regarde.

**Le point qui rend ça possible** : un lecteur d'écran ne clique pas, il **énumère et annonce**. Il
se moque de la position à l'écran. En séparant le rendu de l'annonce, aucun alignement géométrique
n'est nécessaire — et la caméra redevient libre de bouger sans jamais casser l'accessibilité.

**Approches écartées**, à consigner dans l'ADR 0013 :

- **Reprojeter la grille DOM sur la 3D à chaque image** — superposition au pixel, mais 100 éléments
  repositionnés par frame, désalignés au moindre mouvement de caméra, et une couche d'accessibilité
  qui devient une cible mouvante.
- **Caméra orthographique figée** pour que DOM et 3D coïncident statiquement — simple, mais une
  caméra qui ne bouge jamais annule l'intérêt du passage en 3D.

**Le cas à traiter sérieusement** : l'utilisateur **voyant qui navigue au clavier** doit voir où il
est. Le bouton focalisé donne son index ; la case 3D correspondante s'éclaire. Direct, sans calcul
géométrique inverse.

---

## 3. La scène et le tir

**Scène** : une mer, deux plateaux côte à côte posés dessus, les navires en volume. La grille est
projetée sur chaque plateau (lignes, repères `A`-`J` et `1`-`10`) pour qu'on sache où l'on tire.

**Tir** : un rayon partant du curseur croise le plan du plateau adverse ; l'intersection donne la
case. Survol → la case s'éclaire. Clic → `FireAsync`, le même appel gRPC qu'aujourd'hui.

**Caméra** : cadrage d'ensemble par défaut, plongée brève sur la case touchée, retour. Elle s'adapte
à la largeur — de face sur mobile, plus de recul sur grand écran.

**Découpage du module** : le fichier unique actuel est remplacé par trois responsabilités séparées —
la scène (ce qu'on dessine), les entrées (survol, clic, raycasting), la caméra. Un seul bloc de
167 lignes était déjà à la limite du lisible ; ce chantier le triplerait.

---

## 4. Le secret — et le piège que la revue précédente avait nommé

Pour dessiner un plateau jouable, la scène a besoin de **plus** que ce que `SceneState` porte : l'état
de chaque case adverse (manqué / touché / coulé) et les cases reçues sur le plateau du joueur.
`SceneState` gagne donc des champs.

Or la revue finale du chantier précédent a laissé exactement cet avertissement :

> « `ScenePlanTests` teste par les **coordonnées**, donc un sixième champ ajouté un jour à
> `SceneState` passerait sous ces tests — le garde-fou est la relecture, pas le type. »

C'est précisément ce que ce chantier fait. **Règle retenue** : le test de non-fuite cesse d'énumérer
les champs à la main. Il **parcourt l'objet sérialisé** et vérifie qu'aucune coordonnée d'un navire
adverse non coulé n'y figure, quel que soit le champ qui la porte. Un septième champ sera couvert
sans que personne y pense.

Inchangé : `ScenePlan` reste la source unique, le DOM et la 3D consomment la même projection, et le
journal censuré reste le seul chemin de production.

---

## 5. Ce qui sera prouvé, et comment

L'essentiel de ce qu'on livre est **visuel**, donc hors de portée de `dotnet test`. La preuve est
organisée en conséquence.

### Testable en C#

| # | ce que le test affirme | l'erreur qu'il détecte |
|---|---|---|
| 1 | aucune coordonnée d'un navire adverse non coulé n'apparaît dans `SceneState` **sérialisé**, à tous les curseurs, sur les deux formes de journal | une fuite portée par n'importe quel champ, présent ou futur |
| 2 | les nouveaux champs d'état de case reflètent exactement `GameDto` | un état de case inventé ou décalé |
| 3 | les 22 tests existants de `ScenePlanTests` | une régression de la projection |
| 4 | les 191 tests de la suite | une régression du serveur ou du moteur |

Le test 1 est la reformulation du garde-fou : il remplace l'énumération manuelle.

### Vérifié à l'écran, protocole énoncé AVANT exécution

1. un clic sur une case 3D tire sur **cette** case — comparé à la case enregistrée côté **serveur**,
   pas jugé à l'œil ;
2. le clavier parcourt les 100 cases et la case 3D correspondante s'éclaire ;
3. **un lecteur d'écran réel** annonce les cases — le `README.md` admet depuis le début qu'aucun n'a
   jamais été essayé ; ce chantier le fait ;
4. la scène tient de 360 px de large à un grand écran.

### Une précaution tirée du chantier précédent

L'arrière-plan 3D décoratif a passé **trois relectures** avant qu'on découvre qu'il ne s'affichait
pas du tout chez le binôme — les empreintes d'assets de Blazor étaient périmées après une édition de
`wwwroot/` pendant que `dotnet run` tournait. **Aucune erreur en console.**

Donc : toute vérification à l'écran se fait sur un **serveur fraîchement relancé**, et le protocole
**commence** par confirmer que le module est chargé (`typeof window.battleshipScene === 'object'` et
`three.module` présent dans les ressources). Pas de mesure sur un build douteux.

### Ce qui n'est pas promis

Que ce soit beau. Le fonctionnement, l'accessibilité et le secret sont garantis ; le rendu —
modèles, matières, lumière — demandera des allers-retours avec le binôme.

---

## 6. Hors périmètre de ce sous-chantier

- **Coques modélisées, dégâts, naufrages animés** — sous-chantier 2.
- **Placement de la flotte en 3D** — sous-chantier 3. Le placement reste en 2D ici.
- **Rejeu et révélation de fin de partie en 3D** — sous-chantier 4. Le curseur continue de piloter
  le DOM.
- **Contrôles de caméra à la souris** (orbite libre, zoom) — la caméra est scénarisée, pas pilotée.

---

## 7. Références

- ADR 0012 (arrière-plan décoratif) — **remplacé** par l'ADR 0013
- ADR 0009 (apparences, réglage de mouvement) — la scène s'y branche toujours
- Spec de l'axe C : `docs/superpowers/specs/2026-09-22-rendu-3d-design.md`
- `BattleShip.Models/Scene/ScenePlan.cs` — la projection réutilisée, inchangée
- `BattleShip.App/Components/FiringGrid.razor` — les boutons qui deviennent la couche d'annonce
