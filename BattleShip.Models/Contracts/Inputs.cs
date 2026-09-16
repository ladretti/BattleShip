// Shared with BattleShip.App for the same reason as GameDto.cs: the front serializes these
// very types rather than a copy of them (ADR 0008). Their VALIDATION stays on the server —
// CreateGameInputValidator and PlacementInputValidator, in BattleShip.API — since a rule
// checked only client-side is not a rule.
namespace BattleShip.Models.Contracts;

/// <summary>Input of POST /games: the grid size and the opponent's difficulty level.</summary>
public sealed record CreateGameInput(int GridSize, string Difficulty);

/// <summary>Input of POST /games/{id}/placement: the human player's whole fleet at once.</summary>
public sealed record PlacementInput(IReadOnlyList<ShipPlacementInput> Ships);

/// <summary>
/// One ship of a placement request. <see cref="Name"/> must match one of the fleet's
/// <c>ShipTemplate</c> names (see <c>PlacementInputValidator</c>) — its size is looked up
/// from the game's rules, never carried over the wire, since it is not the caller's to
/// choose.
/// </summary>
public sealed record ShipPlacementInput(string Name, int X, int Y, string Orientation);
