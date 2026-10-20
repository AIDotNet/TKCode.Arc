using System.Windows.Input;
using TKCode.Arc.Motion;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.LogicalTree;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;

namespace TKCode.Arc;

/// <summary>
/// Arc's Mention input: an auto-growing text field with <c>@</c> people and <c>#</c> channel mentions that act as
/// single tokens, with suggestions at the caret. The value is plain text plus structured mentions with offsets
/// (<see cref="MentionValue"/>), ready to store (<see cref="MentionSerializer"/>) or to notify by id.
/// </summary>
/// <remarks>
/// <para>
/// The editor is a real multi-line <see cref="TextBox"/> (caret, selection, IME, undo, clipboard) whose own text is
/// transparent; a mirror behind it paints the same text with identical metrics and tints the tokens. Choosing a
/// suggestion types through the TextBox, so Ctrl/Cmd+Z undoes it. The caret never rests inside a token: arrow
/// steps hop over it, a click lands on the nearest edge, selections grow to whole tokens, and one Backspace or
/// Delete next to a token removes all of it.
/// </para>
/// <para>
/// Arc shows no visible label (the textarea is named by <c>aria-label</c>): <see cref="Label"/> only names the editor
/// for assistive technology (<c>AutomationProperties.Name</c> on the control is used when it is null).
/// </para>
/// </remarks>
[TemplatePart(ShellPart, typeof(Border))]
[TemplatePart(EditorPart, typeof(TextBox))]
[TemplatePart(LiveRegionPart, typeof(TextBlock))]
[PseudoClasses(SuggestionsOpenPseudoClass)]
public class MentionInput : TemplatedControl
{
    /// <summary>The field's border (Arc <c>.field</c>); its height springs with the text.</summary>
    public const string ShellPart = "PART_Shell";

    /// <summary>The editor TextBox (Arc's textarea).</summary>
    public const string EditorPart = "PART_TextBox";

    /// <summary>The hidden polite live region that announces the result count.</summary>
    public const string LiveRegionPart = "PART_LiveRegion";

    /// <summary>Set while the suggestion list is open.</summary>
    public const string SuggestionsOpenPseudoClass = ":suggestions-open";

    /// <summary>Defines the <see cref="Value"/> property.</summary>
    public static readonly StyledProperty<MentionValue> ValueProperty =
        AvaloniaProperty.Register<MentionInput, MentionValue>(nameof(Value), MentionValue.Empty,
            defaultBindingMode: BindingMode.TwoWay, coerce: static (_, value) => value ?? MentionValue.Empty);

    /// <summary>Defines the <see cref="Mentions"/> property.</summary>
    public static readonly DirectProperty<MentionInput, IReadOnlyList<Mention>> MentionsProperty =
        AvaloniaProperty.RegisterDirect<MentionInput, IReadOnlyList<Mention>>(nameof(Mentions), static o => o.Mentions);

    /// <summary>Defines the <see cref="People"/> property.</summary>
    public static readonly StyledProperty<IEnumerable<MentionPerson>?> PeopleProperty =
        AvaloniaProperty.Register<MentionInput, IEnumerable<MentionPerson>?>(nameof(People));

    /// <summary>Defines the <see cref="Channels"/> property.</summary>
    public static readonly StyledProperty<IEnumerable<MentionChannel>?> ChannelsProperty =
        AvaloniaProperty.Register<MentionInput, IEnumerable<MentionChannel>?>(nameof(Channels));

    /// <summary>Defines the <see cref="IsSubmitOnEnter"/> property.</summary>
    public static readonly StyledProperty<bool> IsSubmitOnEnterProperty =
        AvaloniaProperty.Register<MentionInput, bool>(nameof(IsSubmitOnEnter));

    /// <summary>Defines the <see cref="SubmitCommand"/> property.</summary>
    public static readonly StyledProperty<ICommand?> SubmitCommandProperty =
        AvaloniaProperty.Register<MentionInput, ICommand?>(nameof(SubmitCommand));

    /// <summary>Defines the <see cref="MinLines"/> property.</summary>
    public static readonly StyledProperty<int> MinLinesProperty =
        AvaloniaProperty.Register<MentionInput, int>(nameof(MinLines), 1, coerce: static (_, v) => Math.Max(0, v));

    /// <summary>Defines the <see cref="MaxLines"/> property.</summary>
    public static readonly StyledProperty<int> MaxLinesProperty =
        AvaloniaProperty.Register<MentionInput, int>(nameof(MaxLines), 8, coerce: static (_, v) => Math.Max(1, v));

    /// <summary>Defines the <see cref="SuggestionPlacement"/> property.</summary>
    public static readonly StyledProperty<MentionSuggestionPlacement> SuggestionPlacementProperty =
        AvaloniaProperty.Register<MentionInput, MentionSuggestionPlacement>(nameof(SuggestionPlacement));

    /// <summary>Defines the <see cref="MaxSuggestions"/> property.</summary>
    public static readonly StyledProperty<int> MaxSuggestionsProperty =
        AvaloniaProperty.Register<MentionInput, int>(nameof(MaxSuggestions), 6, coerce: static (_, v) => Math.Max(0, v));

    /// <summary>Defines the <see cref="Placeholder"/> property.</summary>
    public static readonly StyledProperty<string?> PlaceholderProperty =
        AvaloniaProperty.Register<MentionInput, string?>(nameof(Placeholder));

    /// <summary>Defines the <see cref="Label"/> property.</summary>
    public static readonly StyledProperty<string?> LabelProperty =
        AvaloniaProperty.Register<MentionInput, string?>(nameof(Label));

    /// <summary>Defines the <see cref="NoPeopleMatchFormat"/> property.</summary>
    public static readonly StyledProperty<string> NoPeopleMatchFormatProperty =
        AvaloniaProperty.Register<MentionInput, string>(nameof(NoPeopleMatchFormat), "No people match “{0}”");

    /// <summary>Defines the <see cref="NoChannelsMatchFormat"/> property.</summary>
    public static readonly StyledProperty<string> NoChannelsMatchFormatProperty =
        AvaloniaProperty.Register<MentionInput, string>(nameof(NoChannelsMatchFormat), "No channels match “{0}”");

    /// <summary>Defines the <see cref="NoMatchesText"/> property.</summary>
    public static readonly StyledProperty<string> NoMatchesTextProperty =
        AvaloniaProperty.Register<MentionInput, string>(nameof(NoMatchesText), "No matches");

    /// <summary>Defines the <see cref="PeopleListName"/> property.</summary>
    public static readonly StyledProperty<string> PeopleListNameProperty =
        AvaloniaProperty.Register<MentionInput, string>(nameof(PeopleListName), "People");

    /// <summary>Defines the <see cref="ChannelsListName"/> property.</summary>
    public static readonly StyledProperty<string> ChannelsListNameProperty =
        AvaloniaProperty.Register<MentionInput, string>(nameof(ChannelsListName), "Channels");

    /// <summary>Defines the <see cref="IsSuggestionsOpen"/> property.</summary>
    public static readonly DirectProperty<MentionInput, bool> IsSuggestionsOpenProperty =
        AvaloniaProperty.RegisterDirect<MentionInput, bool>(nameof(IsSuggestionsOpen), static o => o.IsSuggestionsOpen);

    /// <summary>Highlight behind the active suggestion (theme: Foreground 6.5%).</summary>
    internal static readonly StyledProperty<IBrush?> SuggestionHighlightProperty =
        AvaloniaProperty.Register<MentionInput, IBrush?>(nameof(SuggestionHighlight));

    /// <summary>Raised after <see cref="Value"/> changed (typing, choosing, programmatic).</summary>
    public static readonly RoutedEvent<MentionInputValueChangedEventArgs> ValueChangedEvent =
        RoutedEvent.Register<MentionInput, MentionInputValueChangedEventArgs>(nameof(ValueChanged), RoutingStrategies.Bubble);

    /// <summary>Raised when a suggestion becomes a mention (Arc <c>onMentionAdd</c>).</summary>
    public static readonly RoutedEvent<MentionAddedEventArgs> MentionAddedEvent =
        RoutedEvent.Register<MentionInput, MentionAddedEventArgs>(nameof(MentionAdded), RoutingStrategies.Bubble);

    /// <summary>Raised when Enter submits (with <see cref="IsSubmitOnEnter"/>, Arc <c>onSubmit</c>).</summary>
    public static readonly RoutedEvent<MentionSubmittedEventArgs> SubmittedEvent =
        RoutedEvent.Register<MentionInput, MentionSubmittedEventArgs>(nameof(Submitted), RoutingStrategies.Bubble);

    private const double PanelWidth = 272;
    private const double PanelOffsetX = 10;
    private static readonly SpringSpec s_grow = SpringSpec.FromVisual(0.4, 0);

    private Border? _shell;
    private MentionInputTextBox? _box;
    private SpecialInputsLiveRegion? _live;
    private MentionValue _value = MentionValue.Empty;
    private IReadOnlyList<Mention> _mentions = MentionValue.Empty.Mentions;
    private bool _committing;
    private bool _syncingText;
    private bool _quiet;
    private int _lastCaret;
    private int? _caret;
    private int? _dismissed;
    private int _active;
    private string _lastQueryKey = string.Empty;
    private Mention? _pending;
    private bool _pointerAway;
    private int _operationDepth;
    private bool _normalizing;
    private bool _refreshPosted;
    private MentionInputTrigger? _trigger;
    private List<MentionInputSuggestion> _suggestions = new();
    private bool _isSuggestionsOpen;
    private string _announced = string.Empty;
    private MotionValue<double>? _height;
    private double _heightTarget = -1;
    private bool _heightPlaced;
    private MentionInputSuggestionPopup? _popup;
    private Border? _surface;
    private MentionInputSuggestionList? _list;
    private TopLevel? _pressRoot;
    private bool _placementPosted;

    static MentionInput()
    {
        FocusableProperty.OverrideDefaultValue<MentionInput>(true);
        // Focus() lands on the editor; Tab goes straight to it.
        KeyboardNavigation.IsTabStopProperty.OverrideDefaultValue<MentionInput>(false);
        AffectsMeasure<MentionInput>(MinLinesProperty, MaxLinesProperty);
    }

    /// <summary>The text and its mentions. Two-way bindable; typing and choosing suggestions write it.</summary>
    public MentionValue Value
    {
        get => GetValue(ValueProperty);
        set => SetValue(ValueProperty, value);
    }

    /// <summary>The mentions in <see cref="Value"/>, ordered by offset.</summary>
    public IReadOnlyList<Mention> Mentions
    {
        get => _mentions;
        private set => SetAndRaise(MentionsProperty, ref _mentions, value);
    }

    /// <summary>People offered after <c>@</c>. Null or empty turns person mentions off.</summary>
    public IEnumerable<MentionPerson>? People
    {
        get => GetValue(PeopleProperty);
        set => SetValue(PeopleProperty, value);
    }

    /// <summary>Channels offered after <c>#</c>. Null or empty turns channel mentions off.</summary>
    public IEnumerable<MentionChannel>? Channels
    {
        get => GetValue(ChannelsProperty);
        set => SetValue(ChannelsProperty, value);
    }

    /// <summary>Enter submits (when the text is not blank) and Shift+Enter adds a line. Default false.</summary>
    public bool IsSubmitOnEnter
    {
        get => GetValue(IsSubmitOnEnterProperty);
        set => SetValue(IsSubmitOnEnterProperty, value);
    }

    /// <summary>Executed with the <see cref="MentionValue"/> when Enter submits.</summary>
    public ICommand? SubmitCommand
    {
        get => GetValue(SubmitCommandProperty);
        set => SetValue(SubmitCommandProperty, value);
    }

    /// <summary>Lines the field shows at least (Arc <c>minRows</c>, default 1).</summary>
    public int MinLines
    {
        get => GetValue(MinLinesProperty);
        set => SetValue(MinLinesProperty, value);
    }

    /// <summary>The field grows to this many lines, then scrolls (Arc <c>maxRows</c>, default 8).</summary>
    public int MaxLines
    {
        get => GetValue(MaxLinesProperty);
        set => SetValue(MaxLinesProperty, value);
    }

    /// <summary>Where suggestions open. Auto flips above when there is no room below.</summary>
    public MentionSuggestionPlacement SuggestionPlacement
    {
        get => GetValue(SuggestionPlacementProperty);
        set => SetValue(SuggestionPlacementProperty, value);
    }

    /// <summary>The most suggestions listed (default 6).</summary>
    public int MaxSuggestions
    {
        get => GetValue(MaxSuggestionsProperty);
        set => SetValue(MaxSuggestionsProperty, value);
    }

    /// <summary>Muted text shown while the field is empty.</summary>
    public string? Placeholder
    {
        get => GetValue(PlaceholderProperty);
        set => SetValue(PlaceholderProperty, value);
    }

    /// <summary>
    /// Accessible name of the editor (Arc <c>aria-label</c>); never shown. When null, the control's
    /// <c>AutomationProperties.Name</c> is used.
    /// </summary>
    public string? Label
    {
        get => GetValue(LabelProperty);
        set => SetValue(LabelProperty, value);
    }

    /// <summary>Empty state of the people list; <c>{0}</c> is the query.</summary>
    public string NoPeopleMatchFormat
    {
        get => GetValue(NoPeopleMatchFormatProperty);
        set => SetValue(NoPeopleMatchFormatProperty, value);
    }

    /// <summary>Empty state of the channel list; <c>{0}</c> is the query.</summary>
    public string NoChannelsMatchFormat
    {
        get => GetValue(NoChannelsMatchFormatProperty);
        set => SetValue(NoChannelsMatchFormatProperty, value);
    }

    /// <summary>Announced when the open list has no results.</summary>
    public string NoMatchesText
    {
        get => GetValue(NoMatchesTextProperty);
        set => SetValue(NoMatchesTextProperty, value);
    }

    /// <summary>Accessible name of the people list.</summary>
    public string PeopleListName
    {
        get => GetValue(PeopleListNameProperty);
        set => SetValue(PeopleListNameProperty, value);
    }

    /// <summary>Accessible name of the channel list.</summary>
    public string ChannelsListName
    {
        get => GetValue(ChannelsListNameProperty);
        set => SetValue(ChannelsListNameProperty, value);
    }

    /// <summary>True while the suggestion list is open.</summary>
    public bool IsSuggestionsOpen
    {
        get => _isSuggestionsOpen;
        private set => SetAndRaise(IsSuggestionsOpenProperty, ref _isSuggestionsOpen, value);
    }

    /// <summary>Raised after <see cref="Value"/> changed.</summary>
    public event EventHandler<MentionInputValueChangedEventArgs>? ValueChanged
    {
        add => AddHandler(ValueChangedEvent, value);
        remove => RemoveHandler(ValueChangedEvent, value);
    }

    /// <summary>Raised when a suggestion becomes a mention.</summary>
    public event EventHandler<MentionAddedEventArgs>? MentionAdded
    {
        add => AddHandler(MentionAddedEvent, value);
        remove => RemoveHandler(MentionAddedEvent, value);
    }

    /// <summary>Raised when Enter submits a non-blank value.</summary>
    public event EventHandler<MentionSubmittedEventArgs>? Submitted
    {
        add => AddHandler(SubmittedEvent, value);
        remove => RemoveHandler(SubmittedEvent, value);
    }

    internal bool IsSuggestionListOpen => _isSuggestionsOpen;

    internal IBrush? SuggestionHighlight
    {
        get => GetValue(SuggestionHighlightProperty);
        set => SetValue(SuggestionHighlightProperty, value);
    }

    internal MentionInputTextBox? Editor => _box;

    internal MentionInputSuggestionList? SuggestionList => _list;

    internal MentionInputSuggestionPopup? SuggestionPopup => _popup;

    internal SpecialInputsLiveRegion? LiveRegion => _live;

    internal IReadOnlyList<MentionInputSuggestion> Suggestions => _suggestions;

    internal int ActiveSuggestion => Math.Min(_active, Math.Max(0, _suggestions.Count - 1));

    /// <summary>The height the shell is springing to (content + padding, within the line limits).</summary>
    internal double TargetFieldHeight => _heightTarget;

    #region Public methods (Arc MentionInputHandle)

    /// <summary>Inserts <paramref name="text"/> at the caret, as if typed.</summary>
    public void Insert(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        TypeText(text, null, null);
    }

    /// <summary>Types a trigger at the caret so the suggestions open, adding a space first when needed.</summary>
    public void OpenSuggestions(MentionKind kind)
    {
        var text = _value.Text;
        var focused = _box?.IsFocused == true;
        var at = focused ? SelectionLow() : text.Length;
        var to = focused ? SelectionHigh() : at;
        var before = at > 0 && at <= text.Length ? text[at - 1] : (char?)null;
        _dismissed = null;
        var symbol = MentionSerializer.Symbol(kind).ToString();
        TypeText(before is { } c && !char.IsWhiteSpace(c) ? " " + symbol : symbol, at, to);
    }

    /// <summary>Empties the field.</summary>
    public void Clear()
    {
        SetCurrentValue(ValueProperty, MentionValue.Empty);
        _caret = 0;
        _box?.Select(0, 0);
    }

    #endregion

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        if (_box is not null)
        {
            _box.Owner = null;
        }
        _shell = e.NameScope.Find<Border>(ShellPart);
        _box = e.NameScope.Find<MentionInputTextBox>(EditorPart);
        _live = e.NameScope.Find<SpecialInputsLiveRegion>(LiveRegionPart);
        _heightPlaced = false;
        _heightTarget = -1;
        if (_box is not null)
        {
            _box.Owner = this;
            SyncEditorText(moveCaretToEnd: true);
            UpdateEditorTokens();
            UpdateAutomationName();
        }
    }

    internal void OnEditorTemplateApplied() => UpdateEditorTokens();

    protected override void OnGotFocus(FocusChangedEventArgs e)
    {
        base.OnGotFocus(e);
        if (ReferenceEquals(e.Source, this) && _box is { } box && box.IsEffectivelyEnabled)
        {
            box.Focus(e.NavigationMethod, e.KeyModifiers);
        }
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == ValueProperty)
        {
            var oldValue = change.GetOldValue<MentionValue>() ?? MentionValue.Empty;
            var newValue = change.GetNewValue<MentionValue>() ?? MentionValue.Empty;
            _value = newValue;
            Mentions = newValue.Mentions;
            if (!_committing)
            {
                _quiet = true;
                try
                {
                    SyncEditorText(moveCaretToEnd: true);
                }
                finally
                {
                    _quiet = false;
                }
            }
            UpdateEditorTokens();
            if (!oldValue.Equals(newValue))
            {
                RaiseEvent(new MentionInputValueChangedEventArgs(oldValue, newValue));
            }
            if (!_committing)
            {
                _quiet = true;
                try
                {
                    Refresh();
                }
                finally
                {
                    _quiet = false;
                }
            }
        }
        else if (change.Property == PeopleProperty || change.Property == ChannelsProperty ||
                 change.Property == MaxSuggestionsProperty || change.Property == SuggestionPlacementProperty ||
                 change.Property == IsEffectivelyEnabledProperty || change.Property == NoPeopleMatchFormatProperty ||
                 change.Property == NoChannelsMatchFormatProperty)
        {
            ScheduleRefresh();
        }
        else if (change.Property == LabelProperty || change.Property == AutomationProperties.NameProperty)
        {
            UpdateAutomationName();
        }
        else if (change.Property == SuggestionHighlightProperty && _list is not null)
        {
            _list.SetValue(MentionInputSuggestionList.HighlightFillProperty, change.GetNewValue<IBrush?>());
        }
        else if (change.Property == BoundsProperty && _isSuggestionsOpen)
        {
            UpdatePopupPlacement();
        }
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        StopOutsideWatch();
        if (_popup is { IsOpen: true } popup)
        {
            popup.IsMotionEnabled = false;
            popup.IsOpen = false;
        }
        _list?.ResetPlacement();
        IsSuggestionsOpen = false;
        PseudoClasses.Set(SuggestionsOpenPseudoClass, false);
        _heightPlaced = false;
        _heightTarget = -1;
    }

    protected override AutomationPeer OnCreateAutomationPeer() => new MentionInputAutomationPeer(this);

    private void UpdateAutomationName()
    {
        if (_box is null)
        {
            return;
        }
        var name = Label ?? AutomationProperties.GetName(this);
        if (string.IsNullOrEmpty(name))
        {
            _box.ClearValue(AutomationProperties.NameProperty);
        }
        else
        {
            AutomationProperties.SetName(_box, name);
        }
    }

    #region Autosize

    /// <summary>
    /// The shell's height follows the text between <see cref="MinLines"/> and <see cref="MaxLines"/> lines (24px each
    /// plus 22px padding) on Arc's smooth spring (0.4s, no bounce); the first measure jumps. Only the shell's height
    /// is animated, so one element relayouts per frame.
    /// </summary>
    protected override Size MeasureOverride(Size availableSize)
    {
        var size = base.MeasureOverride(availableSize);
        UpdateFieldHeight();
        return size;
    }

    /// <summary>The editor laid its text out again (typing, wrapping, a new width).</summary>
    internal void OnEditorLayoutChanged()
    {
        UpdateFieldHeight();
        if (_isSuggestionsOpen && !_placementPosted)
        {
            // The trigger may have moved (the query wrapped onto the next line); place the panel once layout settles.
            _placementPosted = true;
            Dispatcher.UIThread.Post(() =>
            {
                _placementPosted = false;
                if (_isSuggestionsOpen)
                {
                    UpdatePopupPlacement();
                }
            }, DispatcherPriority.Loaded);
        }
    }

    private void UpdateFieldHeight()
    {
        if (_box?.Presenter is not { } presenter || _shell is null)
        {
            return;
        }
        var padding = _box.Padding;
        var line = double.IsNaN(_box.LineHeight) || _box.LineHeight <= 0 ? Math.Ceiling(_box.FontSize * 1.5) : _box.LineHeight;
        var pad = padding.Top + padding.Bottom;
        var text = presenter.TextLayout.Height;
        var min = line * MinLines + pad;
        var max = line * Math.Max(MinLines, MaxLines) + pad;
        var target = Math.Clamp(text + pad, min, Math.Max(min, max));
        if (_heightPlaced && Math.Abs(target - _heightTarget) <= 0.5)
        {
            return;
        }
        _heightTarget = target;
        _height ??= CreateHeightMotion();
        if (!_heightPlaced)
        {
            _heightPlaced = true;
            _height.Jump(target);
            return;
        }
        _ = _height.AnimateTo(target, MotionSpec.Spring(s_grow));
    }

    private MotionValue<double> CreateHeightMotion()
    {
        var motion = new MotionValue<double>(this, 0, MotionVectors.Double);
        motion.Changed += value =>
        {
            if (_shell is { } shell)
            {
                shell.Height = Math.Max(0, value) + shell.BorderThickness.Top + shell.BorderThickness.Bottom;
            }
        };
        return motion;
    }

    #endregion

    #region Editor events

    private int SelectionLow() => _box is null ? _value.Text.Length : Math.Min(_box.SelectionStart, _box.SelectionEnd);

    private int SelectionHigh() => _box is null ? _value.Text.Length : Math.Max(_box.SelectionStart, _box.SelectionEnd);

    /// <summary>Before a key, text input or pointer operation: remember the caret (the edit hint, Arc <c>lastCaret</c>).</summary>
    internal void BeginEditorOperation(bool pointer = false)
    {
        _operationDepth++;
        _pointerAway = false;
        if (!pointer)
        {
            _lastCaret = SelectionLow();
        }
    }

    /// <summary>After the TextBox finished: keep the caret out of tokens (Arc <c>onSelect</c>) and refresh the list.</summary>
    internal void EndEditorOperation()
    {
        _operationDepth = Math.Max(0, _operationDepth - 1);
        if (_operationDepth > 0)
        {
            return;
        }
        NormalizeSelection();
        Refresh();
    }

    internal void OnEditorSelectionChanged()
    {
        if (_operationDepth == 0 && !_normalizing && !_syncingText)
        {
            ScheduleRefresh();
        }
    }

    internal void OnEditorFocusChanged(bool focused)
    {
        if (focused)
        {
            _pointerAway = false;
        }
        if (_operationDepth == 0)
        {
            Refresh();
        }
    }

    internal void OnEditorScrolled()
    {
        if (_isSuggestionsOpen)
        {
            UpdatePopupPlacement();
        }
    }

    /// <summary>Arc <c>onInput</c>: moves the mentions with the edit, adds the pending one and commits.</summary>
    internal void OnEditorTextChanged()
    {
        if (_syncingText || _box is null)
        {
            return;
        }
        var text = _box.Text ?? string.Empty;
        if (string.Equals(text, _value.Text, StringComparison.Ordinal))
        {
            return;
        }
        var mentions = MentionInputEngine.Reconcile(_value, text, _lastCaret);
        var added = _pending;
        _pending = null;
        var accepted = added is not null && MentionInputEngine.Holds(text, added);
        if (accepted)
        {
            mentions.Add(added!);
        }
        Commit(new MentionValue(text, mentions));
        if (accepted)
        {
            RaiseEvent(new MentionAddedEventArgs(added!));
        }
        if (_operationDepth == 0)
        {
            ScheduleRefresh();
        }
    }

    private void Commit(MentionValue next)
    {
        _committing = true;
        try
        {
            SetCurrentValue(ValueProperty, next);
        }
        finally
        {
            _committing = false;
        }
    }

    private void SyncEditorText(bool moveCaretToEnd)
    {
        if (_box is not { } box || string.Equals(box.Text ?? string.Empty, _value.Text, StringComparison.Ordinal))
        {
            return;
        }
        _syncingText = true;
        try
        {
            box.Text = _value.Text;
            if (moveCaretToEnd)
            {
                // A browser leaves the caret at the end of a programmatically set textarea value.
                box.Select(_value.Text.Length, _value.Text.Length);
            }
        }
        finally
        {
            _syncingText = false;
        }
        _lastCaret = SelectionLow();
    }

    private void UpdateEditorTokens()
    {
        if (_box is null)
        {
            return;
        }
        var mentions = _value.Mentions;
        var tokens = new MentionInputTokenSpan[mentions.Count];
        for (var i = 0; i < mentions.Count; i++)
        {
            var mention = mentions[i];
            tokens[i] = new MentionInputTokenSpan(mention.Start, Math.Max(0, mention.End - mention.Start), mention.Kind);
        }
        _box.SetTokens(tokens);
    }

    /// <summary>Arc <c>onSelect</c>: the caret never rests inside a token; selections grow to whole tokens.</summary>
    private void NormalizeSelection()
    {
        if (_box is not { } box || _normalizing)
        {
            return;
        }
        var anchor = box.SelectionStart;
        var activeEnd = box.SelectionEnd;
        var low = Math.Min(anchor, activeEnd);
        var high = Math.Max(anchor, activeEnd);
        var collapsed = low == high;
        var start = low;
        var end = high;
        foreach (var mention in _value.Mentions)
        {
            if (collapsed && start > mention.Start && start < mention.End)
            {
                var stepped = Math.Abs(start - _lastCaret) == 1;
                var forward = start > _lastCaret;
                start = end = stepped
                    ? forward ? mention.End : mention.Start
                    : start - mention.Start < mention.End - start ? mention.Start : mention.End;
            }
            else if (!collapsed)
            {
                if (start > mention.Start && start < mention.End)
                {
                    start = mention.Start;
                }
                if (end > mention.Start && end < mention.End)
                {
                    end = mention.End;
                }
            }
        }
        if (start != low || end != high)
        {
            _normalizing = true;
            try
            {
                if (collapsed)
                {
                    box.Select(start, start);
                }
                else if (anchor <= activeEnd)
                {
                    box.Select(start, end);
                }
                else
                {
                    box.Select(end, start);
                }
            }
            finally
            {
                _normalizing = false;
            }
        }
        _lastCaret = SelectionLow();
    }

    /// <summary>Arc <c>onKeyDown</c>. Returns true when the key was consumed before the TextBox saw it.</summary>
    internal bool HandleEditorKeyDown(KeyEventArgs e)
    {
        if (_box is not { } box || box.IsComposing)
        {
            return false;
        }
        if (_refreshPosted)
        {
            Refresh();
        }
        _lastCaret = SelectionLow();
        var shift = e.KeyModifiers.HasFlag(KeyModifiers.Shift);
        if (_isSuggestionsOpen)
        {
            var count = _suggestions.Count;
            if (e.Key is Key.Down or Key.Up)
            {
                e.Handled = true;
                if (count > 0)
                {
                    var step = e.Key == Key.Down ? 1 : -1;
                    ActivateSuggestion((ActiveSuggestion + step + count) % count);
                }
                return true;
            }
            if (e.Key is Key.Enter or Key.Tab && count > 0 && !shift)
            {
                e.Handled = true;
                ChooseSuggestion(ActiveSuggestion);
                return true;
            }
            if (e.Key == Key.Escape)
            {
                e.Handled = true;
                DismissSuggestions();
                return true;
            }
        }
        var low = SelectionLow();
        var high = SelectionHigh();
        if (low == high && !e.KeyModifiers.HasFlag(KeyModifiers.Meta) && e.Key is Key.Back or Key.Delete)
        {
            // Select the whole token and let the TextBox delete it: one keystroke removes it and undo restores it.
            foreach (var mention in _value.Mentions)
            {
                var hit = e.Key == Key.Back
                    ? low > mention.Start && low <= mention.End
                    : low >= mention.Start && low < mention.End;
                if (hit)
                {
                    _operationDepth++;
                    try
                    {
                        box.Select(mention.Start, mention.End);
                    }
                    finally
                    {
                        _operationDepth--;
                    }
                    break;
                }
            }
            return false;
        }
        if (e.Key == Key.Enter && IsSubmitOnEnter && !shift)
        {
            e.Handled = true;
            if (!string.IsNullOrWhiteSpace(_value.Text))
            {
                Submit();
            }
            return true;
        }
        return false;
    }

    private void Submit()
    {
        var value = _value;
        RaiseEvent(new MentionSubmittedEventArgs(value));
        if (SubmitCommand is { } command && command.CanExecute(value))
        {
            command.Execute(value);
        }
    }

    /// <summary>Arc <c>typeText</c>: types through the TextBox so the change joins its undo stack; falls back to a direct edit.</summary>
    private void TypeText(string text, int? from, int? to)
    {
        if (_box is { } box && box.IsEffectivelyEnabled && !box.IsReadOnly)
        {
            if (!box.IsFocused)
            {
                box.Focus();
            }
            _operationDepth++;
            try
            {
                if (from is { } start)
                {
                    box.Select(start, to ?? start);
                }
                _lastCaret = SelectionLow();
                var before = box.Text ?? string.Empty;
                box.TypeText(text);
                if (!string.Equals(before, box.Text ?? string.Empty, StringComparison.Ordinal) || text.Length == 0)
                {
                    return;
                }
            }
            finally
            {
                EndEditorOperation();
            }
        }
        // No editor (not templated yet, disabled or read-only): edit the value directly.
        var current = _value;
        var low = Math.Clamp(from ?? (_box is null ? current.Text.Length : SelectionLow()), 0, current.Text.Length);
        var high = Math.Clamp(to ?? (from is null && _box is not null ? SelectionHigh() : low), low, current.Text.Length);
        var nextText = string.Concat(current.Text.AsSpan(0, low), text, current.Text.AsSpan(high));
        var mentions = MentionInputEngine.Reconcile(current, nextText, low);
        var added = _pending;
        _pending = null;
        if (added is not null && MentionInputEngine.Holds(nextText, added))
        {
            mentions.Add(added);
        }
        else
        {
            added = null;
        }
        _lastCaret = low;
        SetCurrentValue(ValueProperty, new MentionValue(nextText, mentions));
        _box?.Select(low + text.Length, low + text.Length);
        if (added is not null)
        {
            RaiseEvent(new MentionAddedEventArgs(added));
        }
    }

    #endregion

    #region Suggestions

    internal void ActivateSuggestion(int index)
    {
        if (index < 0 || index >= _suggestions.Count || index == ActiveSuggestion)
        {
            return;
        }
        _active = index;
        _list?.SetActive(index);
        UpdateActiveDescendant();
    }

    /// <summary>Arc <c>choose</c>: replaces the trigger and query with the token (plus a space unless one follows).</summary>
    internal void ChooseSuggestion(int index)
    {
        if (index < 0 || index >= _suggestions.Count || _trigger is not { } trigger || _caret is not { } caret)
        {
            return;
        }
        var suggestion = _suggestions[index];
        var label = suggestion.Name;
        var token = MentionSerializer.MentionText(suggestion.Kind, label);
        _pending = new Mention(suggestion.Kind, suggestion.Id, label, trigger.Start, trigger.Start + token.Length);
        var text = _value.Text;
        var after = caret < text.Length ? text[caret] : (char?)null;
        TypeText(after is { } c && char.IsWhiteSpace(c) ? token : token + " ", trigger.Start, caret);
    }

    /// <summary>Escape: closes the list for this trigger only.</summary>
    internal void DismissSuggestions()
    {
        if (_trigger is { } trigger)
        {
            _dismissed = trigger.Start;
        }
        Refresh();
    }

    private void ScheduleRefresh()
    {
        if (_refreshPosted)
        {
            return;
        }
        _refreshPosted = true;
        Dispatcher.UIThread.Post(() =>
        {
            if (_refreshPosted)
            {
                if (_operationDepth == 0)
                {
                    NormalizeSelection();
                }
                Refresh();
            }
        });
    }

    /// <summary>Derives the trigger, the ranked suggestions and the open state from the text and caret (Arc's render).</summary>
    private void Refresh()
    {
        _refreshPosted = false;
        var box = _box;
        var focused = box is { IsFocused: true };
        _caret = focused && box!.SelectionStart == box.SelectionEnd ? box.CaretIndex : null;
        var people = People;
        var channels = Channels;
        var peopleOn = people?.Any() == true;
        var channelsOn = channels?.Any() == true;
        var trigger = _caret is { } caret
            ? MentionInputEngine.FindTrigger(_value.Text, caret, _value.Mentions, peopleOn, channelsOn)
            : null;
        _suggestions = trigger is { } t
            ? MentionInputEngine.Suggest(t, people, channels, MaxSuggestions)
            : new List<MentionInputSuggestion>();
        var queryKey = trigger is { } k ? $"{k.Start}:{k.Query}" : string.Empty;
        if (!string.Equals(queryKey, _lastQueryKey, StringComparison.Ordinal))
        {
            _lastQueryKey = queryKey;
            _active = 0;
        }
        if (_dismissed is { } dismissed && trigger?.Start != dismissed)
        {
            _dismissed = null;
        }
        _trigger = trigger;
        // A sentence that simply continues after an unmatched name closes the list instead of showing an empty state.
        var open = focused && IsEffectivelyEnabled && trigger is { } tr && tr.Start != _dismissed && !_pointerAway &&
                   (_suggestions.Count > 0 || !tr.Query.Contains(' '));
        ApplyPopup(open);
        var label = open && trigger is { } shown
            ? MentionInputEngine.CountLabel(shown.Kind, _suggestions.Count, NoMatchesText)
            : string.Empty;
        if (!string.Equals(label, _announced, StringComparison.Ordinal))
        {
            _announced = label;
            if (!_quiet || label.Length == 0)
            {
                _live?.Announce(label);
            }
        }
        UpdateActiveDescendant();
    }

    private void UpdateActiveDescendant()
    {
        if (_box is null)
        {
            return;
        }
        if (_isSuggestionsOpen && _suggestions.Count > 0)
        {
            var item = _suggestions[ActiveSuggestion];
            AutomationProperties.SetItemStatus(_box, string.IsNullOrEmpty(item.Meta) ? item.Name : $"{item.Name}, {item.Meta}");
        }
        else
        {
            _box.ClearValue(AutomationProperties.ItemStatusProperty);
        }
    }

    private void ApplyPopup(bool open)
    {
        var wasOpen = _isSuggestionsOpen;
        if (!open || _trigger is not { } trigger)
        {
            if (_popup is { IsOpen: true } popup)
            {
                popup.IsOpen = false;
            }
            _list?.ResetPlacement();
            StopOutsideWatch();
            IsSuggestionsOpen = false;
        }
        else
        {
            EnsurePopup();
            var person = trigger.Kind == MentionKind.Person;
            var empty = string.Format(System.Globalization.CultureInfo.InvariantCulture,
                person ? NoPeopleMatchFormat : NoChannelsMatchFormat, trigger.Query);
            _list!.Show(trigger.Kind, _suggestions, ActiveSuggestion, trigger.Query, empty,
                person ? PeopleListName : ChannelsListName);
            UpdatePopupPlacement();
            if (!_popup!.IsOpen)
            {
                _popup.IsOpen = true;
                StartOutsideWatch();
            }
            IsSuggestionsOpen = true;
        }
        PseudoClasses.Set(SuggestionsOpenPseudoClass, _isSuggestionsOpen);
        if (wasOpen != _isSuggestionsOpen && _box is not null &&
            ControlAutomationPeer.FromElement(_box) is MentionInputEditorAutomationPeer peer)
        {
            peer.RaiseExpandChanged(_isSuggestionsOpen);
        }
    }

    private void EnsurePopup()
    {
        if (_popup is not null)
        {
            return;
        }
        _list = new MentionInputSuggestionList { Owner = this };
        _list.SetValue(MentionInputSuggestionList.HighlightFillProperty, GetValue(SuggestionHighlightProperty));
        _surface = new Border
        {
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(18),
            ClipToBounds = true,
            Width = PanelWidth,
            Child = _list
        };
        ArcTokens.Bind(_surface, Border.BackgroundProperty, DesignTokens.ArcDesignTokenKind.SurfaceRaised);
        ArcTokens.Bind(_surface, Border.BorderBrushProperty, DesignTokens.ArcDesignTokenKind.Border);
        ArcTokens.Bind(_surface, TextElement.FontFamilyProperty, DesignTokens.ArcDesignTokenKind.BodyFontFamily);
        _popup = new MentionInputSuggestionPopup
        {
            Child = _surface,
            PlacementTarget = this
        };
        ((ISetLogicalParent)_popup).SetParent(this);
    }

    /// <summary>
    /// Hangs the panel from the trigger character (measured in the editor's own layout): x is the trigger's left edge
    /// minus 10px, clamped so the panel stays inside the field; below the line, or above it for Top, or for Auto when
    /// the room below is under 300px and the room above is larger.
    /// </summary>
    private void UpdatePopupPlacement()
    {
        if (_popup is null || _surface is null || _trigger is not { } trigger || _box?.Presenter is not { } presenter)
        {
            return;
        }
        var layout = presenter.TextLayout;
        var charRect = layout.HitTestTextPosition(Math.Clamp(trigger.Start, 0, (presenter.Text ?? string.Empty).Length));
        var origin = presenter.TranslatePoint(charRect.Position, this) ?? default;
        var line = charRect.Height > 0 ? charRect.Height : _box.LineHeight;
        var fieldWidth = Bounds.Width;
        var panelWidth = Math.Min(PanelWidth, fieldWidth);
        _surface.Width = panelWidth;
        var x = Math.Max(0, Math.Min(origin.X - PanelOffsetX, fieldWidth - panelWidth));
        var top = Math.Clamp(origin.Y, 0, Math.Max(0, Bounds.Height - line));
        var above = SuggestionPlacement == MentionSuggestionPlacement.Top;
        if (SuggestionPlacement == MentionSuggestionPlacement.Auto && TopLevel.GetTopLevel(this) is { } topLevel &&
            this.TranslatePoint(default, topLevel) is { } rootTop)
        {
            var roomBelow = topLevel.ClientSize.Height - (rootTop.Y + top + line);
            var roomAbove = rootTop.Y + top;
            above = roomBelow < 300 && roomAbove > roomBelow;
        }
        _popup.Place(above, new Rect(x, top, 1, Math.Max(1, line)));
    }

    /// <summary>A press outside the field and the panel closes the list, as the textarea's blur does in a browser.</summary>
    private void StartOutsideWatch()
    {
        if (_pressRoot is not null || TopLevel.GetTopLevel(this) is not { } root)
        {
            return;
        }
        _pressRoot = root;
        root.AddHandler(PointerPressedEvent, OnRootPointerPressed, RoutingStrategies.Tunnel, handledEventsToo: true);
    }

    private void StopOutsideWatch()
    {
        _pressRoot?.RemoveHandler(PointerPressedEvent, OnRootPointerPressed);
        _pressRoot = null;
    }

    private void OnRootPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (e.Source is Visual source &&
            (ReferenceEquals(source, this) || this.IsVisualAncestorOf(source) ||
             (_surface is not null && (ReferenceEquals(source, _surface) || _surface.IsVisualAncestorOf(source)))))
        {
            return;
        }
        _pointerAway = true;
        Refresh();
    }

    #endregion

    private sealed class MentionInputAutomationPeer : ControlAutomationPeer
    {
        public MentionInputAutomationPeer(MentionInput owner)
            : base(owner)
        {
        }

        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Group;

        protected override bool IsContentElementCore() => false;

        protected override string? GetNameCore()
        {
            var name = base.GetNameCore();
            return string.IsNullOrEmpty(name) ? ((MentionInput)Owner).Label : name;
        }
    }
}
