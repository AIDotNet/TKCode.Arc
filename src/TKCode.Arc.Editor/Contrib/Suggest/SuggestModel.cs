// Ported from vscode (src/vs/editor/contrib/suggest/browser/suggestModel.ts, commit b57d5c9e), MIT license.
// Differences from upstream:
//  - the model talks to an ISuggestEditor (the editor events and services it reads) instead of ICodeEditor and the
//    service container, so it runs on the engine alone; the control and the tests each provide one;
//  - timers go through ISuggestEditor.SetTimeout (the control uses the dispatcher, tests run them by hand);
//  - "offWhenInlineCompletions" does not wait up to 750 ms for the inline completions model: quick suggestions show
//    unless an inline suggestion is visible (ISuggestEditor.IsInlineSuggestionVisible) and suppresses them;
//  - the word at a position uses the model's language (upstream: the language at the position, for embedded
//    languages);
//  - telemetry, the log of invalid items and the environment service are left out.

using System.Text.RegularExpressions;
using TKCode.Arc.Editor.Cursor;
using TKCode.Arc.Editor.Languages;
using TKCode.Arc.Editor.Model;
using TKCode.Arc.Editor.ViewModel;

namespace TKCode.Arc.Editor.Contrib.Suggest;

/// <summary>What the suggest model reads from its editor (upstream's ICodeEditor and the services suggestModel.ts injects).</summary>
internal interface ISuggestEditor
{
    /// <summary>The model, or null when the editor has none.</summary>
    TextModel? Model { get; }

    /// <summary>The primary cursor's position.</summary>
    TextPosition GetPosition();

    /// <summary>The primary selection.</summary>
    TextSelection GetSelection();

    /// <summary>Whether the editor or a widget inside it has keyboard focus.</summary>
    bool HasWidgetFocus();

    bool IsReadOnly { get; }

    SuggestOptions SuggestOptions { get; }

    LanguageFeatureRegistry<ICompletionItemProvider> CompletionProvider { get; }

    LanguageConfigurationService? LanguageConfigurationService { get; }

    /// <summary>The snippet provider asked with the others (upstream's getSnippetSuggestSupport), or null.</summary>
    ICompletionItemProvider? SnippetSuggestSupport => null;

    /// <summary>Whether a snippet session is active (upstream's SnippetController2.isInSnippet).</summary>
    bool IsInSnippet() => false;

    /// <summary>Whether an inline suggestion shows and asks to suppress suggestions (upstream's canShowQuickSuggest, negated).</summary>
    bool IsInlineSuggestionVisible() => false;

    /// <summary>The clipboard's text for snippets that use it.</summary>
    Task<string?> ReadClipboardText() => Task.FromResult<string?>(null);

    /// <summary>Runs <paramref name="callback"/> soon on the editor's thread, after the events being raised (a microtask upstream).</summary>
    void Post(Action callback);

    /// <summary>Runs <paramref name="callback"/> once after <paramref name="delay"/> milliseconds; dispose to cancel.</summary>
    IDisposable SetTimeout(Action callback, int delay);

    event Action? DidChangeModel;

    event Action? DidChangeModelLanguage;

    event Action? DidChangeConfiguration;

    event Action<CursorStateChangedEvent>? DidChangeCursorSelection;

    event Action? DidChangeModelContent;

    event Action<string>? DidType;

    event Action? DidCompositionStart;

    event Action? DidCompositionEnd;
}

/// <summary>Suggestions were cancelled (upstream's ICancelEvent).</summary>
internal sealed record SuggestCancelEvent(bool Retrigger);

/// <summary>Suggestions were requested (upstream's ITriggerEvent).</summary>
internal sealed record SuggestTriggerEvent(bool Auto, bool Shy, TextPosition Position);

/// <summary>Suggestions are ready (upstream's ISuggestEvent).</summary>
internal sealed record SuggestEvent(CompletionModel CompletionModel, bool IsFrozen, SuggestTriggerOptions TriggerOptions);

/// <summary>What a trigger asks for (upstream's SuggestTriggerOptions).</summary>
internal sealed record SuggestTriggerOptions
{
    public bool Auto { get; init; }

    public bool Shy { get; init; }

    public bool Refilter { get; init; }

    public bool Retrigger { get; init; }

    public CompletionTriggerKind? TriggerKind { get; init; }

    public string? TriggerCharacter { get; init; }

    public string? ClipboardText { get; init; }

    /// <summary>Kinds left out; null uses the editor's options.</summary>
    public IReadOnlySet<CompletionItemKind>? KindFilter { get; init; }

    public IReadOnlySet<ICompletionItemProvider>? ProviderFilter { get; init; }

    public IReadOnlyDictionary<ICompletionItemProvider, IReadOnlyList<SuggestCompletionItem>>? ProviderItemsToReuse { get; init; }
}

/// <summary>The line before the cursor when suggestions were asked for (upstream's suggestModel LineContext).</summary>
internal sealed class SuggestLineContext
{
    public SuggestLineContext(TextModel model, TextPosition position, SuggestTriggerOptions triggerOptions, LanguageConfigurationService? languageConfigurationService)
    {
        var line = model.GetLineContent(position.LineNumber);
        LeadingLineContent = line[..Math.Min(line.Length, Math.Max(0, position.Column - 1))];
        LeadingWord = SuggestWords.GetWordUntilPosition(model, position, languageConfigurationService);
        LineNumber = position.LineNumber;
        Column = position.Column;
        TriggerOptions = triggerOptions;
    }

    public int LineNumber { get; }

    public int Column { get; }

    public string LeadingLineContent { get; }

    public WordAtPosition LeadingWord { get; }

    public SuggestTriggerOptions TriggerOptions { get; }

    /// <summary>Whether typing at the cursor should show quick suggestions: at the end of a word that is not a number.</summary>
    public static bool ShouldAutoTrigger(ISuggestEditor editor)
    {
        if (editor.Model is not { } model)
        {
            return false;
        }
        var pos = editor.GetPosition();
        model.Tokenization.TokenizeIfCheap(pos.LineNumber);

        var word = SuggestWords.GetWordAtPosition(model, pos, editor.LanguageConfigurationService);
        if (word is null)
        {
            return false;
        }
        if (word.EndColumn != pos.Column && word.StartColumn + 1 != pos.Column /* after typing a single character before a word */)
        {
            return false;
        }
        if (JsNumber.IsNumber(word.Word))
        {
            return false;
        }
        return true;
    }
}

/// <summary>Whether suggestions show and how they were asked for (upstream's State).</summary>
internal enum SuggestState
{
    Idle = 0,
    Manual = 1,
    Auto = 2,
}

/// <summary>When to ask the completion providers and how to filter their items while typing (upstream's SuggestModel).</summary>
internal sealed class SuggestModel : IDisposable
{
    private static readonly Regex s_trailingNonWhitespace = new(@"\S*\s*$", RegexOptions.CultureInvariant);

    private readonly ISuggestEditor _editor;
    private readonly Dictionary<string, HashSet<ICompletionItemProvider>> _supportsByTriggerCharacter = new(StringComparer.Ordinal);
    private readonly List<IDisposable> _completionDisposables = new();
    private readonly Emitter<SuggestCancelEvent> _onDidCancel = new();
    private readonly Emitter<SuggestTriggerEvent> _onDidTrigger = new();
    private readonly Emitter<SuggestEvent> _onDidSuggest = new();
    private readonly IDisposable _providerRegistration;

    private IDisposable? _triggerQuickSuggest;
    private SuggestTriggerOptions? _triggerState;
    private CancellationTokenSource? _requestToken;
    private SuggestLineContext? _context;
    private TextSelection _currentSelection;
    private (int LineNumber, string Prefix)? _closedWithoutMatch;
    private CompletionModel? _completionModel;
    private bool _editorIsComposing;
    private bool _triggerCharacterListening;
    private bool _disposed;

    public SuggestModel(ISuggestEditor editor)
    {
        _editor = editor;
        _currentSelection = editor.Model is null ? new TextSelection(1, 1, 1, 1) : editor.GetSelection();

        editor.DidChangeModel += OnDidChangeModel;
        editor.DidChangeModelLanguage += OnDidChangeModel;
        editor.DidChangeConfiguration += UpdateTriggerCharacters;
        editor.DidCompositionStart += OnCompositionStart;
        editor.DidCompositionEnd += OnCompositionEndEvent;
        editor.DidChangeCursorSelection += OnCursorSelection;
        editor.DidChangeModelContent += OnModelContent;
        _providerRegistration = new ProviderListener(editor.CompletionProvider, () =>
        {
            UpdateTriggerCharacters();
            UpdateActiveSuggestSession();
        });

        UpdateTriggerCharacters();
    }

    public event Action<SuggestCancelEvent>? DidCancel { add => _onDidCancel.Add(value!); remove => _onDidCancel.Remove(value); }

    public event Action<SuggestTriggerEvent>? DidTrigger { add => _onDidTrigger.Add(value!); remove => _onDidTrigger.Remove(value); }

    public event Action<SuggestEvent>? DidSuggest { add => _onDidSuggest.Add(value!); remove => _onDidSuggest.Remove(value); }

    public SuggestState State => _triggerState is null ? SuggestState.Idle : _triggerState.Auto ? SuggestState.Auto : SuggestState.Manual;

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }
        _disposed = true;
        _editor.DidChangeModel -= OnDidChangeModel;
        _editor.DidChangeModelLanguage -= OnDidChangeModel;
        _editor.DidChangeConfiguration -= UpdateTriggerCharacters;
        _editor.DidCompositionStart -= OnCompositionStart;
        _editor.DidCompositionEnd -= OnCompositionEndEvent;
        _editor.DidChangeCursorSelection -= OnCursorSelection;
        _editor.DidChangeModelContent -= OnModelContent;
        StopTriggerCharacterListening();
        _providerRegistration.Dispose();
        Clear();
        Cancel();
        _onDidCancel.Clear();
        _onDidSuggest.Clear();
        _onDidTrigger.Clear();
    }

    private void OnDidChangeModel()
    {
        UpdateTriggerCharacters();
        Cancel();
    }

    private void OnCompositionStart() => _editorIsComposing = true;

    private void OnCompositionEndEvent()
    {
        _editorIsComposing = false;
        OnCompositionEnd();
    }

    private void OnCursorSelection(CursorStateChangedEvent e)
    {
        // only trigger suggest when the editor isn't composing a character
        if (!_editorIsComposing)
        {
            OnCursorChange(e);
        }
    }

    private void OnModelContent()
    {
        // only filter completions when the editor isn't composing a character
        if (!_editorIsComposing && _triggerState is not null)
        {
            RefilterCompletionItems(true);
        }
    }

    private void StopTriggerCharacterListening()
    {
        if (_triggerCharacterListening)
        {
            _editor.DidType -= OnDidTypeForTriggerCharacter;
            _editor.DidCompositionEnd -= OnCompositionEndForTriggerCharacter;
            _triggerCharacterListening = false;
        }
    }

    private void UpdateTriggerCharacters()
    {
        StopTriggerCharacterListening();
        _supportsByTriggerCharacter.Clear();

        if (_editor.IsReadOnly || _editor.Model is not { } model || !_editor.SuggestOptions.SuggestOnTriggerCharacters)
        {
            return;
        }

        foreach (var support in _editor.CompletionProvider.All(model))
        {
            foreach (var ch in support.TriggerCharacters ?? [])
            {
                if (!_supportsByTriggerCharacter.TryGetValue(ch, out var set))
                {
                    set = new HashSet<ICompletionItemProvider>();
                    if (_editor.SnippetSuggestSupport is { } suggestSupport)
                    {
                        set.Add(suggestSupport);
                    }
                    _supportsByTriggerCharacter[ch] = set;
                }
                set.Add(support);
            }
        }

        _editor.DidType += OnDidTypeForTriggerCharacter;
        _editor.DidCompositionEnd += OnCompositionEndForTriggerCharacter;
        _triggerCharacterListening = true;
    }

    private void OnDidTypeForTriggerCharacter(string text) => CheckTriggerCharacter(text);

    private void OnCompositionEndForTriggerCharacter() => CheckTriggerCharacter(null);

    private void CheckTriggerCharacter(string? text)
    {
        if (!CanShowSuggestOnTriggerCharacters())
        {
            return;
        }

        if (SuggestLineContext.ShouldAutoTrigger(_editor))
        {
            // don't trigger by trigger characters when this is a case for quick suggest
            return;
        }

        if (string.IsNullOrEmpty(text))
        {
            // came here from the compositionEnd-event
            if (_editor.Model is not { } model)
            {
                return;
            }
            var position = _editor.GetPosition();
            var line = model.GetLineContent(position.LineNumber);
            text = line[..Math.Min(line.Length, position.Column - 1)];
        }

        TriggerOnLastCharacter(text);
    }

    private bool CanShowQuickSuggest() => !_editor.IsInlineSuggestionVisible();

    private bool CanShowSuggestOnTriggerCharacters() => !_editor.IsInlineSuggestionVisible();

    /// <summary>Triggers the providers of the last character of the text, as typing that character does; false when it isn't a trigger character.</summary>
    private bool TriggerOnLastCharacter(string text)
    {
        var lastChar = string.Empty;
        if (text.Length > 0 && char.IsLowSurrogate(text[^1]))
        {
            if (text.Length > 1 && char.IsHighSurrogate(text[^2]))
            {
                lastChar = text[^2..];
            }
        }
        else if (text.Length > 0)
        {
            lastChar = text[^1..];
        }

        if (!_supportsByTriggerCharacter.TryGetValue(lastChar, out var supports))
        {
            return false;
        }

        // keep existing items that where not computed by the supports/providers that want to trigger now
        var providerItemsToReuse = new Dictionary<ICompletionItemProvider, IReadOnlyList<SuggestCompletionItem>>();
        if (_completionModel is not null)
        {
            foreach (var (provider, items) in _completionModel.GetItemsByProvider())
            {
                if (!supports.Contains(provider))
                {
                    providerItemsToReuse[provider] = items;
                }
            }
        }

        Trigger(new SuggestTriggerOptions
        {
            Auto = true,
            TriggerKind = CompletionTriggerKind.TriggerCharacter,
            TriggerCharacter = lastChar,
            Retrigger = _completionModel is not null,
            ClipboardText = _completionModel?.ClipboardText,
            ProviderFilter = supports,
            ProviderItemsToReuse = providerItemsToReuse,
        });
        return true;
    }

    // --- trigger/retrigger/cancel suggest

    public void Cancel(bool retrigger = false)
    {
        _triggerQuickSuggest?.Dispose();
        _triggerQuickSuggest = null;
        _closedWithoutMatch = null;

        if (_triggerState is not null)
        {
            _requestToken?.Cancel();
            _requestToken = null;
            _triggerState = null;
            _completionModel = null;
            _context = null;
            _onDidCancel.Fire(new SuggestCancelEvent(retrigger));
        }
    }

    /// <summary>Disposes the completion lists of earlier requests.</summary>
    public void Clear()
    {
        foreach (var disposable in _completionDisposables)
        {
            disposable.Dispose();
        }
        _completionDisposables.Clear();
    }

    private void UpdateActiveSuggestSession()
    {
        if (_triggerState is not null)
        {
            if (_editor.Model is not { } model || !_editor.CompletionProvider.Has(model))
            {
                Cancel();
            }
            else
            {
                Trigger(new SuggestTriggerOptions { Auto = _triggerState.Auto, Retrigger = true });
            }
        }
    }

    private void OnCursorChange(CursorStateChangedEvent e)
    {
        if (_editor.Model is null)
        {
            return;
        }

        var prevSelection = _currentSelection;
        _currentSelection = _editor.GetSelection();
        var selection = e.Selections.Count > 0 ? e.Selections[0] : _currentSelection;

        if (!selection.IsEmpty
            || (e.Reason != CursorChangeReason.NotSet && e.Reason != CursorChangeReason.Explicit)
            || (e.Source != "keyboard" && e.Source != "deleteLeft"))
        {
            // Early exit if nothing needs to be done!
            Cancel();
            return;
        }

        if (_triggerState is null && e.Reason == CursorChangeReason.NotSet)
        {
            if (prevSelection.Range.ContainsRange(_currentSelection.Range) || prevSelection.Range.End.IsBeforeOrEqual(_currentSelection.Position))
            {
                // cursor did move RIGHT due to typing -> trigger quick suggest
                DoTriggerQuickSuggest();
            }
            else if (e.Source == "deleteLeft")
            {
                // cursor did move LEFT due to Backspace -> bring back suggestions that closed because nothing matched
                ReopenAfterDelete();
            }
        }
        else if (_triggerState is not null && e.Reason == CursorChangeReason.Explicit)
        {
            // suggest is active and something like cursor keys are used to move the cursor. this means we can
            // refilter at the new position
            RefilterCompletionItems();
        }
        else if (e.Reason == CursorChangeReason.Explicit)
        {
            // suggest is inactive and the cursor moved away from where it closed
            _closedWithoutMatch = null;
        }
    }

    private void OnCompositionEnd()
    {
        // trigger or refilter when composition ends
        if (_triggerState is null)
        {
            DoTriggerQuickSuggest();
        }
        else
        {
            RefilterCompletionItems();
        }
    }

    /// <summary>Brings back automatic suggestions that closed because nothing matched, when Backspace keeps the cursor in that text.</summary>
    private void ReopenAfterDelete()
    {
        var closed = _closedWithoutMatch;
        _closedWithoutMatch = null;
        if (closed is null || _editor.Model is not { } model || !_editor.SuggestOptions.ReopenOnDelete)
        {
            return;
        }

        var ctx = new SuggestLineContext(model, _editor.GetPosition(), new SuggestTriggerOptions { Auto = true }, _editor.LanguageConfigurationService);
        var (lineNumber, prefix) = closed.Value;
        var text = ctx.LeadingLineContent.StartsWith(prefix, StringComparison.Ordinal) ? ctx.LeadingLineContent[prefix.Length..] : string.Empty;
        if (ctx.LineNumber != lineNumber || !ctx.LeadingLineContent.StartsWith(prefix, StringComparison.Ordinal) || text.Length == 0 || text.Any(char.IsWhiteSpace))
        {
            // deleted all of that text, or typed past it
            return;
        }

        if (ctx.LeadingWord.Word.Length == 0)
        {
            ReopenOnTriggerCharacter(ctx);
        }
        else if (CanShowQuickSuggest())
        {
            Trigger(new SuggestTriggerOptions { Auto = true });
        }
    }

    private bool ReopenOnTriggerCharacter(SuggestLineContext ctx) =>
        _editor.SuggestOptions.ReopenOnDelete && CanShowSuggestOnTriggerCharacters() && TriggerOnLastCharacter(ctx.LeadingLineContent);

    private void DoTriggerQuickSuggest()
    {
        var options = _editor.SuggestOptions;
        if (QuickSuggestionsOptions.IsAllOff(options.QuickSuggestions))
        {
            // not enabled
            return;
        }

        if (options.SnippetsPreventQuickSuggestions && _editor.IsInSnippet())
        {
            // no quick suggestion when in snippet mode
            return;
        }

        Cancel();

        _triggerQuickSuggest = _editor.SetTimeout(() =>
        {
            _triggerQuickSuggest = null;
            if (_triggerState is not null)
            {
                return;
            }
            if (!SuggestLineContext.ShouldAutoTrigger(_editor))
            {
                return;
            }
            if (_editor.Model is not { } model || !_editor.HasWidgetFocus())
            {
                return;
            }
            var pos = _editor.GetPosition();
            // validate enabled now
            var config = _editor.SuggestOptions.QuickSuggestions;
            if (QuickSuggestionsOptions.IsAllOff(config))
            {
                return;
            }

            if (!QuickSuggestionsOptions.IsAllOn(config))
            {
                // Check the type of the token that triggered this
                model.Tokenization.TokenizeIfCheap(pos.LineNumber);
                var lineTokens = model.Tokenization.GetLineTokens(pos.LineNumber);
                var tokenType = lineTokens.GetStandardTokenType(lineTokens.FindTokenIndexAtOffset(Math.Max(pos.Column - 1 - 1, 0)));
                var value = QuickSuggestionsOptions.ValueFor(config, tokenType);
                if (value is QuickSuggestionsValue.Off or QuickSuggestionsValue.Inline)
                {
                    return;
                }
            }

            if (!CanShowQuickSuggest())
            {
                // do not trigger quick suggestions if inline suggestions are shown
                return;
            }

            if (!_editor.CompletionProvider.Has(model))
            {
                return;
            }

            Trigger(new SuggestTriggerOptions { Auto = true });
        }, options.QuickSuggestionsDelay);
    }

    private void RefilterCompletionItems(bool contentChanged = false)
    {
        if (_editor.Model is not { } model || _triggerState is null)
        {
            return;
        }

        var ctx = new SuggestLineContext(model, _editor.GetPosition(), _triggerState with { Refilter = true }, _editor.LanguageConfigurationService);
        OnNewContext(ctx, contentChanged);
    }

    /// <summary>Asks the providers for suggestions at the cursor (upstream's trigger).</summary>
    public void Trigger(SuggestTriggerOptions options)
    {
        if (_editor.Model is not { } model)
        {
            return;
        }

        var position = _editor.GetPosition();
        var ctx = new SuggestLineContext(model, position, options, _editor.LanguageConfigurationService);

        // Cancel previous requests, change state & update UI
        Cancel(options.Retrigger);
        _triggerState = options;
        _onDidTrigger.Fire(new SuggestTriggerEvent(options.Auto, options.Shy, position));

        // Capture context when request was sent
        _context = ctx;

        // Build context for request
        var suggestCtx = new CompletionContext(options.TriggerKind ?? CompletionTriggerKind.Invoke);
        if (!string.IsNullOrEmpty(options.TriggerCharacter))
        {
            suggestCtx = new CompletionContext(CompletionTriggerKind.TriggerCharacter) { TriggerCharacter = options.TriggerCharacter };
        }

        var requestToken = new CancellationTokenSource();
        _requestToken = requestToken;

        // kind filter and snippet sort rules
        var suggestOptions = _editor.SuggestOptions;
        var snippetSortOrder = suggestOptions.SnippetSuggestions switch
        {
            SnippetSuggestions.Top => SnippetSortOrder.Top,
            SnippetSuggestions.Bottom => SnippetSortOrder.Bottom,
            _ => SnippetSortOrder.Inline,
        };

        var (itemKindFilter, showDeprecated) = CreateSuggestFilter(suggestOptions);
        var completionOptions = new CompletionOptions
        {
            SnippetSortOrder = snippetSortOrder,
            KindFilter = options.KindFilter ?? itemKindFilter,
            ProviderFilter = options.ProviderFilter ?? new HashSet<ICompletionItemProvider>(),
            ProviderItemsToReuse = options.ProviderItemsToReuse ?? new Dictionary<ICompletionItemProvider, IReadOnlyList<SuggestCompletionItem>>(),
            ShowDeprecated = showDeprecated,
            SnippetSuggestSupport = _editor.SnippetSuggestSupport,
        };
        var wordDistance = WordDistance.Create(model, position, () => _editor.Model is null ? null : _editor.GetPosition(), _editor.LanguageConfigurationService, suggestOptions.LocalityBonus);

        var completions = SuggestProvider.ProvideSuggestionItems(_editor.CompletionProvider, model, position, completionOptions, suggestCtx, requestToken.Token);

        // the result is handled after the events of the current change, as upstream's promise does, even when every
        // provider answered synchronously
        _editor.Post(() => _ = CompleteTrigger(completions, options, wordDistance, requestToken));
    }

    private async Task CompleteTrigger(
        Task<CompletionItemModel> completionsTask,
        SuggestTriggerOptions options,
        WordDistance wordDistance,
        CancellationTokenSource requestToken)
    {
        try
        {
            var completions = await completionsTask;
            if (requestToken.IsCancellationRequested)
            {
                // a newer request or a cancel came while this one ran
                completions.Disposable.Dispose();
                return;
            }

            if (_requestToken == requestToken)
            {
                _requestToken = null;
            }
            requestToken.Dispose();

            if (_editor.Model is null)
            {
                completions.Disposable.Dispose();
                return;
            }

            var clipboardText = options.ClipboardText;
            if (string.IsNullOrEmpty(clipboardText) && completions.NeedsClipboard)
            {
                clipboardText = await _editor.ReadClipboardText();
            }

            if (_triggerState is null || _context is null)
            {
                completions.Disposable.Dispose();
                return;
            }

            var current = _editor.Model;
            var ctx = new SuggestLineContext(current, _editor.GetPosition(), options, _editor.LanguageConfigurationService);
            var fuzzySearchOptions = FuzzyScoreOptions.Default with { FirstMatchCanBeWeak = !_editor.SuggestOptions.MatchOnWordStartOnly };
            _completionModel = new CompletionModel(
                completions.Items,
                _context.Column,
                new CompletionLineContext(ctx.LeadingLineContent, ctx.Column - _context.Column),
                wordDistance,
                _editor.SuggestOptions,
                _editor.SuggestOptions.SnippetSuggestions,
                fuzzySearchOptions,
                clipboardText);

            // store containers so that they can be disposed later
            _completionDisposables.Add(completions.Disposable);

            OnNewContext(ctx);
        }
        catch (OperationCanceledException)
        {
            // a newer request or a cancel
        }
        catch (Exception e)
        {
            UnexpectedErrors.Report(e);
        }
    }

    /// <summary>The kinds the options hide and whether deprecated items show (upstream's createSuggestFilter).</summary>
    public static (HashSet<CompletionItemKind> ItemKind, bool ShowDeprecated) CreateSuggestFilter(SuggestOptions options)
    {
        // kind filter and snippet sort rules
        var result = new HashSet<CompletionItemKind>();

        // snippet setting
        if (options.SnippetSuggestions == SnippetSuggestions.None)
        {
            result.Add(CompletionItemKind.Snippet);
        }

        // type setting
        void Hide(bool show, CompletionItemKind kind)
        {
            if (!show)
            {
                result.Add(kind);
            }
        }

        Hide(options.ShowMethods, CompletionItemKind.Method);
        Hide(options.ShowFunctions, CompletionItemKind.Function);
        Hide(options.ShowConstructors, CompletionItemKind.Constructor);
        Hide(options.ShowFields, CompletionItemKind.Field);
        Hide(options.ShowVariables, CompletionItemKind.Variable);
        Hide(options.ShowClasses, CompletionItemKind.Class);
        Hide(options.ShowStructs, CompletionItemKind.Struct);
        Hide(options.ShowInterfaces, CompletionItemKind.Interface);
        Hide(options.ShowModules, CompletionItemKind.Module);
        Hide(options.ShowProperties, CompletionItemKind.Property);
        Hide(options.ShowEvents, CompletionItemKind.Event);
        Hide(options.ShowOperators, CompletionItemKind.Operator);
        Hide(options.ShowUnits, CompletionItemKind.Unit);
        Hide(options.ShowValues, CompletionItemKind.Value);
        Hide(options.ShowConstants, CompletionItemKind.Constant);
        Hide(options.ShowEnums, CompletionItemKind.Enum);
        Hide(options.ShowEnumMembers, CompletionItemKind.EnumMember);
        Hide(options.ShowKeywords, CompletionItemKind.Keyword);
        Hide(options.ShowWords, CompletionItemKind.Text);
        Hide(options.ShowColors, CompletionItemKind.Color);
        Hide(options.ShowFiles, CompletionItemKind.File);
        Hide(options.ShowReferences, CompletionItemKind.Reference);
        Hide(options.ShowColors, CompletionItemKind.Customcolor);
        Hide(options.ShowFolders, CompletionItemKind.Folder);
        Hide(options.ShowTypeParameters, CompletionItemKind.TypeParameter);
        Hide(options.ShowSnippets, CompletionItemKind.Snippet);
        Hide(options.ShowUsers, CompletionItemKind.User);
        Hide(options.ShowIssues, CompletionItemKind.Issue);

        return (result, options.ShowDeprecated);
    }

    private static string GetLeadingWhitespace(string text)
    {
        var i = 0;
        while (i < text.Length && text[i] is ' ' or '\t')
        {
            i++;
        }
        return text[..i];
    }

    private void OnNewContext(SuggestLineContext ctx, bool contentChanged = false)
    {
        if (_context is null)
        {
            // happens when 24x7 IntelliSense is enabled and still in its delay
            return;
        }

        if (ctx.LineNumber != _context.LineNumber)
        {
            // e.g. happens when pressing Enter while IntelliSense is computed
            Cancel();
            return;
        }

        if (GetLeadingWhitespace(ctx.LeadingLineContent) != GetLeadingWhitespace(_context.LeadingLineContent))
        {
            // cancel IntelliSense when line start changes; happens when the current word gets outdented
            Cancel();
            return;
        }

        if (ctx.Column < _context.Column)
        {
            // typed -> moved cursor LEFT -> retrigger if still on a word, or if deleted back to a trigger character,
            // as typing it does
            if (ctx.LeadingWord.Word.Length > 0)
            {
                Trigger(new SuggestTriggerOptions { Auto = _context.TriggerOptions.Auto, Retrigger = true });
            }
            else if (!contentChanged || !ReopenOnTriggerCharacter(ctx))
            {
                Cancel();
            }
            return;
        }

        if (_completionModel is null)
        {
            // happens when IntelliSense is not yet computed
            return;
        }

        if (ctx.LeadingWord.Word.Length != 0 && ctx.LeadingWord.StartColumn > _context.LeadingWord.StartColumn)
        {
            // started a new word while IntelliSense shows -> retrigger but reuse all items that we currently have
            var shouldAutoTrigger = SuggestLineContext.ShouldAutoTrigger(_editor);
            if (shouldAutoTrigger && _context is not null)
            {
                var map = _completionModel.GetItemsByProvider();
                Trigger(new SuggestTriggerOptions
                {
                    Auto = _context.TriggerOptions.Auto,
                    Retrigger = true,
                    ClipboardText = _completionModel.ClipboardText,
                    ProviderItemsToReuse = map,
                });
            }
            return;
        }

        if (ctx.Column > _context.Column && _completionModel.GetIncompleteProvider().Count > 0 && ctx.LeadingWord.Word.Length != 0)
        {
            // typed -> moved cursor RIGHT & incomple model & still on a word -> retrigger
            var providerItemsToReuse = new Dictionary<ICompletionItemProvider, IReadOnlyList<SuggestCompletionItem>>();
            var providerFilter = new HashSet<ICompletionItemProvider>();
            foreach (var (provider, items) in _completionModel.GetItemsByProvider())
            {
                if (items.Count > 0 && items[0].Container.Incomplete)
                {
                    providerFilter.Add(provider);
                }
                else
                {
                    providerItemsToReuse[provider] = items;
                }
            }

            Trigger(new SuggestTriggerOptions
            {
                Auto = _context.TriggerOptions.Auto,
                TriggerKind = CompletionTriggerKind.TriggerForIncompleteCompletions,
                Retrigger = true,
                ClipboardText = _completionModel.ClipboardText,
                ProviderFilter = providerFilter,
                ProviderItemsToReuse = providerItemsToReuse,
            });
        }
        else
        {
            // typed -> moved cursor RIGHT -> update UI
            var oldLineContext = _completionModel.LineContext;
            var isFrozen = false;

            _completionModel.LineContext = new CompletionLineContext(ctx.LeadingLineContent, ctx.Column - _context.Column);

            if (_completionModel.Items.Count == 0)
            {
                var shouldAutoTrigger = SuggestLineContext.ShouldAutoTrigger(_editor);
                if (_context is null)
                {
                    // shouldAutoTrigger forces tokenization, which can cause pending cursor change events to be
                    // emitted, which can cause suggestions to be cancelled
                    Cancel();
                    return;
                }

                if (shouldAutoTrigger && _context.LeadingWord.EndColumn < ctx.LeadingWord.StartColumn)
                {
                    // retrigger when heading into a new word
                    Trigger(new SuggestTriggerOptions { Auto = _context.TriggerOptions.Auto, Retrigger = true });
                    return;
                }

                if (!_context.TriggerOptions.Auto)
                {
                    // freeze when IntelliSense was manually requested
                    _completionModel.LineContext = oldLineContext;
                    isFrozen = _completionModel.Items.Count > 0;

                    if (isFrozen && ctx.LeadingWord.Word.Length == 0)
                    {
                        // there were results before but now there aren't and also we are not on a word anymore -> cancel
                        Cancel();
                        return;
                    }
                }
                else
                {
                    // nothing left
                    Cancel();
                    if (_editor.SuggestOptions.ReopenOnDelete)
                    {
                        // remember the text that nothing matched, and whitespace typed after it, for Backspace
                        var leading = ctx.LeadingLineContent;
                        _closedWithoutMatch = (ctx.LineNumber, leading[..s_trailingNonWhitespace.Match(leading).Index]);
                    }
                    return;
                }
            }

            _onDidSuggest.Fire(new SuggestEvent(_completionModel, isFrozen, ctx.TriggerOptions));
        }
    }

    // listens to a registry's DidChange until disposed
    private sealed class ProviderListener : IDisposable
    {
        private readonly LanguageFeatureRegistry<ICompletionItemProvider> _registry;
        private readonly Action<int> _handler;

        public ProviderListener(LanguageFeatureRegistry<ICompletionItemProvider> registry, Action callback)
        {
            _registry = registry;
            _handler = _ => callback();
            registry.DidChange += _handler;
        }

        public void Dispose() => _registry.DidChange -= _handler;
    }
}
