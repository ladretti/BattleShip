namespace BattleShip.Models;

public readonly struct Result<T>
{
    private readonly T? _value;
    private readonly GameError? _error;

    private Result(T value)
    {
        _value = value;
        _error = null;
    }

    private Result(GameError error)
    {
        _value = default;
        _error = error;
    }

    public bool IsOk => _error == null;

    public T Value
    {
        get
        {
            if (!IsOk)
                throw new InvalidOperationException("Cannot access Value of a failed Result.");
            return _value!;
        }
    }

    public GameError Error
    {
        get
        {
            if (IsOk)
                throw new InvalidOperationException("Cannot access Error of a successful Result.");
            return _error!.Value;
        }
    }

    public static Result<T> Ok(T value) => new(value);

    public static Result<T> Fail(GameError error) => new(error);
}
