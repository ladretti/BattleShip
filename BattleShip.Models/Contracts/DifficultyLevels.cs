namespace BattleShip.Models.Contracts;

/// <summary>
/// The single source of truth for the three accepted difficulty names. It sits in
/// <c>Contracts</c> rather than beside the strategies because it is exactly that: a
/// contract, shared by the three parties that must agree on these strings and unable to
/// drift as long as all three read it here (ADR 0008).
///
/// <list type="bullet">
/// <item><c>CreateGameInputValidator</c> rejects, with a 400, any value outside
/// <see cref="All"/>.</item>
/// <item><c>OpponentStrategyFactory</c> maps each of these three names to an
/// <see cref="IOpponentStrategy"/>, and treats anything else as an anomaly — the
/// validator having already made it unreachable.</item>
/// <item><c>NewGame.razor</c> renders <see cref="All"/> as the choices offered to the
/// player, instead of hard-coding three strings the server might later stop
/// accepting.</item>
/// </list>
///
/// Only the NAMES are shared. The behavior behind each one — which strategy, how it
/// aims — stays in <c>BattleShip.API/Strategies/</c>, out of the front's reach: the
/// player picks a level, never an algorithm.
/// </summary>
public static class DifficultyLevels
{
    public const string Easy = "Easy";
    public const string Normal = "Normal";
    public const string Hard = "Hard";

    public static readonly IReadOnlyList<string> All = [Easy, Normal, Hard];
}
