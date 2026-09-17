# ADR 0009 : Trois apparences commutables, une seule structure

## Statut et date
Accepté — 2026-09-16

## Contexte

L'interface livrée aux tâches 16 à 21 était fonctionnelle, accessible et testée, mais avec trois
défauts structurels, pas un manque de décoration : **symétrie sans hiérarchie** (rien ne disait où
regarder alors que le jeu ne se joue que sur une grille), **aucun vocabulaire du sujet** (pas de
règles de coordonnées, donc impossible de nommer une case), **aucun temps fort** (la touche changeait
une couleur de fond). Trois directions ont été proposées — carte hydrographique, coque d'acier, jeu
de plateau — et le binôme a demandé **les trois**, commutables depuis les paramètres.

## Options envisagées

- **A. Trois feuilles de style complètes, une par apparence.** La plus libre, mais elle triple ce
  qu'il faut maintenir *et vérifier* — notamment l'accessibilité livrée à la tâche 21. Trois
  occasions de laisser l'une régresser en silence, qu'aucun test de ce dépôt ne détecterait.
- **B. Un jeu de jetons CSS, redéfini par `[data-theme]` sur `<html>`.** Une seule structure, un
  seul balisage, un seul comportement clavier ; une apparence ne change que le **substrat** et la
  **matière** d'un coup. Moins libre — une skin ne peut pas déplacer un élément — et c'est
  précisément la contrainte recherchée.
- **C. Un thème unique, le meilleur des trois.** Le moins de travail, mais ce n'est pas la demande.

## Décision

Option B. Les trois apparences sont des **jeux de jetons** déclarés par `[data-theme]` dans
`wwwroot/css/app.css`. Trois règles délimitent ce qu'une apparence a le droit de faire :

- **Jamais le balisage** : même composant, mêmes attributs ARIA, même tabindex roving.
- **Jamais le glyphe d'une case** : `■ · ✱ ✖` et le vide sont identiques dans les trois. C'est ce
  qui fait tenir « lisible sans la couleur » **par construction**, et c'est vérifiable en une ligne.
- **Jamais la police** : deux familles variables pour les trois (Fraunces, Archivo), pilotées sur
  leurs propres axes — largeur, graisse, `SOFT`, `WONK`.

Bootstrap est retiré : ses boutons, alertes et listes, reconnaissables partout, étaient la principale
cause du rendu « gabarit ». Un second réglage accompagne l'apparence — **Impacts, complet ou calme** :
le calme supprime la chorégraphie sans arrêter le jeu et rejoint `prefers-reduced-motion`.

## Conséquences

- Ajouter une quatrième apparence, c'est ajouter un bloc de jetons. Aucun composant à toucher.
- Les contrastes sont à mesurer **trois fois**.
- Une apparence ne peut pas proposer une mise en page différente ; il faudrait rouvrir cet ADR et
  retomber sur l'option A et son coût.
- Les réglages vivent dans `localStorage`, appliqués par un script **avant** le démarrage de Blazor
  pour qu'aucune première image ne montre la mauvaise apparence. `localStorage` peut lever en
  navigation privée : chaque accès tolère l'échec et retombe sur les valeurs par défaut. Une valeur
  relue du stockage est validée contre l'ensemble connu avant d'atteindre le DOM.

## Vérification et réexamen

- **Constaté** : les 24 paires de contraste glyphe/fond et texte/fond des trois apparences sont
  au-dessus de 4,5:1 et le filet de grille au-dessus du seuil non textuel de 3:1 ; cinq paires
  étaient sous le seuil à la première mesure, toutes corrigées et remesurées.
- **Constaté** : les cinq glyphes relevés dans le navigateur sont identiques dans les trois
  apparences (`hit:✱ miss:· ship:■ sunk:✖ unknown:`).
- **Constaté** : l'accessibilité de la tâche 21 survit — une seule case sur cent dans l'ordre de
  tabulation, flèches et `R` opérants, focus conservé après un tir, région live mise à jour.
- **Constaté** : le mode calme met `animation-name: none` et `opacity: 0` sur la gerbe et supprime
  la secousse, qui se déclenche sinon sur une touche et un coulé, **pas** sur un manqué.
- **Reste à vérifier** : aucun lecteur d'écran réel n'a été essayé. Sans réseau, les polices Google
  retombent sur la pile de secours et les trois apparences se ressemblent davantage.

## Références

- ADR 0007 (état côté Blazor) — inchangé : les apparences ne touchent pas à l'état.
- Tâche 21 du plan (accessibilité), dont cet ADR préserve les acquis.
- `wwwroot/css/app.css`, `Services/AppearanceState.cs`, `Components/SettingsMenu.razor`
