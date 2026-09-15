# ADR 0007 : Gestion de l'état côté Blazor

## Statut et date
Accepté — 2026-09-15

## Contexte

Le parcours traverse trois pages : nouvelle partie → placement manuel → jeu. L'identifiant de
partie, les règles choisies et l'état courant doivent survivre à la navigation entre ces
pages.

## Options envisagées

**A. Service `GameState` singleton, avec événement `OnChange`.**
Un service enregistré dans `Program.cs` détient la partie courante et notifie les composants
abonnés. Les composants deviennent de l'affichage quasi pur.

**B. État dans le composant page, passé en paramètres.**
Aucun service, aucune abstraction — le plus simple à lire pour une page unique. Mais avec une
page de placement séparée de la page de jeu, il faudrait repasser par le serveur à chaque
navigation.

**C. Tout sur une page unique, sans navigation.**
Élimine la question, mais concentre création, placement et partie dans un seul `.razor` —
exactement le signal « ce fichier en fait trop ».

## Décision

Option A. `GameState` est enregistré en singleton (en Blazor WebAssembly, `Scoped` et
`Singleton` se confondent : il n'y a qu'un seul utilisateur et qu'une seule session).

Le service détient la partie courante et expose un événement `OnChange` ; les composants s'y
abonnent dans `OnInitializedAsync` et s'en **désabonnent** dans `Dispose`.

## Conséquences

- Les composants `.razor` restent centrés sur le rendu et les événements.
- Oublier le désabonnement provoque une fuite et des rendus sur des composants détruits :
  tout composant qui s'abonne implémente `IDisposable`.
- Le service est le point unique où les trois états — **chargement**, **succès**, **échec** —
  sont représentés. Chaque page les gère ; une coupure de communication ne doit pas rendre
  l'interface inutilisable.
- La prévisualisation de validité du placement, côté client, est un **confort** et non une
  garantie : la règle fait foi côté serveur. Le service ne doit jamais devenir le lieu où une
  règle du jeu est décidée.
- `GameState` ne stocke que ce que le serveur a renvoyé. Il ne reconstitue jamais la flotte
  adverse (règle du secret, ADR 0001).

## Vérification et réexamen

À vérifier lors de l'implémentation, non encore fait :

- Vérification manuelle dans le navigateur : créer une partie, placer la flotte, naviguer
  vers le jeu, revenir — l'état doit être conservé.
- Vérifier dans la console F12 qu'aucun avertissement de rendu sur composant détruit
  n'apparaît après plusieurs allers-retours.
- Couper l'API en cours de partie et vérifier que l'interface affiche un état d'échec
  exploitable plutôt qu'une page figée.

À réexaminer si une reprise de partie après rechargement du navigateur entrait au périmètre :
il faudrait alors persister l'identifiant de partie côté client.

## Références

- Spec de conception § 7
- `CLAUDE.md` § 3 (Blazor WebAssembly, cycle de vie, secret du jeu)
