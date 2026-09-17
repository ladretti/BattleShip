using BattleShip.Models;

namespace BattleShip.Tests.Domain;

public sealed class GameTests
{
    private static Game TinyGame()
    {
        var rules = new GameRules(3, [new ShipTemplate("Destroyer", 2)], false, true);
        var fleet = () => new List<Ship>
        {
            new("Destroyer", 2, [new Coordinate(0, 0), new Coordinate(1, 0)])
        };
        return Game.Start(Guid.NewGuid(), rules, fleet(), fleet());
    }

    [Fact]
    public void A_shot_outside_the_grid_is_rejected()
    {
        var game = TinyGame();

        var result = game.PlayerFires(new Coordinate(3, 0));

        Assert.False(result.IsOk);
        Assert.Equal(GameError.OutOfBounds, result.Error);
    }

    [Fact]
    public void A_shot_on_an_already_shot_cell_is_rejected()
    {
        var game = TinyGame();
        game.PlayerFires(new Coordinate(0, 0));

        var result = game.PlayerFires(new Coordinate(0, 0));

        Assert.False(result.IsOk);
        Assert.Equal(GameError.CellAlreadyShot, result.Error);
    }

    [Fact]
    public void The_opponent_cannot_fire_when_it_is_the_player_s_turn()
    {
        var game = TinyGame();

        var result = game.OpponentFires(new Coordinate(0, 0));

        Assert.False(result.IsOk);
        Assert.Equal(GameError.NotYourTurn, result.Error);
    }

    [Fact]
    public void A_missed_shot_by_the_opponent_gives_the_turn_back_to_the_player()
    {
        var game = TinyGame();
        game.PlayerFires(new Coordinate(2, 2));

        var result = game.OpponentFires(new Coordinate(2, 2));

        Assert.True(result.IsOk);
        Assert.Equal(ShotResult.Miss, result.Value.Result);
        Assert.Equal(Player.Human, game.CurrentPlayer);
    }

    [Fact]
    public void A_shot_on_the_last_cell_of_a_ship_sinks_it()
    {
        var game = TinyGame();
        game.PlayerFires(new Coordinate(0, 0));

        var result = game.PlayerFires(new Coordinate(1, 0));

        Assert.Equal(ShotResult.Sunk, result.Value.Result);
        Assert.Equal("Destroyer", result.Value.SunkShipName);
    }

    [Fact]
    public void Sinking_the_last_ship_finishes_the_game()
    {
        var game = TinyGame();
        game.PlayerFires(new Coordinate(0, 0));
        game.PlayerFires(new Coordinate(1, 0));

        Assert.Equal(GameStatus.Finished, game.Status);
    }

    [Fact]
    public void A_shot_after_the_end_of_the_game_is_rejected()
    {
        var game = TinyGame();
        game.PlayerFires(new Coordinate(0, 0));
        game.PlayerFires(new Coordinate(1, 0));

        var result = game.PlayerFires(new Coordinate(2, 2));

        Assert.False(result.IsOk);
        Assert.Equal(GameError.GameAlreadyFinished, result.Error);
    }

    [Fact]
    public void A_hit_leaves_the_turn_to_the_player()
    {
        var game = TinyGame();

        game.PlayerFires(new Coordinate(0, 0));

        Assert.Equal(Player.Human, game.CurrentPlayer);
    }

    [Fact]
    public void A_missed_shot_passes_the_turn_to_the_opponent()
    {
        var game = TinyGame();

        game.PlayerFires(new Coordinate(2, 2));

        Assert.Equal(Player.Opponent, game.CurrentPlayer);
    }

    [Fact]
    public void A_player_shot_when_it_is_not_their_turn_is_rejected()
    {
        var game = TinyGame();
        game.PlayerFires(new Coordinate(2, 2));

        var result = game.PlayerFires(new Coordinate(2, 1));

        Assert.False(result.IsOk);
        Assert.Equal(GameError.NotYourTurn, result.Error);
    }

    [Fact]
    public void A_shot_on_a_game_that_has_not_started_is_rejected()
    {
        var game = TinyGame();
        typeof(Game).GetProperty(nameof(Game.Status))!
            .SetValue(game, GameStatus.Placing);

        var result = game.PlayerFires(new Coordinate(0, 0));

        Assert.False(result.IsOk);
        Assert.Equal(GameError.GameNotStarted, result.Error);
    }

    [Fact]
    public void An_unfinished_game_has_no_winner()
    {
        var game = TinyGame();

        game.PlayerFires(new Coordinate(0, 0));

        Assert.Equal(GameStatus.InProgress, game.Status);
        Assert.Null(game.Winner);
    }

    [Fact]
    public void The_shooter_who_sinks_the_last_ship_is_the_winner()
    {
        var game = TinyGame();
        game.PlayerFires(new Coordinate(0, 0));

        game.PlayerFires(new Coordinate(1, 0));

        Assert.Equal(GameStatus.Finished, game.Status);
        Assert.Equal(Player.Human, game.Winner);
    }
}
