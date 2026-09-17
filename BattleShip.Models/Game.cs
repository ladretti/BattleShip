namespace BattleShip.Models;

public sealed class Game
{
    private readonly List<GameEvent> _events = [];

    public Guid Id { get; }
    public GameRules Rules { get; }

    public string OpponentDifficulty { get; }

    public Board HumanBoard { get; private set; }
    public Board OpponentBoard { get; }
    public Player CurrentPlayer { get; private set; }
    public GameStatus Status { get; private set; }
    public Player? Winner { get; private set; }

    public IReadOnlyList<GameEvent> Events => _events;

    public IReadOnlyList<ShotRecord> History =>
        [.. _events.OfType<ShotFired>()
            .Select(e => new ShotRecord(e.At, e.Result, e.By, e.SunkShipName))];

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
        var game = new Game(id, rules, humanBoard, opponentBoard, GameStatus.InProgress, opponentDifficulty);
        game.Record(seq => new GameCreated(seq, rules, Snapshots(opponentShips), opponentDifficulty));
        game.Record(seq => new HumanFleetPlaced(seq, Snapshots(humanShips)));
        return game;
    }

    public static Game Create(
    Guid id, GameRules rules, IReadOnlyList<Ship> opponentShips, string opponentDifficulty)
    {
        var humanBoard = new Board(rules.GridSize, []);
        var opponentBoard = new Board(rules.GridSize, opponentShips);
        var game = new Game(id, rules, humanBoard, opponentBoard, GameStatus.Placing, opponentDifficulty);
        game.Record(seq => new GameCreated(seq, rules, Snapshots(opponentShips), opponentDifficulty));
        return game;
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
        Record(seq => new HumanFleetPlaced(seq, Snapshots(validation.Value)));
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

        var decision = target.Decide(at);
        if (!decision.IsOk)
            return Result<ShotRecord>.Fail(decision.Error);

        target.Apply(at);
        Record(seq => new ShotFired(seq, at, shooter, decision.Value.Result, decision.Value.SunkShipName));

        if (decision.Value.Result == ShotResult.Miss || !Rules.ExtraTurnOnHit)
            CurrentPlayer = shooter == Player.Human ? Player.Opponent : Player.Human;

        if (target.AllSunk)
        {
            Status = GameStatus.Finished;
            Winner = shooter;
            Record(seq => new GameEnded(seq, shooter));
        }

        return Result<ShotRecord>.Ok(
            new ShotRecord(at, decision.Value.Result, shooter, decision.Value.SunkShipName));
    }

    private void Record(Func<int, GameEvent> build) => _events.Add(build(_events.Count));

    internal void Replay(GameEvent next)
    {
        _events.Add(next);

        switch (next)
        {
            case HumanFleetPlaced placed:
                HumanBoard = new Board(Rules.GridSize, [.. placed.Ships.Select(s => s.ToShip())]);
                Status = GameStatus.InProgress;
                break;

            case ShotFired shot:
                var target = shot.By == Player.Human ? OpponentBoard : HumanBoard;
                target.Apply(shot.At);
                if (shot.Result == ShotResult.Miss || !Rules.ExtraTurnOnHit)
                    CurrentPlayer = shot.By == Player.Human ? Player.Opponent : Player.Human;
                break;

            case GameEnded ended:
                Status = GameStatus.Finished;
                Winner = ended.Winner;
                break;

            case GameCreated:
                throw new InvalidOperationException("GameCreated can only open a journal.");

            default:
                throw new ArgumentOutOfRangeException(
                    nameof(next), next, "Unknown event type.");
        }
    }

    private static IReadOnlyList<ShipSnapshot> Snapshots(IReadOnlyList<Ship> ships) =>
        [.. ships.Select(ShipSnapshot.Of)];
}
