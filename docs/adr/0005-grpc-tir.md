# ADR 0005 : Le tir en gRPC-Web exclusif

## Statut et date
Accepté — 2026-09-15

## Contexte

Le sujet impose un échange gRPC-Web fonctionnel, avec une **réponse et une erreur attendue**
démontrables depuis le navigateur. Tous les échanges ne s'y prêtent pas également : il faut un
appel fréquent, avec des refus **naturels** plutôt que fabriqués pour l'occasion, et de préférence
une charge utile qui illustre une règle du domaine.

## Options envisagées

- **A. Le tir, en gRPC-Web uniquement.** Répartition nette — le tir en gRPC, tout le reste en
  HTTP — et aucune façade dupliquée ; mais `api.http` ne couvre plus l'échange le plus intéressant.
- **B. Le tir, exposé en HTTP *et* en gRPC-Web.** Un gestionnaire de domaine, deux façades, un
  sélecteur dans le front : comparaison côte à côte, au prix de deux façades à maintenir et valider.
- **C. Le tir unaire + un flux serveur des coups adverses.** Très démonstratif avec « touche = on
  rejoue », mais le streaming gRPC-Web a des limites navigateur et ajoute un risque technique.

Critère retenu : **la netteté des responsabilités**, contre la commodité de la démonstration.

## Décision

Option A. Le tir est le seul échange gRPC.

```proto
service BattleService {
  rpc Fire (FireRequest) returns (FireResponse);
}
```

Appel fréquent, et deux erreurs qui surviennent d'elles-mêmes — case déjà jouée
(`InvalidArgument`), partie inconnue (`NotFound`) ; la création n'aurait eu d'erreur qu'artificielle.
Avec « touche = on rejoue » (ADR 0006), `Fire` renvoie une **séquence** : le coup du joueur, puis la
chaîne des coups adverses jusqu'au premier manqué.

## Conséquences

- **`api.http` ne couvre pas le tir.** La démonstration passe par le navigateur (console F12,
  onglet Réseau) et par les tests d'intégration ; le scénario est écrit pas à pas dans le `README.md`.
- Le serveur active `AddGrpc()`, `UseGrpcWeb()` et `MapGrpcService<T>().EnableGrpcWeb()` ; le
  client passe par `GrpcWebHandler`, et CORS doit autoriser les en-têtes gRPC-Web.
- FluentValidation s'applique à `FireRequest` comme aux entrées HTTP ; la démonstration HTTP de
  FluentValidation repose alors sur l'endpoint de **placement**, le validateur le plus riche.
- **Risque technique** : si le montage `GrpcChannel` sur le `HttpMessageHandler` du serveur de test
  résistait, tout le tir deviendrait intestable.
- `app.UseCors("front")` précède `app.UseGrpcWeb()`, comme le recommande la documentation CORS
  d'ASP.NET Core. `CorsTests` passe dans les **deux** ordres (permutation essayée : réponses
  identiques) : cet ordre repose sur la documentation, pas sur un test discriminant.

## Vérification et réexamen

- Le spike de la tâche 2 a levé le risque : le montage `GrpcChannel` + `GrpcWebHandler` sur
  `TestServer.CreateHandler()` fonctionne. Il a cédé la place à `Api/FireGrpcTests.cs`.
- Cinq tests d'intégration sur `Fire` : succès, case déjà jouée, hors grille, partie inconnue,
  enchaînement adverse. Les sept `GameError` sont traduits dans le seul `ErrorMapping.cs`, partagé
  avec la façade HTTP.
- Pouvoir discriminant établi : `GameNotFound` remappé vers `StatusCode.Unknown` fait échouer
  `A_shot_on_an_unknown_game_returns_NotFound` (`Expected: NotFound / Actual: Unknown`).
- Course `Read` / `Mutate` : verrou de `Read` retiré, **5 exécutions sur 5 en échec**
  (`ArgumentException`) ; rétabli, 5/5 au vert et suite 133/133 (commit `aef815e`).
- **Constaté** : le scénario du README piloté dans Chrome renvoie `HTTP 200` avec `grpc-status` `3`
  puis `5` dans les trailers — trace et captures dans `docs/demo/grpc-web-trace.md`.

## Références

- Spec de conception § 6 ; `CLAUDE.md` § 3 (gRPC-Web)
- `csharp-school/Ressources Bataille Navale/Exemples/catalogue.proto`
