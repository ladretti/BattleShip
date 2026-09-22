namespace BattleShip.Models;

public sealed record SceneShip(string Name, int X, int Y, int Size, bool Vertical, bool Sunk);

public sealed record SceneState(
    int GridSize,
    IReadOnlyList<SceneShip> Friendly,
    IReadOnlyList<SceneShip> Wrecks,
    Coordinate? LastImpact,
    bool Revealed);
