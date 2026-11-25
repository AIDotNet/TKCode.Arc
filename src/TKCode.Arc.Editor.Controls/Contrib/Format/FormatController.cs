// Ported from vscode (src/vs/editor/contrib/format/browser/formatActions.ts: FormatOnType, FormatOnPaste,
// FormatDocumentAction, FormatSelectionAction and the editor.action.format command; commit b57d5c9e), MIT license.
// Differences:
//  - the options formatOnType and formatOnPaste are the CodeEditor properties FormatOnType and FormatOnPaste; the
//    contributions follow their changes, the language and the provider registries;
//  - no progress, accessibility signals or context menu entries; the formatter selector is Monaco's (the first);
//  - editor.action.format is an editor command (upstream registers it in the global CommandsRegistry and runs it on
//    the focused editor).

using Avalonia;
using TKCode.Arc.Editor;
using TKCode.Arc.Editor.Contrib.Format;
using TKCode.Arc.Editor.Model;
using TKCode.Arc.Editor.Platform;

namespace TKCode.Arc;

/// <summary>Formats the line after a trigger character is typed (upstream's FormatOnType).</summary>
internal sealed class FormatOnTypeContribution : ICodeEditorContribution
{
    public const string Id = "editor.contrib.autoFormat";

    public static readonly EditorContributionDescription Description =
        new(Id, editor => new FormatOnTypeContribution(editor), EditorContributionInstantiation.BeforeFirstInteraction);

    private readonly CodeEditor _editor;
    private readonly List<IDisposable> _session = [];
    private HashSet<char>? _triggerChars;

    private FormatOnTypeContribution(CodeEditor editor)
    {
        _editor = editor;
        editor.LanguageFeatures.OnTypeFormattingEditProvider.DidChange += OnProvidersChanged;
        editor.DidChangeModelLanguage += OnLanguageChanged;
        editor.PropertyChanged += OnEditorPropertyChanged;
        Update();
    }

    /// <summary>The last on-type formatting (for tests).</summary>
    internal Task? PendingFormat { get; private set; }

    public static FormatOnTypeContribution? Get(CodeEditor editor) => editor.GetContribution<FormatOnTypeContribution>(Id);

    public void Dispose()
    {
        _editor.LanguageFeatures.OnTypeFormattingEditProvider.DidChange -= OnProvidersChanged;
        _editor.DidChangeModelLanguage -= OnLanguageChanged;
        _editor.PropertyChanged -= OnEditorPropertyChanged;
        ClearSession();
    }

    private void OnProvidersChanged(int count) => Update();

    private void OnLanguageChanged(ModelLanguageChangedEvent e) => Update();

    private void OnEditorPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (e.Property == CodeEditor.FormatOnTypeProperty)
        {
            Update();
        }
    }

    private void ClearSession()
    {
        if (_triggerChars is not null)
        {
            _editor.DidType -= OnDidType;
            _triggerChars = null;
        }

        foreach (var disposable in _session)
        {
            disposable.Dispose();
        }

        _session.Clear();
    }

    private void Update()
    {
        // clean up
        ClearSession();

        // we are disabled
        if (!_editor.FormatOnType)
        {
            return;
        }

        // no support
        var support = _editor.LanguageFeatures.OnTypeFormattingEditProvider.Ordered(_editor.Model).FirstOrDefault();
        if (support is null || support.AutoFormatTriggerCharacters.Count == 0)
        {
            return;
        }

        // register typing listeners that will trigger the format
        _triggerChars = [];
        foreach (var ch in support.AutoFormatTriggerCharacters)
        {
            if (ch.Length > 0)
            {
                _triggerChars.Add(ch[0]);
            }
        }

        _editor.DidType += OnDidType;
    }

    private void OnDidType(string text)
    {
        if (text.Length > 0 && _triggerChars is { } triggers && triggers.Contains(text[^1]))
        {
            PendingFormat = Trigger(text[^1].ToString());
        }
    }

    private async Task Trigger(string ch)
    {
        if (_editor.GetSelections().Count > 1 || _editor.GetSelection() is not { IsEmpty: true } || _editor.GetPosition() is not { } position)
        {
            return;
        }

        var model = _editor.Model;
        using var cts = new CancellationTokenSource();

        // install a listener that checks if edits happens before the
        // position on which we format right now. If so, we won't
        // apply the format edits
        void OnContent(ModelContentChangedEvent e)
        {
            if (e.IsFlush)
            {
                // a model.setValue() was called
                cts.Cancel();
                return;
            }

            foreach (var change in e.Changes)
            {
                if (change.Range.EndLineNumber <= position.LineNumber)
                {
                    cts.Cancel();
                    return;
                }
            }
        }

        _editor.DidChangeModelContent += OnContent;
        try
        {
            var edits = await Formatting.GetOnTypeFormattingEdits(_editor.LanguageFeatures, model, position, ch, Formatting.GetFormattingOptions(model), cts.Token).ConfigureAwait(true);
            if (cts.IsCancellationRequested)
            {
                return;
            }

            if (edits is { Count: > 0 })
            {
                FormattingEdit.Execute(_editor, edits, true);
            }
        }
        finally
        {
            _editor.DidChangeModelContent -= OnContent;
        }
    }
}

/// <summary>Formats pasted text with the range formatter (upstream's FormatOnPaste).</summary>
internal sealed class FormatOnPasteContribution : ICodeEditorContribution
{
    public const string Id = "editor.contrib.formatOnPaste";

    public static readonly EditorContributionDescription Description =
        new(Id, editor => new FormatOnPasteContribution(editor), EditorContributionInstantiation.BeforeFirstInteraction);

    private readonly CodeEditor _editor;
    private bool _listening;

    private FormatOnPasteContribution(CodeEditor editor)
    {
        _editor = editor;
        editor.PropertyChanged += OnEditorPropertyChanged;
        editor.DidChangeModelLanguage += OnLanguageChanged;
        editor.LanguageFeatures.DocumentRangeFormattingEditProvider.DidChange += OnProvidersChanged;
        Update();
    }

    /// <summary>The last paste formatting (for tests).</summary>
    internal Task? PendingFormat { get; private set; }

    public static FormatOnPasteContribution? Get(CodeEditor editor) => editor.GetContribution<FormatOnPasteContribution>(Id);

    public void Dispose()
    {
        _editor.PropertyChanged -= OnEditorPropertyChanged;
        _editor.DidChangeModelLanguage -= OnLanguageChanged;
        _editor.LanguageFeatures.DocumentRangeFormattingEditProvider.DidChange -= OnProvidersChanged;
        StopListening();
    }

    private void OnEditorPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (e.Property == CodeEditor.FormatOnPasteProperty)
        {
            Update();
        }
    }

    private void OnLanguageChanged(ModelLanguageChangedEvent e) => Update();

    private void OnProvidersChanged(int count) => Update();

    private void StopListening()
    {
        if (_listening)
        {
            _editor.DidPaste -= OnDidPaste;
            _listening = false;
        }
    }

    private void Update()
    {
        // clean up
        StopListening();

        // we are disabled, or there is no formatter
        if (!_editor.FormatOnPaste || !_editor.LanguageFeatures.DocumentRangeFormattingEditProvider.Has(_editor.Model))
        {
            return;
        }

        _editor.DidPaste += OnDidPaste;
        _listening = true;
    }

    private void OnDidPaste(TextRange range)
    {
        if (_editor.GetSelections().Count > 1)
        {
            return;
        }

        PendingFormat = Run(range);
    }

    private async Task Run(TextRange range)
    {
        try
        {
            await EditorFormatting.FormatDocumentRangesWithSelectedProvider(_editor, [range], FormattingMode.Silent, CancellationToken.None).ConfigureAwait(true);
        }
        catch (Exception e)
        {
            UnexpectedErrors.Report(e);
        }
    }
}

/// <summary>The format actions (upstream's FormatDocumentAction, FormatSelectionAction and editor.action.format).</summary>
internal static class FormatActions
{
    /// <summary>Formats the document (Shift+Alt+F; Ctrl+Shift+I on Linux).</summary>
    public static readonly EditorAction FormatDocumentAction = EditorAction.Create(
        "editor.action.formatDocument",
        "Format Document",
        ContextKeyExpr.And(EditorContextKeys.NotInCompositeEditor, EditorContextKeys.Writable, EditorContextKeys.HasDocumentFormattingProvider),
        static (editor, _) => EditorFormatting.FormatDocumentWithSelectedProvider(editor, FormattingMode.Explicit, CancellationToken.None),
        new EditorKeybinding(KeyMod.Shift | KeyMod.Alt | (int)KeyCode.KeyF)
        {
            Linux = new PlatformKeybindings { Primary = KeyMod.CtrlCmd | KeyMod.Shift | (int)KeyCode.KeyI },
            When = EditorContextKeys.EditorTextFocus,
        });

    /// <summary>Formats the selections, or the cursor lines (Ctrl+K Ctrl+F).</summary>
    public static readonly EditorAction FormatSelectionAction = EditorAction.Create(
        "editor.action.formatSelection",
        "Format Selection",
        ContextKeyExpr.And(EditorContextKeys.Writable, EditorContextKeys.HasDocumentSelectionFormattingProvider),
        static (editor, _) =>
        {
            var model = editor.Model;
            var ranges = editor.GetSelections()
                .Select(selection => selection.Range.IsEmpty
                    ? new TextRange(selection.StartLineNumber, 1, selection.StartLineNumber, model.GetLineMaxColumn(selection.StartLineNumber))
                    : selection.Range)
                .ToList();
            return EditorFormatting.FormatDocumentRangesWithSelectedProvider(editor, ranges, FormattingMode.Explicit, CancellationToken.None);
        },
        new EditorKeybinding(KeyCodes.KeyChord(KeyMod.CtrlCmd | (int)KeyCode.KeyK, KeyMod.CtrlCmd | (int)KeyCode.KeyF))
        {
            When = EditorContextKeys.EditorTextFocus,
        });

    /// <summary>The old format command: the document when nothing is selected, else the selection.</summary>
    public static readonly EditorCommand FormatCommand = EditorCommand.Create(
        "editor.action.format",
        null,
        static (editor, _) => editor.GetSelection() is { IsEmpty: true }
            ? editor.GetAction(FormatDocumentAction.Id)?.Run() ?? Task.CompletedTask
            : editor.GetAction(FormatSelectionAction.Id)?.Run() ?? Task.CompletedTask);
}
