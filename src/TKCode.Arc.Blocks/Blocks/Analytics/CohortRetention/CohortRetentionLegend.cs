using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.VisualTree;
using TKCode.Arc.Motion;

namespace TKCode.Arc;

/// <summary>
/// The toolbar legend of a <see cref="CohortRetention"/>, which changes with the view. Heatmap: a title beside six 28×8
/// swatches 2 apart (outer corners 4, inner 2) with the thresholds centred on the gaps below them. Curves: a 16×2 Accent
/// swatch for the average and a 16×1 TextMuted one for the cohorts, each with its text. A view change swaps them: the old
/// legend rises 6px and fades while the new one rises from 6px below, both in 240ms (.16, 1, .3, 1); reduced motion
/// cross-fades in 120ms.
/// </summary>
/// <remarks>Why code-driven: two drawn legends overlap during the swap and only the current one takes space.</remarks>
internal sealed class CohortRetentionLegend : Control
{
    private const double SwatchWidth = 28;
    private const double SwatchHeight = 8;
    private const double SwatchGap = 2;
    private const double TopPad = 6;
    private const double RowGap = 4;
    private const double TickRow = 14;
    private const double ColumnGap = 12;
    private const double Travel = 6;

    private readonly AnalyticsPalette _palette = new();
    private readonly AnalyticsTextCache _text = new(64);
    private readonly LiveLoop _loop;
    private AnalyticsTween _swap = new(1);
    private CohortRetentionView _view;
    private CohortRetentionView _previous;
    private bool _paletteValid;
    private Color[] _ramp = [];
    private StreamGeometry[] _swatches = [];
    private double[] _weights = AnalyticsColors.CohortSteps;
    private string[] _ticks = ["20", "35", "50", "65", "80"];
    private bool _reduced;

    public CohortRetentionLegend()
    {
        _loop = new LiveLoop(this, Advance) { MinimumLevel = ArcMotionLevel.Reduced };
        ActualThemeVariantChanged += (_, _) => Invalidate();
        ResourcesChanged += (_, _) => Invalidate();
        IsHitTestVisible = false;
    }

    /// <summary>The heatmap legend's title ("% retained").</summary>
    public string Title { get; private set; } = "% retained";

    /// <summary>The average line's text.</summary>
    public string AverageText { get; private set; } = "Average, weighted by size";

    /// <summary>The cohort lines' text.</summary>
    public string CohortText { get; private set; } = "One line per cohort";

    /// <summary>The view whose legend shows.</summary>
    public CohortRetentionView View => _view;

    /// <summary>The swap's progress (1 at rest; tests).</summary>
    internal double SwapProgress => _swap.Value;

    /// <summary>Sets the texts, the thresholds and the ramp weights.</summary>
    public void SetContent(string title, string averageText, string cohortText, IReadOnlyList<double> thresholds, double[] weights)
    {
        Title = title;
        AverageText = averageText;
        CohortText = cohortText;
        _ticks = thresholds.Select(t => Math.Round(t * 100, 1).ToString("0.#", AnalyticsFormat.DefaultCulture)).ToArray();
        _weights = weights;
        _paletteValid = false;
        InvalidateMeasure();
        InvalidateVisual();
    }

    /// <summary>Shows the legend of <paramref name="view"/>, swapping when <paramref name="animate"/> and motion allows.</summary>
    public void SetView(CohortRetentionView view, bool animate)
    {
        if (view == _view)
        {
            return;
        }
        _previous = _view;
        _view = view;
        var level = ArcMotion.GetEffectiveLevel(this);
        if (animate && level != ArcMotionLevel.Off && this.IsAttachedToVisualTree())
        {
            _reduced = level != ArcMotionLevel.Full;
            _swap.Jump(0);
            _swap.Start(1, _reduced ? ArcMotionPresets.Instant : ArcMotionPresets.Standard, AnalyticsEasings.Enter);
            _loop.Start();
        }
        else
        {
            _swap.Jump(1);
        }
        InvalidateMeasure();
        InvalidateVisual();
        AutomationProperties.SetName(this, SpokenText);
    }

    /// <summary>What a screen reader hears for the current legend.</summary>
    public string SpokenText => _view == CohortRetentionView.Heatmap
        ? $"{Title}: darker cells kept more, steps start at {string.Join(", ", _ticks)}%"
        : $"{AverageText}; {CohortText}";

    private void Invalidate()
    {
        _paletteValid = false;
        _text.Clear();
        InvalidateMeasure();
        InvalidateVisual();
    }

    private bool Advance(double dt)
    {
        var moving = _swap.Advance(dt);
        InvalidateVisual();
        return moving;
    }

    private void EnsurePalette()
    {
        if (_paletteValid || !this.IsAttachedToVisualTree())
        {
            return;
        }
        _palette.Refresh(this);
        _ramp = AnalyticsColors.Ramp(_palette.Accent, _palette.Surface, _weights);
        _swatches = new StreamGeometry[_ramp.Length];
        for (var i = 0; i < _ramp.Length; i++)
        {
            var left = i == 0 ? 4.0 : 2.0;
            var right = i == _ramp.Length - 1 ? 4.0 : 2.0;
            var rect = new Rect(i * (SwatchWidth + SwatchGap), 0, SwatchWidth, SwatchHeight);
            _swatches[i] = AnalyticsGeometry.RoundedRect(rect, left, right, right, left);
        }
        _paletteValid = true;
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        EnsurePalette();
        return Measure(_view);
    }

    private Size Measure(CohortRetentionView view)
    {
        if (!_palette.IsReady)
        {
            return new Size(0, view == CohortRetentionView.Heatmap ? TopPad + SwatchHeight + RowGap + TickRow : 16.8);
        }
        var body = new Typeface(_palette.BodyFont);
        if (view == CohortRetentionView.Heatmap)
        {
            var title = _text.Get(Title, body, 12).Width;
            var bar = _weights.Length * SwatchWidth + Math.Max(0, _weights.Length - 1) * SwatchGap;
            return new Size(Math.Ceiling(title + ColumnGap + bar), TopPad + SwatchHeight + RowGap + TickRow);
        }
        var average = _text.Get(AverageText, body, 12).Width;
        var cohort = _text.Get(CohortText, body, 12).Width;
        return new Size(Math.Ceiling(16 + 8 + average + 16 + 16 + 8 + cohort), 16.8);
    }

    public override void Render(DrawingContext context)
    {
        EnsurePalette();
        if (!_palette.IsReady)
        {
            return;
        }
        var t = Math.Clamp(_swap.Value, 0, 1);
        if (t < 1)
        {
            var travel = _reduced ? 0 : Travel;
            DrawLegend(context, _previous, 1 - t, -travel * t);
            DrawLegend(context, _view, t, travel * (1 - t));
        }
        else
        {
            DrawLegend(context, _view, 1, 0);
        }
    }

    private void DrawLegend(DrawingContext context, CohortRetentionView view, double opacity, double dy)
    {
        if (opacity <= 0.001)
        {
            return;
        }
        var body = new Typeface(_palette.BodyFont);
        var size = Measure(view);
        // Each legend is centred on the toolbar row like the current one (the leaving one overlaps it).
        var top = (Bounds.Height - size.Height) / 2 + dy;
        using (context.PushOpacity(opacity))
        {
            if (view == CohortRetentionView.Heatmap)
            {
                var title = _text.Get(Title, body, 12);
                var titleWidth = title.Width;
                // Measured: the title sits on the swatch row's line, the thresholds below it.
                title.Draw(context, _palette.Brush(_palette.TextSecondary), new Point(0, top + TopPad + SwatchHeight / 2 - title.Height / 2));
                var x0 = Math.Ceiling(titleWidth) + ColumnGap;
                var swatchTop = top + TopPad;
                using (context.PushTransform(Matrix.CreateTranslation(x0, swatchTop)))
                {
                    for (var i = 0; i < _swatches.Length; i++)
                    {
                        context.DrawGeometry(_palette.Brush(_ramp[i]), null, _swatches[i]);
                    }
                }
                var tickY = swatchTop + SwatchHeight + RowGap + TickRow / 2;
                for (var k = 0; k < _ticks.Length; k++)
                {
                    var tick = _text.Get(_ticks[k], body, 11, true);
                    var cx = x0 + (k + 1) * (SwatchWidth + SwatchGap) - SwatchGap / 2;
                    tick.Draw(context, _palette.Brush(_palette.TextMuted), new Point(cx - tick.Width / 2, tickY - tick.Height / 2));
                }
            }
            else
            {
                var cy = top + size.Height / 2;
                context.DrawRectangle(_palette.Brush(_palette.Accent), null, new RoundedRect(new Rect(0, cy - 1, 16, 2), 1));
                var average = _text.Get(AverageText, body, 12);
                average.Draw(context, _palette.Brush(_palette.TextSecondary), new Point(24, cy - average.Height / 2));
                var x = 24 + average.Width + 16;
                context.FillRectangle(_palette.Brush(_palette.TextMuted), new Rect(x, cy - 0.5, 16, 1));
                var cohort = _text.Get(CohortText, body, 12);
                cohort.Draw(context, _palette.Brush(_palette.TextSecondary), new Point(x + 24, cy - cohort.Height / 2));
            }
        }
    }
}
