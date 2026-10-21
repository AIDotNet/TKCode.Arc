using TKCode.Arc.Motion;
using TKCode.Arc.Primitives;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using AvaloniaTextBox = Avalonia.Controls.TextBox;

namespace TKCode.Arc;

/// <summary>
/// The large search input of <see cref="SearchResults"/>: a leading glass, a clear button that pops in while there is
/// text, and a keycap with the shortcut that focuses the field (hidden while the field has focus).
/// </summary>
[TemplatePart(ClearButtonPart, typeof(FieldIconButton))]
[TemplatePart(ClearPresencePart, typeof(PresenceHost))]
[PseudoClasses(FilledPseudoClass)]
internal sealed class SearchResultsField : AvaloniaTextBox
{
    public const string ClearButtonPart = "PART_ClearButton";
    public const string ClearPresencePart = "PART_ClearPresence";
    public const string FilledPseudoClass = ":filled";

    public static readonly StyledProperty<string> ClearTextProperty =
        AvaloniaProperty.Register<SearchResultsField, string>(nameof(ClearText), "Clear search");

    public static readonly StyledProperty<string> ShortcutTextProperty =
        AvaloniaProperty.Register<SearchResultsField, string>(nameof(ShortcutText), "/");

    private FieldIconButton? _clearButton;
    private PresenceHost? _clearPresence;

    public string ClearText { get => GetValue(ClearTextProperty); set => SetValue(ClearTextProperty, value); }

    public string ShortcutText { get => GetValue(ShortcutTextProperty); set => SetValue(ShortcutTextProperty, value); }

    /// <summary>Raised after the clear button emptied the field.</summary>
    public event EventHandler? Cleared;

    /// <summary>Raised when the text was changed by typing, pasting or deleting (not by code).</summary>
    public event EventHandler? Edited;

    static SearchResultsField()
    {
        FocusAdornerProperty.OverrideDefaultValue<SearchResultsField>(null);
    }

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        if (_clearButton is not null)
        {
            _clearButton.Click -= OnClearClick;
        }
        base.OnApplyTemplate(e);
        _clearButton = e.NameScope.Find<FieldIconButton>(ClearButtonPart);
        _clearPresence = e.NameScope.Find<PresenceHost>(ClearPresencePart);
        if (_clearButton is not null)
        {
            _clearButton.Click += OnClearClick;
            AutomationProperties.SetName(_clearButton, ClearText);
        }
        UpdateFilled();
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == TextProperty)
        {
            UpdateFilled();
        }
        else if (change.Property == ClearTextProperty && _clearButton is not null)
        {
            AutomationProperties.SetName(_clearButton, ClearText);
        }
    }

    protected override void OnTextInput(TextInputEventArgs e)
    {
        base.OnTextInput(e);
        Edited?.Invoke(this, EventArgs.Empty);
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (e.Key is Key.Back or Key.Delete || (e.Key is Key.X or Key.V && (e.KeyModifiers & (KeyModifiers.Control | KeyModifiers.Meta)) != 0))
        {
            Edited?.Invoke(this, EventArgs.Empty);
        }
    }

    private void UpdateFilled()
    {
        var filled = !string.IsNullOrEmpty(Text);
        PseudoClasses.Set(FilledPseudoClass, filled);
        if (_clearPresence is not null)
        {
            _clearPresence.IsPresent = filled;
        }
    }

    private void OnClearClick(object? sender, RoutedEventArgs e)
    {
        e.Handled = true;
        SetCurrentValue(TextProperty, string.Empty);
        Focus(NavigationMethod.Pointer);
        Cleared?.Invoke(this, EventArgs.Empty);
    }
}

/// <summary>One suggestion row: a clock (recent) or a glass (suggested), the text, and a return glyph while it is the active row.</summary>
[PseudoClasses(ActivePseudoClass, RecentPseudoClass)]
internal sealed class SearchResultsSuggestionRow : TemplatedControl
{
    public const string ActivePseudoClass = ":active";
    public const string RecentPseudoClass = ":recent";

    public static readonly StyledProperty<string?> TextProperty =
        AvaloniaProperty.Register<SearchResultsSuggestionRow, string?>(nameof(Text));

    public static readonly StyledProperty<bool> IsRecentProperty =
        AvaloniaProperty.Register<SearchResultsSuggestionRow, bool>(nameof(IsRecent));

    public string? Text { get => GetValue(TextProperty); set => SetValue(TextProperty, value); }

    public bool IsRecent { get => GetValue(IsRecentProperty); set => SetValue(IsRecentProperty, value); }

    /// <summary>Position among the entries (not counting group labels).</summary>
    public int Index { get; set; }

    public bool IsActive { get; private set; }

    static SearchResultsSuggestionRow()
    {
        FocusableProperty.OverrideDefaultValue<SearchResultsSuggestionRow>(false);
    }

    public void SetActive(bool active)
    {
        if (IsActive != active)
        {
            IsActive = active;
            PseudoClasses.Set(ActivePseudoClass, active);
        }
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == IsRecentProperty)
        {
            PseudoClasses.Set(RecentPseudoClass, IsRecent);
        }
        else if (change.Property == TextProperty)
        {
            AutomationProperties.SetName(this, Text);
        }
    }
}

/// <summary>A group label of the suggestions ("Recent", "Suggested").</summary>
internal sealed class SearchResultsSuggestionLabel : TemplatedControl
{
    public static readonly StyledProperty<string?> TextProperty =
        AvaloniaProperty.Register<SearchResultsSuggestionLabel, string?>(nameof(Text));

    public string? Text { get => GetValue(TextProperty); set => SetValue(TextProperty, value); }

    static SearchResultsSuggestionLabel()
    {
        FocusableProperty.OverrideDefaultValue<SearchResultsSuggestionLabel>(false);
        IsHitTestVisibleProperty.OverrideDefaultValue<SearchResultsSuggestionLabel>(false);
    }
}

/// <summary>Arguments of <see cref="SearchResultsSuggestionList.Chosen"/>.</summary>
internal sealed class SuggestionChosenEventArgs : EventArgs
{
    public SuggestionChosenEventArgs(string text, bool isRecent)
    {
        Text = text;
        IsRecent = isRecent;
    }

    public string Text { get; }

    public bool IsRecent { get; }
}

/// <summary>
/// The suggestions under the search field: a "Recent" group and a "Suggested" group of rows, one shared highlight
/// behind the active row (the same cursor block that glides in the result list), and a return glyph on the active row.
/// Rows are pooled; <see cref="SetEntries"/> reuses them.
/// </summary>
[TemplatePart(RowsPart, typeof(StackPanel))]
[TemplatePart(CursorPart, typeof(SearchResultsCursor))]
internal sealed class SearchResultsSuggestionList : TemplatedControl
{
    public const string RowsPart = "PART_Rows";
    public const string CursorPart = "PART_Cursor";

    private readonly record struct Entry(string Text, bool IsRecent);

    private readonly List<Entry> _entries = new();
    private readonly List<SearchResultsSuggestionRow> _rowPool = new();
    private readonly List<SearchResultsSuggestionLabel> _labelPool = new();
    private readonly List<SearchResultsSuggestionRow> _rows = new();
    private StackPanel? _rowsPanel;
    private SearchResultsCursor? _cursor;
    private int _active = -1;
    private bool _placeQueued;
    private SearchResultsCursorMove _move;
    private Point _lastPointer = new(double.NaN, double.NaN);

    static SearchResultsSuggestionList()
    {
        FocusableProperty.OverrideDefaultValue<SearchResultsSuggestionList>(false);
        AutomationProperties.ControlTypeOverrideProperty.OverrideDefaultValue<SearchResultsSuggestionList>(AutomationControlType.List);
    }

    /// <summary>Raised when a row is pressed.</summary>
    public event EventHandler<SuggestionChosenEventArgs>? Chosen;

    /// <summary>The number of entries.</summary>
    public int Count => _entries.Count;

    /// <summary>The active entry, or -1.</summary>
    public int ActiveIndex => _active;

    /// <summary>The text of the active entry.</summary>
    public string? ActiveText => _active >= 0 && _active < _entries.Count ? _entries[_active].Text : null;

    /// <summary>Whether the active entry came from the recent searches.</summary>
    public bool ActiveIsRecent => _active >= 0 && _active < _entries.Count && _entries[_active].IsRecent;

    internal IReadOnlyList<SearchResultsSuggestionRow> Rows => _rows;

    internal SearchResultsCursor? CursorView => _cursor;

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        _rowsPanel = e.NameScope.Find<StackPanel>(RowsPart);
        _cursor = e.NameScope.Find<SearchResultsCursor>(CursorPart);
        _rows.Clear();
        if (_entries.Count > 0)
        {
            var snapshot = _entries.ToList();
            _entries.Clear();
            SetEntries(snapshot.Where(x => x.IsRecent).Select(x => x.Text).ToList(), snapshot.Where(x => !x.IsRecent).Select(x => x.Text).ToList(),
                _recentLabel, _suggestedLabel, _activeText);
        }
    }

    private string _recentLabel = "Recent";
    private string _suggestedLabel = "Suggested";
    private string? _activeText;

    /// <summary>
    /// Lists <paramref name="recent"/> and <paramref name="suggested"/> (each under its label, when it has entries).
    /// The entry equal to <paramref name="activeText"/> (ignoring case) is the active one; none otherwise.
    /// </summary>
    public void SetEntries(IReadOnlyList<string> recent, IReadOnlyList<string> suggested, string recentLabel, string suggestedLabel, string? activeText)
    {
        _recentLabel = recentLabel;
        _suggestedLabel = suggestedLabel;
        _activeText = activeText;
        _entries.Clear();
        foreach (var text in recent)
        {
            _entries.Add(new Entry(text, true));
        }
        foreach (var text in suggested)
        {
            _entries.Add(new Entry(text, false));
        }
        if (_rowsPanel is null)
        {
            return;
        }
        _rowsPanel.Children.Clear();
        _rows.Clear();
        var labels = 0;
        var rows = 0;
        void AddGroup(string label, bool isRecent)
        {
            var any = false;
            foreach (var entry in _entries)
            {
                if (entry.IsRecent != isRecent)
                {
                    continue;
                }
                if (!any)
                {
                    any = true;
                    if (labels >= _labelPool.Count)
                    {
                        _labelPool.Add(new SearchResultsSuggestionLabel());
                    }
                    var heading = _labelPool[labels++];
                    heading.Text = label;
                    _rowsPanel.Children.Add(heading);
                }
                if (rows >= _rowPool.Count)
                {
                    var created = new SearchResultsSuggestionRow();
                    created.PointerMoved += OnRowPointerMoved;
                    created.PointerPressed += OnRowPointerPressed;
                    _rowPool.Add(created);
                }
                var row = _rowPool[rows++];
                row.Text = entry.Text;
                row.IsRecent = entry.IsRecent;
                row.Index = _rows.Count;
                _rows.Add(row);
                _rowsPanel.Children.Add(row);
            }
        }
        AddGroup(recentLabel, true);
        AddGroup(suggestedLabel, false);
        var active = -1;
        if (activeText is { Length: > 0 })
        {
            for (var i = 0; i < _entries.Count; i++)
            {
                if (string.Equals(_entries[i].Text, activeText, StringComparison.OrdinalIgnoreCase))
                {
                    active = i;
                    break;
                }
            }
        }
        _active = -1;
        SetActive(active, SearchResultsCursorMove.Snap);
    }

    /// <summary>Moves the active entry by <paramref name="delta"/> rows (from "none", down goes to the first and up to the last); does not wrap.</summary>
    public void Move(int delta)
    {
        if (_entries.Count == 0)
        {
            return;
        }
        var next = _active < 0 ? (delta > 0 ? 0 : _entries.Count - 1) : Math.Clamp(_active + delta, 0, _entries.Count - 1);
        SetActive(next, SearchResultsCursorMove.Key);
    }

    /// <summary>Moves the active entry up; from the first entry it goes to "none" (the typed text).</summary>
    public void MoveUp()
    {
        if (_active == 0)
        {
            SetActive(-1, SearchResultsCursorMove.Key);
            return;
        }
        Move(-1);
    }

    /// <summary>Makes no entry active.</summary>
    public void ClearActive() => SetActive(-1, SearchResultsCursorMove.Snap);

    private void SetActive(int index, SearchResultsCursorMove move)
    {
        if (_active >= 0 && _active < _rows.Count)
        {
            _rows[_active].SetActive(false);
        }
        _active = index;
        if (index >= 0 && index < _rows.Count)
        {
            _rows[index].SetActive(true);
        }
        _move = move;
        if (!_placeQueued)
        {
            _placeQueued = true;
            Avalonia.Threading.Dispatcher.UIThread.Post(PlaceCursor, Avalonia.Threading.DispatcherPriority.Loaded);
        }
    }

    private void PlaceCursor()
    {
        _placeQueued = false;
        if (_cursor is null || _rowsPanel is null)
        {
            return;
        }
        if (_active < 0 || _active >= _rows.Count)
        {
            _cursor.Hide();
            return;
        }
        var row = _rows[_active];
        if (row.Bounds.Width <= 0 || row.Bounds.Height <= 0)
        {
            _cursor.Hide(animate: false);
            return;
        }
        _cursor.MoveTo(new Rect(row.Bounds.Position + _rowsPanel.Bounds.Position, row.Bounds.Size), _move);
    }

    protected override void OnSizeChanged(SizeChangedEventArgs e)
    {
        base.OnSizeChanged(e);
        if (_active >= 0 && !_placeQueued)
        {
            _move = SearchResultsCursorMove.Snap;
            _placeQueued = true;
            Avalonia.Threading.Dispatcher.UIThread.Post(PlaceCursor, Avalonia.Threading.DispatcherPriority.Loaded);
        }
    }

    private void OnRowPointerMoved(object? sender, PointerEventArgs e)
    {
        if (sender is not SearchResultsSuggestionRow row || e.Pointer.Type == PointerType.Touch)
        {
            return;
        }
        var point = e.GetPosition(this);
        if (point == _lastPointer)
        {
            return;
        }
        _lastPointer = point;
        if (row.Index != _active)
        {
            SetActive(row.Index, SearchResultsCursorMove.Pointer);
        }
    }

    private void OnRowPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (sender is SearchResultsSuggestionRow row && e.GetCurrentPoint(row).Properties.IsLeftButtonPressed && row.Index < _entries.Count)
        {
            // The field keeps focus: the row is not a tab stop and the press is handled here.
            e.Handled = true;
            var entry = _entries[row.Index];
            Chosen?.Invoke(this, new SuggestionChosenEventArgs(entry.Text, entry.IsRecent));
        }
    }
}
