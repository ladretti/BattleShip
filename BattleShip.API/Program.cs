using BattleShip.API.Benchmark;
using BattleShip.API.Services;
using BattleShip.API.Validation;
using BattleShip.Models;
using FluentValidation;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();
builder.Services.AddGrpc();
builder.Services.AddScoped<IValidator<BenchmarkInput>, BenchmarkInputValidator>();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();

app.UseGrpcWeb();
app.MapGrpcService<PingGrpcService>().EnableGrpcWeb();

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
