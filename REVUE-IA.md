# Revues de propositions IA

Trois revues argumentées minimum. Aucune erreur n'est exigée ; chaque conclusion doit être
étayée.

> **État au 2026-09-15** : deux revues (1 et 4) sont complètes et étayées par une exécution.
> Les revues 2 et 3 restent **ouvertes** : l'hypothèse et le résultat attendu y sont énoncés
> *avant* exécution, comme le demande la discipline de vérification, mais l'exécution n'a pas
> encore eu lieu. Elles doivent être closes avant la remise.

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

## Revue 3 — *(ouverte)* Les chiffres de performance des stratégies

- **Proposition et référence dans le dépôt** : l'IA a avancé, pour justifier le périmètre à
  trois niveaux d'adversaire, des nombres moyens de coups de ≈ 96 (aléatoire), ≈ 65
  (chasse/cible), ≈ 60 (avec parité) et ≈ 42 (densité probabiliste). Repris en tant
  qu'**hypothèse** dans `docs/adr/0003-strategie-adversaire.md` et dans la spec § 4.

- **Hypothèse à vérifier** : ces valeurs proviennent de la littérature sur une grille 10×10
  classique **sans** règle de non-adjacence. Notre ADR 0006 interdit aux navires de se
  toucher, ce qui donne plus d'information à la densité et **change donc les valeurs**. Pour
  que le périmètre à trois niveaux soit justifié, il suffit que l'**ordre** soit respecté :
  aléatoire > chasse/cible+parité > densité.

- **Scénario, données ou commande** : endpoint de duel d'IA (backlog nº 1), N = 1000 parties
  par stratégie, `Random` à graine fixe, avec les règles réellement retenues (10×10,
  flotte 5-4-3-3-2, non-adjacence). À compléter avec la commande exacte.

- **Résultat attendu avant exécution** — *énoncé le 2026-09-15, avant toute implémentation* :
  l'ordre `RandomStrategy` > `HuntTargetStrategy` > `DensityStrategy` sera respecté, et
  `DensityStrategy` restera **sous 55 coups** en moyenne.

- **Erreur que ce contrôle pourrait détecter** : une stratégie fautive — une densité qui ne
  pondérerait pas les touches non coulées ferait à peine mieux que la chasse/cible ; une
  chasse/cible qui ne ratisserait pas correctement les voisines s'effondrerait vers les
  valeurs de l'aléatoire. Si l'ordre n'est pas respecté, le périmètre à trois niveaux n'est
  plus défendable et doit être revu.

- **Résultat réellement observé** : **non exécuté à ce jour.**

- **Décision et justification** : les chiffres sont consignés partout comme **hypothèse non
  vérifiée** et ne doivent apparaître dans aucun document comme un résultat tant que cette
  revue n'est pas close.

- **Preuves reproductibles et liens vers les commits** :
  `docs/adr/0003-strategie-adversaire.md` (commit `f379852`).

- **Après correction éventuelle : résultat avant / après** : sans objet à ce stade.

- **Limites et points non vérifiés** : la mesure portera sur une seule composition de flotte
  et une seule taille de grille. Elle ne dira rien du comportement sur une grille réduite.

---

## Revue 4 — Le pouvoir discriminant du test de concurrence du store

- **Proposition et référence dans le dépôt** : `BattleShip.Tests/Domaine/InMemoryGameStoreTests.cs`,
  test `Un_seul_tir_simultane_sur_la_meme_case_reussit` (5 `[InlineData]`), qui vérifie
  qu'un seul de 32 tirs concurrents sur la même case réussit. L'implémentation testée est
  `InMemoryGameStore.Mutate` (`BattleShip.API/Stores/InMemoryGameStore.cs`, commits `883fe0b`
  puis `37f9734`), qui protège chaque partie par un verrou obtenu via
  `ConcurrentDictionary<Guid, object>.GetOrAdd`.

- **Hypothèse à vérifier** : pour qu'un passage au vert de ce test signifie quelque chose,
  il faut qu'il **puisse échouer** en l'absence du verrou — pas seulement une fois, mais de
  façon fiable. Un contrôle de course qui rate le bug une fois sur trois est un contrôle dont
  le vert ne prouve rien.

- **Scénario, données ou commande** : commenter temporairement le `lock (gameLock)` dans
  `Mutate` (corps conservé, exécuté sans synchronisation), puis lancer trois fois de suite :

  ```bash
  dotnet test --filter "Un_seul_tir_simultane_sur_la_meme_case_reussit"
  ```

  Répété une seconde fois après avoir remplacé `Enumerable.Range(0, 32).Select(_ => Task.Run(...))`
  par un départ synchronisé par `Barrier(32)` (chaque tâche appelle `depart.SignalAndWait()`
  avant `store.Mutate(...)`), pour mesurer si la fenêtre de course s'élargit.

- **Résultat attendu avant exécution** — *première mesure, sans Barrier* : sans verrou, les 32
  appels `Task.Run` concurrents devraient laisser plusieurs threads passer les gardes de
  `Game.Fire` avant qu'aucun n'ait fini de muter l'état ; j'attendais un échec sur les 3
  exécutions, probablement systématique.

  *Seconde mesure, avec Barrier* — énoncée avant de relancer : en forçant les 32 tâches à
  démarrer réellement ensemble au lieu de dépendre de l'ordonnanceur du pool de threads,
  j'attendais que la collision devienne indépendante du hasard d'ordonnancement et donc que
  le test échoue sur les **3 exécutions sur 3**, probablement avec un nombre de tirs acceptés
  (`Actual`) plus élevé qu'avant.

- **Erreur que ce contrôle pourrait détecter** : une implémentation de `Mutate` qui ne
  sérialise pas réellement les mutations concurrentes sur une même partie — verrou absent,
  verrou mal indexé (un seul verrou partagé par erreur pour toutes les parties, ou une clé de
  verrou qui ne correspond pas à l'identifiant de la partie), ou lecture de l'état hors du
  verrou (le défaut corrigé au commit `37f9734`).

- **Résultat réellement observé** :

  *Sans Barrier* (`Task.Run` seul), 3 exécutions sans verrou :
  - Exécution 1 : ÉCHEC — `execution: 3`, `Expected: 1, Actual: 2`
  - Exécution 2 : ÉCHEC — `execution: 3`, `Expected: 1, Actual: 2`
  - Exécution 3 : **succès complet, 5/5**, sans aucune synchronisation en place

  → Le test ne détectait la course que **2 fois sur 3**. Ce résultat a **infirmé**
  l'attendu initial (échec systématique) : `Task.Run` seul ne garantit pas que les tâches se
  chevauchent réellement — l'ordonnanceur peut démarrer et terminer les premières avant même
  d'avoir lancé les dernières, ce qui a produit un vert qui ne prouvait rien un tiers du temps.

  *Avec Barrier(32)* (départ synchronisé, code du commit `44bd57c`), 3 exécutions sans
  verrou :
  - Exécution 1 : **succès complet, 5/5**, toujours sans aucune synchronisation en place
  - Exécution 2 : ÉCHEC — `execution: 4`, `Expected: 1, Actual: 3`
  - Exécution 3 : ÉCHEC — `execution: 4`, `Expected: 1, Actual: 4` ; **et** `execution: 1`,
    `Expected: 1, Actual: 4`

  → Toujours **2 échecs sur 3**, pas 3 sur 3. La Barrier a rendu les échecs observés plus
  sévères (jusqu'à 4 tirs acceptés au lieu de 2), signe d'une collision plus large entre
  threads, mais elle n'a **pas** rendu le test déterministe. Ce second résultat infirme
  également l'attendu de la seconde mesure : la Barrière synchronise le *départ* des tâches,
  pas leur exécution effective par le pool de threads, qui doit d'abord injecter 32 threads
  disponibles (les exécutions ont pris 15 à 18 s au lieu de 80 ms, signe de cette montée en
  charge), et rien ne garantit qu'elles entrent ensuite dans `Board.Fire` de façon
  parfaitement simultanée.

- **Décision et justification** : le test est **conservé avec la Barrier** (commit `44bd57c`)
  car il constitue une amélioration mesurable et sans régression (voir avant/après ci-dessous),
  mais la conclusion est **rapportée sans l'enjoliver** : ce test, tel qu'il existe aujourd'hui,
  a un pouvoir discriminant réel mais **imparfait** (échec observé sur 4 des 6 exécutions sans
  verrou au total, tous scénarios confondus). Un vert isolé de ce test ne suffit pas à
  conclure à l'absence de course ; c'est la revue de code de `Mutate` (verrou par `Guid`,
  lecture sous verrou) qui reste la garantie principale, le test n'étant qu'un filet de
  sécurité partiel. Ne pas invoquer ce test seul comme preuve d'absence de race condition en
  soutenance.

- **Preuves reproductibles et liens vers les commits** :
  - `883fe0b` — ajout du store et du test de concurrence initial (`Task.Run` seul).
  - `37f9734` — correction de la lecture hors verrou dans `Mutate`, documentation de `Find`
    et du dictionnaire de verrous.
  - `44bd57c` — remplacement du départ par une `Barrier(32)` dans le test, sans modification
    des assertions.
  - Commande rejouable telle quelle (avec le `lock` commenté manuellement pour l'expérience) :
    `dotnet test --filter "Un_seul_tir_simultane_sur_la_meme_case_reussit"`.

- **Après correction éventuelle : résultat avant / après** :
  - *Avant* (verrou en place, test original `Task.Run`) : 3 exécutions consécutives, 7/7 à
    chaque fois (voir `.superpowers/sdd/2026-09-15-bataille-navale/task-7-report.md`).
  - *Après renforcement par Barrier, verrou en place* : re-testé après le commit `44bd57c`,
    3/5 puis 5/5 sur les exécutions ciblées, et suite complète à 92/92 (`dotnet test`), verrou
    confirmé stable — aucune régression introduite par le changement du mécanisme de départ.
  - *Sans verrou* : le passage de `Task.Run` seul à `Barrier(32)` a fait passer la sévérité
    des échecs observés de `Actual: 2` à `Actual: 3`/`4`, mais pas le taux de détection
    (2 échecs sur 3 dans les deux cas mesurés).

- **Limites et points non vérifiés** : la Barrier ne garantit que le départ synchronisé des
  tâches, pas leur entrée simultanée dans `Board.Fire` une fois relâchées par le pool de
  threads — l'injection progressive de threads par le pool (15-18 s observées) reste un
  facteur non contrôlé. Une mesure plus poussée (fixer `ThreadPool.SetMinThreads` avant le
  test, ou remplacer `Task.Run` par des `Thread` dédiés) n'a pas été tentée : elle est laissée
  en piste pour une tâche ultérieure si une garantie plus forte est un jour nécessaire. Cette
  revue ne porte que sur la case `(0,0)` d'une grille 10×10 ; elle ne dit rien d'un
  comportement à plus grande échelle (plusieurs cases visées simultanément, par exemple).
