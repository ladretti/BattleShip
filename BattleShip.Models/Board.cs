namespace BattleShip.Models;

/// <summary>
/// Représente la grille d'un joueur : sa flotte et les tirs reçus. C'est la seule
/// source de vérité — aucune matrice de cellules n'est construite ni mise en cache
/// ici. Une vue matricielle, si elle est nécessaire (affichage, DTO), se calcule à
/// la demande à partir de Ships et ReceivedShots, jamais en la dupliquant.
/// </summary>
public sealed class Board(int gridSize, IReadOnlyList<Ship> ships)
{
    private readonly HashSet<Coordinate> _receivedShots = new();

    public int GridSize { get; } = gridSize;

    // Copie défensive : sans elle, Board partagerait la référence de la liste passée
    // par l'appelant. Si celui-ci mute sa propre liste (retire un navire, par
    // exemple), Fire et AllSunk liraient cette mutation sur ce qui est censé être
    // l'invariant central du plateau.
    public IReadOnlyList<Ship> Ships { get; } = [.. ships];
    public IReadOnlySet<Coordinate> ReceivedShots => _receivedShots;

    public bool AllSunk => Ships.All(s => s.IsSunk);

    public Result<ShotRecord> Fire(Coordinate at, Player by)
    {
        if (IsOutOfBounds(at))
            return Result<ShotRecord>.Fail(GameError.OutOfBounds);

        if (!_receivedShots.Add(at))
            return Result<ShotRecord>.Fail(GameError.CellAlreadyShot);

        var hitShip = Ships.FirstOrDefault(s => s.TryHit(at));
        if (hitShip is null)
            return Result<ShotRecord>.Ok(new ShotRecord(at, ShotResult.Miss, by, null));

        var result = hitShip.IsSunk ? ShotResult.Sunk : ShotResult.Hit;
        var sunkShipName = hitShip.IsSunk ? hitShip.Name : null;
        return Result<ShotRecord>.Ok(new ShotRecord(at, result, by, sunkShipName));
    }

    private bool IsOutOfBounds(Coordinate at) =>
        at.X < 0 || at.X >= GridSize || at.Y < 0 || at.Y >= GridSize;
}
