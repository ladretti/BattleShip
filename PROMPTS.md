# Échanges décisifs avec l'IA

Une entrée par échange qui a compté ; outil : Claude Code, claude-opus-5 (contexte 1M) sauf mention contraire.
Contrôles détaillés et preuves : `REVUE-IA.md`.

## 2026-09-15 — Brainstorming des cinq familles de choix de conception

**Contexte** : dépôt échafaudé, aucun code métier ; les cinq choix laissés libres par le sujet devaient être
arrêtés avant d'écrire une ligne. **Prompt** : « Je dois faire les choix suivants — règles et flotte,
représentations, stockage et algorithme de l'adversaire, interface, extensions — aide-moi à brainstormer. »
**Réponse** : questionnement guidé en neuf questions plutôt qu'une conception d'un bloc ; quatre familles
d'adversaires chiffrées ≈ 96 / 65 / 60 / 42 coups (hypothèse : littérature sur grille 10×10 **sans**
non-adjacence, non mesurés ici).
**Décision** : adaptée — suivie sur le modèle (navires + tirs = vérité), le verrou par partie, les trois
niveaux, `Result<T>` (ADR 0004), `GameState` et le backlog ; **écartée** sur le placement manuel (meilleure
démonstration FluentValidation du projet) et sur « touche = on rejoue » (retenu contre le tour alterné) — les
deux écarts en ADR 0006.
**Vérification** : `find . -type f ; find .. -name global.json ; dotnet --version` → attendu d'après
`CLAUDE.md` § 1 bis : aucun échafaudage et un conflit de `.git` avec `csharp-school` ; observé : solution et
quatre projets déjà présents, `csharp-school/` répertoire **frère** donc aucun conflit, `global.json` hors
racine, SDK 10.0.401.
**Preuve / limite** : spec de conception, commit `935a8b7` ;
ADR 0001 à 0007, commit `f379852` ; limite : aucun code exécuté à ce stade, les chiffres des stratégies
restent une hypothèse non vérifiée.

## 2026-09-15 — Tâche 14 : tir gRPC-Web, `ErrorMapping` unique, test de course

**Contexte** : dernière brique de la contrainte centrale du sujet ; la traduction des erreurs était dupliquée
dans `GameEndpoints.ToProblem`. **Prompt** : « Reprends le brief de la tâche 14 en TDD strict, avec un
contrôle discriminant sur le mapping `NotFound` et un test de course `Read`/`Mutate`. »
**Réponse** : `Fire` renvoie une **séquence** (le coup du joueur, puis la chaîne des coups adverses tant
qu'ils touchent) et un `ErrorMapping` unique sert les deux façades ; hypothèse : l'adversaire ne reçoit qu'un
`ShotHistory` sans `OpponentBoard`.
**Décision** : adaptée — cinq tests et `.proto` repris tels quels ; le test de course HTTP/gRPC est resté
**vert** malgré 224 chevauchements instrumentés entre `_history.Add` et l'énumération, pour 4-6 s de suite :
redescendu au store sur correction du coordinateur (`Mutate`/`Read`, `Thread` + `Barrier`, sans réseau).
**Vérification** : `dotnet test --filter FireGrpc` → attendu `error CS0246` avant implémentation, observé ;
mutant `GameNotFound => Unknown` → attendu un échec qui distingue, observé `Expected: NotFound / Actual:
Unknown` ; verrou de `Read` retiré → attendu `InvalidOperationException: Collection was modified`, observé
**5/5 en échec** mais par `ArgumentException: Destination array is not long enough`, sur `Board.ReceivedShots` ;
verrou rétabli 5/5 vert ; suite 133/133 en ~4 s.
**Preuve / limite** : commit `1a8f5dc` (tir gRPC-Web) ;
commit `aef815e` (test redescendu au store) ; limite : la course sur `Game.History` seule n'a pas été
démontrée hors réseau (verte jusqu'à 4800 tirs) — revue 4.

## 2026-09-16 — Tâche 16 : état Blazor et place du contrat partagé

**Contexte** : ouverture du front alors que `BattleShip.App` ne référence que `BattleShip.Models` et que les
DTO vivaient dans `BattleShip.API`. **Prompt** : « /superpowers:executing-plans
@docs/superpowers/plans/2026-09-15-bataille-navale.md — reprends le plan à partir de la tâche 16. »
**Réponse** : avant d'écrire une ligne, l'IA a relevé que le plan ne dit pas d'où le front tire `GameDto` et a
**posé la question au binôme** au lieu de trancher : recopier les records (dérive silencieuse) ou les déplacer
dans `Models/Contracts` (définition unique).
**Décision** : déplacement accepté (ADR 0008), `DtoMappings` restant dans l'API ; deux propositions du plan
**rejetées** : les durées de vie DI (`GameState` `Singleton` dépendant d'un client `Scoped` — dépendance
captive, revue 5) et `Adopt(GameDto)`, retiré avant commit faute d'usage (YAGNI).
**Vérification** : renommage d'une propriété puis `dotnet build BattleShip.App` (la copie compilerait — revue
6) ; `BuildServiceProvider(validateScopes: true)` — attendu : le plan lève, le livré résout ; observé : conforme
(revue 5) ; Chrome piloté par DevTools → attendu préflight puis `POST /games` en `201`, observé `OPTIONS 204`,
`POST 201`, console vide ; API arrêtée → message d'échec et page toujours utilisable, comme attendu.
**Preuve / limite** : commit `fe16e5b` (contrat déplacé) ;
commit `d037bcf` (tâche 16) ; limite : Chrome lancé avec `--ignore-certificate-errors`, la confiance faite au
certificat de développement n'est donc pas établie.

## 2026-09-16 — Tâches 17 à 19 : placement, jeu, démonstration navigateur

**Contexte** : pages de placement et de jeu, client gRPC-Web, puis la démonstration exigée par le sujet — une
réponse **et** une erreur observables depuis le navigateur. **Prompt** : le même que l'entrée précédente,
l'exécution du plan se poursuivant tâche par tâche.
**Réponse** : la prévisualisation réutilise `PlacementRules.Validate` — la règle que le serveur exécute — sur
la flotte partielle, et le clic **ne bloque jamais** le placement : bloquer rendrait le refus serveur
indémontrable et ferait passer une règle côté client. Les erreurs gRPC sont relues par
`Enum.TryParse<GameError>` sur `Status.Detail`, jamais par chaînes.
**Décision** : acceptées, plus un défaut trouvé **par le pilotage et non par la relecture** : la
prévisualisation du navire suivant masquait celui qu'on venait de poser derrière un calque « invalide » ; elle
est désormais effacée après le clic.
**Vérification** : Chrome piloté sur l'application lancée → *placement* attendu `400` portant « adjacent »
puis `204` après correction, observé conforme ; *jeu* : partie menée à son terme comme attendu — **victoire en
96 tirs, 116 appels `Fire`** —, les deux erreurs revenant en **`HTTP 200`** avec `grpc-status` `3` puis `5`
dans les trailers ; refus métier attendus à zéro, observé **37 903 tirs, 0 refus**, le compteur remontant 200
refus sur une stratégie fautive (revue 1).
**Preuve / limite** : commit `0e5ec13` (placement) ;
commit `ed4cb56` (jeu et client gRPC-Web) ; limite : Chrome lancé sans interface et avec
`--ignore-certificate-errors` ; un `400` isolé observé une fois ne s'est pas reproduit.

## 2026-09-16 — Tâches 20 et 21 : historique, rejeu, accessibilité

**Contexte** : phases closes et vertes, extensions du backlog légitimes ; l'endpoint d'historique existait
depuis la tâche 13 sans tests ni interface. **Prompt** : toujours le même, l'exécution du plan s'est
poursuivie jusqu'à la tâche 21.
**Réponse** : deux hypothèses fausses dans les tests d'historique, démenties par l'exécution : la case `(5,5)`
crue vide (vrai du joueur, faux de la flotte adverse placée aléatoirement : le test passait par chance), et
l'unicité des navires coulés vérifiée globalement alors que **les deux flottes portent les mêmes noms**.
**Décision** : accepté après corrections — le résultat d'un tir est découvert en tirant jusqu'au raté,
l'unicité est vérifiée par camp ; le focus perdu après chaque tir (`activeElement` retombé sur `<body>`) a
demandé **deux** corrections, la première consommant la demande de focus au premier rendu.
**Vérification** : les quatre tests passant du premier coup sur du code préexistant, leur pouvoir discriminant
a été établi en renvoyant l'historique **inversé** : attendu les deux tests d'ordre en échec et le `404` vert,
observé conforme, puis `git diff` vide. Contrastes **calculés** (WCAG 2.1) : 3,13:1 → 6,59:1 et 1,53:1 →
3,02:1. Rejeu attendu sans requête, observé **zéro requête** ; au clavier (vrais `Input.dispatchKeyEvent`),
focus conservé sur la case tirée.
**Preuve / limite** : commit `224bb39` (historique et rejeu) ;
commit `9f5d151` (accessibilité) ; limite : **aucun lecteur d'écran réel** n'a été essayé, les annonces ne
sont vérifiées qu'au niveau du DOM.

## 2026-09-16 — Refonte de l'interface en trois apparences

**Contexte** : interface correcte et accessible mais sans parti pris. **Prompt** : « l'UI est un peu trop
simpliste, je ne suis pas convaincu … fais les trois styles, commutables à tout moment via les paramètres. »
(compétence `frontend-design`)
**Réponse** : l'IA a refusé de coder d'abord et posé un diagnostic — symétrie sans hiérarchie (rien ne dit où
regarder), aucun vocabulaire du sujet (pas de coordonnées, donc impossible de nommer une case), aucun temps
fort.
**Décision** : acceptée sous la forme d'**un seul jeu de jetons CSS** redéfini par `[data-theme]` (ADR 0009) :
une apparence change le substrat, jamais le balisage ni le glyphe d'une case. Bootstrap retiré, principale
cause du rendu « gabarit ».
**Vérification** : `node contrast.mjs` sur les trois palettes → attendu toutes paires ≥ 4,5:1, observé **cinq
échecs** (3,82 / 4,21 / 4,49 / 4,35 / 4,35), corrigés puis remesurés ; glyphes identiques dans les trois
apparences ; et un défaut qu'aucun test ne voit, trouvé au navigateur : une douzième ligne fantôme
(`gridTemplateRows` = 12 au lieu de 11), corrigée en rendant la gerbe dans la case touchée.
**Preuve / limite** : commit `499d046` ; `docs/demo/` régénéré, les captures précédentes étant devenues
fausses ; limite : la refonte n'est couverte par **aucun test automatisé**.

## 2026-09-16 — Des coques dessinées plutôt que des carrés

**Contexte** : après la refonte, un navire restait n carrés colorés. **Prompt** : « ça serait intéressant de
mettre des skins de bateaux … que proposerais-tu comme solution technique ? »
**Réponse** : l'obstacle n'est pas le dessin mais que **le navire est un objet unique alors que tout le reste
du système est par case** ; fusionner des cases détruirait le clavier vérifié à la tâche 21. D'où une couche
SVG superposée, au gabarit identique.
**Décision** : acceptée (ADR 0010), avec une conséquence non évidente : les **dégâts vivent hors du SVG** de
la coque, celle-ci couvrant les gouttières dont la largeur est une décision de thème — une marque placée dans
son `viewBox` dériverait d'un dixième de case selon l'apparence.
**Vérification** : `dotnet run --file hull.cs` → attendu `viewBox` correct et culture invariante, observé
**53/53**, culture prouvée discriminante : sous `fr-FR` un `double` brut rend `"1,5"`, soit une paire de
coordonnées en trop par tracé. Le navigateur a révélé le défaut principal : la prévisualisation validait la
**flotte entière**, donc deux navires posés qui se touchent faisaient refuser toute case, même en eau libre ;
corrigée par une évaluation **par paires**, adjacence, chevauchement et débordement restant refusés.
**Preuve / limite** : commit `c06774e` ; limite : aucun test automatisé du dépôt ne couvre cela — la géométrie
l'est par un script hors solution, le reste par le navigateur.

## 2026-09-16 — Page d'accueil : démonstration rejetée, illustration retenue

**Contexte** : la page d'accueil se réduisait à un titre, deux listes et un bouton. **Prompt** : « refais la
page d'accueil pour qu'elle donne envie de jouer … un véritable champ de bataille naval », puis « je voulais
plus une illustration imagée qu'une présentation du jeu ». (compétence `frontend-design`)
**Réponse** : première proposition — une partie auto-jouée par le composant `FiringGrid` réel, les trois
niveaux annoncés par leurs nombres mesurés 95,7 / 52,6 / 41,2 coups (revue 2). Seconde lecture : la demande
était une **image** — le plateau se regarde de dessus, une illustration de profil.
**Décision** : démonstration fonctionnelle **rejetée par le binôme** et retirée du dépôt, pas laissée « au cas
où » ; remplacée par la scène `SeaBattle` en SVG, thémée par les mêmes jetons.
**Vérification** : navigateur piloté en **arrêtant l'animation sur des instants précis**
(`getAnimations().currentTime`) → attendu une scène repeinte depuis les jetons dans les trois apparences,
observé `rgb(147,171,181)` / `rgb(62,71,79)` / `rgb(35,100,180)`, aucune couleur en dur ; attendu en mode
calme une **image figée** et non une mer vide, observé `keyart__scene--still`, vagues à `animation-name: none` ;
attendu zéro élément focalisable, observé `0`.
**Preuve / limite** : commit `31b1407` (démonstration, rejetée) ;
commit `31073e1` (illustration retenue) ; limite : la scène n'est couverte par aucun test ; leçon : ne pas
juger un thème sur un JPEG compressé, une couleur se vérifie par `getComputedStyle`.

## 2026-09-17 — Code sans commentaires, audit de conformité, livrables condensés

**Outil** : Claude Code (claude-fable-5-1). **Contexte** : 142/142 tests, ~1 500 lignes de commentaires,
livrables de 639 et 746 lignes. **Prompt** : « retire tous les commentaires du code (je n'aime pas les
commentaires typiques de l'IA) ; vérifie que tout ce qui est demandé dans le support et toutes ses bonnes
pratiques sont respectés ; mets à jour les fichiers IA et raccourcis-les — trop longs, on ne les lira pas ».
**Réponse** : strip par Roslyn (trivia de commentaire seuls) + regex pour Razor/CSS/proto ; audit diapo par
diapo dans `docs/conformite.md` ; trois écarts : le `.http` du gabarit, **aucun vainqueur** dans le moteur ni le
contrat (diapo 36, déduit par le front), aucun test d'intégration ne menant une partie à `Finished`.
**Décision** : acceptée — commentaires retirés y compris la documentation XML ; `Game.Winner` et
`GameDto.Winner` ajoutés en TDD ; test de partie complète par gRPC-Web ajouté. **Vérification** : `dotnet build`
(0 warning), `dotnet test` (142 → 145), greps de résidus à 0, `docs/conformite.md` rejouable ligne à ligne.
**Preuve / limite** : commits `9c4550f` (commentaires), `5948009` (vainqueur), `0d873f9` (partie complète),
`d4ca64b` (conformité) ; limite : les greps de conventions détectent les formes, pas l'intention — la lisibilité
sans commentaire n'est établie que par relecture.

## 2026-09-17 — Brainstorming du journal d'événements

**Outil** : Claude Code (claude-opus-5). **Contexte** : socle et trois extensions livrés, plus de contrainte de
délai ; le binôme veut un axe qui sorte du « jeu en 2D » et impressionne sur le fond. **Prompt** : « un simple
jeu en 2D c'est trop bateau, je veux aller plus loin et en mettre plein les yeux — qu'est-ce qui côté
architecture pourrait améliorer le jeu ? » (compétence `superpowers:brainstorming`)
**Réponse** : trois axes chiffrés — **A** journal d'événements, **B** temps réel multijoueur, **C** rendu 3D.
Argument décisif pour A, trouvé en lisant le moteur et non supposé : `Game._history` **existe déjà** et
`DtoMappings.ToOwnBoardDto` en dérive déjà les cases reçues, pendant que `Board._receivedShots` et
`Ship._hitCells` stockent le même fait en mutable — le même fait est écrit **trois fois** dans `Board.Fire`.
Le journal **retire** cette duplication au lieu d'ajouter une couche : l'objection « event sourcing = cérémonie »
tombe.
**Décision** : **A et C retenus** par le binôme, **B écarté** (plomberie hors domaine) ; **une spec par
sous-système**, A d'abord car C consomme son flux et car A seul touche le cœur protégé par les tests.
Persistance disque **écartée du périmètre** par le binôme malgré un coût quasi nul — une spec, un sujet.
Concurrence optimiste **examinée puis écartée** : le verrou par partie de l'ADR 0002 est correct et testé,
`IGameStore` n'est étendu que d'un `ReadEvents`.
**Vérification** : **aucune à ce stade — rien n'a été exécuté, c'est une session de conception.** Les contrôles
sont définis et ordonnés dans la spec § 6 : cinq tests neufs dont la paire 3/4 qui encadre la censure du secret
(chacun seul est satisfait par une implémentation triviale et fausse), et le test 2 qui **doit échouer sur le
code actuel** avant d'être rendu vert.
**Preuve / limite** : spec `docs/superpowers/specs/2026-09-17-journal-evenements-design.md`, commit `81449d5` ;
ADR 0011 à écrire **avant** la première ligne de code. Limites : l'hypothèse centrale — le repli produit le
même état que la mutation — n'est **pas prouvée**, elle sera mise à l'épreuve par les tests existants ; la
sérialisation des records polymorphes en Blazor WASM est **inconnue** et se tranche par `dotnet run --file`
avant d'écrire le contrat ; une première rédaction de la spec affirmait à tort qu'« aucun contrat public ne
change » alors que `Board.ReceivedShots` et `Ship.HitCells` sont publiques — corrigé en conservant ces
propriétés, calculées sur l'état replié.

## 2026-09-18 — Implémentation du journal d'événements, tâches 0 à 8

**Contexte** : spec et ADR 0011 écrits la veille ; neuf tâches à exécuter en TDD, avec un plan auto-relu
signalant deux paires de tâches à risque (Board → Game/Fold sur la pureté de `Decide`, Game/Fold → censure du
front sur la flotte adverse censurée en cours de partie). **Prompt** : « /superpowers:executing-plans
@docs/superpowers/plans/2026-09-17-journal-evenements.md », dispatché tâche par tâche avec relecture
indépendante après chaque implémentation.
**Réponse** : le spike de la tâche 1 a tranché la question ouverte de la sérialisation avant d'écrire le
contrat — `[JsonPolymorphic]` + `[JsonDerivedType]` fonctionnent en .NET 10, sortie observée `count=2
first=ShotFiredDto second=GameEndedDto` (projet console jetable, pas `dotnet run --file` : contrainte
d'outillage assumée, sans conséquence sur la conclusion). Les événements portent des `ShipSnapshot` immuables,
jamais des `Ship` vivants (R7) : sans cela, `Fold` se serait contaminé lui-même en mutant les mêmes instances
à chaque repli.
**Décision** : le plan est suivi avec des écarts documentés en temps réel dans `progress.md` plutôt qu'après
coup — 18 décisions numérotées (R1 à R18), dont R7 (Critique, requalifié après vérification dans le code) et
R18 (régression front trouvée par le contrôleur, seul défaut du chantier situé dans le code livré et non dans
les tests ou la spec). Sur ces 18 décisions, **six portent sur des tests incapables de discriminer une
conception correcte d'une conception fautive**, contre un seul défaut de code de production — ratio à charge
de la conception des tests, pas de l'implémentation (détail : revue 9).
**Vérification** : `dotnet test` mesuré après chaque tâche, croissant sans régression — 145 (ligne de base) →
148 → 150 → 155 → 160 → 164 → 168 → **169** (front, aucun projet de tests dédié, attendu) ; `dotnet build`
0 avertissement à chaque étape. Vérification navigateur du 2026-09-18, serveurs relancés sur le build courant :
bout-en-bout HTTP de `/events` (17 cases exposées = exactement celles du joueur en cours de partie, `from=9999`
→ `200` + liste vide, `from=-1` → `400`, id inconnu → `404`) ; reprise après rechargement de page constatée à
l'écran (flotte redessinée, 5 navires à 0 touche) ; balayage du curseur de rejeu sur une partie en cours
(Submarine coulé au tir 7) montrant la coque apparaître **exactement** au coup qui l'a coulée et ne jamais
disparaître — la régression R18 est morte à l'écran, pas seulement en test.
**Preuve / limite** : commits `d488e8d`..`fa5422b` (ADR 0011 à la tâche 8) ; `progress.md` (journal complet des
18 rulings). Limite dite telle quelle : la révélation de la flotte adverse **après** `GameEnded` n'a **pas**
été jouée à l'écran — finir une partie manuellement demande une vingtaine de tirs — et repose sur le seul test
d'intégration `After_the_game_ends_the_full_journal_is_served`.
