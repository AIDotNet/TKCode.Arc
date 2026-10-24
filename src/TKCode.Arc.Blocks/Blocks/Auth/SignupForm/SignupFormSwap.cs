// Ported from Arc's free signup-form block (MIT, Copyright (c) 2026 Elia Kuratli): the swap() presence of signup-form.tsx.
using Avalonia;
using Avalonia.Animation.Easings;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Media;
using TKCode.Arc.Motion;
using TKCode.Arc.Primitives;

namespace TKCode.Arc;

/// <summary>
/// One line of text that changes in place, Arc's <c>AnimatePresence mode="popLayout"</c> with <c>swap(reduce)</c>: a new
/// key lifts the old line out of layout and away (y −4, blur 2, opacity 0 in 160 ms on motion's default ease-out) while the
/// new line rises out of a soft blur (y 6, blur 4, opacity 0 → rest in 240 ms on the enter curve). The same key updates the
/// text without motion. Reduced motion fades the new line in over 120 ms and drops the old one at once; Off, the first line
/// and a hidden line change at once. An empty line takes no height (an empty block span).
/// </summary>
/// <remarks>
/// Code-driven because a ControlTheme cannot keep the outgoing line alive outside layout while the incoming one takes its
/// place. Used by the password feedback and the footer status.
/// </remarks>
internal sealed class SignupFormSwap : Control
{
    /// <summary>Motion's default curve for a tween without an ease (WAAPI <c>ease-out</c>), measured on the live preview.</summary>
    internal static readonly Easing EaseOut = new SplineEasing(0, 0, 0.58, 1);

    public static readonly StyledProperty<double> FontSizeProperty = TextElement.FontSizeProperty.AddOwner<SignupFormSwap>();
    public static readonly StyledProperty<IBrush?> ForegroundProperty = TextElement.ForegroundProperty.AddOwner<SignupFormSwap>();
    public static readonly StyledProperty<FontWeight> FontWeightProperty = TextElement.FontWeightProperty.AddOwner<SignupFormSwap>();

    /// <summary>Line height of each line.</summary>
    public static readonly StyledProperty<double> LineHeightProperty =
        TextBlock.LineHeightProperty.AddOwner<SignupFormSwap>();

    /// <summary>Letter spacing of each line.</summary>
    public static readonly StyledProperty<double> LetterSpacingProperty =
        TextBlock.LetterSpacingProperty.AddOwner<SignupFormSwap>();

    /// <summary>Whether a long line wraps (the password feedback) or ends in an ellipsis (the status).</summary>
    public static readonly StyledProperty<bool> IsSingleLineProperty =
        AvaloniaProperty.Register<SignupFormSwap, bool>(nameof(IsSingleLine));

    private const double EnterOffset = 6;
    private const double EnterBlur = 4;
    private const double ExitOffset = -4;
    private const double ExitBlur = 2;

    private Layer? _current;
    private readonly List<Layer> _exiting = new();

    static SignupFormSwap()
    {
        AffectsMeasure<SignupFormSwap>(IsSingleLineProperty, LineHeightProperty, LetterSpacingProperty);
    }

    public double FontSize { get => GetValue(FontSizeProperty); set => SetValue(FontSizeProperty, value); }

    public IBrush? Foreground { get => GetValue(ForegroundProperty); set => SetValue(ForegroundProperty, value); }

    public FontWeight FontWeight { get => GetValue(FontWeightProperty); set => SetValue(FontWeightProperty, value); }

    /// <inheritdoc cref="LineHeightProperty"/>
    public double LineHeight { get => GetValue(LineHeightProperty); set => SetValue(LineHeightProperty, value); }

    /// <inheritdoc cref="LetterSpacingProperty"/>
    public double LetterSpacing { get => GetValue(LetterSpacingProperty); set => SetValue(LetterSpacingProperty, value); }

    /// <inheritdoc cref="IsSingleLineProperty"/>
    public bool IsSingleLine { get => GetValue(IsSingleLineProperty); set => SetValue(IsSingleLineProperty, value); }

    /// <summary>The key of the line on screen.</summary>
    public string? Key => _current?.Key;

    /// <summary>The text of the line on screen.</summary>
    public string Text => _current?.Block.Text ?? string.Empty;

    /// <summary>Number of outgoing lines still on screen (tests read it).</summary>
    public int ExitingCount => _exiting.Count;

    /// <summary>The block of the current line (tests read it).</summary>
    internal TextBlock? CurrentBlock => _current?.Block;

    private sealed class Layer
    {
        public required string Key { get; set; }
        public required TextBlock Block { get; init; }
        public required TranslateTransform Translate { get; init; }
        public MotionValue<double>? Progress { get; set; }
    }

    /// <summary>Shows <paramref name="text"/> under <paramref name="key"/>: a new key swaps, the same key rewrites in place.</summary>
    public void Show(string key, string text)
    {
        if (_current is null)
        {
            _current = CreateLayer(key, text);
            InvalidateMeasure();
            return;
        }
        if (_current.Key == key)
        {
            _current.Block.Text = text;
            InvalidateMeasure();
            return;
        }
        var level = ArcMotion.GetEffectiveLevel(this);
        if (!IsLoaded || level == ArcMotionLevel.Off || !IsEffectivelyVisible)
        {
            foreach (var layer in _exiting.ToArray())
            {
                RemoveLayer(layer);
            }
            _current.Progress?.Stop();
            ResetLayer(_current);
            _current.Key = key;
            _current.Block.Text = text;
            InvalidateMeasure();
            return;
        }
        var full = level == ArcMotionLevel.Full;
        var outgoing = _current;
        _exiting.Add(outgoing);
        _current = CreateLayer(key, text);
        StartExit(outgoing, full);
        StartEnter(_current, full);
        InvalidateMeasure();
    }

    private Layer CreateLayer(string key, string text)
    {
        var translate = new TranslateTransform();
        var block = new TextBlock { Text = text, RenderTransform = translate };
        ApplyLineStyle(block);
        LogicalChildren.Add(block);
        VisualChildren.Add(block);
        return new Layer { Key = key, Block = block, Translate = translate };
    }

    private void ApplyLineStyle(TextBlock block)
    {
        block.LineHeight = LineHeight;
        block.LetterSpacing = LetterSpacing;
        block.TextWrapping = IsSingleLine ? TextWrapping.NoWrap : TextWrapping.Wrap;
        block.TextTrimming = IsSingleLine ? TextTrimming.CharacterEllipsis : TextTrimming.None;
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == IsSingleLineProperty || change.Property == LineHeightProperty || change.Property == LetterSpacingProperty)
        {
            foreach (var layer in Layers())
            {
                ApplyLineStyle(layer.Block);
            }
        }
    }

    private IEnumerable<Layer> Layers()
    {
        foreach (var layer in _exiting)
        {
            yield return layer;
        }
        if (_current is not null)
        {
            yield return _current;
        }
    }

    private void RemoveLayer(Layer layer)
    {
        layer.Progress?.Dispose();
        layer.Progress = null;
        LogicalChildren.Remove(layer.Block);
        VisualChildren.Remove(layer.Block);
        _exiting.Remove(layer);
    }

    private static void ResetLayer(Layer layer)
    {
        layer.Block.Opacity = 1;
        layer.Block.Effect = null;
        layer.Translate.Y = 0;
    }

    private void StartEnter(Layer layer, bool full)
    {
        var offset = full ? EnterOffset : 0;
        var blur = full ? EnterBlur : 0;
        layer.Block.Opacity = 0;
        layer.Translate.Y = offset;
        layer.Block.Effect = ArcBlur.Get(blur);
        var progress = new MotionValue<double>(this, 0, MotionVectors.Double);
        layer.Progress = progress;
        progress.Changed += p =>
        {
            layer.Block.Opacity = p;
            layer.Translate.Y = offset * (1 - p);
            layer.Block.Effect = ArcBlur.Get(blur * (1 - p));
        };
        var spec = full
            ? MotionSpec.Tween(ArcMotionPresets.Standard, ArcEasings.Enter)
            : MotionSpec.Tween(ArcMotionPresets.Instant, EaseOut);
        _ = progress.AnimateTo(1, spec);
    }

    private void StartExit(Layer layer, bool full)
    {
        layer.Progress?.Stop();
        if (!full)
        {
            // Reduced motion: the old line leaves with a zero duration.
            RemoveLayer(layer);
            return;
        }
        var startOpacity = layer.Block.Opacity;
        var startY = layer.Translate.Y;
        var progress = new MotionValue<double>(this, 0, MotionVectors.Double);
        layer.Progress = progress;
        progress.Changed += p =>
        {
            layer.Block.Opacity = startOpacity * (1 - p);
            layer.Translate.Y = startY + (ExitOffset - startY) * p;
            layer.Block.Effect = ArcBlur.Get(ExitBlur * p);
        };
        _ = RemoveWhenDone(layer, progress.AnimateTo(1, MotionSpec.Tween(ArcMotionPresets.Fast, EaseOut)));
    }

    private async Task RemoveWhenDone(Layer layer, Task<bool> exit)
    {
        // Resumes on the UI thread (the dispatcher's synchronization context).
        await exit;
        if (_exiting.Contains(layer))
        {
            RemoveLayer(layer);
        }
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        foreach (var layer in _exiting)
        {
            layer.Block.Measure(availableSize);
        }
        if (_current is null)
        {
            return default;
        }
        _current.Block.Measure(availableSize);
        // An empty line is an empty block span: no height.
        return string.IsNullOrEmpty(_current.Block.Text) ? new Size(0, 0) : _current.Block.DesiredSize;
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        foreach (var layer in _exiting)
        {
            // popLayout: the outgoing line keeps its own size at its old place (top left).
            var size = layer.Block.DesiredSize;
            layer.Block.Arrange(new Rect(0, 0, Math.Max(size.Width, IsSingleLine ? finalSize.Width : size.Width), size.Height));
        }
        if (_current is not null)
        {
            _current.Block.Arrange(new Rect(0, 0, finalSize.Width, Math.Max(finalSize.Height, _current.Block.DesiredSize.Height)));
        }
        return finalSize;
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        foreach (var layer in _exiting.ToArray())
        {
            RemoveLayer(layer);
        }
        if (_current is not null)
        {
            _current.Progress?.Stop();
            ResetLayer(_current);
        }
    }
}
