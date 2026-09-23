using BattleShip.API.Benchmark;
using BattleShip.Models.Contracts;
using BattleShip.API.Endpoints;
using BattleShip.API.Grpc;
using BattleShip.API.Services;
using BattleShip.API.Stores;
using BattleShip.API.Strategies;
using BattleShip.API.Validation;
using BattleShip.Models;
using FluentValidation;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi();
builder.Services.AddGrpc();

builder.Services.AddCors(options => options.AddPolicy("front", policy => policy
    .WithOrigins(builder.Configuration.GetSection("Cors:Origins").Get<string[]>() ?? [])
    .AllowAnyMethod()
    .AllowAnyHeader()
    .WithExposedHeaders("grpc-status", "grpc-message", "grpc-encoding",
                        "grpc-accept-encoding")));
builder.Services.AddScoped<IValidator<BenchmarkInput>, BenchmarkInputValidator>();

builder.Services.AddSingleton<IGameStore, InMemoryGameStore>();
builder.Services.AddSingleton(_ => Random.Shared);
builder.Services.AddSingleton<IOpponentStrategyFactory, OpponentStrategyFactory>();
builder.Services.AddScoped<IValidator<CreateGameInput>, CreateGameInputValidator>();
builder.Services.AddScoped<IValidator<PlacementInput>, PlacementInputValidator>();
builder.Services.AddScoped<IValidator<FireRequest>, FireRequestValidator>();
builder.Services.AddScoped<IValidator<EventQuery>, EventQueryValidator>();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();

app.UseCors("front");

app.UseGrpcWeb();
app.MapGrpcService<BattleGrpcService>().EnableGrpcWeb();

app.MapGameEndpoints();

app.MapPost("/benchmark", async Task<IResult> (
    BenchmarkInput input, IValidator<BenchmarkInput> validator) =>
{
    var check = await validator.ValidateAsync(input);
    if (!check.IsValid)
        return TypedResults.ValidationProblem(check.ToDictionary());

    return TypedResults.Ok(StrategyBenchmark.Run(GameRules.Default, input.Games, input.Seed));
});

app.Run();

public partial class Program;
