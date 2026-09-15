# Revues de propositions IA

Trois revues argumentées minimum. Aucune erreur n'est exigée ; chaque conclusion doit être
étayée.

> **État au 2026-09-15** : trois revues (1, 3 et 4) sont complètes et étayées par une
> exécution. La revue 2 reste **ouverte** : l'hypothèse et le résultat attendu y sont énoncés
> *avant* exécution, comme le demande la discipline de vérification, mais l'exécution n'a pas
> encore eu lieu. Elle doit être close avant la remise.

---

## Revue 1 — L'état du dépôt décrit par `CLAUDE.md` § 1 bis

- **Proposition et référence dans le dépôt** : `CLAUDE.md` § 1 bis, « Au 2026-09-15, rien
  n'est encore échafaudé : aucun `.slnx`, aucun `.csproj`, aucun fichier source », et « le
  `.git/` interne de `csharp-school/` entrera en conflit : l'exclure via `.gitignore`, le
  déplacer hors du dépôt rendu, ou en faire un sous-module — décision à consigner en ADR ».

- **Hypothèse à vérifier** : pour que la section d'amorçage soit applicable telle quelle, il
  faut qu'aucun projet n'existe, que `global.json` soit absent de la racine, et que
  `csharp-school/` soit **à l'intérieur** du dépôt rendu.

- **Scénario, données ou commande** :

  ```bash
  cd /home/luca/git/9-2-2-Env-aspnet/BattleShip
  ls -a
  find . -type f -not -path './.git/*'
  find .. -maxdepth 2 -name global.json
  dotnet --version
  ```

- **Résultat attendu avant exécution** : si le § 1 bis est à jour, la commande `find` ne doit
  remonter que `CLAUDE.md`, et `csharp-school/` doit apparaître dans le listing de `ls -a`.

- **Erreur que ce contrôle pourrait détecter** : une conception ou un plan bâtis sur un état
  de dépôt périmé — refaire l'échafaudage par-dessus l'existant, ou ouvrir un ADR pour un
  problème de sous-module qui n'existe pas.

- **Résultat réellement observé** : le § 1 bis est **périmé sur trois points**.
  1. `BattleShip.slnx` et les quatre projets existent, avec les pages modèle
     `Counter.razor` et `Weather.razor` encore en place.
  2. `csharp-school/` n'est **pas** dans le dépôt : le dépôt Git est `BattleShip/`, et
     `csharp-school/` est un répertoire **frère**. Le conflit de `.git` n'existe pas.
  3. `global.json` est resté dans `csharp-school/Ressources Bataille Navale/` et **n'est pas
     à la racine** ; le SDK utilisé est donc 10.0.401 sans épinglage.

- **Décision et justification** : **rejetée** en tant que description de l'état courant, et
  **adaptée** en conséquences :
  - aucun ADR n'est ouvert sur le sous-module — la spec § 9 note explicitement le problème
    comme sans objet, plutôt que de le laisser croire non traité ;
  - la copie de `global.json` à la racine et la purge des pages modèle deviennent la
    **première tâche** du plan d'implémentation ;
  - `CLAUDE.md` § 1 bis doit être corrigé, sinon il induira en erreur à chaque session.

- **Preuves reproductibles et liens vers les commits** : les commandes ci-dessus sont
  rejouables telles quelles. Conséquences consignées dans
  `docs/superpowers/specs/2026-09-15-bataille-navale-design.md` § 9 (commit `935a8b7`).

- **Après correction éventuelle : résultat avant / après** : à compléter après la tâche 1 du
  plan — `find . -maxdepth 1 -name global.json` doit passer de « aucun résultat » à
  `./global.json`, et `dotnet --version` doit rester en 10.x en étant désormais épinglé.

- **Limites et points non vérifiés** : le contrôle porte sur la présence des fichiers, pas sur
  leur contenu. Il n'établit pas que l'échafaudage existant est conforme aux contraintes
  (Minimal API et non contrôleurs, références inter-projets correctes) — à vérifier
  séparément lors de la tâche 1.

---

## Revue 2 — *(ouverte)* L'argument de performance des exceptions (ADR 0004)

- **Proposition et référence dans le dépôt** : `CLAUDE.md` § 3 bis, décision en suspens :
  « Argument à instruire si la question est ouverte : le coût des exceptions en boucle serrée
  quand l'adversaire probabiliste évalue beaucoup de coups — à **mesurer**, pas à supposer. »
  L'IA a soutenu que cette mesure n'a pas lieu d'être dans la conception retenue
  (`docs/adr/0004-result-refus-metier.md`, commit `f379852`).

- **Hypothèse à vérifier** : pour que l'argument de l'IA tienne, il faut que
  `DensityStrategy` ne provoque **aucune** levée d'exception par tour d'adversaire —
  c'est-à-dire qu'elle n'appelle jamais le moteur pour évaluer un coup candidat, et qu'elle ne
  propose jamais un coup invalide.

- **Scénario, données ou commande** : une fois `DensityStrategy` écrite, jouer N parties
  complètes contre elle avec un compteur d'exceptions de domaine levées, ou un point d'arrêt
  sur le constructeur de `GameError`. À compléter avec la commande exacte.

- **Résultat attendu avant exécution** : **zéro** refus métier émis pendant les tours de
  l'adversaire, quel que soit le niveau. Les seuls refus observés doivent provenir de coups
  du joueur.

- **Erreur que ce contrôle pourrait détecter** : une implémentation de `DensityStrategy` qui
  aurait été écrite en « essayant » des coups contre le moteur — auquel cas l'argument de
  l'ADR 0004 s'effondre et la question de la performance redevient réelle.

- **Résultat réellement observé** : **non exécuté à ce jour.** L'argument repose pour l'instant
  sur une analyse de la structure prévue du code, pas sur une mesure. L'ADR 0004 le dit
  explicitement.

- **Décision et justification** : provisoirement **acceptée**, sur la base que le choix de
  `Result<T>` se justifie de toute façon par la lisibilité et par la traduction vers les
  façades — la performance n'est pas le critère décisif. À confirmer ou infirmer par le
  contrôle ci-dessus.

- **Preuves reproductibles et liens vers les commits** : `docs/adr/0004-result-refus-metier.md`
  (commit `f379852`).

- **Après correction éventuelle : résultat avant / après** : sans objet à ce stade.

- **Limites et points non vérifiés** : tout. Une analyse structurelle n'est pas une mesure ;
  cette revue n'est pas close.

---

## Revue 3 — Les chiffres de performance des stratégies

- **Proposition et référence dans le dépôt** : l'IA a avancé, pour justifier le périmètre à
  trois niveaux d'adversaire, des nombres moyens de coups de ≈ 96 (aléatoire), ≈ 65
  (chasse/cible), ≈ 60 (avec parité) et ≈ 42 (densité probabiliste). Repris en tant
  qu'**hypothèse** dans `docs/adr/0003-strategie-adversaire.md` et dans la spec § 4.

- **Hypothèse à vérifier** : ces valeurs proviennent de la littérature sur une grille 10×10
  classique **sans** règle de non-adjacence. Notre ADR 0006 interdit aux navires de se
  toucher, ce qui donne plus d'information à la densité et **change donc les valeurs**. Pour
  que le périmètre à trois niveaux soit justifié, il suffit que l'**ordre** soit respecté :
  aléatoire > chasse/cible+parité > densité.

- **Scénario, données ou commande** : `StrategyBenchmark.Run` (tâche 11,
  `BattleShip.API/Benchmark/StrategyBenchmark.cs`), N = 200 parties par stratégie, graine de
  placement dérivée de `seed + i` (même flotte pour les trois stratégies à un indice de partie
  donné), avec les règles réellement retenues (`GameRules.Default` : 10×10, flotte 5-4-3-3-2,
  `ShipsMayTouch: false`). Commande exacte, testée par `StrategyBenchmarkTests` :

  ```bash
  dotnet test --filter "FullyQualifiedName~StrategyBenchmark" --logger "console;verbosity=detailed"
  ```

  Les trois moyennes elles-mêmes ont été relevées via un petit programme jetable référençant
  `BattleShip.API` et appelant `StrategyBenchmark.Run(GameRules.Default, games: 200, seed:
  20260915)` directement (le test xUnit ne fait qu'asserter l'ordre et le seuil, il n'imprime
  pas les valeurs) — voir le rapport de la tâche 11 pour le script.

- **Résultat attendu avant exécution** — *énoncé le 2026-09-15, avant toute implémentation* :
  l'ordre `RandomStrategy` > `HuntTargetStrategy` > `DensityStrategy` sera respecté, et
  `DensityStrategy` restera **sous 55 coups** en moyenne.

- **Erreur que ce contrôle pourrait détecter** : une stratégie fautive — une densité qui ne
  pondérerait pas les touches non coulées ferait à peine mieux que la chasse/cible ; une
  chasse/cible qui ne ratisserait pas correctement les voisines s'effondrerait vers les
  valeurs de l'aléatoire. Si l'ordre n'est pas respecté, le périmètre à trois niveaux n'est
  plus défendable et doit être revu.

- **Résultat réellement observé** : sur 200 parties par stratégie, graine 20260915,
  `GameRules.Default` :

  | Stratégie   | Moyenne | Min | Max |
  |---|---|---|---|
  | `Random`     | **95,69** coups | 80 | 100 |
  | `HuntTarget` | **52,60** coups | 29 | 67  |
  | `Density`    | **41,22** coups | 26 | 58  |

  `dotnet test --filter "FullyQualifiedName~StrategyBenchmark" --logger "console;verbosity=detailed"`
  → `Passed! - Failed: 0, Passed: 2, Skipped: 0, Total: 2` (les deux `[Fact]` de
  `StrategyBenchmarkTests`, dont l'assertion d'ordre et de seuil ci-dessus).

  L'ordre `95,69 > 52,60 > 41,22` respecte l'attendu, et `41,22 < 55`.

- **Décision et justification** : **confirmée.** L'attendu énoncé le 2026-09-15 avant toute
  implémentation est vérifié par la mesure : le périmètre à trois niveaux d'adversaire est
  défendable tel quel, sans ajustement de seuil ni de stratégie. Les valeurs de la littérature
  citées initialement (≈ 96 / ≈ 65-60 / ≈ 42) se sont révélées étonnamment proches malgré la
  règle de non-adjacence — la marge de `HuntTarget` (52,6 contre ≈ 60-65 attendu par analogie)
  est même meilleure que l'hypothèse, probablement grâce au filtre de parité qui élimine la
  moitié de la grille dès la phase de chasse.

- **Preuves reproductibles et liens vers les commits** :
  `BattleShip.API/Benchmark/StrategyBenchmark.cs`,
  `BattleShip.Tests/Opponent/StrategyBenchmarkTests.cs`,
  `docs/adr/0003-strategie-adversaire.md` (tâche 11). La commande ci-dessus est rejouable
  telle quelle et redonne le même ordre (test
  `The_measurement_is_reproducible_for_an_equal_seed` : deux exécutions à graine 1 sur 50 parties
  produisent des moyennes strictement égales).

- **Après correction éventuelle : résultat avant / après** : sans objet — aucune correction
  n'a été nécessaire, l'attendu a été confirmé du premier coup.

- **Limites et points non vérifiés** : la mesure porte sur une seule composition de flotte et
  une seule taille de grille (`GameRules.Default`, 10×10). Elle ne dit rien du comportement sur
  une grille réduite ou une flotte différente. Elle ne mesure pas non plus l'effet de
  `ExtraTurnOnHit` isolément : les trois stratégies en bénéficient également, donc la
  comparaison relative reste valide, mais la valeur absolue de chaque moyenne inclut cet
  avantage.

---

## Revue 4 — Le pouvoir discriminant du test de concurrence du store

Cette revue a changé de conclusion à deux reprises au fil des mesures. Les trois tentatives
sont rapportées dans l'ordre où elles ont eu lieu, avec ce que chacune a infirmé.

- **Proposition et référence dans le dépôt** : `BattleShip.Tests/Domain/InMemoryGameStoreTests.cs`,
  test `Only_one_concurrent_shot_on_the_same_cell_succeeds` (5 `[InlineData]`), qui vérifie
  qu'un seul de 32 tirs concurrents sur la même case réussit. L'implémentation testée est
  `InMemoryGameStore.Mutate` (`BattleShip.API/Stores/InMemoryGameStore.cs`, commits `883fe0b`
  puis `37f9734`), qui protège chaque partie par un verrou obtenu via
  `ConcurrentDictionary<Guid, object>.GetOrAdd`.

- **Hypothèse à vérifier** : pour qu'un passage au vert de ce test signifie quelque chose,
  il faut qu'il **puisse échouer** en l'absence du verrou — pas seulement une fois, mais de
  façon fiable — et cela **sans dégrader la durée de la suite complète**, qui doit rester dans
  l'ordre de grandeur de la seconde pour les quatorze tâches restantes du plan.

- **Scénario, données ou commande** : commenter temporairement le `lock (gameLock)` dans
  `Mutate` (corps conservé, exécuté sans synchronisation), puis lancer trois fois de suite :

  ```bash
  dotnet test --filter "Only_one_concurrent_shot_on_the_same_cell_succeeds"
  ```

  répété à chacune des trois versions du mécanisme de départ des 32 tirs concurrents :
  1. `Enumerable.Range(0, 32).Select(_ => Task.Run(...))` (version d'origine, commit `883fe0b`) ;
  2. la même chose précédée d'un départ synchronisé par `Barrier(32)` (`depart.SignalAndWait()`
     avant `store.Mutate(...)`, commit `44bd57c`) ;
  3. 32 `Thread` dédiés (et non mis en file sur le pool) partageant la même `Barrier(32)`
     (commit `c604123`).

  À chaque version, la durée de la suite complète (`dotnet test`) est mesurée **avant** de
  toucher au verrou.

- **Erreur que ce contrôle pourrait détecter** : une implémentation de `Mutate` qui ne
  sérialise pas réellement les mutations concurrentes sur une même partie — verrou absent,
  verrou mal indexé (un seul verrou partagé par erreur pour toutes les parties, ou une clé de
  verrou qui ne correspond pas à l'identifiant de la partie), ou lecture de l'état hors du
  verrou (le défaut corrigé au commit `37f9734`).

- **Résultat attendu avant exécution, puis résultat réellement observé, mesure par mesure** :

  **Mesure 1 — `Task.Run` seul (commit `883fe0b`).** Attendu : sans verrou, les 32 appels
  concurrents devraient laisser plusieurs threads passer les gardes de `Game.Fire` avant
  qu'aucun n'ait fini de muter l'état ; j'attendais un échec sur les 3 exécutions, probablement
  systématique.

  Observé, 3 exécutions sans verrou :
  - Exécution 1 : ÉCHEC — `execution: 3`, `Expected: 1, Actual: 2`
  - Exécution 2 : ÉCHEC — `execution: 3`, `Expected: 1, Actual: 2`
  - Exécution 3 : **succès complet, 5/5**, sans aucune synchronisation en place

  → **Infirmé** : le test ne détectait la course que **2 fois sur 3**. `Task.Run` seul ne
  garantit pas que les tâches se chevauchent réellement — l'ordonnanceur peut démarrer et
  terminer les premières avant même d'avoir lancé les dernières.

  **Mesure 2 — `Barrier(32)` sur `Task.Run` (commit `44bd57c`).** Attendu : en forçant les 32
  tâches à démarrer réellement ensemble, la collision devient indépendante du hasard
  d'ordonnancement et le test échoue sur les **3 exécutions sur 3**, probablement avec un
  `Actual` plus élevé qu'avant.

  Observé, 3 exécutions sans verrou :
  - Exécution 1 : **succès complet, 5/5**, toujours sans aucune synchronisation en place
  - Exécution 2 : ÉCHEC — `execution: 4`, `Expected: 1, Actual: 3`
  - Exécution 3 : ÉCHEC — `execution: 4` et `execution: 1`, `Expected: 1, Actual: 4`

  → **Infirmé, et la correction s'est révélée pire que le mal.** Toujours 2 échecs sur 3, pas
  3 sur 3 : la `Barrier` a rendu les échecs plus sévères (jusqu'à `Actual: 4`) sans améliorer
  le taux de détection. Pire, la suite complète (`dotnet test`) est passée de **0,86 s à 15-18 s
  par exécution du test ciblé** (mesuré indépendamment par la coordination sur la suite
  complète : 0,86 s → 18 s). Cause identifiée : `Task.Run` passe par le pool de threads, qui
  n'injecte de nouveaux threads qu'au compte-gouttes (environ un toutes les 500 ms au-delà du
  seuil initial) ; la `Barrier(32)` doit attendre que 32 threads existent avant de se relâcher,
  donc elle synchronise le *départ nominal* des tâches mais pas leur *entrée effective* dans le
  code critique. **Cette tentative a été abandonnée après la mesure — parce qu'elle dégradait
  la suite pour un gain de détection nul, pas parce qu'elle était mal écrite.**

  **Mesure 3 — `Barrier(32)` sur `Thread` dédiés (commit `c604123`).** Attendu : des `Thread`
  démarrés directement (hors du pool) ne subissent pas l'injection progressive ; la barrière
  devrait se relâcher en quelques millisecondes et les 32 tirs entrer réellement ensemble dans
  `Mutate`. J'attendais une détection plus fiable que les deux mesures précédentes, sans
  préjuger d'un 3 sur 3 exact.

  Durée de la suite complète mesurée **avant** de toucher au verrou : `dotnet test` →
  `Passed! ... Duration: 842 ms` (puis 841 ms après restauration du verrou) — revenue à
  l'ordre de grandeur d'avant les deux mesures précédentes (810/806/856/842/841 ms observés au
  fil des étapes de la tâche 7, contre 15-18 s avec la Barrier sur `Task.Run`).

  Observé, 3 exécutions sans verrou (verrou commenté dans `Mutate`) :
  - Exécution 1 : ÉCHEC — 4 des 5 `[InlineData]` en échec (`execution: 4` → `Actual: 4`,
    `execution: 1` → `Actual: 6`, `execution: 2` → `Actual: 3`, `execution: 5` → `Actual: 4`),
    en 88 ms
  - Exécution 2 : ÉCHEC — 4 des 5 en échec (`Actual: 2`, `Actual: 3`, `Actual: 5`, `Actual: 2`),
    en 97 ms
  - Exécution 3 : ÉCHEC — 4 des 5 en échec (`Actual: 2`, `Actual: 5`, `Actual: 3`, `Actual: 3`),
    en 92 ms

  → **Confirmé, et net.** Les 3 exécutions échouent (contre 2 sur 3 pour les deux mesures
  précédentes), avec 4 des 5 `[InlineData]` en échec à chaque fois, et une durée de test
  (88-97 ms) comparable à la version d'origine — sans le surcoût de la Barrier sur le pool.
  C'est la première mesure de cette revue qui confirme son propre attendu plutôt que de
  l'infirmer.

- **Décision et justification** : le test est **conservé avec des `Thread` dédiés synchronisés
  par une `Barrier`** (commit `c604123`), et c'est la mesure — pas une préférence de conception
  — qui a tranché entre les trois versions : la version 1 était rapide mais peu fiable (2/3), la
  version 2 était tout aussi peu fiable et 20 fois plus lente (rejetée sur cette seule mesure),
  la version 3 est à la fois rapide (retour à l'ordre de grandeur d'origine) et nettement plus
  fiable (3/3 exécutions en échec, 4/5 `[InlineData]` à chaque fois). Elle reste néanmoins
  **imparfaite** : sur les 3 exécutions, 1 `[InlineData]` sur 5 en moyenne n'a pas détecté la
  course (voir le détail par exécution ci-dessus). **Ce test ne doit pas être invoqué seul
  comme preuve d'absence de course : il détecte une régression du verrou de façon probable,
  pas certaine.** La garantie principale reste la revue de code de `Mutate` (verrou indexé par
  `Guid`, lecture de la partie faite sous le verrou).

- **Preuves reproductibles et liens vers les commits** :
  - `883fe0b` — ajout du store et du test de concurrence initial (`Task.Run` seul).
  - `37f9734` — correction de la lecture hors verrou dans `Mutate`, documentation de `Find`
    et du dictionnaire de verrous.
  - `44bd57c` — tentative rejetée : départ synchronisé par `Barrier(32)` sur `Task.Run`.
  - `c604123` — version retenue : `Barrier(32)` sur 32 `Thread` dédiés, assertions inchangées.
  - Commande rejouable telle quelle (avec le `lock` commenté manuellement pour l'expérience) :
    `dotnet test --filter "Only_one_concurrent_shot_on_the_same_cell_succeeds"`.

- **Après correction éventuelle : résultat avant / après** :
  - *Avant toute mesure* (verrou en place, test original `Task.Run`, commit `883fe0b`) :
    3 exécutions consécutives, 7/7 à chaque fois, suite complète ≈ 0,81-0,86 s.
  - *Après la tentative rejetée* (Barrier sur `Task.Run`, commit `44bd57c`, verrou en place) :
    5/5 sur les exécutions ciblées, mais suite complète à 15-18 s par exécution du test —
    régression de durée d'un facteur ≈ 20 pour un taux de détection sans verrou inchangé
    (2/3, contre 2/3 sans Barrier).
  - *Après la version retenue* (Barrier sur `Thread` dédiés, commit `c604123`, verrou en
    place) : 5/5 sur les exécutions ciblées (84 ms), suite complète à 841-842 ms — revenue à
    l'ordre de grandeur d'origine — et taux de détection sans verrou passé de 2/3 à **3/3**
    (avec 4/5 `[InlineData]` en échec par exécution, contre 1/5 auparavant).

- **Limites et points non vérifiés** : même avec des `Thread` dédiés, 1 `[InlineData]` sur 5
  en moyenne n'a pas détecté l'absence de verrou sur les 3 exécutions observées — la fenêtre de
  course reste probabiliste, pas garantie à 100 %. Une mesure plus poussée (augmenter le nombre
  de tirs concurrents au-delà de 32, ou introduire un point de contention supplémentaire dans
  `Board.Fire` lui-même) n'a pas été tentée et est laissée en piste pour une tâche ultérieure si
  une garantie plus forte est un jour nécessaire. Cette revue ne porte que sur la case `(0,0)`
  d'une grille 10×10 ; elle ne dit rien d'un comportement à plus grande échelle (plusieurs
  cases visées simultanément, par exemple).

## Revue 5 — Le pouvoir discriminant du test de course `Read` vs `Mutate` (tâche 14)

Cette revue raconte une hypothèse **corrigée par une mesure, deux fois de suite** : un premier
essai (au niveau HTTP/gRPC) a échoué à démontrer un pouvoir discriminant malgré un
chevauchement horloge confirmé ; relocalisé au niveau du store sur la suggestion du
coordinateur, le même test discrimine de façon fiable, mais pas via l'exception exactement
annoncée. Les deux résultats sont rapportés tels quels (CLAUDE.md § 6, règle 6).

### Première tentative (niveau HTTP/gRPC) — négative

- **Proposition et référence dans le dépôt (au moment de cette tentative)** :
  `BattleShip.Tests/Api/FireGrpcTests.cs`, test
  `Concurrent_fires_and_reads_on_the_same_game_never_return_500_or_corrupt_state` — un test
  additionnel, au-delà des cinq tests donnés par le brief de tâche 14, demandé pour vérifier
  la première occasion du projet où une mutation concurrente réelle (`Fire` gRPC, via
  `IGameStore.Mutate`) peut croiser une lecture qui énumère le même état (`GET /games/{id}`,
  via `IGameStore.Read`) — cas que la tâche 13 avait laissé explicitement non vérifié faute de
  mutation concurrente à l'époque.

- **Hypothèse à vérifier** : que le test **puisse échouer** si `GameEndpoints` revenait à lire
  par `store.Find(id)` plutôt que `store.Read(id, ...)` — c'est-à-dire s'il expose vraiment le
  risque documenté dans `IGameStore.cs` : une lecture qui énumère `Game.History` (une
  `List<ShotRecord>`) pendant qu'un tir y ajoute une entrée doit lever
  `InvalidOperationException: Collection was modified`.

- **Scénario, données ou commande** : `GameEndpoints`'s route `GET /games/{id:guid}` a été
  temporairement ramenée à `store.Find(id)` (verrou contourné), puis le test ci-dessus a été
  exécuté à travers **quatre conceptions successives**, chacune plus agressive que la
  précédente :
  1. ~100 tirs gRPC séquentiels pour faire grossir l'historique, puis 32 `Fire` uniques
     contre 32 fils faisant chacun 3 lectures ;
  2. la même forme, mais avec 1500 paires de tirs de préremplissage (3000 entrées
     d'historique) et 64 fils de tir jouant chacun 30 tirs séquentiels (1920 tirs), contre
     32 fils de lecture tournant en continu ;
  3. la même chose sur une grille 300×300 plutôt que 60×60 (voir plus bas pourquoi) ;
  4. une salve simultanée de 300 tirs uniques (un seul par fil, tous libérés par la même
     `Barrier`, à l'image des deux autres tests de course du dépôt), contre 64 fils de
     lecture en continu.

  Chaque exécution a été **instrumentée** (version jetable, non conservée) : un
  luminaire temporaire horodatait chaque `_history.Add` réel (dans `Game.Fire`) et chaque
  appel réel à `history.Where(...).Select(...).ToList()` (dans `DtoMappings`), pour vérifier
  après coup si les deux se chevauchaient réellement en temps horloge — pas seulement en
  théorie.

  Commande rejouable (verrou de lecture remis à `Find` manuellement pour l'expérience) :
  ```bash
  dotnet test --filter "Concurrent_fires_and_reads_on_the_same_game_never_return_500_or_corrupt_state"
  ```

- **Résultat attendu avant exécution** : au moins une des quatre conceptions ferait échouer le
  test avec une `InvalidOperationException: Collection was modified` et/ou une réponse HTTP
  500, une fois la lecture ramenée à `Find`.

- **Erreur que ce contrôle pourrait détecter** : un retour accidentel de
  `IGameStore.Read` à `IGameStore.Find` dans un endpoint qui énumère l'état d'une partie —
  exactement la régression que le commentaire de `IGameStore.cs` anticipe.

- **Résultat réellement observé** :
  - Les **quatre** conceptions sont restées **vertes** (aucune exception, aucun 500) malgré
    la lecture ramenée à `Find`.
  - L'instrumentation a pourtant confirmé un chevauchement réel : sur la conception 3
    (grille 300×300, 1920 tirs), 224 occurrences d'un `_history.Add` tombant à l'intérieur
    de la fenêtre horloge d'un appel `Where(...).Select(...).ToList()` ont été mesurées sur
    une seule exécution — sans qu'aucune n'ait provoqué l'exception.
  - Un cinquième palier (2000 fils de tir simultanés) a été tenté puis abandonné : au lieu de
    renforcer le test, la sursouscription (2032 fils pour 16 cœurs) a provoqué une pathologie
    d'ordonnancement — le test n'a pas terminé en 180 s. Ce n'est pas un résultat exploitable,
    seulement la preuve qu'« ajouter des fils » cesse d'aider passé un certain point.
  - **Contrôle de cohérence, hors ASP.NET Core et gRPC** : deux répliques isolées du même
    mécanisme (`dotnet run --file`, fichiers non conservés) confirment qu'il est bien réel et
    reproductible sur cette machine : une boucle serrée de 50 000 `Add` contre 8 lecteurs en
    boucle produit l'exception dans 8 lecteurs sur 8 ; une écriture « éparse » (une toutes les
    ~1 ms, plus proche du rythme réel d'un tir) contre 16 lecteurs en produit 4069 sur 11357
    lectures. Le mécanisme n'est donc pas en cause — seul le passage par la pile
    ASP.NET Core/gRPC réelle, des deux côtés, semble diluer suffisamment la fréquence de
    tentative pour ne pas capter la fenêtre exacte requise par l'énumérateur de `List<T>`,
    dans un budget de temps raisonnable pour un test automatisé.

- **Décision (à ce stade de l'investigation)** : le test HTTP/gRPC est **conservé mais
  suspect** — sa portée est revue à la baisse dans son propre commentaire (il vérifie
  l'absence de 500/corruption sous charge réelle, pas le pouvoir discriminant contre `Find`)
  et la revue s'arrête là, en l'état. C'est cette décision que le coordinateur a corrigée :
  un test qui coûte 4-6 s (la suite est passée de 5 s à 10 s, mesuré) et ne discrimine rien
  est pire qu'aucun test — il ralentit chaque vérification des tâches restantes tout en
  donnant une fausse assurance. Voir la suite ci-dessous.

### Seconde tentative (niveau store, directe) — positive, avec une réserve sur le message exact

Le rapport de la première tentative contenait déjà sa propre solution : le mécanisme se
reproduit *de façon fiable en isolation* (8/8, puis 4069/11357). Ce n'est pas le bug qui
manquait, c'est la pile réseau qui absorbait la fenêtre. Le test a donc été redescendu au
niveau où il discrimine : `BattleShip.Tests/Domain/InMemoryGameStoreTests.cs`, test
`Concurrent_fires_and_reads_never_throw_and_return_consistent_snapshots`, qui appelle
`IGameStore.Mutate`/`Read` directement — aucun HTTP, aucun gRPC.

- **Proposition et référence dans le dépôt** :
  `InMemoryGameStoreTests.Concurrent_fires_and_reads_never_throw_and_return_consistent_snapshots`.
  Une partie est construite directement via `Game.Start` sur une grille 100×100, avec les deux
  flottes placées par `FleetPlacer` ; chaque tir concurrent est fait sur une case pré-filtrée
  pour éviter les cellules des DEUX flottes, de sorte que chaque coup est un manqué garanti
  (la partie ne se termine donc jamais et le tour continue d'alterner). Chaque `Mutate` tire
  pour **le joueur dont c'est effectivement le tour**, décidé à l'intérieur même du lambda
  (`g.CurrentPlayer == Player.Human ? g.PlayerFires(at) : g.OpponentFires(at)`) — un
  correctif direct du bug qui avait fait échouer l'ébauche précédente de cette même piste
  (un tour non rejoué correctement après une touche adverse, mentionné dans la première
  version de cette revue). Chaque `Read` énumère exactement ce que `DtoMappings` énumère en
  production : `Game.History` filtré par tireur (deux fois), plus les deux `ReceivedShots`
  (`HashSet<Coordinate>`) des deux plateaux.

- **Hypothèse à vérifier** : inchangée — que le test **puisse échouer** si `IGameStore.Read`
  perdait son verrou, avec une reproductibilité mesurée sur plusieurs exécutions, pas une
  fois par hasard.

- **Scénario, données ou commande** : le verrou de `InMemoryGameStore.Read` a été retiré
  temporairement (bloc `lock` supprimé, le reste du corps inchangé), puis :

  ```bash
  dotnet test --filter "Concurrent_fires_and_reads_never_throw_and_return_consistent_snapshots"
  ```

  relancé 5 fois de suite. Volumes : 8 fils de tir × 100 tirs chacun (800 tirs), 8 fils de
  lecture tournant en continu tant qu'un tir est en cours.

- **Résultat attendu, énoncé avant exécution** (par le coordinateur) : une
  `InvalidOperationException` « Collection was modified » devait apparaître, de façon
  reproductible sur plusieurs exécutions.

- **Résultat réellement observé** :
  - **5 exécutions sur 5 en échec**, chacune avec plusieurs exceptions (5, 10, 14, 9 puis 8
    occurrences selon l'exécution) au message identique :
    ```
    ArgumentException: Destination array is not long enough to copy all the items in the
    collection. Check array index and length.
    ```
    accompagné, sur une des cinq exécutions, d'une `NullReferenceException` supplémentaire.
  - **Ce n'est pas le message annoncé.** Le message attendu, `InvalidOperationException:
    Collection was modified`, vient de l'énumérateur de `List<T>` (donc du côté
    `Game.History`) ; le message réellement obtenu vient de `HashSet<Coordinate>.ToList()`
    (donc du côté `Board.ReceivedShots`) qui course la même `_receivedShots.Add` que
    `Board.Fire` exécute sous verrou. Un contrôle isolant les deux moitiés de la projection l'a
    confirmé : à ce même volume (800 tirs), la moitié `History` seule (les deux
    `Where(...).Select(...).ToList()`, sans les deux lignes `ReceivedShots`) reste **verte
    3 fois sur 3** même avec le verrou retiré ; en la faisant grossir sensiblement (16 fils ×
    300 tirs = 4800), elle reste verte encore 3 fois sur 3. La course sur `List<T>` reste donc,
    même isolée du réseau, plus difficile à déclencher que celle sur `HashSet<T>` — cohérent
    avec la première tentative, où seule la piste `List<T>` avait été testée.
  - Le verrou de `Read` a été rétabli ; les 5 exécutions suivantes (implémentation correcte)
    sont toutes passées, 380-460 ms chacune.
  - Suite complète après remplacement : **133/133, ~4 s** (contre ~10 s avec l'ancien test
    HTTP/gRPC, et ~5 s avant la tâche 14) — l'objectif du coordinateur (« autour de 5 s ») est
    tenu.

- **Erreur que ce contrôle pourrait détecter** : un retour accidentel de `IGameStore.Read` à
  une lecture non verrouillée (`Find` ou équivalent) dans n'importe quel appelant qui énumère
  `Game.History` et/ou `Board.ReceivedShots` — les deux collections que le commentaire de
  `IGameStore.cs` nomme explicitement comme à risque.

- **Décision et justification** : le test HTTP/gRPC est **supprimé** (il ne prouvait rien et
  coûtait cher) et remplacé par celui-ci. La projection combinée (`History` + `ReceivedShots`)
  est **conservée telle quelle**, plutôt que réduite à `History` seul pour coller au message
  d'exception annoncé : `IGameStore.cs` documente les deux collections comme à risque, et le
  test qui les exerce toutes les deux discrimine réellement, vite (< 0,5 s), et de façon
  reproductible — un test qui échoue avec le bon type d'exception sur une collection voisine
  vaut mieux qu'un test plus étroit qui ne discrimine pas du tout à un volume raisonnable.
  Fabriquer artificiellement la course sur `List<T>` seule (volume démesuré, délai injecté
  dans le code de production) a été explicitement écarté, pour la même raison que dans la
  première tentative.

- **Preuves reproductibles et liens vers les commits** : commandes ci-dessus, rejouables
  telles quelles avec le verrou de `Read` commenté manuellement pour l'expérience ; détail
  complet (les quatre conceptions de la première tentative, les deux répliques isolées hors
  ASP.NET Core/gRPC, et cette seconde tentative) dans le rapport de la tâche 14.

- **Après correction éventuelle : résultat avant / après** :
  - *Avant* (verrou de `Read` retiré) : 5/5 exécutions en échec, `ArgumentException`
    (+ `NullReferenceException` une fois), 5 à 14 occurrences par exécution.
  - *Après* (verrou rétabli, état livré) : 5/5 exécutions au vert, 380-460 ms chacune ; suite
    complète 133/133 en ~4 s.

- **Limites et points non vérifiés** : le mécanisme exact par lequel `HashSet<T>.ToList()`
  produit `ArgumentException`/`NullReferenceException` sous course (plutôt que l'
  `InvalidOperationException` propre d'un `List<T>`) n'a pas été tracé dans le code source de
  `HashSet<T>` — seule l'observation empirique, reproductible, est établie. La course sur
  `List<T>` seule reste, à ce volume, non démontrée en isolation du réseau (verte 3/3 à 800
  tirs et encore 3/3 à 4800) ; un volume plus grand la ferait peut-être basculer, mais cela n'a
  plus d'intérêt pratique une fois la projection combinée reconnue comme suffisante et rapide.
