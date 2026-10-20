using TKCode.Arc.Motion;
using Avalonia;
using Avalonia.Animation.Easings;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using Avalonia.Media.TextFormatting;

namespace TKCode.Arc;

/// <summary>
/// A calm light sweeping across a short status line (Arc TextShimmer), such as "Thinking" or "Generating summary":
/// a bell-shaped band of the text color crosses a muted base, then pauses briefly before the next pass (P21). The
/// sweep runs only while <see cref="IsActive"/>, within 64px of the viewport and visible, and resumes where it
/// stopped. Turning <see cref="IsActive"/> off lets a band in flight glide off while the text settles to solid. A new
/// label rises in place from a soft blur while the line springs to its width (P2, P5). Drawn as one control: the band
/// is the text drawn again through a sliding opacity mask, so a frame allocates nothing.
/// </summary>
public class TextShimmer : Control
{
    /// <summary>The status text; keep it to one short line. A new value rises in place.</summary>
    public static readonly StyledProperty<string?> TextProperty =
        AvaloniaProperty.Register<TextShimmer, string?>(nameof(Text));

    /// <summary>Sweeps while work is ongoing (Arc <c>active</c>, true).</summary>
    public static readonly StyledProperty<bool> IsActiveProperty =
        AvaloniaProperty.Register<TextShimmer, bool>(nameof(IsActive), true);

    /// <summary>One pass across the text (Arc <c>duration</c>, 1.8s).</summary>
    public static readonly StyledProperty<TimeSpan> SweepDurationProperty =
        AvaloniaProperty.Register<TextShimmer, TimeSpan>(nameof(SweepDuration), TimeSpan.FromSeconds(1.8));

    /// <summary>Resting base color; when unset it is 12% of the text color mixed into muted text.</summary>
    public static readonly StyledProperty<IBrush?> BaseForegroundProperty =
        AvaloniaProperty.Register<TextShimmer, IBrush?>(nameof(BaseForeground));

    internal static readonly StyledProperty<IBrush?> MutedForegroundProperty =
        AvaloniaProperty.Register<TextShimmer, IBrush?>(nameof(MutedForeground));

    internal static readonly StyledProperty<double> BaseMixProperty =
        AvaloniaProperty.Register<TextShimmer, double>(nameof(BaseMix), 0.12);

    public static readonly StyledProperty<FontFamily> FontFamilyProperty = TextElement.FontFamilyProperty.AddOwner<TextShimmer>();
    public static readonly StyledProperty<double> FontSizeProperty = TextElement.FontSizeProperty.AddOwner<TextShimmer>();
    public static readonly StyledProperty<FontWeight> FontWeightProperty = TextElement.FontWeightProperty.AddOwner<TextShimmer>();
    public static readonly StyledProperty<IBrush?> ForegroundProperty = TextElement.ForegroundProperty.AddOwner<TextShimmer>();

    public string? Text { get => GetValue(TextProperty); set => SetValue(TextProperty, value); }
    public bool IsActive { get => GetValue(IsActiveProperty); set => SetValue(IsActiveProperty, value); }
    public TimeSpan SweepDuration { get => GetValue(SweepDurationProperty); set => SetValue(SweepDurationProperty, value); }
    public IBrush? BaseForeground { get => GetValue(BaseForegroundProperty); set => SetValue(BaseForegroundProperty, value); }
    internal IBrush? MutedForeground { get => GetValue(MutedForegroundProperty); set => SetValue(MutedForegroundProperty, value); }
    internal double BaseMix { get => GetValue(BaseMixProperty); set => SetValue(BaseMixProperty, value); }
    public FontFamily FontFamily { get => GetValue(FontFamilyProperty); set => SetValue(FontFamilyProperty, value); }
    public double FontSize { get => GetValue(FontSizeProperty); set => SetValue(FontSizeProperty, value); }
    public FontWeight FontWeight { get => GetValue(FontWeightProperty); set => SetValue(FontWeightProperty, value); }
    public IBrush? Foreground { get => GetValue(ForegroundProperty); set => SetValue(ForegroundProperty, value); }

    /// <summary>Half width of the band in em.</summary>
    internal const double BandEm = 2.2;

    private sealed class Label
    {
        public required string Text { get; init; }
        public TextLayout? Base { get; set; }
        public TextLayout? Highlight { get; set; }
        public MotionValue<double>? Presence { get; set; }
        public bool Leaving { get; set; }
        public bool Full { get; set; }
        public double StartOpacity { get; set; } = 1;
        public double StartOffset { get; set; }
    }

    private Label? _current;
    private readonly List<Label> _leaving = new();
    private readonly TextEffectsLoop _loop;
    private double _sweep;
    private double _pause;
    private bool _started;
    private MotionValue<double>? _glide;
    private MotionValue<double>? _settle;
    private MotionValue<double>? _width;
    private bool _inViewport = true;
    private IBrush? _band;
    private double _bandEm;
    private bool _layoutDirty = true;
    private static readonly Easing s_linear = new LinearEasing();

    static TextShimmer()
    {
        AffectsMeasure<TextShimmer>(FontFamilyProperty, FontSizeProperty, FontWeightProperty);
        AutomationProperties.ControlTypeOverrideProperty.OverrideDefaultValue<TextShimmer>(AutomationControlType.Text);
    }

    public TextShimmer()
    {
        _loop = new TextEffectsLoop(this, Step);
        EffectiveViewportChanged += (_, e) =>
        {
            var bounds = new Rect(Bounds.Size).Inflate(64);
            var visible = Bounds.Width <= 0 || e.EffectiveViewport.Intersects(bounds);
            if (visible != _inViewport)
            {
                _inViewport = visible;
                UpdateLoop();
            }
        };
    }

    /// <summary>Sweep position: 0 left of the text, 1 right of it.</summary>
    internal double Sweep => _sweep;

    /// <summary>0 = shimmering base, 1 = solid text.</summary>
    internal double Settle => _settle?.Value ?? (IsActive ? 0 : 1);

    internal bool IsSweeping => _loop.IsRunning;

    /// <summary>Band center for sweep <paramref name="t"/> over a text <paramref name="width"/> wide (Arc: t·100% + (2t − 1)·2.2em).</summary>
    internal static double BandCenter(double t, double width, double em) => t * width + (2 * t - 1) * BandEm * em;

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == TextProperty)
        {
            AutomationProperties.SetName(this, Text ?? string.Empty);
            Swap(Text ?? string.Empty);
        }
        else if (change.Property == IsActiveProperty)
        {
            AutomationProperties.SetItemStatus(this, IsActive ? "Busy" : string.Empty);
            OnActiveChanged();
        }
        else if (change.Property == ForegroundProperty || change.Property == BaseForegroundProperty ||
                 change.Property == MutedForegroundProperty || change.Property == BaseMixProperty ||
                 change.Property == FontFamilyProperty ||
                 change.Property == FontSizeProperty || change.Property == FontWeightProperty)
        {
            _layoutDirty = true;
            InvalidateMeasure();
            InvalidateVisual();
        }
        else if (change.Property == IsVisibleProperty || change.Property == ArcMotion.LevelProperty ||
                 change.Property == SweepDurationProperty)
        {
            UpdateLoop();
        }
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        AutomationProperties.SetItemStatus(this, IsActive ? "Busy" : string.Empty);
        UpdateLoop();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        _loop.Stop();
    }

    private void UpdateLoop()
    {
        var level = ArcMotion.GetEffectiveLevel(this);
        if (level != ArcMotionLevel.Full)
        {
            _sweep = 0;
        }
        var run = IsActive && level == ArcMotionLevel.Full && IsVisible && _inViewport &&
                  TopLevel.GetTopLevel(this) is not null && _glide?.IsAnimating != true;
        if (run)
        {
            if (!_loop.IsRunning && !_started)
            {
                // The first pass waits a beat; a paused band resumes where it stopped.
                _pause = _sweep > 0 ? 0 : 0.12;
                _started = true;
            }
            _loop.Start();
        }
        else
        {
            _loop.Stop();
            InvalidateVisual();
        }
    }

    private bool Step(double dt)
    {
        var duration = Math.Max(SweepDuration.TotalSeconds, 0.05);
        if (_pause > 0)
        {
            _pause -= dt;
            if (_pause > 0)
            {
                return true;
            }
            dt = -_pause;
            _pause = 0;
            if (_sweep >= 1)
            {
                _sweep = 0;
            }
        }
        _sweep = Math.Min(1, _sweep + dt / duration);
        if (_sweep >= 1)
        {
            // Rest between passes with the band off to the right, then restart from the left.
            _pause = duration * 0.32;
        }
        InvalidateVisual();
        return true;
    }

    private void OnActiveChanged()
    {
        var level = ArcMotion.GetEffectiveLevel(this);
        _settle ??= CreateValue(IsActive ? 1 : 0, measure: false);
        if (!IsLoaded || level == ArcMotionLevel.Off)
        {
            _settle.Jump(IsActive ? 0 : 1);
            if (!IsActive)
            {
                _sweep = 0;
            }
            UpdateLoop();
            return;
        }
        var full = level == ArcMotionLevel.Full;
        if (IsActive)
        {
            _glide?.Stop();
            _ = _settle.AnimateTo(0, full ? MotionSpec.Tween(ArcMotionPresets.Standard, ArcEasings.Standard) : MotionSpec.Instant);
            _started = false;
            if (_sweep >= 1)
            {
                _sweep = 0;
            }
            UpdateLoop();
            return;
        }
        _loop.Stop();
        var t = _sweep;
        if (full && t > 0 && t < 1 && _pause <= 0)
        {
            // A band in flight glides off on the enter curve.
            _glide ??= CreateGlide();
            _glide.Jump(t);
            var seconds = Math.Min(0.42, SweepDuration.TotalSeconds * (1 - t) * 0.5);
            _ = _glide.AnimateTo(1, MotionSpec.Tween(TimeSpan.FromSeconds(seconds), ArcEasings.Enter));
        }
        _ = _settle.AnimateTo(1, MotionSpec.Tween(full ? TimeSpan.FromSeconds(0.36) : ArcMotionPresets.Instant, ArcEasings.Standard));
    }

    private MotionValue<double> CreateGlide()
    {
        var value = new MotionValue<double>(this, _sweep, MotionVectors.Double);
        value.Changed += v =>
        {
            _sweep = v;
            InvalidateVisual();
        };
        return value;
    }

    private MotionValue<double> CreateValue(double initial, bool measure)
    {
        var value = new MotionValue<double>(this, initial, MotionVectors.Double);
        value.Changed += _ =>
        {
            if (measure)
            {
                InvalidateMeasure();
            }
            InvalidateVisual();
        };
        return value;
    }

    private void Swap(string text)
    {
        var level = ArcMotion.GetEffectiveLevel(this);
        if (_current is null || !IsLoaded || level == ArcMotionLevel.Off || _current.Text == text)
        {
            _current = new Label { Text = text };
            _layoutDirty = true;
            InvalidateMeasure();
            InvalidateVisual();
            return;
        }
        var full = level == ArcMotionLevel.Full;
        var outgoing = _current;
        var p = Math.Clamp(outgoing.Presence?.Value ?? 1, 0, 1);
        outgoing.Presence?.Stop();
        outgoing.Leaving = true;
        outgoing.StartOpacity = p;
        outgoing.StartOffset = outgoing.Full ? 0.3 * FontSize * (1 - p) : 0;
        outgoing.Full = full;
        outgoing.Presence = CreateValue(0, measure: false);
        _leaving.Add(outgoing);
        var leaving = outgoing;
        outgoing.Presence.AnimateTo(1, full
                ? MotionSpec.Tween(ArcMotionPresets.Fast, ArcEasings.Standard)
                : MotionSpec.Tween(ArcMotionPresets.Instant, s_linear))
            .ContinueWith(_ => Avalonia.Threading.Dispatcher.UIThread.Post(() =>
            {
                _leaving.Remove(leaving);
                InvalidateVisual();
            }), TaskScheduler.Default);
        _current = new Label { Text = text, Full = full, Presence = CreateValue(0, measure: false) };
        _ = _current.Presence.AnimateTo(1, full
            ? MotionSpec.Tween(ArcMotionPresets.Standard, ArcEasings.Enter)
            : MotionSpec.Tween(ArcMotionPresets.Instant, s_linear));
        _layoutDirty = true;
        InvalidateMeasure();
        InvalidateVisual();
    }

    private Color BaseColor()
    {
        if (BaseForeground is ISolidColorBrush custom)
        {
            return custom.Color;
        }
        var highlight = (Foreground as ISolidColorBrush)?.Color ?? Colors.Black;
        var muted = (MutedForeground as ISolidColorBrush)?.Color ?? Colors.Gray;
        return Oklch.MixOklab(muted, highlight, BaseMix);
    }

    private void EnsureLayouts(Label label)
    {
        if (label.Base is not null && label.Highlight is not null)
        {
            return;
        }
        var typeface = new Typeface(FontFamily, FontStyle.Normal, FontWeight);
        label.Base = new TextLayout(label.Text, typeface, FontSize, new ImmutableSolidColorBrush(BaseColor()));
        label.Highlight = new TextLayout(label.Text, typeface, FontSize, Foreground ?? Brushes.Black);
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        if (_current is null && !string.IsNullOrEmpty(Text))
        {
            _current = new Label { Text = Text! };
        }
        if (_layoutDirty)
        {
            _layoutDirty = false;
            if (_current is not null)
            {
                _current.Base = null;
                _current.Highlight = null;
            }
        }
        if (_current is null)
        {
            return default;
        }
        EnsureLayouts(_current);
        var target = _current.Highlight!.WidthIncludingTrailingWhitespace;
        if (_width is null)
        {
            _width = CreateValue(target, measure: true);
        }
        else if (Math.Abs(_width.Target - target) > 0.5)
        {
            if (IsLoaded && ArcMotion.IsFull(this) && _current.Presence?.IsAnimating == true)
            {
                _ = _width.AnimateTo(target, MotionSpec.Spring(ArcSpring.Morph));
            }
            else
            {
                _width.Jump(target);
            }
        }
        var width = Math.Max(0, _width.Value);
        return new Size(Math.Min(Math.Ceiling(width), availableSize.Width), _current.Highlight.Height);
    }

    public override void Render(DrawingContext context)
    {
        foreach (var label in _leaving)
        {
            if (label.Base is null)
            {
                continue;
            }
            var q = Math.Clamp(label.Presence?.Value ?? 1, 0, 1);
            var opacity = label.StartOpacity * (1 - q);
            var dy = label.Full ? label.StartOffset + (-0.24 * FontSize - label.StartOffset) * q : 0;
            DrawLabel(context, label, opacity, dy, label.Full ? 2 * q : 0);
        }
        if (_current?.Base is not null)
        {
            var p = Math.Clamp(_current.Presence?.Value ?? 1, 0, 1);
            var dy = _current.Full ? 0.3 * FontSize * (1 - p) : 0;
            DrawLabel(context, _current, p, dy, _current.Full ? 4 * (1 - p) : 0);
        }
    }

    private void DrawLabel(DrawingContext context, Label label, double opacity, double dy, double blur)
    {
        if (opacity <= 0.001 || label.Base is null || label.Highlight is null)
        {
            return;
        }
        var bounds = new Rect(0, dy, label.Highlight.WidthIncludingTrailingWhitespace, label.Highlight.Height);
        using (context.PushOpacity(opacity))
        {
            var effect = TextEffectsBlur.Get(blur);
            using (effect is null ? default(DrawingContext.PushedState?) : context.PushEffect(effect, bounds))
            {
                DrawShimmer(context, label, dy, bounds.Width, bounds.Height);
            }
        }
    }

    /// <summary>Base text, then the solid text by the settle amount, then the band through a sliding mask.</summary>
    private void DrawShimmer(DrawingContext context, Label label, double dy, double width, double height)
    {
        var origin = new Point(0, dy);
        var settle = Math.Clamp(Settle, 0, 1);
        if (settle >= 0.999)
        {
            label.Highlight!.Draw(context, origin);
            return;
        }
        label.Base!.Draw(context, origin);
        if (settle > 0.001)
        {
            using (context.PushOpacity(settle))
            {
                label.Highlight!.Draw(context, origin);
            }
        }
        var t = _sweep;
        if (t <= 0 || t >= 1)
        {
            return;
        }
        var em = FontSize;
        if (_band is null || Math.Abs(_bandEm - em) > 0.01)
        {
            _bandEm = em;
            // Bell falloff: [offset, strength] = [-1,0] [-.5,.5] [-.2,.9] [0,1] [.2,.9] [.5,.5] [1,0].
            _band = new ImmutableLinearGradientBrush(
            [
                new ImmutableGradientStop(0, Color.FromArgb(0, 0, 0, 0)),
                new ImmutableGradientStop(0.25, Color.FromArgb(128, 0, 0, 0)),
                new ImmutableGradientStop(0.4, Color.FromArgb(230, 0, 0, 0)),
                new ImmutableGradientStop(0.5, Color.FromArgb(255, 0, 0, 0)),
                new ImmutableGradientStop(0.6, Color.FromArgb(230, 0, 0, 0)),
                new ImmutableGradientStop(0.75, Color.FromArgb(128, 0, 0, 0)),
                new ImmutableGradientStop(1, Color.FromArgb(0, 0, 0, 0))
            ], startPoint: new RelativePoint(0, 0, RelativeUnit.Relative), endPoint: new RelativePoint(1, 0, RelativeUnit.Relative));
        }
        var center = BandCenter(t, width, em);
        var band = new Rect(center - BandEm * em, dy, 2 * BandEm * em, height);
        // Over the partly settled base, the band strength applies to what is left: h − base' = (1 − settle)(h − base).
        using (context.PushClip(band))
        using (context.PushOpacityMask(_band, band))
        {
            label.Highlight!.Draw(context, origin);
        }
    }
}
