using TKCode.Arc.Motion;
using TKCode.Arc.Primitives;
using Avalonia;
using Avalonia.Animation.Easings;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.VisualTree;

namespace TKCode.Arc;

/// <summary>
/// Swaps the time list when the day changes (Arc's AnimatePresence pair): the new list comes in from the side of travel
/// (x +16·d → 0 on <see cref="ProjectsSprings.ListSlide"/>, opacity 0 → 1 over 160ms ease-out) while the old one leaves the
/// other way at the same time (x → −16·d and opacity → 0, 160ms standard), out of layout and overlapping. A change while a
/// list is still moving turns the entering list into a leaving one from where it is drawn; at most
/// <see cref="MaxLayers"/> lists are on screen. Reduced motion removes the old list at once and fades the new one in
/// over 120ms; Off and the first list swap at once.
/// </summary>
internal sealed class AvailabilitySlotSwap : Control
{
    /// <summary>The current list and up to two leaving ones.</summary>
    public const int MaxLayers = 3;

    /// <summary>How far the lists travel (16).</summary>
    public static readonly StyledProperty<double> OffsetProperty =
        AvaloniaProperty.Register<AvailabilitySlotSwap, double>(nameof(Offset), 16);

    /// <summary>The gap between time rows of the lists this swap shows (8).</summary>
    public static readonly StyledProperty<double> ItemGapProperty =
        AvaloniaProperty.Register<AvailabilitySlotSwap, double>(nameof(ItemGap), 8);

    // CSS ease-out, cubic-bezier(0, 0, .58, 1): the entering list is opaque early while its slide finishes slowly.
    internal static readonly Easing EaseOut = new SplineEasing(0, 0, 0.58, 1);

    private sealed class Layer
    {
        public required Control Content { get; init; }
        public required TranslateTransform Translate { get; init; }
        public required MotionValue<double> X { get; init; }
        public required MotionValue<double> Fade { get; init; }
    }

    private readonly List<Layer> _leaving = new();
    private Layer? _current;

    public double Offset { get => GetValue(OffsetProperty); set => SetValue(OffsetProperty, value); }

    public double ItemGap { get => GetValue(ItemGapProperty); set => SetValue(ItemGapProperty, value); }

    /// <summary>The list in layout (the one the reader uses).</summary>
    public Control? Current => _current?.Content;

    /// <summary>Lists still leaving, oldest first.</summary>
    public IReadOnlyList<Control> Leaving => _leaving.Select(l => l.Content).ToList();

    /// <summary>The current list's slide (x) and opacity, for tests.</summary>
    internal (double X, double Opacity) CurrentMotion => _current is null ? (0, 1) : (_current.X.Value, _current.Fade.Value);

    /// <summary>A leaving list's slide and opacity, for tests.</summary>
    internal (double X, double Opacity) LeavingMotion(int index) => (_leaving[index].X.Value, _leaving[index].Fade.Value);

    /// <summary>
    /// Shows <paramref name="content"/>. <paramref name="direction"/> is +1 for a later day (enters from the right), −1 for an
    /// earlier one; <paramref name="animate"/> false swaps at once.
    /// </summary>
    public void Show(Control content, int direction, bool animate)
    {
        var level = ArcMotion.GetEffectiveLevel(this);
        var restoreFocus = ContainsFocus();
        var incoming = CreateLayer(content);
        if (_current is null || !animate || !IsLoaded || level == ArcMotionLevel.Off || !IsEffectivelyVisible)
        {
            RemoveAll();
            Add(incoming);
        }
        else if (level == ArcMotionLevel.Reduced)
        {
            RemoveAll();
            Add(incoming);
            incoming.Fade.Jump(0);
            _ = incoming.Fade.AnimateTo(1, MotionSpec.Tween(ArcMotionPresets.Instant, EaseOut));
        }
        else
        {
            var d = direction < 0 ? -1 : 1;
            var outgoing = _current;
            Add(incoming);
            incoming.X.Jump(Offset * d);
            incoming.Fade.Jump(0);
            _ = incoming.X.AnimateTo(0, MotionSpec.Spring(ProjectsSprings.ListSlide));
            _ = incoming.Fade.AnimateTo(1, MotionSpec.Tween(ArcMotionPresets.Fast, EaseOut));
            Leave(outgoing, -Offset * d);
        }
        InvalidateMeasure();
        if (restoreFocus)
        {
            // The focused row left with its list: focus the selected row of the new one (or its first).
            var buttons = content.GetVisualDescendants().OfType<InputElement>().Where(b => b.Focusable).ToList();
            var target = buttons.OfType<AvailabilitySlotButton>().FirstOrDefault(b => b.IsChecked == true) ?? buttons.FirstOrDefault();
            target?.Focus(BlockFocusModality.For(this, NavigationMethod.Directional));
        }
    }

    /// <summary>Replaces the current list in place without motion (the same day's times changed).</summary>
    public void Replace(Control content)
    {
        var restoreFocus = _current is not null && ContainsFocus(_current.Content);
        if (_current is not null)
        {
            Remove(_current);
        }
        Add(CreateLayer(content));
        InvalidateMeasure();
        if (restoreFocus)
        {
            content.GetVisualDescendants().OfType<AvailabilitySlotButton>().FirstOrDefault(b => b.IsChecked == true)
                ?.Focus(BlockFocusModality.For(this, NavigationMethod.Directional));
        }
    }

    private bool ContainsFocus() => _current is not null && ContainsFocus(_current.Content);

    private static bool ContainsFocus(Visual content)
    {
        var focused = TopLevel.GetTopLevel(content)?.FocusManager?.GetFocusedElement() as Visual;
        return focused is not null && (ReferenceEquals(focused, content) || content.IsVisualAncestorOf(focused));
    }

    private Layer CreateLayer(Control content)
    {
        var translate = new TranslateTransform();
        content.RenderTransform = translate;
        var x = new MotionValue<double>(this, 0, MotionVectors.Double);
        var fade = new MotionValue<double>(this, 1, MotionVectors.Double);
        x.Changed += v => translate.X = v;
        fade.Changed += v => content.Opacity = v;
        return new Layer { Content = content, Translate = translate, X = x, Fade = fade };
    }

    private void Add(Layer layer)
    {
        _current = layer;
        layer.Content.Opacity = layer.Fade.Value;
        layer.Translate.X = layer.X.Value;
        LogicalChildren.Add(layer.Content);
        VisualChildren.Add(layer.Content);
    }

    private void Leave(Layer layer, double toX)
    {
        // Out of reach: no pointer, no focus, not read (the new list says the same thing).
        layer.Content.IsHitTestVisible = false;
        foreach (var element in layer.Content.GetVisualDescendants().OfType<InputElement>())
        {
            element.Focusable = false;
        }
        AutomationProperties.SetAccessibilityView(layer.Content, AccessibilityView.Raw);
        _leaving.Add(layer);
        while (_leaving.Count > MaxLayers - 1)
        {
            Remove(_leaving[0]);
        }
        _ = layer.X.AnimateTo(toX, ProjectsSprings.FadeFast());
        _ = RemoveWhenGoneAsync(layer);
    }

    private async Task RemoveWhenGoneAsync(Layer layer)
    {
        await layer.Fade.AnimateTo(0, ProjectsSprings.FadeFast());
        if (_leaving.Contains(layer))
        {
            Remove(layer);
            InvalidateMeasure();
        }
    }

    private void Remove(Layer layer)
    {
        layer.X.Dispose();
        layer.Fade.Dispose();
        _leaving.Remove(layer);
        if (ReferenceEquals(_current, layer))
        {
            _current = null;
        }
        LogicalChildren.Remove(layer.Content);
        VisualChildren.Remove(layer.Content);
    }

    private void RemoveAll()
    {
        foreach (var layer in _leaving.ToArray())
        {
            Remove(layer);
        }
        if (_current is not null)
        {
            Remove(_current);
        }
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        foreach (var layer in _leaving)
        {
            layer.Content.Measure(availableSize);
        }
        if (_current is null)
        {
            return default;
        }
        _current.Content.Measure(availableSize);
        return _current.Content.DesiredSize;
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        foreach (var layer in _leaving)
        {
            // Out of layout: the leaving list keeps its own height, top-aligned under the new one.
            layer.Content.Arrange(new Rect(0, 0, finalSize.Width, layer.Content.DesiredSize.Height));
        }
        _current?.Content.Arrange(new Rect(finalSize));
        return finalSize;
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        foreach (var layer in _leaving.ToArray())
        {
            Remove(layer);
        }
        if (_current is not null)
        {
            _current.X.Jump(0);
            _current.Fade.Jump(1);
            _current.Translate.X = 0;
            _current.Content.Opacity = 1;
        }
    }
}

/// <summary>
/// A line of text that changes with a soft lift (Arc's slots heading and footer): the old text fades out rising 4px into a
/// 2px blur while the new one fades in from 4px below out of the same blur, both over 160ms standard and at the same time,
/// only when the text really changes. The leaving text is out of layout, pinned to the top left. Reduced motion
/// crossfades in 120ms; Off and the first text swap at once.
/// </summary>
internal sealed class AvailabilityTextSwap : Control
{
    public static readonly StyledProperty<string?> TextProperty =
        AvaloniaProperty.Register<AvailabilityTextSwap, string?>(nameof(Text));

    public static readonly StyledProperty<TextWrapping> TextWrappingProperty =
        TextBlock.TextWrappingProperty.AddOwner<AvailabilityTextSwap>();

    public static readonly StyledProperty<TextTrimming> TextTrimmingProperty =
        TextBlock.TextTrimmingProperty.AddOwner<AvailabilityTextSwap>();

    public static readonly StyledProperty<double> LineHeightProperty =
        TextBlock.LineHeightProperty.AddOwner<AvailabilityTextSwap>();

    public static readonly StyledProperty<double> TextLetterSpacingProperty =
        AvaloniaProperty.Register<AvailabilityTextSwap, double>(nameof(TextLetterSpacing));

    /// <summary>How far the texts travel (4).</summary>
    public static readonly StyledProperty<double> TravelProperty =
        AvaloniaProperty.Register<AvailabilityTextSwap, double>(nameof(Travel), 4);

    /// <summary>The blur at the far end of the travel (2).</summary>
    public static readonly StyledProperty<double> BlurRadiusProperty =
        AvaloniaProperty.Register<AvailabilityTextSwap, double>(nameof(BlurRadius), 2);

    private sealed class Layer
    {
        public required TextBlock Block { get; init; }
        public required TranslateTransform Translate { get; init; }
        public MotionValue<double>? Progress { get; set; }
    }

    private readonly List<Layer> _leaving = new();
    private Layer? _current;

    static AvailabilityTextSwap()
    {
        AffectsMeasure<AvailabilityTextSwap>(TextWrappingProperty, TextTrimmingProperty, LineHeightProperty, TextLetterSpacingProperty);
    }

    public AvailabilityTextSwap()
    {
        _current = CreateLayer(null);
    }

    public string? Text { get => GetValue(TextProperty); set => SetValue(TextProperty, value); }

    public TextWrapping TextWrapping { get => GetValue(TextWrappingProperty); set => SetValue(TextWrappingProperty, value); }

    public TextTrimming TextTrimming { get => GetValue(TextTrimmingProperty); set => SetValue(TextTrimmingProperty, value); }

    public double LineHeight { get => GetValue(LineHeightProperty); set => SetValue(LineHeightProperty, value); }

    public double TextLetterSpacing { get => GetValue(TextLetterSpacingProperty); set => SetValue(TextLetterSpacingProperty, value); }

    public double Travel { get => GetValue(TravelProperty); set => SetValue(TravelProperty, value); }

    public double BlurRadius { get => GetValue(BlurRadiusProperty); set => SetValue(BlurRadiusProperty, value); }

    /// <summary>The text block in layout.</summary>
    internal TextBlock CurrentBlock => _current!.Block;

    /// <summary>Texts still leaving (tests).</summary>
    internal int LeavingCount => _leaving.Count;

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == TextProperty)
        {
            Swap(Text);
        }
        else if (change.Property == TextWrappingProperty || change.Property == TextTrimmingProperty || change.Property == LineHeightProperty
                 || change.Property == TextLetterSpacingProperty)
        {
            foreach (var layer in _leaving.Append(_current!))
            {
                Style(layer.Block);
            }
        }
    }

    private void Style(TextBlock block)
    {
        block.TextWrapping = TextWrapping;
        block.TextTrimming = TextTrimming;
        block.LineHeight = LineHeight;
        block.LetterSpacing = TextLetterSpacing;
    }

    private Layer CreateLayer(string? text)
    {
        var translate = new TranslateTransform();
        var block = new TextBlock { Text = text, RenderTransform = translate };
        Style(block);
        LogicalChildren.Add(block);
        VisualChildren.Add(block);
        return new Layer { Block = block, Translate = translate };
    }

    private void Swap(string? text)
    {
        var current = _current!;
        if (string.Equals(current.Block.Text, text, StringComparison.Ordinal))
        {
            return;
        }
        var level = ArcMotion.GetEffectiveLevel(this);
        if (!IsLoaded || level == ArcMotionLevel.Off || !IsEffectivelyVisible || string.IsNullOrEmpty(current.Block.Text))
        {
            current.Progress?.Stop();
            current.Block.Text = text;
            current.Block.Opacity = 1;
            current.Block.Effect = null;
            current.Translate.Y = 0;
            return;
        }
        var full = level == ArcMotionLevel.Full;
        var incoming = CreateLayer(text);
        _current = incoming;
        Leave(current, full);
        Enter(incoming, full);
        InvalidateMeasure();
    }

    private void Enter(Layer layer, bool full)
    {
        var travel = Travel;
        var blur = BlurRadius;
        layer.Block.Opacity = 0;
        if (full)
        {
            layer.Translate.Y = travel;
            layer.Block.Effect = ArcBlur.Get(blur);
        }
        var progress = new MotionValue<double>(this, 0, MotionVectors.Double);
        layer.Progress = progress;
        progress.Changed += p =>
        {
            layer.Block.Opacity = p;
            if (full)
            {
                layer.Translate.Y = travel * (1 - p);
                layer.Block.Effect = ArcBlur.Get(blur * (1 - p));
            }
        };
        _ = progress.AnimateTo(1, full ? ProjectsSprings.FadeFast() : ProjectsSprings.FadeInstant());
    }

    private void Leave(Layer layer, bool full)
    {
        layer.Progress?.Stop();
        AutomationProperties.SetAccessibilityView(layer.Block, AccessibilityView.Raw);
        _leaving.Add(layer);
        var startOpacity = layer.Block.Opacity;
        var startY = layer.Translate.Y;
        var travel = Travel;
        var blur = BlurRadius;
        var progress = new MotionValue<double>(this, 0, MotionVectors.Double);
        layer.Progress = progress;
        progress.Changed += p =>
        {
            layer.Block.Opacity = startOpacity * (1 - p);
            if (full)
            {
                layer.Translate.Y = startY + (-travel - startY) * p;
                layer.Block.Effect = ArcBlur.Get(blur * p);
            }
        };
        _ = RemoveWhenGoneAsync(layer, progress, full);
    }

    private async Task RemoveWhenGoneAsync(Layer layer, MotionValue<double> progress, bool full)
    {
        await progress.AnimateTo(1, full ? ProjectsSprings.FadeFast() : ProjectsSprings.FadeInstant());
        if (_leaving.Remove(layer))
        {
            layer.Progress?.Dispose();
            LogicalChildren.Remove(layer.Block);
            VisualChildren.Remove(layer.Block);
            InvalidateMeasure();
        }
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        foreach (var layer in _leaving)
        {
            layer.Block.Measure(availableSize);
        }
        _current!.Block.Measure(availableSize);
        return _current.Block.DesiredSize;
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        foreach (var layer in _leaving)
        {
            var size = layer.Block.DesiredSize;
            layer.Block.Arrange(new Rect(0, 0, Math.Min(size.Width, finalSize.Width), size.Height));
        }
        _current!.Block.Arrange(new Rect(finalSize));
        return finalSize;
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        foreach (var layer in _leaving.ToArray())
        {
            layer.Progress?.Dispose();
            LogicalChildren.Remove(layer.Block);
            VisualChildren.Remove(layer.Block);
        }
        _leaving.Clear();
        var current = _current!;
        current.Progress?.Stop();
        current.Block.Opacity = 1;
        current.Block.Effect = null;
        current.Translate.Y = 0;
    }
}
