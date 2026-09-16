# ADR 0009 : Trois apparences commutables, une seule structure

## Statut et date
Accepté — 2026-09-16

## Contexte

L'interface livrée aux tâches 16 à 21 était fonctionnelle, accessible et testée, mais sans
parti pris : deux grilles de même taille côte à côte, des carrés colorés, et le coup le plus
important du jeu — la touche — se contentant de changer une couleur de fond. Trois défauts
structurels, pas un manque de décoration :

1. **Symétrie sans hiérarchie.** Rien ne disait où regarder, alors que le jeu ne se joue que
   sur une des deux grilles.
2. **Aucun vocabulaire du sujet.** Pas de règles de coordonnées — donc impossible de nommer
   une case, ce que ce jeu fait depuis toujours.
3. **Aucun temps fort.** Le moment qui devrait être le plus satisfaisant ne l'était pas.

Trois directions visuelles ont été proposées : carte hydrographique, coque d'acier, jeu de
plateau en plastique. Le binôme a demandé **les trois**, commutables à tout moment depuis les
paramètres.

## Options envisagées

**A. Trois feuilles de style complètes, une par apparence.**
La plus libre : chaque skin peut tout redéfinir, y compris la structure. Mais elle triple
tout ce qu'il faut maintenir *et vérifier* — notamment la promesse d'accessibilité déjà
livrée à la tâche 21 : chaque état de case lisible sans la couleur, contrastes mesurés,
navigation clavier. Trois feuilles, c'est trois occasions de laisser l'une d'elles régresser
en silence, et il n'existe aucun test de ce dépôt capable de le détecter.

**B. Un jeu de jetons CSS, redéfini par `[data-theme]` sur `<html>`.**
Une seule structure, un seul balisage, un seul comportement clavier. Une apparence ne change
que le **substrat** (ce dont le plateau est fait) et la **matière** d'un coup (encre, feu,
plastique). Moins libre : une skin ne peut pas déplacer un élément. C'est précisément la
contrainte recherchée.

**C. Un thème unique, le meilleur des trois.**
Le moins de travail, mais ce n'est pas ce qui a été demandé.

## Décision

Option B. Les trois apparences sont des **jeux de jetons**, déclarés par `[data-theme]` dans
`wwwroot/css/app.css` et appliqués sur `<html>`.

Trois règles délimitent ce qu'une apparence a le droit de faire :

- **Elle ne change jamais le balisage.** Même composant, mêmes attributs ARIA, même tabindex
  roving, mêmes touches.
- **Elle ne change jamais le glyphe d'une case.** `■ · ✱ ✖` et le vide sont identiques dans
  les trois. C'est ce qui fait tenir « lisible sans la couleur » **par construction** plutôt
  que par trois efforts séparés — et c'est vérifiable en une ligne, ce qui a été fait.
- **Elle ne change jamais la police de caractères.** Deux familles variables pour les trois
  (Fraunces en titrage, Archivo en interface), pilotées sur **leurs propres axes** — largeur,
  graisse, `SOFT`, `WONK`. Une skin industrielle et une skin jouet ne demandent pas six
  téléchargements de fontes, elles demandent deux réglages d'axes.

Bootstrap est retiré. Ses boutons, alertes et listes sont reconnaissables partout et
constituaient la principale cause du rendu « gabarit » ; trois identités distinctes ne
peuvent pas cohabiter avec lui.

Un second réglage accompagne l'apparence : **Impacts — complet ou calme**. Le mode calme
supprime la chorégraphie sans arrêter le jeu : les états changent, ils cessent de bouger. Il
rejoint `prefers-reduced-motion` du système, qui reste respecté indépendamment.

## Conséquences

- Ajouter une quatrième apparence, c'est ajouter un bloc de jetons. Aucun composant à toucher.
- Les contrastes sont à mesurer **trois fois**. Ils l'ont été : 24 paires, dont cinq corrigées
  après mesure (voir « Vérification »).
- Une apparence ne peut pas proposer une mise en page différente. Si cela devenait nécessaire,
  cet ADR serait à rouvrir — ce serait retomber sur l'option A et sur son coût.
- Les réglages vivent dans `localStorage`, appliqués par un script **avant** le démarrage de
  Blazor pour qu'aucune première image ne montre la mauvaise apparence. `localStorage` peut
  lever en navigation privée : chaque accès tolère l'échec et retombe sur les valeurs par
  défaut. Un joueur qui ne peut rien mémoriser garde un jeu qui fonctionne.
- Une valeur relue du stockage est une donnée fournie par le navigateur, pas une valeur
  choisie par ce code : elle est validée contre l'ensemble connu avant d'atteindre le DOM.

## Vérification et réexamen

- **Constaté** : les 24 paires contraste glyphe/fond et texte/fond des trois apparences sont
  au-dessus de 4,5:1, et le filet de grille au-dessus du seuil non-textuel de 3:1. **Cinq
  paires étaient sous le seuil** à la première mesure — la tôle des manqués (3,82:1), les
  libellés de règle acier (4,21:1) et plateau (4,49:1), et le rouge du plateau sous du blanc
  (4,35:1, deux fois) — toutes corrigées et remesurées.
- **Constaté** : les cinq glyphes relevés dans le navigateur sont **identiques** dans les
  trois apparences (`hit:✱ miss:· ship:■ sunk:✖ unknown:`).
- **Constaté** : l'accessibilité de la tâche 21 survit à la refonte — une seule case sur cent
  dans l'ordre de tabulation, flèches et `R` opérants, focus conservé sur la case tirée après
  un tir, région live mise à jour.
- **Constaté** : le mode calme met `animation-name: none` et `opacity: 0` sur la gerbe ; le
  mode complet la relance. La secousse du plateau se déclenche sur une touche et un coulé,
  **pas** sur un manqué, et le mode calme la supprime aussi.
- **Reste à vérifier** : aucun lecteur d'écran réel n'a été essayé, dans aucune des trois
  apparences. Les polices viennent de Google Fonts : sans réseau, la pile de secours
  s'applique et les réglages d'axes sont sans effet — l'interface reste lisible, mais les
  trois apparences se ressemblent davantage.

## Références

- ADR 0007 (état côté Blazor) — inchangé : les apparences ne touchent pas à l'état.
- Tâche 21 du plan (accessibilité), dont cet ADR doit préserver les acquis.
- `wwwroot/css/app.css`, `Services/AppearanceState.cs`, `Components/SettingsMenu.razor`
