namespace BattleShip.Models;

public sealed record GameRules(
    int GridSize,
    IReadOnlyList<ShipTemplate> Fleet,
    bool ShipsMayTouch,
    bool ExtraTurnOnHit)
{
    public static GameRules Default { get; } = new(
        GridSize: 10,
        Fleet:
        [
            new ShipTemplate("Carrier", 5),
            new ShipTemplate("Battleship", 4),
            new ShipTemplate("Cruiser", 3),
            new ShipTemplate("Submarine", 3),
            new ShipTemplate("Destroyer", 2)
        ],
        ShipsMayTouch: false,
        ExtraTurnOnHit: true);
}
