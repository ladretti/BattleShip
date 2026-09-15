namespace BattleShip.Models;

/// <summary>
/// Une stratégie adverse remplaçable, pas une méthode privée du moteur (ADR 0003).
/// Chaque implémentation ne reçoit que ce que le joueur a le droit de connaître —
/// via <see cref="ShotHistory"/> — et doit respecter l'invariant vérifié par
/// StrategyInvariantTests : ne jamais proposer un coup hors grille, ni une case
/// déjà jouée.
/// </summary>
public interface IOpponentStrategy
{
    string Name { get; }

    /// <summary>
    /// Propose la prochaine case à tirer à partir de <paramref name="history"/>.
    ///
    /// Précondition : il reste au moins une case jouable dans la grille. Une
    /// implémentation qui énumère un ensemble de candidats (chasse, ratissage...)
    /// n'a aucune case à choisir si toutes sont déjà jouées, et peut alors lever une
    /// exception au lieu de renvoyer une <see cref="Coordinate"/> valide — c'est à
    /// l'appelant de ne jamais invoquer <see cref="NextShot"/> sur une grille pleine
    /// ou une partie déjà terminée, jamais à l'implémentation de le vérifier.
    ///
    /// Ce contrat est respecté par construction tant que le moteur ne fait tirer
    /// l'adversaire que sur une partie au statut InProgress : une partie se termine
    /// dès que la flotte adverse est coulée, ce qui survient strictement avant que
    /// la grille soit épuisée (une grille 10x10 contient toujours davantage de
    /// cases que la flotte n'occupe de cellules). La précondition n'est donc jamais
    /// testée ici — testée, elle imposerait à chaque stratégie de gérer un cas que
    /// le moteur ne produit jamais — mais elle doit rester vraie pour toute future
    /// implémentation d'<see cref="IOpponentStrategy"/> câblée au serveur.
    /// </summary>
    Coordinate NextShot(ShotHistory history);
}
