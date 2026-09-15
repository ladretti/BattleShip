# Revues de propositions IA

Trois revues argumentées minimum. Aucune erreur n'est exigée ; chaque conclusion doit être
étayée.

> **État au 2026-09-15** : une revue est complète et étayée par une exécution. Les deux
> autres sont **ouvertes** : l'hypothèse et le résultat attendu y sont énoncés *avant*
> exécution, comme le demande la discipline de vérification, mais l'exécution n'a pas encore
> eu lieu. Elles doivent être closes avant la remise.

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
