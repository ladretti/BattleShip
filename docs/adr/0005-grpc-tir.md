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

À vérifier lors de l'implémentation, non encore fait :

- **Spike prioritaire, au jour 1** : un test d'intégration qui appelle un service gRPC
  minimal via `WebApplicationFactory` et qui passe — **avant** d'écrire la logique de tir.
  Le découvrir au jour 1 plutôt qu'au jour 4 est tout l'intérêt de le faire d'abord.
- Trois tests d'intégration sur `Fire` : un succès, un `InvalidArgument`, un `NotFound`.
- Une capture de la console réseau du navigateur montrant l'appel et l'erreur.

À réexaminer si le spike échoue : il faudrait alors basculer sur l'option B, où le tir reste
testable par sa façade HTTP.

## Références

- Spec de conception § 6
- `CLAUDE.md` § 3 (gRPC-Web)
- `csharp-school/Ressources Bataille Navale/Exemples/catalogue.proto`
