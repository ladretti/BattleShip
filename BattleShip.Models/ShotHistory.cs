namespace BattleShip.Models;

/// <summary>
/// View of the game handed to an opponent strategy: its own shots and their results,
/// the remaining and sunk ships — never the opposing board nor the position of the
/// undiscovered ships. It is this absence of any reference to <see cref="Board"/>
/// that makes the invariant "a strategy does not cheat" verifiable by the type rather
/// than by review: a strategy cannot consult the opposing fleet, even by mistake,
/// since ShotHistory does not expose it.
///
/// RemainingShips and SunkShips, on the other hand, are legitimate information: they
/// are what is announced to the human player when a ship sinks. The boundary is not
/// between "little" and "much" information, but between what the player is entitled
/// to know and the position of the undiscovered ships.
/// </summary>
public sealed record ShotHistory(
    int GridSize,
    IReadOnlyList<ShotRecord> Shots,
    IReadOnlyList<ShipTemplate> RemainingShips,
    IReadOnlyList<Ship> SunkShips,
    bool ShipsMayTouch);
