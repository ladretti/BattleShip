using BattleShip.Models.Contracts;

namespace BattleShip.App.Services;

/// <summary>
/// The three states every page must render (CLAUDE.md § 3, Blazor), named once here so no
/// page has to invent its own booleans for them. <see cref="Idle"/> is the fourth, and it is
/// not a state of a request: it means no game has been asked for yet.
/// </summary>
public enum LoadState
{
    Idle,
    Loading,
    Ready,
    Failed
}

/// <summary>
/// Holds the current game across the three pages — new game, placement, play — and notifies
/// subscribers when it changes (ADR 0007). Registered in the DI container so that navigating
/// between pages does not mean re-fetching the game from the server, nor cramming the whole
/// journey into a single component.
///
/// <para><b>It holds no game rule.</b> Every property below is what the server last
/// answered, never something computed here. The day a rule lives in this class is the day
/// the front and the server can disagree about who won — and only one of them is
/// authoritative (ADR 0007).</para>
///
/// <para>Nor does it ever reconstruct the opponent's fleet: it stores a
/// <see cref="GameDto"/>, whose <c>Opponent</c> board has no property able to hold an
/// afloat opposing ship in the first place (ADR 0001, the secrecy rule).</para>
///
/// <para>Failure is a state, not an exception: <see cref="State"/> goes to
/// <see cref="LoadState.Failed"/> with an <see cref="ErrorMessage"/>, and
/// <see cref="Current"/> keeps the last known game so the page stays readable while the API
/// is unreachable.</para>
/// </summary>
public sealed class GameState(BattleApiClient api)
{
    /// <summary>
    /// The current game as this player is entitled to see it, or <c>null</c> if none has
    /// been created yet. Kept as-is through a failure, so an interrupted connection does not
    /// wipe the board off the screen.
    /// </summary>
    public GameDto? Current { get; private set; }

    public LoadState State { get; private set; } = LoadState.Idle;

    /// <summary>Set exactly when <see cref="State"/> is <see cref="LoadState.Failed"/>.</summary>
    public string? ErrorMessage { get; private set; }

    /// <summary>
    /// Raised after every transition. Subscribers MUST unsubscribe in <c>Dispose</c>:
    /// this service outlives the pages, so a forgotten unsubscription keeps a destroyed
    /// component alive and re-renders it (ADR 0007).
    /// </summary>
    public event Action? OnChange;

    /// <summary>
    /// Creates a game on the server and adopts it as the current one. On failure the
    /// previous game — if any — is left untouched: a refused creation must not destroy a
    /// game in progress.
    /// </summary>
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

    /// <summary>
    /// Re-reads the current game from the server, which is the only authority on its state.
    /// Does nothing when no game has been created yet — refreshing nothing is not a failure,
    /// so this leaves <see cref="State"/> alone rather than reporting an error the player
    /// could do nothing about.
    /// </summary>
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

    /// <summary>
    /// Submits the human fleet, then re-reads the game from the server rather than guessing
    /// what accepting it changed. That second round trip is the point: the server is the one
    /// that turns the game from Placing to InProgress and that decides which cells the ships
    /// ended up on, and this class is not allowed to decide either (ADR 0007).
    ///
    /// A refusal leaves <see cref="Current"/> on the game as it was — still in Placing — so
    /// the page can show the server's reason and let the player move a ship and try again.
    /// </summary>
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
            // The fleet WAS accepted; only reading the result back failed. Saying so keeps
            // the player from re-submitting a placement the server has already taken.
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
