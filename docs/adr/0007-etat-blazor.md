# ADR 0007 : Gestion de l'état côté Blazor

## Statut et date
Accepté — 2026-09-15 — amendé 2026-09-16 (commit `d037bcf`) : durée de vie `Scoped` — amendé 2026-09-18 :
condition de réexamen satisfaite, reprise de partie livrée (ADR 0011)

## Contexte

Le parcours traverse trois pages : nouvelle partie → placement manuel → jeu. L'identifiant de
partie, les règles choisies et l'état courant doivent survivre à la navigation entre ces pages.

## Options envisagées

- **A. Service `GameState` avec événement `OnChange`.** Un service enregistré dans `Program.cs`
  détient la partie courante et notifie les composants abonnés, qui deviennent de l'affichage quasi
  pur.
- **B. État dans le composant page, passé en paramètres.** Aucun service, le plus simple à lire
  pour une page unique ; mais avec une page de placement séparée de la page de jeu, il faudrait
  repasser par le serveur à chaque navigation.
- **C. Tout sur une page unique, sans navigation.** Élimine la question, mais concentre création,
  placement et partie dans un seul `.razor` — le signal « ce fichier en fait trop ».

## Décision

Option A. `GameState` est enregistré en singleton (en Blazor WebAssembly, `Scoped` et `Singleton`
se confondent : il n'y a qu'un seul utilisateur et qu'une seule session). Le service détient la
partie courante et expose un événement `OnChange` ; les composants s'y abonnent dans
`OnInitializedAsync` et s'en **désabonnent** dans `Dispose`.

## Conséquences

- Les composants `.razor` restent centrés sur le rendu et les événements.
- Oublier le désabonnement provoque une fuite et des rendus sur des composants détruits : tout
  composant qui s'abonne implémente `IDisposable`.
- Le service est le point unique où les trois états — **chargement**, **succès**, **échec** — sont
  représentés. Une coupure de communication ne doit pas rendre l'interface inutilisable.
- La prévisualisation de validité du placement est un **confort**, pas une garantie : la règle fait
  foi côté serveur, et le service ne doit jamais devenir le lieu où une règle est décidée.
- `GameState` ne stocke que ce que le serveur a renvoyé ; il ne reconstitue jamais la flotte adverse
  (règle du secret, ADR 0001).

## Vérification et réexamen

- Réexamen 2026-09-16 : durée de vie `Scoped` pour `GameState`, `BattleApiClient` et `HttpClient`
  (commit `d037bcf`).
- Vérification manuelle dans le navigateur : créer une partie, placer la flotte, naviguer vers le
  jeu, revenir — l'état doit être conservé, et la console F12 ne doit signaler aucun rendu sur
  composant détruit après plusieurs allers-retours.
- Couper l'API en cours de partie : l'interface doit afficher un état d'échec exploitable plutôt
  qu'une page figée.
- À réexaminer si une reprise de partie après rechargement du navigateur entrait au périmètre : il
  faudrait alors persister l'identifiant de partie côté client.

### Amendement 2026-09-18 : condition de réexamen satisfaite

La reprise de partie après rechargement est livrée (ADR 0011, tâche 7, commit `69e2e2d`) :
l'identifiant de la partie en cours est écrit dans `localStorage` par `GameSessionStorage` et relu
au démarrage par `GameState.InitializeAsync`. Une partie disparue côté serveur (id inconnu) est un
cas normal, non bloquant : le client efface l'identifiant et repart de l'accueil. `GameState` ne
reconstitue toujours aucune donnée que le serveur n'a pas renvoyée — le principe du secret du jeu
(§ Conséquences) n'est pas affecté par cet ajout.

## Références

- Spec de conception § 7
- `CLAUDE.md` § 3 (Blazor WebAssembly, cycle de vie, secret du jeu)
