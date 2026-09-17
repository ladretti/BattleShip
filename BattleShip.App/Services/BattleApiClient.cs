using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using BattleShip.Models.Contracts;

namespace BattleShip.App.Services;

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

public sealed class BattleApiClient(HttpClient http)
{
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

    private sealed record ProblemBody(string? Title, string? Error, Dictionary<string, string[]>? Errors);

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
        }
        catch (NotSupportedException)
        {
        }

        return $"The server refused the request ({(int)response.StatusCode} {response.ReasonPhrase}).";
    }

    private static bool IsCommunicationFailure(Exception exception) =>
    exception is HttpRequestException or TaskCanceledException;

    private static string Describe(Exception exception) => exception switch
    {
        TaskCanceledException => "The server did not answer in time. Is BattleShip.API still running?",
        _ => "Could not reach the server. Check that BattleShip.API is running and that its " +
             $"certificate is trusted ({exception.Message})."
    };
}
