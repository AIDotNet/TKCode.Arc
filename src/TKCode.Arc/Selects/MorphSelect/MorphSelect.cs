using TKCode.Arc.DesignTokens;
using TKCode.Arc.Motion;
using TKCode.Arc.Primitives;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Primitives.PopupPositioning;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using AvaloniaButton = Avalonia.Controls.Button;

namespace TKCode.Arc;

/// <summary>When a <see cref="MorphSelect"/> shows a search field.</summary>
public enum MorphSelectSearchMode
{
    /// <summary>Above eight options (Arc's default).</summary>
    Auto,
    On,
    Off
}

/// <summary>Which edge of a <see cref="MorphSelect"/> trigger stays put while the surface grows.</summary>
public enum MorphSelectAlignment
{
    Start,
    End
}

/// <summary>
/// Arc morph select: a select whose trigger is the list. Opening grows the trigger itself into the options surface
/// (width, height and corners spring together, P9), a single highlight glides between options (P7), and the chosen
/// label lifts out of the list and settles into the trigger (P8) while the trigger springs to its new width (P5).
/// Supports groups (<see cref="SelectItem.Group"/>), icons and trailing meta, type-ahead, search for long lists and
/// the full listbox keyboard model. Use it for property pickers and form fields with up to a few hundred options.
/// <para>Keyboard: Enter, Space and the arrows open on the selected option (Home / End open on the first or last);
/// arrows, PageUp / PageDown (8 at a time) and Home / End move; Enter or Space picks; Alt+Up picks and closes;
/// typing jumps (or searches when search is on); Escape closes and returns focus; Tab closes without picking.</para>
/// </summary>
[TemplatePart(AnchorPart, typeof(MorphSelectAnchor))]
[TemplatePart(TriggerPart, typeof(Border))]
[TemplatePart(ValuePart, typeof(SelectValueRoll))]
[TemplatePart(PopupPart, typeof(ArcPopup))]
[TemplatePart(AnnouncerPart, typeof(TextBlock))]
[PseudoClasses(OpenPseudoClass, PlaceholderPseudoClass)]
public class MorphSelect : SelectingItemsControl, ISelectOptionHost, ISelectFieldAutomation
{
    public const string AnchorPart = "PART_Anchor";
    public const string TriggerPart = "PART_Trigger";
    public const string ValuePart = "PART_Value";
    public const string PopupPart = "PART_Popup";
    public const string AnnouncerPart = "PART_Announcer";
    public const string OpenPseudoClass = ":open";
    public const string PlaceholderPseudoClass = ":placeholder";

    #region 公共属性定义

    /// <summary>Visible label of the trigger; also its accessible name.</summary>
    public static readonly StyledProperty<string?> LabelProperty = Select.LabelProperty.AddOwner<MorphSelect>();

    /// <summary>Defines the <see cref="IsLabelVisible"/> property.</summary>
    public static readonly StyledProperty<bool> IsLabelVisibleProperty =
        AvaloniaProperty.Register<MorphSelect, bool>(nameof(IsLabelVisible), true);

    public static readonly StyledProperty<string?> PlaceholderProperty =
        Select.PlaceholderProperty.AddOwner<MorphSelect>(new StyledPropertyMetadata<string?>("Select"));

    /// <summary>Whether the option list is open. Two-way.</summary>
    public static readonly StyledProperty<bool> IsOpenProperty = Select.IsOpenProperty.AddOwner<MorphSelect>();

    /// <summary>Defines the <see cref="SearchMode"/> property.</summary>
    public static readonly StyledProperty<MorphSelectSearchMode> SearchModeProperty =
        AvaloniaProperty.Register<MorphSelect, MorphSelectSearchMode>(nameof(SearchMode));

    /// <summary>Defines the <see cref="SearchPlaceholder"/> property.</summary>
    public static readonly StyledProperty<string?> SearchPlaceholderProperty =
        AvaloniaProperty.Register<MorphSelect, string?>(nameof(SearchPlaceholder));

    /// <summary>Defines the <see cref="PanelWidth"/> property.</summary>
    public static readonly StyledProperty<double> PanelWidthProperty =
        AvaloniaProperty.Register<MorphSelect, double>(nameof(PanelWidth), 272);

    /// <summary>Defines the <see cref="MaxListHeight"/> property.</summary>
    public static readonly StyledProperty<double> MaxListHeightProperty =
        AvaloniaProperty.Register<MorphSelect, double>(nameof(MaxListHeight), 296);

    /// <summary>Defines the <see cref="PanelAlignment"/> property.</summary>
    public static readonly StyledProperty<MorphSelectAlignment> PanelAlignmentProperty =
        AvaloniaProperty.Register<MorphSelect, MorphSelectAlignment>(nameof(PanelAlignment));

    /// <summary>Returns the display text for an option.</summary>
    public static readonly StyledProperty<Func<object?, string?>?> TextSelectorProperty = Select.TextSelectorProperty.AddOwner<MorphSelect>();

    /// <summary>Returns the value of an option, used to match the selection.</summary>
    public static readonly StyledProperty<Func<object?, object?>?> ValueSelectorProperty = Select.ValueSelectorProperty.AddOwner<MorphSelect>();

    /// <summary>Returns true for an option that cannot be chosen.</summary>
    public static readonly StyledProperty<Func<object?, bool>?> DisabledSelectorProperty = Select.DisabledSelectorProperty.AddOwner<MorphSelect>();

    /// <summary>Returns true when an option matches the search text; filters the list while searching.</summary>
    public static readonly StyledProperty<Func<object?, string, bool>?> FilterPredicateProperty =
        Combobox.FilterPredicateProperty.AddOwner<MorphSelect>();

    public static readonly DirectProperty<MorphSelect, string?> SelectedTextProperty =
        AvaloniaProperty.RegisterDirect<MorphSelect, string?>(nameof(SelectedText), o => o.SelectedText);

    /// <summary>Visible label; also the accessible name of the trigger and the list.</summary>
    public string? Label { get => GetValue(LabelProperty); set => SetValue(LabelProperty, value); }

    /// <summary>False keeps the label for assistive technology only (Arc: <c>hideLabel</c>).</summary>
    public bool IsLabelVisible { get => GetValue(IsLabelVisibleProperty); set => SetValue(IsLabelVisibleProperty, value); }

    /// <summary>Shown while nothing is selected (Arc: "Select").</summary>
    public string? Placeholder { get => GetValue(PlaceholderProperty); set => SetValue(PlaceholderProperty, value); }

    /// <inheritdoc cref="IsOpenProperty"/>
    public bool IsOpen { get => GetValue(IsOpenProperty); set => SetValue(IsOpenProperty, value); }

    /// <summary>Shows a search field in place of the trigger when open; Auto turns it on above eight options.</summary>
    public MorphSelectSearchMode SearchMode { get => GetValue(SearchModeProperty); set => SetValue(SearchModeProperty, value); }

    /// <summary>Placeholder of the search field (default: the selected label, else "Search").</summary>
    public string? SearchPlaceholder { get => GetValue(SearchPlaceholderProperty); set => SetValue(SearchPlaceholderProperty, value); }

    /// <summary>Width of the open surface (Arc: 272). It never gets narrower than the trigger.</summary>
    public double PanelWidth { get => GetValue(PanelWidthProperty); set => SetValue(PanelWidthProperty, value); }

    /// <summary>Tallest the option list gets before it scrolls (Arc: 296).</summary>
    public double MaxListHeight { get => GetValue(MaxListHeightProperty); set => SetValue(MaxListHeightProperty, value); }

    /// <summary>Which edge of the trigger stays put while the surface grows.</summary>
    public MorphSelectAlignment PanelAlignment { get => GetValue(PanelAlignmentProperty); set => SetValue(PanelAlignmentProperty, value); }

    /// <inheritdoc cref="Select.TextSelector"/>
    public Func<object?, string?>? TextSelector { get => GetValue(TextSelectorProperty); set => SetValue(TextSelectorProperty, value); }

    /// <inheritdoc cref="Select.ValueSelector"/>
    public Func<object?, object?>? ValueSelector { get => GetValue(ValueSelectorProperty); set => SetValue(ValueSelectorProperty, value); }

    /// <inheritdoc cref="Select.DisabledSelector"/>
    public Func<object?, bool>? DisabledSelector { get => GetValue(DisabledSelectorProperty); set => SetValue(DisabledSelectorProperty, value); }

    /// <inheritdoc cref="Combobox.FilterPredicate"/>
    public Func<object?, string, bool>? FilterPredicate { get => GetValue(FilterPredicateProperty); set => SetValue(FilterPredicateProperty, value); }

    /// <summary>Label of the selected option, or null.</summary>
    public string? SelectedText
    {
        get => _selectedText;
        private set => SetAndRaise(SelectedTextProperty, ref _selectedText, value);
    }

    #endregion

    #region 内部属性定义

    internal static readonly StyledProperty<IBrush?> SurfaceBrushProperty =
        AvaloniaProperty.Register<MorphSelect, IBrush?>("SurfaceBrush");

    internal static readonly StyledProperty<IBrush?> SurfaceRaisedBrushProperty =
        AvaloniaProperty.Register<MorphSelect, IBrush?>("SurfaceRaisedBrush");

    internal static readonly StyledProperty<IBrush?> OutlineBrushProperty =
        AvaloniaProperty.Register<MorphSelect, IBrush?>("OutlineBrush");

    internal static readonly StyledProperty<BoxShadows> RestShadowProperty =
        AvaloniaProperty.Register<MorphSelect, BoxShadows>("RestShadow");

    internal static readonly StyledProperty<BoxShadows> OpenShadowProperty =
        AvaloniaProperty.Register<MorphSelect, BoxShadows>("OpenShadow");

    /// <summary>The gliding highlight: foreground at 6.5% (set by the theme from the MorphSelect token).</summary>
    internal static readonly StyledProperty<IBrush?> OptionHighlightProperty =
        AvaloniaProperty.Register<MorphSelect, IBrush?>(nameof(OptionHighlight));

    /// <summary>Fill of the search clear button and the hovered close hit (foreground at 8%).</summary>
    internal static readonly StyledProperty<IBrush?> ControlFillProperty =
        AvaloniaProperty.Register<MorphSelect, IBrush?>(nameof(ControlFill));

    internal IBrush? OptionHighlight { get => GetValue(OptionHighlightProperty); set => SetValue(OptionHighlightProperty, value); }

    internal IBrush? ControlFill { get => GetValue(ControlFillProperty); set => SetValue(ControlFillProperty, value); }

    #endregion

    private const int TypeaheadReset = 600;
    private const int Page = 8;
    private const double LidHeight = 44;
    private const double OpenRadius = 22;
    /// <summary>Shortest list kept when the room under the trigger is short (three rows and a bit).</summary>
    private const double MinListHeight = 120;
    /// <summary>Gap kept between the open surface and the bottom edge of the window.</summary>
    private const double EdgeGap = 8;

    private readonly SelectEntrySource _entries;
    private readonly List<int> _visible = new();
    private readonly SelectTypeahead _typeahead = new(TypeaheadReset);
    private string? _selectedText;
    private string _query = string.Empty;
    private MorphSelectAnchor? _anchor;
    private Border? _trigger;
    private SelectValueRoll? _value;
    private ArcPopup? _popup;
    private TextBlock? _announcer;
    private TextBlock? _label;
    private int _lastIndex = -1;
    private int _closeGeneration;
    private bool _syncingQuery;
    private (int Direction, Vector? Fly)? _pendingChange;

    // Popup parts (built once, reused).
    private MorphSelectSurface? _surface;
    private Panel? _lid;
    private SelectValueRoll? _lidValue;
    private ArcGlyph? _lidChevron;
    private RotateTransform? _chevronTurn;
    private MotionValue<double>? _chevronAngle;
    private Panel? _searchRow;
    private TextBox? _search;
    private PresenceHost? _clearHost;
    private Border? _face;
    private ScrollViewer? _scroll;
    private SelectOptionView? _options;
    private TextBlock? _empty;
    private MotionValue<double>? _searchFade;

    static MorphSelect()
    {
        FocusableProperty.OverrideDefaultValue<MorphSelect>(true);
        SelectionModeProperty.OverrideDefaultValue<MorphSelect>(SelectionMode.Single);
    }

    public MorphSelect()
    {
        _entries = new SelectEntrySource(this, () => TextSelector, () => DisabledSelector);
        _entries.Changed += (_, _) => OnEntriesChanged();
        SelectedValueBinding = SelectValueConverter.CreateBinding(() => ValueSelector);
        ArcTokens.Bind(this, SurfaceBrushProperty, ArcDesignTokenKind.Surface);
        ArcTokens.Bind(this, SurfaceRaisedBrushProperty, ArcDesignTokenKind.SurfaceRaised);
        ArcTokens.Bind(this, OutlineBrushProperty, ArcDesignTokenKind.Border);
        ArcTokens.Bind(this, RestShadowProperty, ArcDesignTokenKind.ShadowResting);
        ArcTokens.Bind(this, OpenShadowProperty, ArcDesignTokenKind.ShadowFloating);
    }

    SelectEntrySource ISelectOptionHost.Entries => _entries;
    string? ISelectFieldAutomation.AutomationLabel => Label;
    string? ISelectFieldAutomation.AutomationDescription => null;
    string? ISelectFieldAutomation.AutomationValue => SelectedText;

    bool ISelectFieldAutomation.AutomationIsOpen
    {
        get => IsOpen;
        set => SetCurrentValue(IsOpenProperty, value);
    }

    /// <summary>True when the open surface shows a search field.</summary>
    public bool IsSearchActive => CanSearch;

    private bool CanSearch => SearchMode switch
    {
        MorphSelectSearchMode.On => true,
        MorphSelectSearchMode.Off => false,
        _ => _entries.Count > 8
    };

    internal SelectOptionView? OptionView => _options;

    internal SelectTypeahead Typeahead => _typeahead;

    internal MorphSelectSurface? Surface => _surface;

    protected override AutomationPeer OnCreateAutomationPeer() => new SelectFieldAutomationPeer(this, this);

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        if (_trigger is not null)
        {
            _trigger.PointerPressed -= OnTriggerPressed;
            _trigger.PointerReleased -= OnTriggerReleased;
        }
        if (_popup is not null)
        {
            _popup.Closed -= OnPopupClosed;
            _popup.Child = null;
        }
        _anchor = e.NameScope.Find<MorphSelectAnchor>(AnchorPart);
        _trigger = e.NameScope.Find<Border>(TriggerPart);
        _value = e.NameScope.Find<SelectValueRoll>(ValuePart);
        _popup = e.NameScope.Find<ArcPopup>(PopupPart);
        _announcer = e.NameScope.Find<TextBlock>(AnnouncerPart);
        _label = e.NameScope.Find<TextBlock>("PART_Label");
        UpdateLabelVisibility();
        if (_trigger is not null)
        {
            _trigger.PointerPressed += OnTriggerPressed;
            _trigger.PointerReleased += OnTriggerReleased;
        }
        if (_popup is not null)
        {
            _popup.Closed += OnPopupClosed;
            _popup.PlacementTarget = _trigger;
            _popup.Placement = PlacementMode.Custom;
            _popup.CustomPopupPlacementCallback = PlaceSurface;
            _popup.MarginToAnchor = 0;
            // The surface draws its own resting and floating shadows as it morphs.
            _popup.PopupRootShadow = default;
            _popup.OverlayHostShadow = default;
            _popup.IsInstant = true;
            _popup.EnterScale = 1;
            _popup.EnterOffset = 0;
        }
        if (_anchor is not null)
        {
            _anchor.AlignEnd = PanelAlignment == MorphSelectAlignment.End;
        }
        UpdateValue(animate: false);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == IsOpenProperty)
        {
            var open = change.GetNewValue<bool>();
            PseudoClasses.Set(OpenPseudoClass, open);
            if (open)
            {
                OpenSurface(null, string.Empty);
            }
            else
            {
                CloseSurface();
            }
            (ControlAutomationPeer.FromElement(this) as SelectFieldAutomationPeer)?.RaiseExpandChanged(open);
        }
        else if (change.Property == SelectedIndexProperty)
        {
            UpdateValue(animate: true);
            _options?.InvalidateRows();
        }
        else if (change.Property == PlaceholderProperty)
        {
            UpdateValue(animate: false);
        }
        else if (change.Property == LabelProperty || change.Property == IsLabelVisibleProperty)
        {
            UpdateLabelVisibility();
        }
        else if (change.Property == TextSelectorProperty || change.Property == DisabledSelectorProperty ||
                 change.Property == DisplayMemberBindingProperty)
        {
            _entries.Invalidate();
        }
        else if (change.Property == PanelAlignmentProperty && _anchor is not null)
        {
            _anchor.AlignEnd = PanelAlignment == MorphSelectAlignment.End;
        }
        else if (change.Property == SurfaceBrushProperty || change.Property == SurfaceRaisedBrushProperty ||
                 change.Property == OutlineBrushProperty || change.Property == RestShadowProperty ||
                 change.Property == OpenShadowProperty)
        {
            ApplySurfaceStyle();
        }
        else if (change.Property == IsEffectivelyEnabledProperty && !IsEffectivelyEnabled)
        {
            SetCurrentValue(IsOpenProperty, false);
        }
    }

    private void UpdateLabelVisibility()
    {
        if (_label is not null)
        {
            // hideLabel keeps the label for assistive technology only (the peer reports Label as the name).
            _label.IsVisible = IsLabelVisible && !string.IsNullOrEmpty(Label);
        }
    }

    private void OnEntriesChanged()
    {
        if (IsOpen)
        {
            Refilter(keepActive: true);
        }
        Dispatcher.UIThread.Post(() => UpdateValue(animate: false), DispatcherPriority.Background);
    }

    // ---------------------------------------------------------------- value

    private (string Text, object? Icon, bool Placeholder) CurrentValue()
    {
        var index = SelectedIndex;
        if (index >= 0 && index < _entries.Count)
        {
            var entry = _entries[index];
            return (entry.Text, entry.Icon, false);
        }
        return (Placeholder ?? string.Empty, null, true);
    }

    private void UpdateValue(bool animate)
    {
        var index = SelectedIndex;
        var (text, icon, placeholder) = CurrentValue();
        SelectedText = placeholder ? null : text;
        PseudoClasses.Set(PlaceholderPseudoClass, placeholder);
        var change = _pendingChange;
        _pendingChange = null;
        var direction = change?.Direction ?? (index > _lastIndex ? 1 : -1);
        var surfaceVisible = _popup?.IsOpen == true;
        // Under an open surface the window trigger changes at once; the lid on top carries the motion.
        _value?.Show(text, icon, placeholder, direction, animate: animate && !surfaceVisible);
        if (surfaceVisible)
        {
            _lidValue?.Show(text, icon, placeholder, direction, change?.Fly, animate);
        }
        else
        {
            _lidValue?.Show(text, icon, placeholder, direction, animate: false);
        }
        _lastIndex = index;
        if (_search is not null && SearchPlaceholder is null)
        {
            _search.PlaceholderText = SelectedText ?? "Search";
        }
    }

    // ---------------------------------------------------------------- surface

    private void EnsureSurface()
    {
        if (_surface is not null || _popup is null)
        {
            return;
        }
        // Lid: the trigger row as it appears on the open surface.
        _lidValue = new SelectValueRoll
        {
            Margin = new Thickness(14, 0, 36, 0),
            VerticalAlignment = VerticalAlignment.Center,
            EnterTravel = 0.5,
            ExitTravel = 0.45,
            EnterBlur = 4,
            ExitBlur = 2,
            ExitDuration = TimeSpan.FromMilliseconds(120),
            SpringTravel = true
        };
        ArcTokens.Bind(_lidValue, SelectValueRoll.ForegroundProperty, ArcDesignTokenKind.Foreground);
        ArcTokens.Bind(_lidValue, SelectValueRoll.PlaceholderForegroundProperty, ArcDesignTokenKind.TextMuted);
        ArcTokens.Bind(_lidValue, SelectValueRoll.IconForegroundProperty, ArcDesignTokenKind.TextSecondary);
        Avalonia.Controls.Documents.TextElement.SetFontWeight(_lidValue, FontWeight.Medium);

        _chevronTurn = new RotateTransform();
        _lidChevron = new ArcGlyph
        {
            Data = ArcIcons.ChevronDown,
            Size = 16,
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 12, 0),
            RenderTransform = _chevronTurn,
            RenderTransformOrigin = RelativePoint.Center,
            IsHitTestVisible = false
        };
        ArcTokens.Bind(_lidChevron, ArcGlyph.ForegroundProperty, ArcDesignTokenKind.TextMuted);

        _searchRow = BuildSearchRow();
        _lid = new Panel
        {
            Height = LidHeight,
            Background = Brushes.Transparent,
            Cursor = new Cursor(StandardCursorType.Hand),
            Children = { _lidValue, _searchRow, _lidChevron }
        };
        _lid.PointerReleased += OnLidReleased;
        _lid.PointerPressed += (_, e) => e.Handled = true;

        // Face: the option list hanging under the lid.
        _options = new SelectOptionView
        {
            RowHeight = 36,
            RowSpacing = 2,
            RowPadding = new Thickness(8, 0, 10, 0),
            ContentSpacing = 8,
            CheckMode = SelectCheckMode.ScaleIn,
            ReserveCheck = true,
            GlideHighlight = true,
            DisabledOpacity = 0.6,
            LabelFontWeight = FontWeight.Medium,
            ShowGroups = true,
            GroupHeaderHeight = 28,
            GroupSpacing = 4,
            Host = this
        };
        ArcTokens.Bind(_options, SelectOptionView.RowCornerRadiusProperty, ArcDesignTokenKind.RadiusStep16);
        ArcTokens.Bind(_options, SelectOptionView.ForegroundProperty, ArcDesignTokenKind.TextSecondary);
        ArcTokens.Bind(_options, SelectOptionView.EmphasisForegroundProperty, ArcDesignTokenKind.Foreground);
        ArcTokens.Bind(_options, SelectOptionView.MutedForegroundProperty, ArcDesignTokenKind.TextMuted);
        ArcTokens.Bind(_options, SelectOptionView.CheckForegroundProperty, ArcDesignTokenKind.Foreground);
        _options.Bind(SelectOptionView.HighlightBrushProperty, this.GetObservable(OptionHighlightProperty));
        _scroll = new ScrollViewer
        {
            Padding = new Thickness(0, 6, 0, 0),
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            Content = _options
        };
        _empty = new TextBlock { IsVisible = false, Margin = new Thickness(10, 12, 10, 14), TextWrapping = TextWrapping.Wrap };
        ArcTokens.Bind(_empty, TextBlock.ForegroundProperty, ArcDesignTokenKind.TextMuted);
        var separator = new Border { Height = 1, Margin = new Thickness(8, 0), VerticalAlignment = VerticalAlignment.Top };
        ArcTokens.Bind(separator, Border.BackgroundProperty, ArcDesignTokenKind.BorderSubtle);
        _face = new Border
        {
            Padding = new Thickness(6, 0, 6, 6),
            Child = new Panel { Children = { new StackPanel { Children = { _scroll, _empty } }, separator } }
        };

        _surface = new MorphSelectSurface(_lid, _face) { LidHeight = LidHeight, CornerRadiusValue = OpenRadius };
        _surface.OutsidePressed += (_, _) => Close(restoreFocus: false);
        _surface.LayoutUpdated += OnSurfaceLayoutUpdated;
        _popup.Child = _surface;
        ApplySurfaceStyle();
    }

    private Panel BuildSearchRow()
    {
        var glyph = new ArcGlyph { Data = ArcIcons.Search, Size = 16, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 8, 0) };
        ArcTokens.Bind(glyph, ArcGlyph.ForegroundProperty, ArcDesignTokenKind.TextMuted);
        DockPanel.SetDock(glyph, Dock.Left);

        _search = SelectInputBox.Create();
        _search.FontWeight = FontWeight.Normal;
        _search.AddHandler(KeyDownEvent, OnSearchKeyDown, RoutingStrategies.Tunnel);
        _search.PropertyChanged += OnSearchPropertyChanged;
        AutomationProperties.SetName(_search, $"Search {Label?.ToLowerInvariant()}");

        var clear = CreateRoundButton(SelectGlyphs.X, 14, 24, "Clear search", filled: true);
        clear.Click += (_, _) =>
        {
            SetQuery(string.Empty, fromInput: false);
            _search?.Focus(NavigationMethod.Pointer);
        };
        _clearHost = new PresenceHost
        {
            Mode = PresenceMode.Pop,
            EnterScale = 0.6,
            Spring = ArcSpring.Snappy,
            VerticalAlignment = VerticalAlignment.Center,
            Child = clear
        };
        DockPanel.SetDock(_clearHost, Dock.Right);
        var close = CreateRoundButton(null, 16, 36, $"Close {Label?.ToLowerInvariant()}", filled: false);
        close.Click += (_, _) => Close(restoreFocus: true);
        DockPanel.SetDock(close, Dock.Right);
        var row = new DockPanel
        {
            Margin = new Thickness(14, 0, 2, 0),
            IsVisible = false,
            Opacity = 0,
            Children = { glyph, close, _clearHost, _search }
        };
        row.PointerPressed += (_, e) => e.Handled = true;
        row.PointerReleased += (_, e) => e.Handled = true;
        return row;
    }

    private AvaloniaButton CreateRoundButton(Geometry? glyph, double glyphSize, double size, string name, bool filled)
    {
        var button = new AvaloniaButton
        {
            Width = size,
            Height = size,
            Padding = default,
            Background = Brushes.Transparent,
            Cursor = new Cursor(StandardCursorType.Hand),
            VerticalAlignment = VerticalAlignment.Center,
            Focusable = false,
            Template = new Avalonia.Controls.Templates.FuncControlTemplate<AvaloniaButton>((b, _) =>
            {
                var face = new Border().BindCornerRadius(ArcDesignTokenKind.RadiusPill);
                face.Bind(Border.BackgroundProperty, b.GetObservable(BackgroundProperty));
                if (glyph is not null)
                {
                    var mark = new ArcGlyph
                    {
                        Data = glyph,
                        Size = glyphSize,
                        HorizontalAlignment = HorizontalAlignment.Center,
                        VerticalAlignment = VerticalAlignment.Center
                    };
                    mark.Bind(ArcGlyph.ForegroundProperty, b.GetObservable(ForegroundProperty));
                    face.Child = mark;
                }
                return face;
            })
        };
        AutomationProperties.SetName(button, name);
        ArcTokens.Bind(button, ForegroundProperty, ArcDesignTokenKind.TextSecondary);
        if (filled)
        {
            button.Bind(BackgroundProperty, this.GetObservable(ControlFillProperty));
        }
        else
        {
            button.PointerEntered += (_, e) =>
            {
                if (e.Pointer.Type == PointerType.Mouse)
                {
                    button.Background = ControlFill;
                }
            };
            button.PointerExited += (_, _) => button.Background = Brushes.Transparent;
        }
        return button;
    }

    private void ApplySurfaceStyle()
    {
        if (_surface is null)
        {
            return;
        }
        static Color C(IBrush? brush) => brush is ISolidColorBrush solid ? solid.Color : Colors.Transparent;
        _surface.RestFill = C(GetValue(SurfaceBrushProperty));
        _surface.OpenFill = C(GetValue(SurfaceRaisedBrushProperty));
        _surface.BorderPen = new Pen(GetValue(OutlineBrushProperty), 1);
        _surface.RestShadow = GetValue(RestShadowProperty);
        _surface.OpenShadow = GetValue(OpenShadowProperty);
        _surface.InvalidateVisual();
    }

    private void PlaceSurface(CustomPopupPlacement placement)
    {
        var room = _surface?.ShadowRoom ?? default;
        if (PanelAlignment == MorphSelectAlignment.End)
        {
            placement.Anchor = PopupAnchor.TopRight;
            placement.Gravity = PopupGravity.BottomLeft;
            placement.Offset = new Point(room.Right, -room.Top);
        }
        else
        {
            placement.Anchor = PopupAnchor.TopLeft;
            placement.Gravity = PopupGravity.BottomRight;
            placement.Offset = new Point(-room.Left, -room.Top);
        }
        // The surface grows from the trigger in place; it slides into view but never flips.
        placement.ConstraintAdjustment = PopupPositionerConstraintAdjustment.SlideX | PopupPositionerConstraintAdjustment.SlideY;
    }

    private static readonly Thickness s_shadowRoom = new(56, 40, 56, 80);

    /// <summary>
    /// Room for the floating shadow around the shape, trimmed where the window ends so the popup never has to slide
    /// (a slid popup would no longer start exactly over the trigger).
    /// </summary>
    private Thickness ShadowRoomFor(Size capacity, double triggerWidth)
    {
        var topLevel = TopLevel.GetTopLevel(this);
        if (topLevel is null || _trigger is null || _trigger.TranslatePoint(default, topLevel) is not { } origin)
        {
            return s_shadowRoom;
        }
        var client = topLevel.ClientSize;
        var left = PanelAlignment == MorphSelectAlignment.End ? origin.X + triggerWidth - capacity.Width : origin.X;
        static double Fit(double wanted, double space) => Math.Clamp(Math.Min(wanted, Math.Floor(space)), 0, wanted);
        return new Thickness(
            Fit(s_shadowRoom.Left, left),
            Fit(s_shadowRoom.Top, origin.Y),
            Fit(s_shadowRoom.Right, client.Width - (left + capacity.Width)),
            Fit(s_shadowRoom.Bottom, client.Height - (origin.Y + capacity.Height)));
    }

    private double TriggerWidth => _anchor?.TargetWidth ?? _trigger?.Bounds.Width ?? 0;

    private double _listCap = double.NaN;
    private double _slide;
    private bool _awaitingPlacement;

    /// <summary>Tallest the list gets on the current open: <see cref="MaxListHeight"/> or less (see <see cref="ListCapFor"/>).</summary>
    private double ListCap => double.IsNaN(_listCap) ? MaxListHeight : _listCap;

    /// <summary>
    /// Shortens the list so the open surface fits under the trigger: a popup that has to slide no longer starts over the
    /// trigger, and the surface would fold back into a spot above it. Never under <see cref="MinListHeight"/>.
    /// </summary>
    private double ListCapFor()
    {
        var topLevel = TopLevel.GetTopLevel(this);
        if (topLevel is null || _trigger is null || _trigger.TranslatePoint(default, topLevel) is not { } origin)
        {
            return MaxListHeight;
        }
        // The face adds 6px of scroll inset above the list and 6px of padding under it.
        var room = topLevel.ClientSize.Height - origin.Y - LidHeight - 12 - EdgeGap;
        return Math.Min(MaxListHeight, Math.Max(MinListHeight, Math.Floor(room)));
    }

    /// <summary>
    /// How far the popup put the lid above the trigger: more than zero only when even the shortest list did not fit
    /// under it and the popup slid up into view. The surface then starts and ends its morph over the trigger.
    /// </summary>
    private double MeasureSlide()
    {
        if (_trigger is null || _surface is null || TopLevel.GetTopLevel(this) is not { } topLevel)
        {
            return 0;
        }
        var trigger = _trigger.PointToScreen(default);
        var lid = _surface.PointToScreen(new Point(0, _surface.ShadowRoom.Top));
        var slide = (trigger.Y - lid.Y) / topLevel.RenderScaling;
        return slide > 0.5 ? slide : 0;
    }

    private double ListWidth()
    {
        var viewport = TopLevel.GetTopLevel(this)?.ClientSize.Width ?? double.PositiveInfinity;
        return Math.Max(TriggerWidth, Math.Min(PanelWidth, viewport - 32));
    }

    private double FaceHeight(double extent, bool empty)
    {
        var list = empty ? 0 : Math.Min(ListCap, extent);
        var emptyHeight = empty ? 12 + Math.Ceiling(FontSize * 1.45) + 14 : 0;
        // 6px scroll inset at the top, 6px face padding at the bottom.
        return 6 + list + emptyHeight + 6;
    }

    /// <summary>Opens the surface. <paramref name="focus"/> picks the highlighted option; <paramref name="seed"/> starts a search.</summary>
    private void OpenSurface(int? focus, string seed)
    {
        if (!IsEffectivelyEnabled || _popup is null)
        {
            if (!IsEffectivelyEnabled)
            {
                SetCurrentValue(IsOpenProperty, false);
            }
            return;
        }
        EnsureSurface();
        var surface = _surface!;
        _closeGeneration++;
        var canSearch = CanSearch;
        SetQuery(canSearch ? seed : string.Empty, fromInput: false);
        Refilter(keepActive: false);
        var start = focus ?? (SelectedIndex >= 0 && _entries.IsEnabledAt(SelectedIndex) ? PositionOf(SelectedIndex) : -1);
        if (start < 0 || !_entries.IsEnabledAt(_options!.EntryAt(start)))
        {
            start = FirstEnabled(0, 1);
        }

        if (!_popup.IsOpen)
        {
            _listCap = ListCapFor();
        }
        var triggerWidth = TriggerWidth;
        var listWidth = ListWidth();
        var fullExtent = _options!.Extent;
        var openHeight = LidHeight + FaceHeight(_options.Extent, _visible.Count == 0);
        var capacityHeight = LidHeight + Math.Max(FaceHeight(fullExtent, false), FaceHeight(0, true));
        surface.AlignEnd = PanelAlignment == MorphSelectAlignment.End;
        surface.FaceWidth = listWidth;
        surface.Capacity = new Size(Math.Max(triggerWidth, listWidth), Math.Max(capacityHeight, openHeight));
        if (!_popup.IsOpen)
        {
            surface.ShadowRoom = ShadowRoomFor(surface.Capacity, triggerWidth);
        }
        surface.InvalidateMeasure();
        if (!_popup.IsOpen)
        {
            var (text, icon, placeholder) = CurrentValue();
            _lidValue!.Show(text, icon, placeholder, animate: false);
            surface.Jump(new Size(triggerWidth, LidHeight), open: false, drop: 0);
            _slide = 0;
            _awaitingPlacement = true;
            _popup.IsOpen = true;
            // Placement settles in the layout passes before the first frame; later passes leave the drop alone.
            Dispatcher.UIThread.Post(() => _awaitingPlacement = false, DispatcherPriority.Background);
        }
        if (_face is not null)
        {
            _face.Width = listWidth;
        }
        if (_scroll is not null)
        {
            // The 6px top inset is scroll padding outside the list's max height (content-box).
            _scroll.MaxHeight = ListCap + 6;
        }
        _ = surface.MorphTo(new Size(Math.Max(triggerWidth, listWidth), openHeight), open: true, drop: 0);
        TurnChevron(open: true);
        ShowSearch(canSearch);
        _options.SetActive(start, animate: false);
        Dispatcher.UIThread.Post(() =>
        {
            if (!IsOpen || _options is null)
            {
                return;
            }
            var active = _options.ActiveOption;
            if (active >= 0)
            {
                _options.SetActive(active, animate: false);
                _options.ScrollIntoView(active, 6);
            }
            if (canSearch)
            {
                _search?.Focus(NavigationMethod.Tab);
                if (_search is not null)
                {
                    _search.CaretIndex = _search.Text?.Length ?? 0;
                }
            }
        }, DispatcherPriority.Loaded);
        Announce(string.Empty);
    }

    private async void CloseSurface()
    {
        _typeahead.Reset();
        if (_popup is null || _surface is null || !_popup.IsOpen)
        {
            return;
        }
        var generation = ++_closeGeneration;
        var focusInside = _search?.IsFocused == true;
        TurnChevron(open: false);
        ShowSearch(false);
        _options?.SetActive(-1, animate: true);
        if (ArcMotion.GetEffectiveLevel(this) == ArcMotionLevel.Off || !IsLoaded)
        {
            FinishClose(focusInside);
            return;
        }
        // The surface folds back into the trigger, which springs to the new label's width at the same time.
        _awaitingPlacement = false;
        _slide = MeasureSlide();
        var reached = await _surface.MorphTo(new Size(TriggerWidth, LidHeight), open: false, drop: _slide);
        if (generation != _closeGeneration || IsOpen)
        {
            return;
        }
        if (!reached && _surface.IsMoving)
        {
            return;
        }
        FinishClose(focusInside);
    }

    /// <summary>
    /// The popup is placed in the layout passes after it opens (an overlay popup is laid out before it gets its
    /// position, so an early pass measures a wrong slide). Each pass until the first frame measures again; if the popup
    /// had to slide up, the closed shape sits down on the trigger and rises from there.
    /// </summary>
    private void OnSurfaceLayoutUpdated(object? sender, EventArgs e)
    {
        if (!_awaitingPlacement || _surface is null || _popup is not { IsOpen: true })
        {
            return;
        }
        var slide = MeasureSlide();
        if (Math.Abs(slide - _slide) > 0.5)
        {
            _slide = slide;
            _surface.RiseFrom(slide);
        }
    }

    private void FinishClose(bool focusInside)
    {
        if (_popup is { IsOpen: true })
        {
            // The surface already sits exactly over the trigger: closing needs no fade of its own.
            _popup.IsMotionEnabled = false;
            _popup.IsOpen = false;
        }
        SetQuery(string.Empty, fromInput: false);
        if (focusInside || _restoreFocus)
        {
            Focus(NavigationMethod.Tab);
        }
        _restoreFocus = false;
    }

    private bool _restoreFocus;

    private void Close(bool restoreFocus)
    {
        _restoreFocus = restoreFocus;
        if (restoreFocus)
        {
            Focus(NavigationMethod.Tab);
        }
        SetCurrentValue(IsOpenProperty, false);
    }

    private void OnPopupClosed(object? sender, EventArgs e)
    {
        if (IsOpen)
        {
            SetCurrentValue(IsOpenProperty, false);
        }
    }

    private void TurnChevron(bool open)
    {
        if (_chevronTurn is null)
        {
            return;
        }
        _chevronAngle ??= CreateChevronAngle();
        _ = _chevronAngle.AnimateTo(open ? 180 : 0, MotionSpec.Spring(SelectValueRoll.Glide));
    }

    private MotionValue<double> CreateChevronAngle()
    {
        var value = new MotionValue<double>(this, 0, MotionVectors.Double);
        value.Changed += v =>
        {
            if (_chevronTurn is not null)
            {
                _chevronTurn.Angle = v;
            }
        };
        return value;
    }

    /// <summary>Search takes the lid's place (fade with a subtle blur, 40ms after the surface starts to grow).</summary>
    private void ShowSearch(bool show)
    {
        if (_searchRow is null || _lidValue is null)
        {
            return;
        }
        _searchFade ??= CreateSearchFade();
        if (show)
        {
            _searchRow.IsVisible = true;
            _lidValue.Opacity = 0;
            var delay = ArcMotion.IsFull(this) ? TimeSpan.FromMilliseconds(40) : TimeSpan.Zero;
            _ = _searchFade.AnimateTo(1, MotionSpec.Tween(TimeSpan.FromMilliseconds(180), ArcEasings.Enter, delay));
        }
        else
        {
            _lidValue.Opacity = 1;
            _searchFade.AnimateTo(0, MotionSpec.Tween(TimeSpan.FromMilliseconds(100), ArcEasings.Standard)).ContinueWith(
                t => Dispatcher.UIThread.Post(() =>
                {
                    if (t.Result && _searchRow is not null && _searchFade?.Value <= 0.001)
                    {
                        _searchRow.IsVisible = false;
                    }
                }), TaskScheduler.Default);
        }
    }

    private MotionValue<double> CreateSearchFade()
    {
        var value = new MotionValue<double>(this, 0, MotionVectors.Double);
        var blur = new BlurEffect();
        value.Changed += v =>
        {
            if (_searchRow is null)
            {
                return;
            }
            _searchRow.Opacity = Math.Clamp(v, 0, 1);
            var radius = ArcMotion.IsFull(this) ? 2 * (1 - Math.Clamp(v, 0, 1)) : 0;
            if (radius < 0.05)
            {
                _searchRow.Effect = null;
            }
            else
            {
                blur.Radius = radius;
                _searchRow.Effect = blur;
            }
        };
        return value;
    }

    // ---------------------------------------------------------------- filtering

    private void SetQuery(string query, bool fromInput)
    {
        _query = query;
        if (!fromInput && _search is not null && _search.Text != query)
        {
            _syncingQuery = true;
            _search.Text = query;
            _search.CaretIndex = query.Length;
            _syncingQuery = false;
        }
        if (_clearHost is not null)
        {
            _clearHost.IsPresent = query.Length > 0;
        }
    }

    private void OnSearchPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (e.Property != TextBox.TextProperty || _syncingQuery || _search is null)
        {
            return;
        }
        SetQuery(_search.Text ?? string.Empty, fromInput: true);
        Refilter(keepActive: false);
        _scroll?.SetCurrentValue(ScrollViewer.OffsetProperty, default(Vector));
        var text = _query.Trim();
        Announce(text.Length == 0
            ? string.Empty
            : _visible.Count == 0 ? "No matches" : $"{_visible.Count} {(_visible.Count == 1 ? "option" : "options")}");
        ResizeOpen();
    }

    private void Refilter(bool keepActive)
    {
        if (_options is null)
        {
            return;
        }
        var activeEntry = keepActive ? _options.EntryAt(_options.ActiveOption) : -1;
        _entries.Filter(CanSearch ? _query : null, FilterPredicate, _visible);
        _options.SetOptions(_visible);
        if (_empty is not null)
        {
            _empty.IsVisible = _visible.Count == 0;
            _empty.Text = $"No matches for “{_query.Trim()}”";
        }
        // The current option is the active one if it survived the filter, else the first enabled.
        var position = activeEntry >= 0 ? _visible.IndexOf(activeEntry) : -1;
        if (position < 0 || !_entries.IsEnabledAt(_visible[position]))
        {
            position = FirstEnabled(0, 1);
        }
        _options.SetActive(position, animate: false);
    }

    /// <summary>The list changed height while open (search): the surface springs to it.</summary>
    private void ResizeOpen()
    {
        if (_surface is null || _options is null || !IsOpen)
        {
            return;
        }
        var height = LidHeight + FaceHeight(_options.Extent, _visible.Count == 0);
        var width = Math.Max(TriggerWidth, ListWidth());
        if (height > _surface.Capacity.Height)
        {
            _surface.Capacity = new Size(_surface.Capacity.Width, height);
            _surface.InvalidateMeasure();
        }
        _ = _surface.MorphTo(new Size(width, height), open: true, drop: 0);
    }

    private void Announce(string text)
    {
        // An empty live region makes Avalonia.Native raise a macOS accessibility notification with a nil value, which
        // terminates the app (NSInvalidArgumentException), so a clear leaves the last message in place.
        if (_announcer is not null && text.Length > 0)
        {
            _announcer.Text = text;
        }
    }

    // ---------------------------------------------------------------- navigation

    private int PositionOf(int entryIndex) => _visible.IndexOf(entryIndex);

    private int FirstEnabled(int start, int step)
    {
        for (var i = start; i >= 0 && i < _visible.Count; i += step)
        {
            if (_entries.IsEnabledAt(_visible[i]))
            {
                return i;
            }
        }
        return -1;
    }

    /// <summary>Enabled positions in order (Arc navigates the enabled options of the visible sections).</summary>
    private List<int> EnabledPositions()
    {
        var list = new List<int>();
        for (var i = 0; i < _visible.Count; i++)
        {
            if (_entries.IsEnabledAt(_visible[i]))
            {
                list.Add(i);
            }
        }
        return list;
    }

    private void Move(int position)
    {
        if (_options is null || position < 0)
        {
            return;
        }
        _options.SetActive(position, animate: true);
        _options.ScrollIntoView(position, 6);
        Announce(SelectOptionView.Describe(_entries, _options.EntryAt(position), position, _visible.Count));
    }

    private void Commit(int position)
    {
        if (_options is null || position < 0)
        {
            return;
        }
        var entry = _options.EntryAt(position);
        if (entry < 0 || !_entries.IsEnabledAt(entry))
        {
            return;
        }
        if (entry != SelectedIndex)
        {
            Vector? fly = null;
            if (ArcMotion.IsFull(this) && _surface is not null && _lidValue is not null)
            {
                // P8: start the label where it sits in the option and let it settle into the trigger.
                var from = _options.OptionValueRect(position);
                var fromPoint = _options.TranslatePoint(new Point(from.X, from.Center.Y), _surface);
                var slot = _surface.ShapeRect;
                if (fromPoint is { } p)
                {
                    fly = new Vector(p.X - (slot.X + 14), p.Y - (slot.Y + LidHeight / 2));
                }
            }
            var previous = SelectedIndex;
            var direction = previous < 0 ? 1 : Math.Sign(entry - previous) is var sign && sign != 0 ? sign : 1;
            _pendingChange = (direction, fly);
            SetCurrentValue(SelectedIndexProperty, entry);
        }
        Close(restoreFocus: true);
    }

    // ---------------------------------------------------------------- input

    private void OnTriggerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (!IsEffectivelyEnabled || !e.GetCurrentPoint(_trigger).Properties.IsLeftButtonPressed)
        {
            return;
        }
        Focus(NavigationMethod.Pointer);
        e.Handled = true;
    }

    private void OnTriggerReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (!IsEffectivelyEnabled || _trigger is null || !new Rect(_trigger.Bounds.Size).Contains(e.GetPosition(_trigger)))
        {
            return;
        }
        if (IsOpen)
        {
            Close(restoreFocus: true);
        }
        else
        {
            SetCurrentValue(IsOpenProperty, true);
        }
    }

    private void OnLidReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (!CanSearch && IsOpen)
        {
            e.Handled = true;
            Close(restoreFocus: true);
        }
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (e.Handled || !IsEffectivelyEnabled)
        {
            base.OnKeyDown(e);
            return;
        }
        if (HandleKey(e, fromInput: false))
        {
            e.Handled = true;
            return;
        }
        base.OnKeyDown(e);
    }

    private void OnSearchKeyDown(object? sender, KeyEventArgs e)
    {
        if (HandleKey(e, fromInput: true))
        {
            e.Handled = true;
        }
    }

    private bool HandleKey(KeyEventArgs e, bool fromInput)
    {
        var key = e.Key;
        var typing = _typeahead.IsTyping;
        if (!IsOpen)
        {
            switch (key)
            {
                case Key.Down:
                case Key.Up:
                case Key.Enter:
                case Key.Space when !typing:
                    SetCurrentValue(IsOpenProperty, true);
                    return true;
                case Key.Home:
                case Key.End:
                {
                    var all = Enumerable.Range(0, _entries.Count).Where(_entries.IsEnabledAt).ToList();
                    var target = all.Count == 0 ? -1 : key == Key.Home ? all[0] : all[^1];
                    OpenAt(target);
                    return true;
                }
            }
            return false;
        }
        var enabled = EnabledPositions();
        var current = _options?.ActiveOption ?? -1;
        var at = enabled.IndexOf(current);
        var caretKeys = fromInput && _query.Length > 0;
        switch (key)
        {
            case Key.Down:
                Move(enabled.Count == 0 ? -1 : enabled[Math.Min(enabled.Count - 1, at + 1)]);
                return true;
            case Key.Up when e.KeyModifiers.HasFlag(KeyModifiers.Alt):
                Commit(current);
                return true;
            case Key.Up:
                Move(enabled.Count == 0 ? -1 : enabled[Math.Max(0, at - 1)]);
                return true;
            case Key.PageDown:
                Move(enabled.Count == 0 ? -1 : enabled[Math.Min(enabled.Count - 1, at + Page)]);
                return true;
            case Key.PageUp:
                Move(enabled.Count == 0 ? -1 : enabled[Math.Max(0, at - Page)]);
                return true;
            case Key.Home when !caretKeys:
                Move(enabled.Count == 0 ? -1 : enabled[0]);
                return true;
            case Key.End when !caretKeys:
                Move(enabled.Count == 0 ? -1 : enabled[^1]);
                return true;
            case Key.Enter:
                Commit(current);
                return true;
            case Key.Escape:
                Close(restoreFocus: true);
                return true;
            case Key.Tab:
                Close(restoreFocus: false);
                return false;
            case Key.Space when !fromInput && !typing:
                Commit(current);
                return true;
        }
        return false;
    }

    private void OpenAt(int entryIndex)
    {
        if (IsOpen)
        {
            return;
        }
        SetCurrentValue(IsOpenProperty, true);
        var position = PositionOf(entryIndex);
        if (position >= 0)
        {
            _options?.SetActive(position, animate: false);
        }
    }

    protected override void OnTextInput(TextInputEventArgs e)
    {
        if (e.Handled || !IsEffectivelyEnabled || string.IsNullOrEmpty(e.Text) || e.Text == "\r")
        {
            base.OnTextInput(e);
            return;
        }
        if (e.Text == " " && !_typeahead.IsTyping)
        {
            return;
        }
        e.Handled = true;
        if (!IsOpen)
        {
            if (CanSearch)
            {
                // Typing on the closed trigger opens straight into a search for it.
                SetCurrentValue(IsOpenProperty, true);
                SetQuery(e.Text, fromInput: false);
                Refilter(keepActive: false);
                ResizeOpen();
                return;
            }
            var hit = TypeTo(e.Text, SelectedIndex);
            OpenAt(hit >= 0 ? hit : SelectedIndex);
            return;
        }
        if (_options is null)
        {
            return;
        }
        var match = TypeTo(e.Text, _options.EntryAt(_options.ActiveOption));
        if (match >= 0)
        {
            Move(PositionOf(match));
        }
    }

    private int TypeTo(string text, int fromEntry) =>
        _typeahead.Next(text, fromEntry, _entries.Count, _entries.TextAt, _entries.IsEnabledAt);

    // ---------------------------------------------------------------- ISelectOptionHost

    bool ISelectOptionHost.IsEntrySelected(int entryIndex) => entryIndex == SelectedIndex;

    void ISelectOptionHost.OnOptionHovered(int position) => _options?.SetActive(position, animate: true);

    void ISelectOptionHost.OnOptionInvoked(int position) => Commit(position);

    void ISelectOptionHost.OnOptionsPointerExited()
    {
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        _closeGeneration++;
        if (_popup is { IsOpen: true })
        {
            _popup.IsOpen = false;
        }
        SetCurrentValue(IsOpenProperty, false);
    }
}
