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
- **Prompt** : « Implémente le tir en gRPC-Web en TDD strict, avec un contrôle qui prouve que
  `GameNotFound` donne bien `NotFound`, et un test de course entre lecture et tir. »
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
- **Prompt** : « Fais la page de placement de la flotte, puis la page de jeu avec le tir en
  gRPC-Web, et vérifie dans le navigateur qu'on voit une réponse et une erreur. »
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
