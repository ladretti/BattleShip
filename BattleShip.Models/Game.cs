namespace BattleShip.Models;

public sealed class Game
{
    private readonly List<ShotRecord> _history = [];

    public Guid Id { get; }
    public GameRules Rules { get; }

    public string OpponentDifficulty { get; }

    public Board HumanBoard { get; private set; }
    public Board OpponentBoard { get; }
    public Player CurrentPlayer { get; private set; }
    public GameStatus Status { get; private set; }

    public IReadOnlyList<ShotRecord> History => _history;

    private Game(
        Guid id, GameRules rules, Board humanBoard, Board opponentBoard, GameStatus status,
        string opponentDifficulty)
    {
        Id = id;
        Rules = rules;
        HumanBoard = humanBoard;
        OpponentBoard = opponentBoard;
        CurrentPlayer = Player.Human;
        Status = status;
        OpponentDifficulty = opponentDifficulty;
    }

    public static Game Start(
    Guid id, GameRules rules, IReadOnlyList<Ship> humanShips, IReadOnlyList<Ship> opponentShips,
    string opponentDifficulty = "Normal")
    {
        var humanBoard = new Board(rules.GridSize, humanShips);
        var opponentBoard = new Board(rules.GridSize, opponentShips);
        return new Game(id, rules, humanBoard, opponentBoard, GameStatus.InProgress, opponentDifficulty);
    }

    public static Game Create(
    Guid id, GameRules rules, IReadOnlyList<Ship> opponentShips, string opponentDifficulty)
    {
        var humanBoard = new Board(rules.GridSize, []);
        var opponentBoard = new Board(rules.GridSize, opponentShips);
        return new Game(id, rules, humanBoard, opponentBoard, GameStatus.Placing, opponentDifficulty);
    }

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
