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
            new ShipTemplate("Porte-avions", 5),
            new ShipTemplate("Croiseur", 4),
            new ShipTemplate("Contre-torpilleur", 3),
            new ShipTemplate("Sous-marin", 3),
            new ShipTemplate("Torpilleur", 2)
        ],
        ShipsMayTouch: false,
        ExtraTurnOnHit: true);
}
