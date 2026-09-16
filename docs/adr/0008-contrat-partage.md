# ADR 0008 : Le contrat de transport vit dans `BattleShip.Models`

## Statut et date
Accepté — 2026-09-16

## Contexte

La tâche 12 a créé les DTO du contrat HTTP (`GameDto`, `OwnBoardDto`, `OpponentBoardDto`,
`CellDto`, `ShipDto`, `SunkShipDto`, `ShotDto`, `ShipTemplateDto`) et les entrées
(`CreateGameInput`, `PlacementInput`, `ShipPlacementInput`) dans
`BattleShip.API/Contracts/`. À ce stade, seul le serveur les manipulait.

La tâche 16 ouvre le front. `BattleShip.App` doit désérialiser exactement ces formes pour
afficher une partie, et sérialiser exactement ces entrées pour en créer une ou soumettre un
placement. Or `BattleShip.App` ne référence que `BattleShip.Models` : les types du contrat
lui sont inaccessibles, et lui faire référencer `BattleShip.API` tirerait tout ASP.NET Core
dans un projet WebAssembly — ce qui n'est pas envisageable.

Il faut donc décider où vit le contrat, avant d'écrire la première ligne du client HTTP.

## Options envisagées

**A. Recopier les DTO dans `BattleShip.App/Contracts/`.**
Aucune modification du code déjà commité et testé ; c'est le découplage client/serveur
classique, où chaque côté possède sa vue du contrat. Mais ici les deux côtés sont compilés
ensemble, dans la même solution, par le même binôme : le découplage n'achète aucune
indépendance de déploiement, et il achète un risque réel. Renommer une propriété côté
serveur ne casse alors **aucune compilation** : `System.Text.Json` remplit simplement le
champ client par `null` ou `0`. La panne apparaît à l'affichage, loin de sa cause. Dix
records à maintenir en phase à la main, dont un graphe imbriqué à quatre niveaux, sans
qu'aucun test existant ne puisse détecter la dérive.

**B. Un cinquième projet `BattleShip.Contracts`.**
La séparation la plus propre sur le papier : ni domaine, ni serveur, juste le fil.
Mais le sujet fixe quatre projets et leurs rôles (`CLAUDE.md` § 1, contraintes imposées).
Ajouter un projet pour dix records est une cérémonie difficile à défendre à l'oral, et le
gain sur l'option C se réduit à une question de rangement.

**C. Déplacer le contrat dans `BattleShip.Models`.**
Une seule définition, partagée par l'API et le front, donc aucune dérive possible : un
renommage casse la compilation des deux côtés à la fois. `CLAUDE.md` décrit précisément
`BattleShip.Models` comme « **Modèles partagés** + moteur de jeu » — le partage est son rôle
affiché, et `BattleShip.App` le référence déjà pour cette raison. Coût : un refactor des
`using` dans l'API et les tests, et un risque de brouillage entre domaine et transport.

## Décision

Option C. Les records du contrat vivent dans `BattleShip.Models/Contracts/`, namespace
`BattleShip.Models.Contracts`.

Deux garde-fous délimitent ce que ce déplacement autorise :

- **Aucune dépendance ajoutée.** Ce sont des records nus, sans attribut, sans
  `System.Text.Json`, sans `JsonPropertyName`. `BattleShip.Models` ne référence toujours
  aucun autre projet et aucun paquet : la contrainte « le moteur de jeu reste indépendant de
  HTTP, de JSON et de gRPC » (`CLAUDE.md` § 1) est tenue au sens strict — le déplacement
  n'apporte que des types, jamais un mécanisme de transport.
- **La traduction reste dans l'API.** `DtoMappings` — les méthodes d'extension `ToDto()` qui
  projettent `Game` sur le contrat, y compris la règle du secret — reste dans
  `BattleShip.API/Contracts/`. Le domaine porte la **forme** du contrat, jamais la
  **projection** vers lui. C'est ce qui empêche le second de devenir un domaine anémique qui
  se connaîtrait comme DTO.

## Conséquences

- Le front consomme les mêmes types que le serveur produit : la dérive silencieuse
  décrite en option A devient impossible sans erreur de compilation.
- `BattleShip.Models` porte désormais deux familles de types dans deux dossiers distincts :
  le domaine à la racine, le contrat sous `Contracts/`. La frontière est le dossier et le
  namespace, pas le projet — plus faible qu'une frontière de compilation, donc à surveiller
  en revue.
- La règle du secret (ADR 0001) ne change pas de nature : elle est portée par la **forme**
  des types — `OpponentBoardDto` n'a aucune propriété capable de contenir un navire adverse à
  flot — et cette forme voyage avec eux. `SecretTests` continue de la vérifier sur la
  projection réelle.
- Risque assumé : quelqu'un pourrait être tenté d'ajouter un attribut de sérialisation sur
  ces records, ce qui ferait entrer JSON dans le domaine. Le garde-fou est la revue et
  l'absence de tout paquet dans `BattleShip.Models.csproj` — ajouter l'attribut exigerait
  d'abord d'ajouter la référence, geste visible en diff.

## Vérification et réexamen

- **Constaté** : après déplacement, `dotnet build` passe sans avertissement et
  `dotnet test` donne 136/136, à l'identique de la ligne de base prise juste avant le
  refactor. Le déplacement n'a donc modifié aucun comportement observable côté serveur.
- **Constaté** : le corps réellement renvoyé par `POST /games` (camelCase) se relit sans
  perte dans `GameDto` avec les options `JsonSerializerDefaults.Web`, celles-là mêmes
  qu'emploie `System.Net.Http.Json` dans le navigateur — 13 propriétés vérifiées une à une,
  y compris les `IReadOnlyList<T>` imbriquées. Aucun attribut de sérialisation n'a été
  nécessaire.
- **Constaté** : le pouvoir discriminant de l'option retenue a été établi par une expérience
  dédiée — renommer `GameDto.OpponentDifficulty` en `GameDto.Level` sans toucher au front
  fait échouer la compilation de `BattleShip.App` (`NavMenu.razor(27,29): error CS1061`), là
  où l'option A aurait compilé et affiché un niveau vide à l'exécution. Voir `REVUE-IA.md`,
  revue 4.
- **Reste à vérifier** : rien dans la compilation n'empêche un futur attribut JSON sur ces
  records. Aucun test ne garde cette frontière ; seule la revue le fait.
- À réexaminer si un client hors solution apparaissait (application mobile, service tiers) :
  le partage par référence de projet cesserait alors d'être disponible, et l'option B
  redeviendrait pertinente.

## Références

- `CLAUDE.md` § 1 — tableau des quatre projets, « Modèles partagés + moteur de jeu »
- ADR 0001 (représentation et règle du secret), ADR 0007 (état côté Blazor)
- Spec de conception § 6 « Contrat d'API » et § 7 « Interface »
- Plan, tâche 12 (création des DTO) et tâche 16 (ouverture du front)
