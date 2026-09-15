using BattleShip.API.Contracts;
using BattleShip.Models;
using FluentValidation;

namespace BattleShip.API.Validation;

/// <summary>
/// Structural validation of POST /games/{id}/placement: exactly one ship per template of
/// the fleet (matched by name), a parsable orientation, and non-negative coordinates.
///
/// What this validator deliberately does NOT check: overlap, the exact grid bounds of the
/// targeted game, and adjacency. All three come from <see cref="PlacementRules.Validate"/>,
/// called explicitly by the endpoint once this validator passes — never reimplemented
/// here. Grid bounds in particular cannot be checked at this level anyway: a game's exact
/// <c>GridSize</c> (5 to 20, see <see cref="CreateGameInputValidator"/>) is chosen per game
/// at creation and is not part of this request; only a coordinate's sign is
/// context-free enough to validate here.
/// </summary>
public sealed class PlacementInputValidator : AbstractValidator<PlacementInput>
{
    private static readonly IReadOnlyList<string> FleetShipNames =
        [.. GameRules.Default.Fleet.Select(t => t.Name).OrderBy(n => n, StringComparer.Ordinal)];

    public PlacementInputValidator()
    {
        RuleFor(i => i.Ships)
            .Must(HaveExactlyOneShipPerFleetTemplate)
            .WithMessage(
                "Ships must contain exactly one entry per fleet ship: " +
                string.Join(", ", FleetShipNames) + ".");

        RuleForEach(i => i.Ships).ChildRules(ship =>
        {
            ship.RuleFor(s => s.Orientation)
                .Must(orientation => Enum.TryParse<Orientation>(orientation, out _))
                .WithMessage("Orientation must be 'Horizontal' or 'Vertical'.");

            ship.RuleFor(s => s.X)
                .GreaterThanOrEqualTo(0)
                .WithMessage("X must be a non-negative coordinate.");

            ship.RuleFor(s => s.Y)
                .GreaterThanOrEqualTo(0)
                .WithMessage("Y must be a non-negative coordinate.");
        });
    }

    private static bool HaveExactlyOneShipPerFleetTemplate(IReadOnlyList<ShipPlacementInput> ships)
    {
        var names = ships.Select(s => s.Name).OrderBy(n => n, StringComparer.Ordinal).ToList();
        return names.SequenceEqual(FleetShipNames);
    }
}
