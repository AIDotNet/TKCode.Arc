using TKCode.Arc.Primitives;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;

namespace TKCode.Arc;

/// <summary>
/// The list that opens while the person types "/" or "@" in the composer's prompt (Arc's <c>.pop</c>): a raised surface
/// 300 wide and at most 280 high (padding 5, radius 20, 1px border; the floating shadow comes from the popup) whose rows
/// scroll inside it. Commands show as "/name" over their description; mentions are grouped under headings in the order
/// the owner gives. One row is active: the arrow keys move it (wrapping, and scrolling it into view, which Arc's list
/// does not do), the pointer moves it on hover, and Enter, Tab or a click accept it.
/// <para>
/// The list owns filtering results and keyboard position only. The prompt keeps focus throughout: the owner routes the
/// keys (<see cref="TryHandleKey"/> or <see cref="MoveActive"/> / <see cref="AcceptActive"/>), closes the list on Escape,
/// and decides what accepting a row inserts (<see cref="Accepted"/>). When the filtered rows change, the surface height
/// follows on the settle spring (Core <see cref="SpringSizeDecorator"/>); the active fill jumps without a transition.
/// </para>
/// </summary>
[TemplatePart(SizePart, typeof(SpringSizeDecorator))]
[TemplatePart(ScrollPart, typeof(ScrollViewer))]
[TemplatePart(ListPart, typeof(AiSuggestionList))]
internal class AiSuggestionPop : TemplatedControl
{
    public const string SizePart = "PART_Size";
    public const string ScrollPart = "PART_Scroll";
    public const string ListPart = "PART_List";

    /// <summary>The accessible name of the list of commands ("Commands"); the owner sets it.</summary>
    public static readonly StyledProperty<string?> CommandListNameProperty =
        AvaloniaProperty.Register<AiSuggestionPop, string?>(nameof(CommandListName));

    /// <summary>The accessible name of the list of mentions ("Files and docs"); the owner sets it.</summary>
    public static readonly StyledProperty<string?> MentionListNameProperty =
        AvaloniaProperty.Register<AiSuggestionPop, string?>(nameof(MentionListName));

    /// <summary>The surface width (300); a narrow anchor lowers the shown width (the popup sets <c>Width</c>).</summary>
    public static readonly StyledProperty<double> SurfaceWidthProperty =
        AvaloniaProperty.Register<AiSuggestionPop, double>(nameof(SurfaceWidth), 300);

    /// <summary>The surface's greatest height (280, border included); taller lists scroll inside it.</summary>
    public static readonly StyledProperty<double> SurfaceMaxHeightProperty =
        AvaloniaProperty.Register<AiSuggestionPop, double>(nameof(SurfaceMaxHeight), 280);

    /// <summary>The room the popup found for the surface when neither side of the line fits it (infinite otherwise).</summary>
    public static readonly StyledProperty<double> AvailableHeightProperty =
        AvaloniaProperty.Register<AiSuggestionPop, double>(nameof(AvailableHeight), double.PositiveInfinity);

    /// <summary>Raised when the person accepts a row: a click, or <see cref="AcceptActive"/> (Enter or Tab).</summary>
    public static readonly RoutedEvent<AiSuggestionAcceptedEventArgs> AcceptedEvent =
        RoutedEvent.Register<AiSuggestionPop, AiSuggestionAcceptedEventArgs>(nameof(Accepted), RoutingStrategies.Bubble);

    private SpringSizeDecorator? _size;
    private ScrollViewer? _scroll;
    private AiSuggestionList? _list;
    private IReadOnlyList<AiSuggestionRow> _rows = Array.Empty<AiSuggestionRow>();
    private AiSuggestionKind _kind;
    private int _active = -1;
    private int _announcedCount = -1;
    private bool _fresh = true;
    private bool _scrollQueued;

    static AiSuggestionPop()
    {
        FocusableProperty.OverrideDefaultValue<AiSuggestionPop>(false);
        IsTabStopProperty.OverrideDefaultValue<AiSuggestionPop>(false);
        // The theme sets the same values; defaults keep NaturalHeight right before the list was ever styled.
        PaddingProperty.OverrideDefaultValue<AiSuggestionPop>(new Thickness(5));
        BorderThicknessProperty.OverrideDefaultValue<AiSuggestionPop>(new Thickness(1));
        CornerRadiusProperty.OverrideDefaultValue<AiSuggestionPop>(new CornerRadius(20));
    }

    public string? CommandListName { get => GetValue(CommandListNameProperty); set => SetValue(CommandListNameProperty, value); }

    public string? MentionListName { get => GetValue(MentionListNameProperty); set => SetValue(MentionListNameProperty, value); }

    public double SurfaceWidth { get => GetValue(SurfaceWidthProperty); set => SetValue(SurfaceWidthProperty, value); }

    public double SurfaceMaxHeight { get => GetValue(SurfaceMaxHeightProperty); set => SetValue(SurfaceMaxHeightProperty, value); }

    public double AvailableHeight { get => GetValue(AvailableHeightProperty); set => SetValue(AvailableHeightProperty, value); }

    /// <summary>
    /// Turns the number of rows into a polite announcement ("4 commands"). The list calls it when it shows rows after
    /// being closed or empty, and when the number of rows changes; the owner says the text in its status region and
    /// returns it (the list keeps the last one in <see cref="LastAnnouncement"/>). Null announces nothing.
    /// </summary>
    public Func<int, string?>? AnnounceCount { get; set; }

    /// <summary>The text <see cref="AnnounceCount"/> returned last (null before the first call).</summary>
    public string? LastAnnouncement { get; private set; }

    /// <summary>The shown rows, as given to <see cref="Show"/>.</summary>
    public IReadOnlyList<AiSuggestionRow> Rows => _rows;

    /// <summary>Commands or mentions.</summary>
    public AiSuggestionKind Kind => _kind;

    /// <summary>Whether there is nothing to show.</summary>
    public bool IsEmpty => _rows.Count == 0;

    /// <summary>The index of the active row, or -1 when there are no rows.</summary>
    public int ActiveIndex => _active;

    /// <summary>The active row, or null.</summary>
    public AiSuggestionRow? ActiveRow => _active >= 0 && _active < _rows.Count ? _rows[_active] : null;

    /// <summary>
    /// The active row as assistive technology reads it ("/recap, Boil a doc …"): the owner puts it on its text box
    /// (<c>AutomationProperties.ItemStatus</c>), since the list never has focus.
    /// </summary>
    public string? ActiveRowName => ActiveRow is { } row ? AccessibleName(_kind, row) : null;

    /// <summary>The accessible name of the list for the shown kind.</summary>
    public string? ListName => _kind == AiSuggestionKind.Command ? CommandListName : MentionListName;

    /// <summary>The popup that shows this list (set by <see cref="AiSuggestionPopup"/>); an empty <see cref="Show"/> closes it.</summary>
    internal AiSuggestionPopup? Host { get; set; }

    internal AiSuggestionList? List => _list;

    internal ScrollViewer? Scroll => _scroll;

    internal SpringSizeDecorator? SizeDecorator => _size;

    /// <summary>Raised when a row is accepted (see <see cref="AcceptedEvent"/>).</summary>
    public event EventHandler<AiSuggestionAcceptedEventArgs>? Accepted
    {
        add => AddHandler(AcceptedEvent, value);
        remove => RemoveHandler(AcceptedEvent, value);
    }

    /// <summary>Raised when the active row changes (keys, hover, new rows), for the owner's <see cref="ActiveRowName"/>.</summary>
    public event EventHandler? ActiveChanged;

    /// <summary>
    /// Shows <paramref name="rows"/> (already filtered, in order) with the first one active. The same rows again (equal
    /// rows, as a new filter pass gives) keep the active row and the scroll position while the list is shown; after the
    /// popup closed they start fresh. No rows clear the list and close its popup ("no match closes the list"); the result
    /// says whether there is anything to show.
    /// </summary>
    public bool Show(AiSuggestionKind kind, IReadOnlyList<AiSuggestionRow>? rows)
    {
        rows ??= Array.Empty<AiSuggestionRow>();
        if (rows.Count == 0)
        {
            var had = _rows.Count > 0 || _active >= 0;
            _kind = kind;
            _rows = Array.Empty<AiSuggestionRow>();
            _active = -1;
            _announcedCount = -1;
            _list?.SetRows(kind, _rows, -1);
            Host?.Close();
            if (had)
            {
                ActiveChanged?.Invoke(this, EventArgs.Empty);
            }
            return false;
        }
        if (!_fresh && kind == _kind && SameRows(_rows, rows))
        {
            _rows = rows;
            return true;
        }
        _fresh = false;
        _kind = kind;
        _rows = rows;
        _active = 0;
        _list?.SetRows(kind, rows, 0);
        if (_scroll is not null)
        {
            // New results start at the top (the first row and its heading in view).
            _scroll.Offset = default;
        }
        Announce(rows.Count);
        ActiveChanged?.Invoke(this, EventArgs.Empty);
        Host?.OnRowsChanged();
        return true;
    }

    /// <summary>
    /// Moves the active row by <paramref name="delta"/> rows, wrapping around the ends, and scrolls it into view (with its
    /// group heading when it is the first of a group, and the list padding at the ends). False when there are no rows.
    /// </summary>
    public bool MoveActive(int delta)
    {
        var count = _rows.Count;
        if (count == 0)
        {
            return false;
        }
        var from = _active < 0 ? (delta > 0 ? -1 : 0) : _active;
        var next = (from + delta) % count;
        if (next < 0)
        {
            next += count;
        }
        SetActiveCore(next);
        ScrollActiveIntoView();
        return true;
    }

    /// <summary>Makes row <paramref name="index"/> active without scrolling (pointer hover). Out-of-range indexes are ignored.</summary>
    public void SetActive(int index)
    {
        if (index < 0 || index >= _rows.Count)
        {
            return;
        }
        SetActiveCore(index);
    }

    /// <summary>Raises <see cref="Accepted"/> for the active row; false when there is none.</summary>
    public bool AcceptActive()
    {
        if (_active < 0 || _active >= _rows.Count)
        {
            return false;
        }
        AcceptAt(_active);
        return true;
    }

    /// <summary>
    /// The keys the owner routes while the list is shown: Up and Down move the active row (wrapping), Enter and Tab accept
    /// it. Modified keys (Shift+Tab, Shift+Enter, Ctrl+…), Escape, Home and End are left to the owner. True when handled.
    /// </summary>
    public bool TryHandleKey(Key key, KeyModifiers modifiers)
    {
        if (_rows.Count == 0 || modifiers != KeyModifiers.None)
        {
            return false;
        }
        return key switch
        {
            Key.Up => MoveActive(-1),
            Key.Down => MoveActive(1),
            Key.Enter or Key.Tab => AcceptActive(),
            _ => false
        };
    }

    /// <summary>Accepts row <paramref name="index"/> (a click): it becomes active and <see cref="Accepted"/> is raised.</summary>
    internal void AcceptAt(int index)
    {
        if (index < 0 || index >= _rows.Count)
        {
            return;
        }
        SetActiveCore(index);
        RaiseEvent(new AiSuggestionAcceptedEventArgs(AcceptedEvent, _kind, _rows[index], index));
    }

    /// <summary>
    /// The surface height for the current rows: border, padding and rows, capped by <see cref="SurfaceMaxHeight"/>. Computed
    /// from the fixed row heights, so it is known before the list is measured (the popup chooses a side with it).
    /// </summary>
    internal double NaturalHeight
    {
        get
        {
            var titleLine = (_list?.TitleFontSize ?? 14) * AiSuggestionList.Leading;
            var hintLine = (_list?.HintFontSize ?? 12) * AiSuggestionList.Leading;
            var border = BorderThickness;
            var padding = Padding;
            var height = border.Top + border.Bottom + padding.Top + padding.Bottom +
                         AiSuggestionList.Layout(_kind, _rows, titleLine, hintLine, null);
            return Math.Min(height, SurfaceMaxHeight);
        }
    }

    /// <summary>The popup closed: the next rows start fresh (first row active, at the top) and are announced again.</summary>
    internal void OnHidden()
    {
        _announcedCount = -1;
        _fresh = true;
    }

    internal static string AccessibleName(AiSuggestionKind kind, AiSuggestionRow row)
    {
        var title = kind == AiSuggestionKind.Command ? "/" + row.Title : row.Title;
        return string.IsNullOrEmpty(row.Hint) ? title : title + ", " + row.Hint;
    }

    private void SetActiveCore(int index)
    {
        if (index == _active)
        {
            return;
        }
        _active = index;
        _list?.SetActive(index);
        ActiveChanged?.Invoke(this, EventArgs.Empty);
    }

    private void Announce(int count)
    {
        if (count == _announcedCount)
        {
            return;
        }
        _announcedCount = count;
        if (AnnounceCount is { } announce)
        {
            LastAnnouncement = announce(count);
        }
    }

    private static bool SameRows(IReadOnlyList<AiSuggestionRow> a, IReadOnlyList<AiSuggestionRow> b)
    {
        if (ReferenceEquals(a, b))
        {
            return true;
        }
        if (a.Count != b.Count)
        {
            return false;
        }
        for (var i = 0; i < a.Count; i++)
        {
            if (!Equals(a[i], b[i]))
            {
                return false;
            }
        }
        return true;
    }

    #region Scrolling

    /// <summary>Scrolls the active row into view by the shortest distance, once the list has its final layout.</summary>
    private void ScrollActiveIntoView()
    {
        if (_scroll is null || _list is null)
        {
            return;
        }
        if (_list.IsMeasureValid && _list.IsArrangeValid && _scroll.IsArrangeValid && _scroll.Viewport.Height > 0)
        {
            // Positions are final (an arrow key): scroll in the same frame, so the fill never sits outside the viewport.
            ScrollNow();
        }
        else if (!_scrollQueued)
        {
            // Inside a layout pass (new rows): scroll once it is over.
            _scrollQueued = true;
            Dispatcher.UIThread.Post(() =>
            {
                _scrollQueued = false;
                ScrollNow();
            }, DispatcherPriority.Background);
        }
    }

    private void ScrollNow()
    {
        if (_scroll is null || _list is null || _active < 0 || _active >= _list.RowViews.Count)
        {
            return;
        }
        var entries = _list.Entries;
        var at = -1;
        var heading = -1;
        for (var i = 0; i < entries.Count; i++)
        {
            if (entries[i].IsHeading)
            {
                heading++;
            }
            else if (entries[i].RowIndex == _active)
            {
                at = i;
                break;
            }
        }
        if (at < 0)
        {
            return;
        }
        // Arranged positions (layout rounding makes them differ from the entries' fractional ones). The first row of a
        // group brings its heading along; the first and last rows bring the list padding.
        var pad = _list.Margin.Top;
        var row = _list.RowViews[_active].Bounds;
        var groupFirst = at > 0 && entries[at - 1].IsHeading;
        var top = at == 0 || (at == 1 && groupFirst) ? 0
            : groupFirst ? pad + _list.HeadingViews[heading].Bounds.Y
            : pad + row.Y;
        var bottom = at == entries.Count - 1 ? _scroll.Extent.Height : pad + row.Bottom;
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

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        if (_list is not null)
        {
            _list.Owner = null;
        }
        _size = e.NameScope.Find<SpringSizeDecorator>(SizePart);
        _scroll = e.NameScope.Find<ScrollViewer>(ScrollPart);
        _list = e.NameScope.Find<AiSuggestionList>(ListPart);
        if (_list is not null)
        {
            _list.Owner = this;
            _list.SetRows(_kind, _rows, _active);
        }
        UpdateScrollMaxHeight();
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == SurfaceMaxHeightProperty || change.Property == AvailableHeightProperty ||
            change.Property == BorderThicknessProperty)
        {
            UpdateScrollMaxHeight();
        }
    }

    private void UpdateScrollMaxHeight()
    {
        if (_scroll is null)
        {
            return;
        }
        // The scroll area holds the padding (it scrolls with the rows, as in Arc), so only the border is outside it.
        var border = BorderThickness;
        var max = Math.Min(SurfaceMaxHeight, AvailableHeight) - border.Top - border.Bottom;
        _scroll.MaxHeight = Math.Max(0, max);
    }
}
