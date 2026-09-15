namespace BattleShip.Models;

/// <summary>
/// State machine of a game. PlayerFires and OpponentFires are two symmetric methods
/// that share the same firing engine (Fire): only the shooting player and the targeted
/// board change. It is through OpponentFires that the opponent plays — there is no
/// other way to make it fire.
/// </summary>
public sealed class Game
{
    private readonly List<ShotRecord> _history = [];

    public Guid Id { get; }
    public GameRules Rules { get; }
    public Board HumanBoard { get; }
    public Board OpponentBoard { get; }
    /// <summary>
    /// Player the next shot belongs to. Once <see cref="Status"/> has moved to
    /// <see cref="GameStatus.Finished"/>, this value no longer carries a stable
    /// meaning for designating the winner: depending on
    /// <see cref="GameRules.ExtraTurnOnHit"/>, it points either to the shooter who has
    /// just sunk the last ship (the turn stayed with them), or to their opponent (the
    /// turn switched on a missed shot). This is not a bug — Finished blocks any new
    /// shot — but a game state DTO must not rely on it to designate the winner.
    /// </summary>
    public Player CurrentPlayer { get; private set; }
    public GameStatus Status { get; private set; }

    /// <summary>
    /// Log of the game's shots, in chronological order.
    ///
    /// Careful: IReadOnlyList is a view over a mutable List. A cast to List would
    /// make it possible to bypass the controlled insertion done by Fire() and to
    /// corrupt the log. This risk is accepted and confined to the domain
    /// (BattleShip.Models), as it is for Ship.HitCells.
    /// </summary>
    public IReadOnlyList<ShotRecord> History => _history;

    private Game(Guid id, GameRules rules, Board humanBoard, Board opponentBoard)
    {
        Id = id;
        Rules = rules;
        HumanBoard = humanBoard;
        OpponentBoard = opponentBoard;
        CurrentPlayer = Player.Human;
        Status = GameStatus.InProgress;
    }

    public static Game Start(
        Guid id, GameRules rules, IReadOnlyList<Ship> humanShips, IReadOnlyList<Ship> opponentShips)
    {
        var humanBoard = new Board(rules.GridSize, humanShips);
        var opponentBoard = new Board(rules.GridSize, opponentShips);
        return new Game(id, rules, humanBoard, opponentBoard);
    }

    public Result<ShotRecord> PlayerFires(Coordinate at) =>
        Fire(at, Player.Human, OpponentBoard);

    public Result<ShotRecord> OpponentFires(Coordinate at) =>
        Fire(at, Player.Opponent, HumanBoard);

    private Result<ShotRecord> Fire(Coordinate at, Player shooter, Board target)
    {
        if (Status == GameStatus.Finished)
            return Result<ShotRecord>.Fail(GameError.GameAlreadyFinished);

        if (Status != GameStatus.InProgress)
            return Result<ShotRecord>.Fail(GameError.GameNotStarted);

        if (CurrentPlayer != shooter)
            return Result<ShotRecord>.Fail(GameError.NotYourTurn);

        var result = target.Fire(at, shooter);
        if (!result.IsOk)
            return result;

        _history.Add(result.Value);

        if (result.Value.Result == ShotResult.Miss || !Rules.ExtraTurnOnHit)
            CurrentPlayer = shooter == Player.Human ? Player.Opponent : Player.Human;

        if (target.AllSunk)
            Status = GameStatus.Finished;

        return result;
    }
}
