using BattleShip.API.Benchmark;
using BattleShip.Models;

namespace BattleShip.Tests.Adversaire;

public sealed class StrategyBenchmarkTests
{
    [Fact]
    public void Les_trois_niveaux_sont_ordonnes_par_efficacite()
    {
        var results = StrategyBenchmark.Run(GameRules.Default, games: 200, seed: 20260915)
            .ToDictionary(r => r.StrategyName);

        var aleatoire = results["Random"].AverageShots;
        var chasse = results["HuntTarget"].AverageShots;
        var densite = results["Density"].AverageShots;

        Assert.True(aleatoire > chasse,
            $"aléatoire {aleatoire:F1} devrait être pire que chasse/cible {chasse:F1}");
        Assert.True(chasse > densite,
            $"chasse/cible {chasse:F1} devrait être pire que densité {densite:F1}");
        Assert.True(densite < 55,
            $"densité mesurée à {densite:F1} coups, attendue sous 55");
    }

    [Fact]
    public void La_mesure_est_reproductible_a_graine_egale()
    {
        var a = StrategyBenchmark.Run(GameRules.Default, 50, 1);
        var b = StrategyBenchmark.Run(GameRules.Default, 50, 1);

        Assert.Equal(a.Select(r => r.AverageShots), b.Select(r => r.AverageShots));
    }
}
