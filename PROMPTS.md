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
