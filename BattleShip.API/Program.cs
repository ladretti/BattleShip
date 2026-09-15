using BattleShip.API.Benchmark;
using BattleShip.API.Contracts;
using BattleShip.API.Endpoints;
using BattleShip.API.Services;
using BattleShip.API.Stores;
using BattleShip.API.Strategies;
using BattleShip.API.Validation;
using BattleShip.Models;
using FluentValidation;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();
builder.Services.AddGrpc();
builder.Services.AddScoped<IValidator<BenchmarkInput>, BenchmarkInputValidator>();

// IGameStore (ADR 0002): Singleton, so ConcurrentDictionary-backed concurrent access.
// Random.Shared, never a hard-coded call to it, so it can be swapped for a seeded
// instance under test (FleetPlacer, IOpponentStrategy).
builder.Services.AddSingleton<IGameStore, InMemoryGameStore>();
builder.Services.AddSingleton(_ => Random.Shared);
builder.Services.AddSingleton<IOpponentStrategyFactory, OpponentStrategyFactory>();
builder.Services.AddScoped<IValidator<CreateGameInput>, CreateGameInputValidator>();
builder.Services.AddScoped<IValidator<PlacementInput>, PlacementInputValidator>();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();

app.UseGrpcWeb();
app.MapGrpcService<PingGrpcService>().EnableGrpcWeb();

app.MapGameEndpoints();

// AI duel (REVUE-IA.md, Revue 3): measures the number of shots of the three opponent
// levels over N games with a fixed seed. Binding the request body and validating it are
// two distinct responsibilities: the validator is resolved by DI but called explicitly,
// never implicitly.
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
