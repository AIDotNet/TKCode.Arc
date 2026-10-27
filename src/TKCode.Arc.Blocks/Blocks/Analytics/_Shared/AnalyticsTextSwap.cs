using Avalonia;
using Avalonia.Animation.Easings;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Media;
using TKCode.Arc.Motion;

namespace TKCode.Arc;

/// <summary>
/// How an <see cref="AnalyticsTextSwap"/> replaces its text. Offsets are in em (fractions of the font size): negative
/// moves up. The old text leaves in place (it takes no layout space); the new one enters. The exit and the entry start
/// together unless <see cref="EnterDelay"/> holds the entry back.
/// </summary>
internal sealed record AnalyticsSwapStyle
{
    /// <summary>Old text fade-out time.</summary>
    public TimeSpan ExitDuration { get; init; } = ArcMotionPresets.Fast;

    /// <summary>Old text easing.</summary>
    public Easing ExitEasing { get; init; } = AnalyticsEasings.Standard;

    /// <summary>Where the old text ends up (em; negative = up).</summary>
    public double ExitOffset { get; init; }

    /// <summary>New text fade-in time.</summary>
    public TimeSpan EnterDuration { get; init; } = ArcMotionPresets.Standard;

    /// <summary>New text easing.</summary>
    public Easing EnterEasing { get; init; } = AnalyticsEasings.Enter;

    /// <summary>Where the new text starts (em; positive = below, rising into place).</summary>
    public double EnterOffset { get; init; }

    /// <summary>Delay before the new text starts.</summary>
    public TimeSpan EnterDelay { get; init; }

    /// <summary>Reduced motion: a plain cross-fade of this length (120ms (.16, 1, .3, 1)); zero replaces at once.</summary>
    public TimeSpan ReducedDuration { get; init; } = ArcMotionPresets.Instant;

    /// <summary>
    /// MRR waterfall / Cohort retention (default): old fades up 160ms (.22, 1, .36, 1) to −5px (at 14px), new rises from
    /// +6px in 240ms (.16, 1, .3, 1); both start together; interrupted swaps start from the on-screen opacity.
    /// </summary>
    public static AnalyticsSwapStyle Rise { get; } = new() { ExitOffset = -5.0 / 14, EnterOffset = 6.0 / 14 };

    /// <summary>Revenue explorer's "rise": old to −0.45em and new from +0.45em, both 240ms (.16, 1, .3, 1).</summary>
    public static AnalyticsSwapStyle RiseEven { get; } = new()
    {
        ExitDuration = ArcMotionPresets.Standard, ExitEasing = AnalyticsEasings.Enter, ExitOffset = -0.45, EnterOffset = 0.45
    };

    /// <summary>Metric explorer / Uptime: old to −0.2em in 160ms, new from +0.3em in 240ms after 60ms.</summary>
    public static AnalyticsSwapStyle RiseLate { get; } = new()
    {
        ExitOffset = -0.2, EnterOffset = 0.3, EnterDelay = TimeSpan.FromMilliseconds(60)
    };

    /// <summary>Journey flow: old fades out in 120ms (.22, 1, .36, 1), new rises 4px in 240ms (.16, 1, .3, 1).</summary>
    public static AnalyticsSwapStyle Lift { get; } = new()
    {
        ExitDuration = ArcMotionPresets.Instant, EnterOffset = 4.0 / 14, ReducedDuration = ArcMotionPresets.Fast
    };

    /// <summary>KPI drilldown legend / breakdown names: old out in 80ms, then new in 240ms, (.22, 1, .36, 1), no travel.</summary>
    public static AnalyticsSwapStyle Sequential { get; } = new()
    {
        ExitDuration = TimeSpan.FromMilliseconds(80), EnterEasing = AnalyticsEasings.Standard, EnterDelay = TimeSpan.FromMilliseconds(80)
    };

    /// <summary>Replace at once at every motion level (Metrics dashboard deltas and subtitles).</summary>
    public static AnalyticsSwapStyle Instant { get; } = new()
    {
        ExitDuration = TimeSpan.Zero, EnterDuration = TimeSpan.Zero, ReducedDuration = TimeSpan.Zero
    };
}

/// <summary>
/// One line of text that animates when it changes (subtitles, KPI captions, delta values, legend names, panel titles):
/// the old text leaves (fade, optional travel) while the new one enters, per <see cref="Swap"/>. The first text never
/// animates; quick successive changes stack several leaving layers, each continuing from where it is. Reduced motion
/// cross-fades (<see cref="AnalyticsSwapStyle.ReducedDuration"/>), Off replaces. Self-drawn with cached text layouts;
/// the size is the current text's (leaving text does not hold space). Screen readers read <see cref="Text"/>.
/// </summary>
internal sealed class AnalyticsTextSwap : Control
{
    /// <summary>The text.</summary>
    public static readonly StyledProperty<string?> TextProperty =
        AvaloniaProperty.Register<AnalyticsTextSwap, string?>(nameof(Text));

    /// <summary>How changes animate (default <see cref="AnalyticsSwapStyle.Rise"/>).</summary>
    public static readonly StyledProperty<AnalyticsSwapStyle> SwapProperty =
        AvaloniaProperty.Register<AnalyticsTextSwap, AnalyticsSwapStyle>(nameof(Swap), AnalyticsSwapStyle.Rise);

    /// <summary>Digits with tabular figures.</summary>
    public static readonly StyledProperty<bool> IsTabularProperty =
        AvaloniaProperty.Register<AnalyticsTextSwap, bool>(nameof(IsTabular));

    /// <summary>Horizontal placement of the text inside the control (matters when it is wider than the text).</summary>
    public static readonly StyledProperty<TextAlignment> TextAlignmentProperty =
        AvaloniaProperty.Register<AnalyticsTextSwap, TextAlignment>(nameof(TextAlignment));

    /// <summary>Fixed line height (0 uses the font's).</summary>
    public static readonly StyledProperty<double> LineHeightProperty =
        AvaloniaProperty.Register<AnalyticsTextSwap, double>(nameof(LineHeight));

    /// <summary>Font family (inherited).</summary>
    public static readonly StyledProperty<FontFamily> FontFamilyProperty = TextElement.FontFamilyProperty.AddOwner<AnalyticsTextSwap>();

    /// <summary>Font size (inherited).</summary>
    public static readonly StyledProperty<double> FontSizeProperty = TextElement.FontSizeProperty.AddOwner<AnalyticsTextSwap>();

    /// <summary>Font weight (inherited).</summary>
    public static readonly StyledProperty<FontWeight> FontWeightProperty = TextElement.FontWeightProperty.AddOwner<AnalyticsTextSwap>();

    /// <summary>Text brush (inherited).</summary>
    public static readonly StyledProperty<IBrush?> ForegroundProperty = TextElement.ForegroundProperty.AddOwner<AnalyticsTextSwap>();

    private readonly AnalyticsTextCache _cache = new(64);
    private readonly List<Layer> _leaving = new();
    private readonly LiveLoop _loop;
    private Layer? _current;

    static AnalyticsTextSwap()
    {
        AffectsMeasure<AnalyticsTextSwap>(FontFamilyProperty, FontSizeProperty, FontWeightProperty, IsTabularProperty, LineHeightProperty);
        AffectsRender<AnalyticsTextSwap>(ForegroundProperty, TextAlignmentProperty);
        IsHitTestVisibleProperty.OverrideDefaultValue<AnalyticsTextSwap>(false);
    }

    /// <summary>Creates the control.</summary>
    public AnalyticsTextSwap()
    {
        _loop = new LiveLoop(this, Advance) { MinimumLevel = ArcMotionLevel.Reduced };
    }

    /// <inheritdoc cref="TextProperty"/>
    public string? Text { get => GetValue(TextProperty); set => SetValue(TextProperty, value); }

    /// <inheritdoc cref="SwapProperty"/>
    public AnalyticsSwapStyle Swap { get => GetValue(SwapProperty); set => SetValue(SwapProperty, value); }

    /// <inheritdoc cref="IsTabularProperty"/>
    public bool IsTabular { get => GetValue(IsTabularProperty); set => SetValue(IsTabularProperty, value); }

    /// <inheritdoc cref="TextAlignmentProperty"/>
    public TextAlignment TextAlignment { get => GetValue(TextAlignmentProperty); set => SetValue(TextAlignmentProperty, value); }

    /// <inheritdoc cref="LineHeightProperty"/>
    public double LineHeight { get => GetValue(LineHeightProperty); set => SetValue(LineHeightProperty, value); }

    /// <inheritdoc cref="FontFamilyProperty"/>
    public FontFamily FontFamily { get => GetValue(FontFamilyProperty); set => SetValue(FontFamilyProperty, value); }

    /// <inheritdoc cref="FontSizeProperty"/>
    public double FontSize { get => GetValue(FontSizeProperty); set => SetValue(FontSizeProperty, value); }

    /// <inheritdoc cref="FontWeightProperty"/>
    public FontWeight FontWeight { get => GetValue(FontWeightProperty); set => SetValue(FontWeightProperty, value); }

    /// <inheritdoc cref="ForegroundProperty"/>
    public IBrush? Foreground { get => GetValue(ForegroundProperty); set => SetValue(ForegroundProperty, value); }

    /// <summary>Texts still leaving (tests).</summary>
    internal int LeavingCount => _leaving.Count;

    /// <summary>Opacity of the current text (tests).</summary>
    internal double CurrentOpacity => _current?.Opacity.Value ?? 0;

    /// <summary>Offset of the current text in em (tests).</summary>
    internal double CurrentOffset => _current?.Offset.Value ?? 0;

    /// <summary>True while a swap runs.</summary>
    internal bool IsSwapping => _loop.IsActive;

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == TextProperty)
        {
            OnTextChanged(change.GetOldValue<string?>(), change.GetNewValue<string?>());
        }
        else if (change.Property == FontFamilyProperty || change.Property == FontWeightProperty)
        {
            _cache.Clear();
        }
    }

    private void OnTextChanged(string? oldText, string? newText)
    {
        AutomationProperties.SetName(this, newText ?? string.Empty);
        var level = ArcMotion.GetEffectiveLevel(this);
        var style = Swap;
        var animate = IsLoaded && _current is not null && level != ArcMotionLevel.Off &&
                      (level == ArcMotionLevel.Full ? style.EnterDuration > TimeSpan.Zero || style.ExitDuration > TimeSpan.Zero : style.ReducedDuration > TimeSpan.Zero);
        if (!animate)
        {
            _leaving.Clear();
            _current = string.IsNullOrEmpty(newText) ? null : Layer.Resting(newText);
            _loop.Stop();
            InvalidateMeasure();
            InvalidateVisual();
            return;
        }
        var full = level == ArcMotionLevel.Full;
        if (_current is { } old)
        {
            old.Opacity.Start(0, full ? style.ExitDuration : style.ReducedDuration, full ? style.ExitEasing : AnalyticsEasings.Enter);
            old.Offset.Start(full ? style.ExitOffset : old.Offset.Value, full ? style.ExitDuration : style.ReducedDuration, full ? style.ExitEasing : AnalyticsEasings.Enter);
            _leaving.Add(old);
        }
        _current = null;
        if (!string.IsNullOrEmpty(newText))
        {
            var layer = new Layer(newText);
            layer.Opacity.Jump(0);
            layer.Offset.Jump(full ? style.EnterOffset : 0);
            var delay = full ? style.EnterDelay : TimeSpan.Zero;
            layer.Opacity.Start(1, full ? style.EnterDuration : style.ReducedDuration, full ? style.EnterEasing : AnalyticsEasings.Enter, delay);
            layer.Offset.Start(0, full ? style.EnterDuration : style.ReducedDuration, full ? style.EnterEasing : AnalyticsEasings.Enter, delay);
            _current = layer;
        }
        _loop.Start();
        InvalidateMeasure();
        InvalidateVisual();
    }

    private bool Advance(double dt)
    {
        var moving = false;
        if (_current is { } current)
        {
            moving |= current.Opacity.Advance(dt);
            moving |= current.Offset.Advance(dt);
        }
        for (var i = _leaving.Count - 1; i >= 0; i--)
        {
            var layer = _leaving[i];
            var running = layer.Opacity.Advance(dt) | layer.Offset.Advance(dt);
            if (!running)
            {
                _leaving.RemoveAt(i);
            }
            moving |= running;
        }
        InvalidateVisual();
        return moving;
    }

    private Typeface Typeface => new(FontFamily, FontStyle.Normal, FontWeight);

    private AnalyticsText Layout(string text) => _cache.Get(text, Typeface, FontSize, IsTabular);

    private double LineBox(AnalyticsText text) => LineHeight > 0 ? LineHeight : text.Height;

    protected override Size MeasureOverride(Size availableSize)
    {
        if (_current is null)
        {
            var empty = Layout(" ");
            return new Size(0, LineBox(empty));
        }
        var layout = Layout(_current.Text);
        return new Size(Math.Ceiling(layout.WidthIncludingTrailingWhitespace), LineBox(layout));
    }

    public override void Render(DrawingContext context)
    {
        var brush = Foreground ?? Brushes.Black;
        foreach (var layer in _leaving)
        {
            DrawLayer(context, layer, brush);
        }
        if (_current is { } current)
        {
            DrawLayer(context, current, brush);
        }
    }

    private void DrawLayer(DrawingContext context, Layer layer, IBrush brush)
    {
        var opacity = Math.Clamp(layer.Opacity.Value, 0, 1);
        if (opacity <= 0.001)
        {
            return;
        }
        var text = Layout(layer.Text);
        var width = text.WidthIncludingTrailingWhitespace;
        var x = TextAlignment switch
        {
            TextAlignment.Right or TextAlignment.End => Bounds.Width - width,
            TextAlignment.Center => (Bounds.Width - width) / 2,
            _ => 0
        };
        var y = (LineBox(text) - text.Height) / 2 + layer.Offset.Value * FontSize;
        using (context.PushOpacity(opacity))
        {
            text.Draw(context, brush, new Point(x, y));
        }
    }

    protected override AutomationPeer OnCreateAutomationPeer() => new TextPeer(this);

    private sealed class TextPeer(AnalyticsTextSwap owner) : ControlAutomationPeer(owner)
    {
        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Text;

        protected override string? GetNameCore() => owner.Text;

        protected override bool IsContentElementCore() => true;

        protected override bool IsControlElementCore() => true;
    }

    private sealed class Layer(string text)
    {
        public string Text { get; } = text;
        public AnalyticsTween Opacity = new(1);
        public AnalyticsTween Offset = new(0);

        public static Layer Resting(string text) => new(text);
    }
}
