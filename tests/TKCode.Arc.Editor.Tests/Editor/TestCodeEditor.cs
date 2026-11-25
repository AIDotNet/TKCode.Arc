// Ported from vscode (src/vs/editor/test/browser/testCodeEditor.ts: withTestCodeEditor, withAsyncTestCodeEditor,
// createTestCodeEditor and instantiateTestCodeEditor; src/vs/editor/test/browser/config/testConfiguration.ts:
// TestConfiguration; commit b57d5c9e), MIT license.
//
// Upstream's test editor is the real CodeEditorWidget without a view, on a TestConfiguration (a 100 x 100 pixel editor
// with a mock monospace font: line height 19, characters 10 pixels wide) and test services. There is no code editor
// widget here yet, so TestCodeEditor holds what the cursor tests reach through it: a TextModel, a ViewModelConfiguration
// built from TestCodeEditorOptions the way the TestConfiguration computes it, and a ViewModel (with its
// CursorsController), plus the editor methods the tests call (getModel, getValue, getSelection(s), setSelection(s),
// setPosition, executeEdits, executeCommand(s), type, paste, cut, the composition, undo and redo).
// Differences from upstream:
//  - options: upstream's IEditorOptions are TestCodeEditorOptions: the cursor options (EditorOptions), the language,
//    the model's creation options (tab size, ...), word wrap and the other wrapping options, the input mode, focus and
//    a hook (Configure) for any other view model option;
//  - the computed layout is reduced to what the view model reads: the layout height (OuterHeight, upstream's
//    envConfig.outerHeight, 100 by default) and, for word wrap 'on' and 'bounded', the viewport column (ViewportColumn,
//    1 by default: what upstream's layout computes for a 100 pixel wide editor with line numbers, glyph margin, folding
//    and minimap); the content width is 0 (upstream computes a negative one, which the view layout clamps to 0);
//  - the editor id is an option (1 by default; two editors on one model need different ids);
//  - the view model is a TestViewModel: the real ViewModel with the replacement of getCompletelyVisibleViewRange that
//    upstream's tests make on the instance, and counters of the cursor events upstream's tests listen to;
//  - the editor is the ICoreCommandsCodeEditor the core commands run on (getModel, _getViewModel, the readOnly option,
//    pushUndoStop and executeCommands); undo and redo run CoreEditingCommands.Undo and Redo;
//  - there is no instantiation service: the language configuration service is an option (a new one by default, owned
//    and disposed by the editor), and the language id codec is the model's own unless one is given.

using TKCode.Arc.Editor.Commands;
using TKCode.Arc.Editor.Config;
using TKCode.Arc.Editor.Cursor;
using TKCode.Arc.Editor.Languages;
using TKCode.Arc.Editor.Model;
using TKCode.Arc.Editor.ViewLayout;
using TKCode.Arc.Editor.ViewModel;
using EditorViewModel = TKCode.Arc.Editor.ViewModel.ViewModel;

namespace TKCode.Arc.Editor.Tests;

/// <summary>Upstream's <c>wordWrap</c> option.</summary>
internal enum TestWordWrap
{
    Off,
    On,
    WordWrapColumn,
    Bounded,
}

/// <summary>The options of a <see cref="TestCodeEditor"/> (upstream's TestCodeEditorInstantiationOptions).</summary>
internal sealed record TestCodeEditorOptions
{
    /// <summary>The cursor's options (read-only, auto closing, multi cursor, ...).</summary>
    public EditorOptions EditorOptions { get; init; } = EditorOptions.Default;

    /// <summary>The language of a model the editor creates; plaintext by default.</summary>
    public string? LanguageId { get; init; }

    /// <summary>The creation options of a model the editor creates (tab size, indent size, insert spaces, ...).</summary>
    public TextModelCreationOptions? ModelOptions { get; init; }

    /// <summary>The tab size of a model the editor creates; overrides <see cref="ModelOptions"/>' tab size.</summary>
    public int? TabSize { get; init; }

    /// <summary>The language configuration service; a new one (disposed with the editor) by default.</summary>
    public LanguageConfigurationService? LanguageConfigurationService { get; init; }

    /// <summary>The language id codec of a model the editor creates; the model's own by default.</summary>
    public ILanguageIdCodec? LanguageIdCodec { get; init; }

    /// <summary>Upstream's <c>wordWrap</c>.</summary>
    public TestWordWrap WordWrap { get; init; } = TestWordWrap.Off;

    /// <summary>Upstream's <c>wordWrapColumn</c>.</summary>
    public int WordWrapColumn { get; init; } = 80;

    /// <summary>The columns that fit in the viewport (upstream computes it from the editor width); for 'on' and 'bounded'.</summary>
    public int ViewportColumn { get; init; } = 1;

    public WrappingIndent WrappingIndent { get; init; } = WrappingIndent.Same;

    public WrappingStrategy WrappingStrategy { get; init; } = WrappingStrategy.Simple;

    public WordBreak WordBreak { get; init; } = WordBreak.Normal;

    public bool WrapOnEscapedLineFeeds { get; init; }

    /// <summary>The height of the editor in pixels (upstream's envConfig.outerHeight).</summary>
    public double OuterHeight { get; init; } = 100;

    /// <summary>Insert or overtype.</summary>
    public EditorInputMode InputMode { get; init; } = EditorInputMode.Insert;

    /// <summary>If the editor has text focus (upstream's hasTextFocus, true by default).</summary>
    public bool HasTextFocus { get; init; } = true;

    /// <summary>The editor's id (the owner of its decorations).</summary>
    public int EditorId { get; init; } = 1;

    /// <summary>Changes any other view model option.</summary>
    public Func<ViewModelOptions, ViewModelOptions>? Configure { get; init; }
}

/// <summary>
/// The real view model with what upstream's tests do to it: replacing getCompletelyVisibleViewRange, and listening to
/// the cursor events.
/// </summary>
internal sealed class TestViewModel : EditorViewModel
{
    public TestViewModel(
        int editorId,
        ViewModelConfiguration configuration,
        TextModel model,
        ILineBreaksComputerFactory lineBreaksComputerFactory,
        LanguageConfigurationService languageConfigurationService,
        IAttachedView? attachedView = null)
        : base(editorId, configuration, model, lineBreaksComputerFactory, lineBreaksComputerFactory, TestCodeEditor.NoAnimationFrames, languageConfigurationService, attachedView)
    {
        OutgoingEvent += e =>
        {
            if (e is CursorStateChangedEvent cursorEvent)
            {
                CursorStateChangedEvents.Add(cursorEvent);
            }
        };
    }

    /// <summary>What <see cref="GetCompletelyVisibleViewRange"/> returns, when set (upstream's tests replace the method).</summary>
    public TextRange? CompletelyVisibleViewRange { get; set; }

    /// <summary>The outgoing cursor events (upstream's editor.onDidChangeCursorPosition / Selection).</summary>
    public List<CursorStateChangedEvent> CursorStateChangedEvents { get; } = new();

    /// <summary>How many times the cursor state changed.</summary>
    public int CursorStateChangedCount => CursorStateChangedEvents.Count;

    /// <summary>The reason of the last cursor state change.</summary>
    public CursorChangeReason LastChangeReason => CursorStateChangedEvents.Count > 0 ? CursorStateChangedEvents[^1].Reason : CursorChangeReason.NotSet;

    public override TextRange GetCompletelyVisibleViewRange() => CompletelyVisibleViewRange ?? base.GetCompletelyVisibleViewRange();
}

/// <summary>A text model with a view model and its cursors, configured like upstream's test code editor.</summary>
internal sealed class TestCodeEditor : IDisposable, ICoreCommandsCodeEditor
{
    private readonly bool _ownsModel;
    private readonly bool _ownsService;
    private bool _isDisposed;

    private readonly IAttachedView _attachedView;

    private TestCodeEditor(TextModel model, bool ownsModel, TestCodeEditorOptions options)
    {
        Model = model;
        _ownsModel = ownsModel;
        _ownsService = options.LanguageConfigurationService is null;
        LanguageConfigurationService = options.LanguageConfigurationService ?? new LanguageConfigurationService();
        Configuration = new ViewModelConfiguration(CreateViewModelOptions(options));

        // CodeEditorWidget._attachModel
        _attachedView = Model.OnBeforeAttached();
        var lineBreaksComputerFactory = new MonospaceLineBreaksComputerFactory(
            WrappingOptions.WordWrapBreakBeforeCharacters,
            WrappingOptions.WordWrapBreakAfterCharacters);
        ViewModel = new TestViewModel(options.EditorId, Configuration, Model, lineBreaksComputerFactory, LanguageConfigurationService, _attachedView);

        // instantiateTestCodeEditor
        HasTextFocus = options.HasTextFocus;
        ViewModel.SetHasFocus(options.HasTextFocus);
    }

    public TextModel Model { get; }

    public TestViewModel ViewModel { get; }

    public ViewModelConfiguration Configuration { get; }

    public LanguageConfigurationService LanguageConfigurationService { get; }

    /// <summary>Upstream's TestCodeEditor.hasTextFocus.</summary>
    public bool HasTextFocus { get; set; }

    /// <summary>The TestConfiguration's mock font.</summary>
    public static FontInfo TestFontInfo() => new(
        pixelRatio: 1,
        fontFamily: "mockFont",
        fontWeight: "normal",
        fontSize: 14,
        fontFeatureSettings: "\"liga\" off, \"calt\" off",
        fontVariationSettings: "normal",
        lineHeight: 19,
        letterSpacing: 1.5,
        isMonospace: true,
        typicalHalfwidthCharacterWidth: 10,
        typicalFullwidthCharacterWidth: 20,
        canUseHalfwidthRightwardsArrow: true,
        spaceWidth: 10,
        middotWidth: 10,
        wsmiddotWidth: 10,
        maxDigitWidth: 10,
        isTrusted: true);

    /// <summary>The view model options a test editor with <paramref name="options"/> has.</summary>
    public static ViewModelOptions CreateViewModelOptions(TestCodeEditorOptions options)
    {
        var fontInfo = TestFontInfo();

        // EditorLayoutInfoComputer.computeLayout: the wrapping column.
        var isViewportWrapping = false;
        var wrappingColumn = -1;
        switch (options.WordWrap)
        {
            case TestWordWrap.On:
            case TestWordWrap.Bounded:
                isViewportWrapping = true;
                wrappingColumn = Math.Max(1, options.ViewportColumn);
                if (options.WordWrap == TestWordWrap.Bounded)
                {
                    wrappingColumn = Math.Min(wrappingColumn, options.WordWrapColumn);
                }
                break;
            case TestWordWrap.WordWrapColumn:
                wrappingColumn = options.WordWrapColumn;
                break;
        }

        var cursorOptions = options.EditorOptions with
        {
            LineHeight = fontInfo.LineHeight,
            TypicalHalfwidthCharacterWidth = fontInfo.TypicalHalfwidthCharacterWidth,
            LayoutHeight = options.OuterHeight,
        };

        var result = new ViewModelOptions
        {
            Cursor = cursorOptions,
            InputMode = options.InputMode,
            Layout = new ViewLayoutOptions
            {
                LayoutInfo = new ViewLayoutOptions.LayoutInfoOptions(0, options.OuterHeight, 14),
                LineHeight = fontInfo.LineHeight,
                WrappingInfo = new ViewLayoutOptions.WrappingInfoOptions(isViewportWrapping),
                FontInfo = new ViewLayoutOptions.FontInfoOptions(fontInfo.TypicalHalfwidthCharacterWidth),
            },
            FontInfo = fontInfo,
            WrappingStrategy = options.WrappingStrategy,
            WrappingColumn = wrappingColumn,
            WrappingIndent = options.WrappingIndent,
            WordBreak = options.WordBreak,
            WrapOnEscapedLineFeeds = options.WrapOnEscapedLineFeeds,
        };
        return options.Configure is { } configure ? configure(result) : result;
    }

    /// <summary>Upstream's createTestCodeEditor on a new model of <paramref name="text"/>.</summary>
    public static TestCodeEditor Create(string text, TestCodeEditorOptions? options = null)
    {
        options ??= new TestCodeEditorOptions();
        var modelOptions = options.ModelOptions ?? TextModel.DefaultCreationOptions;
        if (options.TabSize is { } tabSize)
        {
            modelOptions = modelOptions with { TabSize = tabSize };
        }
        var model = new TextModel(
            text,
            options.LanguageId ?? TestTextModel.PlaintextLanguageId,
            modelOptions,
            TestTextModel.CreateUndoRedoService(),
            null,
            options.LanguageIdCodec);
        return new TestCodeEditor(model, true, options);
    }

    /// <summary>Upstream's createTestCodeEditor on the lines joined with \n.</summary>
    public static TestCodeEditor Create(string[] lines, TestCodeEditorOptions? options = null) =>
        Create(string.Join('\n', lines), options);

    /// <summary>Upstream's createTestCodeEditor on a model the test owns (several editors may share it).</summary>
    public static TestCodeEditor Create(TextModel model, TestCodeEditorOptions? options = null) =>
        new(model, false, options ?? new TestCodeEditorOptions());

    /// <summary>Upstream's withTestCodeEditor: an editor on the text, disposed after the callback.</summary>
    public static void WithTestCodeEditor(string text, TestCodeEditorOptions? options, Action<TestCodeEditor, TestViewModel> callback)
    {
        using var editor = Create(text, options);
        editor.ViewModel.SetHasFocus(true);
        callback(editor, editor.ViewModel);
    }

    /// <summary>Upstream's withTestCodeEditor on the lines joined with \n.</summary>
    public static void WithTestCodeEditor(string[] lines, TestCodeEditorOptions? options, Action<TestCodeEditor, TestViewModel> callback) =>
        WithTestCodeEditor(string.Join('\n', lines), options, callback);

    /// <summary>Upstream's withTestCodeEditor on a model the test owns.</summary>
    public static void WithTestCodeEditor(TextModel model, TestCodeEditorOptions? options, Action<TestCodeEditor, TestViewModel> callback)
    {
        using var editor = Create(model, options);
        editor.ViewModel.SetHasFocus(true);
        callback(editor, editor.ViewModel);
    }

    /// <summary>Upstream's withAsyncTestCodeEditor.</summary>
    public static async Task WithAsyncTestCodeEditor(string text, TestCodeEditorOptions? options, Func<TestCodeEditor, TestViewModel, Task> callback)
    {
        using var editor = Create(text, options);
        editor.ViewModel.SetHasFocus(true);
        await callback(editor, editor.ViewModel);
    }

    /// <summary>Upstream's withAsyncTestCodeEditor on the lines joined with \n.</summary>
    public static Task WithAsyncTestCodeEditor(string[] lines, TestCodeEditorOptions? options, Func<TestCodeEditor, TestViewModel, Task> callback) =>
        WithAsyncTestCodeEditor(string.Join('\n', lines), options, callback);

    /// <summary>
    /// The animation frame scheduler of a test editor: there is no view, so no frame comes (upstream passes null; smooth
    /// scrolling is off in tests).
    /// </summary>
    internal static IDisposable NoAnimationFrames(Action callback) => NoopDisposable.Instance;

    public void Dispose()
    {
        if (_isDisposed)
        {
            return;
        }
        _isDisposed = true;

        // CodeEditorWidget._detachModel
        ViewModel.Dispose();
        Model.OnBeforeDetached(_attachedView);
        if (_ownsModel)
        {
            Model.Dispose();
        }
        if (_ownsService)
        {
            LanguageConfigurationService.Dispose();
        }
    }

    // ------ the editor (ICodeEditor) methods the tests call

    public TextModel GetModel() => Model;

    /// <summary>Upstream's _getViewModel.</summary>
    public IViewModel? GetViewModel() => ViewModel;

    /// <summary>The editor's readOnly option.</summary>
    public bool IsReadOnly => ViewModel.Configuration.Options.Cursor.ReadOnly;

    /// <summary>Upstream's pushUndoStop: an undo stop on the model, unless the editor is read-only.</summary>
    public bool PushUndoStop()
    {
        if (IsReadOnly)
        {
            // read only editor => sorry!
            return false;
        }
        Model.PushStackElement();
        return true;
    }

    public string GetValue() => Model.GetValue();

    public void SetValue(string value) => Model.SetValue(value);

    public TextSelection GetSelection() => ViewModel.GetSelection();

    public IReadOnlyList<TextSelection> GetSelections() => ViewModel.GetSelections();

    public TextPosition GetPosition() => ViewModel.GetPosition();

    /// <summary>Upstream's setPosition: a collapsed selection at the position.</summary>
    public void SetPosition(TextPosition position, string source = "api") =>
        ViewModel.SetSelections(source, [new TextSelection(position, position)]);

    public void SetSelection(TextRange range, string source = "api") =>
        SetSelection(new TextSelection(range.StartLineNumber, range.StartColumn, range.EndLineNumber, range.EndColumn), source);

    public void SetSelection(TextSelection selection, string source = "api") =>
        ViewModel.SetSelections(source, [selection]);

    public void SetSelections(IReadOnlyList<TextSelection> selections, string source = "api", CursorChangeReason reason = CursorChangeReason.NotSet) =>
        ViewModel.SetSelections(source, selections, reason);

    /// <summary>Upstream's executeEdits: the end cursor state (null keeps what the edits leave).</summary>
    public bool ExecuteEdits(string? source, IReadOnlyList<SingleEditOperation> edits, IReadOnlyList<TextSelection>? endCursorState = null)
    {
        if (ViewModel.Configuration.Options.Cursor.ReadOnly)
        {
            // read only editor => sorry!
            return false;
        }

        ViewModel.ExecuteEdits(source, edits, _ => endCursorState, EditSources.Unknown(source));
        return true;
    }

    /// <summary>Upstream's executeEdits with a cursor state computer.</summary>
    public bool ExecuteEdits(string? source, IReadOnlyList<SingleEditOperation> edits, CursorStateComputer cursorStateComputer)
    {
        if (ViewModel.Configuration.Options.Cursor.ReadOnly)
        {
            // read only editor => sorry!
            return false;
        }

        ViewModel.ExecuteEdits(source, edits, cursorStateComputer, EditSources.Unknown(source));
        return true;
    }

    public void ExecuteCommand(string? source, ICommand command) => ViewModel.ExecuteCommand(command, source);

    public void ExecuteCommands(string? source, IReadOnlyList<ICommand?> commands) => ViewModel.ExecuteCommands(commands, source);

    /// <summary>Upstream's trigger(source, Handler.Type, { text }).</summary>
    public void Type(string text, string? source = "keyboard") => ViewModel.Type(text, source);

    /// <summary>Upstream's trigger(source, Handler.Paste, { text, pasteOnNewLine, multicursorText }).</summary>
    public void Paste(string text, bool pasteOnNewLine = false, IReadOnlyList<string>? multicursorText = null, string? source = "keyboard") =>
        ViewModel.Paste(text, pasteOnNewLine, multicursorText, source);

    /// <summary>Upstream's trigger(source, Handler.Cut).</summary>
    public void Cut(string? source = "keyboard") => ViewModel.Cut(source);

    /// <summary>Upstream's trigger(source, Handler.CompositionStart).</summary>
    public void StartComposition() => ViewModel.StartComposition();

    /// <summary>Upstream's trigger(source, Handler.CompositionEnd).</summary>
    public void EndComposition(string? source = "keyboard") => ViewModel.EndComposition(source);

    /// <summary>Upstream's trigger(source, Handler.CompositionType, { text, replacePrevCharCnt, ... }).</summary>
    public void CompositionType(string text, int replacePrevCharCnt = 0, int replaceNextCharCnt = 0, int positionDelta = 0, string? source = "keyboard") =>
        ViewModel.CompositionType(text, replacePrevCharCnt, replaceNextCharCnt, positionDelta, source);

    /// <summary>Upstream's CoreEditingCommands.Undo.</summary>
    public ValueTask Undo() => CoreEditingCommands.Undo.RunEditorCommand(this, null);

    /// <summary>Upstream's CoreEditingCommands.Redo.</summary>
    public ValueTask Redo() => CoreEditingCommands.Redo.RunEditorCommand(this, null);

    /// <summary>Changes the editor options (upstream's updateOptions).</summary>
    public void UpdateOptions(Func<ViewModelOptions, ViewModelOptions> update) =>
        Configuration.UpdateOptions(update(Configuration.Options));

    private sealed class NoopDisposable : IDisposable
    {
        public static readonly NoopDisposable Instance = new();

        public void Dispose()
        {
        }
    }
}
