using BattleShip.Models;

namespace BattleShip.Tests.Domaine;

public sealed class GameTests
{
    // Grille 3x3 avec un seul torpilleur horizontal en (0,0)-(1,0).
    private static Game PartieMinuscule()
    {
        var rules = new GameRules(3, [new ShipTemplate("Torpilleur", 2)], false, true);
        var flotte = () => new List<Ship>
        {
            new("Torpilleur", 2, [new Coordinate(0, 0), new Coordinate(1, 0)])
        };
        return Game.Start(Guid.NewGuid(), rules, flotte(), flotte());
    }

    [Fact]
    public void Un_tir_hors_grille_est_refuse()
    {
        var game = PartieMinuscule();

        var result = game.PlayerFires(new Coordinate(3, 0));

        Assert.False(result.IsOk);
        Assert.Equal(GameError.OutOfBounds, result.Error);
    }

    [Fact]
    public void Un_tir_sur_une_case_deja_jouee_est_refuse()
    {
        // (0,0) TOUCHE, donc la main reste au joueur (ExtraTurnOnHit).
        // Sur un coup manqué le refus attendu serait NotYourTurn, pas CellAlreadyShot.
        var game = PartieMinuscule();
        game.PlayerFires(new Coordinate(0, 0));

        var result = game.PlayerFires(new Coordinate(0, 0));

        Assert.False(result.IsOk);
        Assert.Equal(GameError.CellAlreadyShot, result.Error);
    }

    [Fact]
    public void L_adversaire_ne_peut_pas_tirer_quand_c_est_au_joueur()
    {
        var game = PartieMinuscule();

        var result = game.OpponentFires(new Coordinate(0, 0));

        Assert.False(result.IsOk);
        Assert.Equal(GameError.NotYourTurn, result.Error);
    }

    [Fact]
    public void Un_coup_manque_de_l_adversaire_rend_la_main_au_joueur()
    {
        var game = PartieMinuscule();
        game.PlayerFires(new Coordinate(2, 2));   // manqué : la main passe

        var result = game.OpponentFires(new Coordinate(2, 2));

        Assert.True(result.IsOk);
        Assert.Equal(ShotResult.Miss, result.Value.Result);
        Assert.Equal(Player.Human, game.CurrentPlayer);
    }

    [Fact]
    public void Un_tir_sur_la_derniere_case_d_un_navire_le_coule()
    {
        var game = PartieMinuscule();
        game.PlayerFires(new Coordinate(0, 0));

        var result = game.PlayerFires(new Coordinate(1, 0));

        Assert.Equal(ShotResult.Sunk, result.Value.Result);
        Assert.Equal("Torpilleur", result.Value.SunkShipName);
    }

    [Fact]
    public void Couler_le_dernier_navire_termine_la_partie()
    {
        var game = PartieMinuscule();
        game.PlayerFires(new Coordinate(0, 0));
        game.PlayerFires(new Coordinate(1, 0));

        Assert.Equal(GameStatus.Finished, game.Status);
    }

    [Fact]
    public void Un_tir_apres_la_fin_de_partie_est_refuse()
    {
        var game = PartieMinuscule();
        game.PlayerFires(new Coordinate(0, 0));
        game.PlayerFires(new Coordinate(1, 0));

        var result = game.PlayerFires(new Coordinate(2, 2));

        Assert.False(result.IsOk);
        Assert.Equal(GameError.GameAlreadyFinished, result.Error);
    }

    [Fact]
    public void Une_touche_laisse_la_main_au_joueur()
    {
        var game = PartieMinuscule();

        game.PlayerFires(new Coordinate(0, 0));

        Assert.Equal(Player.Human, game.CurrentPlayer);
    }

    [Fact]
    public void Un_coup_manque_passe_la_main_a_l_adversaire()
    {
        var game = PartieMinuscule();

        game.PlayerFires(new Coordinate(2, 2));

        Assert.Equal(Player.Opponent, game.CurrentPlayer);
    }

    [Fact]
    public void Un_tir_du_joueur_quand_ce_n_est_pas_son_tour_est_refuse()
    {
        var game = PartieMinuscule();
        game.PlayerFires(new Coordinate(2, 2));   // manqué : la main passe

        var result = game.PlayerFires(new Coordinate(2, 1));

        Assert.False(result.IsOk);
        Assert.Equal(GameError.NotYourTurn, result.Error);
    }

    [Fact]
    public void Un_tir_sur_une_partie_qui_n_a_pas_commence_est_refuse()
    {
        // GameStatus.Placing n'est atteint par aucun chemin à ce stade : ce test force
        // l'état par réflexion pour vérifier que la garde de Fire est fail-closed, donc
        // qu'elle refusera aussi tout état ajouté à l'énumération plus tard.
        var game = PartieMinuscule();
        typeof(Game).GetProperty(nameof(Game.Status))!
            .SetValue(game, GameStatus.Placing);

        var result = game.PlayerFires(new Coordinate(0, 0));

        Assert.False(result.IsOk);
        Assert.Equal(GameError.GameNotStarted, result.Error);
    }
}
