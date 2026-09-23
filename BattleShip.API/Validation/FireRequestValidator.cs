using BattleShip.API.Grpc;
using FluentValidation;

namespace BattleShip.API.Validation;

public sealed class FireRequestValidator : AbstractValidator<FireRequest>
{
    public FireRequestValidator()
    {
        RuleFor(r => r.GameId)
            .Must(id => Guid.TryParse(id, out _))
            .WithMessage("game_id must be a valid GUID.");
    }
}
