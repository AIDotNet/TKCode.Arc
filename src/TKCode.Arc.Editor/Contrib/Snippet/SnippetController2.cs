// Ported from vscode (src/vs/editor/contrib/snippet/browser/snippetController2.ts: ISnippetInsertOptions and
// SnippetController2, commit b57d5c9e), MIT license.
// Differences:
//  - the controller is engine logic over an ISnippetEditor; the editor contribution, its commands and keybindings
//    (jumpToNextSnippetPlaceholder, jumpToPrevSnippetPlaceholder, leaveSnippet, acceptSnippet) and the choice list are
//    TKCode.Arc's CodeEditorSnippetController in the controls package;
//  - errors while inserting cancel the session and are reported through UnexpectedErrors (upstream: the log service);
//  - the choice completions are a provider registered (exclusive, for the model's language, while a choice is active)
//    into the editor's LanguageFeatures when it has them, and shown with ISnippetEditor.ShowSimpleSuggestions, which
//    the host defers like upstream's queueMicrotask;
//  - there is no observable isInSnippetObservable; IsInSnippetChanged is raised when the inSnippetMode key changes;
//  - the editor's model cannot be swapped, so there is no onDidChangeModel listener.

using TKCode.Arc.Editor.Languages;
using TKCode.Arc.Editor.Model;
using TKCode.Arc.Editor.Platform;

namespace TKCode.Arc.Editor.Contrib.Snippet;

/// <summary>How the snippet controller inserts a snippet (upstream's ISnippetInsertOptions).</summary>
internal sealed record SnippetInsertOptions
{
    public static SnippetInsertOptions Default { get; } = new();

    /// <summary>How many characters before the cursor the snippet replaces.</summary>
    public int OverwriteBefore { get; init; }

    /// <summary>How many characters after the cursor the snippet replaces.</summary>
    public int OverwriteAfter { get; init; }

    /// <summary>Indents the snippet's lines like the line it is inserted on.</summary>
    public bool AdjustWhitespace { get; init; } = true;

    /// <summary>Pushes an undo stop before the insert.</summary>
    public bool UndoStopBefore { get; init; } = true;

    /// <summary>Pushes an undo stop after the insert.</summary>
    public bool UndoStopAfter { get; init; } = true;

    /// <summary>The text of the CLIPBOARD variable.</summary>
    public string? ClipboardText { get; init; }

    /// <summary>What the suggest widget overtyped (the SELECTION variable falls back to it).</summary>
    public IOvertypingCapturer? OvertypingCapturer { get; init; }

    internal SnippetSessionInsertOptions ToSessionOptions() => new()
    {
        OverwriteBefore = OverwriteBefore,
        OverwriteAfter = OverwriteAfter,
        AdjustWhitespace = AdjustWhitespace,
        ClipboardText = ClipboardText,
        OvertypingCapturer = OvertypingCapturer,
    };
}

/// <summary>
/// Inserts snippets into an editor and runs snippet mode: tab stop navigation, mirrors, nested snippets, choices
/// (upstream's SnippetController2).
/// </summary>
internal sealed class SnippetController2 : IDisposable
{
    public const string ID = "snippetController2";

    public static readonly RawContextKey<bool> InSnippetMode = new("inSnippetMode", false, "Whether the editor in current in snippet mode");
    public static readonly RawContextKey<bool> HasNextTabstop = new("hasNextTabstop", false, "Whether there is a next tab stop when in snippet mode");
    public static readonly RawContextKey<bool> HasPrevTabstop = new("hasPrevTabstop", false, "Whether there is a previous tab stop when in snippet mode");
    public static readonly RawContextKey<bool> InSnippetChoice = new("inSnippetChoice", false, "Whether the current snippet tab stop is a choice");

    private readonly ISnippetEditor _editor;
    private readonly IContextKey<bool> _inSnippet;
    private readonly IContextKey<bool> _hasNextTabstop;
    private readonly IContextKey<bool> _hasPrevTabstop;
    private readonly IContextKey<bool> _inSnippetChoice;
    private readonly List<IDisposable> _snippetListener = [];

    private SnippetSession? _session;
    private int _modelVersionId = -1;
    private Choice? _currentChoice;
    private ChoiceCompletions? _choiceCompletions;
    private bool _isInSnippet;

    public SnippetController2(ISnippetEditor editor)
    {
        _editor = editor;
        _inSnippet = InSnippetMode.BindTo(editor.ContextKeyService);
        _hasNextTabstop = HasNextTabstop.BindTo(editor.ContextKeyService);
        _hasPrevTabstop = HasPrevTabstop.BindTo(editor.ContextKeyService);
        _inSnippetChoice = InSnippetChoice.BindTo(editor.ContextKeyService);
    }

    /// <summary>Raised when snippet mode starts or ends (upstream's isInSnippetObservable).</summary>
    public event Action<bool>? IsInSnippetChanged;

    /// <summary>Raised when a choice placeholder becomes the current tab stop, before its choices are shown.</summary>
    public event Action<ActiveSnippetChoice>? ChoiceActivated;

    public void Dispose()
    {
        _inSnippet.Reset();
        SetIsInSnippet(false);
        _hasPrevTabstop.Reset();
        _hasNextTabstop.Reset();
        _inSnippetChoice.Reset();
        _session?.Dispose();
        ClearListeners();
    }

    /// <summary>Applies snippet edits (each replaces a range) as one snippet session.</summary>
    public void Apply(IReadOnlyList<SnippetEdit> edits, SnippetInsertOptions? opts = null)
    {
        try
        {
            DoInsert(null, edits, opts ?? SnippetInsertOptions.Default);
        }
        catch (Exception e)
        {
            Cancel();
            UnexpectedErrors.Report(e);
        }
    }

    /// <summary>Inserts <paramref name="template"/> at every cursor, or into the current tab stop when in snippet mode.</summary>
    public void Insert(string template, SnippetInsertOptions? opts = null)
    {
        // this is here to find out more about the yet-not-understood
        // error that sometimes happens when we fail to inserted a nested
        // snippet
        try
        {
            DoInsert(template, null, opts ?? SnippetInsertOptions.Default);
        }
        catch (Exception e)
        {
            Cancel();
            UnexpectedErrors.Report(e);
        }
    }

    private void DoInsert(string? template, IReadOnlyList<SnippetEdit>? edits, SnippetInsertOptions opts)
    {
        // don't listen while inserting the snippet
        // as that is the inflight state causing cancelation
        ClearListeners();

        if (opts.UndoStopBefore)
        {
            _editor.Model.PushStackElement();
        }

        // don't merge
        if (_session is not null && template is null)
        {
            Cancel();
        }

        if (_session is null)
        {
            _modelVersionId = _editor.Model.GetAlternativeVersionId();
            _session = template is not null
                ? new SnippetSession(_editor, template, opts.ToSessionOptions())
                : new SnippetSession(_editor, edits!, opts.ToSessionOptions());
            _session.Insert();
        }
        else
        {
            _session.Merge(template!, opts.ToSessionOptions());
        }

        if (opts.UndoStopAfter)
        {
            _editor.Model.PushStackElement();
        }

        // regster completion item provider when there is any choice element
        if (_session?.HasChoice == true)
        {
            _choiceCompletions = new ChoiceCompletions(this);
        }

        UpdateState();

        _snippetListener.Add(_editor.OnDidChangeModelContent(e =>
        {
            if (e.IsFlush)
            {
                Cancel();
            }
        }));
        _snippetListener.Add(_editor.OnDidChangeCursorSelection(UpdateState));
    }

    private void UpdateState()
    {
        if (_session is null)
        {
            // canceled in the meanwhile
            return;
        }

        if (_modelVersionId == _editor.Model.GetAlternativeVersionId())
        {
            // undo until the 'before' state happened
            // and makes use cancel snippet mode
            Cancel();
            return;
        }

        if (!_session.HasPlaceholder)
        {
            // don't listen for selection changes and don't
            // update context keys when the snippet is plain text
            Cancel();
            return;
        }

        if (_session.IsAtLastPlaceholder || !_session.IsSelectionWithinPlaceholders())
        {
            _editor.Model.PushStackElement();
            Cancel();
            return;
        }

        _inSnippet.Set(true);
        SetIsInSnippet(true);
        _hasPrevTabstop.Set(!_session.IsAtFirstPlaceholder);
        _hasNextTabstop.Set(!_session.IsAtLastPlaceholder);

        HandleChoice();
    }

    private void HandleChoice()
    {
        if (_session is null)
        {
            _currentChoice = null;
            _inSnippetChoice.Reset();
            return;
        }

        var activeChoice = _session.ActiveChoice;
        if (activeChoice is null || _choiceCompletions is null)
        {
            _choiceCompletions?.Disable();
            _currentChoice = null;
            _inSnippetChoice.Reset();
            return;
        }

        _inSnippetChoice.Set(true);

        if (!ReferenceEquals(_currentChoice, activeChoice.Choice))
        {
            _currentChoice = activeChoice.Choice;

            _choiceCompletions.Enable();

            // trigger suggest with the special choice completion provider
            ChoiceActivated?.Invoke(activeChoice);
            _editor.ShowSimpleSuggestions(_choiceCompletions);
        }
    }

    /// <summary>Moves through every remaining tab stop, ending snippet mode (the acceptSnippet command).</summary>
    public void Finish()
    {
        while (_inSnippet.Get())
        {
            Next();
        }
    }

    /// <summary>Ends snippet mode; <paramref name="resetSelection"/> keeps only the primary selection (Escape).</summary>
    public void Cancel(bool resetSelection = false)
    {
        _inSnippet.Reset();
        SetIsInSnippet(false);
        _hasPrevTabstop.Reset();
        _hasNextTabstop.Reset();
        _inSnippetChoice.Reset();
        ClearListeners();

        _currentChoice = null;
        _choiceCompletions?.Disable();
        _choiceCompletions = null;

        _session?.Dispose();
        _session = null;
        _modelVersionId = -1;
        if (resetSelection)
        {
            // reset selection to the primary cursor when being asked
            // for. this happens when explicitly cancelling snippet mode,
            // e.g. when pressing ESC
            _editor.SetSelections([_editor.GetSelection()]);
        }
    }

    /// <summary>Selects the previous tab stop.</summary>
    public void Prev()
    {
        _session?.Prev();
        UpdateState();
    }

    /// <summary>Selects the next tab stop.</summary>
    public void Next()
    {
        _session?.Next();
        UpdateState();
    }

    /// <summary>Whether the editor is in snippet mode.</summary>
    public bool IsInSnippet() => _inSnippet.Get();

    /// <summary>The range of the current session's placeholders.</summary>
    public TextRange? GetSessionEnclosingRange() => _session?.GetEnclosingRange();

    /// <summary>The choice of the current tab stop, if it is a choice placeholder.</summary>
    internal ActiveSnippetChoice? ActiveChoice => _session?.ActiveChoice;

    /// <summary>The provider of the current choice's options, when the session has choices.</summary>
    internal ICompletionItemProvider? ChoiceCompletionProvider => _choiceCompletions;

    private void SetIsInSnippet(bool value)
    {
        if (_isInSnippet != value)
        {
            _isInSnippet = value;
            IsInSnippetChanged?.Invoke(value);
        }
    }

    private void ClearListeners()
    {
        foreach (var listener in _snippetListener)
        {
            listener.Dispose();
        }

        _snippetListener.Clear();
    }

    // the completion provider of the session's choices; registered while a choice is active
    private sealed class ChoiceCompletions(SnippetController2 owner) : ICompletionItemProvider
    {
        private IDisposable? _registration;

        public string DebugDisplayName => "snippetChoiceCompletions";

        public Task<CompletionList?> ProvideCompletionItems(TextModel model, TextPosition position, CompletionContext context, CancellationToken token)
        {
            var editor = owner._editor;
            if (owner._session is null || !ReferenceEquals(model, editor.Model) || editor.GetSelection().Position != position)
            {
                return Task.FromResult<CompletionList?>(null);
            }

            var activeChoice = owner._session.ActiveChoice;
            if (activeChoice is null || activeChoice.Choice.Options.Count == 0)
            {
                return Task.FromResult<CompletionList?>(null);
            }

            var word = model.GetValueInRange(activeChoice.Range);
            var isAnyOfOptions = activeChoice.Choice.Options.Any(o => o.Value == word);
            var suggestions = new List<CompletionItem>();
            for (var i = 0; i < activeChoice.Choice.Options.Count; i++)
            {
                var option = activeChoice.Choice.Options[i];
                suggestions.Add(new CompletionItem
                {
                    Kind = CompletionItemKind.Value,
                    Label = option.Value,
                    InsertText = option.Value,
                    SortText = new string('a', i + 1),
                    Range = activeChoice.Range,
                    FilterText = isAnyOfOptions ? word + "_" + option.Value : null,
                    Command = new LanguageCommand("jumpToNextSnippetPlaceholder", "Go to next placeholder..."),
                });
            }

            return Task.FromResult<CompletionList?>(new CompletionList(suggestions));
        }

        public void Enable()
        {
            if (_registration is null && owner._editor.LanguageFeatures is { } features)
            {
                var model = owner._editor.Model;
                _registration = features.CompletionProvider.Register(new LanguageFilter
                {
                    Language = model.GetLanguageId(),
                    Scheme = model.Uri.Scheme,
                    Exclusive = true,
                }, this);
            }
        }

        public void Disable()
        {
            _registration?.Dispose();
            _registration = null;
        }
    }
}
