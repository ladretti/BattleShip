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

// Duel d'IA (REVUE-IA.md, revue 3) : mesure le nombre de coups des trois niveaux
// d'adversaire sur N parties à graine fixe. La liaison du corps de requête et sa
// validation sont deux responsabilités distinctes : le validateur est résolu par DI
// mais appelé explicitement, jamais implicitement.
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
