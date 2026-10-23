using System.Collections.Specialized;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives.PopupPositioning;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;

namespace TKCode.Arc;

/// <summary>Arguments of <see cref="AiRegenerateMenu.ChosenEvent"/>: how to answer again, and with which model.</summary>
internal sealed class AiRegenerateChosenEventArgs : RoutedEventArgs
{
    public AiRegenerateChosenEventArgs(RoutedEvent routedEvent, object source, AiAnswerMode mode, AiModel? model)
        : base(routedEvent, source)
    {
        Mode = mode;
        Model = model;
    }

    /// <summary><see cref="AiAnswerMode.Retry"/>, <see cref="AiAnswerMode.Concise"/>, <see cref="AiAnswerMode.Detailed"/> or <see cref="AiAnswerMode.SwitchModel"/>.</summary>
    public AiAnswerMode Mode { get; }

    /// <summary>The model chosen in the list (for <see cref="AiAnswerMode.SwitchModel"/>), otherwise null.</summary>
    public AiModel? Model { get; }
}

/// <summary>
/// The menu of "Answer again" under an AI answer (Arc answer): Try again, More concise, More detailed, a hairline, then
/// "Switch model" opening a list headed "Answer again with" with every model of <see cref="Models"/> (the owner leaves the
/// current one out) and its mark. With no models the hairline and "Switch model" are left out. It renders nothing in place:
/// it opens against <see cref="PlacementTarget"/>, above it with left edges aligned, 6px away (below when there is no room),
/// and the list opens against the menu's right edge (left when there is no room), its top 5px above the row.
/// <para>
/// A pointer opens it with nothing highlighted; the keyboard (<c>Open(keyboard: true)</c>) highlights the first row. One
/// highlight per level follows the pointer and the keys. Keys: ↑ ↓ (wrapping), Home, End, letters jump to the next row
/// starting with them, Enter or Space choose, → (or Enter, Space, a click, hovering) on "Switch model" opens the list
/// (the keyboard lands on its first model), ← closes the list back to "Switch model", Escape closes everything and gives
/// focus back to the target with its keyboard ring, Tab does nothing. A press outside closes everything. Choosing closes
/// the menu, gives focus back to the target (with its ring when chosen by keyboard), then raises <see cref="Chosen"/>.
/// </para>
/// </summary>
internal sealed class AiRegenerateMenu : Control
{
    /// <summary>The menu sits this far from its target.</summary>
    internal const double MenuGap = 6;

    /// <summary>The list of models starts this far above its row's top (its first line lines up with the row).</summary>
    internal const double SubmenuOffset = -5;

    /// <summary>How far a menu starts toward its target while entering.</summary>
    internal const double MenuTravel = 5;

    /// <summary>How far the list of models starts toward the menu while entering.</summary>
    internal const double SubmenuTravel = 6;

    /// <summary>Letters typed within this time make one typeahead prefix.</summary>
    internal static readonly TimeSpan TypeaheadWindow = TimeSpan.FromMilliseconds(500);

    // Lucide arrow-left-right (not in the Core subset); its four paths joined, the third one's relative start made absolute.
    private static readonly Avalonia.Media.Geometry s_switchGlyph = ArcIcons.Parse("M8 3 4 7l4 4 M4 7h16 M16 21l4-4-4-4 M20 17H4");

    #region Properties

    /// <summary>The control the menu opens against (the "Answer again" button).</summary>
    public static readonly StyledProperty<Control?> PlacementTargetProperty =
        AvaloniaProperty.Register<AiRegenerateMenu, Control?>(nameof(PlacementTarget));

    /// <summary>The models of "Switch model" (the owner leaves the current one out). Collection changes are followed while open.</summary>
    public static readonly StyledProperty<IEnumerable<AiModel>?> ModelsProperty =
        AvaloniaProperty.Register<AiRegenerateMenu, IEnumerable<AiModel>?>(nameof(Models));

    /// <summary>The providers, for the marks of models without their own.</summary>
    public static readonly StyledProperty<IEnumerable<AiModelProvider>?> ProvidersProperty =
        AvaloniaProperty.Register<AiRegenerateMenu, IEnumerable<AiModelProvider>?>(nameof(Providers));

    /// <summary>The first row ("Try again").</summary>
    public static readonly StyledProperty<string?> TryAgainTextProperty =
        AvaloniaProperty.Register<AiRegenerateMenu, string?>(nameof(TryAgainText));

    /// <summary>The second row ("More concise").</summary>
    public static readonly StyledProperty<string?> MoreConciseTextProperty =
        AvaloniaProperty.Register<AiRegenerateMenu, string?>(nameof(MoreConciseText));

    /// <summary>The third row ("More detailed").</summary>
    public static readonly StyledProperty<string?> MoreDetailedTextProperty =
        AvaloniaProperty.Register<AiRegenerateMenu, string?>(nameof(MoreDetailedText));

    /// <summary>The row that opens the list of models ("Switch model").</summary>
    public static readonly StyledProperty<string?> SwitchModelTextProperty =
        AvaloniaProperty.Register<AiRegenerateMenu, string?>(nameof(SwitchModelText));

    /// <summary>The heading and accessible name of the list of models ("Answer again with").</summary>
    public static readonly StyledProperty<string?> AnswerAgainWithTextProperty =
        AvaloniaProperty.Register<AiRegenerateMenu, string?>(nameof(AnswerAgainWithText));

    /// <summary>The accessible name of the menu (the button's name, "Answer again").</summary>
    public static readonly StyledProperty<string?> MenuNameProperty =
        AvaloniaProperty.Register<AiRegenerateMenu, string?>(nameof(MenuName));

    /// <summary>Whether the menu is open (read-only).</summary>
    public static readonly DirectProperty<AiRegenerateMenu, bool> IsOpenProperty =
        AvaloniaProperty.RegisterDirect<AiRegenerateMenu, bool>(nameof(IsOpen), o => o.IsOpen);

    /// <summary>Whether the list of models is open (read-only).</summary>
    public static readonly DirectProperty<AiRegenerateMenu, bool> IsSubmenuOpenProperty =
        AvaloniaProperty.RegisterDirect<AiRegenerateMenu, bool>(nameof(IsSubmenuOpen), o => o.IsSubmenuOpen);

    /// <summary>Raised (bubbling) after a choice closed the menu.</summary>
    public static readonly RoutedEvent<AiRegenerateChosenEventArgs> ChosenEvent =
        RoutedEvent.Register<AiRegenerateMenu, AiRegenerateChosenEventArgs>(nameof(Chosen), RoutingStrategies.Bubble);

    /// <inheritdoc cref="PlacementTargetProperty"/>
    public Control? PlacementTarget { get => GetValue(PlacementTargetProperty); set => SetValue(PlacementTargetProperty, value); }

    /// <inheritdoc cref="ModelsProperty"/>
    public IEnumerable<AiModel>? Models { get => GetValue(ModelsProperty); set => SetValue(ModelsProperty, value); }

    /// <inheritdoc cref="ProvidersProperty"/>
    public IEnumerable<AiModelProvider>? Providers { get => GetValue(ProvidersProperty); set => SetValue(ProvidersProperty, value); }

    /// <inheritdoc cref="TryAgainTextProperty"/>
    public string? TryAgainText { get => GetValue(TryAgainTextProperty); set => SetValue(TryAgainTextProperty, value); }

    /// <inheritdoc cref="MoreConciseTextProperty"/>
    public string? MoreConciseText { get => GetValue(MoreConciseTextProperty); set => SetValue(MoreConciseTextProperty, value); }

    /// <inheritdoc cref="MoreDetailedTextProperty"/>
    public string? MoreDetailedText { get => GetValue(MoreDetailedTextProperty); set => SetValue(MoreDetailedTextProperty, value); }

    /// <inheritdoc cref="SwitchModelTextProperty"/>
    public string? SwitchModelText { get => GetValue(SwitchModelTextProperty); set => SetValue(SwitchModelTextProperty, value); }

    /// <inheritdoc cref="AnswerAgainWithTextProperty"/>
    public string? AnswerAgainWithText { get => GetValue(AnswerAgainWithTextProperty); set => SetValue(AnswerAgainWithTextProperty, value); }

    /// <inheritdoc cref="MenuNameProperty"/>
    public string? MenuName { get => GetValue(MenuNameProperty); set => SetValue(MenuNameProperty, value); }

    /// <inheritdoc cref="IsOpenProperty"/>
    public bool IsOpen { get => _isOpen; private set => SetAndRaise(IsOpenProperty, ref _isOpen, value); }

    /// <inheritdoc cref="IsSubmenuOpenProperty"/>
    public bool IsSubmenuOpen { get => _isSubmenuOpen; private set => SetAndRaise(IsSubmenuOpenProperty, ref _isSubmenuOpen, value); }

    /// <inheritdoc cref="ChosenEvent"/>
    public event EventHandler<AiRegenerateChosenEventArgs>? Chosen { add => AddHandler(ChosenEvent, value); remove => RemoveHandler(ChosenEvent, value); }

    #endregion

    private readonly List<AiRegenerateMenuRow> _rows = new();
    private readonly List<AiRegenerateMenuRow> _subRows = new();
    private AiRegenerateMenuPanel? _panel;
    private AiRegenerateMenuPanel? _subPanel;
    private AiActionsPopup? _popup;
    private AiActionsPopup? _subPopup;
    private AiActionsPopupPresence? _presence;
    private AiActionsPopupPresence? _subPresence;
    private AiRegenerateMenuRow? _highlighted;
    private AiRegenerateMenuRow? _subHighlighted;
    private TopLevel? _hooked;
    private INotifyCollectionChanged? _observedModels;
    private string _typeahead = string.Empty;
    private DateTime _lastTyped = DateTime.MinValue;
    private bool _isOpen;
    private bool _isSubmenuOpen;

    static AiRegenerateMenu()
    {
        IsHitTestVisibleProperty.OverrideDefaultValue<AiRegenerateMenu>(false);
        FocusableProperty.OverrideDefaultValue<AiRegenerateMenu>(false);
    }

    /// <summary>The clock of typeahead (tests replace it).</summary>
    internal Func<DateTime> Now { get; set; } = static () => DateTime.UtcNow;

    /// <summary>The menu surface (null until the first open).</summary>
    internal AiRegenerateMenuPanel? MenuPanel => _panel;

    /// <summary>The surface of the list of models (null until it first opens).</summary>
    internal AiRegenerateMenuPanel? SubmenuPanel => _subPanel;

    /// <summary>The popup of the menu.</summary>
    internal AiActionsPopup? MenuPopup => _popup;

    /// <summary>The popup of the list of models.</summary>
    internal AiActionsPopup? SubmenuPopup => _subPopup;

    /// <summary>The presence of the menu (motion state, tests).</summary>
    internal AiActionsPopupPresence? MenuPresence => _presence;

    /// <summary>The presence of the list of models (motion state, tests).</summary>
    internal AiActionsPopupPresence? SubmenuPresence => _subPresence;

    /// <summary>The rows of the menu, in order.</summary>
    internal IReadOnlyList<AiRegenerateMenuRow> Rows => _rows;

    /// <summary>The rows of the list of models, in order.</summary>
    internal IReadOnlyList<AiRegenerateMenuRow> SubmenuRows => _subRows;

    /// <summary>The highlighted row of the menu, or null.</summary>
    internal AiRegenerateMenuRow? HighlightedRow => _highlighted;

    /// <summary>The highlighted row of the list of models, or null.</summary>
    internal AiRegenerateMenuRow? SubmenuHighlightedRow => _subHighlighted;

    protected override Size MeasureOverride(Size availableSize) => default;

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == ModelsProperty)
        {
            if (IsOpen)
            {
                Observe(true);
                Rebuild();
            }
        }
        else if (change.Property == ProvidersProperty || change.Property == TryAgainTextProperty || change.Property == MoreConciseTextProperty
                 || change.Property == MoreDetailedTextProperty || change.Property == SwitchModelTextProperty
                 || change.Property == AnswerAgainWithTextProperty || change.Property == MenuNameProperty)
        {
            if (IsOpen)
            {
                Rebuild();
            }
        }
        else if (change.Property == PlacementTargetProperty)
        {
            Close(returnFocus: false, NavigationMethod.Pointer);
            if (_popup is not null)
            {
                _popup.PlacementTarget = change.GetNewValue<Control?>();
                _popup.OverlayInputPassThroughElement = change.GetNewValue<Control?>();
            }
        }
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        Close(returnFocus: false, NavigationMethod.Pointer, immediate: true);
    }

    #region Open and close

    /// <summary>Opens the menu or closes it when open (focus back on the target).</summary>
    public void Toggle(bool keyboard)
    {
        if (IsOpen)
        {
            Close(returnFocus: true, keyboard ? NavigationMethod.Tab : NavigationMethod.Pointer);
        }
        else
        {
            Open(keyboard);
        }
    }

    /// <summary>
    /// Opens the menu against <see cref="PlacementTarget"/>. With <paramref name="keyboard"/> the first row is highlighted
    /// and focused; otherwise nothing is highlighted and the surface takes focus.
    /// </summary>
    public void Open(bool keyboard = false)
    {
        if (IsOpen || PlacementTarget is not { } target || !target.IsEffectivelyEnabled || TopLevel.GetTopLevel(target) is not { } topLevel)
        {
            return;
        }
        EnsureMenu();
        _typeahead = string.Empty;
        Rebuild();
        Observe(true);
        IsOpen = true;
        _popup!.PlacementTarget = target;
        _popup.OverlayInputPassThroughElement = target;
        _presence!.Show();
        Hook(topLevel);
        if (keyboard && _rows.Count > 0)
        {
            MoveTo(_rows[0]);
        }
        else
        {
            _panel!.Focus(NavigationMethod.Pointer);
        }
        // The surface is laid out in the popup only now: focus again once it is in place.
        Dispatcher.UIThread.Post(() => ApplyOpenFocus(keyboard), DispatcherPriority.Loaded);
    }

    private void ApplyOpenFocus(bool keyboard)
    {
        if (!IsOpen || _panel is null || IsSubmenuOpen)
        {
            return;
        }
        if (_highlighted is { } row)
        {
            if (!row.IsFocused)
            {
                row.Focus(NavigationMethod.Directional);
            }
        }
        else if (!keyboard && !_panel.IsKeyboardFocusWithin)
        {
            _panel.Focus(NavigationMethod.Pointer);
        }
    }

    /// <summary>
    /// Closes the menu and its list. With <paramref name="returnFocus"/> (and always when focus is in the menu) the target
    /// takes focus back, by <paramref name="method"/> (Tab shows its keyboard ring).
    /// </summary>
    public void Close(bool returnFocus = true, NavigationMethod method = NavigationMethod.Tab, bool immediate = false)
    {
        if (!IsOpen)
        {
            return;
        }
        var inside = _panel?.IsKeyboardFocusWithin == true || _subPanel?.IsKeyboardFocusWithin == true;
        IsOpen = false;
        Unhook();
        Observe(false);
        SetHighlight(null);
        SetSubHighlight(null);
        // Focus goes back before the surfaces hide: hiding detaches the focused row and the focus manager would put focus
        // back without a method (no keyboard ring), after which asking again does nothing.
        if ((returnFocus || inside) && PlacementTarget is { } target && TopLevel.GetTopLevel(target) is not null)
        {
            target.Focus(method);
        }
        HideSubmenu(immediate);
        _presence?.Hide(immediate);
    }

    private void EnsureMenu()
    {
        if (_popup is not null)
        {
            return;
        }
        _panel = new AiRegenerateMenuPanel(this, isSubmenu: false);
        _popup = new AiActionsPopup(isSubmenu: false)
        {
            Placement = PlacementMode.TopEdgeAlignedLeft,
            MarginToAnchor = MenuGap
        };
        _presence = new AiActionsPopupPresence(_panel, _popup, MenuTravel);
        LogicalChildren.Add(_popup);

        _subPanel = new AiRegenerateMenuPanel(this, isSubmenu: true);
        _subPopup = new AiActionsPopup(isSubmenu: true)
        {
            SubmenuAnchor = SubmenuAnchorRect
        };
        _subPresence = new AiActionsPopupPresence(_subPanel, _subPopup, SubmenuTravel);
        LogicalChildren.Add(_subPopup);
    }

    #endregion

    #region Rows

    private List<AiModel> DistinctModels()
    {
        var result = new List<AiModel>();
        if (Models is { } models)
        {
            foreach (var model in models)
            {
                if (model is not null && !result.Contains(model))
                {
                    result.Add(model);
                }
            }
        }
        return result;
    }

    /// <summary>Builds both levels; a rebuild keeps the highlighted rows (and their focus) when they still exist.</summary>
    private void Rebuild()
    {
        if (_panel is null || _subPanel is null)
        {
            return;
        }
        var previous = _highlighted;
        var previousFocused = previous?.IsFocused == true;
        var previousSub = _subHighlighted;
        var previousSubFocused = previousSub?.IsFocused == true;
        SetHighlight(null);
        SetSubHighlight(null);

        _panel.List.Children.Clear();
        _rows.Clear();
        AutomationProperties.SetName(_panel, MenuName ?? string.Empty);
        Add(new AiRegenerateMenuRow(AiRegenerateMenuRowKind.TryAgain) { Text = TryAgainText ?? string.Empty, Icon = ArcIcons.RotateCcw });
        Add(new AiRegenerateMenuRow(AiRegenerateMenuRowKind.Concise) { Text = MoreConciseText ?? string.Empty, Icon = ArcIcons.Minimize2 });
        Add(new AiRegenerateMenuRow(AiRegenerateMenuRowKind.Detailed) { Text = MoreDetailedText ?? string.Empty, Icon = ArcIcons.Maximize2 });
        var models = DistinctModels();
        if (models.Count > 0)
        {
            _panel.List.Children.Add(new AiActionsHairline { Margin = new Thickness(6, 5) });
            Add(new AiRegenerateMenuRow(AiRegenerateMenuRowKind.SwitchModel) { Text = SwitchModelText ?? string.Empty, Icon = s_switchGlyph });
        }

        _subPanel.List.Children.Clear();
        _subRows.Clear();
        AutomationProperties.SetName(_subPanel, AnswerAgainWithText ?? string.Empty);
        _subPanel.List.Children.Add(new AiRegenerateMenuHeading { Text = AnswerAgainWithText ?? string.Empty });
        foreach (var model in models)
        {
            var mark = ModelMark.Resolve(model, Providers, out var color);
            var row = new AiRegenerateMenuRow(AiRegenerateMenuRowKind.Model, model) { Text = model.Name, Mark = mark, MarkColor = color };
            _subPanel.List.Children.Add(row);
            _subRows.Add(row);
        }
        if (models.Count == 0 && IsSubmenuOpen)
        {
            HideSubmenu(immediate: true);
        }

        if (previous is not null && _rows.FirstOrDefault(r => r.Kind == previous.Kind) is { } again)
        {
            SetHighlight(again);
            if (previousFocused)
            {
                again.Focus(NavigationMethod.Directional);
            }
        }
        if (previousSub?.Model is { } model0 && _subRows.FirstOrDefault(r => ReferenceEquals(r.Model, model0)) is { } subAgain)
        {
            SetSubHighlight(subAgain);
            if (previousSubFocused)
            {
                subAgain.Focus(NavigationMethod.Directional);
            }
        }
        else if (previousSubFocused && SwitchRow is { } switchRow)
        {
            switchRow.Focus(NavigationMethod.Directional);
        }

        void Add(AiRegenerateMenuRow row)
        {
            _panel.List.Children.Add(row);
            _rows.Add(row);
        }
    }

    private AiRegenerateMenuRow? SwitchRow => _rows.FirstOrDefault(r => r.Kind == AiRegenerateMenuRowKind.SwitchModel);

    private void SetHighlight(AiRegenerateMenuRow? row)
    {
        if (ReferenceEquals(row, _highlighted))
        {
            return;
        }
        _highlighted?.SetHighlighted(false);
        _highlighted = row;
        row?.SetHighlighted(true);
    }

    private void SetSubHighlight(AiRegenerateMenuRow? row)
    {
        if (ReferenceEquals(row, _subHighlighted))
        {
            return;
        }
        _subHighlighted?.SetHighlighted(false);
        _subHighlighted = row;
        row?.SetHighlighted(true);
    }

    private void MoveTo(AiRegenerateMenuRow row)
    {
        if (row.Kind == AiRegenerateMenuRowKind.Model)
        {
            SetSubHighlight(row);
        }
        else
        {
            SetHighlight(row);
            // Moving within the menu leaves the list of models.
            if (IsSubmenuOpen && row.Kind != AiRegenerateMenuRowKind.SwitchModel)
            {
                HideSubmenu(immediate: false);
            }
        }
        row.Focus(NavigationMethod.Directional);
    }

    #endregion

    #region Submenu

    /// <summary>The list of models is placed against the menu's edge, at the height of "Switch model".</summary>
    private Rect? SubmenuAnchorRect(TopLevel topLevel)
    {
        if (_panel is null || SwitchRow is not { } row || _panel.TranslatePoint(default, topLevel) is not { } panelOrigin ||
            row.TranslatePoint(default, topLevel) is not { } rowOrigin)
        {
            return null;
        }
        return new Rect(panelOrigin.X, rowOrigin.Y, _panel.Bounds.Width, row.Bounds.Height);
    }

    /// <summary>Opens the list of models beside the menu; with <paramref name="keyboard"/> its first model is highlighted and focused.</summary>
    internal void OpenSubmenu(bool keyboard)
    {
        if (!IsOpen || SwitchRow is not { } row || _subRows.Count == 0 || _subPopup is null || _subPresence is null || _panel is null)
        {
            return;
        }
        SetHighlight(row);
        if (!IsSubmenuOpen)
        {
            var opensLeft = false;
            if (TopLevel.GetTopLevel(row) is { } topLevel && _panel.TranslatePoint(default, topLevel) is { } origin)
            {
                _subPanel!.Measure(Size.Infinity);
                var width = Math.Max(_subPanel.DesiredSize.Width, _subPanel.MinWidth);
                var right = origin.X + _panel.Bounds.Width;
                opensLeft = right + width > topLevel.ClientSize.Width && origin.X - width >= 0;
            }
            _subPopup.ConfigureSubmenu(row, opensLeft, SubmenuOffset);
            IsSubmenuOpen = true;
            _subPresence.Show();
        }
        if (keyboard)
        {
            MoveTo(_subRows[0]);
        }
    }

    /// <summary>Closes the list of models; with <paramref name="focusRow"/> "Switch model" takes the highlight and focus back.</summary>
    internal void CloseSubmenu(bool focusRow)
    {
        if (!IsSubmenuOpen)
        {
            return;
        }
        var row = SwitchRow;
        if (focusRow && row is not null)
        {
            SetHighlight(row);
            row.Focus(NavigationMethod.Directional);
        }
        HideSubmenu(immediate: false);
    }

    private void HideSubmenu(bool immediate)
    {
        SetSubHighlight(null);
        if (!IsSubmenuOpen && _subPresence?.IsShown != true)
        {
            return;
        }
        IsSubmenuOpen = false;
        if (_subPanel?.IsKeyboardFocusWithin == true && IsOpen)
        {
            // Focus never stays in a list that is leaving.
            (SwitchRow as Control ?? _panel)?.Focus(NavigationMethod.Directional);
        }
        _subPresence?.Hide(immediate);
    }

    #endregion

    #region Keys

    /// <summary>Keys from a surface (it or one of its rows has focus).</summary>
    internal void OnMenuKeyDown(AiRegenerateMenuPanel panel, KeyEventArgs e)
    {
        if (e.Handled || !IsOpen)
        {
            return;
        }
        var sub = panel.IsSubmenu;
        var rows = sub ? _subRows : _rows;
        var highlighted = sub ? _subHighlighted : _highlighted;
        var current = highlighted is null ? -1 : rows.IndexOf(highlighted);
        var count = rows.Count;
        switch (e.Key)
        {
            case Key.Down:
            case Key.Up:
            case Key.Home:
            case Key.End:
            {
                e.Handled = true;
                if (count == 0)
                {
                    return;
                }
                var next = e.Key switch
                {
                    Key.Down => (current + 1) % count,
                    Key.Up => current < 0 ? count - 1 : (current - 1 + count) % count,
                    Key.Home => 0,
                    _ => count - 1
                };
                MoveTo(rows[next]);
                return;
            }
            case Key.Enter:
            case Key.Space:
                e.Handled = true;
                if (highlighted is { } row)
                {
                    Activate(row, NavigationMethod.Tab);
                }
                return;
            case Key.Right:
                e.Handled = true;
                if (!sub && highlighted is { Kind: AiRegenerateMenuRowKind.SwitchModel })
                {
                    OpenSubmenu(keyboard: true);
                }
                return;
            case Key.Left:
                e.Handled = true;
                if (sub)
                {
                    CloseSubmenu(focusRow: true);
                }
                return;
            case Key.Escape:
                e.Handled = true;
                Close(returnFocus: true, NavigationMethod.Tab);
                return;
            case Key.Tab:
                // Focus stays in the menu (Radix): Tab does nothing.
                e.Handled = true;
                return;
        }
        if (e.KeySymbol is { Length: 1 } symbol && !char.IsControl(symbol[0]) && symbol[0] != ' ' &&
            (e.KeyModifiers & (KeyModifiers.Control | KeyModifiers.Meta | KeyModifiers.Alt)) == 0)
        {
            e.Handled = true;
            if (TypeAhead(rows, current, symbol) is { } match)
            {
                MoveTo(rows[match]);
            }
        }
    }

    /// <summary>The next row (after the highlighted one, wrapping) whose text starts with the letters typed so far.</summary>
    private int? TypeAhead(IReadOnlyList<AiRegenerateMenuRow> rows, int current, string symbol)
    {
        var now = Now();
        _typeahead = now - _lastTyped > TypeaheadWindow ? symbol : _typeahead + symbol;
        _lastTyped = now;
        var count = rows.Count;
        // A prefix that is still being typed may stay on the current row; a new single letter moves on.
        var start = _typeahead.Length > 1 ? 0 : 1;
        for (var step = start; step <= count; step++)
        {
            var index = ((current + step) % count + count) % count;
            if (rows[index].Text?.StartsWith(_typeahead, StringComparison.CurrentCultureIgnoreCase) == true)
            {
                return index;
            }
        }
        return null;
    }

    #endregion

    #region Pointer and choice

    internal void OnRowPointerEntered(AiRegenerateMenuRow row, PointerEventArgs e)
    {
        if (e.Pointer.Type == PointerType.Touch || !IsOpen)
        {
            return;
        }
        if (row.Kind == AiRegenerateMenuRowKind.Model)
        {
            SetSubHighlight(row);
            row.Focus(NavigationMethod.Pointer);
            return;
        }
        SetHighlight(row);
        row.Focus(NavigationMethod.Pointer);
        if (row.Kind == AiRegenerateMenuRowKind.SwitchModel)
        {
            OpenSubmenu(keyboard: false);
        }
        else if (IsSubmenuOpen)
        {
            HideSubmenu(immediate: false);
        }
    }

    internal void OnRowPointerExited(AiRegenerateMenuRow row, PointerEventArgs e)
    {
        if (e.Pointer.Type == PointerType.Touch || !IsOpen)
        {
            return;
        }
        if (row.Kind == AiRegenerateMenuRowKind.Model)
        {
            if (ReferenceEquals(row, _subHighlighted))
            {
                SetSubHighlight(null);
                _subPanel?.Focus(NavigationMethod.Pointer);
            }
            return;
        }
        // "Switch model" stays highlighted while its list is open (the pointer is on its way there).
        if (!ReferenceEquals(row, _highlighted) || (row.Kind == AiRegenerateMenuRowKind.SwitchModel && IsSubmenuOpen))
        {
            return;
        }
        SetHighlight(null);
        _panel?.Focus(NavigationMethod.Pointer);
    }

    internal void OnRowInvoked(AiRegenerateMenuRow row, NavigationMethod method) => Activate(row, method);

    /// <summary>Opens the list ("Switch model") or chooses the row.</summary>
    private void Activate(AiRegenerateMenuRow row, NavigationMethod method)
    {
        if (!IsOpen)
        {
            return;
        }
        if (row.Kind == AiRegenerateMenuRowKind.SwitchModel)
        {
            OpenSubmenu(keyboard: method != NavigationMethod.Pointer);
            return;
        }
        var mode = row.Kind switch
        {
            AiRegenerateMenuRowKind.TryAgain => AiAnswerMode.Retry,
            AiRegenerateMenuRowKind.Concise => AiAnswerMode.Concise,
            AiRegenerateMenuRowKind.Detailed => AiAnswerMode.Detailed,
            _ => AiAnswerMode.SwitchModel
        };
        var model = row.Model;
        Close(returnFocus: true, method == NavigationMethod.Pointer ? NavigationMethod.Pointer : NavigationMethod.Tab);
        RaiseEvent(new AiRegenerateChosenEventArgs(ChosenEvent, this, mode, model));
    }

    #endregion

    #region Window and data hooks

    private void Hook(TopLevel topLevel)
    {
        Unhook();
        _hooked = topLevel;
        topLevel.AddHandler(PointerPressedEvent, OnTopLevelPointerPressed, RoutingStrategies.Tunnel, handledEventsToo: true);
        topLevel.AddHandler(KeyDownEvent, OnTopLevelKeyDown, RoutingStrategies.Bubble);
    }

    private void Unhook()
    {
        if (_hooked is not { } topLevel)
        {
            return;
        }
        topLevel.RemoveHandler(PointerPressedEvent, OnTopLevelPointerPressed);
        topLevel.RemoveHandler(KeyDownEvent, OnTopLevelKeyDown);
        _hooked = null;
    }

    private static bool IsWithin(Visual? root, Visual visual) =>
        root is not null && (ReferenceEquals(root, visual) || root.IsVisualAncestorOf(visual));

    private void OnTopLevelPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (!IsOpen || e.Source is not Visual source)
        {
            return;
        }
        // Presses in the menus are theirs; a press on the target passes through (it toggles the menu itself).
        if (IsWithin(_panel, source) || IsWithin(_subPanel, source) || IsWithin(PlacementTarget, source))
        {
            return;
        }
        Close(returnFocus: false, NavigationMethod.Pointer);
    }

    private void OnTopLevelKeyDown(object? sender, KeyEventArgs e)
    {
        // Keys from focus inside the menus are handled by their surfaces; this catches Escape with focus elsewhere.
        if (!e.Handled && e.Key == Key.Escape && IsOpen)
        {
            e.Handled = true;
            Close(returnFocus: true, NavigationMethod.Tab);
        }
    }

    // The models are app data that can outlive the menu: they are followed only while it is open.
    private void Observe(bool observe)
    {
        var next = observe ? Models as INotifyCollectionChanged : null;
        if (ReferenceEquals(next, _observedModels))
        {
            return;
        }
        if (_observedModels is not null)
        {
            _observedModels.CollectionChanged -= OnModelsChanged;
        }
        _observedModels = next;
        if (next is not null)
        {
            next.CollectionChanged += OnModelsChanged;
        }
    }

    private void OnModelsChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (IsOpen)
        {
            Rebuild();
        }
    }

    #endregion
}
