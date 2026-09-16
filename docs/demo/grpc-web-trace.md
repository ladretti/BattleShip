# Trace réseau de la démonstration gRPC-Web

Capturée par le protocole DevTools de Chrome pendant le scénario du README, au même
moment que les trois captures d'écran de ce dossier. C'est le contenu de l'onglet
**Réseau** de la console F12, sous forme reproductible.

`grpc-status` vaut `0` pour un succès, `3` pour `InvalidArgument` et `5` pour `NotFound`.
Une erreur gRPC-Web revient en **HTTP 200** : le statut voyage dans les *trailers*, pas
dans le code HTTP — d'où l'exposition de `grpc-status` et `grpc-message` par la politique
CORS de l'API, sans laquelle le navigateur ne pourrait pas les lire.

### 1 — Tir accepté (`01-fire-success.png`)

| Requête | HTTP | Content-Type | grpc-status | grpc-message |
|---|---|---|---|---|
| `OPTIONS /battleship.BattleService/Fire` | 204 | `` | `(sent in trailers)` | — |
| `POST /battleship.BattleService/Fire` | 200 | `application/grpc-web` | `(sent in trailers)` | — |
| `GET /games/9c8bb7db-55d7-431a-887b-7faf2df9c90d` | 200 | `application/json; charset=utf-8` | `(sent in trailers)` | — |

### 2 — Même case : `InvalidArgument` / `CellAlreadyShot` (`02-invalid-argument.png`)

| Requête | HTTP | Content-Type | grpc-status | grpc-message |
|---|---|---|---|---|
| `POST /battleship.BattleService/Fire` | 200 | `application/grpc-web` | `3` | CellAlreadyShot |
| `GET /games/9c8bb7db-55d7-431a-887b-7faf2df9c90d` | 200 | `application/json; charset=utf-8` | `(sent in trailers)` | — |

### 3 — Partie inconnue : `NotFound` (`03-not-found.png`)

| Requête | HTTP | Content-Type | grpc-status | grpc-message |
|---|---|---|---|---|
| `POST /battleship.BattleService/Fire` | 200 | `application/grpc-web` | `5` | GameNotFound |
| `GET /games/9c8bb7db-55d7-431a-887b-7faf2df9c90d` | 200 | `application/json; charset=utf-8` | `(sent in trailers)` | — |
