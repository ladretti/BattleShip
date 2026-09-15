using BattleShip.API.Benchmark;
using FluentValidation;

namespace BattleShip.API.Validation;

/// <summary>
/// Valide l'entrée du duel d'IA exposé sur POST /benchmark. Borné à 1000 parties par
/// stratégie : au-delà, la durée de la requête HTTP devient déraisonnable (trois
/// stratégies jouées séquentiellement, chacune jusqu'à la flotte coulée).
/// </summary>
public sealed class BenchmarkInputValidator : AbstractValidator<BenchmarkInput>
{
    public BenchmarkInputValidator()
    {
        RuleFor(i => i.Games).InclusiveBetween(1, 1000);
    }
}
