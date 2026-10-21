using System.Collections.Specialized;
using TKCode.Arc.Primitives;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives.PopupPositioning;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;

namespace TKCode.Arc;

public partial class SearchResults
{
    private const int MaxSuggested = 6;

    // Parts.
    private SearchResultsField? _field;
    private Panel? _fieldFrame;
    private ArcPopup? _popup;
    private Border? _suggestionSurface;
    private SearchResultsSuggestionList? _suggestionList;

    // The text was edited since the field got focus: the popup then lists only the entries that contain it.
    private bool _edited;
    private bool _suggestionsOpen;

    // Focus handed back by a closing modal is not the user asking for suggestions: the popup waits for their next input.
    private bool _quietFocus;

    #region 测试与诊断

    /// <summary>The suggestions popup (tests).</summary>
    internal ArcPopup? SuggestionsPopup => _popup;

    /// <summary>The suggestion list (tests).</summary>
    internal SearchResultsSuggestionList? SuggestionList => _suggestionList;

    /// <summary>Whether the suggestions popup is open (tests).</summary>
    internal bool AreSuggestionsOpen => _suggestionsOpen && _popup?.IsOpen == true;

    #endregion

    private void AttachField()
    {
        if (_field is not null)
        {
            _field.Text = Query;
            _field.PropertyChanged += OnFieldPropertyChanged;
            _field.AddHandler(KeyDownEvent, OnFieldKeyDown, RoutingStrategies.Tunnel);
            _field.AddHandler(PointerPressedEvent, OnFieldPointerPressed, RoutingStrategies.Tunnel, handledEventsToo: true);
            _field.GotFocus += OnFieldGotFocus;
            _field.LostFocus += OnFieldLostFocus;
            _field.Edited += OnFieldEdited;
            _field.Cleared += OnFieldCleared;
        }
        if (_popup is not null)
        {
            _popup.OverlayInputPassThroughElement = _fieldFrame;
            _popup.Closed += OnPopupClosed;
        }
        if (_suggestionList is not null)
        {
            _suggestionList.Chosen += OnSuggestionChosen;
        }
    }

    private void DetachField()
    {
        if (_field is not null)
        {
            _field.PropertyChanged -= OnFieldPropertyChanged;
            _field.RemoveHandler(KeyDownEvent, OnFieldKeyDown);
            _field.RemoveHandler(PointerPressedEvent, OnFieldPointerPressed);
            _field.GotFocus -= OnFieldGotFocus;
            _field.LostFocus -= OnFieldLostFocus;
            _field.Edited -= OnFieldEdited;
            _field.Cleared -= OnFieldCleared;
        }
        if (_popup is not null)
        {
            _popup.Closed -= OnPopupClosed;
            _popup.IsOpen = false;
        }
        if (_suggestionList is not null)
        {
            _suggestionList.Chosen -= OnSuggestionChosen;
        }
        _suggestionsOpen = false;
    }

    private void FocusSearch(NavigationMethod method, bool selectAll)
    {
        if (_field is null)
        {
            return;
        }
        // Moving focus into the field opens the suggestions; selecting is what a shortcut wants, a click wants a caret.
        _quietFocus = false;
        _field.Focus(method);
        if (selectAll)
        {
            _field.SelectAll();
        }
        else
        {
            _field.CaretIndex = _field.Text?.Length ?? 0;
            _field.ClearSelection();
        }
    }

    #region 输入框

    private void OnFieldPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (e.Property != TextBox.TextProperty || _field is null)
        {
            return;
        }
        var text = _field.Text ?? string.Empty;
        if (Query != text)
        {
            _syncingText = true;
            try
            {
                SetCurrentValue(QueryProperty, text);
            }
            finally
            {
                _syncingText = false;
            }
        }
    }

    private void OnFieldCleared(object? sender, EventArgs e)
    {
        _edited = false;
        RefreshSuggestions(keepOpen: false);
    }

    private void OnFieldGotFocus(object? sender, FocusChangedEventArgs e)
    {
        _edited = false;
        if (_quietFocus)
        {
            return;
        }
        OpenSuggestions();
    }

    private void OnFieldLostFocus(object? sender, RoutedEventArgs e)
    {
        // Moving to the popup is not possible (its rows are not focusable), so losing focus means leaving.
        Dispatcher.UIThread.Post(() =>
        {
            if (_field is { IsFocused: false })
            {
                CloseSuggestions();
            }
        }, DispatcherPriority.Input);
    }

    private void OnFieldEdited(object? sender, EventArgs e)
    {
        _edited = true;
        // The text is updated after the key; look at it then.
        Dispatcher.UIThread.Post(() => RefreshSuggestions(keepOpen: false), DispatcherPriority.Input);
    }

    private void OnFieldPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        _quietFocus = false;
        // A press in a focused field whose popup was closed (Escape) opens it again.
        if (_field is { IsFocused: true } && !_suggestionsOpen)
        {
            OpenSuggestions();
        }
    }

    private void OnFieldKeyDown(object? sender, KeyEventArgs e)
    {
        _quietFocus = false;
        if (IsLoading && e.Key is Key.Down)
        {
            return;
        }
        var open = _suggestionsOpen && _suggestionList is { Count: > 0 };
        if (e.KeyModifiers != KeyModifiers.None)
        {
            return;
        }
        switch (e.Key)
        {
            case Key.Down when open:
                e.Handled = true;
                _suggestionList!.Move(1);
                return;
            case Key.Up when open:
                e.Handled = true;
                _suggestionList!.MoveUp();
                return;
            case Key.Down:
                if (FirstEnabledRow() is var first and >= 0)
                {
                    e.Handled = true;
                    FocusList(first, NavigationMethod.Directional);
                }
                return;
            case Key.Enter:
                e.Handled = true;
                if (open)
                {
                    // The popup is for choosing a query: Enter takes the active row, or the text as typed.
                    if (_suggestionList!.ActiveText is { } picked)
                    {
                        PickSuggestion(picked);
                    }
                    else
                    {
                        SubmitQuery(Query);
                        CloseSuggestions();
                    }
                }
                else if ((_cursor >= 0 && _rows[_cursor].IsEnabled ? _cursor : FirstEnabledRow()) is var target and >= 0 && _rows[target].Item is { } item)
                {
                    Invoke(item);
                }
                else
                {
                    SubmitQuery(Query);
                }
                return;
            case Key.Escape:
                if (_suggestionsOpen)
                {
                    e.Handled = true;
                    CloseSuggestions();
                }
                else if (!string.IsNullOrEmpty(Query))
                {
                    e.Handled = true;
                    SetCurrentValue(QueryProperty, string.Empty);
                }
                return;
        }
    }

    #endregion

    #region 建议与最近搜索

    /// <summary>The recent searches and suggestions the popup lists now.</summary>
    private void CollectSuggestions(List<string> recent, List<string> suggested)
    {
        var text = (_field?.Text ?? string.Empty).Trim();
        var filter = _edited && text.Length > 0;
        foreach (var entry in Recent)
        {
            if (!string.IsNullOrWhiteSpace(entry) && (!filter || entry.Contains(text, StringComparison.CurrentCultureIgnoreCase)))
            {
                recent.Add(entry);
            }
        }
        if (Suggestions is { } suggestions)
        {
            foreach (var entry in suggestions)
            {
                if (suggested.Count >= MaxSuggested)
                {
                    break;
                }
                if (string.IsNullOrWhiteSpace(entry) || (filter && !entry.Contains(text, StringComparison.CurrentCultureIgnoreCase)) ||
                    recent.Exists(r => string.Equals(r, entry, StringComparison.CurrentCultureIgnoreCase)))
                {
                    continue;
                }
                suggested.Add(entry);
            }
        }
    }

    private void OpenSuggestions()
    {
        if (!IsSuggestionsEnabled || _popup is null || _fieldFrame is null || _suggestionList is null || TopLevel.GetTopLevel(this) is null ||
            OpenedItem is not null)
        {
            return;
        }
        RefreshSuggestions(keepOpen: false, open: true);
    }

    private void RefreshSuggestions(bool keepOpen, bool open = false)
    {
        if (_popup is null || _suggestionList is null || _fieldFrame is null)
        {
            return;
        }
        if (!keepOpen && !open && !_suggestionsOpen && !_edited)
        {
            return;
        }
        if (!IsSuggestionsEnabled || _field is not { IsFocused: true } || OpenedItem is not null)
        {
            CloseSuggestions();
            return;
        }
        var recent = new List<string>();
        var suggested = new List<string>();
        CollectSuggestions(recent, suggested);
        if (recent.Count + suggested.Count == 0)
        {
            CloseSuggestions();
            return;
        }
        // The entry that is the query shows as the active row (with its return glyph): Enter runs it either way.
        _suggestionList.SetEntries(recent, suggested, RecentLabel, SuggestedLabel, _edited ? null : Query);
        if (_suggestionSurface is not null && _fieldFrame.Bounds.Width > 0)
        {
            _suggestionSurface.Width = _fieldFrame.Bounds.Width;
        }
        _popup.PlacementTarget = _fieldFrame;
        if (!_popup.IsOpen)
        {
            try
            {
                _popup.IsOpen = true;
            }
            catch (InvalidOperationException)
            {
                // No popup host (a window without an overlay layer, as in some headless hosts): the field still works.
                return;
            }
        }
        _suggestionsOpen = true;
    }

    private void CloseSuggestions()
    {
        _suggestionsOpen = false;
        if (_popup is { IsOpen: true })
        {
            _popup.IsOpen = false;
        }
        _suggestionList?.ClearActive();
    }

    private void OnPopupClosed(object? sender, EventArgs e) => _suggestionsOpen = false;

    private void OnSuggestionChosen(object? sender, SuggestionChosenEventArgs e) => PickSuggestion(e.Text);

    private void PickSuggestion(string text)
    {
        _edited = false;
        CloseSuggestions();
        SetCurrentValue(QueryProperty, text);
        if (_field is not null)
        {
            _field.CaretIndex = text.Length;
        }
        SubmitQuery(text);
        FocusSearch(NavigationMethod.Pointer, selectAll: false);
    }

    /// <summary>Puts <paramref name="query"/> at the top of <see cref="Recent"/> and (with <paramref name="raise"/>) raises <see cref="QuerySubmitted"/>.</summary>
    private void SubmitQuery(string? query, bool raise = true)
    {
        var text = (query ?? string.Empty).Trim();
        if (text.Length == 0)
        {
            return;
        }
        for (var i = Recent.Count - 1; i >= 0; i--)
        {
            if (string.Equals(Recent[i], text, StringComparison.CurrentCultureIgnoreCase))
            {
                Recent.RemoveAt(i);
            }
        }
        Recent.Insert(0, text);
        TrimRecent();
        if (raise)
        {
            RaiseEvent(new SearchQuerySubmittedEventArgs(QuerySubmittedEvent, text));
        }
    }

    private void TrimRecent()
    {
        var max = Math.Max(0, MaxRecent);
        while (Recent.Count > max)
        {
            Recent.RemoveAt(Recent.Count - 1);
        }
    }

    private void OnRecentChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (_suggestionsOpen)
        {
            RefreshSuggestions(keepOpen: true);
        }
    }

    #endregion

    #region 键盘

    private void OnTunnelKeyDown(object? sender, KeyEventArgs e) => _keyboardInput = true;

    private void OnTunnelPointerPressed(object? sender, PointerPressedEventArgs e) => _keyboardInput = false;

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (e.Handled)
        {
            return;
        }
        var command = (e.KeyModifiers & (KeyModifiers.Control | KeyModifiers.Meta)) != 0 && (e.KeyModifiers & KeyModifiers.Alt) == 0;
        if (e.Key == Key.F && command)
        {
            e.Handled = true;
            FocusSearch(NavigationMethod.Tab, selectAll: true);
        }
    }

    protected override void OnTextInput(TextInputEventArgs e)
    {
        base.OnTextInput(e);
        if (e.Handled || e.Text != "/" || _field is null || IsInField(e.Source))
        {
            return;
        }
        e.Handled = true;
        FocusSearch(NavigationMethod.Tab, selectAll: true);
    }

    private bool IsInField(object? source)
    {
        for (var visual = source as Visual; visual is not null; visual = visual.GetVisualParent())
        {
            if (ReferenceEquals(visual, _field))
            {
                return true;
            }
            if (ReferenceEquals(visual, this))
            {
                return false;
            }
        }
        return false;
    }

    #endregion
}
