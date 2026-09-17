# ADR 0008 : Le contrat de transport vit dans `BattleShip.Models`

## Statut et date
Accepté — 2026-09-16

## Contexte

La tâche 12 a créé les DTO du contrat HTTP (`GameDto`, `OwnBoardDto`, `OpponentBoardDto`, `CellDto`,
`ShipDto`, `SunkShipDto`, `ShotDto`, `ShipTemplateDto`) et les entrées (`CreateGameInput`,
`PlacementInput`, `ShipPlacementInput`) dans `BattleShip.API/Contracts/`, où seul le serveur les
manipulait. La tâche 16 ouvre le front : `BattleShip.App` doit désérialiser exactement ces formes.
Or il ne référence que `BattleShip.Models`, et lui faire référencer `BattleShip.API` tirerait tout
ASP.NET Core dans un projet WebAssembly. Il faut donc décider où vit le contrat.

## Options envisagées

- **A. Recopier les DTO dans `BattleShip.App/Contracts/`.** Le découplage client/serveur classique,
  sans toucher au code déjà testé ; mais ici les deux côtés sont compilés ensemble, donc il n'achète
  aucune indépendance de déploiement et achète un risque réel : renommer une propriété côté serveur
  ne casse **aucune compilation**, `System.Text.Json` remplit le champ client par `null` ou `0` et
  la panne apparaît à l'affichage, loin de sa cause. Dix records à tenir en phase à la main.
- **B. Un cinquième projet `BattleShip.Contracts`.** La séparation la plus propre sur le papier,
  mais le sujet fixe quatre projets (`CLAUDE.md` § 1) et le gain sur C se réduit au rangement.
- **C. Déplacer le contrat dans `BattleShip.Models`.** Une seule définition : un renommage casse la
  compilation des deux côtés. `CLAUDE.md` décrit ce projet comme « **Modèles partagés** + moteur de
  jeu ». Coût : un refactor des `using`, et un risque de brouillage domaine/transport.

## Décision

Option C. Les records du contrat vivent dans `BattleShip.Models/Contracts/`, namespace
`BattleShip.Models.Contracts`. Deux garde-fous délimitent ce que ce déplacement autorise :

- **Aucune dépendance ajoutée** : records nus, sans attribut ni `System.Text.Json`.
  `BattleShip.Models` ne référence toujours **aucun paquet**, donc « le moteur reste indépendant de
  HTTP, de JSON et de gRPC » (`CLAUDE.md` § 1) tient au sens strict.
- **La traduction reste dans l'API** : `DtoMappings` — les `ToDto()`, y compris la règle du secret —
  reste dans `BattleShip.API/Contracts/`. Le domaine porte la **forme** du contrat, jamais la
  **projection** vers lui.

## Conséquences

- Le front consomme les types que le serveur produit : la dérive silencieuse de l'option A devient
  impossible sans erreur de compilation.
- `BattleShip.Models` porte deux familles de types en deux dossiers : domaine à la racine, contrat
  sous `Contracts/`. La frontière est le dossier, pas le projet — plus faible, donc à surveiller.
- La règle du secret (ADR 0001) ne change pas de nature : `OpponentBoardDto` n'a aucune propriété
  capable de contenir un navire adverse à flot, et cette forme voyage avec les types.
- Risque assumé : un attribut de sérialisation ajouté sur ces records ferait entrer JSON dans le
  domaine ; le garde-fou est **l'absence de tout paquet dans `BattleShip.Models.csproj`**, qu'il
  faudrait d'abord modifier — geste visible en diff.

## Vérification et réexamen

- **Constaté** : après déplacement, `dotnet build` sans avertissement et `dotnet test` à 136/136,
  identique à la ligne de base prise juste avant le refactor.
- **Constaté** : le corps réellement renvoyé par `POST /games` (camelCase) se relit sans perte dans
  `GameDto` avec `JsonSerializerDefaults.Web` — 13 propriétés vérifiées, `IReadOnlyList<T>`
  imbriquées comprises, sans aucun attribut de sérialisation.
- **Constaté** : pouvoir discriminant établi — renommer `GameDto.OpponentDifficulty` en
  `GameDto.Level` fait échouer la compilation de `BattleShip.App` (`NavMenu.razor(27,29): error
  CS1061`), là où l'option A aurait compilé (`REVUE-IA.md`, revue 6).
- **Reste à vérifier** : aucun test ne garde la frontière domaine/transport, seule la revue le fait.
- À réexaminer si un client hors solution apparaissait : l'option B redeviendrait pertinente.

## Références

- `CLAUDE.md` § 1 — tableau des quatre projets ; spec § 6 et § 7
- ADR 0001 (règle du secret), ADR 0007 (état côté Blazor)
- Plan, tâche 12 (création des DTO) et tâche 16 (ouverture du front)
