# ADR 0006 : Règles du jeu

## Statut et date
Accepté — 2026-09-15

## Contexte

Le sujet laisse libres les règles, la taille de grille et la composition de la flotte. Ces choix ne
sont pas décoratifs : ils déterminent la difficulté du placement, la force de l'adversaire et la
facilité d'écriture des tests.

## Options envisagées

- **Grille et flotte : constantes en dur ou paramétrables.** Des constantes sont plus simples,
  mais rendent les tests de cas limites verbeux — dix-sept coups pour atteindre une fin de partie —
  et imposeraient un refactor du moteur à la première extension.
- **Navires adjacents : autorisés ou interdits.** Les interdire est plus de travail des deux côtés
  (blocages au placement, règle partagée entre placement automatique et validation manuelle), mais
  **renforce** `DensityStrategy`, qui peut exclure la couronne autour d'un navire coulé.
- **Placement du joueur : automatique ou manuel.** L'automatique livrerait l'essentiel de la
  satisfaction pour une fraction du travail ; le manuel coûte cher côté front (sélection, rotation,
  prévisualisation) mais produit la meilleure démonstration FluentValidation du projet.

## Décision

- **Grille et flotte paramétrables**, avec pour défaut 10×10 et la flotte 5-4-3-3-2 — `Carrier` 5,
  `Battleship` 4, `Cruiser` 3, `Submarine` 3, `Destroyer` 2 (`GameRules.Default`). Ces noms sont la
  nomenclature canonique de *Battleship*, **pas une traduction** des anciens noms français :
  `Cruiser` fait **3** (et non 4 comme le « Croiseur ») et `Destroyer` **2**. Le gain principal est
  la **testabilité** : une grille 3×3 avec un navire de taille 2 rend saturation, débordement et
  fin de partie atteignables en quelques coups.
- **Navires adjacents interdits**, diagonales comprises.
- **Touche = on rejoue** : le tour ne change qu'après un coup manqué.
- **Placement du joueur manuel**, dans le navigateur, validé côté serveur ; la flotte adverse reste
  placée par `FleetPlacer`.

## Conséquences

- **Le niveau Difficile sera écrasant** : `DensityStrategy` plus « touche = on rejoue » enchaîne 4
  à 5 coups dès qu'elle touche. C'est le résultat attendu de la combinaison, pas un défaut. **Le
  Normal est le mode jouable, le Difficile une démonstration** — à écrire dans le `README.md`.
- La machine à états se complique : `CurrentPlayer` ne change plus à chaque coup et `Fire` renvoie
  une séquence de coups (ADR 0005).
- `PlacementRules` — bornes, chevauchement, adjacence — est une fonction pure **partagée** par
  `FleetPlacer` et par le validateur du placement manuel ; sans ce partage, le placement automatique
  produirait des placements que le validateur refuserait.
- Le tirage-rejet du `FleetPlacer` peut boucler sous non-adjacence : compteur de garde, puis
  relance complète.
- Si le placement manuel déborde le budget front, le repli est le placement automatique avec un
  bouton « re-générer », arbitrage à consigner.

## Vérification et réexamen

- `FleetPlacer` sur N graines fixes : ni chevauchement, ni débordement, ni adjacence, et
  terminaison dans la limite du compteur de garde.
- Refus de placement manuel **par motif** : débordement, chevauchement, adjacence — trois tests
  distincts, sinon un validateur qui ne vérifierait que le débordement passerait.
- Un test de fin de partie sur grille réduite, un test de tir après la fin, et un test où le joueur
  touche puis rejoue sans que l'adversaire intervienne.
- À réexaminer si le placement manuel déborde le budget (repli ci-dessus) ou si le niveau Difficile
  s'avère si injouable qu'il nuit à la démonstration.

## Références

- Spec de conception § 1
- ADR 0003 (l'adversaire exploite `ShipsMayTouch`), ADR 0005 (`Fire` renvoie une séquence)
