using TKCode.Arc.DesignTokens;
using TKCode.Arc.Motion;
using TKCode.Arc.Primitives;
using Avalonia;
using Avalonia.Animation.Easings;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Media.Immutable;

namespace TKCode.Arc;

/// <summary>
/// The question of an <see cref="AiAnswer"/> (Arc's h2): the display font, wrapped and balanced the way CSS
/// <c>text-wrap: balance</c> does it (two to six lines measured at the smallest width that keeps their count, so the lines
/// come out about equally long). It takes the focus from code (after a related question was asked) but is not a tab stop,
/// and shows no ring: a heading is read, not used. Automation reads it as a header named by its text.
/// </summary>
internal sealed class AiAnswerHeading : TextBlock
{
    /// <summary>Chromium balances up to six lines; longer text wraps normally.</summary>
    internal const int MaxBalancedLines = 6;

    private string? _cachedText;
    private double _cachedAvailable = double.NaN;
    private double _cachedWidth = double.NaN;

    static AiAnswerHeading()
    {
        FocusableProperty.OverrideDefaultValue<AiAnswerHeading>(true);
        IsTabStopProperty.OverrideDefaultValue<AiAnswerHeading>(false);
    }

    protected override Type StyleKeyOverride => typeof(AiAnswerHeading);

    /// <summary>The width the text was last measured at (the balanced width, or the available width).</summary>
    internal double BalancedWidth { get; private set; } = double.NaN;

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        var property = change.Property;
        if (property == TextProperty)
        {
            AutomationProperties.SetName(this, change.GetNewValue<string?>() ?? string.Empty);
        }
        if (property == TextProperty || property == FontSizeProperty || property == FontFamilyProperty ||
            property == FontWeightProperty || property == LetterSpacingProperty || property == LineHeightProperty ||
            property == PaddingProperty || property == TextWrappingProperty)
        {
            _cachedText = null;
        }
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        var text = Text;
        if (TextWrapping == TextWrapping.NoWrap || double.IsInfinity(availableSize.Width) || string.IsNullOrEmpty(text))
        {
            BalancedWidth = availableSize.Width;
            return base.MeasureOverride(availableSize);
        }
        if (ReferenceEquals(text, _cachedText) && availableSize.Width.Equals(_cachedAvailable))
        {
            BalancedWidth = _cachedWidth;
            return base.MeasureOverride(availableSize.WithWidth(_cachedWidth));
        }
        var natural = base.MeasureOverride(availableSize);
        var lines = TextLayout.TextLines.Count;
        var width = availableSize.Width;
        if (lines is >= 2 and <= MaxBalancedLines)
        {
            var padding = Padding.Left + Padding.Right;
            // The text is at least as long as its widest line, so a measure narrower than that over the line count
            // cannot hold it in the same number of lines: a safe lower bound for the search.
            var low = padding + Math.Max(0, natural.Width - padding) / lines;
            var high = Math.Min(availableSize.Width, natural.Width);
            while (high - low > 0.5)
            {
                var middle = (low + high) / 2;
                base.MeasureOverride(availableSize.WithWidth(middle));
                if (TextLayout.TextLines.Count <= lines)
                {
                    high = middle;
                }
                else
                {
                    low = middle;
                }
            }
            // A pixel of slack so layout rounding never re-wraps the balanced lines.
            width = Math.Min(availableSize.Width, Math.Ceiling(high) + 1);
        }
        _cachedText = text;
        _cachedAvailable = availableSize.Width;
        _cachedWidth = width;
        BalancedWidth = width;
        return width.Equals(availableSize.Width) ? natural : base.MeasureOverride(availableSize.WithWidth(width));
    }

    protected override AutomationPeer OnCreateAutomationPeer() => new HeadingPeer(this);

    /// <summary>A header named by the question.</summary>
    private sealed class HeadingPeer(AiAnswerHeading owner) : ControlAutomationPeer(owner)
    {
        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Header;

        protected override bool IsContentElementCore() => true;

        protected override bool IsControlElementCore() => true;
    }
}

/// <summary>
/// A quiet pill button of the answer (the top bar's Share, the note's Try again): 32px high, a 1px Border edge on Surface, a
/// 14px glyph and the label 6px apart in Foreground at 500. Hover: SurfaceMuted with a BorderStrong edge (160ms); pressed:
/// half SurfaceMuted, half Border; disabled: 60% opacity with muted text. A new <see cref="Glyph"/> swaps the way the
/// action bar's glyphs do (<see cref="AiAnswerGlyphSwap"/>). The keyboard ring is 2px, 2px outside. An Arc
/// <see cref="Button"/> underneath (the status note's action part is one) with a theme of its own.
/// </summary>
[PseudoClasses(HoverPseudoClass)]
internal sealed class AiAnswerPillButton : Button
{
    /// <summary>Mouse or pen over the button (touch leaves no hover look behind).</summary>
    public const string HoverPseudoClass = ":hover";

    /// <summary>The Lucide glyph before the label.</summary>
    public static readonly StyledProperty<Geometry?> GlyphProperty =
        AvaloniaProperty.Register<AiAnswerPillButton, Geometry?>(nameof(Glyph));

    /// <summary>The glyph's size (14).</summary>
    public static readonly StyledProperty<double> GlyphSizeProperty =
        AvaloniaProperty.Register<AiAnswerPillButton, double>(nameof(GlyphSize), 14);

    /// <summary>The fill while pressed: SurfaceMuted and Border half and half (mixed here; the theme uses it).</summary>
    public static readonly StyledProperty<IBrush?> PressedFillProperty =
        AvaloniaProperty.Register<AiAnswerPillButton, IBrush?>(nameof(PressedFill));

    private static readonly StyledProperty<IBrush?> MutedProbeProperty =
        AvaloniaProperty.Register<AiAnswerPillButton, IBrush?>("MutedProbe");

    private static readonly StyledProperty<IBrush?> BorderProbeProperty =
        AvaloniaProperty.Register<AiAnswerPillButton, IBrush?>("BorderProbe");

    public AiAnswerPillButton()
    {
        ArcTokens.Bind(this, MutedProbeProperty, ArcDesignTokenKind.SurfaceMuted);
        ArcTokens.Bind(this, BorderProbeProperty, ArcDesignTokenKind.Border);
    }

    /// <inheritdoc cref="GlyphProperty"/>
    public Geometry? Glyph { get => GetValue(GlyphProperty); set => SetValue(GlyphProperty, value); }

    /// <inheritdoc cref="GlyphSizeProperty"/>
    public double GlyphSize { get => GetValue(GlyphSizeProperty); set => SetValue(GlyphSizeProperty, value); }

    /// <inheritdoc cref="PressedFillProperty"/>
    public IBrush? PressedFill { get => GetValue(PressedFillProperty); set => SetValue(PressedFillProperty, value); }

    protected override Type StyleKeyOverride => typeof(AiAnswerPillButton);

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == MutedProbeProperty || change.Property == BorderProbeProperty)
        {
            if (GetValue(MutedProbeProperty) is ISolidColorBrush muted && GetValue(BorderProbeProperty) is ISolidColorBrush border)
            {
                PressedFill = new ImmutableSolidColorBrush(Oklch.MixOklab(muted.Color, border.Color, 0.5));
            }
        }
        else if (change.Property == IsEffectivelyEnabledProperty && !change.GetNewValue<bool>())
        {
            // A button disabled under the pointer gets no PointerExited.
            PseudoClasses.Set(HoverPseudoClass, false);
        }
    }

    protected override void OnPointerEntered(PointerEventArgs e)
    {
        base.OnPointerEntered(e);
        // Arc's hover is (hover: hover) and (pointer: fine): touch never leaves a hover look behind.
        PseudoClasses.Set(HoverPseudoClass, e.Pointer.Type != PointerType.Touch && IsEffectivelyEnabled);
    }

    protected override void OnPointerExited(PointerEventArgs e)
    {
        base.OnPointerExited(e);
        PseudoClasses.Set(HoverPseudoClass, false);
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        PseudoClasses.Set(HoverPseudoClass, false);
    }
}

/// <summary>
/// The answer's reasoning summary (Arc answer's reasoning panel): paragraphs separated by blank lines in the text, 8px apart,
/// 14 / 22.4 in the secondary text color. While the reasoning streams only the last paragraph changes; the others keep
/// their text views. Selectable like the answer.
/// </summary>
internal sealed class AiAnswerReasoningText : StackPanel
{
    /// <summary>The reasoning, paragraphs separated by blank lines.</summary>
    public static readonly StyledProperty<string?> TextProperty =
        AvaloniaProperty.Register<AiAnswerReasoningText, string?>(nameof(Text));

    /// <inheritdoc cref="TextProperty"/>
    public string? Text { get => GetValue(TextProperty); set => SetValue(TextProperty, value); }

    /// <summary>The paragraphs on screen (tests).</summary>
    internal IEnumerable<string?> Paragraphs => Children.OfType<SelectableTextBlock>().Select(p => p.Text);

    protected override Type StyleKeyOverride => typeof(AiAnswerReasoningText);

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == TextProperty)
        {
            Update(change.GetNewValue<string?>());
        }
    }

    /// <summary>One paragraph view per paragraph, reused by position; the text is never relaid out where it did not change.</summary>
    private void Update(string? text)
    {
        var paragraphs = Split(text);
        for (var i = 0; i < paragraphs.Count; i++)
        {
            if (i < Children.Count && Children[i] is SelectableTextBlock existing)
            {
                if (!string.Equals(existing.Text, paragraphs[i], StringComparison.Ordinal))
                {
                    existing.Text = paragraphs[i];
                }
            }
            else
            {
                Children.Add(new AiAnswerReasoningParagraph { Text = paragraphs[i] });
            }
        }
        while (Children.Count > paragraphs.Count)
        {
            Children.RemoveAt(Children.Count - 1);
        }
    }

    /// <summary>The paragraphs of <paramref name="text"/>: split at blank lines, single line breaks kept, empty ones dropped.</summary>
    internal static List<string> Split(string? text)
    {
        var result = new List<string>();
        if (string.IsNullOrWhiteSpace(text))
        {
            return result;
        }
        var normalized = text.Replace("\r\n", "\n", StringComparison.Ordinal);
        foreach (var part in normalized.Split("\n\n", StringSplitOptions.None))
        {
            var trimmed = part.Trim('\n', ' ', '\t');
            if (trimmed.Length > 0)
            {
                result.Add(trimmed);
            }
        }
        return result;
    }
}

/// <summary>A paragraph of <see cref="AiAnswerReasoningText"/> (its own theme: 14 / 22.4, secondary text, wrapped).</summary>
internal sealed class AiAnswerReasoningParagraph : SelectableTextBlock
{
    protected override Type StyleKeyOverride => typeof(AiAnswerReasoningParagraph);
}

/// <summary>
/// The answer area of an <see cref="AiAnswer"/> (Arc's answer host): it holds the answer text and its note, and moves them
/// from one answer to the next. <see cref="Crossfade"/> takes a picture of what is shown, which then fades out over the new
/// answer fading in (Arc lays the old answer absolutely over the new one); after <see cref="Arm"/> the next change of
/// height springs (<see cref="ArcSpring.Smooth"/>), in both directions (ai.md section 8: Arc springs only when it gets
/// shorter), the area clipping while it moves. Every other change of height (text streaming in) follows at once.
/// Reduced motion keeps a 120ms crossfade without the spring; Off switches at once.
/// <para>
/// Code-driven (<see cref="MotionValue{T}"/>): the height's target is the measured height of the new answer, which a
/// ControlTheme cannot know; the old answer is a picture because the same text views show the new one.
/// </para>
/// </summary>
internal sealed class AiAnswerStage : Decorator
{
    /// <summary>How long an armed stage waits for the new answer's height (text is laid out a pass or two later).</summary>
    internal const int ArmWindowMilliseconds = 300;

    /// <summary>Overflow kept visible at the sides while the height moves (focus rings, hover fills).</summary>
    internal const double ClipBleed = 8;

    private static readonly Easing s_linear = new LinearEasing();

    private readonly RectangleGeometry _clip = new();
    private MotionValue<double>? _height;
    private MotionValue<double>? _ghostOpacity;
    private MotionValue<double>? _enter;
    private double _target = double.NaN;
    private bool _armed;
    private long _armedAt;
    private RenderTargetBitmap? _ghost;
    private Size _ghostSize;
    private int _generation;

    /// <summary>True while the height springs.</summary>
    public bool IsMoving => _height?.IsAnimating == true;

    /// <summary>The height shown now.</summary>
    internal double ShownHeight => _height?.Value ?? Bounds.Height;

    /// <summary>Whether the picture of the old answer is still fading out.</summary>
    internal bool HasGhost => _ghost is not null;

    /// <summary>The old answer's opacity while it fades out.</summary>
    internal double GhostOpacity => _ghostOpacity?.Value ?? 0;

    /// <summary>Makes the next change of the content's height spring (within <see cref="ArmWindowMilliseconds"/>).</summary>
    public void Arm()
    {
        if (!ArcMotion.IsFull(this))
        {
            return;
        }
        _armed = true;
        _armedAt = Environment.TickCount64;
        InvalidateMeasure();
    }

    /// <summary>
    /// Fades what is shown now out over <paramref name="exit"/> while what comes next fades in over <paramref name="enter"/>
    /// (Reduced: both 120ms linear; Off: nothing). Call it before the content changes.
    /// </summary>
    public void Crossfade(TimeSpan exit, TimeSpan enter)
    {
        var level = ArcMotion.GetEffectiveLevel(this);
        if (level == ArcMotionLevel.Off || Child is not { } child || !IsLoaded || child.Bounds.Width < 1 || child.Bounds.Height < 1)
        {
            return;
        }
        var generation = ++_generation;
        ReleaseGhost();
        _ghost = Snapshot(child);
        _ghostSize = child.Bounds.Size;
        _ghostOpacity ??= CreateOpacity(_ => InvalidateVisual());
        _enter ??= CreateOpacity(v =>
        {
            if (Child is { } c)
            {
                c.Opacity = Math.Clamp(v, 0, 1);
            }
        });
        var reduced = level == ArcMotionLevel.Reduced;
        var exitSpec = MotionSpec.Tween(reduced ? ArcMotionPresets.Instant : exit, reduced ? s_linear : ArcEasings.Standard);
        var enterSpec = MotionSpec.Tween(reduced ? ArcMotionPresets.Instant : enter, reduced ? s_linear : ArcEasings.Standard);
        if (_ghost is not null)
        {
            _ghostOpacity.Jump(1);
            _ = FadeGhostAsync(exitSpec, generation);
        }
        _enter.Jump(0);
        _ = _enter.AnimateTo(1, enterSpec);
    }

    private async Task FadeGhostAsync(MotionSpec spec, int generation)
    {
        await _ghostOpacity!.AnimateTo(0, spec);
        if (generation == _generation)
        {
            ReleaseGhost();
            InvalidateVisual();
        }
    }

    /// <summary>A picture of <paramref name="child"/> at the screen's scaling, or null where render targets are missing.</summary>
    private RenderTargetBitmap? Snapshot(Control child)
    {
        var scaling = TopLevel.GetTopLevel(this)?.RenderScaling ?? 1;
        var pixels = new PixelSize(Math.Max(1, (int)Math.Ceiling(child.Bounds.Width * scaling)),
            Math.Max(1, (int)Math.Ceiling(child.Bounds.Height * scaling)));
        RenderTargetBitmap? bitmap = null;
        try
        {
            bitmap = new RenderTargetBitmap(pixels, new Vector(96 * scaling, 96 * scaling));
            bitmap.Render(child);
            return bitmap;
        }
        catch (Exception)
        {
            // No render target on this platform: the new answer simply fades in.
            bitmap?.Dispose();
            return null;
        }
    }

    private void ReleaseGhost()
    {
        _ghost?.Dispose();
        _ghost = null;
    }

    private MotionValue<double> CreateOpacity(Action<double> apply)
    {
        var value = new MotionValue<double>(this, 1, MotionVectors.Double);
        value.Changed += apply;
        return value;
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        var armed = _armed && Environment.TickCount64 - _armedAt <= ArmWindowMilliseconds;
        if (Child is not { } child)
        {
            return default;
        }
        child.Measure(availableSize.WithHeight(double.PositiveInfinity));
        var target = child.DesiredSize.Height;
        _height ??= CreateHeight(target);
        if (double.IsNaN(_target))
        {
            _target = target;
            _height.Jump(target);
        }
        else if (Math.Abs(target - _target) >= 0.5)
        {
            _target = target;
            _armed = false;
            if (IsLoaded && ArcMotion.IsFull(this) && (armed || _height.IsAnimating))
            {
                _ = _height.AnimateTo(target, MotionSpec.Spring(ArcSpring.Smooth));
            }
            else
            {
                _height.Jump(target);
            }
        }
        return new Size(child.DesiredSize.Width, _height.IsAnimating ? _height.Value : target);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        if (Child is not { } child)
        {
            return finalSize;
        }
        var height = Math.Max(child.DesiredSize.Height, finalSize.Height);
        child.Arrange(new Rect(0, 0, finalSize.Width, height));
        var overflows = height > finalSize.Height + 0.5 || (_ghost is not null && _ghostSize.Height > finalSize.Height + 0.5);
        if (IsMoving && overflows)
        {
            _clip.Rect = new Rect(-ClipBleed, -ClipBleed, finalSize.Width + 2 * ClipBleed, finalSize.Height + ClipBleed);
            Clip = _clip;
            InvalidateVisual();
        }
        else if (Clip is not null)
        {
            Clip = null;
        }
        return finalSize;
    }

    public override void Render(DrawingContext context)
    {
        base.Render(context);
        if (_ghost is not { } ghost || _ghostOpacity is null || _ghostOpacity.Value <= 0.001)
        {
            return;
        }
        // The source rectangle is in pixels (pitfall: a DIP rectangle takes only the top left at high scaling).
        var source = new Rect(0, 0, ghost.PixelSize.Width, ghost.PixelSize.Height);
        using (context.PushOpacity(_ghostOpacity.Value))
        {
            context.DrawImage(ghost, source, new Rect(_ghostSize));
        }
    }

    private MotionValue<double> CreateHeight(double initial)
    {
        var value = new MotionValue<double>(this, initial, MotionVectors.Double);
        value.Changed += _ => InvalidateMeasure();
        return value;
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        _generation++;
        ReleaseGhost();
        if (Child is { } child)
        {
            child.Opacity = 1;
        }
    }
}
