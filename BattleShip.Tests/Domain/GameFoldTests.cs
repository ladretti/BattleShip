using BattleShip.Models;

namespace BattleShip.Tests.Domain;

public sealed class GameFoldTests
{
    private static Game PlayedGame(int seed, int shots)
    {
        var rules = GameRules.Default;
        var human = new FleetPlacer(new Random(seed)).PlaceAll(rules).Value;
        var opponent = new FleetPlacer(new Random(seed + 1)).PlaceAll(rules).Value;
        var game = Game.Start(Guid.NewGuid(), rules, human, opponent);

        var fired = 0;
        for (var x = 0; x < rules.GridSize && fired < shots; x++)
        {
            for (var y = 0; y < rules.GridSize && fired < shots; y++)
            {
                var shooter = game.CurrentPlayer;
                var target = shooter == Player.Human ? game.OpponentBoard : game.HumanBoard;
                if (target.ReceivedShots.Contains(new Coordinate(x, y)))
                    continue;

                var result = shooter == Player.Human
                    ? game.PlayerFires(new Coordinate(x, y))
                    : game.OpponentFires(new Coordinate(x, y));

                if (result.IsOk)
                    fired++;

                if (game.Status == GameStatus.Finished)
                    return game;
            }
        }

        return game;
    }

    [Fact]
    public void The_history_is_derived_from_the_journal()
    {
        var game = PlayedGame(seed: 21, shots: 12);

        Assert.Equal(
            game.Events.OfType<ShotFired>().Count(),
            game.History.Count);
    }

    [Fact]
    public void Folding_the_whole_journal_reproduces_the_played_state()
    {
        var game = PlayedGame(seed: 21, shots: 12);

        var replayed = GameFold.Fold(game.Id, game.Events);

        Assert.Equal(game.Status, replayed.Status);
        Assert.Equal(game.CurrentPlayer, replayed.CurrentPlayer);
        Assert.Equal(game.Winner, replayed.Winner);
        Assert.Equal(game.HumanBoard.ReceivedShots, replayed.HumanBoard.ReceivedShots);
        Assert.Equal(game.OpponentBoard.ReceivedShots, replayed.OpponentBoard.ReceivedShots);
        Assert.Equal(
            game.OpponentBoard.Ships.Select(s => s.IsSunk),
            replayed.OpponentBoard.Ships.Select(s => s.IsSunk));
    }

    [Fact]
    public void Folding_the_same_prefix_twice_gives_the_same_state()
    {
        var game = PlayedGame(seed: 33, shots: 40);
        var prefix = game.Events.Take(game.Events.Count / 2).ToList();

        var first = GameFold.Fold(game.Id, prefix);
        var second = GameFold.Fold(game.Id, prefix);

        Assert.Equal(first.Status, second.Status);
        Assert.Equal(first.CurrentPlayer, second.CurrentPlayer);
        Assert.Equal(first.HumanBoard.ReceivedShots, second.HumanBoard.ReceivedShots);
        Assert.Equal(first.OpponentBoard.ReceivedShots, second.OpponentBoard.ReceivedShots);
        Assert.Equal(
            first.OpponentBoard.Ships.Select(s => s.HitCells.Count),
            second.OpponentBoard.Ships.Select(s => s.HitCells.Count));
        Assert.Equal(
            first.HumanBoard.Ships.Select(s => s.IsSunk),
            second.HumanBoard.Ships.Select(s => s.IsSunk));
    }

    [Fact]
    public void Folding_every_prefix_of_a_valid_journal_never_throws()
    {
        var game = PlayedGame(seed: 33, shots: 40);

        for (var n = 0; n <= game.Events.Count; n++)
        {
            var prefix = game.Events.Take(n).ToList();
            var exception = Record.Exception(() => GameFold.Fold(game.Id, prefix));
            Assert.Null(exception);
        }
    }

    [Fact]
    public void A_ship_is_not_reported_sunk_before_the_shot_that_sank_it()
    {
        var game = PlayedGame(seed: 33, shots: 60);

        var sinking = game.Events.OfType<ShotFired>()
            .FirstOrDefault(e => e.Result == ShotResult.Sunk);

        Assert.NotNull(sinking);

        var justBefore = GameFold.Fold(game.Id, [.. game.Events.Take(sinking.Sequence)]);
        var justAfter = GameFold.Fold(game.Id, [.. game.Events.Take(sinking.Sequence + 1)]);

        var board = sinking.By == Player.Human ? justBefore.OpponentBoard : justBefore.HumanBoard;
        var after = sinking.By == Player.Human ? justAfter.OpponentBoard : justAfter.HumanBoard;

        Assert.DoesNotContain(board.Ships, s => s.Name == sinking.SunkShipName && s.IsSunk);
        Assert.Contains(after.Ships, s => s.Name == sinking.SunkShipName && s.IsSunk);
    }
}
