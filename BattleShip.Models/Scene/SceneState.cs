namespace BattleShip.Models;

public sealed record SceneShip(string Name, int X, int Y, int Size, bool Vertical, bool Sunk);

public sealed record SceneCell(int X, int Y, string State);

public sealed record SceneSide(IReadOnlyList<SceneShip> Ships, IReadOnlyList<SceneCell> Cells);

public sealed record SceneState(
    int GridSize,
    SceneSide Own,
    SceneSide Opponent,
    Coordinate? LastImpact,
    bool Revealed);
