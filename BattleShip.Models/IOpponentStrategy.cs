namespace BattleShip.Models;

public interface IOpponentStrategy
{
    string Name { get; }

    Coordinate NextShot(ShotHistory history);
}
