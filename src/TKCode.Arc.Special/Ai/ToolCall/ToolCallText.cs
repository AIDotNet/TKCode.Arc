using Avalonia;
using Avalonia.Animation.Easings;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using Avalonia.Media.TextFormatting;
using TKCode.Arc.Motion;
using TKCode.Arc.Primitives;

namespace TKCode.Arc;

/// <summary>How a <see cref="ToolCallText"/> trades one text for the next.</summary>
internal enum ToolCallTextSwap
{
    /// <summary>The new text rises 0.3em out of a 4px blur over 240ms (enter curve) while the old one fades over 160ms,
    /// lifting 0.24em into a 2px blur (Arc's label swap, measured on titles and the group header).</summary>
    Rise,

    /// <summary>A plain crossfade, 160ms each way on CSS ease-out (the group header's meta).</summary>
    Fade,

    /// <summary>The Reasoning block's label swap: the old text fades out over 100ms, the new one fades in over 160ms on the
    /// enter curve while it rises 2px (no blur, no offset for the old text).</summary>
    Lift
}

/// <summary>
/// One line of the tool call parts that changes in place: a row's title, the group header's label and its meta. It
/// truncates with an ellipsis, trades texts with <see cref="Swap"/> (both texts drawn at once in the same place), and while
/// <see cref="IsShimmering"/> a light band (4.4em wide, bell shaped) crosses a dim base (TextMuted with 12% of the text
/// color) once every 1.8s, linear, with a 0.6s rest between passes. A band in flight when the text changes speeds up
/// threefold and leaves with the old text; the new text is solid. Turning the shimmer off without a new text settles to
/// solid over 360ms. Reduced motion: no band (the dim base rests), the old text fades over 130ms and the new one shows
/// at once. Off: everything at once.
/// <para>
/// Drawn in code (cached <see cref="TextLayout"/>s per text and width, <see cref="MotionValue{T}"/> per text, a
/// <see cref="LiveLoop"/> for the band) because two texts overlap with their own blur, offset and band, which a
/// ControlTheme transition cannot express. A frame allocates nothing.
/// </para>
/// </summary>
internal sealed class ToolCallText : Control
{
    /// <summary>The text.</summary>
    public static readonly StyledProperty<string?> TextProperty =
        AvaloniaProperty.Register<ToolCallText, string?>(nameof(Text));

    /// <summary>Whether the band sweeps (the call or the agent is working).</summary>
    public static readonly StyledProperty<bool> IsShimmeringProperty =
        AvaloniaProperty.Register<ToolCallText, bool>(nameof(IsShimmering));

    /// <summary>How texts trade places.</summary>
    public static readonly StyledProperty<ToolCallTextSwap> SwapProperty =
        AvaloniaProperty.Register<ToolCallText, ToolCallTextSwap>(nameof(Swap));

    /// <summary>The text color (a new value applies to the next text and to the current one at once).</summary>
    public static readonly StyledProperty<IBrush?> ForegroundProperty =
        TextElement.ForegroundProperty.AddOwner<ToolCallText>();

    /// <summary>The muted color the dim base is mixed from (theme: TextMuted).</summary>
    public static readonly StyledProperty<IBrush?> MutedForegroundProperty =
        AvaloniaProperty.Register<ToolCallText, IBrush?>(nameof(MutedForeground));

    /// <summary>The font family (inherited).</summary>
    public static readonly StyledProperty<FontFamily> FontFamilyProperty = TextElement.FontFamilyProperty.AddOwner<ToolCallText>();

    /// <summary>The font size (inherited).</summary>
    public static readonly StyledProperty<double> FontSizeProperty = TextElement.FontSizeProperty.AddOwner<ToolCallText>();

    /// <summary>The font weight (inherited).</summary>
    public static readonly StyledProperty<FontWeight> FontWeightProperty = TextElement.FontWeightProperty.AddOwner<ToolCallText>();

    /// <summary>The line height; NaN uses the font's.</summary>
    public static readonly StyledProperty<double> LineHeightProperty =
        AvaloniaProperty.Register<ToolCallText, double>(nameof(LineHeight), double.NaN);

    /// <summary>Extra space between letters (Arc: −0.01em).</summary>
    public static readonly StyledProperty<double> LetterSpacingProperty =
        AvaloniaProperty.Register<ToolCallText, double>(nameof(LetterSpacing));

    /// <summary>Half the band's width in em (the band is 4.4em wide).</summary>
    internal const double BandEm = 2.2;

    /// <summary>One pass of the band and the rest after it.</summary>
    internal const double SweepSeconds = 1.8;

    internal const double RestSeconds = 0.6;

    /// <summary>The wait before the first pass.</summary>
    internal const double FirstDelay = 0.3;

    private static readonly Easing s_linear = new LinearEasing();

    private sealed class Label
    {
        public required string Text { get; init; }
        public IBrush? Brush { get; set; }
        public TextLayout? Base { get; set; }
        public TextLayout? Highlight { get; set; }
        public double LayoutWidth { get; set; } = double.NaN;
        public double NaturalWidth { get; set; }
        public MotionValue<double>? Presence { get; set; }
        public bool Leaving { get; set; }
        public bool Rise { get; set; }
        public bool Lift { get; set; }
        public double Sweep { get; set; }
        public bool Shimmers { get; set; }
    }

    private readonly List<Label> _leaving = new(2);
    private readonly LiveLoop _loop;
    private Label? _current;
    private MotionValue<double>? _settle;
    private double _pause = FirstDelay;
    private IImmutableBrush? _band;
    private double _bandEm;
    private bool _layoutDirty = true;

    static ToolCallText()
    {
        AffectsMeasure<ToolCallText>(FontFamilyProperty, FontSizeProperty, FontWeightProperty, LineHeightProperty, LetterSpacingProperty);
        IsHitTestVisibleProperty.OverrideDefaultValue<ToolCallText>(false);
        FocusableProperty.OverrideDefaultValue<ToolCallText>(false);
    }

    public ToolCallText()
    {
        _loop = new LiveLoop(this, Step) { ViewportMargin = 64 };
    }

    /// <inheritdoc cref="TextProperty"/>
    public string? Text { get => GetValue(TextProperty); set => SetValue(TextProperty, value); }

    /// <inheritdoc cref="IsShimmeringProperty"/>
    public bool IsShimmering { get => GetValue(IsShimmeringProperty); set => SetValue(IsShimmeringProperty, value); }

    /// <inheritdoc cref="SwapProperty"/>
    public ToolCallTextSwap Swap { get => GetValue(SwapProperty); set => SetValue(SwapProperty, value); }

    /// <inheritdoc cref="ForegroundProperty"/>
    public IBrush? Foreground { get => GetValue(ForegroundProperty); set => SetValue(ForegroundProperty, value); }

    /// <inheritdoc cref="MutedForegroundProperty"/>
    public IBrush? MutedForeground { get => GetValue(MutedForegroundProperty); set => SetValue(MutedForegroundProperty, value); }

    /// <inheritdoc cref="FontFamilyProperty"/>
    public FontFamily FontFamily { get => GetValue(FontFamilyProperty); set => SetValue(FontFamilyProperty, value); }

    /// <inheritdoc cref="FontSizeProperty"/>
    public double FontSize { get => GetValue(FontSizeProperty); set => SetValue(FontSizeProperty, value); }

    /// <inheritdoc cref="FontWeightProperty"/>
    public FontWeight FontWeight { get => GetValue(FontWeightProperty); set => SetValue(FontWeightProperty, value); }

    /// <inheritdoc cref="LineHeightProperty"/>
    public double LineHeight { get => GetValue(LineHeightProperty); set => SetValue(LineHeightProperty, value); }

    /// <inheritdoc cref="LetterSpacingProperty"/>
    public double LetterSpacing { get => GetValue(LetterSpacingProperty); set => SetValue(LetterSpacingProperty, value); }

    /// <summary>The band's progress across the current text (0 left of it, 1 right of it).</summary>
    internal double Sweep => _current?.Sweep ?? 0;

    /// <summary>Whether the band moves now.</summary>
    internal bool IsSweeping => _loop.IsRunning;

    /// <summary>The number of texts drawn (2 during a swap).</summary>
    internal int DrawnCount => _leaving.Count + (_current is null ? 0 : 1);

    /// <summary>Whether the current text is cut with an ellipsis.</summary>
    internal bool IsTrimmed => _current is { } label && label.NaturalWidth > Bounds.Width + 0.5;

    /// <summary>The band center for progress <paramref name="t"/> over a text <paramref name="width"/> wide: from 2.2em left
    /// of it to 2.2em right of it.</summary>
    internal static double BandCenter(double t, double width, double em) => t * width + (2 * t - 1) * BandEm * em;

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == TextProperty)
        {
            OnTextChanged(change.GetNewValue<string?>() ?? string.Empty);
        }
        else if (change.Property == IsShimmeringProperty)
        {
            OnShimmerChanged();
        }
        else if (change.Property == ForegroundProperty || change.Property == MutedForegroundProperty ||
                 change.Property == FontFamilyProperty || change.Property == FontSizeProperty ||
                 change.Property == FontWeightProperty || change.Property == LineHeightProperty ||
                 change.Property == LetterSpacingProperty)
        {
            if (change.Property == ForegroundProperty && _current is not null)
            {
                _current.Brush = Foreground;
            }
            _layoutDirty = true;
            InvalidateMeasure();
            InvalidateVisual();
        }
        else if (change.Property == IsVisibleProperty || change.Property == ArcMotion.LevelProperty)
        {
            UpdateLoop();
        }
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        UpdateLoop();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        foreach (var label in _leaving)
        {
            label.Presence?.Stop();
        }
        _leaving.Clear();
        _current?.Presence?.Jump(1);
        _settle?.Jump(IsShimmering ? 0 : 1);
    }

    private ArcMotionLevel Level => ArcMotion.GetEffectiveLevel(this);

    private void OnTextChanged(string text)
    {
        var level = Level;
        if (_current is null || !IsLoaded || level == ArcMotionLevel.Off || _current.Text == text)
        {
            _current = new Label { Text = text, Brush = Foreground, Shimmers = IsShimmering, Sweep = _current?.Sweep ?? 0 };
            InvalidateMeasure();
            InvalidateVisual();
            return;
        }
        var full = level == ArcMotionLevel.Full;
        var rise = full && Swap == ToolCallTextSwap.Rise;
        var lift = full && Swap == ToolCallTextSwap.Lift;
        var outgoing = _current;
        var shown = Math.Clamp(outgoing.Presence?.Value ?? 1, 0, 1);
        outgoing.Presence?.Stop();
        outgoing.Leaving = true;
        outgoing.Rise = rise;
        // The leaving text keeps the settle it had (solid or shimmering) and its band, which now runs three times faster.
        outgoing.Shimmers = outgoing.Shimmers && Settle < 0.999;
        var exit = full
            ? Swap == ToolCallTextSwap.Lift
                ? MotionSpec.Tween(TimeSpan.FromMilliseconds(100), ArcEasings.Standard)
                : MotionSpec.Tween(ArcMotionPresets.Fast, Swap == ToolCallTextSwap.Rise ? ArcEasings.Standard : AiHeightBox.EaseOut)
            : MotionSpec.Tween(TimeSpan.FromMilliseconds(130), s_linear);
        var leaving = outgoing;
        leaving.Presence = CreateValue(shown);
        _leaving.Add(leaving);
        leaving.Presence.AnimateTo(0, exit).ContinueWith(_ => Dispatcher.Post(() =>
        {
            _leaving.Remove(leaving);
            UpdateLoop();
            InvalidateVisual();
        }), TaskScheduler.Default);
        _current = new Label { Text = text, Brush = Foreground, Rise = rise, Lift = lift, Shimmers = IsShimmering };
        if (full)
        {
            _current.Presence = CreateValue(0);
            _ = _current.Presence.AnimateTo(1, Swap switch
            {
                ToolCallTextSwap.Rise => MotionSpec.Tween(ArcMotionPresets.Standard, ArcEasings.Enter),
                ToolCallTextSwap.Lift => MotionSpec.Tween(ArcMotionPresets.Fast, ArcEasings.Enter),
                _ => MotionSpec.Tween(ArcMotionPresets.Fast, AiHeightBox.EaseOut),
            });
        }
        // A new running text starts its band after the usual wait; a settled one is solid at once.
        _pause = FirstDelay;
        _settle?.Jump(IsShimmering ? 0 : 1);
        UpdateLoop();
        InvalidateMeasure();
        InvalidateVisual();
    }

    private double Settle => _settle?.Value ?? (IsShimmering ? 0 : 1);

    private void OnShimmerChanged()
    {
        var shimmering = IsShimmering;
        if (_current is not null)
        {
            _current.Shimmers = shimmering;
        }
        _settle ??= CreateValue(shimmering ? 1 : 0);
        var level = Level;
        if (!IsLoaded || level == ArcMotionLevel.Off)
        {
            _settle.Jump(shimmering ? 0 : 1);
        }
        else if (shimmering)
        {
            _settle.Jump(0);
            _pause = FirstDelay;
            if (_current is not null)
            {
                _current.Sweep = 0;
            }
        }
        else
        {
            _ = _settle.AnimateTo(1, level == ArcMotionLevel.Full
                ? MotionSpec.Tween(TimeSpan.FromMilliseconds(360), ArcEasings.Standard)
                : MotionSpec.Tween(ArcMotionPresets.Instant, s_linear));
        }
        UpdateLoop();
        InvalidateVisual();
    }

    private void UpdateLoop()
    {
        var needs = (IsShimmering && _current is not null) || _leaving.Any(l => l.Shimmers && l.Sweep is > 0 and < 1);
        if (needs && Level == ArcMotionLevel.Full)
        {
            _loop.Start();
        }
        else
        {
            _loop.Stop();
        }
    }

    private bool Step(double dt)
    {
        var moved = false;
        foreach (var label in _leaving)
        {
            if (label.Shimmers && label.Sweep is > 0 and < 1)
            {
                label.Sweep = Math.Min(1, label.Sweep + 3 * dt / SweepSeconds);
                moved = true;
            }
        }
        if (_current is { } current && IsShimmering)
        {
            var left = dt;
            if (_pause > 0)
            {
                _pause -= left;
                left = _pause < 0 ? -_pause : 0;
                if (_pause <= 0)
                {
                    _pause = 0;
                    if (current.Sweep >= 1)
                    {
                        current.Sweep = 0;
                    }
                }
            }
            if (left > 0)
            {
                current.Sweep = Math.Min(1, current.Sweep + left / SweepSeconds);
                if (current.Sweep >= 1)
                {
                    _pause = RestSeconds;
                }
            }
            moved = true;
        }
        if (moved)
        {
            InvalidateVisual();
        }
        return moved || IsShimmering;
    }

    private MotionValue<double> CreateValue(double initial)
    {
        var value = new MotionValue<double>(this, initial, MotionVectors.Double);
        value.Changed += _ => InvalidateVisual();
        return value;
    }

    private Typeface Typeface => new(FontFamily, FontStyle.Normal, FontWeight);

    private Color DimColor()
    {
        var text = (Foreground as ISolidColorBrush)?.Color ?? Colors.Black;
        var muted = (MutedForeground as ISolidColorBrush)?.Color ?? Colors.Gray;
        return Oklch.MixOklab(muted, text, 0.12);
    }

    private TextLayout Build(string text, IBrush brush, double maxWidth) => new(text, Typeface, FontSize, brush,
        textTrimming: double.IsInfinity(maxWidth) ? null : TextTrimming.CharacterEllipsis,
        maxWidth: double.IsInfinity(maxWidth) ? double.PositiveInfinity : Math.Max(0, maxWidth),
        lineHeight: LineHeight, letterSpacing: LetterSpacing);

    private void EnsureLayouts(Label label, double width)
    {
        var fits = label.NaturalWidth <= width + 0.5;
        var target = fits ? double.PositiveInfinity : width;
        if (label.Highlight is not null && label.Base is not null && (label.LayoutWidth.Equals(target) || (fits && double.IsInfinity(label.LayoutWidth))))
        {
            return;
        }
        label.Highlight?.Dispose();
        label.Base?.Dispose();
        var brush = label.Brush ?? Foreground ?? Brushes.Black;
        label.Highlight = Build(label.Text, brush, target);
        label.Base = Build(label.Text, new ImmutableSolidColorBrush(DimColor()), target);
        label.LayoutWidth = target;
    }

    private void Reset(Label label)
    {
        label.Highlight?.Dispose();
        label.Base?.Dispose();
        label.Highlight = null;
        label.Base = null;
        label.LayoutWidth = double.NaN;
        using var natural = Build(label.Text, Brushes.Black, double.PositiveInfinity);
        label.NaturalWidth = natural.WidthIncludingTrailingWhitespace;
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        _current ??= new Label { Text = Text ?? string.Empty, Brush = Foreground, Shimmers = IsShimmering };
        if (_layoutDirty)
        {
            _layoutDirty = false;
            Reset(_current);
            foreach (var label in _leaving)
            {
                Reset(label);
            }
        }
        else if (_current.Highlight is null && double.IsNaN(_current.LayoutWidth))
        {
            Reset(_current);
        }
        var height = double.IsNaN(LineHeight) ? FontSize * 1.4 : LineHeight;
        return new Size(Math.Min(Math.Ceiling(_current.NaturalWidth), availableSize.Width), height);
    }

    public override void Render(DrawingContext context)
    {
        var width = Bounds.Width;
        foreach (var label in _leaving)
        {
            if (label.Highlight is null && double.IsNaN(label.LayoutWidth))
            {
                Reset(label);
            }
            var q = Math.Clamp(label.Presence?.Value ?? 0, 0, 1);
            var dy = label.Rise ? -0.24 * FontSize * (1 - q) : 0;
            DrawLabel(context, label, width, q, dy, label.Rise ? 2 * (1 - q) : 0);
        }
        if (_current is { } current)
        {
            if (current.Highlight is null && double.IsNaN(current.LayoutWidth))
            {
                Reset(current);
            }
            var p = Math.Clamp(current.Presence?.Value ?? 1, 0, 1);
            var dy = current.Rise ? 0.3 * FontSize * (1 - p) : current.Lift ? 2 * (1 - p) : 0;
            DrawLabel(context, current, width, p, dy, current.Rise ? 4 * (1 - p) : 0);
        }
    }

    private void DrawLabel(DrawingContext context, Label label, double width, double opacity, double dy, double blur)
    {
        if (opacity <= 0.001)
        {
            return;
        }
        EnsureLayouts(label, width);
        var bounds = new Rect(-4, dy - 4, width + 8, Bounds.Height + 8);
        using (context.PushOpacity(opacity))
        {
            var effect = ArcBlur.Get(blur);
            using (effect is null ? default(DrawingContext.PushedState?) : context.PushEffect(effect, bounds))
            {
                DrawShimmer(context, label, dy, Math.Min(width, label.NaturalWidth));
            }
        }
    }

    /// <summary>The dim base, the solid text by the settle amount, then the band through a sliding mask.</summary>
    private void DrawShimmer(DrawingContext context, Label label, double dy, double width)
    {
        var origin = new Point(0, dy);
        var settle = label.Leaving ? (label.Shimmers ? 0 : 1) : Math.Clamp(Settle, 0, 1);
        if (!label.Shimmers && settle >= 0.999 || label.Base is null)
        {
            label.Highlight!.Draw(context, origin);
            return;
        }
        label.Base.Draw(context, origin);
        if (settle > 0.001)
        {
            using (context.PushOpacity(settle))
            {
                label.Highlight!.Draw(context, origin);
            }
        }
        var t = label.Sweep;
        if (t <= 0 || t >= 1 || Level != ArcMotionLevel.Full)
        {
            return;
        }
        var em = FontSize;
        if (_band is null || Math.Abs(_bandEm - em) > 0.01)
        {
            _bandEm = em;
            // Strength across the band: 0, .5, .9, 1, .9, .5, 0 at 0, 25, 40, 50, 60, 75, 100%.
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
        var band = new Rect(center - BandEm * em, dy - 2, 2 * BandEm * em, Bounds.Height + 4);
        using (context.PushClip(band))
        using (context.PushOpacityMask(_band, band))
        {
            label.Highlight!.Draw(context, origin);
        }
    }
}
