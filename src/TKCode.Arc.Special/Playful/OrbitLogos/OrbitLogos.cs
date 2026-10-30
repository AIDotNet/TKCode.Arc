using System.Collections.Specialized;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Collections;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Presenters;
using Avalonia.Controls.Primitives;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Metadata;
using Avalonia.Threading;
using TKCode.Arc.DesignTokens;
using TKCode.Arc.Motion;
using TKCode.Arc.Primitives;

namespace TKCode.Arc;

/// <summary>
/// An integrations orbit (Arc Orbit logos, design reference): your product mark at the centre with brand logos on two
/// flat rings that turn slowly and hold still whenever the pointer or keyboard focus is inside. Category chips turn the
/// matching logos toward the detail side and dim the rest. Choosing a logo springs its ring so the logo glides to a
/// fixed spot, draws an accent connector from the centre and morphs a detail card out of the chip, with a
/// description, the status, a Connect action, a Docs link and previous and next.
/// </summary>
/// <remarks>
/// The logos are a labelled list of toggle buttons with a single tab stop. Opening, stepping and filtering are announced
/// politely. Under reduced motion the rings do not turn, the rings and the connector are placed at once and the card
/// appears without the morph or the slides.
/// </remarks>
[TemplatePart(LayoutPart, typeof(OrbitLogosLayout))]
[TemplatePart(StagePart, typeof(OrbitLogosStage))]
[TemplatePart(CardPart, typeof(OrbitLogosCard))]
[TemplatePart(CategoriesPart, typeof(StackPanel))]
public class OrbitLogos : TemplatedControl
{
    public const string LayoutPart = "PART_Layout";
    public const string StagePart = "PART_Stage";
    public const string DiscPart = "PART_Disc";
    public const string CenterPart = "PART_Center";
    public const string TagPart = "PART_Tag";
    public const string TagTextPart = "PART_TagText";
    public const string CategoriesPart = "PART_Categories";
    public const string CategoryScrollerPart = "PART_CategoryScroller";
    public const string PillPart = "PART_Pill";
    public const string DetailPart = "PART_Detail";
    public const string IdlePart = "PART_Idle";
    public const string IdleTitlePart = "PART_IdleTitle";
    public const string IdleSummaryPart = "PART_IdleSummary";
    public const string IdleHintPart = "PART_IdleHint";
    public const string IdleTotalPart = "PART_IdleTotal";
    public const string CardPart = "PART_Card";
    public const string CardSwapPart = "PART_CardSwap";
    public const string LivePart = "PART_Live";

    /// <summary>Content of the centre disc, such as your product mark. Defaults to a simple mark in the accent color.</summary>
    public static readonly StyledProperty<object?> CenterProperty =
        AvaloniaProperty.Register<OrbitLogos, object?>(nameof(Center));

    /// <summary>Id (or name) of the logo whose card is open. Null closes the card. Two-way.</summary>
    public static readonly StyledProperty<string?> SelectedIdProperty =
        AvaloniaProperty.Register<OrbitLogos, string?>(nameof(SelectedId), defaultBindingMode: BindingMode.TwoWay);

    /// <summary>Id (or name) of the logo open on first display, when <see cref="SelectedId"/> is not set.</summary>
    public static readonly StyledProperty<string?> DefaultSelectedProperty =
        AvaloniaProperty.Register<OrbitLogos, string?>(nameof(DefaultSelected));

    /// <summary>
    /// Multiplies the rotation speed. At 1 the inner ring turns once every 64 seconds and the outer ring, the other
    /// way, every 88 seconds. 0 holds the orbit still.
    /// </summary>
    public static readonly StyledProperty<double> SpeedProperty =
        AvaloniaProperty.Register<OrbitLogos, double>(nameof(Speed), 1);

    /// <summary>Holds the orbit still, for a page-level motion control.</summary>
    public static readonly StyledProperty<bool> IsPausedProperty =
        AvaloniaProperty.Register<OrbitLogos, bool>(nameof(IsPaused));

    /// <summary>Label of the first category chip, the one that shows every logo.</summary>
    public static readonly StyledProperty<string> AllLabelProperty =
        AvaloniaProperty.Register<OrbitLogos, string>(nameof(AllLabel), "All");

    /// <summary>Accessible name of the logo list, and the title of the idle panel.</summary>
    public static readonly StyledProperty<string> LabelProperty =
        AvaloniaProperty.Register<OrbitLogos, string>(nameof(Label), "Integrations");

    /// <summary>Color of the two ring lines. The theme uses the subtle border.</summary>
    public static readonly StyledProperty<IBrush?> RingBrushProperty =
        AvaloniaProperty.Register<OrbitLogos, IBrush?>(nameof(RingBrush));

    /// <summary>Color of the connector from the centre to the chosen logo. The theme uses the accent.</summary>
    public static readonly StyledProperty<IBrush?> ConnectorBrushProperty =
        AvaloniaProperty.Register<OrbitLogos, IBrush?>(nameof(ConnectorBrush));

    /// <summary>Raised when a logo is opened, when previous or next steps to another, and with no logo and index -1 when the card closes.</summary>
    public static readonly RoutedEvent<OrbitLogosSelectionChangedEventArgs> SelectionChangedEvent =
        RoutedEvent.Register<OrbitLogos, OrbitLogosSelectionChangedEventArgs>(nameof(SelectionChanged), RoutingStrategies.Bubble);

    /// <summary>Raised when the Connect button is pressed. A handler can set <see cref="OrbitLogosConnectEventArgs.Completion"/>.</summary>
    public static readonly RoutedEvent<OrbitLogosConnectEventArgs> ConnectRequestedEvent =
        RoutedEvent.Register<OrbitLogos, OrbitLogosConnectEventArgs>(nameof(ConnectRequested), RoutingStrategies.Bubble);

    private static readonly TimeSpan SimulatedConnect = TimeSpan.FromMilliseconds(900);

    private readonly AvaloniaList<OrbitLogo> _logos = [];
    private readonly List<OrbitLogosChip> _chips = [];
    private readonly List<OrbitLogosCategoryChip> _categoryChips = [];
    private readonly List<string> _categories = [];
    private readonly HashSet<OrbitLogo> _connecting = [];
    private readonly HashSet<OrbitLogo> _failed = [];
    private readonly RectangleGeometry _clip = new();
    private OrbitLogosLayout? _layout;
    private OrbitLogosStage? _stage;
    private Control? _disc;
    private ContentPresenter? _center;
    private StackPanel? _categoriesPanel;
    private Control? _categoryScroller;
    private GlidingIndicator? _pill;
    private Panel? _detail;
    private Control? _idle;
    private TextBlock? _idleTitle;
    private TextBlock? _idleSummary;
    private TextBlock? _idleHint;
    private TextBlock? _idleTotal;
    private OrbitLogosCard? _card;
    private ArcContentSwap? _swap;
    private TextBlock? _live;
    private MotionValue<double>? _morph;
    private MotionValue<double>? _reveal;
    private CardParts? _cardParts;
    private int _selected = -1;
    private int _lastSelected = -1;
    private int _category;
    private int _focusIndex;
    private int _travel;
    private bool _wide;
    private bool _layoutKnown;
    private bool _syncingId;
    private bool _rebuildQueued;
    private bool _announceFlip;

    static OrbitLogos()
    {
        AffectsMeasure<OrbitLogos>(LabelProperty);
        FocusableProperty.OverrideDefaultValue<OrbitLogos>(false);
    }

    public OrbitLogos()
    {
        _logos.CollectionChanged += OnLogosChanged;
    }

    /// <summary>The integrations to show. Ten to sixteen read best across the two rings.</summary>
    [Content]
    public AvaloniaList<OrbitLogo> Logos => _logos;

    /// <inheritdoc cref="CenterProperty"/>
    public object? Center
    {
        get => GetValue(CenterProperty);
        set => SetValue(CenterProperty, value);
    }

    /// <inheritdoc cref="SelectedIdProperty"/>
    public string? SelectedId
    {
        get => GetValue(SelectedIdProperty);
        set => SetValue(SelectedIdProperty, value);
    }

    /// <inheritdoc cref="DefaultSelectedProperty"/>
    public string? DefaultSelected
    {
        get => GetValue(DefaultSelectedProperty);
        set => SetValue(DefaultSelectedProperty, value);
    }

    /// <inheritdoc cref="SpeedProperty"/>
    public double Speed
    {
        get => GetValue(SpeedProperty);
        set => SetValue(SpeedProperty, value);
    }

    /// <inheritdoc cref="IsPausedProperty"/>
    public bool IsPaused
    {
        get => GetValue(IsPausedProperty);
        set => SetValue(IsPausedProperty, value);
    }

    /// <inheritdoc cref="AllLabelProperty"/>
    public string AllLabel
    {
        get => GetValue(AllLabelProperty);
        set => SetValue(AllLabelProperty, value);
    }

    /// <inheritdoc cref="LabelProperty"/>
    public string Label
    {
        get => GetValue(LabelProperty);
        set => SetValue(LabelProperty, value);
    }

    /// <inheritdoc cref="RingBrushProperty"/>
    public IBrush? RingBrush
    {
        get => GetValue(RingBrushProperty);
        set => SetValue(RingBrushProperty, value);
    }

    /// <inheritdoc cref="ConnectorBrushProperty"/>
    public IBrush? ConnectorBrush
    {
        get => GetValue(ConnectorBrushProperty);
        set => SetValue(ConnectorBrushProperty, value);
    }

    /// <summary>
    /// Called by the Connect button. Return a task to hold the connecting state until it settles; a fault shows
    /// "Try again". Without a handler (and when <see cref="ConnectRequested"/> sets no completion) the card simulates a
    /// short connection.
    /// </summary>
    public Func<OrbitLogo, Task>? ConnectHandler { get; set; }

    /// <inheritdoc cref="SelectionChangedEvent"/>
    public event EventHandler<OrbitLogosSelectionChangedEventArgs>? SelectionChanged
    {
        add => AddHandler(SelectionChangedEvent, value);
        remove => RemoveHandler(SelectionChangedEvent, value);
    }

    /// <inheritdoc cref="ConnectRequestedEvent"/>
    public event EventHandler<OrbitLogosConnectEventArgs>? ConnectRequested
    {
        add => AddHandler(ConnectRequestedEvent, value);
        remove => RemoveHandler(ConnectRequestedEvent, value);
    }

    /// <summary>The index of the open logo, or -1.</summary>
    public int SelectedIndex => _selected;

    /// <summary>The open logo, or null.</summary>
    public OrbitLogo? SelectedLogo => _selected >= 0 && _selected < _logos.Count ? _logos[_selected] : null;

    /// <summary>
    /// Opens the logo at <paramref name="index"/>, or closes the card for -1, as a click would.
    /// </summary>
    public void Select(int index) => SelectLogo(index < 0 || index >= _logos.Count ? -1 : index, user: false, animate: true);

    internal OrbitLogosStage? Stage => _stage;

    internal IReadOnlyList<OrbitLogosChip> Chips => _chips;

    internal IReadOnlyList<OrbitLogosCategoryChip> CategoryChips => _categoryChips;

    internal OrbitLogosCard? Card => _card;

    internal int CategoryIndex => _category;

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        if (_categoriesPanel is not null)
        {
            _categoriesPanel.SizeChanged -= OnCategoriesResized;
        }
        _layout = e.NameScope.Find<OrbitLogosLayout>(LayoutPart);
        _stage = e.NameScope.Find<OrbitLogosStage>(StagePart);
        _disc = e.NameScope.Find<Control>(DiscPart);
        _center = e.NameScope.Find<ContentPresenter>(CenterPart);
        _categoriesPanel = e.NameScope.Find<StackPanel>(CategoriesPart);
        _categoryScroller = e.NameScope.Find<Control>(CategoryScrollerPart);
        _pill = e.NameScope.Find<GlidingIndicator>(PillPart);
        _detail = e.NameScope.Find<Panel>(DetailPart);
        _idle = e.NameScope.Find<Control>(IdlePart);
        _idleTitle = e.NameScope.Find<TextBlock>(IdleTitlePart);
        _idleSummary = e.NameScope.Find<TextBlock>(IdleSummaryPart);
        _idleHint = e.NameScope.Find<TextBlock>(IdleHintPart);
        _idleTotal = e.NameScope.Find<TextBlock>(IdleTotalPart);
        _card = e.NameScope.Find<OrbitLogosCard>(CardPart);
        _swap = e.NameScope.Find<ArcContentSwap>(CardSwapPart);
        _live = e.NameScope.Find<TextBlock>(LivePart);
        var tag = e.NameScope.Find<Border>(TagPart);
        var tagText = e.NameScope.Find<TextBlock>(TagTextPart);
        if (_stage is not null && _disc is not null && tag is not null && tagText is not null)
        {
            _stage.Attach(_disc, tag, tagText);
            _stage.SetBrushes(RingBrush, ConnectorBrush);
            AutomationProperties.SetName(_stage, Label);
        }
        if (_categoriesPanel is not null)
        {
            _categoriesPanel.SizeChanged += OnCategoriesResized;
        }
        if (_card is not null)
        {
            _card.IsVisible = false;
            _card.Clip = null;
            if (_idle is not null)
            {
                _idle.Opacity = 1;
            }
        }
        _layoutKnown = false;
        UpdateCenter();
        Rebuild(restore: false);
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        var size = base.MeasureOverride(availableSize);
        if (_layout is not null && _stage is not null && (!_layoutKnown || _layout.IsSideBySide != _wide))
        {
            var first = !_layoutKnown;
            _layoutKnown = true;
            _wide = _layout.IsSideBySide;
            _stage.IsSideBySide = _wide;
            _stage.Retarget(_selected, Matches(), snap: first);
        }
        return size;
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        var property = change.Property;
        if (property == CenterProperty)
        {
            UpdateCenter();
        }
        else if (property == SelectedIdProperty)
        {
            if (!_syncingId)
            {
                SelectLogo(IndexOfKey(SelectedId), user: false, animate: true);
            }
        }
        else if (property == SpeedProperty || property == IsPausedProperty)
        {
            ApplySpeed();
        }
        else if (property == RingBrushProperty || property == ConnectorBrushProperty)
        {
            _stage?.SetBrushes(RingBrush, ConnectorBrush);
        }
        else if (property == LabelProperty)
        {
            if (_stage is not null)
            {
                AutomationProperties.SetName(_stage, Label);
            }
            RefreshIdle();
        }
        else if (property == AllLabelProperty)
        {
            if (_categoryChips.Count > 0)
            {
                _categoryChips[0].Content = AllLabel;
            }
        }
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (e.Handled)
        {
            return;
        }
        if (e.Key == Key.Escape && _selected >= 0)
        {
            var index = _selected;
            SelectLogo(-1, user: true, animate: true);
            FocusChip(index);
            e.Handled = true;
            return;
        }
        if (e.Source is not OrbitLogosChip chip)
        {
            return;
        }
        var list = CategoryIndices();
        if (list.Count == 0)
        {
            return;
        }
        var position = list.IndexOf(chip.Index);
        int target;
        int delta;
        switch (e.Key)
        {
            case Key.Right or Key.Down:
                delta = 1;
                target = list[(Math.Max(position, 0) + 1) % list.Count];
                break;
            case Key.Left or Key.Up:
                delta = -1;
                target = list[(Math.Max(position, 0) - 1 + list.Count) % list.Count];
                break;
            case Key.Home:
                delta = -1;
                target = list[0];
                break;
            case Key.End:
                delta = 1;
                target = list[^1];
                break;
            default:
                return;
        }
        e.Handled = true;
        FocusChip(target);
        if (_selected >= 0)
        {
            _travel = delta;
            SelectLogo(target, user: true, animate: true);
        }
    }

    private void OnLogosChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.OldItems is not null)
        {
            foreach (OrbitLogo logo in e.OldItems)
            {
                logo.PropertyChanged -= OnLogoPropertyChanged;
            }
        }
        if (e.NewItems is not null)
        {
            foreach (OrbitLogo logo in e.NewItems)
            {
                logo.PropertyChanged += OnLogoPropertyChanged;
            }
        }
        QueueRebuild();
    }

    private void OnLogoPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (e.Property == OrbitLogo.IsConnectedProperty)
        {
            RefreshStatus();
        }
        else if (e.Property == OrbitLogo.DescriptionProperty || e.Property == OrbitLogo.HrefProperty)
        {
            RefreshCard();
        }
        else
        {
            QueueRebuild();
        }
    }

    private void QueueRebuild()
    {
        if (_stage is null || _rebuildQueued)
        {
            return;
        }
        _rebuildQueued = true;
        Dispatcher.UIThread.Post(() =>
        {
            _rebuildQueued = false;
            Rebuild(restore: true);
        });
    }

    /// <summary>Builds the chips and the category row from the logos and restores the open logo.</summary>
    private void Rebuild(bool restore)
    {
        if (_stage is null || _categoriesPanel is null)
        {
            return;
        }
        var key = restore && _selected >= 0 && _selected < _chips.Count ? _chips[_selected].Logo.Key : null;
        var category = restore && _category > 0 && _category <= _categories.Count ? _categories[_category - 1] : null;

        foreach (var chip in _chips)
        {
            chip.Click -= OnChipClick;
            chip.GotFocus -= OnChipGotFocus;
        }
        _chips.Clear();
        for (var i = 0; i < _logos.Count; i++)
        {
            var chip = new OrbitLogosChip(_logos[i], i);
            chip.Click += OnChipClick;
            chip.GotFocus += OnChipGotFocus;
            _chips.Add(chip);
        }
        _categories.Clear();
        foreach (var logo in _logos)
        {
            if (!string.IsNullOrWhiteSpace(logo.Category) && !_categories.Contains(logo.Category))
            {
                _categories.Add(logo.Category);
            }
        }
        BuildCategoryChips();

        _selected = -1;
        _lastSelected = -1;
        _category = category is null ? 0 : _categories.IndexOf(category) + 1;
        _stage.SetChips(_chips);
        _stage.IsSideBySide = _wide;
        ApplySpeed();
        ApplyCategoryState();
        _focusIndex = 0;

        CloseCardImmediately();
        var start = key ?? SelectedId ?? (restore ? null : DefaultSelected);
        var index = IndexOfKey(start);
        if (index >= 0)
        {
            SelectLogo(index, user: false, animate: false);
        }
        else
        {
            _stage.Retarget(-1, Matches(), snap: true);
            _stage.ExternalHold = _category != 0;
            RefreshChips();
            RefreshIdle();
            if (SelectedId is not null && !_syncingId)
            {
                SetSelectedId(null);
            }
        }
        UpdateTabStops();
        InvalidateMeasure();
    }

    private void BuildCategoryChips()
    {
        if (_categoriesPanel is null)
        {
            return;
        }
        foreach (var chip in _categoryChips)
        {
            chip.Click -= OnCategoryClick;
        }
        _categoryChips.Clear();
        _categoriesPanel.Children.Clear();
        if (_categories.Count > 0)
        {
            AddCategoryChip(AllLabel);
            foreach (var category in _categories)
            {
                AddCategoryChip(category);
            }
        }
        if (_categoryScroller is not null)
        {
            _categoryScroller.IsVisible = _categories.Count > 0;
        }
    }

    private void AddCategoryChip(string text)
    {
        var chip = new OrbitLogosCategoryChip { Content = text };
        AutomationProperties.SetName(chip, text);
        chip.Click += OnCategoryClick;
        _categoryChips.Add(chip);
        _categoriesPanel!.Children.Add(chip);
    }

    private void OnCategoryClick(object? sender, RoutedEventArgs e)
    {
        if (sender is OrbitLogosCategoryChip chip)
        {
            SetCategory(_categoryChips.IndexOf(chip), user: true);
        }
    }

    private void OnChipClick(object? sender, RoutedEventArgs e)
    {
        if (sender is not OrbitLogosChip chip)
        {
            return;
        }
        _travel = 0;
        SelectLogo(chip.Index == _selected ? -1 : chip.Index, user: true, animate: true);
    }

    private void OnChipGotFocus(object? sender, FocusChangedEventArgs e)
    {
        if (sender is OrbitLogosChip chip && _focusIndex != chip.Index)
        {
            _focusIndex = chip.Index;
            UpdateTabStops();
        }
    }

    /// <summary>Shows only the logos of category <paramref name="index"/> (0 shows all).</summary>
    internal void SetCategory(int index, bool user)
    {
        if (index < 0 || index > _categories.Count || index == _category)
        {
            return;
        }
        _category = index;
        ApplyCategoryState();
        if (_selected >= 0 && !InCategory(_selected))
        {
            SelectLogo(-1, user, animate: true);
        }
        else
        {
            _stage?.Retarget(_selected, Matches());
            _stage!.ExternalHold = _selected >= 0 || _category != 0;
        }
        RefreshChips();
        RefreshIdle();
        UpdateTabStops();
        var count = CategoryIndices().Count;
        Announce(_category == 0
            ? $"Showing all {count} {Plural(count)}"
            : $"Showing {count} {_categories[_category - 1]} {Plural(count)}");
    }

    private void ApplyCategoryState()
    {
        for (var i = 0; i < _categoryChips.Count; i++)
        {
            _categoryChips[i].IsChecked = i == _category;
        }
        MovePill(animate: true);
    }

    private void MovePill(bool animate)
    {
        if (_pill is null || _category < 0 || _category >= _categoryChips.Count)
        {
            _pill?.Hide(animate: false);
            return;
        }
        var chip = _categoryChips[_category];
        if (chip.Bounds.Width <= 0)
        {
            return;
        }
        _pill.MoveTo(chip.Bounds, animate && ArcMotion.IsFull(this), ArcSpring.Morph);
    }

    private void OnCategoriesResized(object? sender, SizeChangedEventArgs e) => MovePill(animate: false);

    /// <summary>Opens the logo at <paramref name="index"/>, or closes the card for -1.</summary>
    private void SelectLogo(int index, bool user, bool animate)
    {
        if (_stage is null || index == _selected)
        {
            return;
        }
        if (index >= 0 && !InCategory(index))
        {
            // A logo outside the chosen category: show everything again so it can take its place.
            _category = 0;
            ApplyCategoryState();
        }
        var previous = _selected;
        _selected = index;
        if (index >= 0)
        {
            _lastSelected = index;
            _focusIndex = index;
        }
        SetSelectedId(index >= 0 ? _logos[index].Key : null);

        _stage.ExternalHold = _selected >= 0 || _category != 0;
        _stage.Retarget(_selected, Matches(), snap: !animate);
        RefreshChips();
        UpdateTabStops();
        UpdateCard(previous, animate);
        RefreshIdle();

        if (index >= 0)
        {
            var logo = _logos[index];
            Announce($"{logo.Name} opened, {StatusText(logo)}. {logo.Description}".TrimEnd(' ', '.') + ".");
        }
        else if (previous >= 0)
        {
            Announce($"{_logos[previous].Name} closed");
        }
        if (user)
        {
            RaiseEvent(new OrbitLogosSelectionChangedEventArgs(SelectionChangedEvent, this,
                index >= 0 ? _logos[index] : null, index));
        }
    }

    private void SetSelectedId(string? key)
    {
        if (SelectedId == key)
        {
            return;
        }
        _syncingId = true;
        try
        {
            SetCurrentValue(SelectedIdProperty, key);
        }
        finally
        {
            _syncingId = false;
        }
    }

    private int IndexOfKey(string? key)
    {
        if (string.IsNullOrEmpty(key))
        {
            return -1;
        }
        for (var i = 0; i < _logos.Count; i++)
        {
            if (_logos[i].Id == key)
            {
                return i;
            }
        }
        for (var i = 0; i < _logos.Count; i++)
        {
            if (_logos[i].Name == key)
            {
                return i;
            }
        }
        return -1;
    }

    private bool InCategory(int index) =>
        _category == 0 || (_category <= _categories.Count && _logos[index].Category == _categories[_category - 1]);

    private List<int> CategoryIndices()
    {
        var list = new List<int>(_logos.Count);
        for (var i = 0; i < _logos.Count; i++)
        {
            if (InCategory(i))
            {
                list.Add(i);
            }
        }
        return list;
    }

    private bool[]? Matches()
    {
        if (_category == 0)
        {
            return null;
        }
        var matches = new bool[_logos.Count];
        for (var i = 0; i < matches.Length; i++)
        {
            matches[i] = InCategory(i);
        }
        return matches;
    }

    private void RefreshChips()
    {
        for (var i = 0; i < _chips.Count; i++)
        {
            var chip = _chips[i];
            var logo = chip.Logo;
            chip.IsChecked = i == _selected;
            chip.Classes.Set("dim", _category != 0 && !InCategory(i));
            chip.Classes.Set("connected", logo.IsConnected);
            var category = string.IsNullOrWhiteSpace(logo.Category) ? string.Empty : $", {logo.Category}";
            AutomationProperties.SetName(chip, $"{logo.Name}{category}, {(logo.IsConnected ? "connected" : "not connected")}");
        }
    }

    private void UpdateTabStops()
    {
        var list = CategoryIndices();
        var target = _selected >= 0 ? _selected : list.Contains(_focusIndex) ? _focusIndex : list.Count > 0 ? list[0] : 0;
        for (var i = 0; i < _chips.Count; i++)
        {
            _chips[i].IsTabStop = i == target;
        }
    }

    private void FocusChip(int index)
    {
        if (index >= 0 && index < _chips.Count)
        {
            _focusIndex = index;
            UpdateTabStops();
            _chips[index].Focus(NavigationMethod.Directional);
        }
    }

    private void ApplySpeed()
    {
        if (_stage is null)
        {
            return;
        }
        _stage.Simulation.Speed = Speed;
        _stage.Simulation.Paused = IsPaused;
        _stage.Wake();
    }

    private void UpdateCenter()
    {
        if (_center is null)
        {
            return;
        }
        if (Center is { } content)
        {
            _center.Content = content;
            return;
        }
        var mark = new OrbitLogosMark
        {
            Data = ArcIcons.Parse("F1 M12 2l2.6 7.4L22 12l-7.4 2.6L12 22l-2.6-7.4L2 12l7.4-2.6z"),
            Width = 30,
            Height = 30
        };
        ArcTokens.Bind(mark, OrbitLogosMark.FillProperty, ArcDesignTokenKind.Accent);
        _center.Content = mark;
    }

    private void Announce(string message)
    {
        if (_live is null)
        {
            return;
        }
        // The same message twice in a row is still a change.
        _announceFlip = !_announceFlip;
        _live.Text = _announceFlip ? message : message + "​";
        AutomationProperties.SetName(_live, _live.Text);
    }

    private static string Plural(int count) => count == 1 ? "integration" : "integrations";

    private string StatusText(OrbitLogo logo) =>
        logo.IsConnected ? "connected" : _connecting.Contains(logo) ? "connecting" : "not connected";

    private void RefreshStatus()
    {
        RefreshChips();
        RefreshIdle();
        RefreshCard();
    }

    private void RefreshIdle()
    {
        if (_idleTitle is null)
        {
            return;
        }
        var scope = CategoryIndices();
        var connected = scope.Count(i => _logos[i].IsConnected);
        var total = _logos.Count(logo => logo.IsConnected);
        _idleTitle.Text = Label;
        var kind = _category == 0 ? string.Empty : _categories[_category - 1] + " ";
        _idleSummary!.Text = $"{scope.Count} {kind}{Plural(scope.Count)}, {connected} connected";
        _idleHint!.Text = "Pick a logo to see what it does and connect it. Arrow keys step through them.";
        _idleTotal!.Text = $"{total} of {_logos.Count} connected in total";
    }

    #region Card

    private void UpdateCard(int previous, bool animate)
    {
        if (_card is null || _swap is null)
        {
            return;
        }
        if (_selected >= 0)
        {
            var logo = _logos[_selected];
            _card.IsVisible = true;
            AutomationProperties.SetName(_card, logo.Name);
            var body = BuildCardBody(logo, _selected);
            if (previous < 0 || !animate)
            {
                _swap.Mode = ContentSwapMode.None;
                _swap.Content = body;
                OpenMorph(animate);
            }
            else
            {
                _swap.Mode = ContentSwapMode.Slide;
                _swap.Direction = _travel != 0 ? _travel : _selected > previous ? 1 : -1;
                _swap.Content = body;
            }
        }
        else if (previous >= 0)
        {
            CloseMorph(animate);
        }
    }

    private void EnsureMotion()
    {
        if (_morph is null)
        {
            _morph = new MotionValue<double>(this, 0, MotionVectors.Double);
            _morph.Changed += ApplyMorph;
        }
        if (_reveal is null)
        {
            _reveal = new MotionValue<double>(this, 0, MotionVectors.Double);
            _reveal.Changed += value =>
            {
                if (_swap is not null)
                {
                    _swap.Opacity = Math.Clamp(value, 0, 1);
                }
            };
        }
    }

    private void OpenMorph(bool animate)
    {
        EnsureMotion();
        if (!animate || !ArcMotion.IsFull(this) || _card is null || _detail is null)
        {
            _morph!.Jump(1);
            _reveal!.Jump(1);
            ApplyMorph(1);
            return;
        }
        // The card needs its box before it can grow out of the chip.
        _detail.UpdateLayout();
        _reveal!.Jump(0);
        _morph!.Jump(0);
        _ = _morph.AnimateTo(1, MotionSpec.Spring(ArcSpring.Morph));
        _ = _reveal.AnimateTo(1, MotionSpec.Tween(ArcMotionPresets.Standard, ArcEasings.Enter, TimeSpan.FromMilliseconds(120)));
    }

    private void CloseMorph(bool animate)
    {
        EnsureMotion();
        if (!animate || !ArcMotion.IsFull(this))
        {
            CloseCardImmediately();
            return;
        }
        _ = _reveal!.AnimateTo(0, MotionSpec.Tween(ArcMotionPresets.Fast, ArcEasings.Exit));
        _ = CloseAsync();
    }

    private async Task CloseAsync()
    {
        var finished = await _morph!.AnimateTo(0, MotionSpec.Spring(ArcSpring.Snappy));
        if (finished && _selected < 0)
        {
            CloseCardImmediately();
        }
    }

    private void CloseCardImmediately()
    {
        _morph?.Jump(0);
        _reveal?.Jump(0);
        if (_card is not null)
        {
            _card.IsVisible = false;
            _card.Clip = null;
        }
        if (_swap is not null)
        {
            _swap.Content = null;
            _swap.Opacity = 1;
        }
        _cardParts = null;
        if (_idle is not null)
        {
            _idle.Opacity = 1;
            _idle.IsHitTestVisible = true;
        }
    }

    /// <summary>
    /// Draws the card as a clip that grows from the chip's circle to the card's own box (the surface is always laid out
    /// at its final place, so nothing re-measures), and fades the idle panel out under it.
    /// </summary>
    private void ApplyMorph(double progress)
    {
        if (_card is null || _detail is null || _stage is null)
        {
            return;
        }
        var open = _selected >= 0;
        if (progress >= 0.9999 && open)
        {
            _card.Clip = null;
            _card.InvalidateVisual();
            if (_idle is not null)
            {
                _idle.Opacity = 0;
                _idle.IsHitTestVisible = false;
            }
            return;
        }
        if (_idle is not null)
        {
            _idle.Opacity = Math.Clamp(1 - progress * 1.8, 0, 1);
            _idle.IsHitTestVisible = progress < 0.05;
        }
        var cardRect = _card.Bounds;
        var index = open ? _selected : _lastSelected;
        if (cardRect.Width <= 0 || _stage.ChipRect(index, _detail) is not { } chipRect)
        {
            return;
        }
        var q = Math.Clamp(progress, 0, 1.12);
        var rect = new Rect(
            Lerp(chipRect.X, cardRect.X, q),
            Lerp(chipRect.Y, cardRect.Y, q),
            Math.Max(0, Lerp(chipRect.Width, cardRect.Width, q)),
            Math.Max(0, Lerp(chipRect.Height, cardRect.Height, q)));
        var radius = Lerp(chipRect.Width / 2, Math.Min(_card.CornerRadius.TopLeft, Math.Min(cardRect.Width, cardRect.Height) / 2), Math.Min(q, 1));
        _clip.Rect = rect.Translate(new Vector(-cardRect.X, -cardRect.Y));
        _clip.RadiusX = radius;
        _clip.RadiusY = radius;
        _card.Clip = _clip;
        _card.InvalidateVisual();
    }

    private static double Lerp(double a, double b, double t) => a + (b - a) * t;

    private Control BuildCardBody(OrbitLogo logo, int index)
    {
        var parts = new CardParts { Logo = logo };
        var root = new StackPanel { Spacing = 12 };

        var header = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*"), ColumnSpacing = 12 };
        var badge = new Border { Width = 40, Height = 40, ClipToBounds = true }
            .BindCornerRadius(ArcDesignTokenKind.RadiusPill);
        ArcTokens.Bind(badge, Border.BackgroundProperty, ArcDesignTokenKind.SurfaceMuted);
        var mark = OrbitLogosChip.CreateMark(logo, forCard: true);
        if (mark is not null)
        {
            mark.Margin = new Thickness(9);
            badge.Child = mark;
        }
        else
        {
            var initial = new TextBlock
            {
                Text = logo.Name.Length > 0 ? logo.Name[..1].ToUpperInvariant() : "?",
                HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center,
                VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center,
                FontWeight = FontWeight.SemiBold
            };
            badge.Child = initial;
        }
        header.Children.Add(badge);
        var titles = new StackPanel { Spacing = 2, VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center };
        Grid.SetColumn(titles, 1);
        var name = new TextBlock { Text = logo.Name, FontWeight = FontWeight.SemiBold, TextTrimming = TextTrimming.CharacterEllipsis };
        ArcTokens.Bind(name, TextBlock.FontSizeProperty, ArcDesignTokenKind.TypeLG);
        titles.Children.Add(name);
        if (!string.IsNullOrWhiteSpace(logo.Category))
        {
            var category = new TextBlock { Text = logo.Category };
            ArcTokens.Bind(category, TextBlock.FontSizeProperty, ArcDesignTokenKind.TypeXS);
            ArcTokens.Bind(category, TextBlock.ForegroundProperty, ArcDesignTokenKind.TextMuted);
            titles.Children.Add(category);
        }
        header.Children.Add(titles);
        root.Children.Add(header);

        if (!string.IsNullOrWhiteSpace(logo.Description))
        {
            var description = new TextBlock { Text = logo.Description, TextWrapping = TextWrapping.Wrap };
            ArcTokens.Bind(description, TextBlock.FontSizeProperty, ArcDesignTokenKind.TypeSM);
            ArcTokens.Bind(description, TextBlock.ForegroundProperty, ArcDesignTokenKind.TextSecondary);
            root.Children.Add(description);
        }

        var status = new StackPanel { Orientation = Avalonia.Layout.Orientation.Horizontal, Spacing = 6 };
        parts.StatusGlyph = new ArcGlyph { Size = 16, VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center };
        parts.StatusText = new TextBlock { VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center };
        ArcTokens.Bind(parts.StatusText, TextBlock.FontSizeProperty, ArcDesignTokenKind.TypeSM);
        status.Children.Add(parts.StatusGlyph);
        status.Children.Add(parts.StatusText);
        root.Children.Add(status);

        var actions = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,Auto,*,Auto,Auto"), ColumnSpacing = 4 };
        parts.Connect = new Button { Variant = ButtonVariant.Primary, Size = ControlSize.Small };
        parts.Connect.Click += (_, _) => _ = ConnectAsync(logo, index);
        actions.Children.Add(parts.Connect);
        if (!string.IsNullOrWhiteSpace(logo.Href))
        {
            var docs = new Button
            {
                Variant = ButtonVariant.Ghost,
                Size = ControlSize.Small,
                Content = CreateLabelWithGlyph(ArcIcons.ExternalLink, "Docs")
            };
            docs.Click += (_, _) => OpenDocs(logo);
            AutomationProperties.SetName(docs, $"{logo.Name} docs");
            Grid.SetColumn(docs, 1);
            actions.Children.Add(docs);
        }
        var previous = new Button { Variant = ButtonVariant.Ghost, Size = ControlSize.Small, Content = new ArcGlyph { Data = ArcIcons.ChevronLeft, Size = 16 } };
        AutomationProperties.SetName(previous, "Previous integration");
        previous.Click += (_, _) => StepSelection(-1);
        Grid.SetColumn(previous, 3);
        var next = new Button { Variant = ButtonVariant.Ghost, Size = ControlSize.Small, Content = new ArcGlyph { Data = ArcIcons.ChevronRight, Size = 16 } };
        AutomationProperties.SetName(next, "Next integration");
        next.Click += (_, _) => StepSelection(1);
        Grid.SetColumn(next, 4);
        actions.Children.Add(previous);
        actions.Children.Add(next);
        root.Children.Add(actions);

        _cardParts = parts;
        UpdateCardParts(parts);
        return root;
    }

    private static Control CreateLabelWithGlyph(Avalonia.Media.Geometry glyph, string text)
    {
        var row = new StackPanel { Orientation = Avalonia.Layout.Orientation.Horizontal, Spacing = 6 };
        row.Children.Add(new ArcGlyph { Data = glyph, Size = 14, VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center });
        row.Children.Add(new TextBlock { Text = text, VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center });
        return row;
    }

    private void RefreshCard()
    {
        if (_cardParts is { } parts)
        {
            UpdateCardParts(parts);
        }
    }

    /// <summary>Writes the status text and glyph and the Connect button for the logo's state: connected, connecting, failed or idle.</summary>
    private void UpdateCardParts(CardParts parts)
    {
        var logo = parts.Logo;
        var connecting = _connecting.Contains(logo);
        var failed = _failed.Contains(logo);
        string text;
        ArcDesignTokenKind tone;
        Avalonia.Media.Geometry glyph;
        if (logo.IsConnected)
        {
            text = "Connected";
            tone = ArcDesignTokenKind.Success;
            glyph = ArcIcons.CircleCheck;
        }
        else if (connecting)
        {
            text = "Connecting…";
            tone = ArcDesignTokenKind.TextSecondary;
            glyph = ArcIcons.LoaderCircle;
        }
        else if (failed)
        {
            text = "Could not connect";
            tone = ArcDesignTokenKind.Danger;
            glyph = ArcIcons.Link;
        }
        else
        {
            text = "Not connected";
            tone = ArcDesignTokenKind.TextMuted;
            glyph = ArcIcons.Link;
        }
        parts.StatusText!.Text = text;
        parts.StatusGlyph!.Data = glyph;
        ArcTokens.Bind(parts.StatusText, TextBlock.ForegroundProperty, tone);
        ArcTokens.Bind(parts.StatusGlyph, ArcGlyph.ForegroundProperty, tone);

        var connect = parts.Connect!;
        connect.IsVisible = !logo.IsConnected;
        connect.IsLoading = connecting;
        connect.Content = connecting ? "Connecting…" : failed ? "Try again" : "Connect";
        AutomationProperties.SetName(connect, $"{connect.Content} {logo.Name}");
    }

    private void StepSelection(int delta)
    {
        var list = CategoryIndices();
        if (list.Count == 0)
        {
            return;
        }
        var position = list.IndexOf(_selected);
        var target = list[((position < 0 ? 0 : position) + delta + list.Count) % list.Count];
        _travel = delta;
        SelectLogo(target, user: true, animate: true);
    }

    private async Task ConnectAsync(OrbitLogo logo, int index)
    {
        if (_connecting.Contains(logo) || logo.IsConnected)
        {
            return;
        }
        _failed.Remove(logo);
        _connecting.Add(logo);
        RefreshCard();
        Announce($"Connecting {logo.Name}");
        var ok = false;
        try
        {
            var args = new OrbitLogosConnectEventArgs(ConnectRequestedEvent, this, logo, index);
            RaiseEvent(args);
            var task = args.Completion ?? ConnectHandler?.Invoke(logo);
            if (task is null)
            {
                await Task.Delay(SimulatedConnect);
            }
            else
            {
                await task;
            }
            ok = true;
        }
        catch (Exception)
        {
            // A rejected connection is a state the card shows, not an error to throw at the page.
        }
        _connecting.Remove(logo);
        if (ok)
        {
            logo.IsConnected = true;
            Announce($"{logo.Name} connected");
        }
        else
        {
            _failed.Add(logo);
            Announce($"Could not connect {logo.Name}. Try again.");
        }
        RefreshStatus();
    }

    private void OpenDocs(OrbitLogo logo)
    {
        if (Uri.TryCreate(logo.Href, UriKind.Absolute, out var uri) && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps))
        {
            _ = TopLevel.GetTopLevel(this)?.Launcher.LaunchUriAsync(uri);
        }
    }

    private sealed class CardParts
    {
        public required OrbitLogo Logo { get; init; }
        public TextBlock? StatusText { get; set; }
        public ArcGlyph? StatusGlyph { get; set; }
        public Button? Connect { get; set; }
    }

    #endregion
}

/// <summary>Data of <see cref="OrbitLogos.SelectionChanged"/>.</summary>
public sealed class OrbitLogosSelectionChangedEventArgs : RoutedEventArgs
{
    internal OrbitLogosSelectionChangedEventArgs(RoutedEvent routedEvent, object source, OrbitLogo? logo, int index)
        : base(routedEvent, source)
    {
        Logo = logo;
        Index = index;
    }

    /// <summary>The logo that is now open, or null when the card closed.</summary>
    public OrbitLogo? Logo { get; }

    /// <summary>The index of the logo in <see cref="OrbitLogos.Logos"/>, or -1 when the card closed.</summary>
    public int Index { get; }
}

/// <summary>Data of <see cref="OrbitLogos.ConnectRequested"/>.</summary>
public sealed class OrbitLogosConnectEventArgs : RoutedEventArgs
{
    internal OrbitLogosConnectEventArgs(RoutedEvent routedEvent, object source, OrbitLogo logo, int index)
        : base(routedEvent, source)
    {
        Logo = logo;
        Index = index;
    }

    /// <summary>The logo whose Connect button was pressed.</summary>
    public OrbitLogo Logo { get; }

    /// <summary>The index of the logo in <see cref="OrbitLogos.Logos"/>.</summary>
    public int Index { get; }

    /// <summary>
    /// Set this to the task that connects. The card shows Connecting until it completes and Try again if it faults. Left
    /// null, the card simulates a short connection.
    /// </summary>
    public Task? Completion { get; set; }
}
