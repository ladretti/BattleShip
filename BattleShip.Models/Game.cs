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
    /// <summary>
    /// Joueur à qui appartient le tir suivant. Une fois <see cref="Status"/> passé à
    /// <see cref="GameStatus.Finished"/>, cette valeur n'a plus de signification
    /// stable pour désigner le vainqueur : selon <see cref="GameRules.ExtraTurnOnHit"/>,
    /// elle pointe soit vers le tireur qui vient de couler le dernier navire (la main
    /// lui est restée), soit vers son adversaire (la main a basculé sur un coup
    /// manqué). Ce n'est pas un bug — Finished bloque tout nouveau tir — mais un DTO
    /// d'état de partie ne doit pas s'appuyer dessus pour désigner le gagnant.
    /// </summary>
    public Player CurrentPlayer { get; private set; }
    public GameStatus Status { get; private set; }

    /// <summary>
    /// Journal des tirs de la partie, dans l'ordre chronologique.
    ///
    /// Attention : IReadOnlyList est une vue sur une List mutable. Un cast vers
    /// List permettrait de contourner l'ajout contrôlé fait par Fire() et de
    /// corrompre le journal. Ce risque est accepté et confiné au domaine
    /// (BattleShip.Models), comme pour Ship.HitCells.
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
