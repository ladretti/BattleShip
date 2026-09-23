# Échanges décisifs avec l'IA

Seulement les échanges qui ont changé une décision. Outil : Claude Code, modèle claude-opus-5 sauf
mention contraire. Les contrôles détaillés sont dans `REVUE-IA.md`.

## 2026-09-15 — Choisir les règles, les représentations et l'adversaire

- **Contexte** : dépôt vide de code métier, cinq choix laissés libres par le sujet à arrêter avant de
  coder.
- **Prompt** : « Je dois faire les choix suivants — règles et flotte, représentations, stockage et
  algorithme de l'adversaire, interface, extensions — aide-moi à brainstormer. »
- **Réponse résumée** : l'IA a posé neuf questions une par une au lieu de livrer une conception toute
  faite. Elle a proposé quatre familles d'adversaires avec des moyennes tirées de la littérature
  (≈ 96 / 65 / 60 / 42 coups), sur une grille sans règle de non-adjacence.
- **Décision** : adaptée. On a gardé le modèle (navires + tirs), le verrou par partie, trois niveaux
  d'adversaire et `Result<T>` pour les refus. On a refusé deux points : on a voulu un placement manuel
  (meilleure démonstration de FluentValidation) et « touche = on rejoue » au lieu du tour alterné
  (ADR 0006).
- **Vérification** : les chiffres de l'IA étaient une hypothèse. On les a mesurés plus tard avec
  `StrategyBenchmarkTests` (200 parties, graine 20260915) : 95,7 / 52,6 / 41,2 coups. L'ordre annoncé
  tient, et Density reste sous le seuil de 55 coups fixé avant la mesure.
- **Preuve** : spec `935a8b7`, ADR 0001 à 0007 `f379852`.

## 2026-09-15 — Le tir en gRPC-Web et une seule traduction des erreurs

- **Contexte** : la brique centrale du sujet. La traduction `GameError` → statut était dupliquée dans
  les endpoints HTTP.
- **Prompt** : « Reprends le brief de la tâche 14 en TDD strict, avec un contrôle discriminant sur le
  mapping `NotFound` et un test de course `Read`/`Mutate`. »
- **Réponse résumée** : `Fire` renvoie la suite des coups (le nôtre puis ceux de l'adversaire tant
  qu'il touche), et un seul `ErrorMapping` sert HTTP et gRPC.
- **Décision** : adaptée. Le `.proto` et les cinq tests ont été gardés, mais le test de course écrit
  au niveau HTTP/gRPC restait vert même sans verrou : on l'a remplacé par un test sur le store.
- **Vérification** : en changeant `GameNotFound` en `Unknown`, le test échoue (`Expected: NotFound /
  Actual: Unknown`). Sans le verrou de `Read`, le nouveau test échoue 5 fois sur 5 ; avec, 5/5 vert.
- **Preuve** : commits `1a8f5dc` et `aef815e`. On n'a pas réussi à montrer la course sur
  `Game.History` seule.

## 2026-09-16 — Placement, jeu et démonstration dans le navigateur

- **Contexte** : pages de placement et de jeu, et la démonstration gRPC-Web demandée par le sujet
  (une réponse et une erreur visibles depuis le navigateur).
- **Prompt** : suite de l'exécution du même plan, tâches 17 à 19.
- **Réponse résumée** : l'aperçu de placement réutilise la règle du serveur (`PlacementRules.Validate`)
  mais ne bloque jamais le clic, sinon on ne pourrait plus montrer le refus du serveur. Les erreurs
  gRPC sont lues avec `Enum.TryParse<GameError>`, pas en comparant des chaînes.
- **Décision** : acceptée. En jouant, on a trouvé un défaut que la relecture n'avait pas vu : l'aperçu
  du navire suivant cachait celui qu'on venait de poser. Corrigé.
- **Vérification** : placement refusé en `400` (« adjacent ») puis accepté en `204`. Partie complète
  gagnée en 96 tirs. Les deux erreurs gRPC reviennent en `HTTP 200` avec `grpc-status` 3 puis 5 dans
  les trailers.
- **Preuve** : commits `0e5ec13` et `ed4cb56`, captures dans `docs/demo/`. Un `400` isolé vu une fois
  ne s'est jamais reproduit.

## 2026-09-17/18 — Le journal d'événements

- **Contexte** : socle et extensions livrés ; on voulait une amélioration d'architecture plutôt
  qu'un effet visuel.
- **Prompt** : « un simple jeu en 2D c'est trop bateau, je veux aller plus loin — qu'est-ce qui côté
  architecture pourrait améliorer le jeu ? », puis l'exécution du plan
  `docs/superpowers/plans/2026-09-17-journal-evenements.md`.
- **Réponse résumée** : l'IA a proposé un journal d'événements, 3D ou du temps réel. Son argument pour
  le journal venait du code : un tir était écrit trois fois (`Game._history`, `Board._receivedShots`,
  `Ship._hitCells`). Le journal supprime ce doublon au lieu d'ajouter une couche.
- **Décision** : journal et 3D retenus, le journal d'abord (ADR 0011) ; temps réel écarté ;
  persistance au backlog ; verrou par partie conservé. Pendant l'implémentation, l'IA a vu que stocker des `Ship` vivants dans les
  événements les ferait changer après coup : on stocke des `ShipSnapshot` immuables.
- **Vérification** : `dotnet test` de 145 à 169 sans régression. En HTTP, `/events` n'expose pendant
  la partie que les 17 cases du joueur ; `from=-1` donne `400`, un id inconnu `404`. Dans le rejeu,
  la coque d'un navire apparaît exactement au tir qui l'a coulé.
- **Preuve** : spec `81449d5`, commits `d488e8d` à `fa5422b`. La révélation de la flotte adverse en
  fin de partie n'est vérifiée que par un test d'intégration, pas à l'écran. Plusieurs tests écrits
  par l'IA ne pouvaient pas échouer et ont dû être corrigés à la relecture.
