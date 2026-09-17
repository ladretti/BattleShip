namespace BattleShip.Models.Contracts;

public static class DifficultyLevels
{
    public const string Easy = "Easy";
    public const string Normal = "Normal";
    public const string Hard = "Hard";

    public static readonly IReadOnlyList<string> All = [Easy, Normal, Hard];
}
