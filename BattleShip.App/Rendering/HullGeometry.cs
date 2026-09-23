using System.Globalization;

namespace BattleShip.App.Rendering;

public sealed record HullRect(double X, double Y, double Width, double Height, double Radius);

public sealed record HullDisc(double Cx, double Cy, double R);

public sealed record HullShape(
    double ViewWidth,
    double ViewHeight,
    string Outline,
    string DeckLine,
    IReadOnlyList<HullRect> Blocks,
    IReadOnlyList<HullDisc> Turrets);

public static class HullGeometry
{
    public const int Unit = 100;

    public static HullShape For(int size, bool vertical)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(size, 1);

        double length = size * Unit;
        const double beam = Unit;

        var bow = Math.Min(0.30 * length, 74);
        var stern = Math.Min(0.10 * length, 26);

        (double X, double Y) P(double along, double across) =>
            vertical ? (across, along) : (along, across);

        string Point(double along, double across)
        {
            var (x, y) = P(along, across);
            return $"{N(x)},{N(y)}";
        }

        var outline = string.Join(' ',
            $"M {Point(stern, 20)}",
            $"L {Point(length - bow, 14)}",
            $"Q {Point(length, beam / 2)} {Point(length - bow, beam - 14)}",
            $"L {Point(stern, beam - 20)}",
            $"Q {Point(0, beam / 2)} {Point(stern, 20)}",
            "Z");

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

    public static string N(double value) =>
    Math.Round(value, 2).ToString(CultureInfo.InvariantCulture);
}
