// Ported from vscode (src/vs/editor/common/model/textModel.ts, commit b57d5c9e), MIT license.
// This part: creation, disposal, events, options, reading and validation. Editing is in TextModel.Editing.cs,
// decorations in TextModel.Decorations.cs, find in TextModel.Find.cs.
//
// Differences from upstream:
//  - no bracket pair or guides part yet, and the tokenization part is a stand-in that gives each line one token (phase
//    4, design/architecture/code-editor.md): getWordAtPosition, the bracket and guide queries and the font token
//    decorations are missing; the model takes the language id codec as an argument instead of a language service;
//  - events are .NET events backed by upstream's Emitter (Core/Emitter.cs, for its delivery order); DidChangeContent
//    carries the public form of the change, and view models get both forms first through IViewModelContentListener;
//  - members that upstream marks internal with a leading underscore drop it (_applyUndo is ApplyUndo);
//  - disposal throws ObjectDisposedException where upstream throws BugIndicatingError('Model is disposed!').

using TKCode.Arc.Editor.Cursor;
using TKCode.Arc.Editor.Languages;
using TKCode.Arc.Editor.Model.BracketPairs;
using TKCode.Arc.Editor.Platform;
using TKCode.Arc.Editor.ViewModel;

namespace TKCode.Arc.Editor.Model;

/// <summary>
/// The model of one document (Monaco's <c>TextModel</c>): its text, version, end of line sequence and options, the edits
/// with their undo steps, the decorations and find.
/// </summary>
internal sealed partial class TextModel : IDisposable, IDecorationsTreesHost, ICursorSimpleModel
{
    /// <summary>Text above this many characters is too large to send to another process (used in tests).</summary>
    internal static int ModelSyncLimit { get; set; } = 50 * 1024 * 1024; // 50 MB

    private const int LargeFileSizeThreshold = 20 * 1024 * 1024; // 20 MB
    private const int LargeFileLineCountThreshold = 300 * 1000; // 300K lines
    private const long LargeFileHeapOperationThreshold = 256L * 1024 * 1024; // 256M characters, usually ~> 512MB memory usage

    private const int LimitFindCount = 999;
    private const int LongLineBoundary = 10000;

    private static int s_modelId;

    private readonly IUndoRedoService _undoRedoService;
    private readonly HashSet<IViewModelContentListener> _viewModels = new();
    private readonly DidChangeDecorationsEmitter _onDidChangeDecorations;
    private readonly DidChangeContentEmitter _eventEmitter;

    private readonly bool _isTooLargeForSyncing;
    private readonly bool _isTooLargeForTokenization;
    private readonly bool _isTooLargeForHeapOperation;

    private int _attachedEditorCount;
    private PieceTreeTextBuffer _buffer;
    private TextModelResolvedOptions _options;
    private string _languageId;

    private bool _isDisposed;
    private bool _isDisposing;
    private int _versionId;

    // Unlike the version id, this can go down (via undo) or go to previous values (via redo).
    private int _alternativeVersionId;
    private ResourceEditStackSnapshot? _initialUndoRedoSnapshot;

    public TextModel(
        string source,
        string languageId,
        TextModelCreationOptions creationOptions,
        IUndoRedoService undoRedoService,
        Uri? associatedResource = null,
        ILanguageIdCodec? languageIdCodec = null,
        TokenizationRegistry<ITokenizationSupport>? tokenizationRegistry = null,
        IdleScheduler? scheduler = null)
        : this(CreateTextBufferFactory(source), languageId, creationOptions, undoRedoService, associatedResource, languageIdCodec, tokenizationRegistry, scheduler)
    {
    }

    public TextModel(
        PieceTreeTextBufferFactory source,
        string languageId,
        TextModelCreationOptions creationOptions,
        IUndoRedoService undoRedoService,
        Uri? associatedResource = null,
        ILanguageIdCodec? languageIdCodec = null,
        TokenizationRegistry<ITokenizationSupport>? tokenizationRegistry = null,
        IdleScheduler? scheduler = null)
    {
        _undoRedoService = undoRedoService;
        _onDidChangeDecorations = new DidChangeDecorationsEmitter(
            HandleBeforeFireDecorationsChangedEvent, _onDidChangeDecorationsEvent.Fire);
        _eventEmitter = new DidChangeContentEmitter(_onDidChangeContent.Fire);

        // Generate a new unique model id
        var modelId = Interlocked.Increment(ref s_modelId);
        Id = "$model" + modelId;
        IsForSimpleWidget = creationOptions.IsForSimpleWidget;
        Uri = associatedResource ?? new Uri("inmemory://model/" + modelId);
        _attachedEditorCount = 0;

        _buffer = new PieceTreeTextBuffer(source.Create(creationOptions.DefaultEOL));

        var bufferLineCount = _buffer.GetLineCount();
        var bufferTextLength = _buffer.GetValueLengthInRange(
            new TextRange(1, 1, bufferLineCount, _buffer.GetLineLength(bufferLineCount) + 1),
            EndOfLinePreference.TextDefined);

        // !!! Make a decision in the ctor and permanently respect this decision !!!
        // If a model is too large at construction time, it will never get tokenized,
        // under no circumstances.
        if (creationOptions.LargeFileOptimizations)
        {
            _isTooLargeForTokenization = bufferTextLength > LargeFileSizeThreshold
                || bufferLineCount > LargeFileLineCountThreshold;

            _isTooLargeForHeapOperation = bufferTextLength > LargeFileHeapOperationThreshold;
        }
        else
        {
            _isTooLargeForTokenization = false;
            _isTooLargeForHeapOperation = false;
        }

        _options = ResolveOptions(_buffer, creationOptions);
        _languageId = languageId;
        if (languageIdCodec is null)
        {
            // A model on its own: a codec that knows its language. Editors share the codec of their language service.
            var codec = new LanguageIdCodec();
            codec.Register(languageId);
            languageIdCodec = codec;
        }

        // Without a registry the model has no tokenizer (every line is one token of its language); without a scheduler it
        // tokenizes only on demand (ForceTokenization, the visible lines of an attached view).
        Tokenization = new TokenizationTextModelPart(
            this, languageIdCodec, tokenizationRegistry ?? new TokenizationRegistry<ITokenizationSupport>(), scheduler ?? IdleScheduler.None);

        _isTooLargeForSyncing = bufferTextLength > ModelSyncLimit;

        _versionId = 1;
        _alternativeVersionId = 1;
        _initialUndoRedoSnapshot = null;

        _isDisposed = false;
        _isDisposing = false;

        _instanceId = Strings.SingleLetterHash(modelId);
        _lastDecorationId = 0;
        _decorations = new Dictionary<string, IntervalNode>();
        _decorationsTree = new DecorationsTrees();

        _commandManager = new EditStack(this, _undoRedoService);
        _isUndoing = false;
        _isRedoing = false;
        _trimAutoWhitespaceLines = null;
    }

    /// <summary>The options a model gets when nothing else is asked for (Monaco's DEFAULT_CREATION_OPTIONS).</summary>
    public static TextModelCreationOptions DefaultCreationOptions { get; } = new()
    {
        IsForSimpleWidget = false,
        TabSize = EditorModelDefaults.TabSize,
        IndentSize = EditorModelDefaults.IndentSize,
        InsertSpaces = EditorModelDefaults.InsertSpaces,
        DetectIndentation = false,
        DefaultEOL = DefaultEndOfLine.LF,
        TrimAutoWhitespace = EditorModelDefaults.TrimAutoWhitespace,
        LargeFileOptimizations = EditorModelDefaults.LargeFileOptimizations,
        BracketPairColorizationOptions = EditorModelDefaults.BracketPairColorizationOptions,
    };

    #region Events

    // Upstream's Emitter, not .NET events: a listener that raises the event again (an edit from DidChangeContent) must
    // not reach the other listeners before the event in flight does (Emitter.cs).
    private readonly Emitter<bool> _onWillDispose = new();
    private readonly Emitter<ModelDecorationsChangedEvent> _onDidChangeDecorationsEvent = new();
    private readonly Emitter<ModelLanguageChangedEvent> _onDidChangeLanguage = new();
    private readonly Emitter<ModelOptionsChangedEvent> _onDidChangeOptions = new();
    private readonly Emitter<bool> _onDidChangeAttached = new();
    private readonly Emitter<ModelLineHeightChangedEvent> _onDidChangeLineHeight = new();
    private readonly Emitter<ModelFontChangedEvent> _onDidChangeFont = new();

    // DidChangeContent and DidChangeInternalContent share one emitter, as upstream's onDidChangeContent listens to the
    // internal event: their listeners get each change in the order they were added.
    private readonly Emitter<InternalModelContentChangeEvent> _onDidChangeContent = new();

    /// <summary>The model is about to be disposed.</summary>
    public event Action? WillDispose
    {
        add => AddListener(_onWillDispose, value, value is null ? null : _ => value());
        remove => _onWillDispose.Remove(value);
    }

    /// <summary>The decorations changed; raised once per batch.</summary>
    public event Action<ModelDecorationsChangedEvent>? DidChangeDecorations
    {
        add => AddListener(_onDidChangeDecorationsEvent, value, value);
        remove => _onDidChangeDecorationsEvent.Remove(value);
    }

    /// <summary>The tokens of some lines changed (upstream's onDidChangeTokens, forwarded from the tokenization part).</summary>
    public event Action<ModelTokensChangedEvent> DidChangeTokens
    {
        add => Tokenization.DidChangeTokens += value;
        remove => Tokenization.DidChangeTokens -= value;
    }

    /// <summary>The language id changed.</summary>
    public event Action<ModelLanguageChangedEvent>? DidChangeLanguage
    {
        add => AddListener(_onDidChangeLanguage, value, value);
        remove => _onDidChangeLanguage.Remove(value);
    }

    /// <summary>The options changed.</summary>
    public event Action<ModelOptionsChangedEvent>? DidChangeOptions
    {
        add => AddListener(_onDidChangeOptions, value, value);
        remove => _onDidChangeOptions.Remove(value);
    }

    /// <summary>The model got its first editor or lost its last one.</summary>
    public event Action? DidChangeAttached
    {
        add => AddListener(_onDidChangeAttached, value, value is null ? null : _ => value());
        remove => _onDidChangeAttached.Remove(value);
    }

    /// <summary>Decorations changed the height of some lines.</summary>
    public event Action<ModelLineHeightChangedEvent>? DidChangeLineHeight
    {
        add => AddListener(_onDidChangeLineHeight, value, value);
        remove => _onDidChangeLineHeight.Remove(value);
    }

    /// <summary>Decorations changed the font of some lines.</summary>
    public event Action<ModelFontChangedEvent>? DidChangeFont
    {
        add => AddListener(_onDidChangeFont, value, value);
        remove => _onDidChangeFont.Remove(value);
    }

    /// <summary>The content changed; raised once per batch of edits, with the batch's changes merged.</summary>
    public event Action<ModelContentChangedEvent>? DidChangeContent
    {
        add => AddListener(_onDidChangeContent, value, value is null ? null : e => value(e.ContentChangedEvent));
        remove => _onDidChangeContent.Remove(value);
    }

    /// <summary>The content changed: both forms of the change, from the same emitter as <see cref="DidChangeContent"/>.</summary>
    internal event Action<InternalModelContentChangeEvent>? DidChangeInternalContent
    {
        add => AddListener(_onDidChangeContent, value, value);
        remove => _onDidChangeContent.Remove(value);
    }

    private static void AddListener<T>(Emitter<T> emitter, object? key, Action<T>? listener)
    {
        if (listener is not null)
        {
            emitter.Add(listener, key);
        }
    }

    #endregion

    /// <summary>The tokens of the lines and the language at a position (upstream's <c>tokenization</c> part).</summary>
    public TokenizationTextModelPart Tokenization { get; }

    /// <summary>A unique id of the model in this process.</summary>
    public string Id { get; }

    public bool IsForSimpleWidget { get; }

    /// <summary>The resource the model belongs to; its undo steps are kept under this resource.</summary>
    public Uri Uri { get; }

    internal bool IsDisposing => _isDisposing;

    public static PieceTreeTextBufferFactory CreateTextBufferFactory(string text)
    {
        var builder = new PieceTreeTextBufferBuilder();
        builder.AcceptChunk(text);
        return builder.Finish();
    }

    public static PieceTreeTextBufferFactory CreateTextBufferFactoryFromSnapshot(ITextSnapshot snapshot)
    {
        var builder = new PieceTreeTextBufferBuilder();

        string? chunk;
        while ((chunk = snapshot.Read()) is not null)
        {
            builder.AcceptChunk(chunk);
        }

        return builder.Finish();
    }

    public static TextModelResolvedOptions ResolveOptions(PieceTreeTextBuffer textBuffer, TextModelCreationOptions options)
    {
        if (options.DetectIndentation)
        {
            var guessedIndentation = IndentationGuesser.GuessIndentation(textBuffer, options.TabSize, options.InsertSpaces);
            return new TextModelResolvedOptions(
                guessedIndentation.TabSize,
                null, // TODO@Alex: guess indentSize independent of tabSize
                guessedIndentation.InsertSpaces,
                options.DefaultEOL,
                options.TrimAutoWhitespace,
                options.BracketPairColorizationOptions);
        }

        return new TextModelResolvedOptions(
            options.TabSize,
            options.IndentSize,
            options.InsertSpaces,
            options.DefaultEOL,
            options.TrimAutoWhitespace,
            options.BracketPairColorizationOptions);
    }

    public void Dispose()
    {
        if (_isDisposed)
        {
            return;
        }

        _isDisposing = true;
        _onWillDispose.Fire(true);
        _isDisposed = true;
        _isDisposing = false;
        // Manually release reference to previous text buffer to avoid large leaks
        // in case someone leaks a TextModel reference
        DisposeBracketPairs();
        Tokenization.Dispose();
        _buffer = PieceTreeTextBuffer.Create(string.Empty);
        _decorations.Clear();
        _decorationsTree = new DecorationsTrees();
        _viewModels.Clear();
        _onWillDispose.Clear();
        _onDidChangeDecorationsEvent.Clear();
        _onDidChangeLanguage.Clear();
        _onDidChangeOptions.Clear();
        _onDidChangeAttached.Clear();
        _onDidChangeLineHeight.Clear();
        _onDidChangeFont.Clear();
        _onDidChangeContent.Clear();
    }

    internal bool HasListeners() =>
        _onWillDispose.HasListeners
        || _onDidChangeDecorationsEvent.HasListeners
        || _onDidChangeLanguage.HasListeners
        || _onDidChangeOptions.HasListeners
        || _onDidChangeAttached.HasListeners
        || _onDidChangeLineHeight.HasListeners
        || _onDidChangeFont.HasListeners
        || _onDidChangeContent.HasListeners
        || Tokenization.HasListeners;

    internal void AssertNotDisposed() => ObjectDisposedException.ThrowIf(_isDisposed, this);

    internal void RegisterViewModel(IViewModelContentListener viewModel) => _viewModels.Add(viewModel);

    internal void UnregisterViewModel(IViewModelContentListener viewModel) => _viewModels.Remove(viewModel);

    public bool EqualsTextBuffer(PieceTreeTextBuffer other)
    {
        AssertNotDisposed();
        return _buffer.Equals(other);
    }

    internal PieceTreeTextBuffer GetTextBuffer()
    {
        AssertNotDisposed();
        return _buffer;
    }

    private void EmitContentChangedEvent(
        ModelRawContentChangedEvent rawChange,
        ModelContentChangedEvent change,
        IReadOnlyList<TextSelection>? resultingSelection = null)
    {
        if (_isDisposing)
        {
            // Do not confuse listeners by emitting any event after disposing
            return;
        }

        var contentChangeEvent = new InternalModelContentChangeEvent(rawChange, change);
        // Set resultingSelection early so viewModels can use it for cursor positioning
        if (resultingSelection is not null)
        {
            contentChangeEvent.RawContentChangedEvent.ResultingSelection = resultingSelection;
        }

        _bracketPairs?.Part.HandleDidChangeContent(change);
        Tokenization.HandleDidChangeContent(change);
        OnDidChangeContentOrInjectedText(contentChangeEvent);
        _eventEmitter.Fire(contentChangeEvent);
    }

    /// <summary>An editor starts showing the model; it reports its visible lines through the result (upstream's IAttachedView).</summary>
    public IAttachedView OnBeforeAttached()
    {
        _attachedEditorCount++;
        if (_attachedEditorCount == 1)
        {
            Tokenization.HandleDidChangeAttached();
            _onDidChangeAttached.Fire(true);
        }

        return Tokenization.AttachView();
    }

    public void OnBeforeDetached(IAttachedView view)
    {
        _attachedEditorCount--;
        if (_attachedEditorCount == 0)
        {
            Tokenization.HandleDidChangeAttached();
            _onDidChangeAttached.Fire(true);
        }

        Tokenization.DetachView(view);
    }

    public bool IsAttachedToEditor() => _attachedEditorCount > 0;

    public int GetAttachedEditorCount() => _attachedEditorCount;

    public bool IsTooLargeForSyncing() => _isTooLargeForSyncing;

    public bool IsTooLargeForTokenization() => _isTooLargeForTokenization;

    public bool IsTooLargeForHeapOperation() => _isTooLargeForHeapOperation;

    public bool IsDisposed() => _isDisposed;

    public bool IsDominatedByLongLines()
    {
        AssertNotDisposed();
        if (IsTooLargeForTokenization())
        {
            // Cannot word wrap huge files anyways, so it doesn't really matter
            return false;
        }

        long smallLineCharCount = 0;
        long longLineCharCount = 0;

        var lineCount = _buffer.GetLineCount();
        for (var lineNumber = 1; lineNumber <= lineCount; lineNumber++)
        {
            var lineLength = _buffer.GetLineLength(lineNumber);
            if (lineLength >= LongLineBoundary)
            {
                longLineCharCount += lineLength;
            }
            else
            {
                smallLineCharCount += lineLength;
            }
        }

        return longLineCharCount > smallLineCharCount;
    }

    #region Options

    public TextModelResolvedOptions GetOptions()
    {
        AssertNotDisposed();
        return _options;
    }

    /// <summary>The options a formatter needs: the indent size as its tab size, and whether to insert spaces.</summary>
    public (int TabSize, bool InsertSpaces) GetFormattingOptions() => (_options.IndentSize, _options.InsertSpaces);

    public void UpdateOptions(TextModelUpdateOptions newOpts)
    {
        AssertNotDisposed();
        var tabSize = newOpts.TabSize ?? _options.TabSize;
        var indentSize = newOpts.IndentSizeIsTabSize ? null : newOpts.IndentSize ?? _options.OriginalIndentSize;
        var insertSpaces = newOpts.InsertSpaces ?? _options.InsertSpaces;
        var trimAutoWhitespace = newOpts.TrimAutoWhitespace ?? _options.TrimAutoWhitespace;
        var bracketPairColorizationOptions = newOpts.BracketColorizationOptions ?? _options.BracketPairColorizationOptions;

        var resolved = new TextModelResolvedOptions(
            tabSize,
            indentSize,
            insertSpaces,
            _options.DefaultEOL,
            trimAutoWhitespace,
            bracketPairColorizationOptions);

        if (_options.Equals(resolved))
        {
            return;
        }

        var e = _options.CreateChangeEvent(resolved);
        _options = resolved;

        _bracketPairs?.Part.HandleDidChangeOptions();
        _onDidChangeOptions.Fire(e);
    }

    public void DetectIndentation(bool defaultInsertSpaces, int defaultTabSize)
    {
        AssertNotDisposed();
        var guessedIndentation = IndentationGuesser.GuessIndentation(_buffer, defaultTabSize, defaultInsertSpaces);
        UpdateOptions(new TextModelUpdateOptions
        {
            InsertSpaces = guessedIndentation.InsertSpaces,
            TabSize = guessedIndentation.TabSize,
            IndentSize = guessedIndentation.TabSize, // TODO@Alex: guess indentSize independent of tabSize
        });
    }

    public string NormalizeIndentation(string str)
    {
        AssertNotDisposed();
        return Indentation.NormalizeIndentation(str, _options.IndentSize, _options.InsertSpaces);
    }

    #endregion

    #region Reading

    public int GetVersionId()
    {
        AssertNotDisposed();
        return _versionId;
    }

    public bool MightContainRTL() => _buffer.MightContainRTL();

    public bool MightContainUnusualLineTerminators() => _buffer.MightContainUnusualLineTerminators();

    /// <summary>Removes every U+2028 (line separator) and U+2029 (paragraph separator) in one undo step.</summary>
    public void RemoveUnusualLineTerminators(IReadOnlyList<TextSelection>? selections = null)
    {
        var matches = FindMatches(@"[\u2028\u2029]", false, true, false, null, false, int.MaxValue);
        _buffer.ResetMightContainUnusualLineTerminators();
        PushEditOperations(selections, matches.Select(m => new SingleEditOperation(m.Range, null)).ToList(), static _ => null);
    }

    public bool MightContainNonBasicASCII() => _buffer.MightContainNonBasicASCII();

    public int GetAlternativeVersionId()
    {
        AssertNotDisposed();
        return _alternativeVersionId;
    }

    internal ResourceEditStackSnapshot? GetInitialUndoRedoSnapshot()
    {
        AssertNotDisposed();
        return _initialUndoRedoSnapshot;
    }

    public int GetOffsetAt(TextPosition rawPosition)
    {
        AssertNotDisposed();
        var position = ValidatePosition(rawPosition.LineNumber, rawPosition.Column, StringOffsetValidationType.Relaxed);
        return _buffer.GetOffsetAt(position.LineNumber, position.Column);
    }

    public TextPosition GetPositionAt(int rawOffset)
    {
        AssertNotDisposed();
        var offset = Math.Min(_buffer.GetLength(), Math.Max(0, rawOffset));
        return _buffer.GetPositionAt(offset);
    }

    private void IncreaseVersionId()
    {
        _versionId++;
        _alternativeVersionId = _versionId;
    }

    internal void OverwriteVersionId(int versionId) => _versionId = versionId;

    internal void OverwriteAlternativeVersionId(int newAlternativeVersionId) => _alternativeVersionId = newAlternativeVersionId;

    internal void OverwriteInitialUndoRedoSnapshot(ResourceEditStackSnapshot? newInitialUndoRedoSnapshot) =>
        _initialUndoRedoSnapshot = newInitialUndoRedoSnapshot;

    public string GetValue(EndOfLinePreference eol = EndOfLinePreference.TextDefined, bool preserveBOM = false)
    {
        AssertNotDisposed();
        if (IsTooLargeForHeapOperation())
        {
            throw new InvalidOperationException("Operation would exceed heap memory limits");
        }

        var fullModelRange = GetFullModelRange();
        var fullModelValue = GetValueInRange(fullModelRange, eol);

        return preserveBOM ? _buffer.GetBOM() + fullModelValue : fullModelValue;
    }

    public ITextSnapshot CreateSnapshot(bool preserveBOM = false) => new TextModelSnapshot(_buffer.CreateSnapshot(preserveBOM));

    /// <summary>
    /// The text at this version, for reading by line or range on another thread (tokenization, diff, find). Taking it
    /// costs O(pieces) and copies no text; it stays valid and unchanged while the model is edited.
    /// </summary>
    public TextSnapshot CreateTextSnapshot()
    {
        AssertNotDisposed();
        return _buffer.CreateTextSnapshot(_versionId);
    }

    public int GetValueLength(EndOfLinePreference eol = EndOfLinePreference.TextDefined, bool preserveBOM = false)
    {
        AssertNotDisposed();
        var fullModelRange = GetFullModelRange();
        var fullModelValue = GetValueLengthInRange(fullModelRange, eol);

        return preserveBOM ? _buffer.GetBOM().Length + fullModelValue : fullModelValue;
    }

    public string GetValueInRange(TextRange rawRange, EndOfLinePreference eol = EndOfLinePreference.TextDefined)
    {
        AssertNotDisposed();
        return _buffer.GetValueInRange(ValidateRange(rawRange), eol);
    }

    public int GetValueLengthInRange(TextRange rawRange, EndOfLinePreference eol = EndOfLinePreference.TextDefined)
    {
        AssertNotDisposed();
        return _buffer.GetValueLengthInRange(ValidateRange(rawRange), eol);
    }

    public int GetCharacterCountInRange(TextRange rawRange, EndOfLinePreference eol = EndOfLinePreference.TextDefined)
    {
        AssertNotDisposed();
        return _buffer.GetCharacterCountInRange(ValidateRange(rawRange), eol);
    }

    public int GetLineCount()
    {
        AssertNotDisposed();
        return _buffer.GetLineCount();
    }

    public string GetLineContent(int lineNumber)
    {
        AssertNotDisposed();
        AssertLineNumber(lineNumber);
        return _buffer.GetLineContent(lineNumber);
    }

    /// <summary>
    /// The content of a line as a view of the buffer where possible (see <see cref="PieceTreeTextBuffer.GetLineContentSpan"/>).
    /// The view stays valid after later edits but shows the line as it was.
    /// </summary>
    public ReadOnlySpan<char> GetLineContentSpan(int lineNumber)
    {
        AssertNotDisposed();
        AssertLineNumber(lineNumber);
        return _buffer.GetLineContentSpan(lineNumber);
    }

    public int GetLineLength(int lineNumber)
    {
        AssertNotDisposed();
        AssertLineNumber(lineNumber);
        return _buffer.GetLineLength(lineNumber);
    }

    public string[] GetLinesContent()
    {
        AssertNotDisposed();
        if (IsTooLargeForHeapOperation())
        {
            throw new InvalidOperationException("Operation would exceed heap memory limits");
        }

        return _buffer.GetLinesContent();
    }

    public string GetEOL()
    {
        AssertNotDisposed();
        return _buffer.GetEOL();
    }

    public EndOfLineSequence GetEndOfLineSequence()
    {
        AssertNotDisposed();
        return _buffer.GetEOL() == "\n" ? EndOfLineSequence.LF : EndOfLineSequence.CRLF;
    }

    public int GetLineMinColumn(int lineNumber)
    {
        AssertNotDisposed();
        return 1;
    }

    public int GetLineMaxColumn(int lineNumber)
    {
        AssertNotDisposed();
        AssertLineNumber(lineNumber);
        return _buffer.GetLineLength(lineNumber) + 1;
    }

    public int GetLineFirstNonWhitespaceColumn(int lineNumber)
    {
        AssertNotDisposed();
        AssertLineNumber(lineNumber);
        return _buffer.GetLineFirstNonWhitespaceColumn(lineNumber);
    }

    public int GetLineLastNonWhitespaceColumn(int lineNumber)
    {
        AssertNotDisposed();
        AssertLineNumber(lineNumber);
        return _buffer.GetLineLastNonWhitespaceColumn(lineNumber);
    }

    private void AssertLineNumber(int lineNumber)
    {
        if (lineNumber < 1 || lineNumber > GetLineCount())
        {
            throw new ArgumentOutOfRangeException(nameof(lineNumber), lineNumber, "Illegal value for lineNumber");
        }
    }

    /// <summary>
    /// Validates <paramref name="range"/> is within buffer bounds, but allows it to sit in between surrogate pairs, etc.
    /// </summary>
    internal TextRange ValidateRangeRelaxedNoAllocations(TextRange range)
    {
        var linesCount = _buffer.GetLineCount();

        var startLineNumber = range.StartLineNumber;
        var startColumn = range.StartColumn;

        if (startLineNumber < 1)
        {
            startLineNumber = 1;
            startColumn = 1;
        }
        else if (startLineNumber > linesCount)
        {
            startLineNumber = linesCount;
            startColumn = GetLineMaxColumn(startLineNumber);
        }
        else if (startColumn <= 1)
        {
            startColumn = 1;
        }
        else
        {
            var maxColumn = GetLineMaxColumn(startLineNumber);
            if (startColumn >= maxColumn)
            {
                startColumn = maxColumn;
            }
        }

        var endLineNumber = range.EndLineNumber;
        var endColumn = range.EndColumn;

        if (endLineNumber < 1)
        {
            endLineNumber = 1;
            endColumn = 1;
        }
        else if (endLineNumber > linesCount)
        {
            endLineNumber = linesCount;
            endColumn = GetLineMaxColumn(endLineNumber);
        }
        else if (endColumn <= 1)
        {
            endColumn = 1;
        }
        else
        {
            var maxColumn = GetLineMaxColumn(endLineNumber);
            if (endColumn >= maxColumn)
            {
                endColumn = maxColumn;
            }
        }

        return new TextRange(startLineNumber, startColumn, endLineNumber, endColumn);
    }

    private bool IsValidPosition(int lineNumber, int column, StringOffsetValidationType validationType)
    {
        if (lineNumber < 1 || column < 1)
        {
            return false;
        }

        var lineCount = _buffer.GetLineCount();
        if (lineNumber > lineCount)
        {
            return false;
        }

        if (column == 1)
        {
            return true;
        }

        var maxColumn = GetLineMaxColumn(lineNumber);
        if (column > maxColumn)
        {
            return false;
        }

        if (validationType == StringOffsetValidationType.SurrogatePairs)
        {
            // !!At this point, column > 1
            var charCodeBefore = _buffer.GetLineCharCode(lineNumber, column - 2);
            if (char.IsHighSurrogate((char)charCodeBefore))
            {
                return false;
            }
        }

        return true;
    }

    private TextPosition ValidatePosition(int lineNumber, int column, StringOffsetValidationType validationType)
    {
        var lineCount = _buffer.GetLineCount();

        if (lineNumber < 1)
        {
            return new TextPosition(1, 1);
        }

        if (lineNumber > lineCount)
        {
            return new TextPosition(lineCount, GetLineMaxColumn(lineCount));
        }

        if (column <= 1)
        {
            return new TextPosition(lineNumber, 1);
        }

        var maxColumn = GetLineMaxColumn(lineNumber);
        if (column >= maxColumn)
        {
            return new TextPosition(lineNumber, maxColumn);
        }

        if (validationType == StringOffsetValidationType.SurrogatePairs)
        {
            // If the position would end up in the middle of a high-low surrogate pair,
            // we move it to before the pair
            // !!At this point, column > 1
            var charCodeBefore = _buffer.GetLineCharCode(lineNumber, column - 2);
            if (char.IsHighSurrogate((char)charCodeBefore))
            {
                return new TextPosition(lineNumber, column - 1);
            }
        }

        return new TextPosition(lineNumber, column);
    }

    /// <summary>The nearest position inside the text that is not inside a surrogate pair.</summary>
    public TextPosition ValidatePosition(TextPosition position)
    {
        AssertNotDisposed();

        if (IsValidPosition(position.LineNumber, position.Column, StringOffsetValidationType.SurrogatePairs))
        {
            return position;
        }

        return ValidatePosition(position.LineNumber, position.Column, StringOffsetValidationType.SurrogatePairs);
    }

    public bool IsValidRange(TextRange range) => IsValidRange(range, StringOffsetValidationType.SurrogatePairs);

    private bool IsValidRange(TextRange range, StringOffsetValidationType validationType)
    {
        var startLineNumber = range.StartLineNumber;
        var startColumn = range.StartColumn;
        var endLineNumber = range.EndLineNumber;
        var endColumn = range.EndColumn;

        if (!IsValidPosition(startLineNumber, startColumn, StringOffsetValidationType.Relaxed))
        {
            return false;
        }

        if (!IsValidPosition(endLineNumber, endColumn, StringOffsetValidationType.Relaxed))
        {
            return false;
        }

        if (validationType == StringOffsetValidationType.SurrogatePairs)
        {
            var charCodeBeforeStart = startColumn > 1 ? _buffer.GetLineCharCode(startLineNumber, startColumn - 2) : 0;
            var charCodeBeforeEnd = endColumn > 1 && endColumn <= _buffer.GetLineLength(endLineNumber)
                ? _buffer.GetLineCharCode(endLineNumber, endColumn - 2)
                : 0;

            var startInsideSurrogatePair = char.IsHighSurrogate((char)charCodeBeforeStart);
            var endInsideSurrogatePair = char.IsHighSurrogate((char)charCodeBeforeEnd);

            return !startInsideSurrogatePair && !endInsideSurrogatePair;
        }

        return true;
    }

    /// <summary>
    /// The range clamped to the text, with an end inside a surrogate pair moved out of it (an empty range moves, a
    /// non-empty one grows).
    /// </summary>
    public TextRange ValidateRange(TextRange range)
    {
        AssertNotDisposed();

        if (IsValidRange(range, StringOffsetValidationType.SurrogatePairs))
        {
            return range;
        }

        var start = ValidatePosition(range.StartLineNumber, range.StartColumn, StringOffsetValidationType.Relaxed);
        var end = ValidatePosition(range.EndLineNumber, range.EndColumn, StringOffsetValidationType.Relaxed);

        var startLineNumber = start.LineNumber;
        var startColumn = start.Column;
        var endLineNumber = end.LineNumber;
        var endColumn = end.Column;

        var charCodeBeforeStart = startColumn > 1 ? _buffer.GetLineCharCode(startLineNumber, startColumn - 2) : 0;
        var charCodeBeforeEnd = endColumn > 1 && endColumn <= _buffer.GetLineLength(endLineNumber)
            ? _buffer.GetLineCharCode(endLineNumber, endColumn - 2)
            : 0;

        var startInsideSurrogatePair = char.IsHighSurrogate((char)charCodeBeforeStart);
        var endInsideSurrogatePair = char.IsHighSurrogate((char)charCodeBeforeEnd);

        if (!startInsideSurrogatePair && !endInsideSurrogatePair)
        {
            return new TextRange(startLineNumber, startColumn, endLineNumber, endColumn);
        }

        if (startLineNumber == endLineNumber && startColumn == endColumn)
        {
            // do not expand a collapsed range, simply move it to a valid location
            return new TextRange(startLineNumber, startColumn - 1, endLineNumber, endColumn - 1);
        }

        if (startInsideSurrogatePair && endInsideSurrogatePair)
        {
            // expand range at both ends
            return new TextRange(startLineNumber, startColumn - 1, endLineNumber, endColumn + 1);
        }

        if (startInsideSurrogatePair)
        {
            // only expand range at the start
            return new TextRange(startLineNumber, startColumn - 1, endLineNumber, endColumn);
        }

        // only expand range at the end
        return new TextRange(startLineNumber, startColumn, endLineNumber, endColumn + 1);
    }

    /// <summary>The position <paramref name="offset"/> characters after (or before, when negative) a position.</summary>
    public TextPosition ModifyPosition(TextPosition rawPosition, int offset)
    {
        AssertNotDisposed();
        var candidate = GetOffsetAt(rawPosition) + offset;
        return GetPositionAt(Math.Min(_buffer.GetLength(), Math.Max(0, candidate)));
    }

    public TextRange GetFullModelRange()
    {
        AssertNotDisposed();
        var lineCount = GetLineCount();
        return new TextRange(1, 1, lineCount, GetLineMaxColumn(lineCount));
    }

    #endregion

    #region Bracket pairs

    private (BracketPairsTextModelPart Part, LanguageConfigurationService Service, ColorizedBracketPairsDecorationProvider DecorationProvider)? _bracketPairs;

    /// <summary>
    /// The model's bracket pairs (upstream's model.bracketPairs), read with the language configurations of
    /// <paramref name="languageConfigurationService"/>. Upstream's model is created with the editor's language
    /// configuration service; here the part is created on first use with the service it is asked with (the cursor's and
    /// the view's are the editor's), and created again if another service asks.
    /// </summary>
    public BracketPairsTextModelPart GetBracketPairs(LanguageConfigurationService languageConfigurationService)
    {
        if (_bracketPairs is { } existing && ReferenceEquals(existing.Service, languageConfigurationService))
        {
            return existing.Part;
        }

        DisposeBracketPairs();
        var part = new BracketPairsTextModelPart(this, languageConfigurationService);
        languageConfigurationService.DidChange += HandleLanguageConfigurationServiceChange;
        var decorationProvider = new ColorizedBracketPairsDecorationProvider(this, part, FireBracketDecorationsChanged);
        _bracketPairs = (part, languageConfigurationService, decorationProvider);
        return part;
    }

    private GuidesTextModelPart? _guides;

    /// <summary>
    /// The model's indent and bracket pair guides (upstream's model.guides), read with the language configurations and
    /// bracket pairs of <paramref name="languageConfigurationService"/>; created on first use like
    /// <see cref="GetBracketPairs"/>.
    /// </summary>
    public GuidesTextModelPart GetGuides(LanguageConfigurationService languageConfigurationService)
    {
        GetBracketPairs(languageConfigurationService);
        if (_guides is null || !ReferenceEquals(_guidesService, languageConfigurationService))
        {
            _guides = new GuidesTextModelPart(this, languageConfigurationService);
            _guidesService = languageConfigurationService;
        }

        return _guides;
    }

    private LanguageConfigurationService? _guidesService;

    /// <summary>The bracket pairs part, if one was created.</summary>
    internal BracketPairsTextModelPart? BracketPairs => _bracketPairs?.Part;

    private void HandleLanguageConfigurationServiceChange(LanguageConfigurationServiceChangeEvent e) =>
        _bracketPairs?.Part.HandleLanguageConfigurationServiceChange(e);

    private void DisposeBracketPairs()
    {
        if (_bracketPairs is { } existing)
        {
            existing.Service.DidChange -= HandleLanguageConfigurationServiceChange;
            existing.DecorationProvider.Dispose();
            existing.Part.Dispose();
            _bracketPairs = null;
        }
    }

    /// <summary>The colorized bracket decorations (upstream's _decorationProvider), once the bracket pairs part exists.</summary>
    private ColorizedBracketPairsDecorationProvider? BracketDecorationProvider => _bracketPairs?.DecorationProvider;

    private void FireBracketDecorationsChanged()
    {
        _onDidChangeDecorations.BeginDeferredEmit();
        _onDidChangeDecorations.Fire();
        _onDidChangeDecorations.EndDeferredEmit();
    }

    internal void HandleDidChangeTokensForBracketPairs(ModelTokensChangedEvent e) => _bracketPairs?.Part.HandleDidChangeTokens(e);

    internal void HandleDidChangeBackgroundTokenizationStateForBracketPairs() => _bracketPairs?.Part.HandleDidChangeBackgroundTokenizationState();

    #endregion

    #region Language

    public string GetLanguageId() => _languageId;

    public void SetLanguage(string languageId, string? source = null)
    {
        if (_languageId == languageId)
        {
            // There's nothing to do
            return;
        }

        var e = new ModelLanguageChangedEvent(_languageId, languageId, source ?? string.Empty);
        _languageId = languageId;
        Tokenization.HandleLanguageChanged();
        _bracketPairs?.Part.HandleDidChangeLanguage();
        _onDidChangeLanguage.Fire(e);
    }

    /// <summary>The language at a position (of the token there).</summary>
    public string GetLanguageIdAtPosition(int lineNumber, int column) => Tokenization.GetLanguageIdAtPosition(lineNumber, column);

    #endregion

    /// <summary>The model has no injected text of its own between positions, so every position stays as it is.</summary>
    public TextPosition NormalizePosition(TextPosition position, PositionAffinity affinity) => position;

    /// <summary>The column at which indentation stops on a line.</summary>
    public int GetLineIndentColumn(int lineNumber) =>
        // Columns start with 1.
        IndentOfLine(GetLineContent(lineNumber)) + 1;

    internal static int IndentOfLine(string line)
    {
        var indent = 0;
        foreach (var c in line)
        {
            if (c is ' ' or '\t')
            {
                indent++;
            }
            else
            {
                break;
            }
        }

        return indent;
    }

    public override string ToString() => $"TextModel({Uri})";

    private enum StringOffsetValidationType
    {
        /// <summary>Even allowed in surrogate pairs.</summary>
        Relaxed = 0,

        /// <summary>Not allowed in surrogate pairs.</summary>
        SurrogatePairs = 1,
    }

    /// <summary>Reads the buffer's snapshot in chunks of at least 64 KB.</summary>
    private sealed class TextModelSnapshot : ITextSnapshot
    {
        private readonly ITextSnapshot _source;
        private bool _eos;

        public TextModelSnapshot(ITextSnapshot source)
        {
            _source = source;
            _eos = false;
        }

        public string? Read()
        {
            if (_eos)
            {
                return null;
            }

            var result = new List<string>();
            var resultLength = 0;

            while (true)
            {
                var tmp = _source.Read();

                if (tmp is null)
                {
                    // end-of-stream
                    _eos = true;
                    return result.Count == 0 ? null : string.Concat(result);
                }

                if (tmp.Length > 0)
                {
                    result.Add(tmp);
                    resultLength += tmp.Length;
                }

                if (resultLength >= 64 * 1024)
                {
                    return string.Concat(result);
                }
            }
        }
    }
}
