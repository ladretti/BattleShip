using BattleShip.Models.Contracts;

namespace BattleShip.App.Services;

public enum LoadState
{
    Idle,
    Loading,
    Ready,
    Failed
}

public sealed class GameState(BattleApiClient api)
{
    public GameDto? Current { get; private set; }

    public LoadState State { get; private set; } = LoadState.Idle;

    public string? ErrorMessage { get; private set; }

    public event Action? OnChange;

    public async Task CreateAsync(int gridSize, string difficulty)
    {
        Transition(LoadState.Loading, null);

        var result = await api.CreateGameAsync(gridSize, difficulty);

        if (!result.IsOk)
        {
            Transition(LoadState.Failed, result.ErrorMessage);
            return;
        }

        Current = result.Value;
        Transition(LoadState.Ready, null);
    }

    public async Task RefreshAsync()
    {
        if (Current is null)
            return;

        var id = Current.Id;
        Transition(LoadState.Loading, null);

        var result = await api.GetGameAsync(id);

        if (!result.IsOk)
        {
            Transition(LoadState.Failed, result.ErrorMessage);
            return;
        }

        Current = result.Value;
        Transition(LoadState.Ready, null);
    }

    public async Task PlaceFleetAsync(IReadOnlyList<ShipPlacementInput> ships)
    {
        if (Current is null)
        {
            Transition(LoadState.Failed, "No game to place a fleet on. Create one first.");
            return;
        }

        var id = Current.Id;
        Transition(LoadState.Loading, null);

        var placement = await api.PlaceFleetAsync(id, ships);
        if (!placement.IsOk)
        {
            Transition(LoadState.Failed, placement.ErrorMessage);
            return;
        }

        var reread = await api.GetGameAsync(id);
        if (!reread.IsOk)
        {
            Transition(LoadState.Failed,
                $"The fleet was accepted, but reading the game back failed: {reread.ErrorMessage}");
            return;
        }

        Current = reread.Value;
        Transition(LoadState.Ready, null);
    }

    private void Transition(LoadState state, string? errorMessage)
    {
        State = state;
        ErrorMessage = errorMessage;
        OnChange?.Invoke();
    }
}
