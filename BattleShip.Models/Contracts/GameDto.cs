namespace BattleShip.Models.Contracts;

public sealed record CellDto(int X, int Y, string State);

public sealed record OwnBoardDto(int GridSize, IReadOnlyList<ShipDto> Ships, IReadOnlyList<CellDto> ReceivedShots);

public sealed record OpponentBoardDto(int GridSize, IReadOnlyList<CellDto> Shots, IReadOnlyList<SunkShipDto> SunkShips);

public sealed record SunkShipDto(string Name, IReadOnlyList<CellDto> Cells);

public sealed record ShipDto(string Name, int Size, IReadOnlyList<CellDto> Cells, bool IsSunk);

public sealed record ShotDto(int X, int Y, string Result, string By, string? SunkShipName);

public sealed record ShipTemplateDto(string Name, int Size);

public sealed record GameDto(
    Guid Id, string Status, string CurrentPlayer, OwnBoardDto Own, OpponentBoardDto Opponent,
    string OpponentDifficulty, IReadOnlyList<ShipTemplateDto> Fleet, bool ShipsMayTouch,
    string? Winner);
