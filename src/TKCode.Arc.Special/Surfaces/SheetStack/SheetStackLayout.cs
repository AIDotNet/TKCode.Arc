using TKCode.Arc.Gestures;

namespace TKCode.Arc;

/// <summary>The geometry a <see cref="SheetStackLayer"/> lays its sheets out with (values from the tokens).</summary>
internal readonly record struct SheetStackMetrics(
    double SheetMaxWidth,
    double DialogWidth,
    double SheetTopInset,
    double DialogVerticalInset,
    double SheetPeek,
    double DialogPeek,
    double ScaleStep,
    double DimStep,
    double ExitOvershoot)
{
    /// <summary>The measured Arc values (also the token defaults).</summary>
    public static SheetStackMetrics Default { get; } = new(560, 440, 20, 40, 10, 14, 0.05, 0.5, 24);

    public double Peek(bool dialog) => dialog ? DialogPeek : SheetPeek;
}

/// <summary>Where a level of the stack is drawn: its translation, scale (around its top-center) and dim.</summary>
internal readonly record struct SheetStackPose(double TranslateY, double Scale, double Dim);

/// <summary>
/// Pure layout of the stack (Arc Sheet stack, design reference; see the spec's layout section). Sheets sit on the layer's
/// bottom edge and dialogs at its center; a level's available height shrinks by one peek per level under it so every level
/// below can show its top edge; each level below the top is drawn so its visible top edge sits one peek per level above
/// the top sheet's top edge.
/// </summary>
internal static class SheetStackLayout
{
    /// <summary>Panel width: sheets fill a narrow layer and stop at 560; dialogs are 440 (never wider than the layer less 16 a side).</summary>
    public static double PanelWidth(in SheetStackMetrics m, double layerWidth, bool dialog)
    {
        if (dialog)
        {
            return Math.Max(0, Math.Min(m.DialogWidth, layerWidth - 32));
        }
        return Math.Max(0, Math.Min(m.SheetMaxWidth, layerWidth));
    }

    /// <summary>
    /// The tallest a level may get: the layer height less the inset (20 above a sheet, 40 above and below a dialog) and one
    /// peek per level below it (<paramref name="index"/> counts from the bottom, 0 first). Arc: L1 558, L2 548, L3 538 in a 578
    /// layer (sheets); 498, 484, 470 (dialogs).
    /// </summary>
    public static double AvailableHeight(in SheetStackMetrics m, double layerHeight, bool dialog, int index)
    {
        var inset = dialog ? 2 * m.DialogVerticalInset : m.SheetTopInset;
        return Math.Max(0, layerHeight - inset - m.Peek(dialog) * Math.Max(0, index));
    }

    /// <summary>The top edge of a level at rest: on the bottom edge for sheets, centered for dialogs.</summary>
    public static double LayoutTop(double layerHeight, double height, bool dialog)
        => dialog ? (layerHeight - height) / 2 : layerHeight - height;

    /// <summary>
    /// The pose of a level <paramref name="depth"/> levels below the top (continuous: springs and drags move it between
    /// whole levels). <paramref name="referenceTop"/> is the top sheet's top edge at rest, <paramref name="ownTop"/> this
    /// level's own. At depth 0 the level is where it lays out; from depth 1 its visible top edge is
    /// <c>referenceTop − peek·depth</c>, and in between it moves linearly, so a drag that interpolates depth from d to d−1
    /// interpolates the translation, scale and dim linearly too. Without full motion only the dim changes.
    /// </summary>
    public static SheetStackPose Pose(in SheetStackMetrics m, bool dialog, double depth, double referenceTop, double ownTop, bool full)
    {
        var dim = Math.Clamp(m.DimStep * depth, 0, 1);
        if (!full)
        {
            return new SheetStackPose(0, 1, dim);
        }
        var d = Math.Max(0, depth);
        var ty = Math.Min(d, 1) * (referenceTop - ownTop) - m.Peek(dialog) * d;
        return new SheetStackPose(ty, 1 - m.ScaleStep * d, dim);
    }

    /// <summary>How far a drag of the top sheet has gone toward closing it: offset / height, 0..1 (rubber band up counts as 0).</summary>
    public static double DragProgress(double offset, double height)
        => height <= 0 ? 0 : Math.Clamp(offset / height, 0, 1);

    /// <summary>The breakpoint rule of <see cref="SheetStackMode.Auto"/>: dialogs from <paramref name="breakpoint"/> up.</summary>
    public static bool IsDialog(SheetStackMode mode, double width, double breakpoint)
        => mode switch
        {
            SheetStackMode.Dialog => true,
            SheetStackMode.Sheet => false,
            _ => width >= breakpoint
        };
}

/// <summary>What a released drag of the top sheet does.</summary>
internal enum SheetStackRelease
{
    Return,
    Dismiss
}

/// <summary>
/// The vertical drag-to-dismiss model of the top sheet (Arc Sheet stack, design reference): pure, so tests can feed it
/// pointer samples. A drag starts once the pointer has moved more than 6 px vertically and then follows it 1:1 (the 6 px
/// are not made up); upward it resists with <c>RubberBand.Apply(u, H, 0.3)</c>. The release velocity is the average over
/// the newest 100 ms or more (the newest sample against the newest one at least 100 ms older, else the drag's first), and
/// needs two moves after the start; a pointer that rested more than 100 ms before release has none.
/// Velocity is measured here rather than with <see cref="VelocityTracker"/> because Arc's thresholds (450 px/s) were
/// measured against this averaging, not a recency-weighted slope.
/// </summary>
internal sealed class SheetStackDragModel
{
    public const double Slop = 6;
    public const double Resistance = 0.3;
    public const double DismissFraction = 0.35;
    public const double DismissVelocity = 450;
    public const double Overshoot = 24;
    private const double VelocityWindow = 0.1;
    private const double StaleAfter = 0.1;
    private const int Capacity = 32;

    private readonly double[] _t = new double[Capacity];
    private readonly double[] _y = new double[Capacity];
    private int _head;
    private int _count;
    private double _pressY;
    private double _anchor;
    private double _lastTime;

    /// <summary>True between the press and the release.</summary>
    public bool IsPressed { get; private set; }

    /// <summary>True once the pointer passed the slop.</summary>
    public bool IsDragging { get; private set; }

    /// <summary>Pointer travel since the drag started (positive down), without resistance.</summary>
    public double RawOffset { get; private set; }

    /// <summary>The vertical travel since the press (sign included).</summary>
    public double Travel { get; private set; }

    public void Press(double y, double time)
    {
        IsPressed = true;
        IsDragging = false;
        _pressY = y;
        _anchor = y;
        RawOffset = 0;
        Travel = 0;
        _count = 0;
        _head = 0;
        _lastTime = time;
    }

    /// <summary>Feeds a move; returns true when this move started the drag.</summary>
    public bool Move(double y, double time)
    {
        if (!IsPressed)
        {
            return false;
        }
        Travel = y - _pressY;
        var started = false;
        if (!IsDragging)
        {
            if (Math.Abs(Travel) <= Slop)
            {
                return false;
            }
            IsDragging = true;
            started = true;
            _anchor = _pressY + Math.CopySign(Slop, Travel);
        }
        RawOffset = y - _anchor;
        Add(RawOffset, time);
        return started;
    }

    /// <summary>Ends the gesture; returns the release velocity in px/s (positive down).</summary>
    public double Release(double time)
    {
        var velocity = IsDragging ? Velocity(time) : 0;
        IsPressed = false;
        IsDragging = false;
        return velocity;
    }

    /// <summary>Abandons the gesture.</summary>
    public void Cancel()
    {
        IsPressed = false;
        IsDragging = false;
    }

    /// <summary>The velocity seen at <paramref name="now"/>.</summary>
    public double Velocity(double now)
    {
        // The start sample plus two moves.
        if (_count < 3 || now - _lastTime > StaleAfter)
        {
            return 0;
        }
        var newest = (_head - 1 + Capacity) % Capacity;
        var reference = newest;
        for (var i = 1; i < _count; i++)
        {
            reference = (newest - i + Capacity) % Capacity;
            if (_t[newest] - _t[reference] >= VelocityWindow)
            {
                break;
            }
        }
        var dt = _t[newest] - _t[reference];
        return dt <= 1e-6 ? 0 : (_y[newest] - _y[reference]) / dt;
    }

    private void Add(double y, double time)
    {
        _lastTime = time;
        if (_count > 0)
        {
            var last = (_head - 1 + Capacity) % Capacity;
            if (time <= _t[last])
            {
                _y[last] = y;
                return;
            }
        }
        _t[_head] = time;
        _y[_head] = y;
        _head = (_head + 1) % Capacity;
        if (_count < Capacity)
        {
            _count++;
        }
    }

    /// <summary>
    /// The displayed offset of the top sheet for a raw pointer offset: 1:1 downward, rubber band upward. A sheet that
    /// cannot be dismissed resists both ways.
    /// </summary>
    public static double Display(double raw, double height, bool dismissible)
    {
        if (raw < 0 || !dismissible)
        {
            return RubberBand.Apply(raw, Math.Max(height, 1), Resistance);
        }
        return raw;
    }

    /// <summary>
    /// The release rule, in Arc's order: a downward flick faster than 450 px/s closes, an upward one faster than 450 px/s
    /// returns, otherwise the sheet closes once dragged more than 35% of its exit travel (height + 24).
    /// </summary>
    public static SheetStackRelease Decide(double offset, double velocity, double height, bool dismissible)
    {
        if (!dismissible)
        {
            return SheetStackRelease.Return;
        }
        if (velocity > DismissVelocity)
        {
            return SheetStackRelease.Dismiss;
        }
        if (velocity < -DismissVelocity)
        {
            return SheetStackRelease.Return;
        }
        return offset > DismissFraction * (height + Overshoot) ? SheetStackRelease.Dismiss : SheetStackRelease.Return;
    }
}
