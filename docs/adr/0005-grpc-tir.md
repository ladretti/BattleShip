# ADR 0005 : Le tir en gRPC-Web exclusif

## Statut et date
Accepté — 2026-09-15

## Contexte

Le sujet impose un échange gRPC-Web fonctionnel, avec une **réponse et une erreur attendue**
démontrables depuis le navigateur. Tous les échanges ne s'y prêtent pas également. Il faut un
appel fréquent (démonstration facile), avec des refus **naturels** plutôt que fabriqués pour
l'occasion, et de préférence une charge utile qui illustre une règle du domaine.

## Options envisagées

**A. Le tir, en gRPC-Web uniquement.**
Répartition nette : le tir en gRPC, tout le reste en HTTP. Aucune façade dupliquée. Mais
`api.http` ne peut plus couvrir l'échange le plus intéressant du projet.

**B. Le tir, exposé en HTTP *et* en gRPC-Web.**
Un seul gestionnaire de domaine, deux façades, un sélecteur dans le front. Permet une
comparaison côte à côte. Coût : deux façades à maintenir et à valider.

**C. Le tir unaire + un flux serveur des coups de l'adversaire.**
Particulièrement pertinent avec « touche = on rejoue », où l'adversaire joue plusieurs fois
d'affilée. Très démonstratif, mais le streaming gRPC-Web a des limites navigateur et
constitue un risque technique supplémentaire.

Critère retenu : **la netteté de la répartition des responsabilités**, contre la commodité de
la démonstration.

## Décision

Option A. Le tir est le seul échange gRPC.

```proto
service BattleService {
  rpc Fire (FireRequest) returns (FireResponse);
}
```

Il coche les trois critères : appel fréquent, et deux erreurs qui surviennent d'elles-mêmes —
case déjà jouée (`InvalidArgument`) et partie inconnue (`NotFound`). La création de partie, à
l'inverse, n'aurait eu d'erreur qu'artificielle.

Avec « touche = on rejoue » (ADR 0006), `Fire` renvoie une **séquence** et non un coup : le
résultat du coup du joueur, puis la chaîne des coups de l'adversaire jusqu'à son premier
manqué.

## Conséquences

- **`api.http` ne couvre pas le tir.** La démonstration passe par le navigateur (console F12,
  onglet Réseau) et par les tests d'intégration. Le scénario de démonstration **doit** être
  écrit pas à pas dans le `README.md`, sinon la contrainte « démontrable depuis le
  navigateur » ne se prouve pas en soutenance.
- Le serveur active `AddGrpc()`, `UseGrpcWeb()` et `MapGrpcService<T>().EnableGrpcWeb()` ;
  le client passe par `GrpcWebHandler`. CORS doit autoriser les en-têtes gRPC-Web.
- La validation FluentValidation s'applique à `FireRequest` comme aux entrées HTTP.
- La démonstration HTTP de FluentValidation repose alors sur l'endpoint de **placement**, qui
  refuse débordement, chevauchement et adjacence — c'est de toute façon le validateur le plus
  intéressant du projet.
- **Risque technique** : tester un service gRPC via `WebApplicationFactory` demande de
  brancher un `GrpcChannel` sur le `HttpMessageHandler` du serveur de test. Si ce montage
  résiste, tout le tir devient intestable.

## Vérification et réexamen

Vérifié à l'implémentation (tâche 14) :

- Le spike (`GrpcHarnessTests`, un `PingService` minimal monté via `WebApplicationFactory`)
  a réussi dès la tâche 2 : le montage `GrpcChannel` + `GrpcWebHandler` sur
  `TestServer.CreateHandler()` fonctionne. Le spike a été retiré à la tâche 14 (son rôle
  rempli), remplacé par `BattleShip.Tests/Api/FireGrpcTests.cs`.
- Cinq tests d'intégration sur `Fire` (dépassant les trois envisagés ici) : un succès, une
  case déjà jouée (`InvalidArgument`), une case hors grille (`InvalidArgument`), une partie
  inconnue (`NotFound`), et l'enchaînement des tirs adverses sur un tir manqué. Les sept
  membres de `GameError` sont traduits vers un `StatusCode` gRPC en un seul endroit
  (`ErrorMapping.cs`), partagé avec la façade HTTP.
- Contrôle à pouvoir discriminant réalisé sur la traduction d'erreur : `GameNotFound` a été
  temporairement remappé vers `StatusCode.Unknown` — le test `A_shot_on_an_unknown_game_returns_NotFound`
  échoue alors avec `Expected: NotFound / Actual: Unknown`, confirmant que le test mesure
  bien ce qu'il prétend mesurer (rapport de la tâche 14).
- **Course `IGameStore.Read` vs `Mutate` : concluant, au niveau du store.** Une première
  version au niveau HTTP/gRPC (lecture `GET`, tir `Fire`) n'a jamais observé de corruption
  malgré un chevauchement horloge confirmé par instrumentation, et coûtait 4-6 s par exécution
  (suite complète portée à ~10 s) — supprimée sur retour du coordinateur : un test lent qui ne
  discrimine rien vaut moins que pas de test. Remplacée par
  `InMemoryGameStoreTests.Concurrent_fires_and_reads_never_throw_and_return_consistent_snapshots`,
  qui appelle `IGameStore.Mutate`/`Read` directement (sans HTTP ni gRPC) : verrou de `Read`
  retiré temporairement, **5 exécutions sur 5 en échec** avec une `ArgumentException:
  Destination array is not long enough...` reproductible (le symptôme d'un
  `HashSet<Coordinate>.ToList()` — `Board.ReceivedShots` — qui course une écriture
  concurrente ; pas l'`InvalidOperationException` d'un `List<T>`, annoncée mais non observée
  à ce volume). Verrou rétabli : 5/5 au vert, ~400 ms chacune ; suite complète 133/133 en
  ~4 s. Voir `REVUE-IA.md`, revue 5, pour le détail chiffré des deux tentatives.
- Non encore fait : la capture de la console réseau du navigateur montrant l'appel et
  l'erreur (nécessite l'interface Blazor, hors périmètre de la tâche 14).

## Références

- Spec de conception § 6
- `CLAUDE.md` § 3 (gRPC-Web)
- `csharp-school/Ressources Bataille Navale/Exemples/catalogue.proto`
