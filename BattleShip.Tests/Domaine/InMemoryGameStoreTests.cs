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
    public void Un_seul_tir_simultane_sur_la_meme_case_reussit(int execution)
    {
        _ = execution;   // le cas est rejoué : une course qui passe une fois ne prouve rien
        var store = new InMemoryGameStore();
        var game = PartieSurGrandeGrille();
        store.Save(game);

        var cible = new Coordinate(0, 0);
        const int nombreDeTirs = 32;
        // Task.Run passe par le pool de threads, qui n'injecte de nouveaux threads
        // qu'au compte-gouttes (environ un toutes les 500 ms au-delà du seuil
        // initial) : une Barrier posée dessus a été mesurée à 15-18 s par exécution
        // pour un taux de détection inchangé (voir REVUE-IA.md, Revue 4). Des Thread
        // dédiés démarrent immédiatement, sans cette injection progressive : la
        // barrière se relâche en quelques millisecondes et les 32 tirs entrent
        // réellement ensemble dans Mutate. Le nombre de participants DOIT
        // correspondre exactement au nombre de threads : sinon SignalAndWait bloque
        // indéfiniment au lieu de faire échouer le test.
        using var depart = new Barrier(nombreDeTirs);
        var resultats = new Result<ShotRecord>[nombreDeTirs];
        var threads = new Thread[nombreDeTirs];

        for (var i = 0; i < nombreDeTirs; i++)
        {
            var index = i;
            threads[index] = new Thread(() =>
            {
                depart.SignalAndWait();
                resultats[index] = store.Mutate(game.Id, g => g.PlayerFires(cible));
            });
        }

        foreach (var thread in threads)
            thread.Start();
        foreach (var thread in threads)
            thread.Join();

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
