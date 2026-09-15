namespace BattleShip.Models;

using System.Collections.Frozen;

public sealed class Ship(string name, int size, IEnumerable<Coordinate> cells)
{
    public string Name { get; } = name;
    public int Size { get; } = size;
    public IReadOnlySet<Coordinate> Cells { get; } = cells.ToFrozenSet();

    private readonly HashSet<Coordinate> _hitCells = new();

    /// <summary>
    /// Cases du navire qui ont été touchées. Toute mutation de cet ensemble passe
    /// exclusivement par TryHit(), qui garantit que seules les cases valides sont
    /// ajoutées et qu'une case ne peut être touchée qu'une fois.
    ///
    /// Attention : IReadOnlySet est une vue sur un HashSet mutable. Un cast vers
    /// HashSet permettrait de contourner TryHit() et violer les invariants. Ce
    /// risque est accepté et confiné au domaine (BattleShip.Models). Contrairement
    /// à Cells qui est un FrozenSet réellement immuable, cette asymétrie reflète
    /// une décision : HitCells a besoin de mutations fréquentes pendant le jeu,
    /// tandis que Cells est stable à la création du navire.
    /// </summary>
    public IReadOnlySet<Coordinate> HitCells => _hitCells;

    public bool IsSunk => _hitCells.Count == Size;

    public bool TryHit(Coordinate at)
    {
        if (!Cells.Contains(at))
            return false;

        return _hitCells.Add(at);
    }
}
