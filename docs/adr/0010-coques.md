# ADR 0010 : Les navires sont dessinés par une couche superposée, pas par les cases

## Statut et date
Accepté — 2026-09-16

## Contexte

Après l'ADR 0009, un navire restait n carrés colorés portant un glyphe. Il fallait qu'il
devienne **un objet** : une coque continue, avec étrave et poupe, franchissant les gouttières
entre ses cases.

L'obstacle n'est pas de dessiner un bateau. C'est que **le navire est un objet unique alors
que tout le reste du système est par case** : chaque case est un `<button>` focalisable, avec
son propre `aria-label`, son glyphe et son état de dégât. Fusionner cinq cases en un élément
détruirait le clavier, les annonces et la lisibilité sans la couleur — tout ce que la tâche 21
a livré et vérifié.

Toute la décision consiste donc à séparer **ce qui se dessine** de **ce qui s'utilise**.

## Options envisagées

**A. Sprites matriciels, un par navire, par orientation, par apparence.**
5 tailles × 2 orientations × 3 apparences = 30 fichiers, doublés pour les écrans à haute
densité. Et ils n'héritent d'aucun jeton de thème : chaque changement de palette redevient un
travail d'image. Écarté sur le coût de maintenance seul.

**B. Canvas ou WebGL.**
Impose l'interop JS, sort l'affichage du système de jetons CSS et ajoute un second moteur de
rendu à maintenir — pour un plateau statique de cent cases. Disproportionné.

**C. Découper la coque en tranches, une par case.**
Aucun élément nouveau, mais la gouttière recoupe le navire à chaque case : c'est précisément
le défaut qu'on veut supprimer. Ne vaudrait que si l'on renonçait aux gouttières.

**D. Arrondir les coins des cases d'extrémité.**
Le moins cher. Donne une gélule, pas un navire.

**E. Une seconde grille superposée, une coque en SVG paramétrique par navire.**
La grille de jeu ne bouge pas. Une couche sœur, `pointer-events: none` et `aria-hidden`,
déclarée avec **exactement le même gabarit**, porte un SVG par navire occupant
`grid-column: x / span n`.

## Décision

Option E.

- **Une grille séparée, jamais des éléments ajoutés à la grille de jeu.** Un élément placé
  explicitement fait contourner sa case par le placement automatique et décale toutes les
  suivantes — ce défaut a été livré une fois (une douzième ligne fantôme, ADR 0009) et cette
  forme-ci ne peut pas l'avoir.
- **Le gabarit des deux grilles est explicite et identique.** La piste des règles était en
  `auto`, dimensionnée par son texte ; la couche n'a pas de texte, donc son `auto` s'effondrait
  et chaque coque se retrouvait décalée d'une piste.
- **La silhouette est paramétrique** (`HullGeometry`), construite dans un espace
  (longueur, largeur) puis projetée en (x, y). Une seule fonction produit les cinq navires
  dans les deux orientations, et une flotte personnalisée marcherait sans nouveau dessin.
- **`preserveAspectRatio="none"`.** L'élément couvre n cases **et** les n−1 gouttières, dont
  la largeur est une décision de thème que CSS possède et que le composant ne peut pas
  connaître. La coque s'étire donc légèrement sur un plateau à grosses gouttières, ce qui se
  lit comme un plateau plus épais, pas comme une erreur.
- **Les dégâts ne sont pas dans le SVG.** Pour la même raison : une marque placée dans le
  `viewBox` de la coque dériverait jusqu'à un dixième de case selon l'apparence. Chaque
  dégât est son propre élément, placé **par la grille**, sur sa case.
- **Une case posée sur une coque ne peint aucun fond** et passe au-dessus de la couche : la
  coque se voit, le glyphe reste dessus. Le contrat d'accessibilité tient sans exception.
- **Un lit sous la coque** (`--hull-bed`) : sans lui, la case transparente révélait le fond du
  plateau, qui est la couleur de filet — d'où un halo gris autour de chaque navire. Le lit
  efface aussi le filet **à l'intérieur** de l'empreinte du navire, ce qui est exactement ce
  qu'une coque continue doit faire.

## Conséquences

- La règle du secret ne demande aucun effort : côté adverse seul `SunkShipDto` porte des
  cases, donc un navire à flot n'a rien à dessiner. Aucun changement de contrat serveur non
  plus — orientation et étendue se **déduisent** des cases déjà envoyées.
- Trois paires de couleurs de plus à mesurer par apparence (coque sur son lit, glyphe sur la
  coque, glyphe sur la superstructure).
- En acier, la coque a dû devenir **plus claire** que la tôle : la tôle est déjà sombre, et
  même le noir pur n'atteint que 2,02:1 contre elle. Un gris haze est d'ailleurs la couleur
  réelle d'un navire de guerre.
- Une épave est identifiée par son **contour**, pas par son remplissage : le brûlé n'atteint
  pas 3:1 contre un plateau sombre dans deux apparences sur trois.

## Vérification et réexamen

- **Constaté** : 53 contrôles de géométrie exécutés hors navigateur (`dotnet run --file`) —
  `viewBox` correct dans les deux orientations pour les tailles 1 à 5, tous les points du
  tracé dans la boîte, superstructure et tourelles selon la taille. Le contrôle de culture est
  **prouvé discriminant** : sous `fr-FR` un `double` brut rend `"1,5"`, ce qui ajouterait une
  paire de coordonnées à chaque tracé ; le formateur rend `"1.5"`.
- **Constaté** : la boîte de la couche et celle de la grille mesurent exactement la même chose
  (490×490), 11×11 pistes des deux côtés.
- **Constaté** : coque sur son lit — 5,95:1 (carte), 3,31:1 (acier), 4,16:1 (plateau) ; toutes
  au-dessus du seuil **non textuel de 3:1** qui s'applique à un graphique identifiant
  l'emplacement d'un navire. Glyphe sur coque et sur superstructure au-dessus de 4,5:1 dans
  les trois.
- **Constaté** : le clavier survit — une seule case sur cent dans l'ordre de tabulation, focus
  conservé sur la case tirée après un tir, glyphes identiques dans les trois apparences.
- **Reste à vérifier** : le rendu sur un écran à très haute densité, et le comportement des
  coques à une taille de grille de 20 (le maximum accepté) n'ont pas été regardés.

## Références

- ADR 0009 (trois apparences) — cet ADR en applique les trois règles : pas de balisage
  changé, pas de glyphe changé, pas de police changée.
- ADR 0001 (règle du secret), ADR 0007 (le front ne décide aucune règle).
- `Rendering/HullGeometry.cs`, `Rendering/ShipOutline.cs`, `Components/Hull.razor`.
