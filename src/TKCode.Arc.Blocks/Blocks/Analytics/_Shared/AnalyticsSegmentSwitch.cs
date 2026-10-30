using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using TKCode.Arc.DesignTokens;
using TKCode.Arc.Motion;
using TKCode.Arc.Primitives;

namespace TKCode.Arc;

/// <summary>How the selected segment's thumb is painted.</summary>
internal enum AnalyticsThumbStyle
{
    /// <summary>Surface, 1px Border, ShadowResting (KPI drilldown, Journey flow, the 44px default).</summary>
    Raised,

    /// <summary>Surface, shadow <c>0 1px 2px / .08</c> plus a 1px BorderSubtle ring (Metrics dashboard).</summary>
    Ring,

    /// <summary>Foreground at 8%, no edge (Revenue explorer presets).</summary>
    Tint
}

/// <summary>
/// The geometry, colours and spring of an <see cref="AnalyticsSegmentSwitch"/>, one preset per measured look. Copy one
/// with <c>with { … }</c> to vary it. Outer height = <see cref="SegmentHeight"/> + 2 × <see cref="Inset"/> + 2 (border).
/// Radii are ArcDesign tokens (<c>RadiusStepN</c> for N px, <c>RadiusPill</c> for a capsule), so the corner styles
/// reach them.
/// </summary>
internal sealed record AnalyticsSegmentLook
{
    /// <summary>Space between the frame and the segments.</summary>
    public double Inset { get; init; } = 3;

    /// <summary>Space between segments.</summary>
    public double Gap { get; init; } = 2;

    /// <summary>Segment height.</summary>
    public double SegmentHeight { get; init; } = 36;

    /// <summary>Segment horizontal padding.</summary>
    public double SegmentPadding { get; init; } = 12;

    /// <summary>Smallest segment width (0 = text + padding).</summary>
    public double MinSegmentWidth { get; init; }

    /// <summary>Frame radius token (<c>RadiusPill</c>: a capsule).</summary>
    public ArcDesignTokenKind FrameRadius { get; init; } = ArcDesignTokenKind.RadiusStep18;

    /// <summary>Segment and thumb radius token (<c>RadiusPill</c>: a capsule).</summary>
    public ArcDesignTokenKind SegmentRadius { get; init; } = ArcDesignTokenKind.RadiusStep14;

    /// <summary>Focus ring radius token: the segment radius plus the ring's 4px offset (<c>RadiusPill</c> around a capsule).</summary>
    public ArcDesignTokenKind FocusRingRadius { get; init; } = ArcDesignTokenKind.RadiusStep18;

    /// <summary>Label size (14; small looks 12, Journey flow 13), Medium weight, tabular figures.</summary>
    public double FontSize { get; init; } = 14;

    /// <summary>Unselected labels in TextMuted (true) or TextSecondary (false); selected and hovered are Foreground.</summary>
    public bool MutedLabels { get; init; } = true;

    /// <summary>Thumb paint.</summary>
    public AnalyticsThumbStyle Thumb { get; init; } = AnalyticsThumbStyle.Raised;

    /// <summary>Frame filled with SurfaceMuted (false: transparent, Revenue explorer).</summary>
    public bool HasFill { get; init; } = true;

    /// <summary>Thumb spring.</summary>
    public SpringSpec Spring { get; init; } = AnalyticsSprings.Smooth;

    /// <summary>
    /// The 44px Arc segmented control (MRR waterfall, Cohort retention, Usage forecast, Choropleth, Connected scatter,
    /// Horizon chart, Metric explorer ranges): 36px segments, inset 3, gap 2, radii 18 / 14, 14px labels, Smooth thumb.
    /// Public <c>SegmentedControl</c> gives the same look (with <c>AnalyticsSegmented.Spring="Smooth"</c>).
    /// </summary>
    public static AnalyticsSegmentLook Default { get; } = new();

    /// <summary>KPI drilldown range / breakdown switch: 34 high, 26px segments, inset 3, gap 2, radii 10 / 7, 12px, TextMuted, Smooth.</summary>
    public static AnalyticsSegmentLook Compact { get; } = new()
    {
        SegmentHeight = 26, FrameRadius = ArcDesignTokenKind.RadiusStep10, SegmentRadius = ArcDesignTokenKind.RadiusStep7,
        FocusRingRadius = ArcDesignTokenKind.RadiusStep11, FontSize = 12
    };

    /// <summary>Journey flow channel switch: 34 high, 28px segments, inset 2, gap 2, padding 10, radii 10 / 8, 13px, TextSecondary, Quick.</summary>
    public static AnalyticsSegmentLook Flow { get; } = new()
    {
        Inset = 2, SegmentHeight = 28, SegmentPadding = 10, FrameRadius = ArcDesignTokenKind.RadiusStep10,
        SegmentRadius = ArcDesignTokenKind.RadiusStep8, FocusRingRadius = ArcDesignTokenKind.RadiusStep12, FontSize = 13,
        MutedLabels = false, Spring = AnalyticsSprings.Quick
    };

    /// <summary>
    /// Metrics dashboard range switch: 32 high, 26px segments at least 40 wide, inset 2, no gap, padding 10, radii 10 / 8,
    /// 12px, TextSecondary, ring thumb, Quick (measured ω ≈ 20).
    /// </summary>
    public static AnalyticsSegmentLook Small { get; } = new()
    {
        Inset = 2, Gap = 0, SegmentHeight = 26, SegmentPadding = 10, MinSegmentWidth = 40, FrameRadius = ArcDesignTokenKind.RadiusStep10,
        SegmentRadius = ArcDesignTokenKind.RadiusStep8, FocusRingRadius = ArcDesignTokenKind.RadiusStep12, FontSize = 12,
        MutedLabels = false, Thumb = AnalyticsThumbStyle.Ring, Spring = AnalyticsSprings.Quick
    };

    /// <summary>
    /// Revenue explorer presets: 32 high pill, no fill, 26px pill segments, inset 2, no gap, padding 10, 12px,
    /// TextSecondary, Foreground 8% thumb on Gentle; no selection hides the thumb.
    /// </summary>
    public static AnalyticsSegmentLook Pill { get; } = new()
    {
        Inset = 2, Gap = 0, SegmentHeight = 26, SegmentPadding = 10, FrameRadius = ArcDesignTokenKind.RadiusPill,
        SegmentRadius = ArcDesignTokenKind.RadiusPill, FocusRingRadius = ArcDesignTokenKind.RadiusPill, FontSize = 12,
        MutedLabels = false, Thumb = AnalyticsThumbStyle.Tint, HasFill = false, Spring = AnalyticsSprings.Gentle
    };
}

/// <summary>Arguments of <see cref="AnalyticsSegmentSwitch.SelectionChanged"/>.</summary>
internal sealed class AnalyticsSegmentChangedEventArgs(int oldIndex, int newIndex, bool byUser) : EventArgs
{
    /// <summary>Previous index (−1 for none).</summary>
    public int OldIndex { get; } = oldIndex;

    /// <summary>New index (−1 for none).</summary>
    public int NewIndex { get; } = newIndex;

    /// <summary>True when the pointer or keyboard changed it (false for code).</summary>
    public bool ByUser { get; } = byUser;
}

/// <summary>
/// A small segmented switch for the Analytics headers (ranges 7D / 30D / 90D, metric and dimension switches, presets) in
/// the looks the public <c>SegmentedControl</c> cannot take (its sizes are fixed): see <see cref="AnalyticsSegmentLook"/>.
/// A thumb glides under the selected segment (position and width on the look's spring; the first placement lands
/// without motion). Segments are toggle buttons (screen readers hear "pressed", like Arc's <c>aria-pressed</c>);
/// keyboard: one tab stop on the selected segment, ←/↑ previous and →/↓ next (wrapping), Home / End, the selection
/// follows focus; Enter / Space select. <see cref="SelectedIndex"/> −1 shows no thumb (Revenue explorer's custom range).
/// <see cref="IsStretched"/> shares the width equally (Metrics dashboard and Journey flow when narrow).
/// </summary>
internal sealed class AnalyticsSegmentSwitch : Control
{
    /// <summary>Segment labels.</summary>
    public static readonly StyledProperty<IReadOnlyList<string>?> ItemsProperty =
        AvaloniaProperty.Register<AnalyticsSegmentSwitch, IReadOnlyList<string>?>(nameof(Items));

    /// <summary>Spoken names per segment (null: the labels; e.g. "Last 7 days" for "7D").</summary>
    public static readonly StyledProperty<IReadOnlyList<string>?> AccessibleNamesProperty =
        AvaloniaProperty.Register<AnalyticsSegmentSwitch, IReadOnlyList<string>?>(nameof(AccessibleNames));

    /// <summary>Selected index (−1 = none).</summary>
    public static readonly StyledProperty<int> SelectedIndexProperty =
        AvaloniaProperty.Register<AnalyticsSegmentSwitch, int>(nameof(SelectedIndex), -1, defaultBindingMode: Avalonia.Data.BindingMode.TwoWay);

    /// <summary>The look (default <see cref="AnalyticsSegmentLook.Default"/>).</summary>
    public static readonly StyledProperty<AnalyticsSegmentLook> LookProperty =
        AvaloniaProperty.Register<AnalyticsSegmentSwitch, AnalyticsSegmentLook>(nameof(Look), AnalyticsSegmentLook.Default);

    /// <summary>Segments share the available width equally.</summary>
    public static readonly StyledProperty<bool> IsStretchedProperty =
        AvaloniaProperty.Register<AnalyticsSegmentSwitch, bool>(nameof(IsStretched));

    private readonly Border _frame = new() { ClipToBounds = false };
    private readonly Panel _track = new();
    private readonly GlidingIndicator _thumb = new() { IsHitTestVisible = false };
    private readonly SegmentRow _row;
    private readonly List<AnalyticsSegmentButton> _buttons = new();
    private readonly List<IDisposable> _bindings = new();
    private bool _placedOnce;
    private bool _userChange;

    /// <summary>Creates the switch.</summary>
    public AnalyticsSegmentSwitch()
    {
        _row = new SegmentRow(this);
        _track.Children.Add(_thumb);
        _track.Children.Add(_row);
        _frame.Child = _track;
        VisualChildren.Add(_frame);
        LogicalChildren.Add(_frame);
        KeyboardNavigation.SetTabNavigation(_row, KeyboardNavigationMode.Once);
        HorizontalAlignment = HorizontalAlignment.Left;
        VerticalAlignment = VerticalAlignment.Center;
        ActualThemeVariantChanged += (_, _) => ApplyThumbColors();
        AttachedToVisualTree += (_, _) => ApplyThumbColors();
        ApplyLook();
    }

    /// <summary>Raised when the selection changes.</summary>
    public event EventHandler<AnalyticsSegmentChangedEventArgs>? SelectionChanged;

    /// <inheritdoc cref="ItemsProperty"/>
    public IReadOnlyList<string>? Items { get => GetValue(ItemsProperty); set => SetValue(ItemsProperty, value); }

    /// <inheritdoc cref="AccessibleNamesProperty"/>
    public IReadOnlyList<string>? AccessibleNames { get => GetValue(AccessibleNamesProperty); set => SetValue(AccessibleNamesProperty, value); }

    /// <inheritdoc cref="SelectedIndexProperty"/>
    public int SelectedIndex { get => GetValue(SelectedIndexProperty); set => SetValue(SelectedIndexProperty, value); }

    /// <inheritdoc cref="LookProperty"/>
    public AnalyticsSegmentLook Look { get => GetValue(LookProperty); set => SetValue(LookProperty, value); }

    /// <inheritdoc cref="IsStretchedProperty"/>
    public bool IsStretched { get => GetValue(IsStretchedProperty); set => SetValue(IsStretchedProperty, value); }

    /// <summary>The segment buttons (tests, focus).</summary>
    internal IReadOnlyList<AnalyticsSegmentButton> Buttons => _buttons;

    /// <summary>The thumb.</summary>
    internal GlidingIndicator Thumb => _thumb;

    /// <summary>Moves keyboard focus to the selected segment (or the first).</summary>
    public void FocusSelected()
    {
        var index = SelectedIndex >= 0 && SelectedIndex < _buttons.Count ? SelectedIndex : 0;
        if (index < _buttons.Count)
        {
            _buttons[index].Focus(NavigationMethod.Tab);
        }
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == ItemsProperty || change.Property == AccessibleNamesProperty)
        {
            RebuildButtons();
        }
        else if (change.Property == SelectedIndexProperty)
        {
            var byUser = _userChange;
            _userChange = false;
            UpdateChecked();
            _row.InvalidateArrange();
            SelectionChanged?.Invoke(this, new AnalyticsSegmentChangedEventArgs(change.GetOldValue<int>(), change.GetNewValue<int>(), byUser));
        }
        else if (change.Property == LookProperty)
        {
            ApplyLook();
        }
        else if (change.Property == IsStretchedProperty)
        {
            _row.InvalidateMeasure();
        }
    }

    protected override AutomationPeer OnCreateAutomationPeer() => new GroupPeer(this);

    private void RebuildButtons()
    {
        foreach (var button in _buttons)
        {
            button.Click -= OnButtonClick;
            button.KeyDown -= OnButtonKeyDown;
        }
        _row.Children.Clear();
        _buttons.Clear();
        var items = Items ?? Array.Empty<string>();
        for (var i = 0; i < items.Count; i++)
        {
            var button = new AnalyticsSegmentButton { Content = items[i], Index = i };
            var names = AccessibleNames;
            AutomationProperties.SetName(button, names is not null && i < names.Count ? names[i] : items[i]);
            button.Click += OnButtonClick;
            button.KeyDown += OnButtonKeyDown;
            _buttons.Add(button);
            _row.Children.Add(button);
        }
        ApplyLook();
        UpdateChecked();
    }

    private void OnButtonClick(object? sender, RoutedEventArgs e)
    {
        if (sender is AnalyticsSegmentButton button)
        {
            Select(button.Index);
            // A toggle button flips itself on click; the switch decides.
            UpdateChecked();
        }
    }

    private void OnButtonKeyDown(object? sender, KeyEventArgs e)
    {
        if (sender is not AnalyticsSegmentButton button || _buttons.Count == 0)
        {
            return;
        }
        var count = _buttons.Count;
        int? next = e.Key switch
        {
            Key.Left or Key.Up => (button.Index - 1 + count) % count,
            Key.Right or Key.Down => (button.Index + 1) % count,
            Key.Home => 0,
            Key.End => count - 1,
            _ => null
        };
        if (next is not { } index)
        {
            return;
        }
        e.Handled = true;
        Select(index);
        _buttons[index].Focus(NavigationMethod.Directional);
    }

    private void Select(int index)
    {
        if (index == SelectedIndex)
        {
            return;
        }
        _userChange = true;
        SetCurrentValue(SelectedIndexProperty, index);
        _userChange = false;
    }

    private void UpdateChecked()
    {
        for (var i = 0; i < _buttons.Count; i++)
        {
            var selected = i == SelectedIndex;
            _buttons[i].IsChecked = selected;
            // Roving tab stop: only the selected (or, with none, the first) segment is in the tab order.
            _buttons[i].IsTabStop = selected || (SelectedIndex < 0 && i == 0);
        }
    }

    private void ApplyLook()
    {
        var look = Look;
        foreach (var binding in _bindings)
        {
            binding.Dispose();
        }
        _bindings.Clear();
        // Radii follow their tokens (the corner styles scale or zero them); a pill token draws at half the height.
        _bindings.Add(ArcTokens.Bind(_frame, Border.CornerRadiusProperty, look.FrameRadius));
        _frame.BorderThickness = new Thickness(1);
        _frame.Padding = new Thickness(look.Inset);
        _bindings.Add(ArcTokens.Bind(_frame, Border.BorderBrushProperty, ArcDesignTokenKind.Border));
        if (look.HasFill)
        {
            _bindings.Add(ArcTokens.Bind(_frame, Border.BackgroundProperty, ArcDesignTokenKind.SurfaceMuted));
        }
        else
        {
            _frame.Background = null;
        }
        _bindings.Add(ArcTokens.Bind(_thumb, GlidingIndicator.CornerRadiusProperty, look.SegmentRadius));
        foreach (var button in _buttons)
        {
            button.Height = look.SegmentHeight;
            button.MinWidth = look.MinSegmentWidth;
            button.Padding = new Thickness(look.SegmentPadding, 0);
            _bindings.Add(ArcTokens.Bind(button, TemplatedControl.CornerRadiusProperty, look.SegmentRadius));
            _bindings.Add(ArcTokens.Bind(button, AnalyticsSegmentButton.FocusRingRadiusProperty, look.FocusRingRadius));
            button.FontSize = look.FontSize;
            button.Classes.Set("secondary", !look.MutedLabels);
        }
        ApplyThumbColors();
        _row.InvalidateMeasure();
    }

    private void ApplyThumbColors()
    {
        var look = Look;
        var surface = ArcTokens.GetColor(this, ArcDesignTokenKind.Surface);
        var border = ArcTokens.GetColor(this, ArcDesignTokenKind.Border);
        switch (look.Thumb)
        {
            case AnalyticsThumbStyle.Tint:
                _thumb.Fill = new SolidColorBrush(AnalyticsColors.Fade(ArcTokens.GetColor(this, ArcDesignTokenKind.Foreground), 0.08));
                _thumb.Stroke = null;
                _thumb.StrokeThickness = 0;
                _thumb.BoxShadow = default;
                break;
            case AnalyticsThumbStyle.Ring:
                _thumb.Fill = new SolidColorBrush(surface);
                _thumb.Stroke = null;
                _thumb.StrokeThickness = 0;
                _thumb.BoxShadow = new BoxShadows(
                    new BoxShadow { OffsetY = 1, Blur = 2, Color = Color.FromArgb(20, 0, 0, 0) },
                    [new BoxShadow { Spread = 1, Color = ArcTokens.GetColor(this, ArcDesignTokenKind.BorderSubtle) }]);
                break;
            default:
                _thumb.Fill = new SolidColorBrush(surface);
                _thumb.Stroke = new SolidColorBrush(border);
                _thumb.StrokeThickness = 1;
                _thumb.BoxShadow = this.TryFindResource(ArcDesignTokenKind.ShadowResting, ActualThemeVariant, out var value) && value is BoxShadows shadows ? shadows : default;
                break;
        }
    }

    /// <summary>Called by the row after it arranged the segments: glides (or places) the thumb under the selection.</summary>
    private void PlaceThumb()
    {
        if (SelectedIndex < 0 || SelectedIndex >= _buttons.Count)
        {
            _thumb.Hide(_placedOnce);
            return;
        }
        var rect = _buttons[SelectedIndex].Bounds;
        if (rect.Width <= 0)
        {
            return;
        }
        // The 1px edge is centred half a pixel inside the segment, like a CSS border.
        var target = Look.Thumb == AnalyticsThumbStyle.Raised ? rect.Deflate(0.5) : rect;
        if (_thumb.HasTarget && _thumb.CurrentRect == target)
        {
            return;
        }
        _thumb.MoveTo(target, Look.Spring, animate: _placedOnce && IsLoaded);
        _placedOnce = true;
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        _frame.Measure(availableSize);
        return _frame.DesiredSize;
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        _frame.Arrange(new Rect(finalSize));
        return finalSize;
    }

    /// <summary>Lays the segments out in a row (equal widths when stretched) and reports to the switch.</summary>
    private sealed class SegmentRow(AnalyticsSegmentSwitch owner) : Panel
    {
        protected override Size MeasureOverride(Size availableSize)
        {
            var gap = owner.Look.Gap;
            double width = 0;
            double height = 0;
            double widest = 0;
            foreach (var child in Children)
            {
                child.Measure(new Size(double.PositiveInfinity, availableSize.Height));
                width += child.DesiredSize.Width;
                widest = Math.Max(widest, child.DesiredSize.Width);
                height = Math.Max(height, child.DesiredSize.Height);
            }
            width += Math.Max(0, Children.Count - 1) * gap;
            if (owner.IsStretched && double.IsFinite(availableSize.Width))
            {
                width = Math.Max(width, availableSize.Width);
            }
            return new Size(width, height);
        }

        protected override Size ArrangeOverride(Size finalSize)
        {
            var gap = owner.Look.Gap;
            var count = Children.Count;
            var equal = owner.IsStretched && count > 0 ? (finalSize.Width - gap * (count - 1)) / count : 0;
            double x = 0;
            foreach (var child in Children)
            {
                var w = owner.IsStretched ? equal : child.DesiredSize.Width;
                child.Arrange(new Rect(x, 0, w, finalSize.Height));
                x += w + gap;
            }
            owner.PlaceThumb();
            return finalSize;
        }
    }

    private sealed class GroupPeer(AnalyticsSegmentSwitch owner) : ControlAutomationPeer(owner)
    {
        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Group;

        protected override bool IsControlElementCore() => true;
    }
}

/// <summary>
/// One segment of an <see cref="AnalyticsSegmentSwitch"/>: a toggle button (checked = selected) with Medium tabular
/// label, TextMuted (or TextSecondary with the <c>secondary</c> class) turning Foreground on hover and when checked
/// (160ms), and a 2px focus ring 2px outside on keyboard focus. Its theme is
/// <c>Themes/AnalyticsSegmentButtonTheme.axaml</c>.
/// </summary>
internal sealed class AnalyticsSegmentButton : ToggleButton
{
    /// <summary>Corner radius of the focus ring.</summary>
    public static readonly StyledProperty<CornerRadius> FocusRingRadiusProperty =
        AvaloniaProperty.Register<AnalyticsSegmentButton, CornerRadius>(nameof(FocusRingRadius));

    static AnalyticsSegmentButton()
    {
        TextElement.FontFeaturesProperty.OverrideDefaultValue<AnalyticsSegmentButton>(AnalyticsTextCache.TabularFigures);
    }

    /// <inheritdoc cref="FocusRingRadiusProperty"/>
    public CornerRadius FocusRingRadius { get => GetValue(FocusRingRadiusProperty); set => SetValue(FocusRingRadiusProperty, value); }

    /// <summary>Position in the switch.</summary>
    public int Index { get; init; }

    protected override Type StyleKeyOverride => typeof(AnalyticsSegmentButton);

    // The switch owns the checked state: a click asks the switch, it does not toggle by itself.
    protected override void Toggle()
    {
    }
}
