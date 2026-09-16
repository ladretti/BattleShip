# Échanges décisifs avec l'IA

Une entrée par échange qui a compté. Les échanges de pure exécution ne sont pas consignés.

---

## 2026-09-15 — Brainstorming des cinq familles de choix de conception

- **Outil / modèle** : Claude Code (claude-opus-5, contexte 1M), skill `superpowers:brainstorming`.

- **Contexte** : le dépôt est échafaudé (solution `BattleShip.slnx` et les quatre projets)
  mais ne contient aucun code métier. Les cinq choix laissés libres par le sujet — règles et
  flotte, représentations et organisation du code, stockage et algorithme de l'adversaire,
  interface et expérience de jeu, extensions et backlog — devaient être arrêtés avant
  d'écrire la moindre ligne. `CLAUDE.md` fixait déjà trois patterns (Repository, Strategy,
  injection de l'aléa) et laissait l'ADR 0004 en suspens.

- **Prompt réellement utilisé** :
  > « Avant de commencer le développement de la bataille navale je dois faire les choix
  > suivants : règles, grille et composition de la flotte ; représentations et organisation
  > du code ; stockage et algorithme de l'adversaire ; interface et expérience de jeu ;
  > extensions et priorités du backlog. Aide-moi à brainstormer pour le choix de
  > l'algorithme par exemple. »

- **Réponse et hypothèses résumées** : l'IA a conduit un questionnement guidé (neuf questions
  à choix, une famille de décisions à la fois) plutôt que de produire une conception d'un
  bloc. Elle a proposé quatre familles d'algorithmes d'adversaire chiffrées en nombre moyen
  de coups, et signalé trois hypothèses :
  1. les chiffres avancés (≈ 96 / ≈ 65 / ≈ 60 / ≈ 42 coups) proviennent de la littérature sur
     une grille 10×10 **sans** règle de non-adjacence — **non mesurés dans ce projet** ;
  2. `DensityStrategy` exige de connaître les navires restants, donc le contrat
     `ShotHistory` doit porter cette information, sous peine d'être soit impossible soit
     tricheuse ;
  3. `ConcurrentDictionary` ne protège pas les objets `Game` qu'il contient — le §3 bis de
     `CLAUDE.md` laissait ce trou ouvert.

- **Décision et justification** : **adaptée**. Les recommandations de l'IA ont été suivies sur
  le modèle (navires + tirs = vérité, grille calculée), la concurrence (verrou par partie),
  l'adversaire (trois niveaux), l'ADR 0004 (`Result<T>`), l'état Blazor (`GameState`) et le
  backlog. Elles ont été **écartées** sur deux points, délibérément :
  - le **placement manuel** a été retenu contre la recommandation de placement automatique —
    le coût front est accepté en échange de la meilleure démonstration FluentValidation du
    projet ;
  - « **touche = on rejoue** » a été retenu contre la recommandation du tour strictement
    alterné, en connaissance de la conséquence signalée par l'IA (niveau Difficile écrasant).

  Ces deux écarts sont assumés et consignés en conséquence dans l'ADR 0006.

- **Scénario ou commande de vérification** : les affirmations de l'IA sur l'état du dépôt ont
  été confrontées à l'exécution avant d'être reprises dans la conception.

  ```bash
  ls -a ; find . -type f -not -path './.git/*' ; find .. -maxdepth 2 -name global.json ; dotnet --version
  ```

- **Résultat attendu, puis résultat observé** : `CLAUDE.md` § 1 bis affirmait qu'aucun
  échafaudage n'existait et que le `.git` de `csharp-school` entrerait en conflit. **Observé** :
  la solution et les quatre projets existent déjà ; `csharp-school/` est un répertoire
  **frère** du dépôt, donc hors périmètre, et le conflit n'existe pas ; `global.json` n'est
  **pas** à la racine ; le SDK utilisé est 10.0.401. Voir `REVUE-IA.md`, revue 1.

- **Erreur que ce contrôle pourrait détecter** : une conception bâtie sur un état de dépôt
  périmé — par exemple un plan qui recommencerait l'échafaudage, ou qui ouvrirait un ADR pour
  un problème de sous-module déjà résolu.

- **Preuves reproductibles et limites** :
  - Spec : `docs/superpowers/specs/2026-09-15-bataille-navale-design.md` (commit `935a8b7`)
  - ADR 0001 à 0007 : `docs/adr/` (commit `f379852`)
  - **Limite** : aucun code n'a été écrit ni exécuté à ce stade. Les chiffres de performance
    des stratégies restent une **hypothèse non vérifiée** ; la mesure est une tâche du plan.
    Le montage `WebApplicationFactory` + `GrpcChannel` n'est pas davantage vérifié — c'est le
    risque technique principal du projet, traité en premier dans le plan.

---

## 2026-09-15 — Tâche 14 : le tir en gRPC-Web et la traduction des erreurs

- **Outil / modèle** : Claude Code (Claude Opus 5, contexte 1M).
- **Contexte** : dernière brique manquante de la contrainte centrale du sujet — un échange
  gRPC-Web fonctionnel, réponse et erreur attendue démontrables. Le contrat `.proto`, le
  squelette de `BattleGrpcService`, les cinq tests d'intégration et la table ADR 0004 étaient
  déjà spécifiés dans le brief de tâche ; restait à les recopier verbatim, implémenter, et
  fusionner la traduction d'erreur (jusqu'ici dupliquée dans `GameEndpoints.ToProblem`) dans un
  `ErrorMapping` unique servant les deux façades.
- **Prompt réellement utilisé** : le brief de tâche 14 (`task-14-brief.md`), relayé avec des
  contraintes supplémentaires : TDD strict (constater l'échec de compilation avant
  d'implémenter), un contrôle à pouvoir discriminant sur le mapping `NotFound`, et un test de
  course `IGameStore.Read` vs `Mutate` — la première mutation concurrente réelle du projet,
  que la tâche 13 avait explicitement laissée non vérifiée.
- **Réponse et hypothèses résumées** : l'implémentation suppose que `Fire` doit renvoyer une
  **séquence** (le coup du joueur, puis la chaîne des coups adverses tant qu'ils touchent), que
  l'adversaire ne doit recevoir qu'un `ShotHistory` construit à partir de `HumanBoard` et de ses
  propres tirs passés (jamais `OpponentBoard`), et que la casse des champs `Shot.result` /
  `status` / `current_player` doit réutiliser `DtoMappings.ToState` plutôt qu'une seconde
  conversion.
- **Décision et justification** : la proposition du brief a été suivie **telle quelle** pour
  les cinq tests et le contrat `.proto`. Le test de course, en revanche, a demandé plusieurs
  itérations : les deux premières versions (une salve modeste, puis une version « écriture
  soutenue » sur une grille 60×60) ont échoué à faire échouer le mutant `Find` malgré un
  historique pré-rempli et des lectures en boucle — **adaptée** en conséquence (grille 300×300
  pour éviter que le tir aléatoire de l'adversaire ne termine la partie avant la fin de la
  salve, conception en salve simultanée plutôt qu'en écriture séquentielle par fil). Voir
  `REVUE-IA.md`, revue 5 : même after cette adaptation, le test ne démontre pas de pouvoir
  discriminant contre ce mutant précis, ce qui est rapporté comme une limite plutôt que masqué.
- **Scénario ou commande de vérification** :

  ```bash
  dotnet test --filter FireGrpc                 # constat d'échec de compilation, puis 6/6 verts
  dotnet test                                   # 133/133
  # Contrôle discriminant 1 : GameError.GameNotFound => StatusCode.Unknown (temporaire)
  dotnet test --filter "A_shot_on_an_unknown_game_returns_NotFound"
  # Contrôle discriminant 2 : GameEndpoints revient à store.Find (temporaire)
  dotnet test --filter "Concurrent_fires_and_reads_on_the_same_game_never_return_500_or_corrupt_state"
  ```

- **Résultat attendu, puis résultat observé** :
  - Avant l'implémentation : `error CS0246: The type or namespace name 'BattleService' could
    not be found` — **observé**, conforme au brief.
  - Après l'implémentation : 133/133, dont les 6 tests de `FireGrpcTests.cs` (5 donnés + 1
    ajouté) — **observé**.
  - Contrôle discriminant 1, attendu : le test échoue avec un message qui distingue
    `NotFound` de la valeur erronée — **observé** : `Assert.Equal() Failure: Values differ /
    Expected: NotFound / Actual: Unknown`.
  - Contrôle discriminant 2, attendu (avant mesure) : le test échoue également, montrant que
    la lecture non verrouillée corrompt l'énumération de `Game.History` — **observé : le test
    reste vert** malgré une mesure instrumentée (retirée avant ce commit) confirmant un
    chevauchement réel, en temps horloge, entre l'ajout (`_history.Add`) et l'énumération
    (`history.Where(...).Select(...).ToList()`). Le mécanisme lui-même a été vérifié isolément
    (hors ASP.NET Core/gRPC, via `dotnet run --file`) : il s'y reproduit de façon fiable
    (8/8, puis 4069/11357 occurrences selon la densité d'écriture).
- **Erreur que ce contrôle pourrait détecter** : le contrôle discriminant 1 détecte un mapping
  d'erreur gRPC erroné pour n'importe lequel des sept membres de `GameError`. Le contrôle
  discriminant 2, tel que livré, ne détecte **pas de façon fiable** un retour de `IGameStore.Read`
  à `Find` dans `GameEndpoints` — c'est sa limite documentée, pas une affirmation de succès.
- **Preuves reproductibles et limites** : commandes ci-dessus, rejouables telles quelles.
  Limite principale : le test de course additionnel (au-delà des cinq du brief) ne constitue
  pas, à ce jour, une garantie automatisée contre une régression `Find` — cette garantie repose
  sur la revue de code de `IGameStore.Mutate`/`Read` (même verrou, même clé) plutôt que sur ce
  test. Voir `REVUE-IA.md`, revue 5, pour l'analyse complète.

---

## 2026-09-15 — Correction du coordinateur : redescendre le test de course au niveau du store

- **Outil / modèle** : Claude Code (Claude Opus 5, contexte 1M).
- **Contexte** : suite à l'échange précédent (tâche 14), le coordinateur a relevé que le test
  de course HTTP/gRPC coûtait 4-6 s pour un pouvoir discriminant non démontré, et faisait
  passer la suite complète de ~5 s à ~10 s.
- **Prompt réellement utilisé** : remplacer le test par un test direct sur `IGameStore`
  (`Mutate`/`Read`, sans HTTP ni gRPC), avec des `Thread` dédiés et une `Barrier` exacte,
  la moitié tirant, l'autre moitié lisant avec une projection qui énumère l'historique et les
  tirs reçus ; contrôler le pouvoir discriminant en retirant le verrou de `Read` ; si le test
  ne discrimine toujours pas, le supprimer plutôt que le fabriquer.
- **Réponse et hypothèses résumées** : la proposition supposait que retirer le réseau
  suffirait à retrouver le pouvoir discriminant déjà démontré en isolation (répliques
  `dotnet run --file` du rapport précédent). Réalisée : `game.CurrentPlayer` doit être lu
  **à l'intérieur** du verrou pour décider qui tire, sinon le tour se bloque après le premier
  coup (bug qui avait fait échouer une ébauche antérieure de cette même piste).
- **Décision et justification** : **acceptée, avec une réserve signalée plutôt que masquée** :
  le test discrimine bien, de façon fiable et rapide, mais via une `ArgumentException` issue
  de `HashSet<Coordinate>.ToList()` (`Board.ReceivedShots`), pas via l'`InvalidOperationException`
  « Collection was modified » d'un `List<T>` (`Game.History`) qu'on attendait. La moitié
  `History` seule de la projection, isolée pour vérifier, reste verte même sans verrou à un
  volume testé jusqu'à 4800 tirs. La projection combinée est conservée : les deux collections
  sont documentées comme à risque dans `IGameStore.cs`, et celle qui casse en premier suffit à
  prouver que `Read` protège l'ensemble.
- **Scénario ou commande de vérification** :

  ```bash
  dotnet test --filter "Concurrent_fires_and_reads_never_throw_and_return_consistent_snapshots"
  dotnet test    # suite complète
  ```

- **Résultat attendu, puis résultat observé** : attendu, énoncé par le coordinateur avant
  exécution : une `InvalidOperationException: Collection was modified`, reproductible sur
  plusieurs exécutions. **Observé** : 5 exécutions sur 5 en échec, verrou retiré, mais avec
  `ArgumentException: Destination array is not long enough to copy all the items in the
  collection.` (une fois accompagnée d'une `NullReferenceException`) — un symptôme différent
  du mécanisme annoncé, sur une collection différente. Verrou rétabli : 5/5 au vert
  (380-460 ms). Suite complète : 133/133 en ~4 s (objectif « autour de 5 s » tenu).
- **Erreur que ce contrôle pourrait détecter** : un retour de `IGameStore.Read` à une lecture
  non verrouillée, sur `Game.History` comme sur `Board.ReceivedShots`.
- **Preuves reproductibles et limites** : commandes ci-dessus. Limite assumée : la course sur
  `List<T>` (`Game.History`) seule n'a pas été démontrée en isolation du réseau à un volume
  raisonnable (verte 3/3 à 800 tirs, encore 3/3 à 4800) — seule la course combinée avec
  `HashSet<T>` (`Board.ReceivedShots`) l'a été. Détail complet dans `REVUE-IA.md`, revue 5, et
  `.superpowers/sdd/2026-09-15-bataille-navale/task-14-report.md` (addendum).

---

## 2026-09-16 — Tâche 16 : état partagé Blazor, page de création, et où vit le contrat

- **Outil / modèle** : Claude Code (claude-opus-5, 1M context)

- **Contexte** : les tâches 1 à 15 sont livrées (API, gRPC-Web, validation, CORS). La tâche
  16 ouvre le front : `GameState`, `BattleApiClient`, `NewGame.razor`. Contrainte de
  structure : `BattleShip.App` ne référence que `BattleShip.Models`, alors que les DTO du
  contrat vivent dans `BattleShip.API`.

- **Prompt réellement utilisé** : « /superpowers:executing-plans
  @docs/superpowers/plans/2026-09-15-bataille-navale.md — reprends le plan à partir de la
  tâche 16 ».

- **Réponse et hypothèses résumées** : l'IA a relevé avant d'écrire la moindre ligne que le
  plan ne dit pas d'où le front tire `GameDto`, et a posé la question au binôme au lieu de
  trancher seule. Deux options présentées : recopier les records côté front (zéro churn,
  dérive silencieuse possible) ou les déplacer dans `BattleShip.Models` (définition unique,
  refactor des fichiers déjà commités). Hypothèse sous-jacente à l'option retenue : des
  records nus n'introduisent aucune dépendance JSON dans le domaine, et `CLAUDE.md` décrit
  bien `BattleShip.Models` comme « Modèles partagés + moteur de jeu ».

- **Décision et justification** : **déplacement accepté** (ADR 0008). `DtoMappings` reste
  dans l'API : le domaine porte la forme du contrat, jamais la projection vers lui.
  `DifficultyLevels` a suivi le même chemin, sa documentation le désignant déjà comme « un
  contrat avec le front ».

  Deux propositions du plan ont par ailleurs été **adaptées** :
  - les durées de vie DI de l'étape 2 (`GameState` en `Singleton` dépendant d'un client
    `Scoped`) — dépendance captive, voir `REVUE-IA.md` revue 6 ;
  - `Adopt(GameDto)` avait été écrit dans `GameState` « pour les tâches 17-18 » puis
    **retiré** avant commit : aucune des deux n'en a l'usage (le placement répond `204`, le
    tir répond un `FireResponse`), et le plan proscrit la cérémonie sans contrepartie.

- **Scénario ou commande de vérification** : quatre expériences, toutes avec un attendu
  énoncé *avant* exécution.
  1. Relecture du corps réel de `POST /games` dans `GameDto` (`dotnet run --file roundtrip.cs`).
  2. Renommage d'une propriété du contrat, puis `dotnet build BattleShip.App`.
  3. `BuildServiceProvider(validateScopes: true)` sur les deux jeux de durées de vie.
  4. **Vrai navigateur** : Chrome piloté par le protocole DevTools (Node 24, sans
     dépendance), API et front lancés, onglet Réseau et console observés — d'abord API
     démarrée, puis API arrêtée.

- **Résultat attendu, puis résultat observé** :
  - Attendu (4) : rendu de `<h1>New game</h1>`, clic → préflight `OPTIONS` puis
    `POST /games` en `201`, URL devenue `/placement`, aucune erreur console. API arrêtée :
    message d'échec, page toujours utilisable.
  - Observé (4) : `OPTIONS` → `204`, `POST` → `201`, `location = /placement`, console vide ;
    le menu affiche « Opponent: Normal · Grid: 10 × 10 » **après** la navigation, ce qui
    constitue la preuve directe de la raison d'être de `GameState` (ADR 0007). API arrêtée :
    alerte « Could not reach the server… », `location` reste `/`, bouton et listes déroulantes
    toujours actifs. Les quatre expériences conformes à leur attendu ; détail en revues 6 et 7.

- **Erreur que ce contrôle pourrait détecter** : l'expérience 2 distingue les deux options
  de placement des DTO (la copie compilerait) ; l'expérience 3 distingue « ces durées de vie
  sont correctes » de « elles ne sont inoffensives que grâce à l'hébergeur » ; l'expérience 4
  distingue une page qui gère l'échec d'une page qui se fige — c'est le seul des quatre
  contrôles qui exerce réellement CORS, le préflight et le cycle de vie Blazor.

- **Preuves reproductibles et limites** : commits `fe16e5b` (contrat partagé) et `d037bcf`
  (tâche 16) ; scripts dans le scratchpad de session (`roundtrip.cs`, `lifetimes.cs`,
  `drive.mjs`). Limites : Chrome a été lancé avec `--ignore-certificate-errors`, donc la
  confiance faite au certificat de développement n'est **pas** établie par cette
  vérification ; la redirection mène pour l'instant à la page « Not Found », `/placement`
  n'existant qu'à la tâche 17 ; le comportement de la sérialisation sous publication trimmée
  n'a pas été éprouvé.
