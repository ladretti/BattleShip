# ADR 0003 : `IOpponentStrategy` et contrat `ShotHistory`

## Statut et date
Accepté — 2026-09-15

## Contexte

L'adversaire doit être remplaçable et son niveau doit être mesurable. Deux exigences en
découlent : plusieurs implémentations réelles (un pattern Strategy avec une seule
implémentation est difficile à défendre), et une garantie qu'aucune d'elles ne peut tricher.

Un adversaire qui consulte la grille adverse est un adversaire dont le niveau ne veut rien
dire. Cette garantie ne peut pas reposer sur la relecture : elle doit tenir dans le type.

## Options envisagées

**Périmètre.** Une, deux ou trois stratégies.
Une seule rend l'abstraction difficile à justifier. Deux (aléatoire, chasse/cible) suffisent
à justifier le pattern mais laissent de côté la pièce la plus démonstrative. Trois donnent
trois niveaux de difficulté réels et un `[Theory]` d'invariant qui prend tout son sens.

**Forme du contrat.** Passer le `Game`, passer le `Board` adverse, ou passer une vue
restreinte.
Passer `Game` ou `Board` rend la triche possible et fait dépendre la stratégie de tout le
modèle. Une vue restreinte dédiée coûte un type de plus, mais rend la triche impossible.

**Information sur les navires coulés.** L'inclure ou non dans la vue.
Sans elle, `DensityStrategy` ne peut pas énumérer les placements légaux : elle est
impossible. Avec un accès au `Board`, elle serait tricheuse. L'information « tel navire est
coulé » est en revanche **légitime** : on l'annonce aussi au joueur humain.

## Décision

Trois stratégies, et une vue restreinte qui porte exactement l'information publique.

```csharp
public sealed record ShotHistory(
    int GridSize,
    IReadOnlyList<ShotRecord> Shots,
    IReadOnlyList<ShipTemplate> RemainingShips,
    IReadOnlyList<Ship> SunkShips,
    bool ShipsMayTouch);

public interface IOpponentStrategy
{
    string Name { get; }
    Coordinate NextShot(ShotHistory history);
}
```

| Niveau | Stratégie | Principe |
|---|---|---|
| Facile | `RandomStrategy` | tire au hasard parmi les cases non jouées |
| Normal | `HuntTargetStrategy` | chasse sur les cases de parité `(x+y)%2==0`, puis ratisse les voisines après une touche |
| Difficile | `DensityStrategy` | énumère les placements encore légaux de chaque navire non coulé, compte les passages par case, vise le maximum |

La parité est retenue parce qu'elle a le meilleur rapport gain/effort du lot : tout navire de
taille ≥ 2 couvre forcément une case de parité paire, donc la phase de chasse peut ignorer la
moitié de la grille. Une ligne de filtre, et elle s'explique en une phrase.

`RandomStrategy` reçoit un `Random` par injection, jamais `Random.Shared` en dur.

## Conséquences

- `ShipsMayTouch` figure dans la vue : avec la non-adjacence (ADR 0006), `DensityStrategy`
  peut exclure la couronne autour d'un navire coulé, ce qui la renforce nettement.
- Combinée à la règle « touche = on rejoue », elle exploite cette information dans le tour
  même — d'où un niveau Difficile très dur (voir ADR 0006, conséquences).
- Ajouter une quatrième stratégie plus tard ne coûte qu'une classe : le test d'invariant est
  paramétré par la liste des implémentations et la couvrira automatiquement.
- Le moteur doit construire `ShotHistory` à chaque tour de l'adversaire. Coût négligeable,
  mais c'est un point de passage obligé à ne pas court-circuiter « pour aller plus vite ».

## Vérification et réexamen

À vérifier lors de l'implémentation, non encore fait :

- **Invariant** — `[Theory]` paramétré par les trois implémentations, graine fixe, parties
  jouées jusqu'au bout : jamais un coup hors grille, jamais deux fois la même case. Ce test
  échoue sur une stratégie qui ne filtrerait pas les cases déjà jouées, ou qui déborderait au
  ratissage sur un bord — les deux fautes les plus probables.
- **Mesure** — duel d'IA sur N parties à graine fixe, nombre moyen de coups par niveau.
  Résultat attendu avant exécution : `RandomStrategy` > `HuntTargetStrategy` >
  `DensityStrategy`. Si l'ordre n'est pas respecté, une stratégie est fautive.

Les valeurs souvent citées dans la littérature (≈ 96 / ≈ 65 / ≈ 42 coups) portent sur la
grille 10×10 classique **sans** non-adjacence. Elles ne sont pas transposables ici et ne sont
pas mesurées à ce jour : **hypothèse, pas résultat.**

## Références

- Spec de conception § 4
- `CLAUDE.md` § 3 bis (Strategy, injection de la source d'aléa)
