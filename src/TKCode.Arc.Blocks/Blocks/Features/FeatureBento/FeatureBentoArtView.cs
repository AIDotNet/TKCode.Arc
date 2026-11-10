using System.Globalization;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using Avalonia.Media.TextFormatting;
using TKCode.Arc.Motion;

namespace TKCode.Arc;

/// <summary>
/// The live drawing of a <see cref="FeatureBento"/> card. One control draws every <see cref="FeatureBentoArt"/>; a
/// <see cref="LiveLoop"/> advances its clock only while the card is on screen, visible and at full motion, so reduced
/// motion and off show a still frame. Pens, brushes and the two lines of terminal text are cached when the colors or the
/// size change; a frame draws lines, ellipses and rectangles and allocates nothing.
/// </summary>
internal sealed class FeatureBentoArtView : Control
{
    public static readonly StyledProperty<FeatureBentoArt> ArtProperty =
        AvaloniaProperty.Register<FeatureBentoArtView, FeatureBentoArt>(nameof(Art));

    public static readonly StyledProperty<IBrush?> AccentBrushProperty =
        AvaloniaProperty.Register<FeatureBentoArtView, IBrush?>(nameof(AccentBrush));

    public static readonly StyledProperty<IBrush?> InkBrushProperty =
        AvaloniaProperty.Register<FeatureBentoArtView, IBrush?>(nameof(InkBrush));

    public static readonly StyledProperty<IBrush?> LineBrushProperty =
        AvaloniaProperty.Register<FeatureBentoArtView, IBrush?>(nameof(LineBrush));

    public static readonly StyledProperty<IBrush?> PanelBrushProperty =
        AvaloniaProperty.Register<FeatureBentoArtView, IBrush?>(nameof(PanelBrush));

    public static readonly StyledProperty<FontFamily?> MonoFontProperty =
        AvaloniaProperty.Register<FeatureBentoArtView, FontFamily?>(nameof(MonoFont));

    public static readonly StyledProperty<string> CommandTextProperty =
        AvaloniaProperty.Register<FeatureBentoArtView, string>(nameof(CommandText), "$ ship deploy --prod");

    public static readonly StyledProperty<string> ResultTextProperty =
        AvaloniaProperty.Register<FeatureBentoArtView, string>(nameof(ResultText), "✓ live in 38s");

    private const int PulsePoints = 40;
    private const int BarCount = 14;
    private static readonly double[] s_orbitSpeeds = [0.6, -0.42, 0.28];
    private LiveLoop? _loop;
    private double _time = 1.7;
    private IPen? _accentPen;
    private IPen? _accentThin;
    private IPen? _linePen;
    private IBrush? _accentSoft;
    private TextLayout? _command;
    private TextLayout? _result;

    static FeatureBentoArtView()
    {
        AffectsRender<FeatureBentoArtView>(ArtProperty);
        IsHitTestVisibleProperty.OverrideDefaultValue<FeatureBentoArtView>(false);
    }

    public FeatureBentoArtView()
    {
        AutomationProperties.SetAccessibilityView(this, AccessibilityView.Raw);
    }

    public FeatureBentoArt Art { get => GetValue(ArtProperty); set => SetValue(ArtProperty, value); }

    public IBrush? AccentBrush { get => GetValue(AccentBrushProperty); set => SetValue(AccentBrushProperty, value); }

    public IBrush? InkBrush { get => GetValue(InkBrushProperty); set => SetValue(InkBrushProperty, value); }

    public IBrush? LineBrush { get => GetValue(LineBrushProperty); set => SetValue(LineBrushProperty, value); }

    public IBrush? PanelBrush { get => GetValue(PanelBrushProperty); set => SetValue(PanelBrushProperty, value); }

    public FontFamily? MonoFont { get => GetValue(MonoFontProperty); set => SetValue(MonoFontProperty, value); }

    public string CommandText { get => GetValue(CommandTextProperty); set => SetValue(CommandTextProperty, value); }

    public string ResultText { get => GetValue(ResultTextProperty); set => SetValue(ResultTextProperty, value); }

    /// <summary>The clock, in seconds (tests read it).</summary>
    internal double Time => _time;

    /// <summary>True while the loop advances the clock.</summary>
    internal bool IsLooping => _loop?.IsRunning == true;

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        if (_loop is null)
        {
            _loop = new LiveLoop(this, Step);
            _loop.RunningChanged += _ => InvalidateVisual();
        }
        UpdateLoop();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        _loop?.Stop();
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == AccentBrushProperty || change.Property == LineBrushProperty || change.Property == InkBrushProperty
            || change.Property == MonoFontProperty || change.Property == CommandTextProperty || change.Property == ResultTextProperty)
        {
            _accentPen = null;
            DisposeText();
            InvalidateVisual();
        }
        else if (change.Property == ArtProperty)
        {
            UpdateLoop();
        }
    }

    private void UpdateLoop()
    {
        if (_loop is null)
        {
            return;
        }
        if (Art == FeatureBentoArt.None)
        {
            _loop.Stop();
        }
        else
        {
            _loop.Start();
        }
    }

    private bool Step(double seconds)
    {
        _time += Math.Min(seconds, 0.1);
        InvalidateVisual();
        return true;
    }

    public override void Render(DrawingContext context)
    {
        var size = Bounds.Size;
        if (size.Width <= 0 || size.Height <= 0 || Art == FeatureBentoArt.None)
        {
            return;
        }
        EnsurePens();
        switch (Art)
        {
            case FeatureBentoArt.Pulse:
                DrawPulse(context, size);
                break;
            case FeatureBentoArt.Orbit:
                DrawOrbit(context, size);
                break;
            case FeatureBentoArt.Typing:
                DrawTyping(context, size);
                break;
            case FeatureBentoArt.Bars:
                DrawBars(context, size);
                break;
            case FeatureBentoArt.Sync:
                DrawSync(context, size);
                break;
            case FeatureBentoArt.Shield:
                DrawShield(context, size);
                break;
        }
    }

    private static double Wave(double x, double t)
        => 0.5 + 0.22 * Math.Sin(x * 6.3 + t * 1.6) + 0.12 * Math.Sin(x * 13.1 - t * 2.3) + 0.06 * Math.Sin(x * 29 + t * 3.1);

    private void DrawPulse(DrawingContext context, Size size)
    {
        var h = size.Height;
        var w = size.Width;
        for (var i = 1; i <= 3; i++)
        {
            var y = h * i / 4;
            context.DrawLine(_linePen!, new Point(0, y), new Point(w, y));
        }
        var previous = default(Point);
        for (var i = 0; i < PulsePoints; i++)
        {
            var x = (double)i / (PulsePoints - 1);
            var point = new Point(x * w, h * (1 - Wave(x, _time)));
            if (i > 0)
            {
                context.DrawLine(_accentPen!, previous, point);
            }
            previous = point;
        }
        context.DrawEllipse(_accentSoft, null, previous, 9, 9);
        context.DrawEllipse(AccentBrush, null, previous, 4, 4);
    }

    private void DrawOrbit(DrawingContext context, Size size)
    {
        var center = new Point(size.Width / 2, size.Height / 2);
        var reach = Math.Min(size.Width, size.Height) / 2 - 8;
        if (reach <= 8)
        {
            return;
        }
        for (var ring = 0; ring < 3; ring++)
        {
            var r = reach * (0.42 + 0.29 * ring);
            context.DrawEllipse(null, _linePen, center, r, r);
            for (var k = 0; k < ring + 2; k++)
            {
                var angle = _time * s_orbitSpeeds[ring] + k * Math.PI * 2 / (ring + 2) + ring;
                var dot = new Point(center.X + Math.Cos(angle) * r, center.Y + Math.Sin(angle) * r);
                context.DrawEllipse(PanelBrush, _accentThin, dot, 5, 5);
            }
        }
        context.DrawEllipse(_accentSoft, null, center, reach * 0.26, reach * 0.26);
        context.DrawEllipse(AccentBrush, null, center, reach * 0.14, reach * 0.14);
    }

    private void DrawTyping(DrawingContext context, Size size)
    {
        EnsureText();
        if (_command is null || _result is null)
        {
            return;
        }
        // A 5 s cycle: type for 2.2 s, show the answer, hold, start again.
        var cycle = _time % 5.0;
        var typed = Math.Clamp(cycle / 2.2, 0, 1);
        var x = 4.0;
        var y = Math.Max(0, (size.Height - _command.Height * 2 - 10) / 2);
        var shown = _command.WidthIncludingTrailingWhitespace * typed;
        using (context.PushClip(new Rect(x, y, shown, _command.Height)))
        {
            _command.Draw(context, new Point(x, y));
        }
        var blink = typed < 1 || ((int)(cycle * 2.4) & 1) == 0;
        if (blink && typed < 1)
        {
            context.FillRectangle(AccentBrush!, new Rect(x + shown + 1, y + 2, 7, _command.Height - 4));
        }
        if (cycle > 2.6)
        {
            var fade = Math.Clamp((cycle - 2.6) / 0.3, 0, 1);
            using (context.PushOpacity(fade))
            {
                _result.Draw(context, new Point(x, y + _command.Height + 10));
            }
        }
    }

    private void DrawBars(DrawingContext context, Size size)
    {
        var gap = 6.0;
        var barWidth = Math.Max(2, (size.Width - gap * (BarCount - 1)) / BarCount);
        for (var i = 0; i < BarCount; i++)
        {
            var level = 0.25 + 0.7 * Math.Abs(Math.Sin(_time * (0.9 + i * 0.07) + i * 0.8)) * (0.6 + 0.4 * Math.Sin(i * 0.5));
            var height = Math.Max(4, size.Height * Math.Clamp(level, 0.08, 1));
            var rect = new Rect(i * (barWidth + gap), size.Height - height, barWidth, height);
            context.DrawRectangle(i % 3 == 2 ? AccentBrush : _accentSoft, null, new RoundedRect(rect, Math.Min(3, barWidth / 2)));
        }
    }

    private void DrawSync(DrawingContext context, Size size)
    {
        var deviceW = Math.Min(70, size.Width * 0.22);
        var deviceH = Math.Min(size.Height - 8, deviceW * 1.25);
        var top = (size.Height - deviceH) / 2;
        var left = new Rect(4, top, deviceW, deviceH);
        var right = new Rect(size.Width - deviceW - 4, top, deviceW, deviceH);
        context.DrawRectangle(PanelBrush, _linePen, new RoundedRect(left, 10));
        context.DrawRectangle(PanelBrush, _linePen, new RoundedRect(right, 10));
        for (var i = 0; i < 3; i++)
        {
            var lineY = top + 16 + i * 10;
            context.DrawLine(_linePen!, new Point(left.X + 12, lineY), new Point(left.Right - 12 - i * 8, lineY));
            context.DrawLine(_linePen!, new Point(right.X + 12, lineY), new Point(right.Right - 12 - i * 6, lineY));
        }
        var y1 = size.Height / 2 - 7;
        var y2 = size.Height / 2 + 7;
        var from = left.Right + 10;
        var to = right.X - 10;
        if (to <= from)
        {
            return;
        }
        context.DrawLine(_linePen!, new Point(from, y1), new Point(to, y1));
        context.DrawLine(_linePen!, new Point(from, y2), new Point(to, y2));
        for (var k = 0; k < 3; k++)
        {
            var a = (_time * 0.45 + k / 3.0) % 1.0;
            var b = (_time * 0.38 + k / 3.0 + 0.17) % 1.0;
            context.DrawRectangle(AccentBrush, null, new RoundedRect(new Rect(from + (to - from) * a - 6, y1 - 2.5, 12, 5), 2.5));
            context.DrawRectangle(_accentSoft, null, new RoundedRect(new Rect(to - (to - from) * b - 6, y2 - 2.5, 12, 5), 2.5));
        }
    }

    private void DrawShield(DrawingContext context, Size size)
    {
        var edge = Math.Min(size.Width, size.Height) - 8;
        if (edge <= 16)
        {
            return;
        }
        var center = new Point(size.Width / 2, size.Height / 2);
        // Two rings widen from the center and fade as they go.
        for (var k = 0; k < 2; k++)
        {
            var phase = (_time * 0.35 + k * 0.5) % 1.0;
            using (context.PushOpacity(1 - phase))
            {
                context.DrawEllipse(null, _accentThin, center, edge * (0.25 + 0.35 * phase), edge * (0.25 + 0.35 * phase));
            }
        }
        var scale = edge * 0.7 / 24;
        var matrix = Matrix.CreateTranslation(-12, -12) * Matrix.CreateScale(scale, scale) * Matrix.CreateTranslation(center.X, center.Y);
        using (context.PushTransform(matrix))
        {
            context.DrawGeometry(_accentSoft, _accentPen, ArcIcons.Shield);
        }
        var scan = (Math.Sin(_time * 1.4) + 1) / 2;
        var half = edge * 0.35;
        var y = center.Y - half + scan * half * 2;
        context.DrawLine(_accentThin!, new Point(center.X - half * 0.8, y), new Point(center.X + half * 0.8, y));
    }

    private void EnsurePens()
    {
        if (_accentPen is not null)
        {
            return;
        }
        var accent = (AccentBrush as ISolidColorBrush)?.Color ?? Colors.SteelBlue;
        var line = (LineBrush as ISolidColorBrush)?.Color ?? Colors.LightGray;
        _accentPen = new ImmutablePen(new ImmutableSolidColorBrush(accent), 2, lineCap: PenLineCap.Round, lineJoin: PenLineJoin.Round);
        _accentThin = new ImmutablePen(new ImmutableSolidColorBrush(accent), 1.25);
        _linePen = new ImmutablePen(new ImmutableSolidColorBrush(line), 1);
        _accentSoft = new ImmutableSolidColorBrush(Oklch.WithAlpha(accent, 0.22));
    }

    private void EnsureText()
    {
        if (_command is not null)
        {
            return;
        }
        var typeface = new Typeface(MonoFont ?? FontFamily.Default);
        _command = new TextLayout(CommandText, typeface, 13, InkBrush);
        _result = new TextLayout(ResultText, typeface, 13, AccentBrush);
    }

    private void DisposeText()
    {
        _command?.Dispose();
        _result?.Dispose();
        _command = null;
        _result = null;
    }
}
