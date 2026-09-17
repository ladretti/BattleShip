using BattleShip.Models;
using BattleShip.Models.Contracts;

namespace BattleShip.API.Contracts;

public static class EventProjection
{
    public static IReadOnlyList<GameEventDto> ForPlayer(
        IReadOnlyList<GameEvent> events, bool gameIsOver) =>
        [.. events.Select(e => ToDto(e, gameIsOver))];

    private static GameEventDto ToDto(GameEvent source, bool gameIsOver) => source switch
    {
        GameCreated created => new GameCreatedDto(
            created.Sequence,
            created.Rules.GridSize,
            [.. created.Rules.Fleet.Select(t => new ShipTemplateDto(t.Name, t.Size))],
            created.Rules.ShipsMayTouch,
            created.Difficulty,
            gameIsOver ? ToShipDtos(created.OpponentShips) : []),

        HumanFleetPlaced placed => new HumanFleetPlacedDto(
            placed.Sequence, ToShipDtos(placed.Ships)),

        ShotFired shot => new ShotFiredDto(
            shot.Sequence, shot.At.X, shot.At.Y, shot.By.ToString(),
            DtoMappings.ToState(shot.Result), shot.SunkShipName),

        GameEnded ended => new GameEndedDto(ended.Sequence, ended.Winner.ToString()),

        _ => throw new ArgumentOutOfRangeException(
            nameof(source), source, "Unknown event type.")
    };

    private static IReadOnlyList<ShipDto> ToShipDtos(IReadOnlyList<ShipSnapshot> ships) =>
        [.. ships.Select(s => new ShipDto(
            s.Name, s.Size,
            [.. s.Cells.Select(c => new CellDto(c.X, c.Y, "ship"))],
            IsSunk: false))];
}
