using System.Text.Json;
using BattleShip.API.Contracts;
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

        var json = JsonSerializer.Serialize(game.ToDto());

        var revealed = game.OpponentBoard.ReceivedShots;
        var secretCells = opponentFleet
            .SelectMany(s => s.Cells)
            .Where(c => !revealed.Contains(c))
            .ToList();

        Assert.NotEmpty(secretCells);   // otherwise the test would prove nothing

        foreach (var c in secretCells)
            Assert.DoesNotContain($"\"x\":{c.X},\"y\":{c.Y}",
                json, StringComparison.OrdinalIgnoreCase);
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
}
