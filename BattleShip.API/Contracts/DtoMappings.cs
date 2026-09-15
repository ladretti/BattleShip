using BattleShip.Models;

namespace BattleShip.API.Contracts;

/// <summary>
/// Maps the domain model to the wire contract. This is the only place the secrecy
/// rule is enforced: the opponent board projects only what the player has actually
/// discovered — shots and sunk ships — and never walks <see cref="Board.Ships"/> for a
/// ship that is still afloat. The player's own board, by contrast, projects the whole
/// fleet: it is theirs, so there is nothing to hide from them.
/// </summary>
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
            game.Rules.ShipsMayTouch);

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

        // Secrecy rule: filtering on IsSunk only checks a status, it never reads the
        // cells of a ship that is still afloat. An afloat opponent ship is skipped
        // entirely — its name and cells are never touched, let alone projected.
        var sunkShips = board.Ships
            .Where(ship => ship.IsSunk)
            .Select(ToSunkShipDto)
            .ToList();

        return new OpponentBoardDto(board.GridSize, shots, sunkShips);
    }

    private static ShipDto ToShipDto(Ship ship)
    {
        // The player's own ship: every cell is projected, hit or not — there is
        // nothing to hide on your own board. "ship" (an afloat, untouched cell) must
        // never appear under OpponentBoardDto; ToSunkShipDto never produces it, since
        // it only ever runs on ships that are already fully hit.
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

    /// <summary>
    /// Lower-case rendering of a shot's outcome ("miss" | "hit" | "sunk") — the casing
    /// rule <see cref="GameDto"/>'s own doc comment fixes for shot outcomes, shared
    /// verbatim with the gRPC-Web contract's <c>Shot.result</c> field (task 14). Internal,
    /// not private: <c>BattleGrpcService</c> reuses this exact conversion for the
    /// <c>FireResponse</c> it builds, rather than writing a second one.
    /// </summary>
    internal static string ToState(ShotResult result) => result switch
    {
        ShotResult.Miss => "miss",
        ShotResult.Hit => "hit",
        ShotResult.Sunk => "sunk",
        _ => throw new ArgumentOutOfRangeException(nameof(result), result, "Unknown shot result.")
    };
}
