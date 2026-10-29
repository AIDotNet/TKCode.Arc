using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Layout;
using Avalonia.Media;
using TKCode.Arc.Motion;
using TKCode.Arc.Primitives;

namespace TKCode.Arc;

/// <summary>
/// One line of text swapped in place, as the multi-step form's count and submit label do: the new text rises 0.3em out of
/// a 4px blur while fading in (240 ms, enter curve) and the old text leaves 0.3em the other way, blurring to 2px while it
/// fades out (120 ms, standard curve). Both start together. <see cref="Direction"/> −1 runs the travel the other way (the
/// count rolling back). Only the new text sizes the control; the old copy keeps its own width, pinned left. Reduced motion,
/// Off and the first text replace at once (Arc's reduced branch keeps the old line for one frame only).
/// </summary>
/// <remarks>
/// Code-driven because the outgoing text must stay on screen outside layout while the incoming one takes its place, which a
/// ControlTheme transition cannot express. Core's <c>ArcContentSwap</c> has fixed travel and timings per mode (BlurSwap rises
/// 4px and leaves in 160 ms); this block's swap travels 0.3em and leaves in 120 ms.
/// </remarks>
internal sealed class MultiStepSwap : Control
{
    /// <summary>Defines the <see cref="Text"/> property.</summary>
    public static readonly StyledProperty<string?> TextProperty =
        AvaloniaProperty.Register<MultiStepSwap, string?>(nameof(Text));

    /// <summary>Defines the <see cref="Direction"/> property.</summary>
    public static readonly StyledProperty<int> DirectionProperty =
        AvaloniaProperty.Register<MultiStepSwap, int>(nameof(Direction), 1);

    /// <summary>Defines the <see cref="IsTabular"/> property.</summary>
    public static readonly StyledProperty<bool> IsTabularProperty =
        AvaloniaProperty.Register<MultiStepSwap, bool>(nameof(IsTabular));

    /// <summary>Defines the <see cref="LineHeight"/> property.</summary>
    public static readonly StyledProperty<double> LineHeightProperty =
        TextBlock.LineHeightProperty.AddOwner<MultiStepSwap>();

    /// <summary>How far the texts travel, in em of the inherited font size.</summary>
    public const double TravelEm = 0.3;

    /// <summary>The blur the new text starts from and the one the old text ends at.</summary>
    public const double EnterBlur = 4;
    public const double ExitBlur = 2;

    private static readonly FontFeatureCollection s_tabular = new() { FontFeature.Parse("tnum") };

    private Layer? _current;
    private readonly List<Layer> _exiting = new();

    /// <summary>The text shown; a change swaps it with the motion described on the type.</summary>
    public string? Text { get => GetValue(TextProperty); set => SetValue(TextProperty, value); }

    /// <summary>1: the new text comes up from below and the old one leaves upward; −1: the other way round.</summary>
    public int Direction { get => GetValue(DirectionProperty); set => SetValue(DirectionProperty, value); }

    /// <summary>Tabular figures (the count's digits all have the same width).</summary>
    public bool IsTabular { get => GetValue(IsTabularProperty); set => SetValue(IsTabularProperty, value); }

    /// <summary>The line height of the text (NaN: the font's own), so the swap lines up with the text around it.</summary>
    public double LineHeight { get => GetValue(LineHeightProperty); set => SetValue(LineHeightProperty, value); }

    /// <summary>Outgoing copies still on screen (tests read it).</summary>
    internal int ExitingCount => _exiting.Count;

    /// <summary>The text block of the current text (tests read it).</summary>
    internal TextBlock? CurrentBlock => _current?.Block;

    private sealed class Layer
    {
        public required TextBlock Block { get; init; }
        public required TranslateTransform Translate { get; init; }
        public MotionValue<double>? Progress { get; set; }
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == TextProperty)
        {
            Swap(change.GetNewValue<string?>());
        }
        else if (change.Property == LineHeightProperty)
        {
            foreach (var layer in Layers())
            {
                layer.Block.LineHeight = LineHeight;
            }
            InvalidateMeasure();
        }
        else if (change.Property == IsTabularProperty)
        {
            foreach (var layer in Layers())
            {
                layer.Block.FontFeatures = IsTabular ? s_tabular : null;
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

    private Layer CreateLayer(string? text)
    {
        var translate = new TranslateTransform();
        var block = new TextBlock
        {
            Text = text,
            RenderTransform = translate,
            HorizontalAlignment = HorizontalAlignment.Left,
            LineHeight = LineHeight,
            FontFeatures = IsTabular ? s_tabular : null
        };
        LogicalChildren.Add(block);
        VisualChildren.Add(block);
        return new Layer { Block = block, Translate = translate };
    }

    private void RemoveLayer(Layer layer)
    {
        layer.Progress?.Dispose();
        layer.Progress = null;
        LogicalChildren.Remove(layer.Block);
        VisualChildren.Remove(layer.Block);
        _exiting.Remove(layer);
    }

    private void Swap(string? text)
    {
        if (_current is null)
        {
            _current = CreateLayer(text);
            InvalidateMeasure();
            return;
        }
        if (_current.Block.Text == text)
        {
            return;
        }
        if (!IsLoaded || !IsEffectivelyVisible || !ArcMotion.IsFull(this))
        {
            foreach (var layer in _exiting.ToArray())
            {
                RemoveLayer(layer);
            }
            _current.Progress?.Stop();
            Reset(_current);
            _current.Block.Text = text;
            InvalidateMeasure();
            return;
        }
        var outgoing = _current;
        _exiting.Add(outgoing);
        _current = CreateLayer(text);
        var travel = TravelEm * GetValue(TextElement.FontSizeProperty) * (Direction < 0 ? -1 : 1);
        StartExit(outgoing, -travel);
        StartEnter(_current, travel);
        InvalidateMeasure();
    }

    private static void Reset(Layer layer)
    {
        layer.Block.Opacity = 1;
        layer.Block.Effect = null;
        layer.Translate.Y = 0;
    }

    private void StartEnter(Layer layer, double offset)
    {
        layer.Block.Opacity = 0;
        layer.Translate.Y = offset;
        layer.Block.Effect = ArcBlur.Get(EnterBlur);
        var progress = new MotionValue<double>(this, 0, MotionVectors.Double);
        layer.Progress = progress;
        progress.Changed += p =>
        {
            layer.Block.Opacity = p;
            layer.Translate.Y = offset * (1 - p);
            layer.Block.Effect = ArcBlur.Get(EnterBlur * (1 - p));
        };
        _ = progress.AnimateTo(1, MotionSpec.Tween(ArcMotionPresets.Standard, ArcEasings.Enter));
    }

    private void StartExit(Layer layer, double offset)
    {
        layer.Progress?.Stop();
        var startOpacity = layer.Block.Opacity;
        var startY = layer.Translate.Y;
        var progress = new MotionValue<double>(this, 0, MotionVectors.Double);
        layer.Progress = progress;
        progress.Changed += p =>
        {
            layer.Block.Opacity = startOpacity * (1 - p);
            layer.Translate.Y = startY + (offset - startY) * p;
            layer.Block.Effect = ArcBlur.Get(ExitBlur * p);
        };
        progress.AnimateTo(1, MotionSpec.Tween(ArcMotionPresets.Instant, ArcEasings.Standard))
            .ContinueWith(_ => Dispatcher.Post(() =>
            {
                if (_exiting.Contains(layer))
                {
                    RemoveLayer(layer);
                }
            }), TaskScheduler.Default);
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
        return _current.Block.DesiredSize;
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        foreach (var layer in _exiting)
        {
            // Out of layout: the old text keeps its own width, pinned to the left edge like the new one.
            layer.Block.Arrange(new Rect(0, 0, layer.Block.DesiredSize.Width, finalSize.Height));
        }
        _current?.Block.Arrange(new Rect(finalSize));
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
            Reset(_current);
        }
    }
}
