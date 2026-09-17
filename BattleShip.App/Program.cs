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

var frontScheme = new Uri(builder.HostEnvironment.BaseAddress).Scheme;
var apiBaseAddress =
    builder.Configuration["ApiBaseAddress"]
    ?? builder.Configuration[$"Api:{frontScheme}"]
    ?? throw new InvalidOperationException(
        $"No API address configured for scheme '{frontScheme}'. Set \"Api:{frontScheme}\" " +
        "or \"ApiBaseAddress\" in wwwroot/appsettings.json.");

builder.Services.AddScoped(_ => new HttpClient
{
    BaseAddress = new Uri(apiBaseAddress.EndsWith('/') ? apiBaseAddress : apiBaseAddress + "/")
});

builder.Services.AddScoped<BattleApiClient>();
builder.Services.AddScoped<GameState>();

builder.Services.AddScoped<AppearanceState>();
builder.Services.AddScoped(_ => Random.Shared);

builder.Services.AddScoped(_ => GrpcChannel.ForAddress(apiBaseAddress, new GrpcChannelOptions
{
    HttpHandler = new GrpcWebHandler(GrpcWebMode.GrpcWeb, new HttpClientHandler())
}));
builder.Services.AddScoped(sp => new BattleService.BattleServiceClient(
    sp.GetRequiredService<GrpcChannel>()));
builder.Services.AddScoped<BattleGrpcClient>();

await builder.Build().RunAsync();
