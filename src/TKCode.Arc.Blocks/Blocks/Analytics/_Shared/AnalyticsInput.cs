using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.VisualTree;
using TKCode.Arc.Motion;
using TKCode.Arc.Primitives;

namespace TKCode.Arc;

/// <summary>
/// Attached settings for the public <see cref="SegmentedControl"/> when an Analytics block uses its 44px look: its pill
/// glides on Morph (bounce 0.16) by default, but every Analytics note measured a non-overshooting thumb (Smooth on MRR
/// waterfall, Cohort retention, Usage forecast, Horizon chart, Metric explorer). <c>AnalyticsSegmented.Spring="Smooth"</c>
/// (XAML in the Blocks assembly) or <c>AnalyticsSegmented.SetSpring(control, ArcSpring.Smooth)</c> sets the pill's
/// spring once the template is applied.
/// </summary>
internal static class AnalyticsSegmented
{
    /// <summary>The selection pill's spring (null leaves the control's default).</summary>
    public static readonly AttachedProperty<ArcSpring?> SpringProperty =
        AvaloniaProperty.RegisterAttached<SegmentedControl, ArcSpring?>("Spring", typeof(AnalyticsSegmented));

    static AnalyticsSegmented()
    {
        SpringProperty.Changed.AddClassHandler<SegmentedControl>((control, _) =>
        {
            control.TemplateApplied -= OnTemplateApplied;
            control.TemplateApplied += OnTemplateApplied;
            Apply(control);
        });
    }

    /// <summary>Reads the attached spring.</summary>
    public static ArcSpring? GetSpring(SegmentedControl control) => control.GetValue(SpringProperty);

    /// <summary>Sets the attached spring.</summary>
    public static void SetSpring(SegmentedControl control, ArcSpring? value) => control.SetValue(SpringProperty, value);

    private static void OnTemplateApplied(object? sender, TemplateAppliedEventArgs e)
    {
        if (sender is SegmentedControl control && GetSpring(control) is { } spring &&
            e.NameScope.Find<GlidingIndicator>(SegmentedControl.SelectionPart) is { } indicator)
        {
            indicator.Spring = spring;
        }
    }

    private static void Apply(SegmentedControl control)
    {
        if (GetSpring(control) is not { } spring)
        {
            return;
        }
        // Already templated: the pill sits inside the scroll viewer's content, found by walking the visual tree.
        foreach (var indicator in control.GetVisualDescendants().OfType<GlidingIndicator>())
        {
            if (indicator.Name == SegmentedControl.SelectionPart)
            {
                indicator.Spring = spring;
            }
        }
    }
}

/// <summary>
/// Keyboard stepping for inspectable plots (a cursor over n points or bars). The notes' common model: with no cursor,
/// → shows the last point and ← the one before it (Metrics dashboard, KPI drilldown, Uptime latency); Home / End jump to the
/// ends; PageUp / PageDown move by <c>page</c> (Uptime bars 7); ends clamp unless <c>wrap</c> (MRR waterfall bars wrap).
/// Escape is the caller's (clear the cursor).
/// </summary>
internal static class AnalyticsKeyboard
{
    /// <summary>
    /// The cursor after <paramref name="key"/>, or null when the key does not move it. <paramref name="current"/> −1 means
    /// no cursor. Left / Up step back, Right / Down forward (pass <paramref name="vertical"/> false to ignore Up / Down).
    /// </summary>
    public static int? Step(Key key, int current, int count, bool wrap = false, int page = 0, bool vertical = true)
    {
        if (count <= 0)
        {
            return null;
        }
        var last = count - 1;
        int delta;
        switch (key)
        {
            case Key.Home:
                return 0;
            case Key.End:
                return last;
            case Key.Left:
                delta = -1;
                break;
            case Key.Right:
                delta = 1;
                break;
            case Key.Up when vertical:
                delta = -1;
                break;
            case Key.Down when vertical:
                delta = 1;
                break;
            case Key.PageUp when page > 0:
                delta = -page;
                break;
            case Key.PageDown when page > 0:
                delta = page;
                break;
            default:
                return null;
        }
        if (current < 0 || current > last)
        {
            // No cursor yet: forward shows the last point, back the one before it.
            return delta > 0 ? last : Math.Max(0, last + Math.Max(delta, -last));
        }
        var next = current + delta;
        if (wrap)
        {
            return ((next % count) + count) % count;
        }
        return Math.Clamp(next, 0, last);
    }
}

/// <summary>Focus rings drawn by self-drawn Analytics plots (Arc: 2px FocusRing, 2px outside, following the radius).</summary>
internal static class AnalyticsFocus
{
    /// <summary>
    /// Draws a ring around <paramref name="rect"/>: <paramref name="offset"/> (2) outside, <paramref name="width"/> (2)
    /// wide, radius = <paramref name="radius"/> + offset. A negative offset draws inside (plots clipped by their block,
    /// where Arc's outside ring is cut off: Horizon chart, KPI tabs).
    /// </summary>
    public static void Draw(DrawingContext context, AnalyticsPalette palette, Rect rect, double radius = 0, double offset = 2, double width = 2)
    {
        var inflate = offset + width / 2;
        var ring = rect.Inflate(inflate);
        var r = Math.Max(0, radius + inflate);
        context.DrawRectangle(null, palette.Pen(palette.FocusRing, width, cap: PenLineCap.Flat, join: PenLineJoin.Miter), new RoundedRect(ring, r));
    }

    /// <summary>True when <paramref name="control"/> has keyboard focus that should show a ring (<c>:focus-visible</c>).</summary>
    public static bool IsVisible(Control control) => control.IsFocused && ((IPseudoClasses)control.Classes).Contains(":focus-visible");
}
