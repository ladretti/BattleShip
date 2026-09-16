using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using BattleShip.Models.Contracts;

namespace BattleShip.App.Services;

/// <summary>
/// The outcome of one call to the API, from the front's point of view.
///
/// This is deliberately NOT <see cref="BattleShip.Models.Result{T}"/>. That type carries a
/// <c>GameError</c> — a business refusal decided by the engine — whereas a call from the
/// browser can also fail for reasons the engine knows nothing about: the API is down, the
/// certificate is not trusted, CORS blocked the response, the body could not be parsed.
/// Forcing those into a <c>GameError</c> would mean inventing an engine refusal that never
/// happened. So the failure is carried as a message meant to be shown to the player, and
/// the two registers stay distinct (ADR 0004 applied to the client side).
/// </summary>
public readonly record struct ApiResult<T>
{
    private readonly T? _value;

    private ApiResult(bool isOk, T? value, string? errorMessage)
    {
        IsOk = isOk;
        _value = value;
        ErrorMessage = errorMessage;
    }

    public bool IsOk { get; }

    public string? ErrorMessage { get; }

    public T Value => IsOk
        ? _value!
        : throw new InvalidOperationException("Cannot access Value of a failed ApiResult.");

    public static ApiResult<T> Ok(T value) => new(true, value, null);

    public static ApiResult<T> Fail(string errorMessage) => new(false, default, errorMessage);
}

/// <summary>
/// Every HTTP call the front makes, in one place. Firing is absent on purpose: it goes
/// exclusively through gRPC-Web (ADR 0005), and lands in <c>BattleGrpcClient</c> at task 18.
///
/// No method here throws for a failure the player could plausibly cause or witness — a
/// stopped API, an unknown game, a refused placement. They all come back as a failed
/// <see cref="ApiResult{T}"/> carrying a displayable message, so that a communication
/// breakdown produces an error state on the page rather than an unhandled exception that
/// freezes the UI (CLAUDE.md § 3, Blazor: the three states are mandatory). Nothing is
/// swallowed: every caught exception becomes a message the player actually sees.
///
/// This client holds NO game rule. It sends what the player asked for and reports what the
/// server answered; the server decides (ADR 0007).
/// </summary>
public sealed class BattleApiClient(HttpClient http)
{
    /// <summary>
    /// POST /games — expects 201 with the created game. A 400 can only mean the grid size
    /// or the difficulty was outside what <c>CreateGameInputValidator</c> accepts; the page
    /// offers nothing else, so it would signal a drift between the two, and the server's
    /// own wording is surfaced rather than a guess.
    /// </summary>
    public async Task<ApiResult<GameDto>> CreateGameAsync(
        int gridSize, string difficulty, CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await http.PostAsJsonAsync(
                "games", new CreateGameInput(gridSize, difficulty), cancellationToken);

            if (response.StatusCode != HttpStatusCode.Created)
                return ApiResult<GameDto>.Fail(await DescribeFailureAsync(response, cancellationToken));

            var game = await response.Content.ReadFromJsonAsync<GameDto>(cancellationToken);
            return game is null
                ? ApiResult<GameDto>.Fail("The server created the game but returned an empty body.")
                : ApiResult<GameDto>.Ok(game);
        }
        catch (Exception exception) when (IsCommunicationFailure(exception))
        {
            return ApiResult<GameDto>.Fail(Describe(exception));
        }
    }

    /// <summary>GET /games/{id} — expects 200 with the game as this player may see it, or 404.</summary>
    public async Task<ApiResult<GameDto>> GetGameAsync(
        Guid id, CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await http.GetAsync($"games/{id}", cancellationToken);

            if (response.StatusCode == HttpStatusCode.NotFound)
                return ApiResult<GameDto>.Fail($"Game {id} no longer exists on the server.");

            if (!response.IsSuccessStatusCode)
                return ApiResult<GameDto>.Fail(await DescribeFailureAsync(response, cancellationToken));

            var game = await response.Content.ReadFromJsonAsync<GameDto>(cancellationToken);
            return game is null
                ? ApiResult<GameDto>.Fail("The server returned an empty game.")
                : ApiResult<GameDto>.Ok(game);
        }
        catch (Exception exception) when (IsCommunicationFailure(exception))
        {
            return ApiResult<GameDto>.Fail(Describe(exception));
        }
    }

    /// <summary>
    /// POST /games/{id}/placement — expects 204. A 400 is the point of this call as much as
    /// the 204 is: it is how the server refuses an overlapping, out-of-bounds or adjacent
    /// fleet, and its wording is handed back untouched so the page can show the reason the
    /// server actually gave rather than a guess made here.
    /// </summary>
    public async Task<ApiResult<bool>> PlaceFleetAsync(
        Guid id, IReadOnlyList<ShipPlacementInput> ships, CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await http.PostAsJsonAsync(
                $"games/{id}/placement", new PlacementInput(ships), cancellationToken);

            if (response.StatusCode == HttpStatusCode.NoContent)
                return ApiResult<bool>.Ok(true);

            if (response.StatusCode == HttpStatusCode.NotFound)
                return ApiResult<bool>.Fail($"Game {id} no longer exists on the server.");

            return ApiResult<bool>.Fail(await DescribeFailureAsync(response, cancellationToken));
        }
        catch (Exception exception) when (IsCommunicationFailure(exception))
        {
            return ApiResult<bool>.Fail(Describe(exception));
        }
    }

    /// <summary>
    /// GET /games/{id}/history — every shot of the game, both sides, in the order they were
    /// played. Not secret: it only reports shots that were actually fired and their outcome,
    /// which is precisely what both players already witnessed.
    /// </summary>
    public async Task<ApiResult<IReadOnlyList<ShotDto>>> GetHistoryAsync(
        Guid id, CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await http.GetAsync($"games/{id}/history", cancellationToken);

            if (response.StatusCode == HttpStatusCode.NotFound)
                return ApiResult<IReadOnlyList<ShotDto>>.Fail($"Game {id} no longer exists on the server.");

            if (!response.IsSuccessStatusCode)
                return ApiResult<IReadOnlyList<ShotDto>>.Fail(
                    await DescribeFailureAsync(response, cancellationToken));

            var history = await response.Content
                .ReadFromJsonAsync<List<ShotDto>>(cancellationToken);

            return ApiResult<IReadOnlyList<ShotDto>>.Ok(history ?? []);
        }
        catch (Exception exception) when (IsCommunicationFailure(exception))
        {
            return ApiResult<IReadOnlyList<ShotDto>>.Fail(Describe(exception));
        }
    }

    /// <summary>
    /// The two shapes an error body can take on this API, read leniently: either the
    /// <c>ValidationProblemDetails</c> that <c>TypedResults.ValidationProblem</c> produces
    /// (<c>errors</c>, a field name to its messages), or the <c>{ "error": "..." }</c>
    /// object <c>GameEndpoints.ToProblem</c> returns for a refused placement. Both
    /// properties are nullable because any given response carries only one of them.
    /// </summary>
    private sealed record ProblemBody(string? Title, string? Error, Dictionary<string, string[]>? Errors);

    /// <summary>
    /// Turns a non-success response into something worth showing. The server's own wording
    /// is preferred over anything invented here: a validation message names the field that
    /// was refused, which is what lets the player fix it. The status code is the fallback
    /// when the body carries nothing usable — never the first choice, because "400" alone
    /// tells the player nothing.
    /// </summary>
    private static async Task<string> DescribeFailureAsync(
        HttpResponseMessage response, CancellationToken cancellationToken)
    {
        try
        {
            var problem = await response.Content.ReadFromJsonAsync<ProblemBody>(cancellationToken);

            if (problem?.Errors is { Count: > 0 } errors)
            {
                var messages = errors.SelectMany(entry => entry.Value).Where(m => !string.IsNullOrWhiteSpace(m));
                return $"The server refused the request: {string.Join(" ", messages)}";
            }

            if (!string.IsNullOrWhiteSpace(problem?.Error))
                return $"The server refused the request: {problem.Error}";

            if (!string.IsNullOrWhiteSpace(problem?.Title))
                return $"The server refused the request: {problem.Title}";
        }
        catch (JsonException)
        {
            // The body was not one of the two known shapes. That is not worth failing over:
            // the status code below still tells the player the request did not go through.
        }
        catch (NotSupportedException)
        {
            // Same, for a body whose content type is not JSON at all.
        }

        return $"The server refused the request ({(int)response.StatusCode} {response.ReasonPhrase}).";
    }

    /// <summary>
    /// The failures that mean "the call did not complete", as opposed to a bug in this
    /// front. <see cref="HttpRequestException"/> covers a stopped API, a refused connection,
    /// an untrusted certificate and a CORS rejection alike — in the browser, the fetch API
    /// deliberately hides which one it was, so no finer message is honestly available here.
    /// <see cref="TaskCanceledException"/> covers the timeout and a navigation that abandons
    /// the call in flight.
    ///
    /// Anything else — a <see cref="NullReferenceException"/>, say — is an anomaly in this
    /// code and is left to propagate (ADR 0004): turning it into a polite message on screen
    /// would hide a defect instead of reporting it.
    /// </summary>
    private static bool IsCommunicationFailure(Exception exception) =>
        exception is HttpRequestException or TaskCanceledException;

    private static string Describe(Exception exception) => exception switch
    {
        TaskCanceledException => "The server did not answer in time. Is BattleShip.API still running?",
        _ => "Could not reach the server. Check that BattleShip.API is running and that its " +
             $"certificate is trusted ({exception.Message})."
    };
}
