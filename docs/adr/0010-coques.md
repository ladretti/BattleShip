# ADR 0010 : Les navires sont dessinés par une couche superposée, pas par les cases

## Statut et date
Accepté — 2026-09-16

## Contexte

Après l'ADR 0009, un navire restait n carrés colorés ; il fallait qu'il devienne **un objet**, une
coque continue, avec étrave et poupe, franchissant les gouttières. Or **le navire est un objet
unique quand tout le reste du système est par case** : chaque case est un `<button>` focalisable, et
en fusionner cinq détruirait le clavier et l'accessibilité — d'où le **dessin** séparé de l'**usage**.

## Options envisagées

- **A. Sprites matriciels.** 5 tailles × 2 orientations × 3 apparences = 30 fichiers, doublés en
  haute densité, n'héritant d'aucun jeton de thème. Écarté sur le coût de maintenance.
- **B. Canvas ou WebGL.** Interop JS, affichage sorti du système de jetons CSS, second moteur de
  rendu à maintenir — disproportionné pour un plateau statique de cent cases.
- **C. Découper la coque en tranches, une par case.** Aucun élément nouveau, mais la gouttière
  recoupe le navire à chaque case : c'est précisément le défaut à supprimer.
- **D. Arrondir les coins des cases d'extrémité.** Le moins cher ; donne une gélule, pas un navire.
- **E. Une seconde grille superposée, une coque SVG paramétrique par navire.** La grille de jeu ne
  bouge pas ; une couche sœur, `pointer-events: none` et `aria-hidden`, au même gabarit, porte un
  SVG par navire en `grid-column: x / span n`.

## Décision

Option E.

- **Une grille séparée, jamais des éléments ajoutés à la grille de jeu.** Un élément placé fait
  contourner sa case par le placement automatique et décale les suivantes — défaut livré une fois
  (douzième ligne fantôme, ADR 0009 ; `PROMPTS.md`).
- **Le gabarit des deux grilles est explicite et identique** : la piste des règles était en `auto`,
  dimensionnée par son texte, et la couche, sans texte, voyait son `auto` s'effondrer.
- **La silhouette est paramétrique** (`HullGeometry`), construite en (longueur, largeur) puis
  projetée en (x, y) : une fonction produit les cinq navires dans les deux orientations.
- **`preserveAspectRatio="none"`** : l'élément couvre n cases **et** les n−1 gouttières, dont la
  largeur est une décision de thème. La coque s'étire un peu sur un plateau à grosses gouttières.
- **Les dégâts ne sont pas dans le SVG** — une marque dans le `viewBox` dériverait jusqu'à un dixième
  de case : chaque dégât est son propre élément, placé **par la grille**, sur sa case.
- **Une case posée sur une coque ne peint aucun fond** et passe au-dessus : la coque se voit, le
  glyphe reste dessus. Un **lit** (`--hull-bed`) comble la case transparente, qui révélait sinon la
  couleur de filet — d'où un halo gris autour de chaque navire.

## Conséquences

- La règle du secret ne demande aucun effort : côté adverse seul `SunkShipDto` porte des cases, donc
  un navire à flot n'a rien à dessiner. Orientation et étendue se **déduisent** des cases envoyées.
- Trois paires de couleurs de plus à mesurer par apparence. En acier, la coque a dû devenir **plus
  claire** que la tôle : celle-ci est sombre et même le noir pur n'atteint que 2,02:1 contre elle.
- Une épave est identifiée par son **contour**, pas son remplissage : le brûlé n'atteint pas 3:1
  contre un plateau sombre dans deux apparences sur trois.

## Vérification et réexamen

- **Constaté** : 53 contrôles de géométrie sur 53 hors navigateur (`dotnet run --file`) — `viewBox`,
  points du tracé, superstructure et tourelles, tailles 1 à 5 dans les deux orientations. Contrôle de
  culture **prouvé discriminant** : sous `fr-FR` un `double` brut rend `"1,5"` et ajouterait une
  coordonnée à chaque tracé ; le formateur rend `"1.5"`.
- **Constaté** : la boîte de la couche et celle de la grille mesurent 490×490, 11×11 pistes.
- **Constaté** : coque sur son lit — 5,95:1 (carte), 3,31:1 (acier), 4,16:1 (plateau), au-dessus du
  seuil non textuel de 3:1 ; glyphe sur coque et superstructure au-dessus de 4,5:1. Le clavier
  survit : une seule case sur cent tabulable, focus conservé après un tir.
- **Reste à vérifier** : le rendu sur écran à très haute densité et les coques à une grille de 20.

## Références

- ADR 0009 (trois apparences, dont cet ADR applique les trois règles), ADR 0001 (règle du secret),
  ADR 0007 (le front ne décide aucune règle).
- `Rendering/HullGeometry.cs`, `Rendering/ShipOutline.cs`, `Components/Hull.razor`.
