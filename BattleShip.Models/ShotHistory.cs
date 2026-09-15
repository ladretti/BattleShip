namespace BattleShip.Models;

/// <summary>
/// Vue de la partie communiquée à une stratégie adverse : ses propres tirs et leurs
/// résultats, les navires restants et coulés — jamais le plateau adverse ni la
/// position des navires non découverts. C'est cette absence de référence à
/// <see cref="Board"/> qui rend l'invariant « une stratégie ne triche pas »
/// vérifiable par le type plutôt que par relecture : une stratégie ne peut
/// consulter la flotte adverse, même par erreur, puisque ShotHistory ne l'expose
/// pas.
///
/// RemainingShips et SunkShips sont en revanche des informations légitimes : ce
/// sont celles annoncées au joueur humain lorsqu'un navire coule. La frontière
/// n'est pas entre « peu » et « beaucoup » d'informations, mais entre ce que le
/// joueur a le droit de savoir et la position des navires non découverts.
/// </summary>
public sealed record ShotHistory(
    int GridSize,
    IReadOnlyList<ShotRecord> Shots,
    IReadOnlyList<ShipTemplate> RemainingShips,
    IReadOnlyList<Ship> SunkShips,
    bool ShipsMayTouch);
