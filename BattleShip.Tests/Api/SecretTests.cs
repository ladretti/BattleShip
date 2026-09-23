using BattleShip.API.Contracts;
using BattleShip.Models.Contracts;
using BattleShip.Models;

namespace BattleShip.Tests.Api;

public sealed class SecretTests
{
    [Fact]
    public void The_dto_reveals_no_undiscovered_opposing_cell()
    {
        var rules = GameRules.Default;
        var playerFleet = new FleetPlacer(new Random(1)).PlaceAll(rules).Value;
        var opponentFleet = new FleetPlacer(new Random(2)).PlaceAll(rules).Value;
        var game = Game.Start(Guid.NewGuid(), rules, playerFleet, opponentFleet);

        game.PlayerFires(new Coordinate(0, 0));

        var dto = game.ToDto();

        var exposedByOpponentBoard = dto.Opponent.Shots
            .Concat(dto.Opponent.SunkShips.SelectMany(s => s.Cells))
            .Select(c => new Coordinate(c.X, c.Y))
            .ToHashSet();

        var revealed = game.OpponentBoard.ReceivedShots;
        var secretCells = opponentFleet
            .SelectMany(s => s.Cells)
            .Where(c => !revealed.Contains(c))
            .ToList();

        Assert.NotEmpty(secretCells);
        Assert.Empty(exposedByOpponentBoard.Intersect(secretCells));
    }

    [Fact]
    public void The_dto_exposes_the_player_s_fleet()
    {
        var rules = GameRules.Default;
        var playerFleet = new FleetPlacer(new Random(1)).PlaceAll(rules).Value;
        var game = Game.Start(Guid.NewGuid(), rules, playerFleet,
            new FleetPlacer(new Random(2)).PlaceAll(rules).Value);

        var dto = game.ToDto();

        Assert.Equal(rules.Fleet.Count, dto.Own.Ships.Count);
    }

    [Fact]
    public void The_own_board_is_the_player_board_and_not_the_opponent_one()
    {
        var rules = GameRules.Default;
        var playerFleet = new FleetPlacer(new Random(1)).PlaceAll(rules).Value;
        var opponentFleet = new FleetPlacer(new Random(2)).PlaceAll(rules).Value;
        var game = Game.Start(Guid.NewGuid(), rules, playerFleet, opponentFleet);

        var ownCells = game.ToDto().Own.Ships
            .SelectMany(s => s.Cells)
            .Select(c => new Coordinate(c.X, c.Y))
            .ToHashSet();

        var playerCells = playerFleet.SelectMany(s => s.Cells).ToHashSet();

        Assert.Equal(playerCells, ownCells);
    }
}
