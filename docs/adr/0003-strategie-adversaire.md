# ADR 0003 : `IOpponentStrategy` et contrat `ShotHistory`

## Statut et date
Accepté — 2026-09-15

## Contexte

L'adversaire doit être remplaçable et son niveau mesurable : d'où plusieurs implémentations réelles
(un Strategy à implémentation unique est indéfendable) et la garantie qu'aucune ne triche. Le niveau
d'un adversaire qui lit la grille adverse ne veut rien dire : la garantie tient dans le type.

## Options envisagées

- **Périmètre : une, deux ou trois stratégies.** Une seule rend l'abstraction injustifiable ; deux
  justifient le pattern sans la pièce la plus démonstrative ; trois donnent trois niveaux réels et
  un `[Theory]` d'invariant qui prend son sens.
- **Contrat : le `Game`, le `Board` adverse, ou une vue restreinte.** Les deux premiers rendent la
  triche possible ; la vue restreinte coûte un type de plus et la rend impossible.
- **Navires coulés dans la vue : les inclure ou non.** Sans eux `DensityStrategy` ne peut énumérer
  les placements légaux ; « tel navire est coulé » est **légitime**, on l'annonce aussi au joueur.

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

La parité a le meilleur rapport gain/effort : tout navire de taille ≥ 2 couvre une case paire.
`RandomStrategy` reçoit un `Random` injecté, jamais `Random.Shared` en dur.

## Conséquences

- `ShipsMayTouch` figure dans la vue : avec la non-adjacence (ADR 0006), `DensityStrategy` exclut
  la couronne autour d'un navire coulé ; avec « touche = on rejoue », le Difficile devient très dur.
- Une quatrième stratégie ne coûterait qu'une classe : le test d'invariant, paramétré par la liste
  des implémentations, la couvrirait. Le moteur construit `ShotHistory` à chaque tour adverse.

## Vérification et réexamen

- **Invariant** : `[Theory]` sur les trois implémentations, graine fixe, parties jouées jusqu'au
  bout — jamais un coup hors grille ni deux fois la même case. Il échoue sur une stratégie qui ne
  filtre pas les cases jouées ou déborde sur un bord.
- **Mesuré** : 95,69 / 52,60 / 41,22 coups en moyenne sur 200 parties à graine fixe
  (`StrategyBenchmark.Run(GameRules.Default, games: 200, seed: 20260915)` ; `REVUE-IA.md`, revue 2)
  — l'ordre attendu `Random` > `HuntTarget` > `Density`, `Density` sous le seuil de 55 fixé avant
  mesure. Reproductible : `dotnet test --filter "FullyQualifiedName~StrategyBenchmark"`. Limite :
  une seule flotte, une seule grille — à réexaminer si l'une des deux changeait.

## Références

- Spec de conception § 4 ; `CLAUDE.md` § 3 bis (Strategy, injection de la source d'aléa)
