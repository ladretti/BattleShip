using BattleShip.Models.Contracts;
using BattleShip.Models;
using FluentValidation;

namespace BattleShip.API.Validation;

public sealed class PlacementInputValidator : AbstractValidator<PlacementInput>
{
    private static readonly IReadOnlyList<string> FleetShipNames =
        [.. GameRules.Default.Fleet.Select(t => t.Name).OrderBy(n => n, StringComparer.Ordinal)];

    public PlacementInputValidator()
    {
        RuleFor(i => i.Ships).Custom((ships, context) =>
        {
            if (ships is null)
            {
                context.AddFailure("Ships must not be null.");
                return;
            }

            if (ships.Any(s => s is null))
            {
                context.AddFailure("Ships must not contain a null entry.");
                return;
            }

            if (!HaveExactlyOneShipPerFleetTemplate(ships))
            {
                context.AddFailure(
                    "Ships must contain exactly one entry per fleet ship: " +
                    string.Join(", ", FleetShipNames) + ".");
                return;
            }

            foreach (var ship in ships)
            {
                ValidateShip(ship, context);
            }
        });
    }

    private static void ValidateShip(ShipPlacementInput ship, ValidationContext<PlacementInput> context)
    {
        if (ship.Orientation is not ("Horizontal" or "Vertical"))
        {
            context.AddFailure(
                nameof(ShipPlacementInput.Orientation),
                $"Orientation must be 'Horizontal' or 'Vertical' (was '{ship.Orientation}').");
        }

        if (ship.X < 0)
            context.AddFailure(nameof(ShipPlacementInput.X), "X must be a non-negative coordinate.");

        if (ship.Y < 0)
            context.AddFailure(nameof(ShipPlacementInput.Y), "Y must be a non-negative coordinate.");
    }

    private static bool HaveExactlyOneShipPerFleetTemplate(IReadOnlyList<ShipPlacementInput> ships)
    {
        var names = ships.Select(s => s.Name).OrderBy(n => n, StringComparer.Ordinal).ToList();
        return names.SequenceEqual(FleetShipNames);
    }
}
