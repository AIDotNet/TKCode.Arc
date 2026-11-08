using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using TKCode.Arc.Motion;

namespace TKCode.Arc;

/// <summary>
/// The pricing calculator's text swap (Arc pricing-calculator, design reference, notes 6.5): the new content fades in over
/// 240 ms on the enter curve (.16, 1, .3, 1) while rising from 0.35em below; the old content leaves out of layout, fading
/// out over 160 ms on the standard curve (.22, 1, .36, 1) while rising 0.3em. Both start together and the box takes the
/// new content's size at once. Reduced motion keeps only a 120 ms fade-in of the new content (the old one goes at once);
/// Off, the first content and hidden swaps replace at once.
/// </summary>
/// <remarks>
/// Code-driven because the outgoing copy must stay on screen outside layout while the new one takes its place, which a
/// ControlTheme transition cannot express. Core's <c>ArcContentSwap</c> modes have other travels and timings (Label rises
/// 2px, its exit lasts 100 ms), so the measured 0.35em / 0.3em and 240 / 160 ms live here.
/// </remarks>
internal static class PricingCalculatorSwapMotion
{
    /// <summary>How far the new content starts below its place, in em.</summary>
    public const double EnterTravelEm = 0.35;

    /// <summary>How far the old content rises while leaving, in em.</summary>
    public const double ExitTravelEm = 0.3;

    /// <summary>Duration of the enter fade (240 ms).</summary>
    public static readonly TimeSpan EnterDuration = TimeSpan.FromMilliseconds(240);

    /// <summary>Duration of the exit fade (160 ms).</summary>
    public static readonly TimeSpan ExitDuration = TimeSpan.FromMilliseconds(160);

    /// <summary>Duration of the reduced-motion fade-in (120 ms).</summary>
    public static readonly TimeSpan ReducedDuration = TimeSpan.FromMilliseconds(120);

    /// <summary>Plays the entrance on <paramref name="target"/>; returns the progress value (null when nothing plays).</summary>
    public static MotionValue<double>? Enter(Control owner, Control target, TranslateTransform translate, double fontSize)
    {
        var level = ArcMotion.GetEffectiveLevel(owner);
        if (!owner.IsLoaded || !owner.IsEffectivelyVisible || level == ArcMotionLevel.Off)
        {
            target.Opacity = 1;
            translate.Y = 0;
            return null;
        }
        var full = level == ArcMotionLevel.Full;
        var offset = full ? EnterTravelEm * fontSize : 0;
        target.Opacity = 0;
        translate.Y = offset;
        var progress = new MotionValue<double>(owner, 0, MotionVectors.Double);
        progress.Changed += p =>
        {
            target.Opacity = Math.Clamp(p, 0, 1);
            translate.Y = offset * (1 - p);
        };
        _ = progress.AnimateTo(1, full
            ? MotionSpec.Tween(EnterDuration, ArcEasings.Enter)
            : MotionSpec.Tween(ReducedDuration, ArcEasings.Standard));
        return progress;
    }

    /// <summary>
    /// Plays the exit on <paramref name="target"/> and calls <paramref name="done"/> on the UI thread afterwards (at once
    /// when nothing plays). Returns the progress value or null.
    /// </summary>
    public static MotionValue<double>? Exit(Control owner, Control target, TranslateTransform translate, double fontSize, Action done)
    {
        if (!owner.IsLoaded || !owner.IsEffectivelyVisible || !ArcMotion.IsFull(owner))
        {
            done();
            return null;
        }
        var startOpacity = target.Opacity;
        var startY = translate.Y;
        var endY = -ExitTravelEm * fontSize;
        var progress = new MotionValue<double>(owner, 0, MotionVectors.Double);
        progress.Changed += p =>
        {
            target.Opacity = startOpacity * (1 - Math.Clamp(p, 0, 1));
            translate.Y = startY + (endY - startY) * p;
        };
        progress.AnimateTo(1, MotionSpec.Tween(ExitDuration, ArcEasings.Standard))
            .ContinueWith(_ => Dispatcher.UIThread.Post(done), TaskScheduler.Default);
        return progress;
    }
}

/// <summary>
/// One line (or a few wrapped lines) of text swapped with <see cref="PricingCalculatorSwapMotion"/>. Only the new text
/// sizes the control; the old copy keeps its own width, pinned left.
/// </summary>
internal sealed class PricingCalculatorSwapText : Control
{
    /// <summary>Defines the <see cref="Text"/> property.</summary>
    public static readonly StyledProperty<string?> TextProperty =
        AvaloniaProperty.Register<PricingCalculatorSwapText, string?>(nameof(Text));

    /// <summary>Defines the <see cref="TextWrapping"/> property.</summary>
    public static readonly StyledProperty<TextWrapping> TextWrappingProperty =
        TextBlock.TextWrappingProperty.AddOwner<PricingCalculatorSwapText>();

    /// <summary>Defines the <see cref="LineHeight"/> property.</summary>
    public static readonly StyledProperty<double> LineHeightProperty =
        TextBlock.LineHeightProperty.AddOwner<PricingCalculatorSwapText>();

    /// <summary>Defines the <see cref="TextAlignment"/> property.</summary>
    public static readonly StyledProperty<TextAlignment> TextAlignmentProperty =
        TextBlock.TextAlignmentProperty.AddOwner<PricingCalculatorSwapText>();

    /// <summary>Defines the <see cref="FontSize"/> property.</summary>
    public static readonly StyledProperty<double> FontSizeProperty = TextElement.FontSizeProperty.AddOwner<PricingCalculatorSwapText>();

    /// <summary>Defines the <see cref="FontWeight"/> property.</summary>
    public static readonly StyledProperty<FontWeight> FontWeightProperty = TextElement.FontWeightProperty.AddOwner<PricingCalculatorSwapText>();

    /// <summary>Defines the <see cref="Foreground"/> property.</summary>
    public static readonly StyledProperty<IBrush?> ForegroundProperty = TextElement.ForegroundProperty.AddOwner<PricingCalculatorSwapText>();

    /// <summary>Font size of the text (inherited).</summary>
    public double FontSize { get => GetValue(FontSizeProperty); set => SetValue(FontSizeProperty, value); }

    /// <summary>Font weight of the text (inherited).</summary>
    public FontWeight FontWeight { get => GetValue(FontWeightProperty); set => SetValue(FontWeightProperty, value); }

    /// <summary>Color of the text (inherited).</summary>
    public IBrush? Foreground { get => GetValue(ForegroundProperty); set => SetValue(ForegroundProperty, value); }

    private sealed class Layer
    {
        public required TextBlock Block { get; init; }
        public required TranslateTransform Translate { get; init; }
        public MotionValue<double>? Progress { get; set; }
    }

    private Layer? _current;
    private readonly List<Layer> _exiting = new();

    /// <summary>The text shown; a change swaps it.</summary>
    public string? Text { get => GetValue(TextProperty); set => SetValue(TextProperty, value); }

    /// <summary>Wrapping of the text (no wrapping by default).</summary>
    public TextWrapping TextWrapping { get => GetValue(TextWrappingProperty); set => SetValue(TextWrappingProperty, value); }

    /// <summary>The line height (NaN: the font's own).</summary>
    public double LineHeight { get => GetValue(LineHeightProperty); set => SetValue(LineHeightProperty, value); }

    /// <summary>Horizontal alignment of the text inside the control.</summary>
    public TextAlignment TextAlignment { get => GetValue(TextAlignmentProperty); set => SetValue(TextAlignmentProperty, value); }

    /// <summary>Outgoing copies still on screen (tests read it).</summary>
    internal int ExitingCount => _exiting.Count;

    /// <summary>The block of the current text (tests read it).</summary>
    internal TextBlock? CurrentBlock => _current?.Block;

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == TextProperty)
        {
            Swap(change.GetNewValue<string?>());
        }
        else if (change.Property == TextWrappingProperty || change.Property == LineHeightProperty || change.Property == TextAlignmentProperty)
        {
            foreach (var layer in Layers())
            {
                Apply(layer.Block);
            }
            InvalidateMeasure();
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

    private void Apply(TextBlock block)
    {
        block.TextWrapping = TextWrapping;
        block.LineHeight = LineHeight;
        block.TextAlignment = TextAlignment;
    }

    private Layer CreateLayer(string? text)
    {
        var translate = new TranslateTransform();
        var block = new TextBlock { Text = text, RenderTransform = translate };
        Apply(block);
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
        var fontSize = GetValue(TextElement.FontSizeProperty);
        var outgoing = _current;
        outgoing.Progress?.Stop();
        _current = CreateLayer(text);
        _current.Progress = PricingCalculatorSwapMotion.Enter(this, _current.Block, _current.Translate, fontSize);
        _exiting.Add(outgoing);
        outgoing.Progress = PricingCalculatorSwapMotion.Exit(this, outgoing.Block, outgoing.Translate, fontSize, () =>
        {
            if (_exiting.Contains(outgoing))
            {
                RemoveLayer(outgoing);
            }
        });
        InvalidateMeasure();
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
            layer.Block.Arrange(new Rect(0, 0, Math.Max(finalSize.Width, layer.Block.DesiredSize.Width), finalSize.Height));
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
            _current.Block.Opacity = 1;
            _current.Translate.Y = 0;
        }
    }
}

/// <summary>
/// A set of alternative contents of which one shows (<see cref="SelectedIndex"/>), swapped with
/// <see cref="PricingCalculatorSwapMotion"/>: the price block and the cap note of a card, the savings line and the yearly
/// offer of the totals. Only the selected child sizes the panel; a leaving child keeps its own size out of layout.
/// </summary>
internal sealed class PricingCalculatorSwapPanel : Panel
{
    /// <summary>Defines the <see cref="SelectedIndex"/> property.</summary>
    public static readonly StyledProperty<int> SelectedIndexProperty =
        AvaloniaProperty.Register<PricingCalculatorSwapPanel, int>(nameof(SelectedIndex));

    private readonly Dictionary<Control, MotionValue<double>?> _exiting = new();
    private readonly Dictionary<Control, MotionValue<double>?> _entering = new();
    private bool _initialized;

    /// <summary>The index of the child shown.</summary>
    public int SelectedIndex { get => GetValue(SelectedIndexProperty); set => SetValue(SelectedIndexProperty, value); }

    /// <summary>Children still leaving (tests read it).</summary>
    internal int ExitingCount => _exiting.Count;

    protected override void ChildrenChanged(object? sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
    {
        base.ChildrenChanged(sender, e);
        foreach (var child in Children)
        {
            child.RenderTransform ??= new TranslateTransform();
        }
        if (!_initialized)
        {
            for (var i = 0; i < Children.Count; i++)
            {
                Children[i].IsVisible = i == SelectedIndex;
            }
        }
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        _initialized = true;
        for (var i = 0; i < Children.Count; i++)
        {
            if (!_exiting.ContainsKey(Children[i]))
            {
                Children[i].IsVisible = i == SelectedIndex;
                Children[i].Opacity = 1;
            }
        }
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == SelectedIndexProperty)
        {
            Swap(change.GetOldValue<int>(), change.GetNewValue<int>());
        }
    }

    private void Swap(int oldIndex, int newIndex)
    {
        if (!_initialized)
        {
            for (var i = 0; i < Children.Count; i++)
            {
                Children[i].IsVisible = i == newIndex;
            }
            return;
        }
        var fontSize = GetValue(TextElement.FontSizeProperty);
        if (oldIndex >= 0 && oldIndex < Children.Count)
        {
            var outgoing = Children[oldIndex];
            if (_entering.Remove(outgoing, out var entering))
            {
                entering?.Dispose();
            }
            var translate = (TranslateTransform)outgoing.RenderTransform!;
            _exiting[outgoing] = PricingCalculatorSwapMotion.Exit(this, outgoing, translate, fontSize, () =>
            {
                if (_exiting.Remove(outgoing, out var value))
                {
                    value?.Dispose();
                }
                if (Children.IndexOf(outgoing) != SelectedIndex)
                {
                    outgoing.IsVisible = false;
                    outgoing.Opacity = 1;
                    translate.Y = 0;
                }
                InvalidateMeasure();
            });
        }
        if (newIndex >= 0 && newIndex < Children.Count)
        {
            var incoming = Children[newIndex];
            if (_exiting.Remove(incoming, out var exiting))
            {
                exiting?.Dispose();
            }
            incoming.IsVisible = true;
            _entering[incoming] = PricingCalculatorSwapMotion.Enter(this, incoming, (TranslateTransform)incoming.RenderTransform!, fontSize);
        }
        InvalidateMeasure();
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        var size = default(Size);
        for (var i = 0; i < Children.Count; i++)
        {
            var child = Children[i];
            child.Measure(availableSize);
            if (i == SelectedIndex)
            {
                size = child.DesiredSize;
            }
        }
        return size;
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        for (var i = 0; i < Children.Count; i++)
        {
            var child = Children[i];
            if (i == SelectedIndex)
            {
                child.Arrange(new Rect(finalSize));
            }
            else
            {
                var desired = child.DesiredSize;
                var width = child.HorizontalAlignment == HorizontalAlignment.Stretch ? Math.Max(finalSize.Width, desired.Width) : desired.Width;
                var x = child.HorizontalAlignment switch
                {
                    HorizontalAlignment.Right => finalSize.Width - width,
                    HorizontalAlignment.Center => (finalSize.Width - width) / 2,
                    _ => 0
                };
                var height = child.VerticalAlignment == VerticalAlignment.Stretch ? Math.Max(finalSize.Height, desired.Height) : desired.Height;
                var y = child.VerticalAlignment switch
                {
                    VerticalAlignment.Bottom => finalSize.Height - height,
                    VerticalAlignment.Center => (finalSize.Height - height) / 2,
                    _ => 0
                };
                child.Arrange(new Rect(x, y, width, height));
            }
        }
        return finalSize;
    }
}
