using BattleShip.Models;
using BattleShip.Models.Contracts;

namespace BattleShip.Tests.Domain;

public sealed class ScenePlanTests
{
    private static (Guid Id, GameDto Dto, IReadOnlyList<GameEvent> Events, Game Game) Played(int seed, int shots)
    {
        var rules = GameRules.Default;
        var human = new FleetPlacer(new Random(seed)).PlaceAll(rules).Value;
        var opponent = new FleetPlacer(new Random(seed + 1)).PlaceAll(rules).Value;
        var game = Game.Start(Guid.NewGuid(), rules, human, opponent);

        var fired = 0;
        foreach (var cell in opponent.SelectMany(s => s.Cells).ToList())
        {
            if (fired >= shots || game.Status == GameStatus.Finished)
                break;

            if (game.CurrentPlayer != Player.Human)
            {
                var free = Enumerable.Range(0, rules.GridSize * rules.GridSize)
                    .Select(i => new Coordinate(i % rules.GridSize, i / rules.GridSize))
                    .First(c => !game.HumanBoard.ReceivedShots.Contains(c));
                Assert.True(game.OpponentFires(free).IsOk);
                continue;
            }

            Assert.True(game.PlayerFires(cell).IsOk);
            fired++;
        }

        return (game.Id, DtoFor(game), game.Events, game);
    }

    private static GameDto DtoFor(Game game) =>
        new(game.Id,
            game.Status.ToString(),
            game.CurrentPlayer.ToString(),
            new OwnBoardDto(
                game.Rules.GridSize,
                [.. game.HumanBoard.Ships.Select(s => new ShipDto(
                    s.Name, s.Size,
                    [.. s.Cells.Select(c => new CellDto(c.X, c.Y, s.IsSunk ? "sunk" : "ship"))],
                    s.IsSunk))],
                []),
            new OpponentBoardDto(
                game.Rules.GridSize,
                [],
                [.. game.OpponentBoard.Ships.Where(s => s.IsSunk)
                    .Select(s => new SunkShipDto(
                        s.Name,
                        [.. s.Cells.Select(c => new CellDto(c.X, c.Y, "sunk"))]))]),
            game.OpponentDifficulty,
            [.. game.Rules.Fleet.Select(t => new ShipTemplateDto(t.Name, t.Size))],
            game.Rules.ShipsMayTouch,
            game.Winner?.ToString());

    [Fact]
    public void During_a_game_the_wrecks_are_only_sunk_ships()
    {
        var (id, dto, events, game) = Played(seed: 41, shots: 6);
        Assert.NotEqual(GameStatus.Finished, game.Status);

        var scene = ScenePlan.For(id, dto, events, cursor: null, lastImpact: null);

        Assert.Equal(
            game.OpponentBoard.Ships.Where(s => s.IsSunk).Select(s => s.Name).Order(),
            scene.Wrecks.Select(w => w.Name).Order());
        Assert.False(scene.Revealed);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(7)]
    [InlineData(12)]
    [InlineData(41)]
    [InlineData(99)]
    [InlineData(123)]
    [InlineData(2024)]
    public void During_a_game_no_unsunk_opposing_cell_reaches_the_scene(int seed)
    {
        var (id, dto, events, game) = Played(seed, shots: 6);
        Assert.NotEqual(GameStatus.Finished, game.Status);

        var scene = ScenePlan.For(id, dto, events, cursor: null, lastImpact: null);

        var revealed = game.OpponentBoard.ReceivedShots;
        var secret = game.OpponentBoard.Ships
            .SelectMany(s => s.Cells)
            .Where(c => !revealed.Contains(c))
            .ToHashSet();

        Assert.NotEmpty(secret);

        var exposedWrecks = scene.Wrecks.SelectMany(Footprint).ToHashSet();
        Assert.Empty(exposedWrecks.Intersect(secret));

        var friendlyCells = scene.Friendly.SelectMany(Footprint).ToHashSet();
        var humanCells = game.HumanBoard.Ships.SelectMany(s => s.Cells).ToHashSet();
        Assert.Equal(humanCells, friendlyCells);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(7)]
    [InlineData(12)]
    [InlineData(41)]
    [InlineData(99)]
    [InlineData(123)]
    [InlineData(2024)]
    public void At_every_cursor_on_either_journal_no_unsunk_opposing_cell_reaches_the_scene(int seed)
    {
        var (id, dto, events, _) = Played(seed, shots: 500);
        var censored = Censored(events);
        var totalShots = events.OfType<ShotFired>().Count();
        Assert.True(totalShots > 0);

        for (var cursor = 0; cursor <= totalShots; cursor++)
        {
            var secret = TrueSecretAt(id, events, cursor);

            var full = ScenePlan.For(id, dto, events, cursor, lastImpact: null);
            var onCensored = ScenePlan.For(id, dto, censored, cursor, lastImpact: null);

            Assert.Empty(full.Wrecks.SelectMany(Footprint).ToHashSet().Intersect(secret));
            Assert.Empty(onCensored.Wrecks.SelectMany(Footprint).ToHashSet().Intersect(secret));
        }
    }

    [Fact]
    public void After_the_game_ends_the_whole_opposing_fleet_is_revealed()
    {
        var (id, dto, events, game) = Played(seed: 41, shots: 500);
        Assert.Equal(GameStatus.Finished, game.Status);

        var scene = ScenePlan.For(id, dto, events, cursor: null, lastImpact: null);

        Assert.True(scene.Revealed);
        Assert.Equal(
            game.OpponentBoard.Ships.Count,
            scene.Wrecks.Count);
    }

    [Fact]
    public void At_cursor_n_the_wrecks_are_those_sunk_at_that_instant()
    {
        var (id, dto, events, game) = Played(seed: 41, shots: 500);

        var sinking = game.Events.OfType<ShotFired>()
            .First(e => e.By == Player.Human && e.Result == ShotResult.Sunk);

        var shotsBefore = game.Events.OfType<ShotFired>()
            .TakeWhile(e => e.Sequence < sinking.Sequence)
            .Count();

        var before = ScenePlan.For(id, dto, events, cursor: shotsBefore, lastImpact: null);
        var after = ScenePlan.For(id, dto, events, cursor: shotsBefore + 1, lastImpact: null);

        Assert.DoesNotContain(before.Wrecks, w => w.Name == sinking.SunkShipName);
        Assert.Contains(after.Wrecks, w => w.Name == sinking.SunkShipName);
    }

    [Fact]
    public void On_a_censored_journal_the_wreck_appears_at_the_shot_that_sank_it()
    {
        var (id, dto, events, _) = Played(seed: 41, shots: 500);

        var censored = Censored(events);

        var sinking = events.OfType<ShotFired>()
            .First(e => e.By == Player.Human && e.Result == ShotResult.Sunk);

        var shotsBefore = events.OfType<ShotFired>()
            .TakeWhile(e => e.Sequence < sinking.Sequence)
            .Count();

        var before = ScenePlan.For(id, dto, censored, shotsBefore, lastImpact: null);
        var after = ScenePlan.For(id, dto, censored, shotsBefore + 1, lastImpact: null);

        Assert.DoesNotContain(before.Wrecks, w => w.Name == sinking.SunkShipName);
        Assert.Contains(after.Wrecks, w => w.Name == sinking.SunkShipName);
    }

    private static IReadOnlyList<GameEvent> Censored(IReadOnlyList<GameEvent> events) =>
    [
        .. events.Select(e => e is GameCreated created
            ? new GameCreated(created.Sequence, created.Rules, [], created.Difficulty)
            : e)
    ];

    private static HashSet<Coordinate> TrueSecretAt(Guid id, IReadOnlyList<GameEvent> events, int cursor)
    {
        var upTo = Prefix(events, cursor);
        var replayed = GameFold.Fold(id, upTo);
        var revealed = replayed.OpponentBoard.ReceivedShots;

        return replayed.OpponentBoard.Ships
            .SelectMany(s => s.Cells)
            .Where(c => !revealed.Contains(c))
            .ToHashSet();
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

    private static IEnumerable<Coordinate> Footprint(SceneShip ship) =>
        Enumerable.Range(0, ship.Size)
            .Select(i => ship.Vertical
                ? new Coordinate(ship.X, ship.Y + i)
                : new Coordinate(ship.X + i, ship.Y));
}
