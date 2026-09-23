using System.Text.Json.Serialization;

namespace BattleShip.Models.Contracts;

[JsonPolymorphic(TypeDiscriminatorPropertyName = "type")]
[JsonDerivedType(typeof(GameCreatedDto), "gameCreated")]
[JsonDerivedType(typeof(HumanFleetPlacedDto), "humanFleetPlaced")]
[JsonDerivedType(typeof(ShotFiredDto), "shotFired")]
[JsonDerivedType(typeof(GameEndedDto), "gameEnded")]
public abstract record GameEventDto(int Sequence);

public sealed record GameCreatedDto(
    int Sequence, int GridSize, IReadOnlyList<ShipTemplateDto> Fleet,
    bool ShipsMayTouch, string Difficulty,
    IReadOnlyList<ShipDto> OpponentShips) : GameEventDto(Sequence);

public sealed record HumanFleetPlacedDto(
    int Sequence, IReadOnlyList<ShipDto> Ships) : GameEventDto(Sequence);

public sealed record ShotFiredDto(
    int Sequence, int X, int Y, string By,
    string Result, string? SunkShipName) : GameEventDto(Sequence);

public sealed record GameEndedDto(int Sequence, string Winner) : GameEventDto(Sequence);
