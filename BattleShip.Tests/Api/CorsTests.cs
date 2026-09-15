using System.Net;
using Microsoft.AspNetCore.Mvc.Testing;

namespace BattleShip.Tests.Api;

// CORS is invisible to every other test in this project: WebApplicationFactory drives
// the pipeline directly and never goes through the browser's preflight dance, so a
// missing or misordered CORS policy would pass the whole rest of the suite and only
// break in an actual browser (task 15 brief). These two tests are the only guard.
public sealed class CorsTests : IClassFixture<WebApplicationFactory<Program>>
{
    // Matches BattleShip.App/Properties/launchSettings.json's https profile.
    private const string FrontOrigin = "https://localhost:7073";

    private readonly HttpClient _client;

    public CorsTests(WebApplicationFactory<Program> factory)
        => _client = factory.CreateClient();

    [Fact]
    public async Task A_preflight_request_from_the_front_origin_is_allowed()
    {
        var request = new HttpRequestMessage(HttpMethod.Options, "/games");
        request.Headers.Add("Origin", FrontOrigin);
        request.Headers.Add("Access-Control-Request-Method", "POST");
        request.Headers.Add("Access-Control-Request-Headers", "content-type");

        var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.True(response.Headers.TryGetValues("Access-Control-Allow-Origin", out var allowed));
        Assert.Equal(FrontOrigin, Assert.Single(allowed!));
    }

    [Fact]
    public async Task The_grpc_web_headers_are_exposed_to_the_front_origin()
    {
        // Access-Control-Expose-Headers is carried on the actual response, not on the
        // preflight one, so this must be a real (non-OPTIONS) cross-origin request.
        var request = new HttpRequestMessage(HttpMethod.Get, $"/games/{Guid.NewGuid()}");
        request.Headers.Add("Origin", FrontOrigin);

        var response = await _client.SendAsync(request);

        Assert.True(
            response.Headers.TryGetValues("Access-Control-Expose-Headers", out var exposedValues));
        var exposed = string.Join(",", exposedValues!)
            .Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);

        Assert.Contains("grpc-status", exposed);
        Assert.Contains("grpc-message", exposed);
        Assert.Contains("grpc-encoding", exposed);
        Assert.Contains("grpc-accept-encoding", exposed);
    }
}
