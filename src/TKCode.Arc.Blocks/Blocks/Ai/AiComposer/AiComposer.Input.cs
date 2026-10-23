using System.Collections.Specialized;
using System.Text;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;

namespace TKCode.Arc;

public partial class AiComposer
{
    /// <summary>The editor's token kind of commands ("/").</summary>
    internal const string CommandKind = "Command";

    /// <summary>The editor's token kind of mentions ("@").</summary>
    internal const string MentionKind = "Mention";

    private TokenTrigger? _commandTrigger;
    private TokenTrigger? _mentionTrigger;
    private AiSuggestionPopup? _suggest;
    // Text and Value mirror the editor; a change the editor reports must not be pushed back into it.
    private bool _fromEditor;

    #region Public methods

    /// <summary>Puts the keyboard focus in the text area.</summary>
    public void FocusInput() => FocusInput(NavigationMethod.Unspecified);

    /// <summary>
    /// Inserts a command or a mention as a token at the caret (over an open "/" or "@" query of the same kind), followed by
    /// a space; the caret ends after it.
    /// </summary>
    /// <param name="kind">Command ("/") or mention ("@").</param>
    /// <param name="id">The id of the <see cref="AiCommand"/> or <see cref="AiContextItem"/>, reported in the token.</param>
    /// <param name="label">The name the token shows after the "/" or "@".</param>
    public void InsertToken(AiComposerTokenKind kind, string id, string label)
    {
        ArgumentNullException.ThrowIfNull(id);
        ArgumentNullException.ThrowIfNull(label);
        var name = KindName(kind);
        if (_editor is { } editor)
        {
            if (editor.TriggerState?.Kind == name)
            {
                editor.InsertToken(name, id, label);
            }
            else
            {
                editor.InsertTokenAtCaret(name, id, label);
            }
            return;
        }
        // No template yet: the token goes at the end of the value.
        var value = Value;
        var text = value.Text;
        var lead = text.Length > 0 && !char.IsWhiteSpace(text[^1]) ? " " : string.Empty;
        var start = text.Length + lead.Length;
        var token = new AiPromptToken(kind, id, label, start, start + 1 + label.Length);
        var tokens = new List<AiPromptToken>(value.Tokens) { token };
        SetCurrentValue(ValueProperty, new AiComposerValue(text + lead + token.Text + " ", tokens));
    }

    /// <summary>
    /// Replaces the prompt with <paramref name="prompt"/> (a suggestion the person picked): a known command at the start of
    /// a line ("/recap") and a known item after "@" ("@Sprint 15 review") become tokens. The caret goes to the end and the
    /// text area takes the focus. Attachments, tools and the model stay.
    /// </summary>
    /// <param name="prompt">The prompt text.</param>
    public void SetPrompt(string prompt)
    {
        ArgumentNullException.ThrowIfNull(prompt);
        var value = Tokenize(prompt);
        CloseSuggestions(dismiss: false, immediate: true);
        if (_editor is { } editor)
        {
            editor.SetTextWithTokens(value.Text, ToSpans(value.Tokens));
            FocusInput();
        }
        else
        {
            SetCurrentValue(ValueProperty, value);
        }
    }

    /// <summary>
    /// Empties the prompt: its text, its tokens and its attachments (each raises <see cref="AttachmentRemoved"/> and running
    /// uploads are cancelled). The tools, the model and the effort stay as they are.
    /// </summary>
    public void Clear()
    {
        CloseSuggestions(dismiss: false, immediate: true);
        if (_editor is { } editor)
        {
            editor.Clear();
        }
        else
        {
            SetCurrentValue(ValueProperty, AiComposerValue.Empty);
        }
        RemoveAllAttachments(raiseEvents: true);
    }

    #endregion

    #region Editor

    internal bool FocusInput(NavigationMethod method) => _editor is { } editor && editor.Focus(method);

    private void HookEditor()
    {
        if (_editor is not { } editor)
        {
            return;
        }
        editor.TextChanged += OnEditorTextChanged;
        editor.TriggerChanged += OnEditorTriggerChanged;
        editor.SubmitRequested += OnEditorSubmitRequested;
        editor.CollapseRequested += OnEditorCollapseRequested;
        // On the editor, after its own tunnel handler: a refresh still queued has run, so the open query is current.
        editor.AddHandler(KeyDownEvent, OnEditorKeyDown, RoutingStrategies.Tunnel);
        editor.AddHandler(TextBox.PastingFromClipboardEvent, OnEditorPasting);
    }

    private void UnhookEditor()
    {
        if (_editor is not { } editor)
        {
            return;
        }
        editor.TextChanged -= OnEditorTextChanged;
        editor.TriggerChanged -= OnEditorTriggerChanged;
        editor.SubmitRequested -= OnEditorSubmitRequested;
        editor.CollapseRequested -= OnEditorCollapseRequested;
        editor.RemoveHandler(KeyDownEvent, OnEditorKeyDown);
        editor.RemoveHandler(TextBox.PastingFromClipboardEvent, OnEditorPasting);
    }

    /// <summary>
    /// The editor's triggers: "/" only as the first character of a line; "@" at a line start or after whitespace (not
    /// after "(") and without spaces in the query. A trigger without anything to list is off but still holds its symbol.
    /// </summary>
    private void SetUpEditor()
    {
        if (_editor is not { } editor)
        {
            return;
        }
        _commandTrigger = new TokenTrigger('/', CommandKind) { Position = TokenTriggerPosition.LineStart };
        _mentionTrigger = new TokenTrigger('@', MentionKind) { Position = TokenTriggerPosition.AfterWhitespaceOrLineStart };
        editor.Triggers.Clear();
        editor.Triggers.Add(_commandTrigger);
        editor.Triggers.Add(_mentionTrigger);
        var value = Value;
        editor.SetTextWithTokens(value.Text, ToSpans(value.Tokens));
    }

    /// <summary>"/" and "@" open their lists while there is something to list, and never in the chat's single row.</summary>
    private void UpdateTriggers()
    {
        if (_commandTrigger is not null)
        {
            _commandTrigger.IsEnabled = !IsInline && Commands.Count > 0;
        }
        if (_mentionTrigger is not null)
        {
            _mentionTrigger.IsEnabled = !IsInline && ContextItems.Count > 0;
        }
    }

    /// <summary>The line limits; the chat's single row stops at 7 lines (180) unless <see cref="MaxLines"/> was set.</summary>
    private void UpdateEditorLimits()
    {
        if (_editor is not { } editor)
        {
            return;
        }
        editor.MinLines = MinLines;
        editor.MaxLines = IsInline && !IsSet(MaxLinesProperty) ? InlineMaxLines : MaxLines;
    }

    private void OnEditorTextChanged(object? sender, TokenTextChangedEventArgs e)
    {
        var was = _fromEditor;
        _fromEditor = true;
        try
        {
            SetCurrentValue(TextProperty, e.Text);
            if (!SameValue(Value, e.Text, e.Tokens))
            {
                SetCurrentValue(ValueProperty, new AiComposerValue(e.Text, ToTokens(e.Tokens)));
            }
        }
        finally
        {
            _fromEditor = was;
        }
        UpdateCounter();
        UpdateSendState();
    }

    /// <summary>Code set <see cref="Text"/>: the editor takes it (tokens the edit did not touch stay).</summary>
    private void OnTextChanged(string text)
    {
        if (_fromEditor)
        {
            return;
        }
        if (_editor is { } editor)
        {
            editor.SetCurrentValue(TokenTextEditor.TextProperty, text);
        }
        else
        {
            _fromEditor = true;
            try
            {
                SetCurrentValue(ValueProperty, Reconcile(Value, text));
            }
            finally
            {
                _fromEditor = false;
            }
        }
        UpdateCounter();
        UpdateSendState();
    }

    /// <summary>Code set <see cref="Value"/>: the editor takes its text and tokens.</summary>
    private void OnValueChanged(AiComposerValue value)
    {
        if (_fromEditor)
        {
            return;
        }
        if (_editor is { } editor)
        {
            editor.SetTextWithTokens(value.Text, ToSpans(value.Tokens));
        }
        else
        {
            _fromEditor = true;
            try
            {
                SetCurrentValue(TextProperty, value.Text);
            }
            finally
            {
                _fromEditor = false;
            }
        }
        UpdateCounter();
        UpdateSendState();
    }

    /// <summary>Enter: send, unless a list is open (the list took Enter already) or the prompt cannot be sent.</summary>
    private void OnEditorSubmitRequested(object? sender, TokenTextSubmitRequestedEventArgs e)
    {
        e.Handled = true;
        if (_suggest is { IsOpen: true })
        {
            return;
        }
        TrySend();
    }

    /// <summary>
    /// The list's keys while it shows (the text area keeps the focus): Up and Down move the active row, Enter and Tab
    /// insert it, Escape closes the list and keeps what was typed. Without a list Escape stops a reply being generated.
    /// </summary>
    private void OnEditorKeyDown(object? sender, KeyEventArgs e)
    {
        if (_editor is not { } editor || editor.IsComposing)
        {
            return;
        }
        if (_suggest is { IsOpen: true } suggest)
        {
            if (suggest.Pop.TryHandleKey(e.Key, e.KeyModifiers))
            {
                e.Handled = true;
                return;
            }
            if (e.Key == Key.Escape)
            {
                e.Handled = true;
                CloseSuggestions(dismiss: true, immediate: false);
            }
            return;
        }
        if (e.Key == Key.Escape && e.KeyModifiers == KeyModifiers.None && IsGenerating)
        {
            e.Handled = true;
            RequestStop();
        }
    }

    private void OnEditorCollapseRequested(object? sender, RoutedEventArgs e) => CloseSuggestions(dismiss: true, immediate: false);

    #endregion

    #region "/" and "@" lists

    private AiSuggestionPopup Suggest
    {
        get
        {
            if (_suggest is null)
            {
                _suggest = new AiSuggestionPopup { HorizontalShift = SuggestionShift };
                _suggest.Pop.CommandListName = Labels.CommandList;
                _suggest.Pop.MentionListName = Labels.MentionList;
                _suggest.Pop.AnnounceCount = count =>
                {
                    var text = Labels.AnnounceSuggestions(count);
                    Announce(text);
                    return text;
                };
                _suggest.Pop.Accepted += OnSuggestionAccepted;
                _suggest.Pop.ActiveChanged += OnSuggestionActiveChanged;
                _suggest.Closed += OnSuggestionsClosed;
            }
            return _suggest;
        }
    }

    /// <summary>The "/" or "@" list (tests).</summary>
    internal AiSuggestionPopup? Suggestions => _suggest;

    /// <summary>The headings of the "@" list in order: <see cref="MentionGroups"/>, or "Files" then "Docs".</summary>
    private IReadOnlyList<string> EffectiveMentionGroups =>
        MentionGroups.Count > 0 ? MentionGroups : new[] { Labels.FilesGroup, Labels.DocsGroup };

    private void OnEditorTriggerChanged(object? sender, TokenTriggerChangedEventArgs e) => UpdateSuggestions(e.State);

    /// <summary>Filters the rows for the open query and shows the list above its line, or closes it (no query, no match).</summary>
    private void UpdateSuggestions(TokenTriggerState? state)
    {
        if (state is null || _editor is not { } editor || _surface is not { } surface || !IsEffectivelyEnabled)
        {
            CloseSuggestions(dismiss: false, immediate: false);
            return;
        }
        var suggest = Suggest;
        var kind = state.Kind == CommandKind ? AiSuggestionKind.Command : AiSuggestionKind.Mention;
        var rows = kind == AiSuggestionKind.Command
            ? AiSuggestionFilter.Commands(Commands, state.Query)
            : AiSuggestionFilter.Mentions(ContextItems, EffectiveMentionGroups, state.Query);
        if (!suggest.Pop.Show(kind, rows))
        {
            // No match: Show closed the list.
            SetSuggestionsExpanded(false);
            return;
        }
        var origin = editor.TranslatePoint(state.TriggerRect.TopLeft, surface) ?? state.TriggerRect.TopLeft;
        var shown = suggest.Open(surface, new Rect(origin, state.TriggerRect.Size));
        SetSuggestionsExpanded(shown);
    }

    /// <summary>The rows again (the sources or the labels changed), when a query is open.</summary>
    private void RefreshSuggestions()
    {
        if (_editor?.TriggerState is { } state)
        {
            UpdateSuggestions(state);
        }
    }

    /// <summary>
    /// Closes the list. <paramref name="dismiss"/> (Escape) also tells the editor the query was dismissed, so it stays
    /// closed until the caret enters another trigger; the typed text stays.
    /// </summary>
    private void CloseSuggestions(bool dismiss, bool immediate)
    {
        if (dismiss && _editor is { TriggerState: not null } editor)
        {
            editor.CancelTrigger();
        }
        _suggest?.Close(immediate);
        SetSuggestionsExpanded(false);
    }

    private void SetSuggestionsExpanded(bool expanded)
    {
        if (_editor is not { } editor)
        {
            return;
        }
        editor.SuggestionsExpanded = expanded;
        var status = expanded ? _suggest?.Pop.ActiveRowName : null;
        if (string.IsNullOrEmpty(status))
        {
            editor.ClearValue(AutomationProperties.ItemStatusProperty);
        }
        else
        {
            AutomationProperties.SetItemStatus(editor, status);
        }
    }

    private void OnSuggestionAccepted(object? sender, AiSuggestionAcceptedEventArgs e)
    {
        e.Handled = true;
        _editor?.InsertToken(e.Kind == AiSuggestionKind.Command ? CommandKind : MentionKind, e.Row.Id, e.Row.Title);
    }

    /// <summary>The active row is the text area's item status (Arc's <c>aria-activedescendant</c>).</summary>
    private void OnSuggestionActiveChanged(object? sender, EventArgs e) => SetSuggestionsExpanded(_suggest is { IsOpen: true });

    private void OnSuggestionsClosed(object? sender, EventArgs e) => SetSuggestionsExpanded(false);

    private void OnSuggestionSourceChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        UpdateTriggers();
        RefreshSuggestions();
    }

    /// <summary>The surface grew or shrank (more lines, a chip row): an open list moves with its line.</summary>
    private void OnSurfaceSizeChanged(object? sender, SizeChangedEventArgs e)
    {
        if (_suggest is { IsOpen: true })
        {
            RefreshSuggestions();
        }
    }

    #endregion

    #region Values and tokens

    private static string KindName(AiComposerTokenKind kind) => kind == AiComposerTokenKind.Command ? CommandKind : MentionKind;

    private static AiComposerTokenKind KindOf(string kind) =>
        string.Equals(kind, CommandKind, StringComparison.Ordinal) ? AiComposerTokenKind.Command : AiComposerTokenKind.Mention;

    private static IReadOnlyList<AiPromptToken> ToTokens(IReadOnlyList<TokenSpan> spans)
    {
        if (spans.Count == 0)
        {
            return Array.Empty<AiPromptToken>();
        }
        var tokens = new AiPromptToken[spans.Count];
        for (var i = 0; i < tokens.Length; i++)
        {
            var span = spans[i];
            tokens[i] = new AiPromptToken(KindOf(span.Kind), span.Id, span.Label, span.Start, span.End);
        }
        return tokens;
    }

    private static List<TokenSpan> ToSpans(IReadOnlyList<AiPromptToken> tokens)
    {
        var spans = new List<TokenSpan>(tokens.Count);
        foreach (var token in tokens)
        {
            spans.Add(new TokenSpan(KindName(token.Kind), token.Id, token.Label, token.Start, token.End));
        }
        return spans;
    }

    private static bool SameValue(AiComposerValue value, string text, IReadOnlyList<TokenSpan> spans)
    {
        if (!string.Equals(value.Text, text, StringComparison.Ordinal) || value.Tokens.Count != spans.Count)
        {
            return false;
        }
        for (var i = 0; i < spans.Count; i++)
        {
            var token = value.Tokens[i];
            var span = spans[i];
            if (token.Kind != KindOf(span.Kind) || token.Id != span.Id || token.Label != span.Label || token.Start != span.Start ||
                token.End != span.End)
            {
                return false;
            }
        }
        return true;
    }

    /// <summary>Without an editor: the tokens whose text still stands at their place stay, the others dissolve.</summary>
    private static AiComposerValue Reconcile(AiComposerValue value, string text)
    {
        var kept = new List<AiPromptToken>();
        foreach (var token in value.Tokens)
        {
            if (token.Start >= 0 && token.End <= text.Length && token.End > token.Start &&
                string.CompareOrdinal(text, token.Start, token.Text, 0, token.End - token.Start) == 0 &&
                token.Text.Length == token.End - token.Start)
            {
                kept.Add(token);
            }
        }
        return new AiComposerValue(text, kept);
    }

    /// <summary>
    /// Turns the known commands ("/" + <see cref="AiCommand.Name"/> at the start of a line) and mentions ("@" +
    /// <see cref="AiContextItem.Label"/> at a line start or after whitespace) of <paramref name="prompt"/> into tokens;
    /// the longest name wins and must end at whitespace, punctuation or the end. Names compare ignoring case and are
    /// written as declared.
    /// </summary>
    internal AiComposerValue Tokenize(string prompt)
    {
        var text = new StringBuilder(prompt.Length);
        var tokens = new List<AiPromptToken>();
        var i = 0;
        while (i < prompt.Length)
        {
            var c = prompt[i];
            var before = i > 0 ? prompt[i - 1] : '\n';
            if (c == '/' && before == '\n' && MatchCommand(prompt, i + 1) is { } command)
            {
                var start = text.Length;
                text.Append('/').Append(command.Name);
                tokens.Add(new AiPromptToken(AiComposerTokenKind.Command, command.Id, command.Name, start, text.Length));
                i += 1 + command.Name.Length;
                continue;
            }
            if (c == '@' && char.IsWhiteSpace(before) && MatchMention(prompt, i + 1) is { } item)
            {
                var start = text.Length;
                text.Append('@').Append(item.Label);
                tokens.Add(new AiPromptToken(AiComposerTokenKind.Mention, item.Id, item.Label, start, text.Length));
                i += 1 + item.Label.Length;
                continue;
            }
            text.Append(c);
            i++;
        }
        return new AiComposerValue(text.ToString(), tokens);
    }

    private AiCommand? MatchCommand(string text, int at)
    {
        AiCommand? best = null;
        foreach (var command in Commands)
        {
            var name = command?.Name;
            if (string.IsNullOrEmpty(name) || (best is not null && name.Length <= best.Name.Length))
            {
                continue;
            }
            if (Matches(text, at, name))
            {
                best = command;
            }
        }
        return best;
    }

    private AiContextItem? MatchMention(string text, int at)
    {
        AiContextItem? best = null;
        foreach (var item in ContextItems)
        {
            var label = item?.Label;
            if (string.IsNullOrEmpty(label) || (best is not null && label.Length <= best.Label.Length))
            {
                continue;
            }
            if (Matches(text, at, label))
            {
                best = item;
            }
        }
        return best;
    }

    private static bool Matches(string text, int at, string name)
    {
        if (at + name.Length > text.Length || string.Compare(text, at, name, 0, name.Length, StringComparison.OrdinalIgnoreCase) != 0)
        {
            return false;
        }
        var end = at + name.Length;
        return end == text.Length || char.IsWhiteSpace(text[end]) || ".,;:!?)]}\"'".IndexOf(text[end]) >= 0;
    }

    #endregion
}
