using System.Globalization;

namespace BattleShip.App.Rendering;

/// <summary>A rectangle in the hull's own coordinate space, corners already mapped.</summary>
public sealed record HullRect(double X, double Y, double Width, double Height, double Radius);

/// <summary>A disc in the hull's own coordinate space.</summary>
public sealed record HullDisc(double Cx, double Cy, double R);

/// <summary>
/// Everything needed to draw one ship: the viewBox it is drawn in, the outline of the hull,
/// and the deck furniture on top of it.
/// </summary>
public sealed record HullShape(
    double ViewWidth,
    double ViewHeight,
    string Outline,
    string DeckLine,
    IReadOnlyList<HullRect> Blocks,
    IReadOnlyList<HullDisc> Turrets);

/// <summary>
/// Builds a ship silhouette from nothing but its size and orientation — no sprite, no asset,
/// no per-ship drawing. Five ships, two orientations and three skins would otherwise mean
/// thirty images to keep in step; here they are one function and three CSS custom properties.
///
/// <para><b>Coordinate space.</b> One cell is <see cref="Unit"/> units. A ship of size n is
/// drawn along its length from 0 to n × <see cref="Unit"/>, across its beam from 0 to
/// <see cref="Unit"/>. Everything is written in that (along, across) space and mapped to
/// (x, y) at the end, so a vertical ship is the same drawing with the axes swapped rather
/// than a second set of coordinates to maintain.</para>
///
/// <para><b>What this deliberately does NOT do.</b> It never places damage. The hull spans n
/// cells <i>and the gutters between them</i>, and the gutter width differs per skin (1px on
/// the chart, 6px on the tabletop), so a mark positioned inside this viewBox would drift by
/// up to a tenth of a cell depending on the theme. Damage is drawn as its own per-cell
/// element in the same overlay grid, where the position is exact by construction.</para>
/// </summary>
public static class HullGeometry
{
    /// <summary>One board cell, in viewBox units.</summary>
    public const int Unit = 100;

    public static HullShape For(int size, bool vertical)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(size, 1);

        double length = size * Unit;
        const double beam = Unit;

        // The bow taper grows with the ship but stops short of swallowing a small hull: a
        // two-cell destroyer must still read as a hull with a point, not as an arrowhead.
        var bow = Math.Min(0.30 * length, 74);
        var stern = Math.Min(0.10 * length, 26);

        (double X, double Y) P(double along, double across) =>
            vertical ? (across, along) : (along, across);

        string Point(double along, double across)
        {
            var (x, y) = P(along, across);
            return $"{N(x)},{N(y)}";
        }

        // Stern (rounded), straight sides, bow (pointed). Written once, read either way.
        var outline = string.Join(' ',
            $"M {Point(stern, 20)}",
            $"L {Point(length - bow, 14)}",
            $"Q {Point(length, beam / 2)} {Point(length - bow, beam - 14)}",
            $"L {Point(stern, beam - 20)}",
            $"Q {Point(0, beam / 2)} {Point(stern, 20)}",
            "Z");

        // A line inset from the outline: on a real hull this is the deck edge, and it is what
        // stops the shape reading as a flat blob at a glance.
        var deck = string.Join(' ',
            $"M {Point(stern + 10, 32)}",
            $"L {Point(length - bow - 4, 28)}",
            $"Q {Point(length - 22, beam / 2)} {Point(length - bow - 4, beam - 28)}",
            $"L {Point(stern + 10, beam - 32)}",
            $"Q {Point(stern + 2, beam / 2)} {Point(stern + 10, 32)}",
            "Z");

        var blocks = new List<HullRect>();
        var turrets = new List<HullDisc>();

        HullRect Block(double along0, double across0, double along1, double across1, double radius)
        {
            var (x0, y0) = P(along0, across0);
            var (x1, y1) = P(along1, across1);
            return new HullRect(Math.Min(x0, x1), Math.Min(y0, y1),
                Math.Abs(x1 - x0), Math.Abs(y1 - y0), radius);
        }

        // Superstructure and bridge, placed in PROPORTION to the hull so a destroyer and a
        // carrier look like the same navy rather than two unrelated drawings.
        blocks.Add(Block(length * 0.30, 34, length * 0.52, beam - 34, 10));
        blocks.Add(Block(length * 0.34, 41, length * 0.45, beam - 41, 6));

        if (size >= 3)
            blocks.Add(Block(length * 0.55, 40, length * 0.62, beam - 40, 8));

        if (size >= 4)
        {
            var (fx, fy) = P(length * 0.15, beam / 2);
            var (ax, ay) = P(length * 0.74, beam / 2);
            turrets.Add(new HullDisc(fx, fy, 13));
            turrets.Add(new HullDisc(ax, ay, 13));
        }

        var (viewWidth, viewHeight) = vertical ? (beam, length) : (length, beam);
        return new HullShape(viewWidth, viewHeight, outline, deck, blocks, turrets);
    }

    /// <summary>
    /// SVG never accepts a decimal comma, and this app runs under whatever locale the
    /// player's browser reports — so the invariant culture is not optional here.
    /// </summary>
    public static string N(double value) =>
        Math.Round(value, 2).ToString(CultureInfo.InvariantCulture);
}
