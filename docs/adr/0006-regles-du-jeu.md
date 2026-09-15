# ADR 0006 : Règles du jeu

## Statut et date
Accepté — 2026-09-15

## Contexte

Le sujet laisse libres les règles, la taille de grille et la composition de la flotte. Ces
choix ne sont pas décoratifs : ils déterminent la difficulté du placement, la force de
l'adversaire et la facilité d'écriture des tests.

## Options envisagées et décisions

### Grille et flotte : paramétrables ou fixes

Des constantes en dur sont plus simples, mais rendent les tests de cas limites verbeux — il
faut jouer dix-sept coups pour atteindre une fin de partie — et imposeraient un refactor du
moteur à la première extension.

**Décidé : paramétrables**, avec pour défaut 10×10 et la flotte classique 5-4-3-3-2. Le
paramétrage est quasi gratuit s'il est fait dès le départ, très coûteux en rétrofit.

Le gain principal est **la testabilité** : une grille 3×3 avec un seul navire de taille 2
rend la saturation, le débordement et la fin de partie atteignables en quelques coups.

### Navires adjacents : autorisés ou interdits

**Décidé : interdits**, diagonales comprises.

C'est plus de travail des deux côtés — le placement doit gérer les blocages, et la règle doit
être partagée entre placement automatique et validation manuelle — mais la contrainte
**renforce** `DensityStrategy`, qui peut exclure la couronne autour d'un navire coulé.

### Enchaînement des tours

**Décidé : touche = on rejoue.** Le tour ne change qu'après un coup manqué.

### Placement de la flotte du joueur

**Décidé : manuel**, dans le navigateur, validé côté serveur. La flotte adverse reste placée
automatiquement par `FleetPlacer`.

Le placement automatique pour le joueur aurait livré l'essentiel de la satisfaction pour une
fraction du travail. Le placement manuel est retenu malgré son coût front (sélection,
rotation, prévisualisation) parce qu'il produit la meilleure démonstration FluentValidation
du projet : un placement soumis peut violer trois règles distinctes, toutes vérifiées côté
serveur.

## Conséquences

- **Le niveau Difficile sera écrasant.** `DensityStrategy` plus « touche = on rejoue »
  enchaîne typiquement 4 à 5 coups dès qu'elle touche. Le joueur perdra presque
  systématiquement. Ce n'est pas un défaut à corriger : c'est le résultat attendu de la
  combinaison de règles. **Le niveau Normal est le mode jouable ; le niveau Difficile est une
  démonstration de l'algorithme.** À écrire dans le `README.md` comme limite connue.
- La machine à états se complique : `CurrentPlayer` ne change plus à chaque coup. `Fire`
  renvoie une séquence de coups (ADR 0005).
- `PlacementRules` — bornes, chevauchement, adjacence — est une fonction pure **partagée**
  par `FleetPlacer` et par le validateur du placement manuel. Sans ce partage, la règle
  d'adjacence serait écrite deux fois et le placement automatique produirait des placements
  que le validateur refuserait.
- Le tirage-rejet du `FleetPlacer` peut boucler sous contrainte de non-adjacence : compteur
  de garde, puis relance complète du placement.
- Le budget front augmente. Si le placement manuel déborde, le repli est d'exposer au joueur
  le placement automatique avec un bouton « re-générer », et de consigner l'arbitrage.

## Vérification et réexamen

À vérifier lors de l'implémentation, non encore fait :

- `FleetPlacer` sur N graines fixes : ni chevauchement, ni débordement, ni adjacence, et
  terminaison dans la limite du compteur de garde.
- Un test de refus de placement manuel **par motif** : débordement, chevauchement, adjacence.
  Trois tests distincts, pas un seul — sinon un validateur qui ne vérifierait que le
  débordement passerait.
- Un test de fin de partie sur une grille réduite, et un test de tir après la fin.
- La règle « touche = on rejoue » vérifiée par un test où le joueur touche puis rejoue sans
  que l'adversaire intervienne.

À réexaminer si le placement manuel déborde le budget (repli ci-dessus), ou si le niveau
Difficile s'avère si injouable qu'il nuit à la démonstration.

## Références

- Spec de conception § 1
- ADR 0003 (l'adversaire exploite `ShipsMayTouch`), ADR 0005 (`Fire` renvoie une séquence)
