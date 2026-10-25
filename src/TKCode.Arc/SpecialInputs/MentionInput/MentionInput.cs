using TKCode.Arc.DesignTokens;
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
/// The editor is a <see cref="TokenTextEditor"/>: a real multi-line <see cref="TextBox"/> (caret, selection, IME, undo,
/// clipboard) whose own text is transparent, with a mirror behind it that paints the same text with identical metrics
/// and tints the tokens. Choosing a suggestion types through the TextBox, so Ctrl/Cmd+Z undoes it. The caret never
/// rests inside a token: arrow steps hop over it, a click lands on the nearest edge, selections grow to whole tokens,
/// and one Backspace or Delete next to a token removes all of it.
/// </para>
/// <para>
/// Arc shows no visible label (the textarea is named by <c>aria-label</c>): <see cref="Label"/> only names the editor
/// for assistive technology (<c>AutomationProperties.Name</c> on the control is used when it is null).
/// </para>
/// </remarks>
[TemplatePart(ShellPart, typeof(Border))]
[TemplatePart(EditorPart, typeof(TokenTextEditor))]
[TemplatePart(LiveRegionPart, typeof(TextBlock))]
[PseudoClasses(SuggestionsOpenPseudoClass)]
public class MentionInput : TemplatedControl, ITokenTextEditorHost
{
    /// <summary>The field's border (Arc <c>.field</c>); its height springs with the text.</summary>
    public const string ShellPart = "PART_Shell";

    /// <summary>The editor (Arc's textarea), a <see cref="TokenTextEditor"/>.</summary>
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

    /// <summary>Background of channel tokens (theme: Foreground 7%).</summary>
    internal static readonly StyledProperty<IBrush?> ChannelTokenFillProperty =
        AvaloniaProperty.Register<MentionInput, IBrush?>("ChannelTokenFill");

    /// <summary>Text of channel tokens (theme: Foreground).</summary>
    internal static readonly StyledProperty<IBrush?> ChannelTokenForegroundProperty =
        AvaloniaProperty.Register<MentionInput, IBrush?>("ChannelTokenForeground");

    /// <summary>Raised after <see cref="Value"/> changed (typing, choosing, programmatic).</summary>
    public static readonly RoutedEvent<MentionInputValueChangedEventArgs> ValueChangedEvent =
        RoutedEvent.Register<MentionInput, MentionInputValueChangedEventArgs>(nameof(ValueChanged), RoutingStrategies.Bubble);

    /// <summary>Raised when a suggestion becomes a mention (Arc <c>onMentionAdd</c>).</summary>
    public static readonly RoutedEvent<MentionAddedEventArgs> MentionAddedEvent =
        RoutedEvent.Register<MentionInput, MentionAddedEventArgs>(nameof(MentionAdded), RoutingStrategies.Bubble);

    /// <summary>Raised when Enter submits (with <see cref="IsSubmitOnEnter"/>, Arc <c>onSubmit</c>).</summary>
    public static readonly RoutedEvent<MentionSubmittedEventArgs> SubmittedEvent =
        RoutedEvent.Register<MentionInput, MentionSubmittedEventArgs>(nameof(Submitted), RoutingStrategies.Bubble);

    /// <summary>The editor's token kind for people.</summary>
    internal const string PersonKind = "Person";

    /// <summary>The editor's token kind for channels.</summary>
    internal const string ChannelKind = "Channel";

    private const double PanelWidth = 272;
    private const double PanelOffsetX = 10;
    private static readonly SpringSpec s_grow = SpringSpec.FromVisual(0.4, 0);

    // Arc's two triggers (MentionInputEngine.PersonRule / ChannelRule); IsEnabled follows People / Channels.
    private readonly TokenTrigger _personTrigger = new('@', PersonKind)
    {
        AllowAfterOpeningBracket = true,
        AllowSpacesInQuery = true,
        MaxQuerySpaces = 1
    };

    private readonly TokenTrigger _channelTrigger = new('#', ChannelKind) { AllowAfterOpeningBracket = true };
    private readonly TokenStyle _channelStyle = new() { Kind = ChannelKind };
    private Border? _shell;
    private TokenTextEditor? _editor;
    private SpecialInputsLiveRegion? _live;
    private MentionValue _value = MentionValue.Empty;
    private IReadOnlyList<Mention> _mentions = MentionValue.Empty.Mentions;
    private bool _committing;
    private bool _syncing;
    private bool _quiet;
    private int _active;
    private string _lastQueryKey = string.Empty;
    private bool _pointerAway;
    private MentionInputTrigger? _trigger;
    private List<MentionInputSuggestion> _suggestions = new();
    private bool _isSuggestionsOpen;
    private string _announced = string.Empty;
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
    }

    /// <summary>Creates a mention input.</summary>
    public MentionInput()
    {
        _channelStyle.Bind(TokenStyle.FillProperty, this.GetObservable(ChannelTokenFillProperty));
        _channelStyle.Bind(TokenStyle.ForegroundProperty, this.GetObservable(ChannelTokenForegroundProperty));
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

    internal IBrush? SuggestionHighlight
    {
        get => GetValue(SuggestionHighlightProperty);
        set => SetValue(SuggestionHighlightProperty, value);
    }

    internal IBrush? ChannelTokenFill
    {
        get => GetValue(ChannelTokenFillProperty);
        set => SetValue(ChannelTokenFillProperty, value);
    }

    internal IBrush? ChannelTokenForeground
    {
        get => GetValue(ChannelTokenForegroundProperty);
        set => SetValue(ChannelTokenForegroundProperty, value);
    }

    /// <summary>The TextBox inside the editor (tests).</summary>
    internal TokenTextBox? Editor => _editor?.Box;

    /// <summary>The shared editor this control is built on.</summary>
    internal TokenTextEditor? TokenEditor => _editor;

    internal MentionInputSuggestionList? SuggestionList => _list;

    internal MentionInputSuggestionPopup? SuggestionPopup => _popup;

    internal SpecialInputsLiveRegion? LiveRegion => _live;

    internal IReadOnlyList<MentionInputSuggestion> Suggestions => _suggestions;

    internal int ActiveSuggestion => Math.Min(_active, Math.Max(0, _suggestions.Count - 1));

    /// <summary>The height the editor is springing to (content + padding, within the line limits).</summary>
    internal double TargetFieldHeight => _editor?.TargetHeight ?? -1;

    #region Public methods (Arc MentionInputHandle)

    /// <summary>Inserts <paramref name="text"/> at the caret, as if typed.</summary>
    public void Insert(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        if (_editor is { } editor)
        {
            editor.InsertText(text);
        }
        else
        {
            EditValue(text, _value.Text.Length);
        }
    }

    /// <summary>Types a trigger at the caret so the suggestions open, adding a space first when needed.</summary>
    public void OpenSuggestions(MentionKind kind)
    {
        if (_editor is { } editor)
        {
            editor.OpenTrigger(KindName(kind));
            return;
        }
        var text = _value.Text;
        var symbol = MentionSerializer.Symbol(kind).ToString();
        EditValue(text.Length > 0 && !char.IsWhiteSpace(text[^1]) ? " " + symbol : symbol, text.Length);
    }

    /// <summary>Empties the field.</summary>
    public void Clear()
    {
        SetCurrentValue(ValueProperty, MentionValue.Empty);
        _editor?.Select(0, 0);
    }

    #endregion

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        if (_editor is { } old)
        {
            old.Host = null;
            old.MirrorFactory = null;
            old.RemoveHandler(KeyDownEvent, OnEditorKeyDown);
            old.TextChanged -= OnEditorTextChanged;
            old.TokenInserted -= OnEditorTokenInserted;
            old.SubmitRequested -= OnEditorSubmitRequested;
            old.CollapseRequested -= OnEditorCollapseRequested;
            old.Triggers.Clear();
            old.TokenStyles.Clear();
        }
        _shell = e.NameScope.Find<Border>(ShellPart);
        _editor = e.NameScope.Find<TokenTextEditor>(EditorPart);
        _live = e.NameScope.Find<SpecialInputsLiveRegion>(LiveRegionPart);
        if (_editor is { } editor)
        {
            editor.Host = this;
            editor.MirrorFactory = static () => new MentionInputMirror();
            editor.RestoresTokensOnUndo = false;
            editor.HeightMotion = MotionSpec.Spring(s_grow);
            editor.Triggers.Add(_personTrigger);
            editor.Triggers.Add(_channelTrigger);
            editor.TokenStyles.Add(_channelStyle);
            // Tunnel, so the list keys run before the editor's own (token deletion, Enter) and the TextBox's.
            editor.AddHandler(KeyDownEvent, OnEditorKeyDown, RoutingStrategies.Tunnel);
            editor.TextChanged += OnEditorTextChanged;
            editor.TokenInserted += OnEditorTokenInserted;
            editor.SubmitRequested += OnEditorSubmitRequested;
            editor.CollapseRequested += OnEditorCollapseRequested;
            PushValueToEditor();
            UpdateAutomationName();
        }
    }

    protected override void OnGotFocus(FocusChangedEventArgs e)
    {
        base.OnGotFocus(e);
        if (ReferenceEquals(e.Source, this) && _editor?.Box is { } box && box.IsEffectivelyEnabled)
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
                    PushValueToEditor();
                }
                finally
                {
                    _quiet = false;
                }
            }
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
    }

    protected override AutomationPeer OnCreateAutomationPeer() => new MentionInputAutomationPeer(this);

    /// <summary>The editor forwards its accessible name to its TextBox.</summary>
    private void UpdateAutomationName()
    {
        if (_editor is null)
        {
            return;
        }
        var name = Label ?? AutomationProperties.GetName(this);
        if (string.IsNullOrEmpty(name))
        {
            _editor.ClearValue(AutomationProperties.NameProperty);
        }
        else
        {
            AutomationProperties.SetName(_editor, name);
        }
    }

    #region Editor

    internal static string KindName(MentionKind kind) => kind == MentionKind.Person ? PersonKind : ChannelKind;

    internal static MentionKind KindOf(string kind) =>
        string.Equals(kind, ChannelKind, StringComparison.Ordinal) ? MentionKind.Channel : MentionKind.Person;

    internal static TokenSpan ToSpan(Mention mention) =>
        new(KindName(mention.Kind), mention.Id, mention.Label, mention.Start, mention.End);

    internal static Mention ToMention(TokenSpan token) => new(KindOf(token.Kind), token.Id, token.Label, token.Start, token.End);

    /// <summary>Shows <see cref="Value"/> in the editor (text and tokens); its echo is not a user edit.</summary>
    private void PushValueToEditor()
    {
        if (_editor is not { } editor)
        {
            return;
        }
        var mentions = _value.Mentions;
        var tokens = new TokenSpan[mentions.Count];
        for (var i = 0; i < tokens.Length; i++)
        {
            tokens[i] = ToSpan(mentions[i]);
        }
        _syncing = true;
        try
        {
            editor.SetContent(_value.Text, tokens, refresh: false);
        }
        finally
        {
            _syncing = false;
        }
    }

    /// <summary>Arc <c>onInput</c>: the editor moved the tokens with the edit (and added a chosen one); commit them.</summary>
    private void OnEditorTextChanged(object? sender, TokenTextChangedEventArgs e)
    {
        if (_syncing)
        {
            return;
        }
        var mentions = new Mention[e.Tokens.Count];
        for (var i = 0; i < mentions.Length; i++)
        {
            mentions[i] = ToMention(e.Tokens[i]);
        }
        Commit(new MentionValue(e.Text, mentions));
    }

    private void OnEditorTokenInserted(object? sender, TokenInsertedEventArgs e) => RaiseEvent(new MentionAddedEventArgs(ToMention(e.Token)));

    private void OnEditorSubmitRequested(object? sender, TokenTextSubmitRequestedEventArgs e)
    {
        e.Handled = true;
        if (!string.IsNullOrWhiteSpace(_value.Text))
        {
            Submit();
        }
    }

    private void OnEditorCollapseRequested(object? sender, RoutedEventArgs e) => DismissSuggestions();

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

    /// <summary>Without an editor (not templated yet): replaces the end of the text directly.</summary>
    private void EditValue(string text, int at)
    {
        var current = _value;
        at = Math.Clamp(at, 0, current.Text.Length);
        var nextText = string.Concat(current.Text.AsSpan(0, at), text, current.Text.AsSpan(at));
        SetCurrentValue(ValueProperty, new MentionValue(nextText, MentionInputEngine.Reconcile(current, nextText, at)));
    }

    /// <summary>Arc <c>onKeyDown</c> for the open list: arrows move, Enter / Tab choose, Escape dismisses.</summary>
    private void OnEditorKeyDown(object? sender, KeyEventArgs e)
    {
        if (_editor is not { } editor || editor.IsComposing || !_isSuggestionsOpen)
        {
            return;
        }
        var count = _suggestions.Count;
        if (e.Key is Key.Down or Key.Up)
        {
            e.Handled = true;
            if (count > 0)
            {
                var step = e.Key == Key.Down ? 1 : -1;
                ActivateSuggestion((ActiveSuggestion + step + count) % count);
            }
            return;
        }
        if (e.Key is Key.Enter or Key.Tab && count > 0 && !e.KeyModifiers.HasFlag(KeyModifiers.Shift))
        {
            e.Handled = true;
            ChooseSuggestion(ActiveSuggestion);
            return;
        }
        if (e.Key == Key.Escape)
        {
            e.Handled = true;
            DismissSuggestions();
        }
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

    void ITokenTextEditorHost.OnEditorOperationStarting() => _pointerAway = false;

    void ITokenTextEditorHost.OnEditorFocusChanged(bool focused)
    {
        if (focused)
        {
            _pointerAway = false;
        }
    }

    /// <summary>A kind without items opens nothing (read on every refresh: the collections may change in place).</summary>
    void ITokenTextEditorHost.OnTriggerRefreshing()
    {
        _personTrigger.IsEnabled = People?.Any() == true;
        _channelTrigger.IsEnabled = Channels?.Any() == true;
    }

    void ITokenTextEditorHost.OnTriggerRefreshed() => ApplyTrigger();

    /// <summary>The editor laid its text out again: the trigger may have moved (the query wrapped onto the next line).</summary>
    void ITokenTextEditorHost.OnEditorLayoutChanged()
    {
        if (_isSuggestionsOpen && !_placementPosted)
        {
            // Place the panel once layout settles.
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

    void ITokenTextEditorHost.OnEditorScrolled()
    {
        if (_isSuggestionsOpen)
        {
            UpdatePopupPlacement();
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

    /// <summary>Arc <c>choose</c>: the editor replaces the trigger and query with the token (plus a space unless one follows).</summary>
    internal void ChooseSuggestion(int index)
    {
        if (index < 0 || index >= _suggestions.Count || _trigger is null || _editor is not { } editor)
        {
            return;
        }
        var suggestion = _suggestions[index];
        editor.InsertToken(KindName(suggestion.Kind), suggestion.Id, suggestion.Name);
    }

    /// <summary>Escape: closes the list for this trigger only.</summary>
    internal void DismissSuggestions()
    {
        if (_editor is { } editor)
        {
            editor.CancelTrigger();
        }
        else
        {
            ApplyTrigger();
        }
    }

    private void ScheduleRefresh() => _editor?.ScheduleRefresh();

    /// <summary>Has the editor derive the trigger again; the list follows in <see cref="ApplyTrigger"/>.</summary>
    private void Refresh()
    {
        if (_editor is { } editor)
        {
            editor.Refresh();
        }
        else
        {
            ApplyTrigger();
        }
    }

    /// <summary>The ranked suggestions and the open state for the editor's trigger (Arc's render).</summary>
    private void ApplyTrigger()
    {
        var focused = _editor?.Box is { IsFocused: true };
        var people = People;
        var channels = Channels;
        var trigger = _editor?.TriggerState is { } state
            ? new MentionInputTrigger(KindOf(state.Kind), state.Start, state.Query)
            : (MentionInputTrigger?)null;
        _suggestions = trigger is { } t
            ? MentionInputEngine.Suggest(t, people, channels, MaxSuggestions)
            : new List<MentionInputSuggestion>();
        var queryKey = trigger is { } k ? $"{k.Start}:{k.Query}" : string.Empty;
        if (!string.Equals(queryKey, _lastQueryKey, StringComparison.Ordinal))
        {
            _lastQueryKey = queryKey;
            _active = 0;
        }
        _trigger = trigger;
        // A sentence that simply continues after an unmatched name closes the list instead of showing an empty state.
        var open = focused && IsEffectivelyEnabled && trigger is { } tr && !_pointerAway &&
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

    /// <summary>The active suggestion is the editor's item status (forwarded to its TextBox; Arc <c>aria-activedescendant</c>).</summary>
    private void UpdateActiveDescendant()
    {
        if (_editor is null)
        {
            return;
        }
        if (_isSuggestionsOpen && _suggestions.Count > 0)
        {
            var item = _suggestions[ActiveSuggestion];
            AutomationProperties.SetItemStatus(_editor, string.IsNullOrEmpty(item.Meta) ? item.Name : $"{item.Name}, {item.Meta}");
        }
        else
        {
            _editor.ClearValue(AutomationProperties.ItemStatusProperty);
        }
    }

    private void ApplyPopup(bool open)
    {
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
        if (_editor is not null)
        {
            // The editor's TextBox reports it as its expanded state.
            _editor.SuggestionsExpanded = _isSuggestionsOpen;
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
            ClipToBounds = true,
            Width = PanelWidth,
            Child = _list
        };
        _surface.BindCornerRadius(ArcDesignTokenKind.RadiusStep18);
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
        if (_popup is null || _surface is null || _trigger is not { } trigger || _editor?.Box?.Presenter is null)
        {
            return;
        }
        var charRect = _editor.CharacterRect(trigger.Start, this);
        var origin = charRect.Position;
        var line = charRect.Height;
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
