using BattleShip.API.Stores;
using BattleShip.Models;

namespace BattleShip.Tests.Domaine;

public sealed class InMemoryGameStoreTests
{
    private static Game PartieSurGrandeGrille()
    {
        var rules = GameRules.Default;
        var flotte = () => new FleetPlacer(new Random(7)).PlaceAll(rules).Value;
        return Game.Start(Guid.NewGuid(), rules, flotte(), flotte());
    }

    [Fact]
    public void Une_partie_absente_renvoie_GameNotFound()
    {
        var store = new InMemoryGameStore();

        var result = store.Mutate(Guid.NewGuid(), g => g.PlayerFires(new Coordinate(0, 0)));

        Assert.False(result.IsOk);
        Assert.Equal(GameError.GameNotFound, result.Error);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(5)]
    public async Task Un_seul_tir_simultane_sur_la_meme_case_reussit(int execution)
    {
        _ = execution;   // le cas est rejoué : une course qui passe une fois ne prouve rien
        var store = new InMemoryGameStore();
        var game = PartieSurGrandeGrille();
        store.Save(game);

        var cible = new Coordinate(0, 0);
        var tirs = Enumerable.Range(0, 32).Select(_ => Task.Run(() =>
            store.Mutate(game.Id, g => g.PlayerFires(cible))));

        var resultats = await Task.WhenAll(tirs);

        Assert.Equal(1, resultats.Count(r => r.IsOk));
        Assert.All(resultats.Where(r => !r.IsOk), r =>
            Assert.Contains(r.Error, new[]
            {
                GameError.CellAlreadyShot, GameError.NotYourTurn,
                GameError.GameAlreadyFinished
            }));
    }

    [Fact]
    public void Deux_parties_distinctes_ne_se_bloquent_pas()
    {
        var store = new InMemoryGameStore();
        var a = PartieSurGrandeGrille();
        var b = PartieSurGrandeGrille();
        store.Save(a);
        store.Save(b);

        var ra = store.Mutate(a.Id, g => g.PlayerFires(new Coordinate(0, 0)));
        var rb = store.Mutate(b.Id, g => g.PlayerFires(new Coordinate(0, 0)));

        Assert.True(ra.IsOk);
        Assert.True(rb.IsOk);
    }
}
