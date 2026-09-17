# Revues de propositions IA

Sept revues, chacune étayée par une exécution dont le résultat attendu avait été énoncé **avant** de
lancer la commande — une proposition acceptée exige une preuve autant qu'une proposition rejetée.
**Bilan : sept revues, toutes closes : 2 acceptées, 3 adaptées, 1 rejetée, 1 corrigée** (la revue 2,
confirmée par la mesure, compte parmi les acceptées).

## Revue 1 — Aucune exception par tour d'adversaire (ADR 0004) — acceptée

**Proposition** : contre `CLAUDE.md` § 3 bis (« le coût des exceptions en boucle serrée quand
l'adversaire probabiliste évalue beaucoup de coups — à mesurer »), l'IA a soutenu que cette mesure
est sans objet, `DensityStrategy` n'appelant jamais le moteur (ADR 0004, commit `f379852`).

**Hypothèse à vérifier** : deux moitiés, vérifiables différemment. **(a)** la stratégie ne propose
jamais un coup que le moteur refuse — mesurable ; **(b)** elle n'a aucun moyen d'interroger le
moteur — structurel, vérifiable sur les types.

**Expérience** : `dotnet run --file refusals.cs` (scratchpad, réf. `BattleShip.API`) — **(a)** 200
parties par stratégie, graine 20260915, `GameRules.Default`, compteur des `Result` en échec sur
`Board.Fire` aux tours adverses ; **(b)** parcours par réflexion du graphe de `ShotHistory`, seul
argument de `NextShot`. Attendu : **(a)** zéro refus, **(b)** aucun chemin vers `Board` ni `Game`.
Erreur détectable : une stratégie « essayant » ses coups contre le moteur, ou visant une case déjà
tirée.

**Observation** : `Random 19139`, `HuntTarget 10520`, `Density 8244` tirs d'adversaire, `refusals:
none` pour les trois — **37 903 tirs, 0 refus** ; `types visited from ShotHistory: 9`, `no path from
ShotHistory reaches Board or Game`. Pouvoir discriminant établi, non supposé : la stratégie fautive
`AlwaysSameCell` remonte `FAIL BrokenAlways00: 400 opponent shots, refusals: CellAlreadyShot x200`.

**Décision** : **acceptée**, revue close. L'ADR 0004 peut affirmer qu'aucune exception n'est levée
par tour d'adversaire : ce n'est plus une analyse structurelle mais une mesure, et le
micro-benchmark envisagé porterait sur un scénario qui n'existe pas.

**Preuves et limites** : `scratchpad/refusals.cs`, `scratchpad/refusals-broken.cs`, commit
`f379852`. Limites : une seule graine, un seul jeu de règles ; **(b)** ne vaut que pour le graphe de
types de `ShotHistory`, pas contre un plateau reçu par constructeur.

## Revue 2 — Les chiffres de performance des stratégies — confirmée

**Proposition** : pour justifier les trois niveaux, l'IA a avancé des moyennes de littérature (≈ 96
aléatoire, ≈ 65-60 chasse/cible, ≈ 42 densité), reprises en **hypothèse** par l'ADR 0003.

**Hypothèse à vérifier** : ces valeurs viennent d'une littérature portant sur une grille 10×10
**sans** la règle de non-adjacence que l'ADR 0006 impose ici — ce qui donne plus d'information à la
densité. Pour que le périmètre à trois niveaux tienne, il suffit que l'**ordre** soit respecté.

**Expérience** : `dotnet test --filter "FullyQualifiedName~StrategyBenchmark" --logger
"console;verbosity=detailed"`, sur `StrategyBenchmark.Run(GameRules.Default, games: 200, seed:
20260915)`. Attendu **énoncé le 2026-09-15, avant toute implémentation** : ordre `Random >
HuntTarget > Density`, `Density` **sous 55 coups**. Erreur détectable : une densité qui ne pondère
pas les touches non coulées ferait à peine mieux que la chasse/cible ; une chasse/cible qui ne
ratisse pas les voisines s'effondrerait vers l'aléatoire.

**Observation** : sur 200 parties par stratégie, `Random` **95,69** coups (min 80, max 100),
`HuntTarget` **52,60** (29-67), `Density` **41,22** (26-58) ; `Passed! - Failed: 0, Passed: 2`.
L'ordre `95,69 > 52,60 > 41,22` est respecté et `41,22 < 55`.

**Décision** : **confirmée.** L'attendu est vérifié sans ajustement de seuil ni de stratégie : le
périmètre à trois niveaux tient. `HuntTarget` fait même mieux que l'analogie (52,6 contre ≈ 60-65),
probablement grâce au filtre de parité.

**Preuves et limites** : `BattleShip.API/Benchmark/StrategyBenchmark.cs` et
`StrategyBenchmarkTests.cs` (dont `The_measurement_is_reproducible_for_an_equal_seed`). Limites :
une seule flotte, une seule grille ; `ExtraTurnOnHit` non isolé — les trois stratégies en profitent
également, donc la comparaison relative tient, mais chaque moyenne absolue inclut cet avantage.

## Revue 3 — Pouvoir discriminant du test de concurrence du store — adaptée

**Proposition** : `InMemoryGameStoreTests.Only_one_concurrent_shot_on_the_same_cell_succeeds` (5
`[InlineData]`) vérifie qu'un seul de 32 tirs concurrents sur la même case réussit, contre
`InMemoryGameStore.Mutate` (commits `883fe0b` puis `37f9734`).

**Hypothèse à vérifier** : pour qu'un passage au vert signifie quelque chose, le test doit pouvoir
échouer **de façon fiable** sans le verrou, **sans dégrader** la durée de la suite.

**Expérience** : `lock (gameLock)` commenté dans `Mutate`, puis `dotnet test --filter
"Only_one_concurrent_shot_on_the_same_cell_succeeds"` lancé 3 fois pour chacune des trois versions
du départ des 32 tirs, durée de la suite mesurée **avant** de toucher au verrou. Attendu : un départ
simultané fait échouer 3 exécutions sur 3. Erreur détectable : un `Mutate` qui ne sérialise pas les
mutations d'une même partie — verrou absent, mal indexé, ou lecture hors du verrou.

**Observation**, trois tentatives dans l'ordre où elles ont eu lieu :
1. `Task.Run` seul (`883fe0b`) : **2/3** seulement (`Expected: 1, Actual: 2`, puis un succès complet
   5/5 sans aucune synchronisation) — l'ordonnanceur peut terminer les premières tâches avant
   d'avoir lancé les dernières. Attendu infirmé ; suite complète 0,86 s.
2. `Barrier(32)` sur `Task.Run` (`44bd57c`) : toujours **2/3**, échecs plus sévères (`Actual: 4`),
   et suite passée à **15-18 s** — le pool n'injecte qu'environ un thread toutes les 500 ms et la
   barrière attend que 32 threads existent. Abandonnée sur la mesure : facteur ≈ 20 pour rien.
3. `Barrier(32)` sur 32 `Thread` dédiés (`c604123`) : **3/3**, avec **4 des 5 `[InlineData]`** en
   échec à chaque exécution (88/97/92 ms), suite complète revenue à **842 ms**. Attendu confirmé.

**Décision** : **adaptée** — version 3 retenue, tranchée par la mesure et non par une préférence de
conception. Le test reste imparfait : 1 `[InlineData]` sur 5 en moyenne ne détecte pas la course,
donc **il ne vaut pas seul comme preuve d'absence de course** ; la garantie principale reste la
revue de `Mutate` (verrou indexé par `Guid`, lecture sous le verrou).

**Preuves et limites** : commits `883fe0b`, `37f9734`, `44bd57c`, `c604123` ; commande ci-dessus,
rejouable avec le `lock` commenté à la main. Limites : fenêtre de course probabiliste et non
garantie, sur la seule case `(0,0)` d'une grille 10×10 ; dépasser 32 tirs n'a pas été tenté.

## Revue 4 — Test de course `Read` vs `Mutate` — adaptée

**Proposition** : un test additionnel (tâche 14) devait démontrer qu'une lecture énumérant l'état
d'une partie pendant un tir concurrent échoue si `GameEndpoints` revenait de `store.Read` à
`store.Find` — d'abord au niveau HTTP/gRPC (`FireGrpcTests`), commit `aef815e`.

**Hypothèse à vérifier** : que le test **puisse échouer**, de façon reproductible, une fois la
lecture ramenée à `Find` — donc qu'il expose le risque que `IGameStore.cs` documente sur
`Game.History` et `Board.ReceivedShots`.

**Expérience** : lecture ramenée à `store.Find(id)`, puis `dotnet test --filter
"Concurrent_fires_and_reads_on_the_same_game_never_return_500_or_corrupt_state"` sur quatre
conceptions de plus en plus agressives (jusqu'à grille 300×300, 1920 tirs, salve de 300 tirs libérés
par une même `Barrier`), chacune instrumentée pour horodater les `_history.Add` et les
`Where(...).Select(...).ToList()` réels. Attendu : au moins une conception fait échouer le test.
Erreur détectable : un retour de `Read` à `Find` dans un endpoint qui énumère une partie.

**Observation, première tentative — négative** : les **quatre conceptions restent vertes** malgré
**224 chevauchements** mesurés à l'horloge sur une exécution, pour un coût de 4-6 s (suite de ≈ 5 s
à ≈ 10 s) ; un cinquième palier (2000 fils) n'a pas terminé en 180 s. Hors ASP.NET Core, deux
répliques isolées reproduisent le mécanisme (8 lecteurs sur 8 ; 4069 exceptions sur 11357
lectures) : c'est la pile réseau qui dilue la fenêtre.

**Observation, seconde tentative — positive** : test redescendu au niveau du store
(`InMemoryGameStoreTests.Concurrent_fires_and_reads_never_throw_and_return_consistent_snapshots`, 8
fils × 100 tirs contre 8 lecteurs). Verrou de `Read` retiré → **5 exécutions sur 5 en échec**, 5 à
14 occurrences de `ArgumentException: Destination array is not long enough to copy all the items in
the collection. Check array index and length.` (plus une `NullReferenceException` une fois). Verrou
rétabli → 5/5 au vert, 380-460 ms ; suite 133/133 en ≈ 4 s.

**Décision** : **adaptée** — le test HTTP/gRPC est **supprimé** (il ne discriminait rien et coûtait
4-6 s : une fausse assurance payée cher), remplacé par celui du store ; la projection combinée
`History` + `ReceivedShots` est conservée, `IGameStore.cs` désignant les deux comme à risque.

**Preuves et limites** : commit `aef815e` ; commandes ci-dessus, rejouables avec le verrou de `Read`
commenté à la main. **Réserve** : l'exception obtenue n'est pas celle annoncée — elle vient de
`HashSet<Coordinate>.ToList()` (`ReceivedShots`), pas de l'énumérateur de `List<T>` (`History`),
dont la moitié isolée reste verte 3/3 à 800 tirs et encore 3/3 à 4 800 tirs. Le mécanisme exact côté
`HashSet<T>` n'a pas été tracé dans son code source.

## Revue 5 — Durées de vie de `GameState` — adaptée

**Proposition** : le plan et l'ADR 0007 enregistrent `GameState` en `Singleton` alors que
`BattleApiClient` et le `HttpClient` sont `Scoped` (`Program.cs:22-31`, commit `d037bcf`).

**Hypothèse à vérifier** : c'est la définition d'une **dépendance captive**. L'ADR 0007 la juge sans
effet parce qu'en Blazor WebAssembly il n'y a qu'une portée — exact, mais la question est autre : le
code est-il correct *par construction*, ou parce que l'hébergeur rend le défaut inoffensif ?

**Expérience** : `dotnet run --file lifetimes.cs` (scratchpad, réf. `BattleShip.App`) construit le
conteneur avec `BuildServiceProvider(validateScopes: true)` — ce que Blazor WebAssembly n'active
jamais — sur les **vrais types du projet**, pour les deux enregistrements. Attendu : le plan lève
une `InvalidOperationException`, tout en `Scoped` se résout. Erreur détectable : une dépendance
captive (Singleton consommant un Scoped) qui ne se manifeste que grâce à l'hébergeur WebAssembly.

**Observation** : plan → `Cannot consume scoped service 'System.Net.Http.HttpClient' from singleton
'BattleShip.App.Services.GameState'.` ; livré (tout `Scoped`) → `resolved GameState`. Conforme à
l'attendu ; le message nomme `HttpClient` car la validation remonte au premier `Scoped` rencontré.
Suite **136/136 dans les deux cas** : **les tests ne voient pas ce défaut**, la validation si.

**Décision** : **adaptée** — les trois services passent en `Scoped`. Le comportement livré est
identique, mais ne dépend plus de cette particularité de l'hébergeur ; l'ADR 0007 n'est pas
renversé.

**Preuves et limites** : `scratchpad/lifetimes.cs`, commit `d037bcf`. Limite : l'expérience établit
que le conteneur *refuserait* ces durées de vie s'il les validait, pas qu'un bug observable existe
dans l'application livrée — il n'en existe pas. Le bénéfice est la portabilité, pas un correctif.

## Revue 6 — Recopier les DTO côté front — rejetée

**Proposition** : les DTO vivant dans `BattleShip.API`, que `BattleShip.App` ne référence pas, la
proposition spontanée était de **recopier** les records côté front. Rejetée au profit du partage par
`BattleShip.Models/Contracts/` (commit `fe16e5b`, ADR 0008).

**Hypothèse à vérifier** : pour que la copie soit acceptable, une divergence entre les deux
définitions doit se manifester **tôt et bruyamment** ; si elle passe la compilation et ne se voit
qu'à l'affichage, l'option achète un mode de panne silencieux.

**Expérience**, deux volets. (1) Relire le corps exact de `POST /games` avec les options
`JsonSerializerDefaults.Web`, celles qu'emploie `System.Net.Http.Json` (`curl` puis `dotnet run
--file roundtrip.cs game.json`). (2) `sed -i 's/string OpponentDifficulty, /string Level, /'
BattleShip.Models/Contracts/GameDto.cs` puis `dotnet build BattleShip.App`. Attendu : (1) `201` et
13 propriétés conformes ; (2) la compilation du front **échoue**. Erreur détectable : sous copie, ce
même renommage compilerait des deux côtés et produirait un niveau vide à l'écran, sans message — le
contrôle ne peut échouer que sous une seule des deux options.

**Observation** : (1) `HTTP/1.1 201 Created`, corps en camelCase, **13 contrôles sur 13 au vert**
(dont `Fleet[0] = Carrier/5` et les quatre listes imbriquées), aucun attribut de sérialisation
nécessaire ; (2) `error CS1061: 'GameDto' ne contient pas de définition pour 'OpponentDifficulty'`.
Conforme aux deux attendus ; le contrat a ensuite été restauré à l'identique (`git diff` vide).

**Décision** : **rejetée.** Les DTO et les entrées passent dans `BattleShip.Models/Contracts/`,
`DtoMappings` restant dans l'API. L'argument décisif n'est pas l'élégance mais le mode de panne :
client et serveur sont compilés ensemble, et le découplage n'achèterait aucune indépendance.

**Preuves et limites** : `scratchpad/roundtrip.cs` ; commits `fe16e5b` (déplacement) et `d037bcf`
(premier usage côté front) ; ADR 0008. Limites : rien n'empêche d'ajouter demain un attribut
`System.Text.Json` sur ces records et de faire entrer JSON dans le domaine — aucun test ne garde
cette frontière, seule la revue le fait ; la publication *trimmée* n'a pas été éprouvée.

## Revue 7 — Le vainqueur déduit par le front — rejetée puis corrigée

**Proposition** : `Play.razor`, `PlayerWon(game) => game.Opponent.SunkShips.Count ==
game.Fleet.Count` (commit `ed4cb56`) : la victoire est une règle calculée côté client. **Hypothèse à
vérifier** : la diapo 36 exige que le moteur identifie le gagnant, l'ADR 0007 que le front ne décide
aucune règle ; les deux sont violées si `Game` ne porte pas de vainqueur. **Expérience** : `grep -rn
Winner BattleShip.Models/` — attendu si conforme : au moins une propriété ; erreur détectable : une
règle de fin de partie qui n'existe que dans l'interface. **Observation** : 0 occurrence. Après
correction, `The_shooter_who_sinks_the_last_ship_is_the_winner` et
`A_game_played_to_the_end_finishes_with_the_player_as_winner` au vert ; le second, lancé avec
`"Opponent"` à la place de `"Human"`, échoue (`Expected: Opponent / Actual: Human`) — il discrimine.
**Décision** : corrigée — `Game.Winner : Player?`, `GameDto.Winner : string?`, front qui lit la
donnée. **Preuves et limites** : commits `5948009` (moteur et contrat) et `0d873f9` (partie
complète) ; limite : `FireResponse` (gRPC) ne porte pas le vainqueur, la page relit l'état par HTTP
après chaque échange.
