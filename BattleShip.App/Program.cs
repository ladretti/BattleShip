using BattleShip.API.Grpc;
using BattleShip.App;
using BattleShip.App.Services;
using Grpc.Net.Client;
using Grpc.Net.Client.Web;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

// The API is a different origin from this front (different port), so the HttpClient points
// at it rather than at the host environment's base address, and the API declares this origin
// in its CORS policy.
//
// The address MUST match the scheme this front is itself served on, and pinning a single one
// was a real defect: launched on its http profile, the front called the https API, which the
// API's own http profile never starts — the call never reached a server at all, and the
// browser reports that as "CORS request did not succeed, status (null)", which sends you
// looking at the CORS policy instead of at the address. The mirror case is worse: a page on
// https CANNOT call an http API, the browser blocks it as mixed content, whatever CORS says.
//
// So the scheme decides, and "ApiBaseAddress" still overrides both when the API is elsewhere.
var frontScheme = new Uri(builder.HostEnvironment.BaseAddress).Scheme;
var apiBaseAddress =
    builder.Configuration["ApiBaseAddress"]
    ?? builder.Configuration[$"Api:{frontScheme}"]
    ?? throw new InvalidOperationException(
        $"No API address configured for scheme '{frontScheme}'. Set \"Api:{frontScheme}\" " +
        "or \"ApiBaseAddress\" in wwwroot/appsettings.json.");

builder.Services.AddScoped(_ => new HttpClient
{
    // A base address without a trailing slash silently drops its last segment when a
    // relative URI is appended, so it is enforced here rather than trusted to the
    // configuration file.
    BaseAddress = new Uri(apiBaseAddress.EndsWith('/') ? apiBaseAddress : apiBaseAddress + "/")
});

// Scoped, not Singleton. ADR 0007 settled on "singleton" while noting that in Blazor
// WebAssembly the two coincide — there is one user and one scope — and that is still true.
// Scoped is preferred here for a reason the ADR did not consider: GameState depends on
// BattleApiClient, which depends on the HttpClient the template registers as Scoped. A
// Singleton depending on a Scoped service is a captive dependency; it happens to be
// harmless in WebAssembly and would be a genuine defect the day this ran on Blazor Server.
// Registering the three at the same lifetime removes the question instead of relying on the
// hosting model to make it moot.
builder.Services.AddScoped<BattleApiClient>();
builder.Services.AddScoped<GameState>();

// gRPC-Web, the only transport for firing (ADR 0005). A browser cannot speak plain gRPC
// over HTTP/2, so the call is wrapped by GrpcWebHandler and unwrapped server-side by
// app.UseGrpcWeb(). The channel targets the same API address as the HttpClient above, and
// the API's CORS policy exposes the grpc-status/grpc-message trailers without which this
// client could not read a refusal's status at all.
builder.Services.AddScoped(_ => GrpcChannel.ForAddress(apiBaseAddress, new GrpcChannelOptions
{
    HttpHandler = new GrpcWebHandler(GrpcWebMode.GrpcWeb, new HttpClientHandler())
}));
builder.Services.AddScoped(sp => new BattleService.BattleServiceClient(
    sp.GetRequiredService<GrpcChannel>()));
builder.Services.AddScoped<BattleGrpcClient>();

await builder.Build().RunAsync();
