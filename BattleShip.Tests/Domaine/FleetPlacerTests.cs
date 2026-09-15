using BattleShip.Models;

namespace BattleShip.Tests.Domaine;

public sealed class FleetPlacerTests
{
    public static TheoryData<int> Graines()
    {
        var data = new TheoryData<int>();
        for (var seed = 1; seed <= 50; seed++) data.Add(seed);
        return data;
    }

    [Theory]
    [MemberData(nameof(Graines))]
    public void Un_placement_automatique_respecte_toujours_les_regles(int seed)
    {
        var placer = new FleetPlacer(new Random(seed));

        var result = placer.PlaceAll(GameRules.Default);

        Assert.True(result.IsOk);
        var cells = result.Value.SelectMany(s => s.Cells).ToList();

        // Aucune case hors grille.
        Assert.All(cells, c =>
        {
            Assert.InRange(c.X, 0, GameRules.Default.GridSize - 1);
            Assert.InRange(c.Y, 0, GameRules.Default.GridSize - 1);
        });

        // Aucun chevauchement.
        Assert.Equal(cells.Count, cells.Distinct().Count());

        // Aucune adjacence entre deux navires distincts.
        foreach (var a in result.Value)
            foreach (var b in result.Value.Where(x => !ReferenceEquals(x, a)))
                foreach (var ca in a.Cells)
                    foreach (var cb in b.Cells)
                        Assert.False(
                            Math.Abs(ca.X - cb.X) <= 1 && Math.Abs(ca.Y - cb.Y) <= 1,
                            $"navires adjacents en {ca} et {cb} (graine {seed})");
    }

    [Fact]
    public void Deux_graines_identiques_produisent_le_meme_placement()
    {
        var a = new FleetPlacer(new Random(12345)).PlaceAll(GameRules.Default);
        var b = new FleetPlacer(new Random(12345)).PlaceAll(GameRules.Default);

        Assert.Equal(
            a.Value.SelectMany(s => s.Cells).ToList(),
            b.Value.SelectMany(s => s.Cells).ToList());
    }

    [Fact]
    public void Une_flotte_qui_ne_tient_pas_dans_la_grille_est_refusee_sans_boucler()
    {
        var impossible = new GameRules(
            GridSize: 3,
            Fleet: [new ShipTemplate("A", 3), new ShipTemplate("B", 3),
                    new ShipTemplate("C", 3)],
            ShipsMayTouch: false,
            ExtraTurnOnHit: true);

        var result = new FleetPlacer(new Random(1)).PlaceAll(impossible);

        Assert.False(result.IsOk);
        Assert.Equal(GameError.InvalidPlacement, result.Error);
    }
}
