// Ported from Arc's free command-palette block (MIT, Copyright (c) 2026 Elia Kuratli).

using System.Collections;
using System.Collections.Specialized;
using System.Windows.Input;
using TKCode.Arc.Motion;
using TKCode.Arc.Primitives;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Collections;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Metadata;
using Avalonia.Threading;
using Avalonia.VisualTree;
using AvaloniaButton = Avalonia.Controls.Button;
using AvaloniaTextBox = Avalonia.Controls.TextBox;

namespace TKCode.Arc;

/// <summary>
/// Arc command palette: the keyboard entry point to product-wide actions and navigation. A search field over grouped
/// results with icons, descriptions and shortcut caps. Typing filters the commands (label, description, group and
/// keywords); ↓ / ↑ / Home / End move one shared highlight (a 70ms slide for keys, a snappy spring for the pointer),
/// Enter runs the highlighted command (or, after typing, the top match), Escape clears the query and then raises
/// <see cref="CloseRequested"/>. Cmd/Ctrl+K anywhere in the window focuses the field. While filtering, the remaining
/// rows glide into place and the results frame springs to the new height. Only the rows near the visible part of the
/// list exist, so a thousand commands cost the same as ten.
/// <para>
/// The control itself is inline, like Arc's: it does not open or close. For a popup that opens on Cmd+K (or from a
/// button) over the page and closes with a key, use <see cref="CommandPaletteDialog"/>, which hosts a palette.
/// </para>
/// Ported from Arc's free command-palette block (MIT, Copyright (c) 2026 Elia Kuratli).
/// </summary>
[TemplatePart(SurfacePart, typeof(Border))]
[TemplatePart(SearchRowPart, typeof(Border))]
[TemplatePart(InputPart, typeof(AvaloniaTextBox))]
[TemplatePart(ClearButtonPart, typeof(AvaloniaButton))]
[TemplatePart(CloseButtonPart, typeof(AvaloniaButton))]
[TemplatePart(ResultsFramePart, typeof(SpringSizeDecorator))]
[TemplatePart(ScrollPart, typeof(ScrollViewer))]
[TemplatePart(ResultsPart, typeof(Border))]
[TemplatePart(HighlightPart, typeof(Control))]
[TemplatePart(ListPart, typeof(Panel))]
[TemplatePart(EmptyPart, typeof(Control))]
[PseudoClasses(NarrowPseudoClass, NarrowestPseudoClass, HasQueryPseudoClass, NoHintPseudoClass)]
public class CommandPalette : TemplatedControl
{
    public const string SurfacePart = "PART_Surface";
    public const string SearchRowPart = "PART_SearchRow";
    public const string InputPart = "PART_Input";
    public const string ClearButtonPart = "PART_ClearButton";
    public const string CloseButtonPart = "PART_CloseButton";
    public const string ResultsFramePart = "PART_ResultsFrame";
    public const string ScrollPart = "PART_Scroll";
    public const string ResultsPart = "PART_Results";
    public const string HighlightPart = "PART_Highlight";
    public const string ListPart = "PART_List";
    public const string EmptyPart = "PART_Empty";

    /// <summary>Set while the window is at most 480px wide (Arc's <c>max-width: 480px</c> media query): tighter padding, no shortcut caps.</summary>
    public const string NarrowPseudoClass = ":narrow";

    /// <summary>Set while the window is at most 340px wide: rows also tighten their sides.</summary>
    public const string NarrowestPseudoClass = ":narrowest";

    /// <summary>Set while the query has text.</summary>
    public const string HasQueryPseudoClass = ":has-query";

    /// <summary>Set while <see cref="ShortcutHint"/> is empty: the shortcut cap is hidden.</summary>
    public const string NoHintPseudoClass = ":no-hint";

    /// <summary>Widths at or below which the narrow layouts apply (Arc's media queries).</summary>
    private const double NarrowWidth = 480;
    private const double NarrowestWidth = 340;

    #region Properties

    /// <summary>The commands to list; when set it replaces <see cref="Items"/>. Collection changes are followed.</summary>
    public static readonly StyledProperty<IEnumerable<CommandPaletteItem>?> ItemsSourceProperty =
        AvaloniaProperty.Register<CommandPalette, IEnumerable<CommandPaletteItem>?>(nameof(ItemsSource));

    /// <summary>The text in the search field (two-way).</summary>
    public static readonly StyledProperty<string> QueryProperty =
        AvaloniaProperty.Register<CommandPalette, string>(nameof(Query), string.Empty,
            defaultBindingMode: BindingMode.TwoWay, coerce: static (_, value) => value ?? string.Empty);

    /// <summary>Hint in the empty search field.</summary>
    public static readonly StyledProperty<string> PlaceholderTextProperty =
        AvaloniaProperty.Register<CommandPalette, string>(nameof(PlaceholderText), "Search commands");

    /// <summary>Accessible name of the search field.</summary>
    public static readonly StyledProperty<string> LabelProperty =
        AvaloniaProperty.Register<CommandPalette, string>(nameof(Label), "Command palette");

    /// <summary>Focuses the search field once the palette is loaded (a palette that opens on demand).</summary>
    public static readonly StyledProperty<bool> AutoFocusProperty =
        AvaloniaProperty.Register<CommandPalette, bool>(nameof(AutoFocus));

    /// <summary>Shows the "Esc" button that raises <see cref="CloseRequested"/> (for a palette in an overlay).</summary>
    public static readonly StyledProperty<bool> ShowCloseButtonProperty =
        AvaloniaProperty.Register<CommandPalette, bool>(nameof(ShowCloseButton));

    /// <summary>Cmd/Ctrl+K anywhere in the window focuses the search field (handled before the window's own shortcuts).</summary>
    public static readonly StyledProperty<bool> IsShortcutEnabledProperty =
        AvaloniaProperty.Register<CommandPalette, bool>(nameof(IsShortcutEnabled), true);

    /// <summary>Text of the shortcut cap at the end of the search row; null shows "⌘ K" on macOS and "Ctrl K" elsewhere, an empty string hides the cap.</summary>
    public static readonly StyledProperty<string?> ShortcutHintProperty =
        AvaloniaProperty.Register<CommandPalette, string?>(nameof(ShortcutHint));

    /// <summary>Title of the empty state when nothing matches.</summary>
    public static readonly StyledProperty<string> EmptyTitleProperty =
        AvaloniaProperty.Register<CommandPalette, string>(nameof(EmptyTitle), "No matching actions");

    /// <summary>Description of the empty state when nothing matches.</summary>
    public static readonly StyledProperty<string> EmptyDescriptionProperty =
        AvaloniaProperty.Register<CommandPalette, string>(nameof(EmptyDescription), "Try a different word or clear the search.");

    /// <summary>Heading for commands without a <see cref="CommandPaletteItem.Group"/>.</summary>
    public static readonly StyledProperty<string> DefaultGroupProperty =
        AvaloniaProperty.Register<CommandPalette, string>(nameof(DefaultGroup), "Actions");

    /// <summary>Accessible name of the button that clears the query.</summary>
    public static readonly StyledProperty<string> ClearButtonLabelProperty =
        AvaloniaProperty.Register<CommandPalette, string>(nameof(ClearButtonLabel), "Clear search");

    /// <summary>Accessible name of the "Esc" close button.</summary>
    public static readonly StyledProperty<string> CloseButtonLabelProperty =
        AvaloniaProperty.Register<CommandPalette, string>(nameof(CloseButtonLabel), "Close command palette");

    /// <summary>Accessible name of the result list.</summary>
    public static readonly StyledProperty<string> ResultsLabelProperty =
        AvaloniaProperty.Register<CommandPalette, string>(nameof(ResultsLabel), "Command results");

    /// <summary>Executed with the chosen <see cref="CommandPaletteItem"/>.</summary>
    public static readonly StyledProperty<ICommand?> SelectCommandProperty =
        AvaloniaProperty.Register<CommandPalette, ICommand?>(nameof(SelectCommand));

    /// <summary>The highlighted command (keyboard or pointer), or null.</summary>
    public static readonly DirectProperty<CommandPalette, CommandPaletteItem?> ActiveItemProperty =
        AvaloniaProperty.RegisterDirect<CommandPalette, CommandPaletteItem?>(nameof(ActiveItem), o => o.ActiveItem);

    internal static readonly DirectProperty<CommandPalette, string> EffectiveShortcutHintProperty =
        AvaloniaProperty.RegisterDirect<CommandPalette, string>(nameof(EffectiveShortcutHint), o => o.EffectiveShortcutHint);

    /// <summary>Raised when a command is chosen (Enter, or a click), after the query was cleared.</summary>
    public static readonly RoutedEvent<CommandPaletteItemSelectedEventArgs> ItemSelectedEvent =
        RoutedEvent.Register<CommandPalette, CommandPaletteItemSelectedEventArgs>(nameof(ItemSelected), RoutingStrategies.Bubble);

    /// <summary>
    /// Raised by Escape in an empty field or by the "Esc" button: the host should close the palette. Escape is not marked
    /// handled, so an overlay around the palette can also close on it (<see cref="CommandPaletteDialog"/> does).
    /// </summary>
    public static readonly RoutedEvent<CommandPaletteCloseRequestedEventArgs> CloseRequestedEvent =
        RoutedEvent.Register<CommandPalette, CommandPaletteCloseRequestedEventArgs>(nameof(CloseRequested), RoutingStrategies.Bubble);

    /// <inheritdoc cref="ItemsSourceProperty"/>
    public IEnumerable<CommandPaletteItem>? ItemsSource { get => GetValue(ItemsSourceProperty); set => SetValue(ItemsSourceProperty, value); }

    /// <inheritdoc cref="QueryProperty"/>
    public string Query { get => GetValue(QueryProperty); set => SetValue(QueryProperty, value); }

    /// <inheritdoc cref="PlaceholderTextProperty"/>
    public string PlaceholderText { get => GetValue(PlaceholderTextProperty); set => SetValue(PlaceholderTextProperty, value); }

    /// <inheritdoc cref="LabelProperty"/>
    public string Label { get => GetValue(LabelProperty); set => SetValue(LabelProperty, value); }

    /// <inheritdoc cref="AutoFocusProperty"/>
    public bool AutoFocus { get => GetValue(AutoFocusProperty); set => SetValue(AutoFocusProperty, value); }

    /// <inheritdoc cref="ShowCloseButtonProperty"/>
    public bool ShowCloseButton { get => GetValue(ShowCloseButtonProperty); set => SetValue(ShowCloseButtonProperty, value); }

    /// <inheritdoc cref="IsShortcutEnabledProperty"/>
    public bool IsShortcutEnabled { get => GetValue(IsShortcutEnabledProperty); set => SetValue(IsShortcutEnabledProperty, value); }

    /// <inheritdoc cref="ShortcutHintProperty"/>
    public string? ShortcutHint { get => GetValue(ShortcutHintProperty); set => SetValue(ShortcutHintProperty, value); }

    /// <inheritdoc cref="EmptyTitleProperty"/>
    public string EmptyTitle { get => GetValue(EmptyTitleProperty); set => SetValue(EmptyTitleProperty, value); }

    /// <inheritdoc cref="EmptyDescriptionProperty"/>
    public string EmptyDescription { get => GetValue(EmptyDescriptionProperty); set => SetValue(EmptyDescriptionProperty, value); }

    /// <inheritdoc cref="DefaultGroupProperty"/>
    public string DefaultGroup { get => GetValue(DefaultGroupProperty); set => SetValue(DefaultGroupProperty, value); }

    /// <inheritdoc cref="ClearButtonLabelProperty"/>
    public string ClearButtonLabel { get => GetValue(ClearButtonLabelProperty); set => SetValue(ClearButtonLabelProperty, value); }

    /// <inheritdoc cref="CloseButtonLabelProperty"/>
    public string CloseButtonLabel { get => GetValue(CloseButtonLabelProperty); set => SetValue(CloseButtonLabelProperty, value); }

    /// <inheritdoc cref="ResultsLabelProperty"/>
    public string ResultsLabel { get => GetValue(ResultsLabelProperty); set => SetValue(ResultsLabelProperty, value); }

    /// <inheritdoc cref="SelectCommandProperty"/>
    public ICommand? SelectCommand { get => GetValue(SelectCommandProperty); set => SetValue(SelectCommandProperty, value); }

    /// <inheritdoc cref="ActiveItemProperty"/>
    public CommandPaletteItem? ActiveItem
    {
        get => _activeItem;
        private set => SetAndRaise(ActiveItemProperty, ref _activeItem, value);
    }

    internal string EffectiveShortcutHint
    {
        get => _effectiveShortcutHint;
        private set => SetAndRaise(EffectiveShortcutHintProperty, ref _effectiveShortcutHint, value);
    }

    /// <summary>
    /// Set by <see cref="CommandPaletteDialog"/> while it presents the palette: the dialog's presence fades the palette in
    /// and out, so the palette's own entrance only moves it (a second fade would square the curve) and the palette's own
    /// Cmd/Ctrl+K handler stays off (the dialog toggles).
    /// </summary>
    internal bool IsHosted
    {
        get => _hosted;
        set
        {
            if (_hosted == value)
            {
                return;
            }
            _hosted = value;
            ApplyEntrance();
            if (_hosted)
            {
                DetachShortcut();
            }
            else if (_attached)
            {
                AttachShortcut();
            }
        }
    }

    /// <summary>Inline commands (used when <see cref="ItemsSource"/> is null).</summary>
    [Content]
    public AvaloniaList<CommandPaletteItem> Items { get; } = new();

    /// <summary>The commands that match the query, in list order (grouped).</summary>
    public IReadOnlyList<CommandPaletteItem> VisibleItems => _visible;

    /// <summary>Index of <see cref="ActiveItem"/> in <see cref="VisibleItems"/>, or -1.</summary>
    public int ActiveIndex => _activeIndex;

    /// <inheritdoc cref="ItemSelectedEvent"/>
    public event EventHandler<CommandPaletteItemSelectedEventArgs>? ItemSelected
    {
        add => AddHandler(ItemSelectedEvent, value);
        remove => RemoveHandler(ItemSelectedEvent, value);
    }

    /// <inheritdoc cref="CloseRequestedEvent"/>
    public event EventHandler<CommandPaletteCloseRequestedEventArgs>? CloseRequested
    {
        add => AddHandler(CloseRequestedEvent, value);
        remove => RemoveHandler(CloseRequestedEvent, value);
    }

    #endregion

    private enum ResultsView
    {
        None,
        Results,
        Empty
    }

    private enum RefreshReason
    {
        Query,

        /// <summary>The commands changed in a way that needs every one of them to be looked at again (and watched again).</summary>
        Data,

        /// <summary>Commands were added; they are already watched.</summary>
        Added
    }

    private static readonly MotionSpec s_enter = MotionSpec.Tween(ArcMotionPresets.Standard, ArcEasings.Enter);
    private static readonly MotionSpec s_leave = MotionSpec.Tween(ArcMotionPresets.Instant, ArcEasings.Standard);

    private Border? _surface;
    private AvaloniaTextBox? _input;
    private AvaloniaButton? _clearButton;
    private AvaloniaButton? _closeButton;
    private ScrollViewer? _scroll;
    private Border? _results;
    private CommandPaletteHighlight? _highlight;
    private CommandPaletteList? _list;
    private Control? _empty;

    private readonly ScaleTransform _surfaceScale = new();
    private readonly TranslateTransform _surfaceTranslate = new();
    private readonly ScaleTransform _clearScale = new();
    private readonly TranslateTransform _emptyTranslate = new();
    private MotionValue<double>? _entrance;
    private MotionValue<double>? _entranceOpacity;
    private Visual? _entranceOwner;
    private MotionValue<double>? _clearPop;
    private MotionValue<double>? _clearOpacity;
    private MotionValue<double>? _listFade;
    private MotionValue<double>? _emptyOpacity;
    private MotionValue<double>? _emptyRise;
    private MotionValue<double>? _emptyBlur;

    private CommandPaletteItem? _activeItem;
    private string _effectiveShortcutHint = DefaultShortcutHint;
    private List<CommandPaletteItem> _visible = new();
    private List<CommandPaletteGroup> _groups = new();
    private int _activeIndex = -1;
    private CommandPaletteHighlightMove _activeMove;
    private bool _highlightDirty;
    private bool _scrollPending;
    private CommandPaletteItem? _pressed;
    private Point _lastPointer = new(double.NaN, double.NaN);

    private ResultsView _view;
    private bool _leaving;
    private int _viewGeneration;
    private bool _clearShown;
    private int _clearToken;
    private bool _entranceArmed;
    private bool _focusPending;
    private bool _attached;
    private bool _hosted;
    private TopLevel? _shortcutHost;
    private IDisposable? _clientSizeSubscription;
    private INotifyCollectionChanged? _watchedSource;
    private readonly HashSet<CommandPaletteItem> _watched = new();
    private readonly HashSet<CommandPaletteItem> _keep = new();
    private bool _syncingText;

    static CommandPalette()
    {
        FocusableProperty.OverrideDefaultValue<CommandPalette>(false);
    }

    /// <summary>Creates an empty palette.</summary>
    public CommandPalette()
    {
        Items.CollectionChanged += OnItemsChanged;
    }

    private static string DefaultShortcutHint => OperatingSystem.IsMacOS() || OperatingSystem.IsIOS() ? "⌘ K" : "Ctrl K";

    private IReadOnlyList<CommandPaletteItem> SourceItems => ItemsSource switch
    {
        null => Items,
        IReadOnlyList<CommandPaletteItem> list => list,
        { } items => items.ToList()
    };

    private ArcMotionLevel MotionLevel => IsLoaded ? ArcMotion.GetEffectiveLevel(this) : ArcMotionLevel.Off;

    #region Public methods

    /// <summary>Moves the keyboard focus into the search field.</summary>
    public void FocusInput()
    {
        if (_input is null || TopLevel.GetTopLevel(_input) is null)
        {
            _focusPending = true;
            return;
        }
        _focusPending = false;
        _input.Focus(NavigationMethod.Pointer);
    }

    /// <summary>Clears the query and the highlight.</summary>
    public void Clear()
    {
        SetCurrentValue(QueryProperty, string.Empty);
        if (_activeIndex >= 0)
        {
            _activeIndex = -1;
            UpdateActive(CommandPaletteHighlightMove.Jump, scroll: false);
        }
    }

    #endregion

    #region Template

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        DetachParts();
        base.OnApplyTemplate(e);
        _surface = e.NameScope.Find<Border>(SurfacePart);
        _input = e.NameScope.Find<AvaloniaTextBox>(InputPart);
        _clearButton = e.NameScope.Find<AvaloniaButton>(ClearButtonPart);
        _closeButton = e.NameScope.Find<AvaloniaButton>(CloseButtonPart);
        _scroll = e.NameScope.Find<ScrollViewer>(ScrollPart);
        _results = e.NameScope.Find<Border>(ResultsPart);
        _highlight = e.NameScope.Find<CommandPaletteHighlight>(HighlightPart);
        _list = e.NameScope.Find<CommandPaletteList>(ListPart);
        _empty = e.NameScope.Find<Control>(EmptyPart);

        if (_surface is not null)
        {
            _surface.RenderTransform = new TransformGroup { Children = { _surfaceScale, _surfaceTranslate } };
            ApplyEntrance();
        }
        if (_input is not null)
        {
            _input.AddHandler(KeyDownEvent, OnInputKeyDown, RoutingStrategies.Tunnel);
            _input.PropertyChanged += OnInputPropertyChanged;
            _syncingText = true;
            _input.Text = Query;
            _syncingText = false;
        }
        if (_clearButton is not null)
        {
            _clearButton.Click += OnClearClick;
            _clearButton.RenderTransform = _clearScale;
            _clearShown = Query.Length > 0;
            _clearButton.IsVisible = _clearShown;
            _clearPop = null;
            _clearOpacity = null;
        }
        if (_closeButton is not null)
        {
            _closeButton.Click += OnCloseClick;
        }
        if (_list is not null)
        {
            _list.Arranged += OnListArranged;
            _list.RowAutomationSelected = OnRowAutomationSelected;
            _list.PointerMoved += OnListPointerMoved;
            _list.PointerPressed += OnListPointerPressed;
            _list.PointerReleased += OnListPointerReleased;
            _list.PointerCaptureLost += OnListPointerCaptureLost;
        }
        if (_empty is not null)
        {
            _empty.RenderTransform = _emptyTranslate;
        }
        _listFade = null;
        _emptyOpacity = null;
        _emptyRise = null;
        _emptyBlur = null;
        _view = ResultsView.None;
        _leaving = false;
        UpdateAutomation();
        UpdateWidthClass(TopLevel.GetTopLevel(this)?.ClientSize.Width ?? 0);
        Refresh(RefreshReason.Data);
    }

    private void DetachParts()
    {
        if (_input is not null)
        {
            _input.RemoveHandler(KeyDownEvent, OnInputKeyDown);
            _input.PropertyChanged -= OnInputPropertyChanged;
        }
        if (_clearButton is not null)
        {
            _clearButton.Click -= OnClearClick;
        }
        if (_closeButton is not null)
        {
            _closeButton.Click -= OnCloseClick;
        }
        if (_list is not null)
        {
            _list.Arranged -= OnListArranged;
            _list.RowAutomationSelected = null;
            _list.PointerMoved -= OnListPointerMoved;
            _list.PointerPressed -= OnListPointerPressed;
            _list.PointerReleased -= OnListPointerReleased;
            _list.PointerCaptureLost -= OnListPointerCaptureLost;
        }
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == QueryProperty)
        {
            OnQueryChanged();
        }
        else if (change.Property == ItemsSourceProperty)
        {
            WatchSource();
            Refresh(RefreshReason.Data);
        }
        else if (change.Property == DefaultGroupProperty)
        {
            Refresh(RefreshReason.Data);
        }
        else if (change.Property == ShortcutHintProperty)
        {
            EffectiveShortcutHint = ShortcutHint ?? DefaultShortcutHint;
            PseudoClasses.Set(NoHintPseudoClass, EffectiveShortcutHint.Length == 0);
        }
        else if (change.Property == LabelProperty || change.Property == ClearButtonLabelProperty ||
                 change.Property == CloseButtonLabelProperty || change.Property == ResultsLabelProperty)
        {
            UpdateAutomation();
        }
    }

    private void UpdateAutomation()
    {
        if (_input is not null)
        {
            AutomationProperties.SetName(_input, Label);
        }
        if (_clearButton is not null)
        {
            AutomationProperties.SetName(_clearButton, ClearButtonLabel);
        }
        if (_closeButton is not null)
        {
            AutomationProperties.SetName(_closeButton, CloseButtonLabel);
        }
        if (_list is not null)
        {
            AutomationProperties.SetName(_list, ResultsLabel);
        }
    }

    #endregion

    #region Lifetime

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        _attached = true;
        AttachShortcut();
        AttachWidthClass();
        WatchSource();
        Refresh(RefreshReason.Data);
        ArmEntrance();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        _attached = false;
        DetachShortcut();
        _clientSizeSubscription?.Dispose();
        _clientSizeSubscription = null;
        WatchSource();
        WatchItems(Array.Empty<CommandPaletteItem>());
        _entranceArmed = false;
        _entrance?.Jump(1);
        _entranceOpacity?.Jump(1);
        ApplyEntrance();
    }

    protected override void OnLoaded(RoutedEventArgs e)
    {
        base.OnLoaded(e);
        PlayEntrance();
        if (AutoFocus || _focusPending)
        {
            FocusInput();
        }
    }

    /// <summary>Holds the entrance's starting pose from the first frame (Arc: initial opacity 0, y 6, scale .98).</summary>
    private void ArmEntrance()
    {
        var level = ArcMotion.GetEffectiveLevel(_surface ?? (AvaloniaObject)this);
        _entranceArmed = level != ArcMotionLevel.Off;
        ApplyEntrance();
    }

    /// <summary>
    /// The palette's mount: opacity 0→1 (fast, enter curve), y 6→0 and scale .98→1 on the smooth spring, from the top
    /// centre. Code-driven because it plays when the palette mounts, which ControlTheme transitions skip by design.
    /// Reduced motion keeps only the fade. The values belong to the surface, a plain element that reads the theme's
    /// motion mode directly (the palette's own tracked level arrives a moment after it is loaded).
    /// </summary>
    private void PlayEntrance()
    {
        if (!_entranceArmed || _surface is null)
        {
            return;
        }
        if (_entrance is null || !ReferenceEquals(_entranceOwner, _surface))
        {
            _entranceOwner = _surface;
            _entrance = new MotionValue<double>(_surface, 0, MotionVectors.Double);
            _entranceOpacity = new MotionValue<double>(_surface, 0, MotionVectors.Double);
            _entrance.Changed += _ => ApplyEntrance();
            _entranceOpacity.Changed += _ => ApplyEntrance();
        }
        _entranceArmed = false;
        var level = ArcMotion.GetEffectiveLevel(_surface);
        _entrance.Jump(level == ArcMotionLevel.Full ? 0 : 1);
        _entranceOpacity!.Jump(level == ArcMotionLevel.Off ? 1 : 0);
        if (level == ArcMotionLevel.Full)
        {
            _ = _entrance.AnimateTo(1, MotionSpec.Spring(ArcSpring.Smooth));
        }
        if (_hosted)
        {
            _entranceOpacity.Jump(1);
            return;
        }
        _ = Animate(_entranceOpacity, 1, MotionSpec.Tween(ArcMotionPresets.Fast, ArcEasings.Enter), level);
    }

    private void ApplyEntrance()
    {
        if (_surface is null)
        {
            return;
        }
        double p;
        double opacity;
        if (_entranceArmed)
        {
            // Armed, not started yet: the starting pose (reduced motion only fades).
            p = ArcMotion.GetEffectiveLevel(_surface) == ArcMotionLevel.Full ? 0 : 1;
            opacity = 0;
        }
        else
        {
            p = _entrance?.Value ?? 1;
            opacity = _entranceOpacity?.Value ?? 1;
        }
        var scale = 0.98 + 0.02 * p;
        _surfaceScale.ScaleX = scale;
        _surfaceScale.ScaleY = scale;
        _surfaceTranslate.Y = 6 * (1 - p);
        _surface.Opacity = _hosted ? 1 : Math.Clamp(opacity, 0, 1);
    }

    #endregion

    #region Data

    private void OnItemsChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (ItemsSource is null)
        {
            OnCollectionChanged(e);
        }
    }

    private void OnSourceCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e) => OnCollectionChanged(e);

    private void OnCollectionChanged(NotifyCollectionChangedEventArgs e)
    {
        // Adding commands one by one (a long list filled in a loop) watches only the new ones instead of every command again.
        if (e.Action == NotifyCollectionChangedAction.Add && e.NewItems is { } added)
        {
            if (_attached)
            {
                foreach (var item in added)
                {
                    if (item is CommandPaletteItem command && _watched.Add(command))
                    {
                        command.PropertyChanged += OnItemPropertyChanged;
                    }
                }
            }
            Refresh(RefreshReason.Added);
            return;
        }
        Refresh(RefreshReason.Data);
    }

    private void WatchSource()
    {
        var next = _attached ? ItemsSource as INotifyCollectionChanged : null;
        if (ReferenceEquals(next, _watchedSource))
        {
            return;
        }
        if (_watchedSource is not null)
        {
            _watchedSource.CollectionChanged -= OnSourceCollectionChanged;
        }
        _watchedSource = next;
        if (_watchedSource is not null)
        {
            _watchedSource.CollectionChanged += OnSourceCollectionChanged;
        }
    }

    /// <summary>Follows property changes of the listed commands while the palette is in a window (no leaks from long-lived items).</summary>
    private void WatchItems(IReadOnlyList<CommandPaletteItem> source)
    {
        var keep = _keep;
        keep.Clear();
        if (_attached)
        {
            foreach (var item in source)
            {
                if (item is not null)
                {
                    keep.Add(item);
                }
            }
        }
        List<CommandPaletteItem>? gone = null;
        foreach (var item in _watched)
        {
            if (!keep.Contains(item))
            {
                (gone ??= new()).Add(item);
            }
        }
        if (gone is not null)
        {
            foreach (var item in gone)
            {
                item.PropertyChanged -= OnItemPropertyChanged;
                _watched.Remove(item);
            }
        }
        foreach (var item in keep)
        {
            if (_watched.Add(item))
            {
                item.PropertyChanged += OnItemPropertyChanged;
            }
        }
    }

    private void OnItemPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (e.Property == CommandPaletteItem.TagProperty || e.Property == CommandPaletteItem.CommandProperty ||
            e.Property == CommandPaletteItem.CommandParameterProperty)
        {
            return;
        }
        Refresh(RefreshReason.Data);
    }

    private void OnQueryChanged()
    {
        var text = Query;
        if (_input is not null && _input.Text != text)
        {
            _syncingText = true;
            _input.Text = text;
            _syncingText = false;
        }
        PseudoClasses.Set(HasQueryPseudoClass, text.Length > 0);
        UpdateClearButton();
        Refresh(RefreshReason.Query);
    }

    private void Refresh(RefreshReason reason)
    {
        var source = SourceItems;
        if (reason == RefreshReason.Data)
        {
            WatchItems(source);
        }
        var groups = CommandPaletteFilter.Group(source, CommandPaletteFilter.Normalize(Query), DefaultGroup);
        var visible = new List<CommandPaletteItem>();
        foreach (var group in groups)
        {
            visible.AddRange(group.Items);
        }
        var glide = CommandPaletteFilter.ShouldGlide(_visible, visible);
        var previousActive = _activeIndex >= 0 && _activeIndex < _visible.Count ? _visible[_activeIndex] : null;
        _visible = visible;
        _groups = groups;
        if (reason == RefreshReason.Query)
        {
            // Typing starts over: nothing is highlighted until an arrow key or the pointer picks a row.
            _activeIndex = -1;
        }
        else if (_activeIndex >= 0)
        {
            var kept = previousActive is null ? -1 : visible.IndexOf(previousActive);
            _activeIndex = kept >= 0 ? kept : Math.Min(_activeIndex, visible.Count - 1);
            if (_activeIndex >= 0 && !visible[_activeIndex].IsEnabled)
            {
                _activeIndex = -1;
            }
        }
        ApplyView(glide);
        UpdateActive(reason != RefreshReason.Query ? CommandPaletteHighlightMove.Glide : CommandPaletteHighlightMove.Jump, scroll: false);
    }

    #endregion

    #region Results and empty state

    /// <summary>
    /// Shows the results or the empty state. Like Arc's <c>AnimatePresence mode="wait"</c>, the leaving view fades out
    /// first and only then the other one enters, so the frame makes one height change instead of two; if the query comes
    /// back to the leaving view before it is gone, it returns from where it is.
    /// </summary>
    private void ApplyView(bool glide)
    {
        if (_list is null || _empty is null)
        {
            return;
        }
        var level = MotionLevel;
        var desired = _visible.Count > 0 ? ResultsView.Results : ResultsView.Empty;
        if (_view == ResultsView.None)
        {
            ShowView(desired, ArcMotionLevel.Off);
            return;
        }
        if (desired == _view)
        {
            if (_leaving)
            {
                _leaving = false;
                _viewGeneration++;
                ReturnView(level);
            }
            if (desired == ResultsView.Results)
            {
                _list.Update(_groups, glide && level == ArcMotionLevel.Full, fadeIn: level != ArcMotionLevel.Off);
            }
            return;
        }
        if (_leaving)
        {
            return;
        }
        _leaving = true;
        _ = LeaveViewAsync(++_viewGeneration, level);
    }

    private async Task LeaveViewAsync(int generation, ArcMotionLevel level)
    {
        EnsureViewValues();
        if (_view == ResultsView.Results)
        {
            await Animate(_listFade!, 0, s_leave, level);
        }
        else
        {
            var rise = level == ArcMotionLevel.Full ? Animate(_emptyRise!, -4, s_leave, level) : Task.FromResult(true);
            await Animate(_emptyOpacity!, 0, s_leave, level);
            await rise;
        }
        if (generation != _viewGeneration || !_leaving)
        {
            return;
        }
        _leaving = false;
        ShowView(_visible.Count > 0 ? ResultsView.Results : ResultsView.Empty, MotionLevel);
        UpdateActive(CommandPaletteHighlightMove.Jump, scroll: false);
    }

    private void ReturnView(ArcMotionLevel level)
    {
        EnsureViewValues();
        if (_view == ResultsView.Results)
        {
            _ = Animate(_listFade!, 1, s_enter, level);
        }
        else
        {
            _ = Animate(_emptyOpacity!, 1, s_enter, level);
            _ = Animate(_emptyRise!, 0, s_enter, level);
        }
    }

    private void ShowView(ResultsView view, ArcMotionLevel level)
    {
        EnsureViewValues();
        _view = view;
        if (view == ResultsView.Results)
        {
            _empty!.IsVisible = false;
            _list!.IsVisible = true;
            _list.Clear();
            _list.Update(_groups, glide: false, fadeIn: false);
            if (level == ArcMotionLevel.Off)
            {
                _listFade!.Jump(1);
            }
            else
            {
                _listFade!.Jump(0);
                _ = _listFade.AnimateTo(1, s_enter);
            }
            return;
        }
        _list!.Clear();
        _list.IsVisible = false;
        _empty!.IsVisible = true;
        _highlight?.Hide();
        if (level == ArcMotionLevel.Off)
        {
            _emptyOpacity!.Jump(1);
            _emptyRise!.Jump(0);
            _emptyBlur!.Jump(0);
            return;
        }
        // Arc: opacity, y 6 and a 2px blur, on the standard duration and the enter curve; reduced motion only fades.
        var full = level == ArcMotionLevel.Full;
        _emptyOpacity!.Jump(0);
        _emptyRise!.Jump(full ? 6 : 0);
        _emptyBlur!.Jump(full ? 2 : 0);
        _ = _emptyOpacity.AnimateTo(1, s_enter);
        _ = _emptyRise.AnimateTo(0, s_enter);
        _ = _emptyBlur.AnimateTo(0, s_enter);
    }

    private static Task<bool> Animate(MotionValue<double> value, double target, MotionSpec spec, ArcMotionLevel level)
    {
        if (level == ArcMotionLevel.Off)
        {
            value.Jump(target);
            return Task.FromResult(true);
        }
        return value.AnimateTo(target, spec);
    }

    private void EnsureViewValues()
    {
        if (_listFade is not null)
        {
            return;
        }
        _listFade = new MotionValue<double>(this, 1, MotionVectors.Double);
        _listFade.Changed += opacity =>
        {
            if (_list is not null)
            {
                _list.Opacity = Math.Clamp(opacity, 0, 1);
            }
        };
        _emptyOpacity = new MotionValue<double>(this, 1, MotionVectors.Double);
        _emptyRise = new MotionValue<double>(this, 0, MotionVectors.Double);
        _emptyBlur = new MotionValue<double>(this, 0, MotionVectors.Double);
        _emptyOpacity.Changed += _ => ApplyEmpty();
        _emptyRise.Changed += _ => ApplyEmpty();
        _emptyBlur.Changed += _ => ApplyEmpty();
    }

    private void ApplyEmpty()
    {
        if (_empty is null)
        {
            return;
        }
        _empty.Opacity = Math.Clamp(_emptyOpacity!.Value, 0, 1);
        _emptyTranslate.Y = _emptyRise!.Value;
        _empty.Effect = ArcBlur.Get(_emptyBlur!.Value);
    }

    #endregion

    #region Highlight

    private void SetActiveIndex(int index, CommandPaletteHighlightMove move)
    {
        if (index == _activeIndex)
        {
            return;
        }
        _activeIndex = index;
        UpdateActive(move, scroll: true);
    }

    private void UpdateActive(CommandPaletteHighlightMove move, bool scroll)
    {
        ActiveItem = _activeIndex >= 0 && _activeIndex < _visible.Count ? _visible[_activeIndex] : null;
        if (_input is not null)
        {
            AutomationProperties.SetHelpText(_input, ActiveItem?.Label);
        }
        _activeMove = move;
        _scrollPending = scroll;
        _highlightDirty = true;
        if (_list is { IsVisible: true, IsMeasureValid: false } or { IsVisible: true, IsArrangeValid: false })
        {
            // The list is about to lay out (new rows, new positions): the highlight is placed when it has arranged.
            _list.InvalidateArrange();
        }
        else
        {
            // Positions are final, so moving the highlight needs no layout pass at all.
            PlaceHighlight();
        }
    }

    private void OnListArranged() => PlaceHighlight();

    /// <summary>
    /// One highlight follows the active row: a snappy spring for the pointer, a 70ms slide for keys, a fade when it
    /// first appears, a fade out when nothing is active. It aims at the row's layout slot (never its glide offset).
    /// </summary>
    private void PlaceHighlight()
    {
        if (_highlight is null || _list is null)
        {
            return;
        }
        var entry = ActiveItem is { } item && _view == ResultsView.Results && !_leaving ? _list.FindRow(item) : null;
        _list.SetActiveItem(entry?.Item);
        if (entry is null || double.IsNaN(entry.Y))
        {
            _highlightDirty = false;
            if (_highlight.IsShown)
            {
                _highlight.Hide();
            }
            return;
        }
        var top = entry.Y;
        var height = entry.Height;
        if (_highlightDirty)
        {
            _highlightDirty = false;
            _highlight.Show(top, height, _activeMove);
            if (_scrollPending)
            {
                _scrollPending = false;
                if (_list.IsMeasureValid && _list.IsArrangeValid)
                {
                    // Positions are final (an arrow key or the pointer): scroll in the same frame, so the highlight never
                    // sits outside the viewport for a frame.
                    ScrollActiveIntoView();
                }
                else
                {
                    // Inside a layout pass (the data changed): scroll once it is over.
                    Dispatcher.UIThread.Post(ScrollActiveIntoView, DispatcherPriority.Background);
                }
            }
        }
        else if (Math.Abs(_highlight.TargetTop - top) > 0.5)
        {
            _highlight.Show(top, height, CommandPaletteHighlightMove.Glide);
        }
    }

    /// <summary>Scrolls the active row into view by the shortest distance (Arc's <c>scrollIntoView({ block: "nearest" })</c>).</summary>
    private void ScrollActiveIntoView()
    {
        if (_scroll is null || _list is null || _results is null || ActiveItem is not { } item || _list.FindRow(item) is not { } entry)
        {
            return;
        }
        if (_list.TranslatePoint(new Point(0, entry.Y), _results) is not { } origin)
        {
            return;
        }
        var top = origin.Y;
        var bottom = top + entry.Height;
        var offset = _scroll.Offset.Y;
        var viewport = _scroll.Viewport.Height;
        double next;
        if (top < offset)
        {
            next = top;
        }
        else if (bottom > offset + viewport)
        {
            next = bottom - viewport;
        }
        else
        {
            return;
        }
        _scroll.Offset = new Vector(_scroll.Offset.X, Math.Max(0, next));
    }

    #endregion

    #region Input

    private void OnInputPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (e.Property != AvaloniaTextBox.TextProperty || _syncingText)
        {
            return;
        }
        var text = _input?.Text ?? string.Empty;
        if (Query != text)
        {
            SetCurrentValue(QueryProperty, text);
        }
    }

    private void OnInputKeyDown(object? sender, KeyEventArgs e)
    {
        switch (e.Key)
        {
            case Key.Down:
                e.Handled = true;
                MoveActive(_activeIndex < 0 ? 0 : _activeIndex + 1, 1);
                break;
            case Key.Up:
                e.Handled = true;
                MoveActive(_activeIndex < 0 ? _visible.Count - 1 : _activeIndex - 1, -1);
                break;
            case Key.Home:
                e.Handled = true;
                MoveActive(0, 1);
                break;
            case Key.End:
                e.Handled = true;
                MoveActive(_visible.Count - 1, -1);
                break;
            case Key.Enter:
            {
                // Enter runs the highlighted command; after typing, with nothing highlighted yet, it runs the top match.
                var target = ActiveItem ?? (Query.Length > 0 ? FirstEnabled() : null);
                if (target is { IsEnabled: true })
                {
                    e.Handled = true;
                    Choose(target);
                }
                break;
            }
            case Key.Escape:
                if (Query.Length > 0)
                {
                    e.Handled = true;
                    Clear();
                }
                else
                {
                    // Left unhandled so an overlay around the palette can close on Escape too.
                    RaiseEvent(new CommandPaletteCloseRequestedEventArgs(CloseRequestedEvent, CommandPaletteCloseReason.Escape));
                }
                break;
        }
    }

    private CommandPaletteItem? FirstEnabled()
    {
        foreach (var item in _visible)
        {
            if (item.IsEnabled)
            {
                return item;
            }
        }
        return null;
    }

    /// <summary>
    /// Moves to the first enabled command from <paramref name="start"/> in <paramref name="step"/> direction; past the
    /// last one the highlight stays where it is (Arc clamps instead of wrapping).
    /// </summary>
    private void MoveActive(int start, int step)
    {
        for (var i = start; i >= 0 && i < _visible.Count; i += step)
        {
            if (_visible[i].IsEnabled)
            {
                SetActiveIndex(i, CommandPaletteHighlightMove.Keyboard);
                return;
            }
        }
    }

    private void OnClearClick(object? sender, RoutedEventArgs e)
    {
        e.Handled = true;
        Clear();
        FocusInput();
    }

    private void OnCloseClick(object? sender, RoutedEventArgs e)
    {
        e.Handled = true;
        RaiseEvent(new CommandPaletteCloseRequestedEventArgs(CloseRequestedEvent, CommandPaletteCloseReason.CloseButton));
    }

    private void Choose(CommandPaletteItem item)
    {
        if (!item.IsEnabled)
        {
            return;
        }
        _pressed = null;
        // The field is reset and refocused first, so a handler that closes the palette (and returns the focus to its
        // opener) has the last word.
        Clear();
        FocusInput();
        RaiseEvent(new CommandPaletteItemSelectedEventArgs(ItemSelectedEvent, item));
        Execute(SelectCommand, item);
        Execute(item.Command, item.CommandParameter);
    }

    private static void Execute(ICommand? command, object? parameter)
    {
        if (command is not null && command.CanExecute(parameter))
        {
            command.Execute(parameter);
        }
    }

    private void OnRowAutomationSelected(CommandPaletteRow row)
    {
        if (row.Item is { IsEnabled: true } item && _list?.FindRow(item) is { } entry)
        {
            SetActiveIndex(entry.ItemIndex, CommandPaletteHighlightMove.Keyboard);
        }
    }

    #endregion

    #region Pointer

    private void OnListPointerMoved(object? sender, PointerEventArgs e)
    {
        if (_list is null)
        {
            return;
        }
        // Ignore moves that only come from the list scrolling under a still pointer.
        var position = e.GetPosition(null);
        if (position == _lastPointer)
        {
            return;
        }
        _lastPointer = position;
        if (_list.HitRow(e.GetPosition(_list)) is not { Item: { IsEnabled: true } } hit)
        {
            return;
        }
        SetActiveIndex(hit.ItemIndex, CommandPaletteHighlightMove.Pointer);
    }

    private void OnListPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (_list is null || !e.GetCurrentPoint(_list).Properties.IsLeftButtonPressed)
        {
            return;
        }
        _pressed = _list.HitRow(e.GetPosition(_list))?.Item;
        if (_pressed is not null)
        {
            // Pressing a row keeps the focus in the field.
            e.Handled = true;
        }
    }

    private void OnListPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        var pressed = _pressed;
        _pressed = null;
        if (_list is null || pressed is null || e.InitialPressMouseButton != MouseButton.Left)
        {
            return;
        }
        if (ReferenceEquals(_list.HitRow(e.GetPosition(_list))?.Item, pressed))
        {
            e.Handled = true;
            Choose(pressed);
        }
    }

    private void OnListPointerCaptureLost(object? sender, PointerCaptureLostEventArgs e) => _pressed = null;

    #endregion

    #region Clear button

    /// <summary>
    /// The clear button pops in from scale .6 out of a 2px blur on the snappy spring (opacity on the instant tween) and
    /// leaves the same way on the instant tween. Code-driven because enter and exit use different curves per property
    /// and the button must stay until its exit ends, which a ControlTheme transition cannot express.
    /// </summary>
    private void UpdateClearButton()
    {
        var show = Query.Length > 0;
        if (show == _clearShown || _clearButton is null)
        {
            _clearShown = show;
            return;
        }
        _clearShown = show;
        _clearPop ??= CreateClearValue(show ? 0 : 1);
        _clearOpacity ??= CreateClearValue(show ? 0 : 1);
        var level = MotionLevel;
        var token = ++_clearToken;
        if (show)
        {
            _clearButton.IsVisible = true;
            if (level == ArcMotionLevel.Off)
            {
                _clearPop.Jump(1);
                _clearOpacity.Jump(1);
                return;
            }
            if (level == ArcMotionLevel.Full)
            {
                _ = _clearPop.AnimateTo(1, MotionSpec.Spring(ArcSpring.Snappy));
            }
            else
            {
                _clearPop.Jump(1);
            }
            _ = _clearOpacity.AnimateTo(1, s_leave);
            return;
        }
        if (level == ArcMotionLevel.Off)
        {
            _clearPop.Jump(0);
            _clearOpacity.Jump(0);
            _clearButton.IsVisible = false;
            return;
        }
        if (level == ArcMotionLevel.Full)
        {
            _ = _clearPop.AnimateTo(0, s_leave);
        }
        _ = HideClearWhenDone(token, _clearOpacity.AnimateTo(0, s_leave));
    }

    private async Task HideClearWhenDone(int token, Task<bool> exit)
    {
        await exit;
        if (token == _clearToken && !_clearShown && _clearButton is not null)
        {
            _clearButton.IsVisible = false;
        }
    }

    private MotionValue<double> CreateClearValue(double initial)
    {
        var value = new MotionValue<double>(this, initial, MotionVectors.Double);
        value.Changed += _ => ApplyClear();
        return value;
    }

    private void ApplyClear()
    {
        if (_clearButton is null || _clearPop is null || _clearOpacity is null)
        {
            return;
        }
        var p = _clearPop.Value;
        var scale = 0.6 + 0.4 * p;
        _clearScale.ScaleX = scale;
        _clearScale.ScaleY = scale;
        _clearButton.Effect = ArcBlur.Get(2 * Math.Clamp(1 - p, 0, 1));
        _clearButton.Opacity = Math.Clamp(_clearOpacity.Value, 0, 1);
    }

    #endregion

    #region Shortcut and size

    private void AttachShortcut()
    {
        if (_hosted)
        {
            return;
        }
        var top = TopLevel.GetTopLevel(this);
        if (ReferenceEquals(top, _shortcutHost))
        {
            return;
        }
        DetachShortcut();
        _shortcutHost = top;
        // Tunnel: the palette answers Cmd/Ctrl+K before the window and its other controls see it.
        _shortcutHost?.AddHandler(KeyDownEvent, OnTopLevelKeyDown, RoutingStrategies.Tunnel);
    }

    private void DetachShortcut()
    {
        _shortcutHost?.RemoveHandler(KeyDownEvent, OnTopLevelKeyDown);
        _shortcutHost = null;
    }

    private void OnTopLevelKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Handled || e.Key != Key.K || (e.KeyModifiers != KeyModifiers.Meta && e.KeyModifiers != KeyModifiers.Control) ||
            !IsShortcutEnabled || !IsEffectivelyVisible || !IsEffectivelyEnabled || _input is null)
        {
            return;
        }
        // While a modal that does not contain this palette is on top (a dialog, a palette dialog), the page behind it
        // does not answer the shortcut.
        if (_shortcutHost is { } top && TopModal(top) is { } modal && !modal.Contains(this))
        {
            return;
        }
        // When another palette holds the focus (one opened in an overlay), that one answers for itself.
        if (_shortcutHost?.FocusManager?.GetFocusedElement() is Visual focused &&
            focused.FindAncestorOfType<CommandPalette>(includeSelf: true) is { } owner &&
            !ReferenceEquals(owner, this) && owner.IsShortcutEnabled)
        {
            return;
        }
        e.Handled = true;
        FocusInput();
    }

    /// <summary>The topmost open modal. A presence that does not trap focus (a non-modal palette) is a popup, not a modal, and does not count.</summary>
    private static ModalPresence? TopModal(TopLevel topLevel)
    {
        var modals = ModalPresence.GetModals(topLevel);
        for (var i = modals.Count - 1; i >= 0; i--)
        {
            if (modals[i].IsOpen && modals[i].TrapFocus)
            {
                return modals[i];
            }
        }
        return null;
    }

    /// <summary>
    /// Arc's narrow layouts are <c>@media (max-width: 480px)</c> rules: they follow the window, not the palette, so a
    /// popup 440px wide in a desktop window keeps its shortcut caps (and a palette in a side panel does too).
    /// </summary>
    private void AttachWidthClass()
    {
        _clientSizeSubscription?.Dispose();
        _clientSizeSubscription = null;
        if (TopLevel.GetTopLevel(this) is { } top)
        {
            _clientSizeSubscription = top.GetObservable(TopLevel.ClientSizeProperty).Subscribe(new ClientSizeObserver(this));
            UpdateWidthClass(top.ClientSize.Width);
        }
    }

    private sealed class ClientSizeObserver(CommandPalette owner) : IObserver<Size>
    {
        public void OnCompleted()
        {
        }

        public void OnError(Exception error)
        {
        }

        public void OnNext(Size value) => owner.UpdateWidthClass(value.Width);
    }

    private void UpdateWidthClass(double width)
    {
        var narrow = width > 0 && width <= NarrowWidth;
        var narrowest = width > 0 && width <= NarrowestWidth;
        PseudoClasses.Set(NarrowPseudoClass, narrow);
        PseudoClasses.Set(NarrowestPseudoClass, narrowest);
        _list?.SetWidthClass(narrow, narrowest);
    }

    #endregion
}
