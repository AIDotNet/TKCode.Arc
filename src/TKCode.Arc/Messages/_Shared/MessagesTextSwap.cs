using TKCode.Arc.Motion;
using Avalonia;
using Avalonia.Animation.Easings;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Media;

namespace TKCode.Arc;

/// <summary>
/// Text that morphs in place (Arc P2 / P3): the new text rises 0.3em out of a soft blur while the old copy lifts
/// away with a subtle blur and fades. Unlike <c>ArcContentSwap</c>, outgoing copies keep the alignment of the
/// slot (left aligned copy stays left aligned) and wrapped text can be balanced like CSS <c>text-wrap: balance</c>.
/// The outgoing copy is out of layout and hidden from assistive technology; only the current text sizes the slot.
/// The first text never animates. Shared by the Messages and Cards categories.
/// </summary>
internal sealed class MessagesTextSwap : Control
{
    public static readonly StyledProperty<string?> TextProperty =
        AvaloniaProperty.Register<MessagesTextSwap, string?>(nameof(Text));

    public static readonly StyledProperty<TextWrapping> TextWrappingProperty =
        TextBlock.TextWrappingProperty.AddOwner<MessagesTextSwap>();

    public static readonly StyledProperty<TextTrimming> TextTrimmingProperty =
        TextBlock.TextTrimmingProperty.AddOwner<MessagesTextSwap>();

    public static readonly StyledProperty<TextAlignment> TextAlignmentProperty =
        TextBlock.TextAlignmentProperty.AddOwner<MessagesTextSwap>();

    public static readonly StyledProperty<double> LineHeightProperty =
        TextBlock.LineHeightProperty.AddOwner<MessagesTextSwap>();

    /// <summary>Wrapped lines are rebalanced to even widths (CSS <c>text-wrap: balance</c>).</summary>
    public static readonly StyledProperty<bool> IsBalancedProperty =
        AvaloniaProperty.Register<MessagesTextSwap, bool>(nameof(IsBalanced));

    /// <summary>1: the new text rises from below (default); -1: it drops from above (a smaller number).</summary>
    public static readonly StyledProperty<int> DirectionProperty =
        AvaloniaProperty.Register<MessagesTextSwap, int>(nameof(Direction), 1);

    /// <summary>When false the copy crossfades without travel or blur.</summary>
    public static readonly StyledProperty<bool> IsTravelEnabledProperty =
        AvaloniaProperty.Register<MessagesTextSwap, bool>(nameof(IsTravelEnabled), true);

    public static readonly StyledProperty<double> FontSizeProperty =
        TextElement.FontSizeProperty.AddOwner<MessagesTextSwap>();

    public string? Text
    {
        get => GetValue(TextProperty);
        set => SetValue(TextProperty, value);
    }

    public TextWrapping TextWrapping
    {
        get => GetValue(TextWrappingProperty);
        set => SetValue(TextWrappingProperty, value);
    }

    public TextTrimming TextTrimming
    {
        get => GetValue(TextTrimmingProperty);
        set => SetValue(TextTrimmingProperty, value);
    }

    public TextAlignment TextAlignment
    {
        get => GetValue(TextAlignmentProperty);
        set => SetValue(TextAlignmentProperty, value);
    }

    public double LineHeight
    {
        get => GetValue(LineHeightProperty);
        set => SetValue(LineHeightProperty, value);
    }

    /// <summary>Wrapped lines are rebalanced to even widths (CSS <c>text-wrap: balance</c>).</summary>
    public bool IsBalanced
    {
        get => GetValue(IsBalancedProperty);
        set => SetValue(IsBalancedProperty, value);
    }

    /// <summary>1: the new text rises from below (default); -1: it drops from above (a smaller number).</summary>
    public int Direction
    {
        get => GetValue(DirectionProperty);
        set => SetValue(DirectionProperty, value);
    }

    /// <summary>When false the copy crossfades without travel or blur.</summary>
    public bool IsTravelEnabled
    {
        get => GetValue(IsTravelEnabledProperty);
        set => SetValue(IsTravelEnabledProperty, value);
    }

    public double FontSize
    {
        get => GetValue(FontSizeProperty);
        set => SetValue(FontSizeProperty, value);
    }

    /// <summary>The block showing the current text (null while empty). Exposed for tests.</summary>
    internal TextBlock? CurrentBlock => _current?.Block;

    internal int ExitingCount => _exiting.Count;

    private sealed class Layer
    {
        public required TextBlock Block { get; init; }
        public required TranslateTransform Translate { get; init; }
        public BlurEffect? Blur { get; set; }
        public MotionValue<double>? Progress { get; set; }
        public double ArrangeWidth { get; set; } = double.NaN;
        public int Generation { get; set; }
    }

    private static readonly Easing s_linear = new LinearEasing();
    private readonly List<Layer> _exiting = new();
    private readonly Stack<Layer> _pool = new();
    private Layer? _current;
    private bool _initialized;

    static MessagesTextSwap()
    {
        AffectsMeasure<MessagesTextSwap>(TextWrappingProperty, TextTrimmingProperty, IsBalancedProperty, LineHeightProperty);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == TextProperty)
        {
            Swap(change.GetNewValue<string?>());
        }
        else if (change.Property == TextWrappingProperty || change.Property == TextTrimmingProperty ||
                 change.Property == TextAlignmentProperty || change.Property == LineHeightProperty)
        {
            if (_current is not null)
            {
                Configure(_current.Block);
            }
            foreach (var layer in _exiting)
            {
                Configure(layer.Block);
            }
        }
    }

    private void Configure(TextBlock block)
    {
        block.TextWrapping = TextWrapping;
        block.TextTrimming = TextTrimming;
        block.TextAlignment = TextAlignment;
        block.LineHeight = LineHeight;
    }

    private Layer Rent(string text)
    {
        Layer layer;
        if (_pool.Count > 0)
        {
            layer = _pool.Pop();
        }
        else
        {
            var translate = new TranslateTransform();
            // Stretch: the block lays its text out at exactly the width it is arranged at (balanced lines stay put).
            var block = new TextBlock
            {
                RenderTransform = translate,
                HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Stretch,
                VerticalAlignment = Avalonia.Layout.VerticalAlignment.Top
            };
            layer = new Layer { Block = block, Translate = translate };
        }
        Configure(layer.Block);
        layer.Block.Text = text;
        layer.Block.Opacity = 1;
        layer.Block.Effect = null;
        layer.Translate.Y = 0;
        layer.ArrangeWidth = double.NaN;
        layer.Generation++;
        AutomationProperties.SetAccessibilityView(layer.Block, AccessibilityView.Default);
        LogicalChildren.Add(layer.Block);
        VisualChildren.Add(layer.Block);
        return layer;
    }

    private void Return(Layer layer)
    {
        layer.Progress?.Stop();
        layer.Generation++;
        _exiting.Remove(layer);
        LogicalChildren.Remove(layer.Block);
        VisualChildren.Remove(layer.Block);
        layer.Block.Effect = null;
        if (_pool.Count < 2)
        {
            _pool.Push(layer);
        }
    }

    private void Swap(string? text)
    {
        var empty = string.IsNullOrEmpty(text);
        var level = ArcMotion.GetEffectiveLevel(this);
        var animate = _initialized && IsLoaded && IsEffectivelyVisible && level != ArcMotionLevel.Off;
        _initialized = true;
        if (!animate)
        {
            foreach (var layer in _exiting.ToArray())
            {
                Return(layer);
            }
            if (empty)
            {
                if (_current is not null)
                {
                    Return(_current);
                    _current = null;
                }
            }
            else if (_current is null)
            {
                _current = Rent(text!);
            }
            else
            {
                _current.Block.Text = text;
            }
            InvalidateMeasure();
            return;
        }
        var full = level == ArcMotionLevel.Full && IsTravelEnabled;
        var direction = Direction >= 0 ? 1 : -1;
        if (_current is not null)
        {
            var outgoing = _current;
            _exiting.Add(outgoing);
            StartExit(outgoing, full, direction);
        }
        _current = empty ? null : Rent(text!);
        if (_current is not null)
        {
            StartEnter(_current, full, direction);
        }
        InvalidateMeasure();
    }

    private double Travel => 0.3 * (double.IsNaN(FontSize) || FontSize <= 0 ? 14 : FontSize);

    private void StartEnter(Layer layer, bool full, int direction)
    {
        var from = full ? Travel * direction : 0;
        layer.Block.Opacity = 0;
        layer.Translate.Y = from;
        if (full)
        {
            layer.Blur = new BlurEffect { Radius = 4 };
            layer.Block.Effect = layer.Blur;
        }
        var progress = new MotionValue<double>(layer.Block, 0, MotionVectors.Double);
        layer.Progress = progress;
        progress.Changed += p =>
        {
            layer.Block.Opacity = Math.Clamp(p, 0, 1);
            if (!full)
            {
                return;
            }
            layer.Translate.Y = from * (1 - p);
            var radius = 4 * (1 - p);
            if (radius < 0.05)
            {
                layer.Block.Effect = null;
            }
            else if (layer.Blur is not null)
            {
                layer.Blur.Radius = radius;
            }
        };
        _ = progress.AnimateTo(1, full
            ? MotionSpec.Tween(ArcMotionPresets.Standard, ArcEasings.Enter)
            : MotionSpec.Tween(ArcMotionPresets.Instant, s_linear));
    }

    private void StartExit(Layer layer, bool full, int direction)
    {
        layer.Progress?.Stop();
        AutomationProperties.SetAccessibilityView(layer.Block, AccessibilityView.Raw);
        var startOpacity = layer.Block.Opacity;
        var startY = layer.Translate.Y;
        var to = full ? -Travel * direction : startY;
        if (full)
        {
            layer.Blur ??= new BlurEffect { Radius = 0 };
            layer.Block.Effect = layer.Blur;
        }
        var generation = layer.Generation;
        var progress = new MotionValue<double>(layer.Block, 0, MotionVectors.Double);
        layer.Progress = progress;
        progress.Changed += p =>
        {
            layer.Block.Opacity = startOpacity * (1 - p);
            if (full)
            {
                layer.Translate.Y = startY + (to - startY) * p;
                if (layer.Blur is not null)
                {
                    layer.Blur.Radius = 2 * p;
                }
            }
        };
        FinishExit(layer, generation, progress.AnimateTo(1, full
            ? MotionSpec.Tween(ArcMotionPresets.Fast, ArcEasings.Standard)
            : MotionSpec.Tween(ArcMotionPresets.Instant, s_linear)));
    }

    private void FinishExit(Layer layer, int generation, Task<bool> task)
    {
        task.ThenOnUiThread(() =>
        {
            if (layer.Generation == generation && _exiting.Contains(layer))
            {
                Return(layer);
            }
        });
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        foreach (var layer in _exiting)
        {
            MeasureLayer(layer, availableSize);
        }
        if (_current is null)
        {
            return default;
        }
        return MeasureLayer(_current, availableSize);
    }

    private Size MeasureLayer(Layer layer, Size availableSize)
    {
        var block = layer.Block;
        block.Measure(availableSize);
        layer.ArrangeWidth = double.NaN;
        if (!IsBalanced || TextWrapping == TextWrapping.NoWrap || double.IsInfinity(availableSize.Width))
        {
            return block.DesiredSize;
        }
        var lines = block.TextLayout.TextLines.Count;
        if (lines < 2)
        {
            return block.DesiredSize;
        }
        // Binary search the narrowest width that keeps the same number of lines (text-wrap: balance).
        var full = availableSize.Width;
        var low = full / lines;
        var high = full;
        for (var i = 0; i < 7 && high - low > 1; i++)
        {
            var mid = (low + high) / 2;
            block.Measure(availableSize.WithWidth(mid));
            if (block.TextLayout.TextLines.Count > lines)
            {
                low = mid;
            }
            else
            {
                high = mid;
            }
        }
        // Leave a pixel of slack so layout rounding never re-wraps the balanced lines.
        var width = Math.Min(full, Math.Ceiling(high) + 1);
        block.Measure(availableSize.WithWidth(width));
        while (block.TextLayout.TextLines.Count > lines && width < full)
        {
            width = Math.Min(full, width + 2);
            block.Measure(availableSize.WithWidth(width));
        }
        layer.ArrangeWidth = width;
        return block.DesiredSize;
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        foreach (var layer in _exiting)
        {
            ArrangeLayer(layer, finalSize);
        }
        if (_current is not null)
        {
            ArrangeLayer(_current, finalSize);
        }
        return finalSize;
    }

    private void ArrangeLayer(Layer layer, Size finalSize)
    {
        var block = layer.Block;
        var current = layer == _current;
        var height = Math.Max(block.DesiredSize.Height, current ? finalSize.Height : 0);
        // An outgoing copy keeps its own width (no re-wrapping while it leaves), aligned like the slot.
        var width = !double.IsNaN(layer.ArrangeWidth) ? layer.ArrangeWidth
            : current ? finalSize.Width
            : Math.Max(finalSize.Width, block.DesiredSize.Width);
        if (current && width >= finalSize.Width)
        {
            block.Arrange(new Rect(0, 0, finalSize.Width, height));
            return;
        }
        var x = TextAlignment switch
        {
            TextAlignment.Center => (finalSize.Width - width) / 2,
            TextAlignment.Right or TextAlignment.End => finalSize.Width - width,
            _ => 0
        };
        block.Arrange(new Rect(x, 0, width, height));
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        foreach (var layer in _exiting.ToArray())
        {
            Return(layer);
        }
        if (_current is not null)
        {
            _current.Progress?.Stop();
            _current.Block.Opacity = 1;
            _current.Block.Effect = null;
            _current.Translate.Y = 0;
        }
    }
}
