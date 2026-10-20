using TKCode.Arc.Motion;
using Avalonia;
using Avalonia.Animation.Easings;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Media;
using Avalonia.Media.TextFormatting;

namespace TKCode.Arc;

/// <summary>
/// P2 text swap for the Progress category, drawn in <see cref="Render"/>: a new text rises 0.3em out of a soft blur
/// (standard + enter) while the old one lifts away with a subtle blur and fades (fast). The outgoing text keeps its
/// own position and is out of layout (popLayout), so left-aligned labels never shift. Reduced motion crossfades.
/// </summary>
internal sealed class ProgressSwapText : Control
{
    public static readonly StyledProperty<string?> TextProperty =
        AvaloniaProperty.Register<ProgressSwapText, string?>(nameof(Text));

    public static readonly StyledProperty<bool> IsWrappingProperty =
        AvaloniaProperty.Register<ProgressSwapText, bool>(nameof(IsWrapping));

    public static readonly StyledProperty<double> LineHeightProperty =
        AvaloniaProperty.Register<ProgressSwapText, double>(nameof(LineHeight), double.NaN);

    /// <summary>1: new text rises from below; -1: it drops in from above.</summary>
    public static readonly StyledProperty<int> DirectionProperty =
        AvaloniaProperty.Register<ProgressSwapText, int>(nameof(Direction), 1);

    /// <summary>Exit duration preset (Arc uses fast for labels, instant for meter captions).</summary>
    public static readonly StyledProperty<ArcTween> ExitTweenProperty =
        AvaloniaProperty.Register<ProgressSwapText, ArcTween>(nameof(ExitTween), ArcTween.Fast);

    public static readonly StyledProperty<FontFamily> FontFamilyProperty = TextElement.FontFamilyProperty.AddOwner<ProgressSwapText>();
    public static readonly StyledProperty<double> FontSizeProperty = TextElement.FontSizeProperty.AddOwner<ProgressSwapText>();
    public static readonly StyledProperty<FontWeight> FontWeightProperty = TextElement.FontWeightProperty.AddOwner<ProgressSwapText>();
    public static readonly StyledProperty<IBrush?> ForegroundProperty = TextElement.ForegroundProperty.AddOwner<ProgressSwapText>();

    public string? Text { get => GetValue(TextProperty); set => SetValue(TextProperty, value); }
    public bool IsWrapping { get => GetValue(IsWrappingProperty); set => SetValue(IsWrappingProperty, value); }
    public double LineHeight { get => GetValue(LineHeightProperty); set => SetValue(LineHeightProperty, value); }
    public int Direction { get => GetValue(DirectionProperty); set => SetValue(DirectionProperty, value); }
    public ArcTween ExitTween { get => GetValue(ExitTweenProperty); set => SetValue(ExitTweenProperty, value); }
    public FontFamily FontFamily { get => GetValue(FontFamilyProperty); set => SetValue(FontFamilyProperty, value); }
    public double FontSize { get => GetValue(FontSizeProperty); set => SetValue(FontSizeProperty, value); }
    public FontWeight FontWeight { get => GetValue(FontWeightProperty); set => SetValue(FontWeightProperty, value); }
    public IBrush? Foreground { get => GetValue(ForegroundProperty); set => SetValue(ForegroundProperty, value); }

    private sealed class Layer
    {
        public string Text { get; init; } = string.Empty;
        public TextLayout? Layout { get; set; }
        public double LayoutWidth { get; set; } = double.NaN;
        public MotionValue<double>? Progress { get; set; }
        public bool Full { get; set; }
        public int Direction { get; set; } = 1;
        public double StartOpacity { get; set; } = 1;
        public double StartOffset { get; set; }
    }

    private Layer? _current;
    private readonly List<Layer> _leaving = new();
    private static readonly Easing s_linear = new LinearEasing();

    static ProgressSwapText()
    {
        AffectsMeasure<ProgressSwapText>(IsWrappingProperty, LineHeightProperty, FontFamilyProperty, FontSizeProperty, FontWeightProperty);
        AffectsRender<ProgressSwapText>(ForegroundProperty);
        IsHitTestVisibleProperty.OverrideDefaultValue<ProgressSwapText>(false);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == TextProperty)
        {
            Swap(Text);
        }
        else if (change.Property == ForegroundProperty || change.Property == FontFamilyProperty ||
                 change.Property == FontSizeProperty || change.Property == FontWeightProperty ||
                 change.Property == LineHeightProperty || change.Property == IsWrappingProperty)
        {
            if (_current is not null)
            {
                _current.Layout = null;
            }
            InvalidateMeasure();
        }
    }

    private void Swap(string? text)
    {
        var level = ArcMotion.GetEffectiveLevel(this);
        if (_current is null || !IsLoaded || level == ArcMotionLevel.Off || !IsEffectivelyVisible)
        {
            _current = new Layer { Text = text ?? string.Empty };
            InvalidateMeasure();
            InvalidateVisual();
            return;
        }
        var full = level == ArcMotionLevel.Full;
        var outgoing = _current;
        if (!string.IsNullOrEmpty(outgoing.Text))
        {
            var outgoingOpacity = LayerOpacity(outgoing, out var outgoingOffset);
            outgoing.Progress?.Stop();
            outgoing.StartOpacity = outgoingOpacity;
            outgoing.StartOffset = outgoingOffset;
            outgoing.Full = full;
            outgoing.Direction = Direction >= 0 ? 1 : -1;
            outgoing.Progress = CreateProgress(0);
            _leaving.Add(outgoing);
            var exitDuration = full ? TweenDuration(ExitTween) : ArcMotionPresets.Instant;
            var leaving = outgoing;
            outgoing.Progress.AnimateTo(1, MotionSpec.Tween(exitDuration, full ? ArcEasings.Standard : s_linear))
                    .ContinueWith(_ => Avalonia.Threading.Dispatcher.UIThread.Post(() =>
                    {
                        _leaving.Remove(leaving);
                        InvalidateVisual();
                    }), TaskScheduler.Default);
        }

        _current = new Layer { Text = text ?? string.Empty, Full = full, Direction = Direction >= 0 ? 1 : -1 };
        if (!string.IsNullOrEmpty(_current.Text))
        {
            _current.Progress = CreateProgress(0);
            _ = _current.Progress.AnimateTo(1, full
                ? MotionSpec.Tween(ArcMotionPresets.Standard, ArcEasings.Enter)
                : MotionSpec.Tween(ArcMotionPresets.Instant, s_linear));
        }
        InvalidateMeasure();
        InvalidateVisual();
    }

    private static TimeSpan TweenDuration(ArcTween tween) => tween switch
    {
        ArcTween.Instant => ArcMotionPresets.Instant,
        ArcTween.Exit => ArcMotionPresets.Exit,
        ArcTween.Standard or ArcTween.Enter => ArcMotionPresets.Standard,
        ArcTween.Considered => ArcMotionPresets.Considered,
        _ => ArcMotionPresets.Fast
    };

    private MotionValue<double> CreateProgress(double initial)
    {
        var value = new MotionValue<double>(this, initial, MotionVectors.Double);
        value.Changed += _ => InvalidateVisual();
        return value;
    }

    private TextLayout Build(string text, double maxWidth)
    {
        var wrap = IsWrapping;
        return new TextLayout(text, new Typeface(FontFamily, FontStyle.Normal, FontWeight), FontSize, Foreground ?? Brushes.Black,
            textWrapping: wrap ? TextWrapping.Wrap : TextWrapping.NoWrap,
            textTrimming: wrap || double.IsInfinity(maxWidth) ? TextTrimming.None : TextTrimming.CharacterEllipsis,
            maxWidth: double.IsInfinity(maxWidth) ? double.PositiveInfinity : Math.Max(0, maxWidth),
            lineHeight: LineHeight);
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        var current = _current;
        if (current is null && !string.IsNullOrEmpty(Text))
        {
            current = _current = new Layer { Text = Text! };
        }
        if (current is null || string.IsNullOrEmpty(current.Text))
        {
            return default;
        }
        var maxWidth = availableSize.Width;
        if (current.Layout is null || !SameWidth(current.LayoutWidth, maxWidth))
        {
            current.Layout = Build(current.Text, maxWidth);
            current.LayoutWidth = maxWidth;
        }
        var layout = current.Layout;
        var width = IsWrapping ? layout.Width : layout.WidthIncludingTrailingWhitespace;
        return new Size(Math.Min(Math.Ceiling(width), availableSize.Width), layout.Height);
    }

    private static bool SameWidth(double a, double b) => (double.IsInfinity(a) && double.IsInfinity(b)) || Math.Abs(a - b) < 0.5;

    /// <summary>Current opacity of a layer (used when a swap interrupts another).</summary>
    private double LayerOpacity(Layer layer, out double offset)
    {
        var p = Math.Clamp(layer.Progress?.Value ?? 1, 0, 1);
        offset = layer.Full ? 0.3 * FontSize * layer.Direction * (1 - p) : 0;
        return p;
    }

    public override void Render(DrawingContext context)
    {
        var em = FontSize;
        foreach (var layer in _leaving)
        {
            if (layer.Layout is null)
            {
                continue;
            }
            var p = Math.Clamp(layer.Progress?.Value ?? 1, 0, 1);
            var opacity = layer.StartOpacity * (1 - p);
            if (opacity <= 0.001)
            {
                continue;
            }
            var dy = layer.Full ? layer.StartOffset + (-0.3 * em * layer.Direction - layer.StartOffset) * p : 0;
            Draw(context, layer.Layout, dy, opacity, layer.Full ? 2 * p : 0);
        }
        if (_current?.Layout is { } layout)
        {
            var p = Math.Clamp(_current.Progress?.Value ?? 1, 0, 1);
            var dy = _current.Full ? 0.3 * em * _current.Direction * (1 - p) : 0;
            Draw(context, layout, dy, p, _current.Full ? 4 * (1 - p) : 0);
        }
    }

    private static void Draw(DrawingContext context, TextLayout layout, double dy, double opacity, double blur)
    {
        if (opacity >= 0.999 && Math.Abs(dy) < 0.01 && blur < 0.1)
        {
            layout.Draw(context, default);
            return;
        }
        using (context.PushOpacity(opacity))
        {
            var effect = ProgressBlur.Get(blur);
            if (effect is null)
            {
                layout.Draw(context, new Point(0, dy));
                return;
            }
            using (context.PushEffect(effect, new Rect(0, dy, layout.WidthIncludingTrailingWhitespace, layout.Height)))
            {
                layout.Draw(context, new Point(0, dy));
            }
        }
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        foreach (var layer in _leaving)
        {
            layer.Progress?.Dispose();
        }
        _leaving.Clear();
        _current?.Progress?.Jump(1);
    }
}
