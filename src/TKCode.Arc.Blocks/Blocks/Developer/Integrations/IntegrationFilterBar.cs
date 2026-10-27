using TKCode.Arc.DesignTokens;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using TKCode.Arc.Primitives;

namespace TKCode.Arc;

/// <summary>One choice of the filter bar: its key, text and count.</summary>
internal sealed record IntegrationFilterChoice(string Key, string Label, int Count);

/// <summary>
/// A filter of <see cref="IntegrationFilterBar"/>: a 28-high pill radio with its text and its count (tabular, rolling when
/// it changes). Only the checked one is a tab stop (roving); its accessible name is "{text}, {count}".
/// </summary>
[TemplatePart(CountPart, typeof(ArcContentSwap))]
internal sealed class IntegrationFilterPill : RadioButton
{
    internal const string CountPart = "PART_Count";

    /// <summary>The filter's text.</summary>
    public static readonly StyledProperty<string?> LabelProperty =
        AvaloniaProperty.Register<IntegrationFilterPill, string?>(nameof(Label));

    private ArcContentSwap? _count;
    private int? _shownCount;
    private int _pendingCount;

    public IntegrationFilterPill()
    {
        // Each pill is its own group: the bar decides which one is checked.
        GroupName = null;
        IsThreeState = false;
    }

    /// <summary>The filter key.</summary>
    public string Key { get; init; } = string.Empty;

    /// <inheritdoc cref="LabelProperty"/>
    public string? Label { get => GetValue(LabelProperty); set => SetValue(LabelProperty, value); }

    /// <summary>The count shown (tests).</summary>
    public int Count => _pendingCount;

    protected override Type StyleKeyOverride => typeof(IntegrationFilterPill);

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        _count = e.NameScope.Find<ArcContentSwap>(CountPart);
        _shownCount = null;
        SetCount(_pendingCount);
    }

    /// <summary>Shows <paramref name="count"/>; a change rolls up when it grows and down when it shrinks.</summary>
    public void SetCount(int count)
    {
        _pendingCount = count;
        if (_count is null || _shownCount == count)
        {
            return;
        }
        _count.Direction = _shownCount is { } old && count < old ? -1 : 1;
        _shownCount = count;
        var text = new TextBlock { Text = count.ToString(System.Globalization.CultureInfo.CurrentCulture), FontFeatures = TabularNumbers };
        _count.Content = text;
    }

    private static readonly Avalonia.Media.FontFeatureCollection TabularNumbers = new() { new Avalonia.Media.FontFeature { Tag = "tnum", Value = 1 } };

    protected override void OnClick()
    {
        // The bar owns the checked state: a click only asks it to select this filter.
        RaiseEvent(new RoutedEventArgs(ClickEvent));
    }
}

/// <summary>
/// The filter row of <see cref="Integrations"/> (Arc design reference): a radio group of pills ("All", "Connected", one per
/// category, each with its count) and one highlight (SurfaceMuted with a 1px Border inset, pill radius) that glides to the
/// checked pill on Settle. Arrow keys move and select (selection follows focus) and wrap; Home and End jump. Wide, the
/// pills wrap; compact, they scroll sideways without a bar and the checked one is brought into view.
/// </summary>
[TemplatePart(ScrollPart, typeof(ScrollViewer))]
[TemplatePart(PillsPart, typeof(Panel))]
[TemplatePart(HighlightPart, typeof(GlidingIndicator))]
[PseudoClasses(CompactPseudoClass)]
internal sealed class IntegrationFilterBar : TemplatedControl
{
    internal const string ScrollPart = "PART_Scroll";
    internal const string PillsPart = "PART_Pills";
    internal const string HighlightPart = "PART_Highlight";
    internal const string CompactPseudoClass = ":compact";

    /// <summary>Whether the pills scroll sideways (compact) instead of wrapping.</summary>
    public static readonly StyledProperty<bool> IsCompactProperty =
        AvaloniaProperty.Register<IntegrationFilterBar, bool>(nameof(IsCompact));

    private readonly List<IntegrationFilterPill> _pills = new();
    private ScrollViewer? _scroll;
    private Panel? _panel;
    private GlidingIndicator? _highlight;
    private string _selected = IntegrationsQuery.All;
    private Rect _lastTarget;
    private bool _placed;
    private IReadOnlyList<IntegrationFilterChoice> _choices = Array.Empty<IntegrationFilterChoice>();
    private Func<string, int, string>? _nameFormat;

    public IntegrationFilterBar()
    {
        // Tunnel: the radios must not see the arrows first.
        AddHandler(KeyDownEvent, OnKeyDown, RoutingStrategies.Tunnel);
    }

    /// <inheritdoc cref="IsCompactProperty"/>
    public bool IsCompact { get => GetValue(IsCompactProperty); set => SetValue(IsCompactProperty, value); }

    /// <summary>The pills in order (tests).</summary>
    public IReadOnlyList<IntegrationFilterPill> Pills => _pills;

    /// <summary>The highlight (tests).</summary>
    internal GlidingIndicator? Highlight => _highlight;

    /// <summary>The checked filter.</summary>
    public string Selected => _selected;

    /// <summary>Raised when the person picks a filter (click or keys): the key.</summary>
    public event EventHandler<string>? SelectionRequested;

    /// <summary>Rebuilds or updates the pills for <paramref name="choices"/> and checks <paramref name="selected"/>.</summary>
    public void Update(IReadOnlyList<IntegrationFilterChoice> choices, string selected, Func<string, int, string> nameFormat)
    {
        _choices = choices;
        _nameFormat = nameFormat;
        _selected = selected;
        if (_panel is null)
        {
            return;
        }
        var same = _pills.Count == choices.Count;
        for (var i = 0; same && i < choices.Count; i++)
        {
            same = _pills[i].Key == choices[i].Key;
        }
        if (!same)
        {
            foreach (var pill in _pills)
            {
                pill.Click -= OnPillClick;
                pill.SizeChanged -= OnPillSizeChanged;
            }
            _panel.Children.Clear();
            _pills.Clear();
            foreach (var choice in choices)
            {
                var pill = new IntegrationFilterPill { Key = choice.Key };
                pill.Click += OnPillClick;
                pill.SizeChanged += OnPillSizeChanged;
                _pills.Add(pill);
                _panel.Children.Add(pill);
            }
            _placed = false;
        }
        for (var i = 0; i < choices.Count; i++)
        {
            var pill = _pills[i];
            pill.Label = choices[i].Label;
            pill.SetCount(choices[i].Count);
            AutomationProperties.SetName(pill, nameFormat(choices[i].Label, choices[i].Count));
        }
        ApplySelection();
    }

    /// <summary>Focuses the checked pill.</summary>
    public bool FocusSelected(NavigationMethod method) => PillFor(_selected)?.Focus(method) == true;

    private IntegrationFilterPill? PillFor(string key)
    {
        foreach (var pill in _pills)
        {
            if (pill.Key == key)
            {
                return pill;
            }
        }
        return null;
    }

    private void ApplySelection()
    {
        var any = false;
        foreach (var pill in _pills)
        {
            var on = pill.Key == _selected;
            any |= on;
            pill.IsChecked = on;
            pill.IsTabStop = on;
        }
        if (!any && _pills.Count > 0)
        {
            _pills[0].IsChecked = true;
            _pills[0].IsTabStop = true;
        }
        InvalidateArrange();
    }

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        if (_panel is not null)
        {
            _panel.Children.Clear();
        }
        foreach (var pill in _pills)
        {
            pill.Click -= OnPillClick;
            pill.SizeChanged -= OnPillSizeChanged;
        }
        _pills.Clear();
        _scroll = e.NameScope.Find<ScrollViewer>(ScrollPart);
        _panel = e.NameScope.Find<Panel>(PillsPart);
        _highlight = e.NameScope.Find<GlidingIndicator>(HighlightPart);
        _placed = false;
        if (_nameFormat is not null)
        {
            Update(_choices, _selected, _nameFormat);
        }
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == IsCompactProperty)
        {
            PseudoClasses.Set(CompactPseudoClass, IsCompact);
        }
    }

    // A pill that changes width (its count) moves the ones after it: re-place the highlight after the next arrange.
    private void OnPillSizeChanged(object? sender, SizeChangedEventArgs e) => InvalidateArrange();

    protected override Size ArrangeOverride(Size finalSize)
    {
        var size = base.ArrangeOverride(finalSize);
        PlaceHighlight();
        return size;
    }

    private void PlaceHighlight()
    {
        if (_highlight is null || _panel is null)
        {
            return;
        }
        var pill = PillFor(_selected) ?? (_pills.Count > 0 ? _pills[0] : null);
        if (pill is null || pill.Bounds.Width <= 0)
        {
            return;
        }
        // The pills sit in the panel; the highlight is the panel's first child, so they share coordinates.
        var target = pill.Bounds;
        if (_placed && target == _lastTarget)
        {
            return;
        }
        var animate = _placed;
        _lastTarget = target;
        _placed = true;
        // Arc moves the shared highlight on the same curve as the rows (≈ Settle); first placement does not animate.
        _highlight.MoveTo(target, DeveloperSprings.Settle, animate);
    }

    private void OnPillClick(object? sender, RoutedEventArgs e)
    {
        if (sender is IntegrationFilterPill pill)
        {
            e.Handled = true;
            Request(pill, focus: false);
        }
    }

    private void Request(IntegrationFilterPill pill, bool focus)
    {
        if (focus)
        {
            pill.Focus(NavigationMethod.Directional);
        }
        if (IsCompact)
        {
            pill.BringIntoView();
        }
        if (pill.Key != _selected)
        {
            SelectionRequested?.Invoke(this, pill.Key);
        }
        else
        {
            ApplySelection();
        }
    }

    private void OnKeyDown(object? sender, KeyEventArgs e)
    {
        if (_pills.Count == 0 || e.KeyModifiers != KeyModifiers.None)
        {
            return;
        }
        var current = _pills.FindIndex(p => p.IsFocused);
        if (current < 0)
        {
            return;
        }
        var target = e.Key switch
        {
            Key.Left or Key.Up => (current - 1 + _pills.Count) % _pills.Count,
            Key.Right or Key.Down => (current + 1) % _pills.Count,
            Key.Home => 0,
            Key.End => _pills.Count - 1,
            _ => -1,
        };
        if (target < 0)
        {
            return;
        }
        e.Handled = true;
        Request(_pills[target], focus: true);
    }
}
