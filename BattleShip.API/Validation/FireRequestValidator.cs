using BattleShip.API.Grpc;
using FluentValidation;

namespace BattleShip.API.Validation;

/// <summary>
/// Structural validation of the gRPC <c>Fire</c> request: <see cref="FireRequest.GameId"/>
/// must be a well-formed GUID. This is deliberately the only rule here: an unknown-but-
/// well-formed id is <see cref="Models.GameError.GameNotFound"/> (a business refusal,
/// translated by <see cref="Endpoints.ErrorMapping"/>), not a validation failure, and the
/// grid bounds of <see cref="FireRequest.X"/>/<see cref="FireRequest.Y"/> depend on the
/// targeted game's own <c>GridSize</c> (7 to 20, chosen per game) — exactly the same
/// reason <c>PlacementInputValidator</c> leaves bounds checking to the domain
/// (<c>Board.Fire</c>, which reports <see cref="Models.GameError.OutOfBounds"/>) instead
/// of duplicating it here without knowing which game is targeted.
/// </summary>
public sealed class FireRequestValidator : AbstractValidator<FireRequest>
{
    public FireRequestValidator()
    {
        RuleFor(r => r.GameId)
            .Must(id => Guid.TryParse(id, out _))
            .WithMessage("game_id must be a valid GUID.");
    }
}
