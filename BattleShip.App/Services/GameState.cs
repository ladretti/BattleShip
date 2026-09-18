using BattleShip.Models.Contracts;

namespace BattleShip.App.Services;

public enum LoadState
{
    Idle,
    Loading,
    Ready,
    Failed
}

public sealed class GameState(BattleApiClient api, GameSessionStorage session)
{
    public GameDto? Current { get; private set; }

    public LoadState State { get; private set; } = LoadState.Idle;

    public string? ErrorMessage { get; private set; }

    public event Action? OnChange;

    public async Task InitializeAsync()
    {
        if (Current is not null)
            return;

        var id = await session.ReadAsync();
        if (id is null)
            return;

        var result = await api.GetGameAsync(id.Value);
        if (!result.IsOk)
        {
            await session.ClearAsync();
            return;
        }

        if (Current is not null)
            return;

        await SetCurrentAsync(result.Value);
    }

    public async Task CreateAsync(int gridSize, string difficulty)
    {
        Transition(LoadState.Loading, null);

        var result = await api.CreateGameAsync(gridSize, difficulty);

        if (!result.IsOk)
        {
            Transition(LoadState.Failed, result.ErrorMessage);
            return;
        }

        await SetCurrentAsync(result.Value);
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

        await SetCurrentAsync(result.Value);
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

        await SetCurrentAsync(reread.Value);
    }

    private async Task SetCurrentAsync(GameDto game)
    {
        Current = game;

        if (game.Status == "Finished")
            await session.ClearAsync();
        else
            await session.SaveAsync(game.Id);

        Transition(LoadState.Ready, null);
    }

    private void Transition(LoadState state, string? errorMessage)
    {
        State = state;
        ErrorMessage = errorMessage;
        OnChange?.Invoke();
    }
}
