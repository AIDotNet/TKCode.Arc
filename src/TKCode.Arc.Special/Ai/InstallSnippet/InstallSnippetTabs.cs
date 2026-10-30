using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Automation.Provider;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.VisualTree;
using TKCode.Arc.Motion;
using TKCode.Arc.Primitives;
using AvaloniaButton = Avalonia.Controls.Button;

namespace TKCode.Arc;

/// <summary>One tab of an <see cref="InstallSnippetTabs"/> list: an id, a name and an optional glyph or monogram.</summary>
internal sealed record InstallSnippetTabItem(string Id, string Label, Geometry? Mark, string? Monogram = null);

/// <summary>
/// A tab list of an <see cref="InstallSnippet"/> (the frameworks, or the package managers): tabs on a muted track and a
/// raised highlight that slides to the measured tab on a bounce-free spring (visual duration 0.24s; reduced motion moves
/// it at once). Roving focus: only the selected tab is a tab stop; the arrow keys move to and select the previous or next
/// tab (wrapping), Home and End the first and last. <see cref="Picked"/> reports the reader's picks only.
/// </summary>
[TemplatePart(HighlightPart, typeof(GlidingIndicator))]
[TemplatePart(StripPart, typeof(StackPanel))]
[PseudoClasses(SmallPseudoClass)]
internal sealed class InstallSnippetTabs : TemplatedControl
{
    internal const string HighlightPart = "PART_Highlight";
    internal const string StripPart = "PART_Strip";
    internal const string SmallPseudoClass = ":small";

    /// <summary>The highlight's glide: bounce-free, visual duration 0.24s.</summary>
    internal static readonly SpringSpec Glide = SpringSpec.FromVisual(0.24, 0);

    /// <summary>The smaller package manager size.</summary>
    public static readonly StyledProperty<bool> IsSmallProperty =
        AvaloniaProperty.Register<InstallSnippetTabs, bool>(nameof(IsSmall));

    /// <summary>Whether the tabs show their glyph or monogram.</summary>
    public static readonly StyledProperty<bool> ShowsMarksProperty =
        AvaloniaProperty.Register<InstallSnippetTabs, bool>(nameof(ShowsMarks), true);

    /// <summary>Whether the tabs show their names (the narrow package manager tabs show marks only).</summary>
    public static readonly StyledProperty<bool> ShowsLabelsProperty =
        AvaloniaProperty.Register<InstallSnippetTabs, bool>(nameof(ShowsLabels), true);

    private readonly List<InstallSnippetTab> _tabs = [];
    private IReadOnlyList<InstallSnippetTabItem> _items = [];
    private GlidingIndicator? _highlight;
    private StackPanel? _strip;
    private string? _selected;
    private bool _animateNext;
    private Rect _target;

    /// <inheritdoc cref="IsSmallProperty"/>
    public bool IsSmall { get => GetValue(IsSmallProperty); set => SetValue(IsSmallProperty, value); }

    /// <inheritdoc cref="ShowsMarksProperty"/>
    public bool ShowsMarks { get => GetValue(ShowsMarksProperty); set => SetValue(ShowsMarksProperty, value); }

    /// <inheritdoc cref="ShowsLabelsProperty"/>
    public bool ShowsLabels { get => GetValue(ShowsLabelsProperty); set => SetValue(ShowsLabelsProperty, value); }

    /// <summary>Raised when the reader picks a tab (click, Enter, Space or an arrow key), with its id.</summary>
    public event EventHandler<string>? Picked;

    /// <summary>The tabs, in order.</summary>
    internal IReadOnlyList<InstallSnippetTab> Tabs => _tabs;

    /// <summary>The highlight.</summary>
    internal GlidingIndicator? Highlight => _highlight;

    /// <summary>The selected tab's id.</summary>
    internal string? SelectedId => _selected;

    /// <summary>Shows <paramref name="items"/> and selects <paramref name="selected"/>; same ids keep their tabs.</summary>
    public void SetItems(IReadOnlyList<InstallSnippetTabItem> items, string? selected)
    {
        _items = items;
        Build();
        Select(selected, animate: false);
    }

    /// <summary>Selects a tab; the highlight slides there when <paramref name="animate"/>.</summary>
    public void Select(string? id, bool animate)
    {
        var changed = id != _selected;
        _selected = id;
        foreach (var tab in _tabs)
        {
            tab.IsSelected = tab.Id == id;
        }
        _animateNext = animate && changed;
        PlaceHighlight();
        InvalidateArrange();
    }

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        _highlight = e.NameScope.Find<GlidingIndicator>(HighlightPart);
        _strip = e.NameScope.Find<StackPanel>(StripPart);
        _tabs.Clear();
        Build();
        Select(_selected, animate: false);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == IsSmallProperty)
        {
            PseudoClasses.Set(SmallPseudoClass, IsSmall);
            foreach (var tab in _tabs)
            {
                tab.IsSmall = IsSmall;
            }
        }
        else if (change.Property == ShowsMarksProperty || change.Property == ShowsLabelsProperty)
        {
            foreach (var tab in _tabs)
            {
                tab.SetShown(ShowsMarks, ShowsLabels);
            }
        }
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        var size = base.ArrangeOverride(finalSize);
        PlaceHighlight();
        return size;
    }

    protected override AutomationPeer OnCreateAutomationPeer() => new ListPeer(this);

    private void Build()
    {
        if (_strip is null)
        {
            return;
        }
        var same = _items.Count == _tabs.Count;
        for (var i = 0; same && i < _items.Count; i++)
        {
            same = _tabs[i].Id == _items[i].Id;
        }
        if (!same)
        {
            foreach (var tab in _tabs)
            {
                tab.Click -= OnTabClick;
                tab.KeyDown -= OnTabKeyDown;
            }
            _tabs.Clear();
            _strip.Children.Clear();
            foreach (var item in _items)
            {
                var tab = new InstallSnippetTab(item.Id) { IsSmall = IsSmall };
                tab.Click += OnTabClick;
                tab.KeyDown += OnTabKeyDown;
                _tabs.Add(tab);
                _strip.Children.Add(tab);
            }
        }
        for (var i = 0; i < _items.Count; i++)
        {
            _tabs[i].Show(_items[i]);
            _tabs[i].SetShown(ShowsMarks, ShowsLabels);
        }
    }

    private void PlaceHighlight()
    {
        if (_highlight is null)
        {
            return;
        }
        var tab = _tabs.FirstOrDefault(t => t.IsSelected);
        if (tab is null)
        {
            _highlight.Hide(animate: false);
            return;
        }
        if (tab.Bounds.Width <= 0)
        {
            return;
        }
        var target = tab.Bounds;
        // Compare with the last target, not the drawn rect: an arrange pass during a glide must not cut it short.
        if (_highlight.HasTarget && !_animateNext && _target == target)
        {
            return;
        }
        var animate = _animateNext && _highlight.HasTarget;
        _animateNext = false;
        _target = target;
        // A resize or a label change follows at once; a new selection glides.
        _highlight.MoveTo(target, Glide, animate);
    }

    private void OnTabClick(object? sender, RoutedEventArgs e)
    {
        if (sender is InstallSnippetTab tab)
        {
            Pick(tab, focus: false);
        }
    }

    /// <summary>Selects a tab as the reader's pick and moves focus with it when asked.</summary>
    internal void Pick(InstallSnippetTab tab, bool focus)
    {
        if (focus)
        {
            tab.Focus(NavigationMethod.Directional);
        }
        if (tab.Id == _selected)
        {
            return;
        }
        Select(tab.Id, animate: true);
        Picked?.Invoke(this, tab.Id);
    }

    private void OnTabKeyDown(object? sender, KeyEventArgs e)
    {
        if (sender is not InstallSnippetTab tab || _tabs.Count == 0 || e.KeyModifiers != KeyModifiers.None)
        {
            return;
        }
        var index = _tabs.IndexOf(tab);
        var flip = FlowDirection == FlowDirection.RightToLeft;
        var next = e.Key switch
        {
            Key.Right => flip ? index - 1 : index + 1,
            Key.Left => flip ? index + 1 : index - 1,
            Key.Home => 0,
            Key.End => _tabs.Count - 1,
            _ => int.MinValue,
        };
        if (next == int.MinValue)
        {
            return;
        }
        e.Handled = true;
        Pick(_tabs[(next % _tabs.Count + _tabs.Count) % _tabs.Count], focus: true);
    }

    private sealed class ListPeer(InstallSnippetTabs owner) : ControlAutomationPeer(owner)
    {
        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Tab;
    }
}

/// <summary>
/// One tab: a glyph or a monogram and a name, the text muted until it is selected or under the pointer, a keyboard focus
/// ring. Screen readers get a tab item that reports whether it is selected and keeps its name when the name is hidden.
/// </summary>
[TemplatePart(MarkPart, typeof(ArcGlyph))]
[TemplatePart(MonogramPart, typeof(Border))]
[TemplatePart(LabelPart, typeof(TextBlock))]
[PseudoClasses(SelectedPseudoClass, SmallPseudoClass)]
internal sealed class InstallSnippetTab : AvaloniaButton
{
    internal const string MarkPart = "PART_Mark";
    internal const string MonogramPart = "PART_Monogram";
    internal const string MonogramTextPart = "PART_MonogramText";
    internal const string LabelPart = "PART_Label";
    internal const string SelectedPseudoClass = ":selected";
    internal const string SmallPseudoClass = ":small";

    private ArcGlyph? _mark;
    private Border? _monogram;
    private TextBlock? _monogramText;
    private TextBlock? _label;
    private InstallSnippetTabItem? _item;
    private bool _showsMarks = true;
    private bool _showsLabels = true;
    private bool _isSelected;
    private bool _isSmall;

    public InstallSnippetTab(string id)
    {
        Id = id;
        IsTabStop = false;
    }

    protected override Type StyleKeyOverride => typeof(InstallSnippetTab);

    /// <summary>The tab's id.</summary>
    public string Id { get; }

    /// <summary>Whether the tab is selected; only the selected tab is a tab stop.</summary>
    public bool IsSelected
    {
        get => _isSelected;
        set
        {
            _isSelected = value;
            PseudoClasses.Set(SelectedPseudoClass, value);
            IsTabStop = value;
        }
    }

    /// <summary>The smaller package manager size.</summary>
    public bool IsSmall
    {
        get => _isSmall;
        set
        {
            _isSmall = value;
            PseudoClasses.Set(SmallPseudoClass, value);
        }
    }

    /// <summary>Whether the mark shows (tests).</summary>
    internal bool MarkShown => (_mark?.IsVisible ?? false) || (_monogram?.IsVisible ?? false);

    /// <summary>Whether the name shows (tests).</summary>
    internal bool LabelShown => _label?.IsVisible ?? false;

    /// <summary>Shows an item.</summary>
    public void Show(InstallSnippetTabItem item)
    {
        _item = item;
        AutomationProperties.SetName(this, item.Label);
        Apply();
    }

    /// <summary>Which parts show.</summary>
    public void SetShown(bool marks, bool labels)
    {
        _showsMarks = marks;
        _showsLabels = labels;
        Apply();
    }

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        _mark = e.NameScope.Find<ArcGlyph>(MarkPart);
        _monogram = e.NameScope.Find<Border>(MonogramPart);
        _monogramText = e.NameScope.Find<TextBlock>(MonogramTextPart);
        _label = e.NameScope.Find<TextBlock>(LabelPart);
        Apply();
    }

    private void Apply()
    {
        if (_item is null)
        {
            return;
        }
        var hasGlyph = _item.Mark is not null;
        var hasMonogram = !hasGlyph && !string.IsNullOrEmpty(_item.Monogram);
        // A tab never ends up empty: with no mark its name stays.
        var labels = _showsLabels || (!hasGlyph && !hasMonogram) || !_showsMarks;
        if (_mark is not null)
        {
            _mark.Data = _item.Mark;
            _mark.IsVisible = hasGlyph && _showsMarks;
        }
        if (_monogram is not null)
        {
            _monogram.IsVisible = hasMonogram && _showsMarks;
        }
        if (_monogramText is not null)
        {
            _monogramText.Text = _item.Monogram;
        }
        if (_label is not null)
        {
            _label.Text = _item.Label;
            _label.IsVisible = labels;
        }
        Tooltip.SetTip(this, labels ? null : _item.Label);
    }

    protected override AutomationPeer OnCreateAutomationPeer() => new TabPeer(this);

    private sealed class TabPeer(InstallSnippetTab owner) : ButtonAutomationPeer(owner), ISelectionItemProvider
    {
        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.TabItem;

        public bool IsSelected => owner.IsSelected;

        public ISelectionProvider? SelectionContainer => null;

        public void Select()
        {
            if (owner.FindAncestorOfType<InstallSnippetTabs>() is { } tabs)
            {
                tabs.Pick(owner, focus: false);
            }
        }

        public void AddToSelection() => Select();

        public void RemoveFromSelection()
        {
        }
    }
}
