# Plan de refactor — passage du code à l'anglais

Date : 2026-09-15
Décidé par le binôme, en cours d'exécution du plan `2026-09-15-bataille-navale.md` (11 tâches
closes sur 21).

## 1. Objectif

Tout le **code** de l'application est en anglais. La documentation destinée aux humains
(`README.md`, ADR, spec, `PROMPTS.md`, `REVUE-IA.md`, `CONTEXTE-IA.md`, messages de commit)
reste en français.

### Dans le périmètre

| Zone | Volume mesuré |
|---|---|
| Fichiers `.cs`, `.razor`, `.proto` | 46 fichiers, 2 077 lignes |
| Commentaires et documentation XML | 197 lignes |
| Lignes portant un identifiant français | 184 |
| Noms de méthodes de test | 49 |
| Littéraux de chaîne (assertions, gardes, libellés) | 14 |
| Dossiers et namespaces de test | `Adversaire/` → `Opponent/`, `Domaine/` → `Domain/` |
| Blocs de code du plan d'implémentation | 56 blocs, 1 175 lignes |
| Règle de `CLAUDE.md` § 2 sur les noms de tests | 1 paragraphe |

### Hors périmètre

- La **prose** de tous les documents, y compris celle du plan d'implémentation.
- Les extraits de code des ADR et de la spec (49 lignes) : leurs identifiants sont **déjà**
  anglais (`IGameStore`, `Find`, `Mutate`, `ShotHistory`, `GameError`), seuls des commentaires
  en ligne y sont français. Les laisser ne crée aucune divergence avec le code.
- Les messages de commit, qui restent en français (`CLAUDE.md` § 5.5).

### Décision sur l'interface

Le texte affiché au joueur passe **aussi** en anglais. Conséquence directe : la composition de
flotte par défaut est renommée, puisque ces noms sont à la fois des données de `GameRules` et
des libellés affichés.

| Avant | Après | Taille |
|---|---|---|
| `Porte-avions` | `Carrier` | 5 |
| `Croiseur` | `Battleship` | 4 |
| `Contre-torpilleur` | `Cruiser` | 3 |
| `Sous-marin` | `Submarine` | 3 |
| `Torpilleur` | `Destroyer` | 2 |

C'est la flotte classique anglophone. **Attention** : ces noms sont codés en dur dans les tests
des tâches 13 et 14 du plan d'implémentation — d'où l'obligation de traduire aussi le plan.

## 2. Glossaire canonique

Une traduction incohérente est pire que pas de traduction : elle crée deux vocabulaires pour
une même notion. Ce glossaire fait autorité, et tout écart doit être signalé plutôt qu'improvisé.

### Domaine

| Français | Anglais | Note |
|---|---|---|
| coup, tir | `shot` | l'action ; `Fire` reste le verbe |
| case, cellule | `cell` | |
| grille | `grid` | |
| navire | `ship` | |
| flotte | `fleet` | |
| partie | `game` | |
| joueur | `player` | |
| adversaire | `opponent` | |
| tireur | `shooter` | |
| touche | `hit` | |
| manqué | `miss` / `missed` | |
| coulé | `sunk` | |
| placement | `placement` | déjà anglais |
| graine | `seed` | |
| historique | `history` | |
| verrou | `lock` | |

### Algorithmes

| Français | Anglais | Note |
|---|---|---|
| jouées, joués | `alreadyShot` | ensemble des cases déjà tirées |
| restantes, restants | `remaining` | |
| candidat, candidats | `candidate`, `candidates` | |
| voisin, voisins | `neighbor`, `neighbors` | orthographe **américaine**, sans `u` |
| couronne | `halo` | voisinage de Moore d'un navire coulé |
| interdite | `blocked` | |
| prolongements | `lineExtensions` | prolongement d'un alignement de touches |
| chasse | `hunt` | |
| ratissage | `target` | phase « target » de hunt/target |
| parité paire | `evenParity` | |
| repli | `fallback` | |
| tentative | `attempt` | |
| relance complète | `fullRestart` | |
| réserve | `pool` | |
| meilleur, meilleures | `best` | |
| score | `score` | déjà anglais |
| poids | `weight` | |

### Membres à renommer

Aucun n'est référencé depuis un autre fichier — les deux `HistoriqueDepuis` sont des
**homonymes indépendants** (l'un `private` dans `BattleShip.API`, l'autre `internal` dans
`BattleShip.Tests`). La traduction est donc parallélisable fichier par fichier.

| Fichier | Avant | Après |
|---|---|---|
| `DensityStrategy.cs` | `PoidsToucheNonCoulee` | `UnsunkHitWeight` |
| | `EstLegal` | `IsLegal` |
| | `PlacementsPossibles` | `PossiblePlacements` |
| | `CouronneDe` | `HaloOf` |
| | `TirDeRepli` | `FallbackShot` |
| `HuntTargetStrategy.cs` | `TirDeChasse` | `HuntShot` |
| | `CandidatsDeRatissage` | `TargetCandidates` |
| | `AjouterProlongements` | `AddLineExtensions` |
| | `AjouterSiValide` | `AddIfValid` |
| | `Voisins` | `Neighbors` |
| `FleetPlacer.cs` | `MaxTentativesParNavire` | `MaxAttemptsPerShip` |
| | `MaxRelancesCompletes` | `MaxFullRestarts` |
| `StrategyBenchmark.cs` | `JoueUnePartie` | `PlayGame` |
| | `HistoriqueDepuis` | `HistoryFrom` |
| `StrategyInvariantTests.cs` | `HistoriqueDepuis` | `HistoryFrom` |
| | `CoupsJoues` | `ShotsPlayed` |
| `HuntTargetStrategyTests.cs` | `Historique` | `History` |
| `FleetPlacerTests.cs` | `Graines` | `Seeds` |
| `GameTests.cs` | `PartieMinuscule` | `TinyGame` |
| `InMemoryGameStoreTests.cs` | `PartieSurGrandeGrille` | `GameOnFullGrid` |
| `PlacementRulesTests.cs` | `Petite` | `Small` |

### Noms de tests

Les 49 méthodes passent du français descriptif à l'anglais descriptif, en conservant
**exactement** la même intention. Exemples :

| Avant | Après |
|---|---|
| `Un_navire_neuf_n_est_pas_coule` | `A_new_ship_is_not_sunk` |
| `Un_tir_hors_grille_est_refuse` | `A_shot_outside_the_grid_is_rejected` |
| `Deux_navires_qui_se_touchent_sont_refuses` | `Two_touching_ships_are_rejected` |
| `Un_seul_tir_simultane_sur_la_meme_case_reussit` | `Only_one_concurrent_shot_on_the_same_cell_succeeds` |
| `Les_trois_niveaux_sont_ordonnes_par_efficacite` | `The_three_levels_are_ordered_by_efficiency` |

La convention `Mots_separes_par_des_underscores` est conservée : elle rend la sortie de test
lisible et ne dépend pas de la langue.

## 3. Invariants de sûreté

Ce refactor est un **renommage**, pas une réécriture. Rien de ce qui suit ne doit changer.

1. **Le compte de tests reste à 108.** Aucun test ajouté, aucun retiré, aucun fusionné.
2. **Aucune assertion n'est affaiblie.** Même opérateur, mêmes valeurs, même ordre. Les seuils
   chiffrés du benchmark (`< 55`, ordre des trois moyennes) sont intouchables.
3. **Aucun `[InlineData]`, `[Theory]`, `[MemberData]` ni cas de `TheoryData` n'est retiré.**
4. **Aucune logique de contrôle ne bouge** : ordre des gardes, conditions, bornes de boucle.
5. **Les mesures restent reproductibles** : les graines, le nombre de parties et l'ordre des
   appels à `Random` sont inchangés. Le benchmark doit toujours rendre 95,69 / 52,60 / 41,22.
6. **Les contrats publics déjà anglais ne bougent pas** — sauf les noms de flotte, qui sont
   des données.

## 4. Découpage en lots

Les lots 2 à 5 sont indépendants et parallélisables ; le lot 1 les précède.

| Lot | Contenu | Dépendance |
|---|---|---|
| **1. Glossaire** | Confronter le glossaire ci-dessus au code réel, compléter les manques | — |
| **2. Domaine** | 9 fichiers de `BattleShip.Models` portant du français | lot 1 |
| **3. Serveur** | 5 fichiers de `BattleShip.API` (stratégies, store, benchmark, validation) | lot 1 |
| **4. Tests** | 11 fichiers, + renommage des dossiers et namespaces | lot 1 |
| **5. Front** | `BattleShip.App` — aucun français détecté, vérification seule | lot 1 |
| **6. Plan** | 56 blocs de code du plan d'implémentation (tâches 12 à 21) | lots 2-4 |
| **7. CLAUDE.md** | Règle § 2 sur les noms de tests | — |

## 5. Vérification

Dans cet ordre, et chaque étape conditionne la suivante :

1. `dotnet build` — 0 avertissement, 0 erreur.
2. `dotnet test` — **108/108**, même compte qu'avant le refactor.
3. `dotnet test --filter The_three_levels_are_ordered_by_efficiency` — les moyennes mesurées
   doivent être **identiques** : 95,69 / 52,60 / 41,22. Une dérive signalerait qu'un appel à
   `Random` a changé d'ordre ou de nombre.
4. Recherche résiduelle : aucun identifiant ni commentaire français dans les fichiers de code.
5. Relecture adverse : un contrôle indépendant que rien du § 3 n'a bougé, assertion par
   assertion.

## 6. Commits

Un sujet par commit (`CLAUDE.md` § 5.5) :

1. `docs: ajoute le plan de refactor du code vers l'anglais`
2. `refactor: passe le code de l'application à l'anglais`
3. `docs: aligne le plan d'implémentation et CLAUDE.md sur le code anglais`

## 7. Risques

| Risque | Parade |
|---|---|
| Une assertion affaiblie au passage | Relecture adverse dédiée, comparaison assertion par assertion |
| Un ordre d'appel à `Random` modifié | Les trois moyennes du benchmark servent de témoin chiffré |
| Le plan non traduit réintroduit du français en tâche 12 | Le lot 6 est dans le périmètre, pas en option |
| `CLAUDE.md` continue d'imposer le français | Le lot 7 lève la règle |
| Un renommage de flotte casse les tests des tâches 13-14 | Les noms sont traduits dans le plan au lot 6, en même temps |
