using Avalonia;
using TKCode.Arc.Gestures;

namespace TKCode.Arc;

/// <summary>Arc card-stack constants, release rules and exit geometry, kept pure for tests.</summary>
internal static class CardStackMath
{
    /// <summary>Cards rendered in the stack. The last one waits invisibly one step behind and fades in as it rises.</summary>
    public const int Visible = 4;

    /// <summary>How far each card behind peeks out below the one in front (px), and how much smaller it is.</summary>
    public const double Peek = 12;

    public const double Shrink = 0.05;

    /// <summary>How far the cards behind rise while the top card is dragged away, in stack steps.</summary>
    public const double Lift = 0.4;

    /// <summary>Drag distance at which the cards behind have fully risen.</summary>
    public const double LiftDistance = 160;

    /// <summary>Degrees of rotation per pixel of horizontal travel.</summary>
    public const double Rotate = 0.065;

    /// <summary>A release faster than this (px/s) throws the card even when it has only moved a little.</summary>
    public const double Flick = 500;

    /// <summary>Vertical travel is not a decision: past a small free zone it resists like a rubber band.</summary>
    public const double FreeY = 48;

    public const double StretchY = 140;

    /// <summary>Default flick of a button or key decision (px/s).</summary>
    public static readonly Vector DefaultFling = new(900, -120);

    public static double ResistY(double raw)
    {
        var distance = Math.Abs(raw);
        return distance <= FreeY ? raw : Math.CopySign(FreeY + RubberBand.Apply(distance - FreeY, StretchY), raw);
    }

    public static double UnresistY(double shown)
    {
        var distance = Math.Abs(shown);
        if (distance <= FreeY)
        {
            return shown;
        }
        var stretch = Math.Min(distance - FreeY, StretchY - 1);
        return Math.CopySign(FreeY + RubberBand.Invert(stretch, StretchY), shown);
    }

    /// <summary>How far the stack behind has risen for a top card at (<paramref name="x"/>, <paramref name="y"/>).</summary>
    public static double LiftFor(double x, double y) => Math.Clamp(Math.Sqrt(x * x + y * 0.5 * (y * 0.5)) / LiftDistance, 0, 1);

    /// <summary>Opacity of a card at <paramref name="depth"/>: 1 up to depth 2, 0 at depth 3.</summary>
    public static double Presence(double depth) => Math.Clamp(Visible - 1 - depth, 0, 1);

    /// <summary>Darkening of a card at <paramref name="depth"/>: none at the top, full at depth 2.</summary>
    public static double Shade(double depth) => Math.Clamp(depth / 2, 0, 1);

    /// <summary>Opacity of the right ("Keep") stamp: fades in between 16 and 96px of rightward travel.</summary>
    public static double KeepStamp(double x) => Math.Clamp((x - 16) / 80, 0, 1);

    /// <summary>Opacity of the left ("Pass") stamp.</summary>
    public static double PassStamp(double x) => Math.Clamp((-16 - x) / 80, 0, 1);

    /// <summary>
    /// Whether a release decides the card: a flick in the direction of travel, or a drag far enough (a third of the
    /// card, at most 140px) that is not being flung back. Returns the direction (−1 left, +1 right) or 0.
    /// </summary>
    public static int ReleaseDirection(Point offset, Vector velocity, double width)
    {
        var direction = Math.Sign(offset.X);
        if (direction == 0)
        {
            return 0;
        }
        var flick = Math.Abs(velocity.X) > Flick && Math.Sign(velocity.X) == direction && Math.Abs(offset.X) > 12;
        var far = Math.Abs(offset.X) > Math.Min(width * 0.35, 140) &&
                  !(Math.Sign(velocity.X) == -direction && Math.Abs(velocity.X) > 300);
        return flick || far ? direction : 0;
    }

    /// <summary>
    /// Where a thrown card leaves to: along the flick when it heads the chosen way, otherwise along the drag, otherwise
    /// straight out; never steeper than about 63°; 1.6 card widths away (far enough to clear a typical card column).
    /// </summary>
    public static Point ExitFor(Point current, Vector velocity, int direction, double width)
    {
        var speed = Math.Sqrt(velocity.X * velocity.X + velocity.Y * velocity.Y);
        double ux = velocity.X, uy = velocity.Y;
        if (speed < 200 || Math.Sign(ux) != direction)
        {
            var dragged = Math.Abs(current.X) > 8;
            ux = dragged ? current.X : direction;
            uy = dragged ? current.Y * 0.5 : -0.12;
        }
        var length = Math.Sqrt(ux * ux + uy * uy);
        if (length == 0)
        {
            length = 1;
        }
        ux /= length;
        uy /= length;
        if (Math.Abs(ux) < 0.45)
        {
            ux = direction * 0.45;
            uy = (uy == 0 ? -1 : Math.Sign(uy)) * Math.Sqrt(1 - 0.45 * 0.45);
        }
        var reach = width * 1.6;
        return new Point(current.X + ux * reach, current.Y + uy * reach);
    }
}
