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
    // Private setter: only PlaceHumanFleet replaces it, once, to move a Placing game to
    // InProgress. OpponentBoard has no such setter — the opponent fleet is placed once,
    // up front, and never replaced.
    public Board HumanBoard { get; private set; }
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

    private Game(Guid id, GameRules rules, Board humanBoard, Board opponentBoard, GameStatus status)
    {
        Id = id;
        Rules = rules;
        HumanBoard = humanBoard;
        OpponentBoard = opponentBoard;
        CurrentPlayer = Player.Human;
        Status = status;
    }

    public static Game Start(
        Guid id, GameRules rules, IReadOnlyList<Ship> humanShips, IReadOnlyList<Ship> opponentShips)
    {
        var humanBoard = new Board(rules.GridSize, humanShips);
        var opponentBoard = new Board(rules.GridSize, opponentShips);
        return new Game(id, rules, humanBoard, opponentBoard, GameStatus.InProgress);
    }

    /// <summary>
    /// Creates a game whose human fleet is not placed yet: <see cref="HumanBoard"/> starts
    /// empty and <see cref="Status"/> is <see cref="GameStatus.Placing"/>. The opponent's
    /// fleet, by contrast, is placed up front by the server (<see cref="FleetPlacer"/>)
    /// and never changes afterwards, so it is supplied here just like in
    /// <see cref="Start"/>. Call <see cref="PlaceHumanFleet"/> to complete the setup and
    /// move the game to <see cref="GameStatus.InProgress"/>.
    /// </summary>
    public static Game Create(Guid id, GameRules rules, IReadOnlyList<Ship> opponentShips)
    {
        var humanBoard = new Board(rules.GridSize, []);
        var opponentBoard = new Board(rules.GridSize, opponentShips);
        return new Game(id, rules, humanBoard, opponentBoard, GameStatus.Placing);
    }

    /// <summary>
    /// Installs the human player's fleet and moves the game from
    /// <see cref="GameStatus.Placing"/> to <see cref="GameStatus.InProgress"/>. Refuses
    /// with <see cref="GameError.InvalidPlacement"/> in two distinct cases that this
    /// single error deliberately does not distinguish between (see
    /// <see cref="PlacementRules"/>, which already collapses overlap, out-of-bounds, wrong
    /// fleet composition and adjacency into the same error): the placement itself may be
    /// illegal, or the game may no longer be in the phase that accepts one (fleet already
    /// placed, game already finished). No other <see cref="GameError"/> member describes
    /// "placement no longer possible" without being misleading: <see cref="GameError.GameAlreadyFinished"/>
    /// would be wrong for a game merely InProgress, and <see cref="GameError.GameNotStarted"/>
    /// would say the opposite of what is true here (the game HAS started, which is
    /// precisely why placing is refused).
    /// </summary>
    public Result<bool> PlaceHumanFleet(IReadOnlyList<ShipPlacement> placements)
    {
        if (Status != GameStatus.Placing)
            return Result<bool>.Fail(GameError.InvalidPlacement);

        var validation = PlacementRules.Validate(placements, Rules);
        if (!validation.IsOk)
            return Result<bool>.Fail(validation.Error);

        HumanBoard = new Board(Rules.GridSize, validation.Value);
        Status = GameStatus.InProgress;
        return Result<bool>.Ok(true);
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
