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

---

## 2026-09-16 — Tâches 17 à 19 : placement, jeu en gRPC-Web, et démonstration navigateur

- **Outil / modèle** : Claude Code (claude-opus-5, 1M context)

- **Contexte** : suite immédiate de la tâche 16. Trois pages à écrire (placement, jeu), un
  client gRPC-Web, puis la démonstration exigée par le sujet — une réponse **et** une erreur
  gRPC-Web observables depuis le navigateur.

- **Prompt réellement utilisé** : le même que l'entrée précédente ; l'exécution du plan s'est
  poursuivie tâche par tâche sans nouvelle consigne.

- **Réponse et hypothèses résumées** : trois propositions valaient d'être discutées.

  1. **La prévisualisation du placement.** Plutôt que de recoder bornes, chevauchement et
     adjacence dans le front, l'IA a réutilisé `PlacementRules.Validate` — la règle que le
     serveur exécute — en lui décrivant la flotte *partielle*, exactement comme `FleetPlacer`
     le fait déjà. Hypothèse : `PlacementRules` ne lit de `GameRules` que `GridSize`, `Fleet`
     et `ShipsMayTouch`, ce qui rend anodin le quatrième champ absent du DTO. **Accepté.**
  2. **Le clic ne doit pas être bloqué.** Corollaire non évident : si le front interdisait de
     poser un navire là où sa prévisualisation est rouge, le refus du serveur deviendrait
     indémontrable et une règle serait *de facto* passée côté client. Le clic pose donc le
     navire quoi qu'il arrive. **Accepté**, et c'est ce qui rend l'étape 3 du README possible.
  3. **La traduction des erreurs gRPC.** `Status.Detail` porte `GameError.ToString()` ;
     l'IA le relit dans l'enum partagée (`Enum.TryParse<GameError>`) plutôt que de comparer
     des chaînes. Un membre ajouté demain tombe dans la branche par défaut avec son propre
     nom, jamais dans un message faux. **Accepté.**

- **Décision et justification** : les trois acceptées. Une proposition de l'IA a en revanche
  été **corrigée par la vérification, pas par la relecture** : après un clic, la
  prévisualisation du navire *suivant* se superposait au navire qu'on venait de poser et le
  masquait derrière un calque « invalide ». Rien dans le code ne le signalait ; c'est le
  pilotage du navigateur qui l'a révélé, en butant sur un décompte de cases occupées qui ne
  correspondait pas à l'attendu. La prévisualisation est désormais effacée après un clic.

- **Scénario ou commande de vérification** : Chrome piloté par le protocole DevTools, sur
  l'application réellement lancée.
  - *Placement* : poser une flotte **collée**, valider, puis corriger et revalider.
  - *Jeu* : partie complète, plus les deux erreurs attendues.
  - *Revue 2* : 200 parties × 3 stratégies avec un compteur de refus métier
    (`dotnet run --file refusals.cs`), plus un parcours par réflexion du graphe de types de
    `ShotHistory`.

- **Résultat attendu, puis résultat observé** :
  - Placement — attendu : `400` portant « adjacent », maintien sur `/placement`, puis `204`
    et redirection. Observé : conforme, et 4 allers-retours entre pages sans le moindre
    avertissement en console (ADR 0007, désabonnement).
  - Jeu — attendu : partie menée à son terme, tirs en `POST /battleship.BattleService/Fire`,
    `InvalidArgument` sur case rejouée, `NotFound` sur partie inconnue. Observé : victoire en
    96 tirs, 116 appels `Fire`, et **les deux erreurs reviennent en `HTTP 200`** avec
    `grpc-status: 3` puis `5` dans les *trailers* — détail qui a été reporté tel quel dans le
    README, la formulation initiale laissant croire à un code HTTP d'erreur.
  - Revue 2 — attendu : zéro refus. Observé : 37 903 tirs, zéro refus ; et le compteur remonte
    bien 200 refus quand on lui soumet une stratégie délibérément fautive.

- **Erreur que ce contrôle pourrait détecter** : le pilotage navigateur est le seul des
  contrôles du projet qui exerce réellement CORS, le préflight, les *trailers* gRPC-Web et le
  cycle de vie des composants Blazor — aucun test xUnit de ce dépôt ne couvre ce chemin. Il a
  d'ailleurs détecté deux choses qu'aucune relecture n'avait vues : le défaut de
  prévisualisation, et le fait que reconstruire pendant que `dotnet run` tourne casse le front
  (empreintes de `_framework/` périmées, page blanche, `404` sur `dotnet.<hash>.js`) — cette
  dernière consignée en « Limites connues » du README.

- **Preuves reproductibles et limites** : commits `0e5ec13` (tâche 17) et `ed4cb56`
  (tâche 18) ; preuves de démonstration dans `docs/demo/` (trois captures + trace réseau).
  Limites : Chrome a été lancé **sans interface et avec `--ignore-certificate-errors`**, donc
  la confiance faite au certificat de développement n'est pas établie par ces contrôles ; les
  captures montrent la page, pas l'onglet Réseau lui-même — c'est `docs/demo/grpc-web-trace.md`
  qui en tient lieu, et il est produit par le même passage. Un `400` isolé avait été observé en
  console pendant une première partie longue ; il ne s'est **pas reproduit** lors d'une seconde
  partie complète instrumentée pour tracer toute réponse ≥ 400, et son origine n'est pas
  établie — la piste la plus probable, sans preuve, est la même péremption d'empreintes que
  ci-dessus.

---

## 2026-09-16 — Tâches 20 et 21 : historique, rejeu, accessibilité

- **Outil / modèle** : Claude Code (claude-opus-5, 1M context)

- **Contexte** : phases 0 à 4 closes et vertes, donc les extensions du backlog deviennent
  légitimes. L'endpoint d'historique existait déjà depuis la tâche 13 ; il manquait ses tests
  et son interface.

- **Prompt réellement utilisé** : toujours le même — l'exécution du plan s'est poursuivie
  jusqu'à la tâche 21.

- **Réponse et hypothèses résumées, et ce qui a été corrigé** : c'est la session où
  l'exécution a le plus souvent démenti l'IA, et c'est ce qui la rend intéressante.

  1. **Deux hypothèses fausses dans les tests d'historique.** L'IA a écrit
     `Assert.Equal("miss", history[0].Result)` en croyant que `(5,5)` était hors de toute
     flotte — vrai pour la flotte du *joueur*, dont elle contrôlait le placement, faux pour la
     flotte *adverse*, placée aléatoirement par le serveur. Le test passait par chance.
     Deuxième hypothèse fausse dans la foulée : l'unicité des navires coulés, vérifiée
     globalement alors que **les deux flottes portent les mêmes noms**. Les deux ont été
     corrigées : le résultat d'un tir est désormais *découvert* en tirant jusqu'à obtenir un
     raté, et l'unicité est vérifiée par camp.
  2. **Un test sur du code préexistant ne prouve rien tant qu'il n'a pas échoué.** L'endpoint
     datant de la tâche 13, les quatre tests sont passés du premier coup. Son pouvoir
     discriminant a donc été établi en faisant renvoyer l'historique **inversé** : les deux
     tests d'ordre tombent, le test `404` reste vert. Puis restauration, `git diff` vide.
  3. **Le focus perdu après chaque tir.** Trouvé en pilotant le navigateur au clavier :
     après un coup, `document.activeElement` retombait sur `<body>`, la grille passant de
     `<button>` à `<div>` pendant le tir. Aucune relecture ne l'aurait vu. Première correction
     insuffisante — elle consommait la demande de focus au premier rendu, celui où le plateau
     est encore inerte ; la seconde ne la consomme qu'une fois la remise au focus réussie.
  4. **Deux couleurs sous le seuil.** Les contrastes ont été **calculés** (formule WCAG 2.1)
     plutôt que jugés à l'œil : le vert de prévisualisation était à 3,13:1 et le séparateur de
     cases à 1,53:1. Corrigés à 6,59:1 et 3,02:1.

- **Décision et justification** : tout accepté après correction. Le rejeu est délibérément un
  **rendu seul** — aucune requête, aucune mutation serveur —, ce qui est vérifié plutôt
  qu'affirmé : zéro requête émise pendant un déplacement du curseur.

- **Scénario ou commande de vérification** : `dotnet test --filter HistoryEndpoint` avec et
  sans l'endpoint saboté ; `node contrast.mjs` pour les ratios ; deux scénarios navigateur, le
  premier au **clavier seul** avec de vrais événements `Input.dispatchKeyEvent` — pas des
  événements DOM synthétiques, qui contourneraient précisément la gestion du focus qu'on
  cherche à éprouver.

- **Résultat attendu, puis résultat observé** :
  - Rejeu — attendu : curseur à 1 ⇒ 1 marque sur la grille adverse, 0 sur la sienne, tir
    désactivé, **zéro requête**. Observé : exactement cela, dans les deux sens.
  - Clavier — attendu : 1 seule case sur 100 dans l'ordre de tabulation, 3 × → puis 2 × ↓
    amènent en colonne 4 ligne 3, `r` bascule la rotation, `Entrée` agit. Observé : conforme,
    et le focus reste sur la case tirée (`column 5, row 5, hit`) après correction.

- **Erreur que ce contrôle pourrait détecter** : le pilotage au clavier discrimine ce qu'aucun
  test unitaire de ce dépôt ne peut voir — l'ordre de tabulation, le déplacement du focus, sa
  survie à un re-rendu. Il a trouvé le seul défaut réel de la tâche 21.

- **Preuves reproductibles et limites** : commits `224bb39` et `9f5d151` ; scripts
  `replay.mjs`, `a11y.mjs`, `glyphs.mjs`, `contrast.mjs` dans le scratchpad de session.
  Limites : **aucun lecteur d'écran réel n'a été essayé** — les annonces sont vérifiées au
  niveau du DOM, pas à l'oreille ; les flèches ne suppriment pas le défilement de page ; et le
  glyphe `hit` n'a pas été observé simultanément aux quatre autres (le navire touché avait
  coulé entre-temps), il l'a été séparément.

---

## 2026-09-16 — Refonte de l'interface : trois apparences commutables

- **Outil / modèle** : Claude Code (claude-opus-5, 1M context), compétence `frontend-design`

- **Contexte** : l'interface livrée aux tâches 16 à 21 était correcte et accessible, mais sans
  parti pris. Le binôme : « l'UI est un peu trop simpliste, je ne suis pas convaincu ».

- **Prompt réellement utilisé** : « /frontend-design l'ui est un peu trop simpliste je ne suis
  pas convaincu tu vas me reprendre tout ça et en faire quelque chose de seamless avec une
  bonne compréhension des animations d'explosion et d'autres idées que tu pourras me donner »,
  puis, après présentation de trois directions : « fais les trois styles et fais en sorte qu'on
  puisse changer à tous moments via les paramètres ».

- **Réponse et hypothèses résumées** : l'IA a d'abord refusé de coder et a posé un diagnostic —
  le problème n'était pas un manque de couleurs mais trois défauts de structure : symétrie sans
  hiérarchie (deux grilles égales, rien ne dit où regarder), aucun vocabulaire du sujet (pas de
  règles de coordonnées, donc impossible de nommer une case), aucun temps fort (la touche ne
  changeait qu'une couleur de fond). Trois directions ont été proposées avec maquettes ASCII,
  chacune construite pour **éviter** le cliché du radar vert phosphore sur noir.

  Hypothèse structurante de la réponse au « fais les trois » : trois feuilles de style
  séparées tripleraient ce qu'il faut *vérifier*, en particulier l'accessibilité livrée à la
  tâche 21 — et aucun test de ce dépôt ne détecterait qu'une skin régresse.

- **Décision et justification** : un seul jeu de jetons CSS, redéfini par `[data-theme]`
  (ADR 0009). Une apparence change le substrat et la matière d'un coup, jamais le balisage,
  jamais le comportement clavier, **jamais le glyphe d'une case**. Bootstrap retiré : ses
  boutons et alertes sont la principale cause du rendu « gabarit ». Deux familles variables
  pour les trois skins, pilotées sur leurs propres axes, plutôt que six fontes.

- **Scénario ou commande de vérification** : mesure des contrastes des **trois** palettes
  (`node contrast.mjs`), puis pilotage du navigateur pour rejouer les contrôles
  d'accessibilité de la tâche 21 sur l'interface refondue, plus deux contrôles nouveaux :
  les glyphes relevés dans les trois skins, et le mode calme.

- **Résultat attendu, puis résultat observé** :
  - Contrastes — attendu : toutes les paires ≥ 4,5:1. Observé : **cinq échecs** à la première
    mesure (3,82 / 4,21 / 4,49 / 4,35 / 4,35), corrigés puis remesurés.
  - Glyphes — attendu : identiques dans les trois. Observé : identiques
    (`hit:✱ miss:· ship:■ sunk:✖ unknown:`), ce qui rend la promesse « lisible sans la
    couleur » vraie par construction et non par trois efforts.
  - Clavier — attendu : rien de perdu. Observé : 1 case sur 100 dans l'ordre de tabulation,
    flèches et `R` opérants, focus conservé sur la case tirée, région live à jour.
  - Secousse du plateau — attendu : sur une touche et un coulé, pas sur un manqué, et
    supprimée en mode calme. Observé : `flinch / none / flinch / none` exactement.

- **Erreur que ce contrôle pourrait détecter** : un défaut de mise en page **invisible pour
  tout test de ce dépôt** a été trouvé ainsi. La gerbe d'impact était placée explicitement
  dans la grille CSS ; le placement automatique contournait alors sa case et décalait toutes
  les suivantes d'un cran, faisant apparaître une douzième ligne fantôme. Mesuré
  (`gridTemplateRows` = 12 au lieu de 11), corrigé en rendant la gerbe **dans** la case
  touchée, revérifié à 11×11.

- **Preuves reproductibles et limites** : `docs/demo/` régénéré sur la nouvelle interface —
  les captures précédentes montraient l'ancienne et seraient devenues des preuves fausses.
  Scripts `contrast.mjs`, `demo.mjs` dans le scratchpad. Limites : aucun lecteur d'écran réel
  essayé, dans aucune des trois apparences ; les polices viennent de Google Fonts, donc sans
  réseau les trois skins se ressemblent davantage ; et la refonte n'est couverte par **aucun
  test automatisé** — seules l'exécution dans le navigateur et la mesure des contrastes
  l'étayent.

---

## 2026-09-16 — Des coques dessinées plutôt que des carrés

- **Outil / modèle** : Claude Code (claude-opus-5, 1M context)

- **Contexte** : après la refonte en trois apparences, un navire restait n carrés colorés.
  Le binôme : « ça serait intéressant de mettre des skins de bateaux, un truc beau et
  seamless, pas juste des triangles — que proposerais-tu comme solution technique ? »

- **Prompt réellement utilisé** : la question ci-dessus, puis « coques + dégât sur la coque +
  placement repensé » en réponse aux trois périmètres proposés.

- **Réponse et hypothèses résumées** : l'IA a d'abord nommé l'obstacle réel, qui n'est pas le
  dessin : **le navire est un objet unique alors que tout le reste du système est par case**.
  Fusionner cinq cases en un élément détruirait le clavier et l'accessibilité vérifiées à la
  tâche 21. La proposition sépare donc ce qui se dessine de ce qui s'utilise : une seconde
  grille superposée, au gabarit identique, portant un SVG paramétrique par navire. Sprites
  matriciels (30 fichiers), canvas et découpage par case ont été écartés avec leurs raisons.

- **Décision et justification** : acceptée (ADR 0010). Deux points non évidents s'en sont
  déduits et méritent d'être signalés :
  - **Les dégâts ne peuvent pas vivre dans le SVG de la coque.** La coque couvre n cases *et*
    les n−1 gouttières, dont la largeur est une décision de thème ; une marque placée dans son
    `viewBox` dériverait jusqu'à un dixième de case selon l'apparence. Chaque dégât est donc
    son propre élément, placé par la grille.
  - **En acier, la coque a dû devenir plus claire que la tôle.** La tôle est déjà sombre :
    même le noir pur ne dépasse pas 2,02:1 contre elle. Le gris haze est d'ailleurs la couleur
    réelle d'un navire de guerre.

- **Scénario ou commande de vérification** : 53 contrôles de géométrie **hors navigateur**
  (`dotnet run --file hull.cs`), mesure des nouvelles paires de contraste, puis pilotage du
  navigateur : alignement des deux grilles, coques dans les trois apparences et les deux
  orientations, dégâts, épaves, fantôme de placement, clavier.

- **Résultat attendu, puis résultat observé** :
  - Géométrie — attendu : `viewBox` correct, tous les points dans la boîte, culture invariante.
    Observé : 53/53. Le contrôle de culture est prouvé discriminant — sous `fr-FR`, un `double`
    brut rend `"1,5"`, ce qui ajouterait une paire de coordonnées à chaque tracé.
  - Alignement — attendu : les deux grilles à la même taille. Observé : 490×490 des deux côtés,
    11×11 pistes. Il a fallu pour cela **cesser de dimensionner la piste des règles en `auto`** :
    la couche n'a pas de texte, donc son `auto` s'effondrait et décalait chaque coque.

- **Erreur que ce contrôle pourrait détecter, et ce qu'il a réellement trouvé** : trois
  défauts qu'aucun test du dépôt ne pouvait voir.
  1. Un **halo gris** autour de chaque navire — la case posée sur une coque ne peint rien, et
     révélait donc le fond du plateau, qui est la couleur de filet. Corrigé par un lit sous la
     coque, qui efface au passage le filet à l'intérieur de l'empreinte du navire.
  2. Le **tampon « coulé » restait indéfiniment** en mode calme, faute d'animation pour
     l'effacer, couvrant le plateau. Supprimé dans ce mode.
  3. **Le plus important, trouvé par accident** : la prévisualisation validait la flotte
     *entière*. Deux navires déjà posés qui se touchent — ce que cette page autorise
     délibérément — et *toute* case répondait « le navire ne tient pas ici », y compris en eau
     libre. La prévisualisation accusait le navire qu'on pose d'un problème situé ailleurs.
     Diagnostiqué en interrogeant le domaine directement (`dotnet run --file preview2.cs`), qui
     a nommé la paire fautive. Corrigé : le candidat est désormais évalué **par paires** contre
     chaque navire déjà posé, ce qui reste la règle du domaine et non une réimplémentation, et
     une alerte dédiée signale une flotte déjà en conflit. Contrôle du pouvoir discriminant :
     adjacence, chevauchement et débordement sont toujours refusés, l'eau libre acceptée.

- **Preuves reproductibles et limites** : ADR 0010 ; scripts `hull.cs`, `preview.cs`,
  `preview2.cs`, `contrast.mjs` dans le scratchpad de session. Limites : rien n'est couvert par
  un test automatisé du dépôt — la géométrie l'est par un script hors solution, le reste par
  l'exécution dans le navigateur ; le rendu sur écran à très haute densité et le comportement
  des coques sur une grille de 20 n'ont pas été regardés.

---

## 2026-09-16 — Une page d'accueil qui joue au lieu de décrire

- **Outil / modèle** : Claude Code (claude-opus-5, 1M context), compétence `frontend-design`

- **Contexte** : la page d'accueil se réduisait à un titre, deux listes déroulantes et un
  bouton. Le binôme : « refais la page d'accueil pour qu'elle donne envie de jouer, avec des
  navires qui bougent et qui explosent, un véritable champ de bataille naval ».

- **Prompt réellement utilisé** : la demande ci-dessus, telle quelle.

- **Réponse et hypothèses résumées** : la tentation était une animation décorative — vagues,
  navires qui dérivent, particules. L'IA a proposé l'inverse : **le champ de bataille, c'est le
  jeu lui-même**. Une partie se déroule toute seule sur la page, rendue par le composant
  `FiringGrid` réel, avec les vraies coques, les vrais impacts et l'apparence que le joueur a
  choisie. Aucun second langage visuel à maintenir, et ce que le visiteur voit est exactement
  ce qu'il va obtenir.

  Un choix de fidélité mérite d'être signalé : la démonstration montre **votre** plateau sous
  le feu, pas celui de l'adversaire. Sur le plateau adverse un navire à flot n'a rien à
  dessiner — règle du secret — et en montrer un aurait fait la publicité de quelque chose que
  le jeu ne fait jamais.

  Deuxième parti pris : les trois niveaux sont présentés par leurs **nombres mesurés**
  (95,7 / 52,6 / 41,2 coups), issus du duel d'IA de la revue 3. « Difficile est plus dur » ne
  dit rien ; « Difficile a besoin de 41 coups au lieu de 96 » dit quelque chose, et ce projet
  l'a mesuré.

- **Décision et justification** : acceptée. La séquence de tirs est **écrite**, pas tirée au
  hasard : une page d'accueil n'a qu'une occasion d'être regardée, et une séquence aléatoire
  passe l'essentiel de ce temps à rater. Deux ratés, un navire démonté, et on recommence.

- **Scénario ou commande de vérification** : pilotage du navigateur — présence et rythme de la
  bataille, absence de point de tabulation volé au formulaire, comportement en mode calme,
  libération de la minuterie quand on quitte la page.

- **Résultat attendu, puis résultat observé** :
  - Attendu : la démonstration est `aria-hidden` et n'expose **aucun** élément focalisable.
    Observé : `focusableInDemo = 0`.
  - Attendu : en mode calme le plateau se fige sans devenir vide, et repart en mode complet.
    Observé : figé (deux relevés identiques à 2,6 s d'intervalle), toujours porteur d'une
    bataille lisible, et reparti après bascule.
  - Attendu : trois allers-retours entre l'accueil et le placement sans message de console.
    Observé : console vide — la minuterie est bien annulée par `IAsyncDisposable`.

- **Erreur que ce contrôle pourrait détecter** : une boucle temporisée non libérée continue de
  battre sur un composant détruit et Blazor s'en plaint bruyamment ; c'est exactement le type
  de fuite qu'aucun test de ce dépôt ne verrait. Le contrôle du mode calme discrimine par
  ailleurs « l'animation ne scintille plus » de « la bataille s'est réellement arrêtée » —
  seule la seconde est acceptable pour quelqu'un qui a demandé moins de mouvement.

- **Preuves reproductibles et limites** : commit ci-après. Limites : le tampon de coulage
  s'ouvrait à `scale(2.1)` et débordait largement d'un plateau de huit cases — ramené à 1,45
  et borné à 92 % de la largeur. Et je me suis fait tromper **deux fois** par des captures
  JPEG réduites, en croyant lire un mauvais thème là où les valeurs calculées étaient bonnes :
  les couleurs se vérifient par `getComputedStyle`, pas à l'œil sur une image compressée.

---

## 2026-09-16 — Une illustration, pas une démonstration (correction de l'entrée précédente)

- **Outil / modèle** : Claude Code (claude-opus-5, 1M context), compétence `frontend-design`

- **Contexte** : l'entrée précédente décrit une page d'accueil où une partie se jouait toute
  seule. Le binôme l'a **rejetée** : « ce n'est pas vraiment ce que j'ai demandé, je voulais
  plus une illustration imagée qu'une présentation du jeu ».

- **Prompt réellement utilisé** : la remarque ci-dessus.

- **Réponse et hypothèses résumées** : l'IA avait interprété « champ de bataille naval »
  comme *montrer le jeu*, et livré une démonstration fonctionnelle. La demande était une
  **image**. Le glissement à opérer n'est pas cosmétique, il est de point de vue : le plateau
  se regarde **de dessus**, une illustration se regarde **de profil** — horizon, mer, coques
  silhouettées, obus qui tracent, gerbes et fumée.

  Hypothèse conservée de la proposition précédente, et c'est la seule : dessiner plutôt que
  photographier. Une image fixe demanderait un fichier par apparence et ne participerait pas
  au thème ; en SVG, chaque couleur est un jeton et la même scène devient une gravure sur
  carte, un Atlantique bas de plafond ou une mer de plastique.

- **Décision et justification** : démonstration fonctionnelle **retirée** du dépôt — elle
  n'est pas laissée en place « au cas où », elle ne répondait pas à la demande. Remplacée par
  la scène `SeaBattle`, posée en pleine image derrière le texte, celui-ci reposant sur la
  partie opaque d'un voile pour que chaque mot reste lu contre `--bg` et jamais contre la
  peinture.

- **Scénario ou commande de vérification** : pilotage du navigateur, en **arrêtant
  l'animation sur des instants précis** (`getAnimations().currentTime`) plutôt qu'en espérant
  tomber sur le bon frame — une scène ne se juge pas sur une capture prise au hasard.

- **Résultat attendu, puis résultat observé** :
  - Attendu : la scène se repeint entièrement depuis les jetons dans les trois apparences.
    Observé : mer et coques différentes à chaque skin (`rgb(147,171,181)` / `rgb(62,71,79)` /
    `rgb(35,100,180)`), aucune couleur en dur.
  - Attendu : en mode calme, une **image figée**, pas une mer vide. Observé : classe
    `keyart__scene--still`, vagues à `animation-name: none`, boule de feu tenue à 0,9
    d'opacité.
  - Attendu : zéro élément focalisable dans l'illustration. Observé : 0.

- **Erreur que ce contrôle pourrait détecter** : trois défauts de composition qu'aucune
  mesure n'aurait donnés, et qui ne se voient qu'en regardant.
  1. **Notre navire était presque entièrement caché** derrière le voile de texte — on n'en
     voyait que la proue. Recomposé : la poupe passe sous les mots, la tourelle et les canons
     sortent au clair.
  2. **Les gerbes d'eau étaient dessinées sous les bandes de mer**, donc invisibles. Le groupe
     d'impact est passé au-dessus.
  3. **L'adversaire ne ripostait jamais** : c'était un tir au but, pas une bataille. Ajout de
     sa bouche à feu, de son obus et d'une colonne d'eau courte devant notre proue.

- **Preuves reproductibles et limites** : commit ci-après. Limites : la scène n'est couverte
  par aucun test — seule l'observation l'étaye ; les filtres de flou SVG ont un coût de rendu
  qui n'a pas été mesuré sur une machine modeste ; et le cycle dure 7,5 s, durée choisie à
  l'œil et non éprouvée auprès de quiconque.
