using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using TKCode.Arc.Localization;

namespace TKCode.Arc;

/// <summary>
/// The pane switcher (spec 10, 11): a card in the middle of the layout listing every pane, most recently used first. Ctrl+Tab
/// and Ctrl+Shift+Tab move the highlight and releasing Ctrl activates it; Up / Down, Enter, a click and Escape work too.
/// The <see cref="DockLayout"/> drives it; it only shows the rows and reports the choice.
/// </summary>
[TemplatePart(ItemsPart, typeof(StackPanel))]
[TemplatePart(CardPart, typeof(Border))]
[TemplatePart(CaptionPart, typeof(TextBlock))]
internal sealed class DockPaneSwitcher : TemplatedControl
{
    public const string ItemsPart = "PART_Items";
    public const string CardPart = "PART_Card";
    public const string CaptionPart = "PART_Caption";

    private readonly List<DockPaneSwitcherItem> _items = [];
    private StackPanel? _panel;
    private Border? _card;
    private TextBlock? _caption;
    private int _index = -1;
    private IInputElement? _returnFocus;

    static DockPaneSwitcher()
    {
        FocusableProperty.OverrideDefaultValue<DockPaneSwitcher>(true);
    }

    public DockPaneSwitcher()
    {
        IsVisible = false;
    }

    public DockLayout? Layout { get; set; }

    /// <summary>True while the switcher shows.</summary>
    public bool IsOpen => IsVisible;

    /// <summary>The rows' panes in order (tests).</summary>
    internal IReadOnlyList<DockPane> Panes => _items.Select(i => i.Pane).ToList();

    /// <summary>The highlighted pane, or null.</summary>
    public DockPane? Selected => _index >= 0 && _index < _items.Count ? _items[_index].Pane : null;

    /// <summary>The rows (tests).</summary>
    internal IReadOnlyList<DockPaneSwitcherItem> Items => _items;

    /// <summary>The card (tests).</summary>
    internal Border? Card => _card;

    /// <summary>Shows <paramref name="panes"/> with row <paramref name="index"/> highlighted and takes keyboard focus.</summary>
    public void Open(IReadOnlyList<DockPane> panes, int index)
    {
        ClearItems();
        foreach (var pane in panes)
        {
            var item = new DockPaneSwitcherItem(this, pane);
            _items.Add(item);
            _panel?.Children.Add(item);
        }
        _index = -1;
        Select(panes.Count == 0 ? -1 : ((index % panes.Count) + panes.Count) % panes.Count);
        if (!IsVisible)
        {
            _returnFocus = TopLevel.GetTopLevel(this)?.FocusManager?.GetFocusedElement();
        }
        IsVisible = true;
        Focus(NavigationMethod.Tab);
    }

    /// <summary>Moves the highlight by <paramref name="delta"/> rows, wrapping around.</summary>
    public void Move(int delta)
    {
        if (_items.Count == 0)
        {
            return;
        }
        Select(((_index + delta) % _items.Count + _items.Count) % _items.Count);
    }

    /// <summary>Hides the switcher; with <paramref name="restoreFocus"/> focus goes back where it was.</summary>
    public void Close(bool restoreFocus)
    {
        if (!IsVisible)
        {
            return;
        }
        var back = _returnFocus;
        _returnFocus = null;
        IsVisible = false;
        ClearItems();
        if (restoreFocus && back is InputElement { IsEffectivelyVisible: true } element)
        {
            element.Focus(NavigationMethod.Tab);
        }
    }

    private void ClearItems()
    {
        _panel?.Children.Clear();
        _items.Clear();
        _index = -1;
    }

    private void Select(int index)
    {
        if (_index >= 0 && _index < _items.Count)
        {
            _items[_index].IsSelected = false;
        }
        _index = index;
        if (_index >= 0 && _index < _items.Count)
        {
            var item = _items[_index];
            item.IsSelected = true;
            item.BringIntoView();
            AutomationProperties.SetName(this, item.Pane.ToString());
        }
    }

    internal void OnItemClicked(DockPaneSwitcherItem item)
    {
        var index = _items.IndexOf(item);
        if (index >= 0)
        {
            Select(index);
            Layout?.CommitPaneSwitcher();
        }
    }

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        _panel?.Children.Clear();
        _panel = e.NameScope.Find<StackPanel>(ItemsPart);
        _card = e.NameScope.Find<Border>(CardPart);
        _caption = e.NameScope.Find<TextBlock>(CaptionPart);
        if (_caption is not null)
        {
            _caption.Text = DockText.Get(DockLangResourceKind.PaneSwitcher);
        }
        foreach (var item in _items)
        {
            _panel?.Children.Add(item);
        }
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        // A press outside the card closes the switcher without switching.
        if (!e.Handled && _card is not null && !_card.Bounds.Contains(e.GetPosition(_card.Parent as Visual ?? this)))
        {
            e.Handled = true;
            Layout?.ClosePaneSwitcher(restoreFocus: true);
        }
    }

    protected override AutomationPeer OnCreateAutomationPeer() => new DockPaneSwitcherAutomationPeer(this);

    private sealed class DockPaneSwitcherAutomationPeer : ControlAutomationPeer
    {
        public DockPaneSwitcherAutomationPeer(DockPaneSwitcher owner) : base(owner)
        {
        }

        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.List;

        protected override string? GetHelpTextCore() => DockText.Get(DockLangResourceKind.PaneSwitcher);
    }
}

/// <summary>One row of the pane switcher: the pane's icon and title; the highlighted row takes the accent.</summary>
[PseudoClasses(SelectedPseudoClass)]
internal sealed class DockPaneSwitcherItem : TemplatedControl
{
    public const string SelectedPseudoClass = ":selected";

    public static readonly StyledProperty<string> TitleProperty =
        AvaloniaProperty.Register<DockPaneSwitcherItem, string>(nameof(Title), string.Empty);

    public static readonly StyledProperty<Geometry?> IconProperty =
        AvaloniaProperty.Register<DockPaneSwitcherItem, Geometry?>(nameof(Icon));

    public static readonly StyledProperty<bool> IsSelectedProperty =
        AvaloniaProperty.Register<DockPaneSwitcherItem, bool>(nameof(IsSelected));

    private readonly DockPaneSwitcher _owner;

    public DockPaneSwitcherItem(DockPaneSwitcher owner, DockPane pane)
    {
        _owner = owner;
        Pane = pane;
        Title = pane.ToString();
        Icon = pane.Icon;
        AutomationProperties.SetName(this, Title);
    }

    public DockPane Pane { get; }

    public string Title { get => GetValue(TitleProperty); set => SetValue(TitleProperty, value); }

    public Geometry? Icon { get => GetValue(IconProperty); set => SetValue(IconProperty, value); }

    public bool IsSelected { get => GetValue(IsSelectedProperty); set => SetValue(IsSelectedProperty, value); }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == IsSelectedProperty)
        {
            PseudoClasses.Set(SelectedPseudoClass, change.GetNewValue<bool>());
        }
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        if (e.InitialPressMouseButton == MouseButton.Left && new Rect(Bounds.Size).Contains(e.GetPosition(this)))
        {
            e.Handled = true;
            _owner.OnItemClicked(this);
        }
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        e.Handled = true;
    }

    protected override AutomationPeer OnCreateAutomationPeer() => new ItemPeer(this);

    private sealed class ItemPeer : ControlAutomationPeer
    {
        public ItemPeer(DockPaneSwitcherItem owner) : base(owner)
        {
        }

        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.ListItem;
    }
}
