namespace BattleShip.API.Contracts;

/// <summary>
/// A single cell, always tied to something the receiving player is entitled to know:
/// a shot that was fired (<see cref="OpponentBoardDto.Shots"/>,
/// <see cref="OwnBoardDto.ReceivedShots"/>) or a cell of a ship that hit status has
/// already revealed (<see cref="ShipDto.Cells"/>, <see cref="SunkShipDto.Cells"/>).
/// <see cref="State"/> is one of "miss", "hit" or "sunk" — there is deliberately no
/// fourth value for "nothing happened here yet", because this type never carries a
/// cell whose outcome is still secret.
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
/// One of the player's own ships. <see cref="Cells"/> lists the cells hit so far (all
/// of them, once <see cref="IsSunk"/> is true) — never the cells still afloat, since a
/// cell with no shot outcome has no valid <see cref="CellDto.State"/> to carry.
/// </summary>
public sealed record ShipDto(string Name, int Size, IReadOnlyList<CellDto> Cells, bool IsSunk);

/// <summary>One entry of the shot history, as exposed by the history endpoint (task 20).</summary>
public sealed record ShotDto(int X, int Y, string Result, string By, string? SunkShipName);

/// <summary>The whole state of a game as the current player is entitled to see it.</summary>
public sealed record GameDto(Guid Id, string Status, string CurrentPlayer, OwnBoardDto Own, OpponentBoardDto Opponent);
