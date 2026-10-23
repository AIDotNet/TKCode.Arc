using System.Collections.Specialized;
using TKCode.Arc.DesignTokens;
using TKCode.Arc.Motion;
using TKCode.Arc.Primitives;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Threading;
using Avalonia.VisualTree;

namespace TKCode.Arc;

/// <summary>
/// The prompt studio's version menu: a 352px popover (a non-modal dialog) left-aligned 6px below the version pill. It lists
/// <see cref="Versions"/> as radio rows (avatar, commit message, "v3 · author · last week"); the selected row
/// (<see cref="SelectedVersion"/>) is what the two actions below act on: "Compare with v3" (disabled when the selected row
/// is the open version; it closes the menu and raises <see cref="CompareRequested"/>) and "Promote to production"
/// (disabled when the selected row is already in production; it raises <see cref="PromoteRequested"/> and the menu stays
/// open). The production marker sits on the row of <see cref="ProductionVersion"/> and glides to its new row on the smooth
/// spring when production changes. The last line says "Production serves v4"; after a promotion made here it says "v3 is
/// in production" with an Undo button (<see cref="UndoPromoteRequested"/>), crossfading as a label.
/// <para>
/// Keys: the menu opens with focus on the selected row (by keyboard with its ring, by pointer without); ↑ ↓ ← → move and
/// select (wrapping), Home and End go to the ends, Enter on a row does nothing; Tab cycles the selected row, the enabled
/// actions and Undo; Escape closes and gives focus back to the pill. After Promote focus moves to Undo, after Undo to the
/// selected row (Arc drops it to the page). The menu holds no words: the studio sets every text property.
/// </para>
/// </summary>
internal sealed class PromptVersionMenu : PromptPopoverMenu
{
    internal const double MenuWidth = 352;

    /// <summary>Room between the end of the production row's meta text and the marker.</summary>
    internal const double MarkerGap = 8;

    #region Properties

    /// <summary>The versions, newest first.</summary>
    public static readonly StyledProperty<IEnumerable<PromptVersion>?> VersionsProperty =
        AvaloniaProperty.Register<PromptVersionMenu, IEnumerable<PromptVersion>?>(nameof(Versions));

    /// <summary>The version the editor has open (Compare is disabled on its row).</summary>
    public static readonly StyledProperty<PromptVersion?> OpenVersionProperty =
        AvaloniaProperty.Register<PromptVersionMenu, PromptVersion?>(nameof(OpenVersion));

    /// <summary>The version in production (the marker's row; Promote is disabled on it).</summary>
    public static readonly StyledProperty<PromptVersion?> ProductionVersionProperty =
        AvaloniaProperty.Register<PromptVersionMenu, PromptVersion?>(nameof(ProductionVersion));

    /// <summary>
    /// The selected row, the version the actions act on (two-way). When it is not one of <see cref="Versions"/> as the menu
    /// opens, the menu selects the version after the open one (the one before it when the open one is last).
    /// </summary>
    public static readonly StyledProperty<PromptVersion?> SelectedVersionProperty =
        AvaloniaProperty.Register<PromptVersionMenu, PromptVersion?>(nameof(SelectedVersion), defaultBindingMode: BindingMode.TwoWay);

    /// <summary>The moment relative times are measured from; null uses the current time.</summary>
    public static readonly StyledProperty<DateTimeOffset?> NowProperty =
        AvaloniaProperty.Register<PromptVersionMenu, DateTimeOffset?>(nameof(Now));

    /// <summary>The accessible name of the radio group of versions ("Pick a version to compare or promote").</summary>
    public static readonly StyledProperty<string?> GroupNameProperty =
        AvaloniaProperty.Register<PromptVersionMenu, string?>(nameof(GroupName));

    /// <summary>The word of the production marker ("Production").</summary>
    public static readonly StyledProperty<string?> ProductionTextProperty =
        AvaloniaProperty.Register<PromptVersionMenu, string?>(nameof(ProductionText));

    /// <summary>The compare action, from the selected version's label ("Compare with v3").</summary>
    public static readonly StyledProperty<Func<string, string>?> CompareTextProperty =
        AvaloniaProperty.Register<PromptVersionMenu, Func<string, string>?>(nameof(CompareText));

    /// <summary>The promote action ("Promote to production").</summary>
    public static readonly StyledProperty<string?> PromoteTextProperty =
        AvaloniaProperty.Register<PromptVersionMenu, string?>(nameof(PromoteText));

    /// <summary>The last line, from the production version's label ("Production serves v4").</summary>
    public static readonly StyledProperty<Func<string, string>?> ProductionServesTextProperty =
        AvaloniaProperty.Register<PromptVersionMenu, Func<string, string>?>(nameof(ProductionServesText));

    /// <summary>The last line after a promotion made here, from the label now in production ("v3 is in production").</summary>
    public static readonly StyledProperty<Func<string, string>?> InProductionTextProperty =
        AvaloniaProperty.Register<PromptVersionMenu, Func<string, string>?>(nameof(InProductionText));

    /// <summary>The undo button of the last line ("Undo").</summary>
    public static readonly StyledProperty<string?> UndoTextProperty =
        AvaloniaProperty.Register<PromptVersionMenu, string?>(nameof(UndoText));

    /// <summary>The meta line of a row, from the label, the author and the relative time ("v3 · Lena Brooks · last week").</summary>
    public static readonly StyledProperty<Func<string, string, string, string>?> MetaFormatProperty =
        AvaloniaProperty.Register<PromptVersionMenu, Func<string, string, string, string>?>(nameof(MetaFormat));

    /// <summary>The relative time under a week, from the whole days (0 to 6): "3 days ago".</summary>
    public static readonly StyledProperty<Func<int, string>?> DaysAgoTextProperty =
        AvaloniaProperty.Register<PromptVersionMenu, Func<int, string>?>(nameof(DaysAgoText));

    /// <summary>The relative time for 7 to 13 days ("last week").</summary>
    public static readonly StyledProperty<string?> LastWeekTextProperty =
        AvaloniaProperty.Register<PromptVersionMenu, string?>(nameof(LastWeekText));

    /// <summary>The relative time from two weeks on, from the whole weeks: "3 weeks ago".</summary>
    public static readonly StyledProperty<Func<int, string>?> WeeksAgoTextProperty =
        AvaloniaProperty.Register<PromptVersionMenu, Func<int, string>?>(nameof(WeeksAgoText));

    /// <summary>Whether the last line offers Undo (read-only): a promotion made in this menu is in production.</summary>
    public static readonly DirectProperty<PromptVersionMenu, bool> IsUndoAvailableProperty =
        AvaloniaProperty.RegisterDirect<PromptVersionMenu, bool>(nameof(IsUndoAvailable), o => o.IsUndoAvailable);

    /// <summary>Raised (after the menu closed) when "Compare with …" is chosen, with the selected version.</summary>
    public static readonly RoutedEvent<PromptVersionEventArgs> CompareRequestedEvent =
        RoutedEvent.Register<PromptVersionMenu, PromptVersionEventArgs>(nameof(CompareRequested), RoutingStrategies.Bubble);

    /// <summary>Raised when "Promote to production" is chosen, with the selected version; set <see cref="ProductionVersion"/> to it.</summary>
    public static readonly RoutedEvent<PromptVersionEventArgs> PromoteRequestedEvent =
        RoutedEvent.Register<PromptVersionMenu, PromptVersionEventArgs>(nameof(PromoteRequested), RoutingStrategies.Bubble);

    /// <summary>Raised when Undo is chosen; set <see cref="ProductionVersion"/> back to the version it replaced.</summary>
    public static readonly RoutedEvent<RoutedEventArgs> UndoPromoteRequestedEvent =
        RoutedEvent.Register<PromptVersionMenu, RoutedEventArgs>(nameof(UndoPromoteRequested), RoutingStrategies.Bubble);

    /// <inheritdoc cref="VersionsProperty"/>
    public IEnumerable<PromptVersion>? Versions { get => GetValue(VersionsProperty); set => SetValue(VersionsProperty, value); }

    /// <inheritdoc cref="OpenVersionProperty"/>
    public PromptVersion? OpenVersion { get => GetValue(OpenVersionProperty); set => SetValue(OpenVersionProperty, value); }

    /// <inheritdoc cref="ProductionVersionProperty"/>
    public PromptVersion? ProductionVersion { get => GetValue(ProductionVersionProperty); set => SetValue(ProductionVersionProperty, value); }

    /// <inheritdoc cref="SelectedVersionProperty"/>
    public PromptVersion? SelectedVersion { get => GetValue(SelectedVersionProperty); set => SetValue(SelectedVersionProperty, value); }

    /// <inheritdoc cref="NowProperty"/>
    public DateTimeOffset? Now { get => GetValue(NowProperty); set => SetValue(NowProperty, value); }

    /// <inheritdoc cref="GroupNameProperty"/>
    public string? GroupName { get => GetValue(GroupNameProperty); set => SetValue(GroupNameProperty, value); }

    /// <inheritdoc cref="ProductionTextProperty"/>
    public string? ProductionText { get => GetValue(ProductionTextProperty); set => SetValue(ProductionTextProperty, value); }

    /// <inheritdoc cref="CompareTextProperty"/>
    public Func<string, string>? CompareText { get => GetValue(CompareTextProperty); set => SetValue(CompareTextProperty, value); }

    /// <inheritdoc cref="PromoteTextProperty"/>
    public string? PromoteText { get => GetValue(PromoteTextProperty); set => SetValue(PromoteTextProperty, value); }

    /// <inheritdoc cref="ProductionServesTextProperty"/>
    public Func<string, string>? ProductionServesText { get => GetValue(ProductionServesTextProperty); set => SetValue(ProductionServesTextProperty, value); }

    /// <inheritdoc cref="InProductionTextProperty"/>
    public Func<string, string>? InProductionText { get => GetValue(InProductionTextProperty); set => SetValue(InProductionTextProperty, value); }

    /// <inheritdoc cref="UndoTextProperty"/>
    public string? UndoText { get => GetValue(UndoTextProperty); set => SetValue(UndoTextProperty, value); }

    /// <inheritdoc cref="MetaFormatProperty"/>
    public Func<string, string, string, string>? MetaFormat { get => GetValue(MetaFormatProperty); set => SetValue(MetaFormatProperty, value); }

    /// <inheritdoc cref="DaysAgoTextProperty"/>
    public Func<int, string>? DaysAgoText { get => GetValue(DaysAgoTextProperty); set => SetValue(DaysAgoTextProperty, value); }

    /// <inheritdoc cref="LastWeekTextProperty"/>
    public string? LastWeekText { get => GetValue(LastWeekTextProperty); set => SetValue(LastWeekTextProperty, value); }

    /// <inheritdoc cref="WeeksAgoTextProperty"/>
    public Func<int, string>? WeeksAgoText { get => GetValue(WeeksAgoTextProperty); set => SetValue(WeeksAgoTextProperty, value); }

    /// <inheritdoc cref="IsUndoAvailableProperty"/>
    public bool IsUndoAvailable { get => _isUndoAvailable; private set => SetAndRaise(IsUndoAvailableProperty, ref _isUndoAvailable, value); }

    /// <inheritdoc cref="CompareRequestedEvent"/>
    public event EventHandler<PromptVersionEventArgs>? CompareRequested { add => AddHandler(CompareRequestedEvent, value); remove => RemoveHandler(CompareRequestedEvent, value); }

    /// <inheritdoc cref="PromoteRequestedEvent"/>
    public event EventHandler<PromptVersionEventArgs>? PromoteRequested { add => AddHandler(PromoteRequestedEvent, value); remove => RemoveHandler(PromoteRequestedEvent, value); }

    /// <inheritdoc cref="UndoPromoteRequestedEvent"/>
    public event EventHandler<RoutedEventArgs>? UndoPromoteRequested { add => AddHandler(UndoPromoteRequestedEvent, value); remove => RemoveHandler(UndoPromoteRequestedEvent, value); }

    #endregion

    private readonly List<PromptVersionRow> _rows = new();
    private readonly List<PromptVersion> _observedVersions = new();
    private PromptVersionRowsHost? _rowsHost;
    private PromptMenuAction? _compare;
    private PromptMenuAction? _promote;
    private ArcContentSwap? _statusSwap;
    private PromptVersionStatus? _status;
    private INotifyCollectionChanged? _observedCollection;
    private PromptVersion? _pendingPromotion;
    private PromptVersion? _pendingPrevious;
    private PromptVersion? _undoTo;
    private PromptVersion? _markerVersion;
    private Rect _markerRect;
    private bool _isUndoAvailable;
    private NavigationMethod? _focusUndo;

    protected override PlacementMode Placement => PlacementMode.BottomEdgeAlignedLeft;

    protected override double SurfaceWidth => MenuWidth;

    /// <summary>The rows, in order (empty until the first open).</summary>
    internal IReadOnlyList<PromptVersionRow> Rows => _rows;

    internal PromptMenuAction? CompareAction => _compare;

    internal PromptMenuAction? PromoteAction => _promote;

    /// <summary>The last line's layer on screen.</summary>
    internal PromptVersionStatus? Status => _status;

    internal ArcContentSwap? StatusSwap => _statusSwap;

    internal PromptProductionMarker? Marker => _rowsHost?.Marker;

    internal PromptVersionRowsHost? RowsHost => _rowsHost;

    /// <summary>The version whose row the marker is on (or gliding to), or null.</summary>
    internal PromptVersion? MarkerVersion => _markerVersion;

    private DateTimeOffset Moment => Now ?? DateTimeOffset.Now;

    #region Content

    protected override Control CreateContent()
    {
        var marker = new PromptProductionMarker { Spring = ArcSpring.Smooth };
        _rowsHost = new PromptVersionRowsHost(this, marker);
        AutomationProperties.SetName(_rowsHost, GroupName);

        _compare = new PromptMenuAction { Icon = ArcIcons.GitCompareArrows };
        _compare.Click += OnCompareClick;
        _promote = new PromptMenuAction { Icon = ArcIcons.ArrowUpFromLine };
        _promote.Click += OnPromoteClick;
        // 6px down, a 1px BorderSubtle line, 6px more, then the two 36px actions 2px apart.
        var actions = new Border
        {
            Margin = new Thickness(0, 6, 0, 0),
            Padding = new Thickness(0, 6, 0, 0),
            BorderThickness = new Thickness(0, 1, 0, 0),
            Child = new StackPanel { Spacing = 2, Children = { _compare, _promote } }
        };
        ArcTokens.Bind(actions, Border.BorderBrushProperty, ArcDesignTokenKind.BorderSubtle);

        _statusSwap = new ArcContentSwap
        {
            Mode = ContentSwapMode.Label,
            HorizontalContentAlignment = HorizontalAlignment.Stretch,
            VerticalContentAlignment = VerticalAlignment.Center
        };
        AutomationProperties.SetLiveSetting(_statusSwap, AutomationLiveSetting.Polite);
        // 6px down, a 1px BorderSubtle line, then a 40px line (padding 2 2 0 10) for the words and Undo.
        var status = new Border
        {
            Margin = new Thickness(0, 6, 0, 0),
            Padding = new Thickness(10, 2, 2, 0),
            BorderThickness = new Thickness(0, 1, 0, 0),
            Height = 40,
            Child = _statusSwap
        };
        ArcTokens.Bind(status, Border.BorderBrushProperty, ArcDesignTokenKind.BorderSubtle);

        return new StackPanel { Children = { _rowsHost, actions, status } };
    }

    protected override void OnOpening()
    {
        // The marker lands on its row as the menu opens; it glides only for a change while open.
        _markerVersion = null;
        _rowsHost?.Marker.Hide(animate: false);
        // The rows' arrange places it again (nothing else may lay them out when the menu reopens unchanged).
        _rowsHost?.Rows.InvalidateArrange();
        ObserveData(true);
        BuildRows();
        EnsureSelection();
        RefreshState();
    }

    protected override void OnHidden()
    {
        ObserveData(false);
        _focusUndo = null;
    }

    /// <summary>Builds a row per version (the rows host keeps them while the menu is open).</summary>
    private void BuildRows()
    {
        if (_rowsHost is null)
        {
            return;
        }
        var focused = _rows.FirstOrDefault(r => r.IsKeyboardFocusWithin)?.Version;
        _rowsHost.Rows.Children.Clear();
        _rows.Clear();
        foreach (var version in DistinctVersions())
        {
            var row = new PromptVersionRow(version) { Owner = this };
            _rows.Add(row);
            _rowsHost.Rows.Children.Add(row);
        }
        RefreshRowTexts();
        if (focused is not null && RowOf(focused) is { } again)
        {
            again.Focus(UsesKeyboard ? NavigationMethod.Directional : NavigationMethod.Pointer);
        }
    }

    private List<PromptVersion> DistinctVersions()
    {
        var result = new List<PromptVersion>();
        if (Versions is { } source)
        {
            foreach (var version in source)
            {
                if (version is not null && !result.Contains(version))
                {
                    result.Add(version);
                }
            }
        }
        return result;
    }

    private PromptVersionRow? RowOf(PromptVersion? version) =>
        version is null ? null : _rows.FirstOrDefault(r => ReferenceEquals(r.Version, version));

    /// <summary>Message, author, avatar and meta line of every row (relative times from <see cref="Now"/>).</summary>
    private void RefreshRowTexts()
    {
        var now = Moment;
        foreach (var row in _rows)
        {
            var version = row.Version;
            var author = version.Author?.Name ?? string.Empty;
            var when = RelativeTimeText.Format(version.CreatedAt, now, DaysAgoText, LastWeekText, WeeksAgoText);
            row.Message = version.Message;
            row.AuthorName = author;
            row.AuthorImage = version.Author?.Avatar;
            row.SetMeta(MetaFormat, version.Label, author, when);
        }
        RefreshRowNames();
    }

    private void RefreshRowNames()
    {
        foreach (var row in _rows)
        {
            var production = ReferenceEquals(row.Version, ProductionVersion) && !string.IsNullOrEmpty(ProductionText);
            var parts = new List<string>(3);
            if (!string.IsNullOrEmpty(row.Message))
            {
                parts.Add(row.Message!);
            }
            if (row.MetaText.Length > 0)
            {
                parts.Add(row.MetaText);
            }
            if (production)
            {
                parts.Add(ProductionText!);
            }
            AutomationProperties.SetName(row, string.Join(", ", parts));
        }
    }

    /// <summary>A selection that is not one of the rows becomes the version after the open one (or before it when it is last).</summary>
    private void EnsureSelection()
    {
        if (_rows.Count == 0 || RowOf(SelectedVersion) is not null)
        {
            return;
        }
        var open = _rows.FindIndex(r => ReferenceEquals(r.Version, OpenVersion));
        var index = open < 0 ? 0 : open + 1 < _rows.Count ? open + 1 : Math.Max(0, open - 1);
        SetCurrentValue(SelectedVersionProperty, _rows[index].Version);
    }

    /// <summary>Checked rows, the marker's room, the actions and the last line, from the current versions.</summary>
    private void RefreshState()
    {
        if (_rowsHost is null)
        {
            return;
        }
        var marker = _rowsHost.Marker;
        marker.Text = ProductionText;
        var room = string.IsNullOrEmpty(ProductionText) ? 0 : marker.PillWidth + MarkerGap;
        foreach (var row in _rows)
        {
            row.IsChecked = ReferenceEquals(row.Version, SelectedVersion);
            row.MarkerRoom = ReferenceEquals(row.Version, ProductionVersion) ? room : 0;
        }
        RefreshRowNames();
        RefreshActions();
        RefreshStatus();
        _rowsHost.InvalidateArrange();
    }

    private void RefreshActions()
    {
        if (_compare is null || _promote is null)
        {
            return;
        }
        var selected = SelectedVersion;
        _compare.Text = selected is not null ? CompareText?.Invoke(selected.Label) : null;
        _compare.IsEnabled = selected is not null && RowOf(selected) is not null && !ReferenceEquals(selected, OpenVersion);
        _promote.Text = PromoteText;
        var promoteHadFocus = _promote.IsKeyboardFocusWithin;
        _promote.IsEnabled = selected is not null && RowOf(selected) is not null && !ReferenceEquals(selected, ProductionVersion);
        if (promoteHadFocus && !_promote.IsEnabled)
        {
            // Promote just ran (or production moved under it): focus goes on to Undo, else to the selected row.
            _focusUndo ??= UsesKeyboard ? NavigationMethod.Tab : NavigationMethod.Pointer;
        }
    }

    /// <summary>A new last-line layer (crossfaded) when its words or its Undo change.</summary>
    private void RefreshStatus()
    {
        if (_statusSwap is null)
        {
            return;
        }
        var production = ProductionVersion;
        var undo = _undoTo is not null && production is not null && !ReferenceEquals(production, _undoTo);
        IsUndoAvailable = undo;
        var text = production is null
            ? string.Empty
            : (undo ? InProductionText : ProductionServesText)?.Invoke(production.Label) ?? string.Empty;
        AutomationProperties.SetName(_statusSwap, text);
        if (_status is null || _status.Text != text || _status.ShowsUndo != undo || _status.UndoText != UndoText)
        {
            _status = new PromptVersionStatus { Owner = this, Text = text, ShowsUndo = undo, UndoText = UndoText };
            _statusSwap.Content = _status;
        }
        if (_focusUndo is { } method && IsShown)
        {
            _focusUndo = null;
            var layer = _status;
            Dispatcher.UIThread.Post(() => FocusAfterPromote(layer, method), DispatcherPriority.Loaded);
        }
    }

    private void FocusAfterPromote(PromptVersionStatus layer, NavigationMethod method)
    {
        if (!IsShown)
        {
            return;
        }
        if (layer.ShowsUndo && layer.UndoButton is { IsEffectivelyVisible: true } undo && TopLevel.GetTopLevel(undo) is not null)
        {
            undo.Focus(method);
        }
        else
        {
            RowOf(SelectedVersion)?.Focus(method);
        }
    }

    #endregion

    #region Marker

    /// <summary>
    /// Puts the marker on the production row's meta line, right-aligned: a new production version glides it there, the
    /// first placement and a resize land at once; no production row (or no word) hides it.
    /// </summary>
    internal void PlaceMarker()
    {
        if (_rowsHost is null)
        {
            return;
        }
        var marker = _rowsHost.Marker;
        var production = ProductionVersion;
        // The marker's word is measured once it is in the window: the production row keeps room for it.
        var room = string.IsNullOrEmpty(ProductionText) ? 0 : marker.PillWidth + MarkerGap;
        foreach (var each in _rows)
        {
            var wanted = ReferenceEquals(each.Version, production) ? room : 0;
            if (Math.Abs(each.MarkerRoom - wanted) > 0.01)
            {
                each.MarkerRoom = wanted;
            }
        }
        if (RowOf(production) is not { } row || string.IsNullOrEmpty(ProductionText) || row.MetaLine is not { } line ||
            !line.IsArrangeValid || line.Bounds.Width <= 0 || line.TranslatePoint(default, marker) is not { } origin)
        {
            if (_markerVersion is not null)
            {
                marker.Hide(animate: false);
                _markerVersion = null;
            }
            return;
        }
        var width = marker.PillWidth;
        var rect = new Rect(origin.X + line.Bounds.Width - width, origin.Y + (line.Bounds.Height - PromptProductionMarker.PillHeight) / 2,
            width, PromptProductionMarker.PillHeight).Deflate(PromptProductionMarker.StrokeInset);
        if (ReferenceEquals(_markerVersion, production) && _markerRect == rect)
        {
            return;
        }
        var glide = _markerVersion is not null && !ReferenceEquals(_markerVersion, production) && IsShown;
        _markerVersion = production;
        _markerRect = rect;
        marker.MoveTo(rect, glide, ArcSpring.Smooth);
    }

    #endregion

    #region Input

    internal void OnRowInvoked(PromptVersionRow row, NavigationMethod method)
    {
        if (!IsShown)
        {
            return;
        }
        if (method == NavigationMethod.Pointer)
        {
            UsesKeyboard = false;
        }
        Select(row, method == NavigationMethod.Unspecified ? NavigationMethod.Directional : method);
    }

    private void Select(PromptVersionRow row, NavigationMethod method)
    {
        SetCurrentValue(SelectedVersionProperty, row.Version);
        row.Focus(method);
        row.BringIntoView();
    }

    protected override void OnMenuKeyDown(KeyEventArgs e)
    {
        // Arrows act on the rows only (the actions and Undo are plain buttons).
        if (e.Source is not Visual source || (e.KeyModifiers & (KeyModifiers.Control | KeyModifiers.Alt | KeyModifiers.Meta)) != 0)
        {
            return;
        }
        var index = _rows.FindIndex(r => ReferenceEquals(r, source) || r.IsVisualAncestorOf(source));
        if (index < 0)
        {
            return;
        }
        var count = _rows.Count;
        int? next = e.Key switch
        {
            Key.Down or Key.Right => (index + 1) % count,
            Key.Up or Key.Left => (index - 1 + count) % count,
            Key.Home => 0,
            Key.End => count - 1,
            _ => null
        };
        if (next is { } target)
        {
            e.Handled = true;
            UsesKeyboard = true;
            Select(_rows[target], NavigationMethod.Directional);
            return;
        }
        if (e.Key is Key.Enter or Key.Space)
        {
            // Enter does nothing on a row (Arc); Space checks it, which it already is.
            e.Handled = true;
            UsesKeyboard = true;
            if (e.Key == Key.Space)
            {
                Select(_rows[index], NavigationMethod.Directional);
            }
        }
    }

    protected override void ApplyOpenFocus(ModelMenuOpenFocus focus)
    {
        var row = RowOf(SelectedVersion) ?? _rows.FirstOrDefault();
        if (row is null)
        {
            Surface?.Focus(NavigationMethod.Pointer);
            return;
        }
        row.Focus(focus == ModelMenuOpenFocus.Surface ? NavigationMethod.Pointer : NavigationMethod.Tab);
    }

    protected override IEnumerable<Control> TabStops()
    {
        if (RowOf(SelectedVersion) is { } row)
        {
            yield return row;
        }
        if (_compare is not null)
        {
            yield return _compare;
        }
        if (_promote is not null)
        {
            yield return _promote;
        }
        if (_status is { ShowsUndo: true, UndoButton: { } undo })
        {
            yield return undo;
        }
    }

    private void OnCompareClick(object? sender, RoutedEventArgs e)
    {
        e.Handled = true;
        if (!IsShown || SelectedVersion is not { } version || _compare is not { IsEffectivelyEnabled: true })
        {
            return;
        }
        Close(returnFocus: true);
        RaiseEvent(new PromptVersionEventArgs(CompareRequestedEvent, this, version));
    }

    private void OnPromoteClick(object? sender, RoutedEventArgs e)
    {
        e.Handled = true;
        if (!IsShown || SelectedVersion is not { } version || ReferenceEquals(version, ProductionVersion))
        {
            return;
        }
        _pendingPromotion = version;
        _pendingPrevious = ProductionVersion;
        RaiseEvent(new PromptVersionEventArgs(PromoteRequestedEvent, this, version));
    }

    internal void OnUndoClicked(PromptVersionStatus layer)
    {
        if (!IsShown || !ReferenceEquals(layer, _status) || !layer.ShowsUndo)
        {
            return;
        }
        var hadFocus = layer.IsKeyboardFocusWithin;
        _undoTo = null;
        _pendingPromotion = null;
        if (hadFocus)
        {
            // Undo leaves with its line: focus goes to the selected row.
            var method = UsesKeyboard ? NavigationMethod.Tab : NavigationMethod.Pointer;
            RowOf(SelectedVersion)?.Focus(method);
        }
        RaiseEvent(new RoutedEventArgs(UndoPromoteRequestedEvent, this));
        RefreshStatus();
    }

    #endregion

    #region Changes

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == ProductionVersionProperty)
        {
            OnProductionChanged(change.GetNewValue<PromptVersion?>());
        }
        else if (change.Property == VersionsProperty)
        {
            OnVersionsChanged();
        }
        else if (change.Property == SelectedVersionProperty || change.Property == OpenVersionProperty ||
                 change.Property == ProductionTextProperty || change.Property == CompareTextProperty ||
                 change.Property == PromoteTextProperty || change.Property == ProductionServesTextProperty ||
                 change.Property == InProductionTextProperty || change.Property == UndoTextProperty)
        {
            RefreshState();
        }
        else if (change.Property == MetaFormatProperty || change.Property == DaysAgoTextProperty || change.Property == LastWeekTextProperty ||
                 change.Property == WeeksAgoTextProperty || change.Property == NowProperty)
        {
            RefreshRowTexts();
        }
        else if (change.Property == GroupNameProperty && _rowsHost is not null)
        {
            AutomationProperties.SetName(_rowsHost, GroupName);
        }
    }

    /// <summary>
    /// Production moved: to the version promoted here (Undo becomes available, to go back to the one it replaced), back to
    /// that one (the undo happened), or elsewhere (Undo no longer applies).
    /// </summary>
    private void OnProductionChanged(PromptVersion? production)
    {
        _undoTo = _pendingPromotion is { } target && ReferenceEquals(production, target) ? _pendingPrevious : null;
        _pendingPromotion = null;
        _pendingPrevious = null;
        RefreshState();
    }

    private void OnVersionsChanged()
    {
        if (IsOpen)
        {
            ObserveData(true);
            BuildRows();
            EnsureSelection();
            RefreshState();
        }
    }

    // The versions are app data that can outlive the menu: they are followed only while it is open.
    private void ObserveData(bool observe)
    {
        var next = observe ? Versions as INotifyCollectionChanged : null;
        if (!ReferenceEquals(next, _observedCollection))
        {
            if (_observedCollection is not null)
            {
                _observedCollection.CollectionChanged -= OnCollectionChanged;
            }
            _observedCollection = next;
            if (next is not null)
            {
                next.CollectionChanged += OnCollectionChanged;
            }
        }
        foreach (var version in _observedVersions)
        {
            version.PropertyChanged -= OnVersionPropertyChanged;
        }
        _observedVersions.Clear();
        if (observe)
        {
            foreach (var version in DistinctVersions())
            {
                version.PropertyChanged += OnVersionPropertyChanged;
                _observedVersions.Add(version);
            }
        }
    }

    private void OnCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e) => OnVersionsChanged();

    private void OnVersionPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (e.Property == PromptVersion.LabelProperty || e.Property == PromptVersion.MessageProperty ||
            e.Property == PromptVersion.AuthorProperty || e.Property == PromptVersion.CreatedAtProperty)
        {
            RefreshRowTexts();
            RefreshState();
        }
    }

    #endregion
}
