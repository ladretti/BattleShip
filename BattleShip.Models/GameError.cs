namespace BattleShip.Models;

public enum GameError
{
    GameNotFound, OutOfBounds, CellAlreadyShot,
    GameAlreadyFinished, NotYourTurn, InvalidPlacement
}
