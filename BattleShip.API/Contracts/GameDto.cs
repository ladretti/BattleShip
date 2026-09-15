namespace BattleShip.API.Contracts;

/// <summary>
/// A single cell, always tied to something the receiving player is entitled to know.
/// <see cref="State"/> is one of "miss", "hit", "sunk" or "ship" (lower case: see
/// <see cref="GameDto"/> for the casing rule).
///
/// "miss", "hit" and "sunk" report the outcome of a shot that was actually fired —
/// they appear under <see cref="OpponentBoardDto.Shots"/>,
/// <see cref="OwnBoardDto.ReceivedShots"/> and, once a ship is sunk, under
/// <see cref="ShipDto.Cells"/> / <see cref="SunkShipDto.Cells"/>.
///
/// "ship" marks a cell of one of the player's own ships that has not been hit yet. It
/// can only appear under <see cref="OwnBoardDto"/>: <see cref="SunkShipDto"/> (under
/// <see cref="OpponentBoardDto"/>) only ever projects ships that are already fully
/// hit, so it never has an untouched cell to describe. This is precisely what keeps
/// "ship" from ever exposing an undiscovered opposing cell.
/// </summary>
public sealed record CellDto(int X, int Y, string State);

/// <summary>
/// The player's own board: their whole fleet — every ship, whatever its condition —
/// plus every shot the opponent has fired at them. Nothing here is secret from the
/// player, since it is their own board.
/// </summary>
public sealed record OwnBoardDto(int GridSize, IReadOnlyList<ShipDto> Ships, IReadOnlyList<CellDto> ReceivedShots);

/// <summary>
/// The opponent's board as the player is entitled to see it: the shots the player has
/// fired (with their result) and the opponent's ships once — and only once — they are
/// sunk. An afloat opponent ship never appears here, and this type carries no property
/// that could hold one.
/// </summary>
public sealed record OpponentBoardDto(int GridSize, IReadOnlyList<CellDto> Shots, IReadOnlyList<SunkShipDto> SunkShips);

/// <summary>A sunk ship, identified by name, with the cells the player discovered one hit at a time.</summary>
public sealed record SunkShipDto(string Name, IReadOnlyList<CellDto> Cells);

/// <summary>
/// One of the player's own ships. <see cref="Cells"/> always lists every one of the
/// ship's <see cref="Size"/> cells — it is the player's own ship, so its full shape is
/// never secret. Each cell's <see cref="CellDto.State"/> is "sunk" once the whole ship
/// is sunk, otherwise "hit" or "ship" depending on that individual cell.
/// </summary>
public sealed record ShipDto(string Name, int Size, IReadOnlyList<CellDto> Cells, bool IsSunk);

/// <summary>One entry of the shot history, as exposed by the history endpoint (task 20).</summary>
public sealed record ShotDto(int X, int Y, string Result, string By, string? SunkShipName);

/// <summary>
/// The whole state of a game as the current player is entitled to see it.
///
/// Casing rule for the string fields of this DTO graph, settled to match the gRPC-Web
/// contract of task 14 rather than pick a single convention for its own sake: shot
/// outcomes (<see cref="CellDto.State"/>, <see cref="ShotDto.Result"/>) are lower case
/// ("miss", "hit", "sunk", "ship"), because the task 14 `.proto`'s `Shot.result` field
/// is documented there as `miss | hit | sunk`. <see cref="Status"/> and
/// <see cref="CurrentPlayer"/> stay PascalCase (e.g. "InProgress", "Human"), because
/// that `.proto`'s `FireResponse` documents `status`/`current_player` the same way;
/// <see cref="ShotDto.By"/> follows suit for consistency, since it carries the same
/// <c>Player</c> enum as <see cref="CurrentPlayer"/>. Both casings are deliberate;
/// neither is an oversight.
/// </summary>
public sealed record GameDto(Guid Id, string Status, string CurrentPlayer, OwnBoardDto Own, OpponentBoardDto Opponent);
