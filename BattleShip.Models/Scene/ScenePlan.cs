using BattleShip.Models.Contracts;

namespace BattleShip.Models;

public static class ScenePlan
{
    public static SceneState For(
        Guid id,
        GameDto game,
        IReadOnlyList<GameEvent> events,
        int? cursor,
        Coordinate? lastImpact)
    {
        var friendly = game.Own.Ships.Select(ToSceneShip).OfType<SceneShip>().ToList();

        var wrecks = cursor is { } at
            ? WrecksAt(id, game, events, at)
            : game.Opponent.SunkShips
                .Select(s => ToSceneShip(s.Name, s.Cells, sunk: true))
                .OfType<SceneShip>()
                .ToList();

        var revealed = game.Status == nameof(GameStatus.Finished);

        return new SceneState(game.Own.GridSize, friendly, wrecks, lastImpact, revealed);
    }

    private static List<SceneShip> WrecksAt(
        Guid id, GameDto game, IReadOnlyList<GameEvent> events, int cursor)
    {
        var upTo = Prefix(events, cursor);
        var folded = upTo.Count == 0 ? null : GameFold.Fold(id, upTo);

        if (folded is { } replayed && replayed.OpponentBoard.Ships.Count > 0)
        {
            return [.. replayed.OpponentBoard.Ships
                .Where(s => s.IsSunk)
                .Select(s => ToSceneShip(s.Name, [.. s.Cells.Select(c => new CellDto(c.X, c.Y, "sunk"))], sunk: true))
                .OfType<SceneShip>()];
        }

        var sunkByCursor = upTo.OfType<ShotFired>()
            .Where(shot => shot.By == Player.Human && shot.Result == ShotResult.Sunk)
            .Select(shot => shot.SunkShipName!)
            .ToHashSet();

        return [.. game.Opponent.SunkShips
            .Where(ship => sunkByCursor.Contains(ship.Name))
            .Select(ship => ToSceneShip(ship.Name, ship.Cells, sunk: true))
            .OfType<SceneShip>()];
    }

    private static List<GameEvent> Prefix(IReadOnlyList<GameEvent> events, int cursor)
    {
        var shots = 0;
        var upTo = new List<GameEvent>();

        foreach (var next in events)
        {
            if (next is ShotFired && shots++ >= cursor)
                break;

            upTo.Add(next);
        }

        return upTo;
    }

    private static SceneShip? ToSceneShip(ShipDto ship) =>
        ToSceneShip(ship.Name, ship.Cells, ship.IsSunk);

    private static SceneShip? ToSceneShip(string name, IReadOnlyList<CellDto> cells, bool sunk)
    {
        if (cells.Count == 0)
            return null;

        var sameRow = cells.All(c => c.Y == cells[0].Y);
        var sameColumn = cells.All(c => c.X == cells[0].X);

        if (!sameRow && !sameColumn)
            return null;

        var span = sameRow
            ? cells.Max(c => c.X) - cells.Min(c => c.X)
            : cells.Max(c => c.Y) - cells.Min(c => c.Y);

        if (cells.Count != span + 1)
            return null;

        return new SceneShip(
            name,
            cells.Min(c => c.X),
            cells.Min(c => c.Y),
            cells.Count,
            !sameRow && sameColumn,
            sunk);
    }
}
