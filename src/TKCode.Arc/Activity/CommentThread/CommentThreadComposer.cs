using System.Text.RegularExpressions;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Automation.Provider;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Threading;

namespace TKCode.Arc;

/// <summary>
/// The text field of a <see cref="CommentThreadComposer"/> (Arc's <c>.field</c> textarea: 38–160px, grows with its
/// text, 14px radius; focus strengthens the border). Keys go to the composer first (mentions, send, cancel). Exposed
/// to automation as a combo box whose list is the @mention suggestions.
/// </summary>
internal sealed class CommentThreadField : TextBox
{
    private readonly CommentThreadComposer _owner;

    public CommentThreadField(CommentThreadComposer owner)
    {
        _owner = owner;
    }

    protected override Type StyleKeyOverride => typeof(CommentThreadField);

    protected override void OnKeyDown(KeyEventArgs e)
    {
        _owner.HandleKey(e);
        if (!e.Handled)
        {
            base.OnKeyDown(e);
        }
    }

    protected override AutomationPeer OnCreateAutomationPeer() => new CommentThreadFieldPeer(this, _owner);

    /// <summary>Arc's <c>role="combobox" aria-autocomplete="list" aria-expanded</c> textarea.</summary>
    private sealed class CommentThreadFieldPeer(CommentThreadField field, CommentThreadComposer owner)
        : TextBoxAutomationPeer(field), IExpandCollapseProvider
    {
        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.ComboBox;

        public ExpandCollapseState ExpandCollapseState =>
            owner.IsSuggestionsOpen ? ExpandCollapseState.Expanded : ExpandCollapseState.Collapsed;

        public bool ShowsMenu => false;

        public void Expand() => owner.ReadQuery();

        public void Collapse() => owner.CloseSuggestions();

        protected override string? GetHelpTextCore() =>
            owner.IsSuggestionsOpen && owner.ActivePerson is { } person ? person.Name : base.GetHelpTextCore();
    }
}

/// <summary>The @mention suggestions (Arc's <c>role="listbox"</c> list), named by the thread's labels.</summary>
internal sealed class CommentThreadSuggestionList : StackPanel
{
    protected override Type StyleKeyOverride => typeof(CommentThreadSuggestionList);

    protected override AutomationPeer OnCreateAutomationPeer() => new CommentThreadSuggestionListPeer(this);

    private sealed class CommentThreadSuggestionListPeer(CommentThreadSuggestionList owner) : ControlAutomationPeer(owner)
    {
        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.List;

        protected override bool IsContentElementCore() => true;

        protected override bool IsControlElementCore() => true;
    }
}

/// <summary>One suggested person: a 20px portrait and the name; the active one takes the muted surface.</summary>
internal sealed class CommentThreadSuggestion : Border
{
    public static readonly StyledProperty<bool> IsActiveProperty =
        AvaloniaProperty.Register<CommentThreadSuggestion, bool>(nameof(IsActive));

    public bool IsActive { get => GetValue(IsActiveProperty); set => SetValue(IsActiveProperty, value); }

    private readonly Avatar _avatar;
    private readonly TextBlock _name;

    public CommentThreadSuggestion()
    {
        _avatar = ActivityAvatar.Create(20);
        _avatar.VerticalAlignment = VerticalAlignment.Center;
        _name = new TextBlock { VerticalAlignment = VerticalAlignment.Center, TextTrimming = Avalonia.Media.TextTrimming.CharacterEllipsis };
        Child = new CommentThreadStack
        {
            Role = CommentThreadStackRole.Suggestion,
            Orientation = Orientation.Horizontal,
            VerticalAlignment = VerticalAlignment.Center,
            Children = { _avatar, _name }
        };
    }

    protected override Type StyleKeyOverride => typeof(CommentThreadSuggestion);

    /// <summary>The person shown.</summary>
    public CommentAuthor? Person { get; private set; }

    public void Show(CommentAuthor person)
    {
        Person = person;
        _name.Text = person.Name;
        ActivityAvatar.Update(_avatar, person.Name, person.Avatar);
        AutomationProperties.SetName(this, person.Name);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == IsActiveProperty && ControlAutomationPeer.FromElement(this) is { } peer)
        {
            peer.RaisePropertyChangedEvent(SelectionItemPatternIdentifiers.IsSelectedProperty, change.OldValue, change.NewValue);
        }
    }

    protected override AutomationPeer OnCreateAutomationPeer() => new CommentThreadSuggestionPeer(this);

    private sealed class CommentThreadSuggestionPeer(CommentThreadSuggestion owner) : ControlAutomationPeer(owner), ISelectionItemProvider
    {
        private CommentThreadSuggestion Row => (CommentThreadSuggestion)Owner;

        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.ListItem;

        protected override bool IsContentElementCore() => true;

        protected override bool IsControlElementCore() => true;

        public bool IsSelected => Row.IsActive;

        public ISelectionProvider? SelectionContainer => null;

        public void AddToSelection()
        {
        }

        public void RemoveFromSelection()
        {
        }

        public void Select() => Row.Selected?.Invoke(Row);
    }

    /// <summary>Raised when automation selects this row.</summary>
    public Action<CommentThreadSuggestion>? Selected { get; set; }
}

/// <summary>
/// A growing text field with @mention suggestions (Arc's <c>Composer</c>). ⌘ or Ctrl + Enter sends; Escape closes the
/// suggestions, then cancels (an edit or a reply). The suggestions open inline under the field, up to five people whose
/// name (or any word of it) starts with what follows "@"; ↑ / ↓ move (wrapping), Enter or Tab inserts "@Full Name ".
/// The main composer leads with the current user's portrait; the compact one (editing) stacks Cancel / Save below.
/// </summary>
internal sealed class CommentThreadComposer : DockPanel
{
    public static readonly StyledProperty<double> ColumnGapProperty =
        AvaloniaProperty.Register<CommentThreadComposer, double>(nameof(ColumnGap), 10);

    public static readonly StyledProperty<double> RowGapProperty =
        AvaloniaProperty.Register<CommentThreadComposer, double>(nameof(RowGap), 8);

    public static readonly StyledProperty<bool> IsNarrowProperty =
        AvaloniaProperty.Register<CommentThreadComposer, bool>(nameof(IsNarrow));

    /// <summary>The compact (edit) composer; set once at construction, read by the theme.</summary>
    public static readonly StyledProperty<bool> IsCompactProperty =
        AvaloniaProperty.Register<CommentThreadComposer, bool>(nameof(IsCompact));

    public bool IsCompact => GetValue(IsCompactProperty);

    public double ColumnGap { get => GetValue(ColumnGapProperty); set => SetValue(ColumnGapProperty, value); }
    public double RowGap { get => GetValue(RowGapProperty); set => SetValue(RowGapProperty, value); }

    /// <summary>Under 420px the main composer hides the portrait.</summary>
    public bool IsNarrow { get => GetValue(IsNarrowProperty); set => SetValue(IsNarrowProperty, value); }

    private static readonly Regex s_query = new(@"(^|\s)@(\p{L}*)$", RegexOptions.CultureInvariant);

    private readonly bool _compact;
    private readonly CommentThreadField _field;
    private readonly CommentThreadGrow _suggestionsHost;
    private readonly CommentThreadSuggestionList _list;
    private readonly CommentThreadStack _actions;
    private readonly CommentThreadButton _send;
    private readonly CommentThreadButton _cancel;
    private readonly Avatar? _avatar;
    private readonly List<CommentAuthor> _suggestions = new();
    private IReadOnlyList<CommentAuthor> _people = Array.Empty<CommentAuthor>();
    private (int Start, string Term)? _query;
    private int _active;
    private bool _inserting;
    private bool _rowPressed;
    private string _initial = string.Empty;
    private Action? _cancelAction;

    public CommentThreadComposer(bool compact, CommentAuthor? leading = null)
    {
        _compact = compact;
        SetValue(IsCompactProperty, compact);
        LastChildFill = true;
        _field = new CommentThreadField(this);
        _field.PropertyChanged += OnFieldPropertyChanged;
        _field.LostFocus += OnFieldLostFocus;

        _list = new CommentThreadSuggestionList();
        _suggestionsHost = new CommentThreadGrow
        {
            EnterFade = Motion.ArcMotionPresets.Fast,
            ExitTween = Motion.ArcMotionPresets.Instant,
            Child = new CommentThreadPart { Role = CommentThreadPartRole.Suggestions, Child = _list }
        };
        _suggestionsHost.SetPresent(false);

        var fieldWrap = new StackPanel { Orientation = Orientation.Vertical, Children = { _field, _suggestionsHost } };

        _send = new CommentThreadButton { Kind = CommentThreadButtonKind.Send, IsCompact = compact, IsEnabled = false };
        _send.Click += (_, e) =>
        {
            e.Handled = true;
            Send();
        };
        _cancel = new CommentThreadButton { Kind = CommentThreadButtonKind.Ghost, IsCompact = compact, IsVisible = false };
        _cancel.Click += (_, e) =>
        {
            e.Handled = true;
            _cancelAction?.Invoke();
        };
        _actions = new CommentThreadStack
        {
            Role = CommentThreadStackRole.ComposerActions,
            Orientation = Orientation.Horizontal,
            VerticalAlignment = VerticalAlignment.Top,
            Children = { _cancel, _send }
        };

        if (compact)
        {
            SetDock(fieldWrap, Dock.Top);
            SetDock(_actions, Dock.Top);
            _actions.HorizontalAlignment = HorizontalAlignment.Right;
            Children.Add(fieldWrap);
            Children.Add(_actions);
        }
        else
        {
            if (leading is not null)
            {
                _avatar = ActivityAvatar.Create(28, leading.Name, leading.Avatar);
                SetDock(_avatar, Dock.Left);
                Children.Add(_avatar);
            }
            SetDock(_actions, Dock.Right);
            Children.Add(_actions);
            Children.Add(fieldWrap);
        }
        UpdateGaps();
    }

    protected override Type StyleKeyOverride => typeof(CommentThreadComposer);

    /// <summary>The field (tests and focus).</summary>
    public TextBox Field => _field;

    /// <summary>The send (or save) button.</summary>
    public Avalonia.Controls.Button SendButton => _send;

    /// <summary>The cancel button (visible when the composer can cancel).</summary>
    public Avalonia.Controls.Button CancelButton => _cancel;

    /// <summary>The portrait leading the main composer.</summary>
    public Avatar? Portrait => _avatar;

    /// <summary>Called with the trimmed text when the composer sends.</summary>
    public Action<string>? Submit { get; set; }

    /// <summary>Whether the @mention suggestions are open.</summary>
    public bool IsSuggestionsOpen => _query is not null && _suggestions.Count > 0;

    /// <summary>The people suggested now.</summary>
    public IReadOnlyList<CommentAuthor> Suggestions => _suggestions;

    /// <summary>Index of the active suggestion.</summary>
    public int ActiveIndex => _active;

    /// <summary>The active suggestion, when open.</summary>
    public CommentAuthor? ActivePerson => IsSuggestionsOpen ? _suggestions[Math.Clamp(_active, 0, _suggestions.Count - 1)] : null;

    /// <summary>The text in the field.</summary>
    public string Text
    {
        get => _field.Text ?? string.Empty;
        set
        {
            _field.Text = value;
            UpdateSend();
        }
    }

    /// <summary>The text an edit started from: Save stays disabled until the text differs.</summary>
    public string Initial
    {
        get => _initial;
        set
        {
            _initial = value;
            UpdateSend();
        }
    }

    /// <summary>Called by Cancel and by Escape; null hides Cancel.</summary>
    public Action? CancelAction
    {
        get => _cancelAction;
        set
        {
            _cancelAction = value;
            _cancel.IsVisible = value is not null;
        }
    }

    /// <summary>Placeholder and accessible name of the field.</summary>
    public void SetWords(string placeholder, string submit, string cancel, string mentions)
    {
        _field.PlaceholderText = placeholder;
        AutomationProperties.SetName(_field, placeholder);
        _send.Content = submit;
        AutomationProperties.SetName(_send, submit);
        _cancel.Content = cancel;
        AutomationProperties.SetName(_cancel, cancel);
        AutomationProperties.SetName(_list, mentions);
    }

    /// <summary>Who can be mentioned.</summary>
    public void SetPeople(IReadOnlyList<CommentAuthor> people)
    {
        _people = people;
        if (_query is not null)
        {
            Filter();
        }
    }

    /// <summary>Points the leading portrait at the current user.</summary>
    public void SetLeading(CommentAuthor person)
    {
        if (_avatar is not null)
        {
            ActivityAvatar.Update(_avatar, person.Name, person.Avatar);
        }
    }

    /// <summary>Focuses the field with the caret at the end (Arc <c>focus({ preventScroll })</c> + <c>setSelectionRange</c>).</summary>
    public void FocusField()
    {
        _field.Focus(NavigationMethod.Tab);
        var length = _field.Text?.Length ?? 0;
        _field.SelectionStart = length;
        _field.SelectionEnd = length;
        _field.CaretIndex = length;
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == ColumnGapProperty || change.Property == RowGapProperty || change.Property == IsNarrowProperty)
        {
            UpdateGaps();
        }
    }

    private void UpdateGaps()
    {
        if (_compact)
        {
            _actions.Margin = new Thickness(0, RowGap, 0, 0);
            return;
        }
        _actions.Margin = new Thickness(ColumnGap, 0, 0, 0);
        if (_avatar is not null)
        {
            _avatar.IsVisible = !IsNarrow;
            _avatar.Margin = new Thickness(0, 5, ColumnGap, 0);
        }
    }

    private void OnFieldPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (e.Property == TextBox.TextProperty)
        {
            UpdateSend();
            if (!_inserting)
            {
                ReadQuery();
            }
        }
        else if (e.Property == TextBox.CaretIndexProperty && !_inserting)
        {
            // Arc's onSelect: moving the caret reads the query again.
            ReadQuery();
        }
    }

    private void UpdateSend()
    {
        var text = (_field.Text ?? string.Empty).Trim();
        _send.IsEnabled = text.Length > 0 && text != _initial.Trim();
    }

    /// <summary>Reads "@term" just before the caret and opens the matching suggestions.</summary>
    public void ReadQuery()
    {
        var text = _field.Text ?? string.Empty;
        var caret = Math.Clamp(_field.CaretIndex, 0, text.Length);
        var match = s_query.Match(text[..caret]);
        _query = match.Success ? (caret - match.Groups[2].Length - 1, match.Groups[2].Value) : null;
        _active = 0;
        Filter();
    }

    private void Filter()
    {
        _suggestions.Clear();
        if (_query is { } query)
        {
            var term = query.Term.ToLowerInvariant();
            foreach (var person in _people)
            {
                var name = person.Name.ToLowerInvariant();
                if (name.StartsWith(term, StringComparison.Ordinal) ||
                    name.Split(' ').Any(part => part.StartsWith(term, StringComparison.Ordinal)))
                {
                    _suggestions.Add(person);
                    if (_suggestions.Count == 5)
                    {
                        break;
                    }
                }
            }
        }
        _active = Math.Clamp(_active, 0, Math.Max(0, _suggestions.Count - 1));
        UpdateList();
    }

    /// <summary>Closes the suggestions (Escape, blur, insert).</summary>
    public void CloseSuggestions()
    {
        _query = null;
        _suggestions.Clear();
        _active = 0;
        UpdateList();
    }

    private void UpdateList()
    {
        var open = IsSuggestionsOpen;
        var index = 0;
        foreach (var person in _suggestions)
        {
            if (index >= _list.Children.Count)
            {
                var row = new CommentThreadSuggestion { Cursor = new Cursor(StandardCursorType.Hand) };
                row.PointerPressed += OnRowPressed;
                row.PointerMoved += OnRowMoved;
                row.Selected = r => Insert(r.Person);
                _list.Children.Add(row);
            }
            var suggestion = (CommentThreadSuggestion)_list.Children[index];
            suggestion.Show(person);
            suggestion.IsActive = index == _active;
            index++;
        }
        // Rows stay while the list shrinks away; extra rows are trimmed only while it is open.
        if (open)
        {
            while (_list.Children.Count > index)
            {
                _list.Children.RemoveAt(_list.Children.Count - 1);
            }
        }
        if (open)
        {
            _suggestionsHost.Enter(animate: true);
        }
        else
        {
            _suggestionsHost.Leave(animate: true);
        }
        if (ControlAutomationPeer.FromElement(_field) is { } peer)
        {
            peer.RaisePropertyChangedEvent(ExpandCollapsePatternIdentifiers.ExpandCollapseStateProperty,
                open ? ExpandCollapseState.Collapsed : ExpandCollapseState.Expanded,
                open ? ExpandCollapseState.Expanded : ExpandCollapseState.Collapsed);
        }
    }

    private void SetActive(int index)
    {
        _active = index;
        for (var i = 0; i < _list.Children.Count; i++)
        {
            ((CommentThreadSuggestion)_list.Children[i]).IsActive = i == index;
        }
    }

    private void OnRowMoved(object? sender, PointerEventArgs e)
    {
        if (sender is CommentThreadSuggestion row && IsSuggestionsOpen)
        {
            var index = _list.Children.IndexOf(row);
            if (index >= 0 && index != _active)
            {
                SetActive(index);
            }
        }
    }

    private void OnRowPressed(object? sender, PointerPressedEventArgs e)
    {
        // Arc keeps the field focused (pointerdown preventDefault) and inserts on click.
        e.Handled = true;
        if (sender is CommentThreadSuggestion { Person: { } person } && IsSuggestionsOpen)
        {
            _rowPressed = true;
            Insert(person);
            _rowPressed = false;
        }
    }

    private void OnFieldLostFocus(object? sender, RoutedEventArgs e)
    {
        Dispatcher.UIThread.Post(() =>
        {
            if (!_rowPressed && !_field.IsFocused)
            {
                CloseSuggestions();
            }
        }, DispatcherPriority.Background);
    }

    /// <summary>Inserts "@Full Name " for <paramref name="person"/> over the query and puts the caret after it.</summary>
    public void Insert(CommentAuthor? person)
    {
        if (person is null || _query is not { } query)
        {
            return;
        }
        var text = _field.Text ?? string.Empty;
        var caret = Math.Clamp(_field.CaretIndex, query.Start, text.Length);
        var next = $"{text[..query.Start]}@{person.Name} {text[caret..]}";
        var at = query.Start + person.Name.Length + 2;
        _inserting = true;
        try
        {
            _field.Text = next;
            _field.SelectionStart = at;
            _field.SelectionEnd = at;
            _field.CaretIndex = at;
        }
        finally
        {
            _inserting = false;
        }
        CloseSuggestions();
        UpdateSend();
        if (!_field.IsFocused)
        {
            _field.Focus(NavigationMethod.Pointer);
        }
    }

    /// <summary>Sends the trimmed text (when there is any) and clears the field.</summary>
    public void Send()
    {
        var body = (_field.Text ?? string.Empty).Trim();
        if (body.Length == 0 || !_send.IsEnabled)
        {
            return;
        }
        Submit?.Invoke(body);
        _inserting = true;
        try
        {
            _field.Text = string.Empty;
        }
        finally
        {
            _inserting = false;
        }
        CloseSuggestions();
        UpdateSend();
    }

    /// <summary>Arc's <c>onKeyDown</c>: suggestions first, then ⌘/Ctrl + Enter, then Escape.</summary>
    public void HandleKey(KeyEventArgs e)
    {
        if (IsSuggestionsOpen)
        {
            switch (e.Key)
            {
                case Key.Down:
                case Key.Up:
                    e.Handled = true;
                    var step = e.Key == Key.Down ? 1 : -1;
                    SetActive((_active + step + _suggestions.Count) % _suggestions.Count);
                    return;
                case Key.Enter:
                case Key.Tab:
                    e.Handled = true;
                    Insert(_suggestions[_active]);
                    return;
                case Key.Escape:
                    e.Handled = true;
                    CloseSuggestions();
                    return;
            }
        }
        if (e.Key == Key.Enter && (e.KeyModifiers.HasFlag(KeyModifiers.Meta) || e.KeyModifiers.HasFlag(KeyModifiers.Control)))
        {
            e.Handled = true;
            Send();
            return;
        }
        if (e.Key == Key.Escape && _cancelAction is not null)
        {
            e.Handled = true;
            _cancelAction();
        }
    }
}
