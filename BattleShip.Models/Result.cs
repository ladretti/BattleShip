namespace BattleShip.Models;

public readonly struct Result<T>
{
    private readonly bool _isOk;
    private readonly T? _value;
    private readonly GameError? _error;

    private Result(bool isOk, T? value, GameError? error)
    {
        _isOk = isOk;
        _value = value;
        _error = error;
    }

    public bool IsOk => _isOk;

    public T Value
    {
        get
        {
            if (!_isOk)
                throw new InvalidOperationException("Cannot access Value of a failed Result.");
            return _value!;
        }
    }

    public GameError Error
    {
        get
        {
            if (_isOk)
                throw new InvalidOperationException("Cannot access Error of a successful Result.");
            if (_error == null)
                throw new InvalidOperationException("Result has no error information.");
            return _error.Value;
        }
    }

    public static Result<T> Ok(T value) => new(true, value, null);

    public static Result<T> Fail(GameError error) => new(false, default, error);
}
