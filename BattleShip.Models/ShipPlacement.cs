namespace BattleShip.Models;

public sealed record ShipPlacement(string Name, Coordinate Origin, Orientation Orientation, int Size)
{
    public IEnumerable<Coordinate> Cells()
    {
        for (var i = 0; i < Size; i++)
        {
            yield return Orientation switch
            {
                Orientation.Horizontal => Origin with { X = Origin.X + i },
                Orientation.Vertical => Origin with { Y = Origin.Y + i },
                _ => throw new ArgumentOutOfRangeException(nameof(Orientation))
            };
        }
    }
}
