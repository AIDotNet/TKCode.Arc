using Avalonia;
using Avalonia.Animation.Easings;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using TKCode.Arc.Motion;

namespace TKCode.Arc;

/// <summary>
/// The scan that sweeps a page once while a model reads it (spec 5.2, first row). It covers the page and draws, in page
/// units, a trailing band <see cref="TrailHeight"/> tall that fades from transparent to the first series color at 9%, with
/// a <see cref="LineThickness"/> line at its lower edge in the series color at 65% that fades out at both ends (20% and 80% of
/// the width). Over <see cref="Duration"/> the band's lower edge travels from the top of the page to its bottom on the
/// in-out curve (Arc: translate −100% → 0), while its opacity rises over the first 12% and falls after 78%. One
/// <see cref="Play"/> is one sweep; the owner creates the beam only at full motion (spec 5.3: no scan when motion is reduced).
/// </summary>
/// <remarks>
/// Code-driven motion (<see cref="MotionValue{T}"/>): one value carries the sweep's linear time and the position and opacity
/// are read from it by their own keyframes every frame; a ControlTheme transition has no keyframes and cannot be replayed on
/// demand. The brushes are built once per sweep; a frame only moves two rectangles.
/// </remarks>
internal sealed class ScanBeam : Control
{
    /// <summary>One sweep.</summary>
    public static readonly TimeSpan Duration = TimeSpan.FromMilliseconds(1500);

    /// <summary>The trailing band's height in page units.</summary>
    public const double TrailHeight = 96;

    /// <summary>The line's thickness in page units.</summary>
    public const double LineThickness = 1.5;

    /// <summary>The band's strongest tint (at its lower edge).</summary>
    public const double TrailAlpha = 0.09;

    /// <summary>The line's tint.</summary>
    public const double LineAlpha = 0.65;

    /// <summary>The share of the sweep over which the beam fades in.</summary>
    public const double FadeInEnd = 0.12;

    /// <summary>The share of the sweep after which the beam fades out.</summary>
    public const double FadeOutStart = 0.78;

    private static readonly Easing s_linear = new LinearEasing();

    private MotionValue<double>? _time;
    private IBrush? _trail;
    private IBrush? _line;
    private double _progress = -1;
    private int _generation;

    static ScanBeam()
    {
        IsHitTestVisibleProperty.OverrideDefaultValue<ScanBeam>(false);
        FocusableProperty.OverrideDefaultValue<ScanBeam>(false);
    }

    /// <summary>Creates a beam (hidden from assistive technology).</summary>
    public ScanBeam()
    {
        AutomationProperties.SetAccessibilityView(this, AccessibilityView.Raw);
    }

    /// <summary>Raised when a sweep reaches its end (not when it is stopped).</summary>
    public event EventHandler? Finished;

    /// <summary>Whether a sweep is running.</summary>
    public bool IsSweeping => _progress >= 0;

    /// <summary>The linear time of the sweep, 0 to 1, or −1 when none runs (tests).</summary>
    internal double Progress => _progress;

    /// <summary>Where the band's lower edge is, as a share of the page's height, at <paramref name="time"/> (0 to 1).</summary>
    internal static double PositionAt(double time) => ArcEasings.InOut.Ease(Math.Clamp(time, 0, 1));

    /// <summary>The beam's opacity at <paramref name="time"/> (0 to 1): 0 → 1 by 12%, 1 until 78%, then → 0, each step in-out.</summary>
    internal static double OpacityAt(double time)
    {
        time = Math.Clamp(time, 0, 1);
        if (time < FadeInEnd)
        {
            return ArcEasings.InOut.Ease(time / FadeInEnd);
        }
        if (time <= FadeOutStart)
        {
            return 1;
        }
        return 1 - ArcEasings.InOut.Ease((time - FadeOutStart) / (1 - FadeOutStart));
    }

    /// <summary>Starts one sweep from the top (a running sweep starts over).</summary>
    public void Play()
    {
        BuildBrushes();
        if (_time is null)
        {
            _time = new MotionValue<double>(this, 0, MotionVectors.Double);
            _time.Changed += OnTime;
        }
        var generation = ++_generation;
        _time.Jump(0);
        _ = SweepAsync(_time.AnimateTo(1, MotionSpec.Tween(Duration, s_linear)), generation);
    }

    /// <summary>Ends the sweep at once.</summary>
    public void Stop()
    {
        _generation++;
        _time?.Stop();
        _progress = -1;
        InvalidateVisual();
    }

    private async Task SweepAsync(Task<bool> run, int generation)
    {
        var finished = await run;
        if (!finished || generation != _generation)
        {
            return;
        }
        _progress = -1;
        InvalidateVisual();
        Finished?.Invoke(this, EventArgs.Empty);
    }

    private void OnTime(double time)
    {
        _progress = time;
        InvalidateVisual();
    }

    /// <summary>Gradients in the first series color of the theme the beam is in, relative to the rectangles they fill.</summary>
    private void BuildBrushes()
    {
        var series = PromptTone.SeriesColor(this, 1);
        // Transparent ends keep the series color's own channels so the fade does not pass through gray.
        var clear = Color.FromArgb(0, series.R, series.G, series.B);
        _trail = new ImmutableLinearGradientBrush(
            [new ImmutableGradientStop(0, clear), new ImmutableGradientStop(1, PromptTone.WithOpacity(series, TrailAlpha))],
            startPoint: new RelativePoint(0, 0, RelativeUnit.Relative),
            endPoint: new RelativePoint(0, 1, RelativeUnit.Relative));
        var line = PromptTone.WithOpacity(series, LineAlpha);
        _line = new ImmutableLinearGradientBrush(
            [
                new ImmutableGradientStop(0, clear), new ImmutableGradientStop(0.2, line),
                new ImmutableGradientStop(0.8, line), new ImmutableGradientStop(1, clear)
            ],
            startPoint: new RelativePoint(0, 0, RelativeUnit.Relative),
            endPoint: new RelativePoint(1, 0, RelativeUnit.Relative));
    }

    public override void Render(DrawingContext context)
    {
        if (_progress < 0 || _trail is null || _line is null)
        {
            return;
        }
        var opacity = OpacityAt(_progress);
        if (opacity <= 0)
        {
            return;
        }
        var size = Bounds.Size;
        var bottom = PositionAt(_progress) * size.Height;
        using (context.PushClip(new Rect(size)))
        using (context.PushOpacity(opacity))
        {
            context.FillRectangle(_trail, new Rect(0, bottom - TrailHeight, size.Width, TrailHeight));
            context.FillRectangle(_line, new Rect(0, bottom - LineThickness, size.Width, LineThickness));
        }
    }
}
