using BattleShip.Models;
using BattleShip.Models.Contracts;

namespace BattleShip.API.Contracts;

public static class DtoMappings
{
    public static GameDto ToDto(this Game game) =>
        new(
            game.Id,
            game.Status.ToString(),
            game.CurrentPlayer.ToString(),
            ToOwnBoardDto(game.HumanBoard, game.History),
            ToOpponentBoardDto(game.OpponentBoard, game.History),
            game.OpponentDifficulty,
            [.. game.Rules.Fleet.Select(t => new ShipTemplateDto(t.Name, t.Size))],
            game.Rules.ShipsMayTouch,
            game.Winner?.ToString());

    public static IReadOnlyList<ShotDto> ToDto(this IReadOnlyList<ShotRecord> history) =>
        [.. history.Select(ToShotDto)];

    private static OwnBoardDto ToOwnBoardDto(Board board, IReadOnlyList<ShotRecord> history)
    {
        var receivedShots = history
            .Where(shot => shot.By == Player.Opponent)
            .Select(ToCellDto)
            .ToList();

        var ships = board.Ships.Select(ToShipDto).ToList();

        return new OwnBoardDto(board.GridSize, ships, receivedShots);
    }

    private static OpponentBoardDto ToOpponentBoardDto(Board board, IReadOnlyList<ShotRecord> history)
    {
        var shots = history
            .Where(shot => shot.By == Player.Human)
            .Select(ToCellDto)
            .ToList();

        var sunkShips = board.Ships
            .Where(ship => ship.IsSunk)
            .Select(ToSunkShipDto)
            .ToList();

        return new OpponentBoardDto(board.GridSize, shots, sunkShips);
    }

    private static ShipDto ToShipDto(Ship ship)
    {
        var cells = ship.Cells
            .Select(cell => new CellDto(cell.X, cell.Y, CellState(ship, cell)))
            .ToList();

        return new ShipDto(ship.Name, ship.Size, cells, ship.IsSunk);
    }

    private static string CellState(Ship ship, Coordinate cell)
    {
        if (ship.IsSunk)
            return "sunk";

        return ship.HitCells.Contains(cell) ? "hit" : "ship";
    }

    private static SunkShipDto ToSunkShipDto(Ship ship) =>
        new(ship.Name, [.. ship.Cells.Select(cell => new CellDto(cell.X, cell.Y, "sunk"))]);

    private static CellDto ToCellDto(ShotRecord shot) =>
        new(shot.At.X, shot.At.Y, ToState(shot.Result));

    private static ShotDto ToShotDto(ShotRecord shot) =>
        new(shot.At.X, shot.At.Y, ToState(shot.Result), shot.By.ToString(), shot.SunkShipName);

    internal static string ToState(ShotResult result) => result switch
    {
        ShotResult.Miss => "miss",
        ShotResult.Hit => "hit",
        ShotResult.Sunk => "sunk",
        _ => throw new ArgumentOutOfRangeException(nameof(result), result, "Unknown shot result.")
    };
}
