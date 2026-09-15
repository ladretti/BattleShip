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

    [Fact]
    public void Un_resultat_par_defaut_n_est_pas_un_succes()
    {
        Result<int> result = default;

        Assert.False(result.IsOk);
    }

    [Fact]
    public void Lire_la_valeur_d_un_resultat_par_defaut_est_une_anomalie()
    {
        Result<int> result = default;

        Assert.Throws<InvalidOperationException>(() => _ = result.Value);
    }

    [Fact]
    public void Lire_l_erreur_d_un_resultat_par_defaut_est_une_anomalie()
    {
        Result<int> result = default;

        Assert.Throws<InvalidOperationException>(() => _ = result.Error);
    }

    [Fact]
    public void Lire_l_erreur_d_un_resultat_reussi_est_une_anomalie()
    {
        var result = Result<int>.Ok(1);

        Assert.Throws<InvalidOperationException>(() => _ = result.Error);
    }

    [Fact]
    public void Un_tableau_de_resultats_non_initialise_ne_contient_aucun_succes()
    {
        var results = new Result<string>[3];

        Assert.All(results, r => Assert.False(r.IsOk));
    }
}
