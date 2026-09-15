using BattleShip.Models;

namespace BattleShip.API.Strategies;

/// <summary>
/// Adversaire le plus simple : tire au hasard parmi les cases jamais jouées.
/// L'aléa arrive par le constructeur, jamais via Random.Shared en dur, pour rester
/// reproductible en test (voir FleetPlacer pour la même discipline).
/// </summary>
public sealed class RandomStrategy(Random random) : IOpponentStrategy
{
    public string Name => "Random";

    public Coordinate NextShot(ShotHistory history)
    {
        var jouees = history.Shots.Select(s => s.At).ToHashSet();

        var restantes = new List<Coordinate>();
        for (var x = 0; x < history.GridSize; x++)
        {
            for (var y = 0; y < history.GridSize; y++)
            {
                var candidate = new Coordinate(x, y);
                if (!jouees.Contains(candidate))
                    restantes.Add(candidate);
            }
        }

        return restantes[random.Next(restantes.Count)];
    }
}
