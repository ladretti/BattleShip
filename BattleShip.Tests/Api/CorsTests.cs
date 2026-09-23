using System.Net;
using Microsoft.AspNetCore.Mvc.Testing;

namespace BattleShip.Tests.Api;

public sealed class CorsTests : IClassFixture<WebApplicationFactory<Program>>
{
    private const string HttpsFrontOrigin = "https://localhost:7073";
    private const string HttpFrontOrigin = "http://localhost:5210";

    private readonly HttpClient _client;

    public CorsTests(WebApplicationFactory<Program> factory)
        => _client = factory.CreateClient();

    [Theory]
    [InlineData(HttpsFrontOrigin)]
    [InlineData(HttpFrontOrigin)]
    public async Task A_preflight_request_from_a_front_origin_is_allowed(string origin)
    {
        var request = new HttpRequestMessage(HttpMethod.Options, "/games");
        request.Headers.Add("Origin", origin);
        request.Headers.Add("Access-Control-Request-Method", "POST");
        request.Headers.Add("Access-Control-Request-Headers", "content-type");

        var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.True(response.Headers.TryGetValues("Access-Control-Allow-Origin", out var allowed));
        Assert.Equal(origin, Assert.Single(allowed!));
    }

    [Fact]
    public async Task An_untrusted_origin_does_not_receive_the_allow_origin_header()
    {
        var request = new HttpRequestMessage(HttpMethod.Options, "/games");
        request.Headers.Add("Origin", "http://evil.example");
        request.Headers.Add("Access-Control-Request-Method", "POST");
        request.Headers.Add("Access-Control-Request-Headers", "content-type");

        var response = await _client.SendAsync(request);

        Assert.False(
            response.Headers.Contains("Access-Control-Allow-Origin"),
            "an untrusted origin must not receive Access-Control-Allow-Origin");
    }

    [Theory]
    [InlineData(HttpsFrontOrigin)]
    [InlineData(HttpFrontOrigin)]
    public async Task The_grpc_web_headers_are_exposed_to_a_front_origin(string origin)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, $"/games/{Guid.NewGuid()}");
        request.Headers.Add("Origin", origin);

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
