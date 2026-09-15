namespace BattleShip.Models;

/// <summary>
/// Machine à états d'une partie. PlayerFires et OpponentFires sont deux méthodes
/// symétriques qui partagent le même moteur de tir (Fire) : seuls le joueur tireur
/// et le plateau visé changent. C'est par OpponentFires que l'adversaire joue — il
/// n'existe aucune autre voie pour le faire tirer.
/// </summary>
public sealed class Game
{
    private readonly List<ShotRecord> _history = [];

    public Guid Id { get; }
    public GameRules Rules { get; }
    public Board HumanBoard { get; }
    public Board OpponentBoard { get; }
    public Player CurrentPlayer { get; private set; }
    public GameStatus Status { get; private set; }
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
