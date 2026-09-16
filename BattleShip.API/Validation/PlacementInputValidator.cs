using BattleShip.Models.Contracts;
using BattleShip.Models;
using FluentValidation;

namespace BattleShip.API.Validation;

/// <summary>
/// Structural validation of POST /games/{id}/placement: <see cref="Ships"/> present and
/// free of null entries, exactly one ship per template of the fleet (matched by name), an
/// orientation that is exactly "Horizontal" or "Vertical", and non-negative coordinates.
///
/// What this validator deliberately does NOT check: overlap, the exact grid bounds of the
/// targeted game, and adjacency. All three come from <see cref="PlacementRules.Validate"/>,
/// called explicitly by the endpoint once this validator passes — never reimplemented
/// here. Grid bounds in particular cannot be checked at this level anyway: a game's exact
/// <c>GridSize</c> (7 to 20, see <see cref="CreateGameInputValidator"/>) is chosen per game
/// at creation and is not part of this request; only a coordinate's sign is
/// context-free enough to validate here.
///
/// Everything for one ship's placement (name/orientation/coordinates) and for the whole
/// list (null, null entries, fleet composition) is written as a single <c>Custom</c> rule
/// rather than <c>RuleForEach</c>/<c>ChildRules</c>: a null <see cref="Ships"/> or a null
/// entry inside it must short-circuit before any per-ship rule runs, and a plain,
/// sequential method is far easier to verify against that requirement by reading it than
/// a chain of <c>Must</c>/<c>When</c> conditions layered on top of a collection rule (see
/// the task 13 correction report for the direct verification of this specific point:
/// <c>RuleFor(...).Custom(...)</c> was confirmed, by execution, to receive a null property
/// value without throwing).
/// </summary>
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
        // Enum.TryParse<Orientation> is deliberately NOT used here: it accepts numeric
        // representations ("2", "-1") that have no corresponding named value, and
        // Enum.IsDefined would not close that gap either — it reports a composed value
        // like "Horizontal,Vertical" as defined, because it evaluates to the same
        // underlying number as the single named value Vertical (both verified by
        // execution, see the task 13 correction report). Comparing against the two exact
        // literal names sidesteps both holes.
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
