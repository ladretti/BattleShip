namespace BattleShip.Models;

/// <summary>
/// A replaceable opponent strategy, not a private method of the engine (ADR 0003).
/// Each implementation receives only what the player is entitled to know — through
/// <see cref="ShotHistory"/> — and must respect the invariant checked by
/// StrategyInvariantTests: never propose a shot outside the grid, nor a cell already
/// shot.
/// </summary>
public interface IOpponentStrategy
{
    string Name { get; }

    /// <summary>
    /// Proposes the next cell to shoot at, based on <paramref name="history"/>.
    ///
    /// Precondition: at least one playable cell remains in the grid. An
    /// implementation that enumerates a set of candidates (hunt, target...) has no
    /// cell left to choose from if all of them have already been shot, and may then
    /// throw an exception instead of returning a valid <see cref="Coordinate"/> — it
    /// is up to the caller never to invoke <see cref="NextShot"/> on a full grid or on
    /// an already finished game, never up to the implementation to check it.
    ///
    /// This contract holds by construction as long as the engine only makes the
    /// opponent fire on a game whose status is InProgress: a game ends as soon as the
    /// opposing fleet is sunk, which happens strictly before the grid is exhausted (a
    /// 10x10 grid always contains more cells than the fleet occupies). The
    /// precondition is therefore never checked here — were it checked, it would force
    /// every strategy to handle a case the engine never produces — but it must remain
    /// true for any future implementation of <see cref="IOpponentStrategy"/> wired to
    /// the server.
    /// </summary>
    Coordinate NextShot(ShotHistory history);
}
