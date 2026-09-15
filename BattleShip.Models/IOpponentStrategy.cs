namespace BattleShip.Models;

/// <summary>
/// Une stratégie adverse remplaçable, pas une méthode privée du moteur (ADR 0003).
/// Chaque implémentation ne reçoit que ce que le joueur a le droit de connaître —
/// via <see cref="ShotHistory"/> — et doit respecter l'invariant vérifié par
/// StrategyInvariantTests : ne jamais proposer un coup hors grille, ni une case
/// déjà jouée.
/// </summary>
public interface IOpponentStrategy
{
    string Name { get; }

    Coordinate NextShot(ShotHistory history);
}
