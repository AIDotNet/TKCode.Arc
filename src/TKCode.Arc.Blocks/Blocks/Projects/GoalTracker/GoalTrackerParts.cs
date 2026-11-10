using TKCode.Arc.DesignTokens;
using System.Globalization;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Automation.Provider;
using Avalonia.Controls;
using Avalonia.Media;
using TKCode.Arc.Motion;

namespace TKCode.Arc;

/// <summary>
/// A progress bar with a mark where the result should be by today: a muted track, a fill in the confidence color that
/// springs to its value (Smooth; Reduced and Off place it at once), and a 2px tick at <see cref="Expected"/>.
/// </summary>
internal sealed class GoalTrackerBar : Control
{
    private readonly AnalyticsPalette _palette = new();
    private MotionValue<double>? _shown;
    private double _shownValue;
    private double _value;
    private bool _placed;

    public GoalTrackerBar()
    {
        Height = 6;
        ActualThemeVariantChanged += (_, _) => Repaint();
        ResourcesChanged += (_, _) => Repaint();
    }

    /// <summary>The progress, 0 to 1.</summary>
    public double Value
    {
        get => _value;
        set
        {
            _value = Math.Clamp(value, 0, 1);
            if (!_placed || !IsAttachedToVisualTree())
            {
                _shownValue = _value;
                InvalidateVisual();
                return;
            }
            _shown ??= CreateMotion();
            _ = _shown.AnimateTo(_value, MotionSpec.Spring(ArcMotionPresets.Smooth));
        }
    }

    /// <summary>Where the mark sits, 0 to 1; NaN draws none.</summary>
    public double Expected { get; set; } = double.NaN;

    public GoalTrackerConfidence Confidence { get; set; }

    /// <summary>What a screen reader says the bar is.</summary>
    public string? Label { get; set; }

    internal double ShownValue => _shownValue;

    private bool IsAttachedToVisualTree() => VisualRoot is not null;

    private MotionValue<double> CreateMotion()
    {
        var motion = new MotionValue<double>(this, _shownValue, MotionVectors.Double);
        motion.Changed += v =>
        {
            _shownValue = v;
            InvalidateVisual();
        };
        return motion;
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        _placed = true;
    }

    private void Repaint()
    {
        if (_palette.IsReady)
        {
            _palette.Refresh(this);
        }
        InvalidateVisual();
    }

    public static Color ColorOf(AnalyticsPalette p, GoalTrackerConfidence confidence) => confidence switch
    {
        GoalTrackerConfidence.OnTrack => p.Success,
        GoalTrackerConfidence.AtRisk => p.Warning,
        _ => p.Danger,
    };

    public override void Render(DrawingContext context)
    {
        if (!_palette.IsReady)
        {
            _palette.Refresh(this);
        }
        var p = _palette;
        var w = Bounds.Width;
        var h = Bounds.Height;
        var r = h / 2;
        context.DrawRectangle(p.Brush(p.SurfaceMuted), null, new Rect(0, 0, w, h), r, r);
        if (_shownValue > 0.001)
        {
            context.DrawRectangle(p.Brush(ColorOf(p, Confidence)), null, new Rect(0, 0, Math.Max(h, w * _shownValue), h), r, r);
        }
        if (!double.IsNaN(Expected) && w >= 4)
        {
            var x = Math.Clamp(w * Expected, 1, w - 1);
            context.FillRectangle(p.Brush(p.Foreground, 0.7), new Rect(x - 1, -3, 2, h + 6));
        }
    }

    protected override AutomationPeer OnCreateAutomationPeer() => new GoalTrackerBarPeer(this);
}

/// <summary>Reports the bar as a read-only progress bar from 0 to 100.</summary>
internal sealed class GoalTrackerBarPeer(GoalTrackerBar owner) : ControlAutomationPeer(owner), IRangeValueProvider
{
    private GoalTrackerBar Bar => (GoalTrackerBar)Owner;

    protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.ProgressBar;

    protected override string? GetNameCore() => Bar.Label;

    protected override bool IsContentElementCore() => true;

    public bool IsReadOnly => true;

    public double Minimum => 0;

    public double Maximum => 100;

    public double Value => Math.Round(Bar.Value * 100);

    public double SmallChange => 1;

    public double LargeChange => 10;

    public void SetValue(double value) => throw new InvalidOperationException("The progress bar is read only.");
}

/// <summary>
/// The timeline view: a row per key result with its title in a gutter, the straight line from start to target across the
/// period (dashed), the check-ins as dots joined by a line in each one's confidence color, and a today line.
/// </summary>
internal sealed class GoalTrackerTimeline : Control
{
    private readonly AnalyticsPalette _palette = new();
    private readonly AnalyticsTextCache _text = new(128);
    private IReadOnlyList<GoalTrackerKeyResult> _rows = Array.Empty<GoalTrackerKeyResult>();

    public GoalTrackerTimeline()
    {
        ActualThemeVariantChanged += (_, _) => Repaint();
        ResourcesChanged += (_, _) => Repaint();
    }

    public DateTime Start { get; set; }

    public DateTime End { get; set; }

    public DateTime Today { get; set; }

    public double RowHeight { get; set; } = 30;

    public double Gutter { get; set; } = 200;

    public string TodayLabel { get; set; } = "Today";

    public IReadOnlyList<GoalTrackerKeyResult> Rows
    {
        get => _rows;
        set
        {
            _rows = value;
            InvalidateMeasure();
            InvalidateVisual();
        }
    }

    private void Repaint()
    {
        _text.Clear();
        if (_palette.IsReady)
        {
            _palette.Refresh(this);
        }
        InvalidateVisual();
    }

    protected override Size MeasureOverride(Size availableSize)
        => new(double.IsFinite(availableSize.Width) ? availableSize.Width : 600, 22 + Math.Max(1, _rows.Count) * RowHeight);

    private double X(DateTime date)
    {
        var span = Math.Max(1, (End - Start).TotalDays);
        var left = Math.Min(Gutter, Bounds.Width * 0.4);
        return left + (Bounds.Width - left - 8) * Math.Clamp((date - Start).TotalDays / span, 0, 1);
    }

    public override void Render(DrawingContext context)
    {
        if (!_palette.IsReady)
        {
            _palette.Refresh(this);
        }
        var p = _palette;
        var typeface = new Typeface(p.BodyFont);
        var left = Math.Min(Gutter, Bounds.Width * 0.4);
        var top = 22.0;
        var bottom = top + _rows.Count * RowHeight;

        // Month ticks along the top.
        var month = new DateTime(Start.Year, Start.Month, 1);
        while (month <= End)
        {
            if (month >= Start)
            {
                var x = X(month);
                context.FillRectangle(p.Brush(p.BorderSubtle), new Rect(x, top - 4, 1, bottom - top + 4));
                _text.Get(month.ToString("MMM", CultureInfo.CurrentCulture), typeface, 11).Draw(context, p.Brush(p.TextMuted), new Point(x + 4, 2));
            }
            month = month.AddMonths(1);
        }

        for (var i = 0; i < _rows.Count; i++)
        {
            var kr = _rows[i];
            var y0 = top + i * RowHeight;
            var mid = y0 + RowHeight / 2;
            var y = (double fraction) => y0 + RowHeight - 4 - (RowHeight - 8) * fraction;
            if (i > 0)
            {
                context.FillRectangle(p.Brush(p.BorderSubtle), new Rect(0, y0, Bounds.Width, 1));
            }
            var label = _text.GetTrimmed(kr.Title, typeface, 12, left - 12);
            label.Draw(context, p.Brush(p.TextSecondary), new Point(0, mid - label.Height / 2));

            // The straight line to the target.
            context.DrawLine(p.Pen(p.BorderStrong, 1, dash: 3, gap: 3), new Point(X(Start), y(0)), new Point(X(End), y(1)));

            // The check-ins.
            var previous = (Point?)new Point(X(Start), y(0));
            foreach (var check in kr.CheckIns.OrderBy(c => c.Date))
            {
                var point = new Point(X(check.Date), y(kr.ProgressOf(check.Value)));
                var color = GoalTrackerBar.ColorOf(p, check.Confidence);
                if (previous is { } from)
                {
                    context.DrawLine(p.Pen(color, 2), from, point);
                }
                context.DrawEllipse(p.Brush(color), p.Pen(p.Surface, 1.5), point, 4, 4);
                previous = point;
            }
        }

        // Today.
        if (Today >= Start && Today <= End)
        {
            var x = X(Today);
            context.FillRectangle(p.Brush(p.Accent), new Rect(x - 0.75, top - 4, 1.5, bottom - top + 4));
            var t = _text.Get(TodayLabel, typeface, 11);
            t.Draw(context, p.Brush(p.Accent), new Point(Math.Min(x + 4, Bounds.Width - t.Width), 2));
        }
    }
}

/// <summary>Builders for the code-built parts of <see cref="GoalTracker"/>.</summary>
internal static class GoalTrackerUi
{
    public static TextBlock Text(string? text, ArcDesignTokenKind color = ArcDesignTokenKind.Foreground,
        ArcDesignTokenKind size = ArcDesignTokenKind.TypeSM, FontWeight weight = FontWeight.Normal, double lineHeight = 19.6, bool wrap = true)
    {
        var block = new TextBlock
        {
            Text = text,
            FontWeight = weight,
            LineHeight = lineHeight,
            TextWrapping = wrap ? TextWrapping.Wrap : TextWrapping.NoWrap,
            TextTrimming = wrap ? TextTrimming.None : TextTrimming.CharacterEllipsis,
        };
        ArcTokens.Bind(block, TextBlock.ForegroundProperty, color);
        ArcTokens.Bind(block, TextBlock.FontSizeProperty, size);
        return block;
    }

    public static Badge Confidence(GoalTrackerConfidence confidence, GoalTrackerLabels labels) => new()
    {
        Content = (int)confidence < labels.Confidences.Count ? labels.Confidences[(int)confidence] : confidence.ToString(),
        Size = BadgeSize.Small,
        Tone = confidence switch { GoalTrackerConfidence.OnTrack => BadgeTone.Success, GoalTrackerConfidence.AtRisk => BadgeTone.Warning, _ => BadgeTone.Danger },
    };

    public static string Number(double value) => value.ToString(Math.Abs(value) < 10 && value % 1 != 0 ? "0.##" : "#,0.#", CultureInfo.CurrentCulture);
}
