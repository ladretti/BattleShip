using BattleShip.Models.Contracts;
using FluentValidation;

namespace BattleShip.API.Validation;

public sealed class EventQueryValidator : AbstractValidator<EventQuery>
{
    public EventQueryValidator()
    {
        RuleFor(q => q.From)
            .GreaterThanOrEqualTo(0)
            .WithMessage("The 'from' sequence must be zero or greater.");
    }
}
