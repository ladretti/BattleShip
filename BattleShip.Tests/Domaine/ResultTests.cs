using BattleShip.Models;

namespace BattleShip.Tests.Domaine;

public sealed class ResultTests
{
    [Fact]
    public void Un_resultat_ok_porte_sa_valeur()
    {
        var result = Result<int>.Ok(42);

        Assert.True(result.IsOk);
        Assert.Equal(42, result.Value);
    }

    [Fact]
    public void Un_resultat_en_echec_porte_son_erreur()
    {
        var result = Result<int>.Fail(GameError.CellAlreadyShot);

        Assert.False(result.IsOk);
        Assert.Equal(GameError.CellAlreadyShot, result.Error);
    }

    [Fact]
    public void Lire_la_valeur_d_un_resultat_en_echec_est_une_anomalie()
    {
        var result = Result<int>.Fail(GameError.OutOfBounds);

        Assert.Throws<InvalidOperationException>(() => _ = result.Value);
    }
}
