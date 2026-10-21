// Parts of the EmptyStates block, ported from Arc's free empty-states block (MIT, Copyright (c) 2026 Elia Kuratli):
// the tab row with its gliding highlight, the copy that swaps with a blur rise, the copy frame whose height springs, and
// the polite status line.

using TKCode.Arc.Motion;
using TKCode.Arc.Primitives;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Automation.Provider;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using AvaloniaButton = Avalonia.Controls.Button;

namespace TKCode.Arc;

/// <summary>
/// The tab row: equal columns while there is room, never narrower than a label (CSS grid
/// <c>repeat(n, minmax(max-content, 1fr))</c>), with one highlight that glides under the labels on the morph spring.
/// When the labels do not fit, the row clips and slides so the selected tab stays in view.
/// </summary>
internal sealed class EmptyStatesTabPanel : Panel
{
    public static readonly StyledProperty<double> SpacingProperty =
        AvaloniaProperty.Register<EmptyStatesTabPanel, double>(nameof(Spacing), 2);

    private readonly List<EmptyStatesTab> _tabs = new();
    private double[] _widths = [];
    private double _total;
    private double _offset;
    private int _selected = -1;
    private Rect _highlightTarget;

    static EmptyStatesTabPanel()
    {
        AffectsMeasure<EmptyStatesTabPanel>(SpacingProperty);
    }

    public double Spacing { get => GetValue(SpacingProperty); set => SetValue(SpacingProperty, value); }

    /// <summary>The gliding highlight (a child declared in the template, drawn under the tabs).</summary>
    public GlidingIndicator? Highlight { get; set; }

    /// <summary>Called with the index of a tab the user clicked.</summary>
    public Action<int>? Invoked { get; set; }

    public IReadOnlyList<EmptyStatesTab> Tabs => _tabs;

    /// <summary>Shows one tab per header, reusing the tabs it already has.</summary>
    public void SetTabs(IReadOnlyList<string> headers)
    {
        while (_tabs.Count > headers.Count)
        {
            var last = _tabs[^1];
            last.Click -= OnTabClick;
            _tabs.RemoveAt(_tabs.Count - 1);
            Children.Remove(last);
        }
        for (var i = 0; i < headers.Count; i++)
        {
            if (i == _tabs.Count)
            {
                var tab = new EmptyStatesTab();
                tab.Click += OnTabClick;
                _tabs.Add(tab);
                Children.Add(tab);
            }
            _tabs[i].Content = headers[i];
            AutomationProperties.SetName(_tabs[i], headers[i]);
            AutomationProperties.SetPositionInSet(_tabs[i], i + 1);
            AutomationProperties.SetSizeOfSet(_tabs[i], headers.Count);
        }
        if (_selected >= _tabs.Count)
        {
            _selected = -1;
        }
        UpdateTabs();
        InvalidateMeasure();
    }

    /// <summary>The selected tab (−1 for none); the highlight glides to it.</summary>
    public int SelectedIndex
    {
        get => _selected;
        set
        {
            if (_selected == value)
            {
                return;
            }
            _selected = value;
            UpdateTabs();
            if (RevealSelected(Bounds.Width))
            {
                InvalidateArrange();
            }
            else
            {
                UpdateHighlight(animate: true, onlyWhenMoved: false);
            }
        }
    }

    /// <summary>Sets the narrow look (≤ 420 wide) and the tab padding on every tab.</summary>
    public void SetDensity(bool narrow, Thickness padding)
    {
        foreach (var tab in _tabs)
        {
            tab.IsNarrow = narrow;
            tab.Padding = padding;
        }
    }

    private void OnTabClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (sender is EmptyStatesTab tab && _tabs.IndexOf(tab) is >= 0 and var index)
        {
            Invoked?.Invoke(index);
        }
    }

    private void UpdateTabs()
    {
        for (var i = 0; i < _tabs.Count; i++)
        {
            _tabs[i].IsSelected = i == _selected;
            // Only the selected tab is a tab stop; the arrows move between tabs.
            KeyboardNavigation.SetIsTabStop(_tabs[i], i == _selected || (_selected < 0 && i == 0));
        }
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        Highlight?.Measure(availableSize);
        var count = _tabs.Count;
        if (_widths.Length != count)
        {
            _widths = new double[count];
        }
        var height = 0.0;
        var largest = 0.0;
        for (var i = 0; i < count; i++)
        {
            var tab = _tabs[i];
            tab.Measure(new Size(double.PositiveInfinity, availableSize.Height));
            _widths[i] = tab.DesiredSize.Width;
            height = Math.Max(height, tab.DesiredSize.Height);
            largest = Math.Max(largest, _widths[i]);
        }
        var gaps = Spacing * Math.Max(0, count - 1);
        if (double.IsInfinity(availableSize.Width))
        {
            Array.Fill(_widths, largest);
        }
        else
        {
            Share(_widths, availableSize.Width - gaps);
        }
        _total = gaps;
        foreach (var width in _widths)
        {
            _total += width;
        }
        var desired = double.IsInfinity(availableSize.Width) ? _total : Math.Min(_total, availableSize.Width);
        return new Size(desired, height);
    }

    /// <summary>
    /// Grid sizing of <c>minmax(max-content, 1fr)</c> columns: every column gets an equal share of the space, except
    /// columns whose label is wider than the share, which keep their label width while the rest share what is left.
    /// </summary>
    private static void Share(double[] widths, double space)
    {
        var count = widths.Length;
        if (count == 0)
        {
            return;
        }
        Span<bool> fixedColumn = stackalloc bool[count];
        var free = space;
        var flexible = count;
        var share = free / flexible;
        var changed = true;
        while (changed && flexible > 0)
        {
            changed = false;
            share = free / flexible;
            for (var i = 0; i < count; i++)
            {
                if (!fixedColumn[i] && widths[i] > share)
                {
                    fixedColumn[i] = true;
                    free -= widths[i];
                    flexible--;
                    changed = true;
                }
            }
        }
        if (flexible == 0)
        {
            return;
        }
        share = free / flexible;
        for (var i = 0; i < count; i++)
        {
            if (!fixedColumn[i])
            {
                widths[i] = share;
            }
        }
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        Highlight?.Arrange(new Rect(finalSize));
        RevealSelected(finalSize.Width);
        var x = -_offset;
        for (var i = 0; i < _tabs.Count; i++)
        {
            var width = i < _widths.Length ? _widths[i] : 0;
            _tabs[i].Arrange(new Rect(x, 0, width, finalSize.Height));
            x += width + Spacing;
        }
        // Clip only while the labels overflow, so the highlight's resting shadow is not cut at rest.
        ClipToBounds = _total > finalSize.Width + 0.5;
        // Passive layout changes (fonts, resizes, items) snap the highlight; an equal target leaves a glide alone.
        UpdateHighlight(animate: false, onlyWhenMoved: true);
        return finalSize;
    }

    /// <summary>Slides an overflowing row so the selected tab is fully visible; true when the offset changed.</summary>
    private bool RevealSelected(double width)
    {
        var previous = _offset;
        if (_total <= width + 0.5 || _selected < 0 || _selected >= _widths.Length)
        {
            _offset = 0;
            return Math.Abs(previous) > 0.01;
        }
        var start = 0.0;
        for (var i = 0; i < _selected; i++)
        {
            start += _widths[i] + Spacing;
        }
        var end = start + _widths[_selected];
        if (start < _offset)
        {
            _offset = start;
        }
        else if (end > _offset + width)
        {
            _offset = end - width;
        }
        _offset = Math.Clamp(_offset, 0, Math.Max(0, _total - width));
        return Math.Abs(previous - _offset) > 0.01;
    }

    private void UpdateHighlight(bool animate, bool onlyWhenMoved)
    {
        if (Highlight is null)
        {
            return;
        }
        if (_selected < 0 || _selected >= _tabs.Count || _tabs[_selected].Bounds.Width <= 0)
        {
            _highlightTarget = default;
            Highlight.Hide(animate: false);
            return;
        }
        // The highlight's 1px stroke is centered half a pixel inside the tab, like a CSS border.
        var target = _tabs[_selected].Bounds.Deflate(0.5);
        if (onlyWhenMoved && Highlight.HasTarget && Near(target, _highlightTarget))
        {
            return;
        }
        _highlightTarget = target;
        Highlight.MoveTo(target, animate && IsLoaded, ArcSpring.Morph);
    }

    private static bool Near(Rect a, Rect b)
        => Math.Abs(a.X - b.X) < 0.5 && Math.Abs(a.Y - b.Y) < 0.5 && Math.Abs(a.Width - b.Width) < 0.5 && Math.Abs(a.Height - b.Height) < 0.5;

    protected override AutomationPeer OnCreateAutomationPeer() => new TabListAutomationPeer(this);

    private sealed class TabListAutomationPeer(EmptyStatesTabPanel owner) : ControlAutomationPeer(owner)
    {
        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Tab;

        protected override bool IsContentElementCore() => true;

        protected override bool IsControlElementCore() => true;
    }
}

/// <summary>One tab of the <see cref="EmptyStates"/> tab row: a label button that never scales or changes weight.</summary>
[PseudoClasses(SelectedPseudoClass, NarrowPseudoClass)]
internal sealed class EmptyStatesTab : AvaloniaButton
{
    public const string SelectedPseudoClass = ":selected";
    public const string NarrowPseudoClass = ":narrow";

    public static readonly StyledProperty<bool> IsSelectedProperty =
        AvaloniaProperty.Register<EmptyStatesTab, bool>(nameof(IsSelected));

    public static readonly StyledProperty<bool> IsNarrowProperty =
        AvaloniaProperty.Register<EmptyStatesTab, bool>(nameof(IsNarrow));

    public bool IsSelected { get => GetValue(IsSelectedProperty); set => SetValue(IsSelectedProperty, value); }

    public bool IsNarrow { get => GetValue(IsNarrowProperty); set => SetValue(IsNarrowProperty, value); }

    protected override Type StyleKeyOverride => typeof(EmptyStatesTab);

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == IsSelectedProperty)
        {
            PseudoClasses.Set(SelectedPseudoClass, IsSelected);
        }
        else if (change.Property == IsNarrowProperty)
        {
            PseudoClasses.Set(NarrowPseudoClass, IsNarrow);
        }
    }

    protected override AutomationPeer OnCreateAutomationPeer() => new TabAutomationPeer(this);

    private sealed class TabAutomationPeer(EmptyStatesTab owner) : ControlAutomationPeer(owner), ISelectionItemProvider
    {
        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.TabItem;

        protected override bool IsContentElementCore() => true;

        protected override bool IsControlElementCore() => true;

        public bool IsSelected => owner.IsSelected;

        public ISelectionProvider? SelectionContainer => null;

        public void AddToSelection() => Select();

        public void RemoveFromSelection()
        {
        }

        public void Select()
        {
            if (!owner.IsSelected)
            {
                owner.Command?.Execute(owner.CommandParameter);
                owner.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(ClickEvent));
            }
        }
    }
}

/// <summary>
/// One line of copy that swaps like Arc's AnimatePresence <c>mode="popLayout"</c>: the old text leaves out of layout
/// (up 6, blurred 4, fading on the exit duration) while the new one rises from 8 below out of a 4px blur on the
/// considered duration and the enter curve, after <see cref="EnterDelay"/>. Reduced motion only fades; Off swaps at once.
/// </summary>
internal sealed class EmptyStatesTextSwap : Control
{
    public static readonly StyledProperty<string?> TextProperty =
        AvaloniaProperty.Register<EmptyStatesTextSwap, string?>(nameof(Text));

    public static readonly StyledProperty<TextAlignment> TextAlignmentProperty =
        TextBlock.TextAlignmentProperty.AddOwner<EmptyStatesTextSwap>();

    public static readonly StyledProperty<double> FontSizeProperty =
        TextElement.FontSizeProperty.AddOwner<EmptyStatesTextSwap>();

    /// <summary>Line height as a multiple of the font size (--leading-body).</summary>
    public static readonly StyledProperty<double> LeadingProperty =
        AvaloniaProperty.Register<EmptyStatesTextSwap, double>(nameof(Leading), 1.4);

    /// <summary>Wrapped lines are rebalanced to even widths (CSS <c>text-wrap: balance</c>).</summary>
    public static readonly StyledProperty<bool> IsBalancedProperty =
        AvaloniaProperty.Register<EmptyStatesTextSwap, bool>(nameof(IsBalanced));

    /// <summary>Digits use tabular figures (<c>font-variant-numeric: tabular-nums</c>).</summary>
    public static readonly StyledProperty<bool> IsTabularProperty =
        AvaloniaProperty.Register<EmptyStatesTextSwap, bool>(nameof(IsTabular));

    /// <summary>Seconds the incoming text waits under full motion (title .06, description .10).</summary>
    public static readonly StyledProperty<double> EnterDelayProperty =
        AvaloniaProperty.Register<EmptyStatesTextSwap, double>(nameof(EnterDelay));

    private const double EnterTravel = 8;
    private const double ExitTravel = -6;
    private const double Blur = 4;

    private readonly List<Layer> _exiting = new();
    private Layer? _current;
    private bool _initialized;

    static EmptyStatesTextSwap()
    {
        AffectsMeasure<EmptyStatesTextSwap>(IsBalancedProperty, LeadingProperty, FontSizeProperty);
        AffectsArrange<EmptyStatesTextSwap>(TextAlignmentProperty);
    }

    public string? Text { get => GetValue(TextProperty); set => SetValue(TextProperty, value); }

    public TextAlignment TextAlignment { get => GetValue(TextAlignmentProperty); set => SetValue(TextAlignmentProperty, value); }

    public double FontSize { get => GetValue(FontSizeProperty); set => SetValue(FontSizeProperty, value); }

    public double Leading { get => GetValue(LeadingProperty); set => SetValue(LeadingProperty, value); }

    public bool IsBalanced { get => GetValue(IsBalancedProperty); set => SetValue(IsBalancedProperty, value); }

    public bool IsTabular { get => GetValue(IsTabularProperty); set => SetValue(IsTabularProperty, value); }

    public double EnterDelay { get => GetValue(EnterDelayProperty); set => SetValue(EnterDelayProperty, value); }

    /// <summary>The block showing the current text (tests).</summary>
    internal TextBlock? CurrentBlock => _current?.Block;

    internal int ExitingCount => _exiting.Count;

    private sealed class Layer(TextBlock block, TranslateTransform translate)
    {
        public TextBlock Block { get; } = block;

        public TranslateTransform Translate { get; } = translate;

        public MotionValue<double>? Progress { get; set; }

        public double ArrangeWidth { get; set; } = double.NaN;
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == TextProperty)
        {
            Swap(change.GetNewValue<string?>());
        }
        else if (change.Property == TextAlignmentProperty || change.Property == LeadingProperty || change.Property == FontSizeProperty ||
                 change.Property == IsTabularProperty)
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
        block.TextAlignment = TextAlignment;
        block.TextWrapping = TextWrapping.Wrap;
        block.LineHeight = FontSize * Leading;
        block.FontFeatures = IsTabular ? new FontFeatureCollection { FontFeature.Parse("tnum") } : null;
    }

    private Layer Create(string text)
    {
        var translate = new TranslateTransform();
        var block = new TextBlock
        {
            Text = text,
            RenderTransform = translate,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Top
        };
        Configure(block);
        LogicalChildren.Add(block);
        VisualChildren.Add(block);
        return new Layer(block, translate);
    }

    private void Remove(Layer layer)
    {
        layer.Progress?.Stop();
        _exiting.Remove(layer);
        LogicalChildren.Remove(layer.Block);
        VisualChildren.Remove(layer.Block);
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
                Remove(layer);
            }
            if (empty)
            {
                if (_current is not null)
                {
                    Remove(_current);
                    _current = null;
                }
            }
            else if (_current is null)
            {
                _current = Create(text!);
            }
            else
            {
                _current.Progress?.Stop();
                _current.Block.Text = text;
                _current.Block.Opacity = 1;
                _current.Block.Effect = null;
                _current.Translate.Y = 0;
            }
            InvalidateMeasure();
            return;
        }
        var full = level == ArcMotionLevel.Full;
        if (_current is not null)
        {
            var outgoing = _current;
            _exiting.Add(outgoing);
            _ = Exit(outgoing, full);
        }
        _current = empty ? null : Create(text!);
        if (_current is not null)
        {
            Enter(_current, full);
        }
        InvalidateMeasure();
    }

    private void Enter(Layer layer, bool full)
    {
        layer.Block.Opacity = 0;
        if (full)
        {
            layer.Translate.Y = EnterTravel;
            layer.Block.Effect = ArcBlur.Get(Blur);
        }
        var progress = new MotionValue<double>(layer.Block, 0, MotionVectors.Double);
        layer.Progress = progress;
        progress.Changed += p =>
        {
            layer.Block.Opacity = Math.Clamp(p, 0, 1);
            if (full)
            {
                layer.Translate.Y = EnterTravel * (1 - p);
                layer.Block.Effect = ArcBlur.Get(Blur * (1 - p));
            }
        };
        _ = progress.AnimateTo(1, full
            ? MotionSpec.Tween(ArcMotionPresets.Considered, ArcEasings.Enter, TimeSpan.FromSeconds(Math.Max(0, EnterDelay)))
            : MotionSpec.Tween(ArcMotionPresets.Standard, ArcEasings.Enter));
    }

    private async Task Exit(Layer layer, bool full)
    {
        layer.Progress?.Stop();
        AutomationProperties.SetAccessibilityView(layer.Block, AccessibilityView.Raw);
        var startOpacity = layer.Block.Opacity;
        var startY = layer.Translate.Y;
        var progress = new MotionValue<double>(layer.Block, 0, MotionVectors.Double);
        layer.Progress = progress;
        progress.Changed += p =>
        {
            layer.Block.Opacity = startOpacity * (1 - p);
            if (full)
            {
                layer.Translate.Y = startY + (ExitTravel - startY) * p;
                layer.Block.Effect = ArcBlur.Get(Blur * p);
            }
        };
        await progress.AnimateTo(1, MotionSpec.Tween(ArcMotionPresets.Exit, ArcEasings.Standard));
        if (_exiting.Contains(layer))
        {
            Remove(layer);
        }
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        foreach (var layer in _exiting)
        {
            // An outgoing copy keeps the width it was laid out at (no re-wrapping while it leaves).
            if (double.IsNaN(layer.ArrangeWidth))
            {
                MeasureLayer(layer, availableSize);
            }
            else
            {
                layer.Block.Measure(availableSize.WithWidth(layer.ArrangeWidth));
            }
        }
        return _current is null ? default : MeasureLayer(_current, availableSize);
    }

    private Size MeasureLayer(Layer layer, Size availableSize)
    {
        var block = layer.Block;
        block.Measure(availableSize);
        layer.ArrangeWidth = double.NaN;
        if (!IsBalanced || double.IsInfinity(availableSize.Width))
        {
            return block.DesiredSize;
        }
        var lines = block.TextLayout.TextLines.Count;
        if (lines < 2)
        {
            return block.DesiredSize;
        }
        // The narrowest width that keeps the same number of lines (text-wrap: balance).
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
        // A pixel of slack so layout rounding never re-wraps the balanced lines.
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
            ArrangeLayer(layer, finalSize, current: false);
        }
        if (_current is not null)
        {
            ArrangeLayer(_current, finalSize, current: true);
        }
        return finalSize;
    }

    private void ArrangeLayer(Layer layer, Size finalSize, bool current)
    {
        var block = layer.Block;
        var height = Math.Max(block.DesiredSize.Height, current ? finalSize.Height : 0);
        var width = !double.IsNaN(layer.ArrangeWidth) ? layer.ArrangeWidth : current ? finalSize.Width : Math.Max(finalSize.Width, block.DesiredSize.Width);
        if (!current && double.IsNaN(layer.ArrangeWidth))
        {
            layer.ArrangeWidth = width;
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
            Remove(layer);
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

/// <summary>
/// The copy block: its height springs (smooth) to the incoming text so the action below never jumps. Only a height
/// change shortly after the copy changed animates (Arc arms it for 700ms); resizes and reflows jump. The content is not
/// clipped, like Arc's frame.
/// </summary>
internal sealed class EmptyStatesCopyFrame : Decorator
{
    private const long ArmMilliseconds = 700;

    private MotionValue<double>? _height;
    private double _target = double.NaN;
    private long _armedUntil;

    /// <summary>The copy is about to change: the next height change springs.</summary>
    public void Arm() => _armedUntil = Environment.TickCount64 + ArmMilliseconds;

    /// <summary>True while the height is springing (tests).</summary>
    internal bool IsMoving => _height?.IsAnimating == true;

    protected override Size MeasureOverride(Size availableSize)
    {
        if (Child is not { } child)
        {
            return default;
        }
        child.Measure(availableSize.WithHeight(double.PositiveInfinity));
        var target = child.DesiredSize.Height;
        if (_height is null)
        {
            _height = new MotionValue<double>(this, target, MotionVectors.Double);
            _height.Changed += _ => InvalidateMeasure();
        }
        else if (double.IsNaN(_target) || Math.Abs(target - _target) > 0.5)
        {
            if (IsLoaded && ArcMotion.IsFull(this) && Environment.TickCount64 <= _armedUntil && !double.IsNaN(_target))
            {
                _ = _height.AnimateTo(target, MotionSpec.Spring(ArcSpring.Smooth));
            }
            else
            {
                _height.Jump(target);
            }
        }
        _target = target;
        return new Size(child.DesiredSize.Width, Math.Max(0, _height.Value));
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        Child?.Arrange(new Rect(0, 0, finalSize.Width, Child.DesiredSize.Height));
        return finalSize;
    }
}

/// <summary>A hidden polite live region (Arc's <c>aria-live="polite"</c> status line).</summary>
internal sealed class EmptyStatesAnnouncer : Control
{
    private string? _last;
    private bool _toggle;

    public EmptyStatesAnnouncer()
    {
        AutomationProperties.SetLiveSetting(this, AutomationLiveSetting.Polite);
        IsHitTestVisible = false;
        Focusable = false;
    }

    /// <summary>The last announced text (without the repeat marker).</summary>
    public string? Announcement => _last;

    public void Announce(string? text)
    {
        if (string.IsNullOrEmpty(text))
        {
            _last = null;
            AutomationProperties.SetName(this, string.Empty);
            return;
        }
        // A repeated message toggles an invisible marker so the name still changes and is read again.
        _toggle = text == _last && !_toggle;
        _last = text;
        AutomationProperties.SetName(this, _toggle ? text + "​" : text);
    }

    protected override Size MeasureOverride(Size availableSize) => default;
}
