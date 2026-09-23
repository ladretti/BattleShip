namespace BattleShip.Models;

public static class GameFold
{
    public static Game Fold(Guid id, IReadOnlyList<GameEvent> events)
    {
        if (events.Count == 0)
            throw new ArgumentException("A journal must contain at least a GameCreated event.", nameof(events));

        if (events[0] is not GameCreated created)
            throw new InvalidOperationException("A journal must open with a GameCreated event.");

        var game = Game.Create(
            id,
            created.Rules,
            [.. created.OpponentShips.Select(s => s.ToShip())],
            created.Difficulty);

        foreach (var next in events.Skip(1))
            game.Replay(next);

        return game;
    }
}
