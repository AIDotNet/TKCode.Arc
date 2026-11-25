// Ported from vscode (src/vs/editor/common/model/textModelTokens.ts, commit b57d5c9e), MIT license.
//
// Differences from upstream:
//  - the states are IState throughout (upstream is generic in TState extends IState, and always uses IState);
//  - the text model is the TextModel (upstream's ITextModel);
//  - DefaultBackgroundTokenizer runs through the model's IdleScheduler (Core/IdleScheduler.cs) where upstream calls
//    runWhenGlobalIdle, setTimeout0, Date.now and StopWatch; like upstream it runs on the model's owning thread;
//  - getFirstInvalidEndStateLineNumberOrMax answers int.MaxValue where upstream answers Number.MAX_SAFE_INTEGER;
//  - safeTokenize reports a tokenizer's exception to UnexpectedErrors (upstream's onUnexpectedError) and falls back to
//    the null tokenizer, as upstream; a tokenizer that returns null tokens gets the null tokenizer's as well (upstream
//    would fail in convertToEndOffset).

using TKCode.Arc.Editor.Languages;
using TKCode.Arc.Editor.Tokens;

namespace TKCode.Arc.Editor.Model;

/// <summary>A tokenizer with the end state of each line.</summary>
internal class TokenizerWithStateStore
{
    private readonly IState _initialState;

    public TokenizerWithStateStore(int lineCount, ITokenizationSupport tokenizationSupport)
    {
        TokenizationSupport = tokenizationSupport;
        _initialState = TokenizationSupport.GetInitialState();
        Store = new TrackingTokenizationStateStore(lineCount);
    }

    public ITokenizationSupport TokenizationSupport { get; }

    public TrackingTokenizationStateStore Store { get; }

    public IState? GetStartState(int lineNumber) => Store.GetStartState(lineNumber, _initialState);

    public (int LineNumber, IState StartState)? GetFirstInvalidLine() => Store.GetFirstInvalidLine(_initialState);
}

/// <summary>A tokenizer with the end state of each line, for the lines of a text model.</summary>
internal sealed class TokenizerWithStateStoreAndTextModel : TokenizerWithStateStore
{
    private const int CheapTokenizationLengthLimit = 2048;

    public TokenizerWithStateStoreAndTextModel(
        int lineCount,
        ITokenizationSupport tokenizationSupport,
        TextModel textModel,
        ILanguageIdCodec languageIdCodec)
        : base(lineCount, tokenizationSupport)
    {
        TextModel = textModel;
        LanguageIdCodec = languageIdCodec;
    }

    public TextModel TextModel { get; }

    public ILanguageIdCodec LanguageIdCodec { get; }

    /// <summary>Tokenizes the invalid lines up to <paramref name="lineNumber"/>, adding their tokens to the builder.</summary>
    public void UpdateTokensUntilLine(ContiguousMultilineTokensBuilder builder, int lineNumber)
    {
        var languageId = TextModel.GetLanguageId();

        while (true)
        {
            var lineToTokenize = GetFirstInvalidLine();
            if (lineToTokenize is not { } line || line.LineNumber > lineNumber)
            {
                break;
            }

            var text = TextModel.GetLineContent(line.LineNumber);

            var r = SafeTokenize(LanguageIdCodec, languageId, TokenizationSupport, text, true, line.StartState);
            builder.Add(line.LineNumber, r.Tokens);
            Store.SetEndState(line.LineNumber, r.EndState);
        }
    }

    /// <summary>Assumes state is up to date.</summary>
    public StandardTokenType GetTokenTypeIfInsertingCharacter(TextPosition position, string character)
    {
        // TODO@hediet: use tokenizeLineWithEdit
        var lineStartState = GetStartState(position.LineNumber);
        if (lineStartState is null)
        {
            return StandardTokenType.Other;
        }

        var languageId = TextModel.GetLanguageId();
        var lineContent = TextModel.GetLineContent(position.LineNumber);

        // Create the text as if `character` was inserted
        var text = lineContent.Substring(0, position.Column - 1) + character + lineContent.Substring(position.Column - 1);

        var r = SafeTokenize(LanguageIdCodec, languageId, TokenizationSupport, text, true, lineStartState);
        var lineTokens = new LineTokens(r.Tokens, text, LanguageIdCodec);
        if (lineTokens.GetCount() == 0)
        {
            return StandardTokenType.Other;
        }

        var tokenIndex = lineTokens.FindTokenIndexAtOffset(position.Column - 1);
        return lineTokens.GetStandardTokenType(tokenIndex);
    }

    /// <summary>Assumes state is up to date.</summary>
    public IReadOnlyList<LineTokens>? TokenizeLinesAt(int lineNumber, IReadOnlyList<string> lines)
    {
        var lineStartState = GetStartState(lineNumber);
        if (lineStartState is null)
        {
            return null;
        }

        var languageId = TextModel.GetLanguageId();
        var result = new List<LineTokens>();

        var state = lineStartState;
        foreach (var line in lines)
        {
            var r = SafeTokenize(LanguageIdCodec, languageId, TokenizationSupport, line, true, state);
            result.Add(new LineTokens(r.Tokens, line, LanguageIdCodec));
            state = r.EndState;
        }

        return result;
    }

    public bool HasAccurateTokensForLine(int lineNumber)
    {
        var firstInvalidLineNumber = Store.GetFirstInvalidEndStateLineNumberOrMax();
        return lineNumber < firstInvalidLineNumber;
    }

    public bool IsCheapToTokenize(int lineNumber)
    {
        var firstInvalidLineNumber = Store.GetFirstInvalidEndStateLineNumberOrMax();
        if (lineNumber < firstInvalidLineNumber)
        {
            return true;
        }

        if (lineNumber == firstInvalidLineNumber && TextModel.GetLineLength(lineNumber) < CheapTokenizationLengthLimit)
        {
            return true;
        }

        return false;
    }

    /// <summary>The result is not cached.</summary>
    public bool TokenizeHeuristically(ContiguousMultilineTokensBuilder builder, int startLineNumber, int endLineNumber)
    {
        if (endLineNumber <= Store.GetFirstInvalidEndStateLineNumberOrMax())
        {
            // nothing to do
            return false;
        }

        if (startLineNumber <= Store.GetFirstInvalidEndStateLineNumberOrMax())
        {
            // tokenization has reached the viewport start...
            UpdateTokensUntilLine(builder, endLineNumber);
            return false;
        }

        var state = GuessStartState(startLineNumber);
        var languageId = TextModel.GetLanguageId();

        for (var lineNumber = startLineNumber; lineNumber <= endLineNumber; lineNumber++)
        {
            var text = TextModel.GetLineContent(lineNumber);
            var r = SafeTokenize(LanguageIdCodec, languageId, TokenizationSupport, text, true, state);
            builder.Add(lineNumber, r.Tokens);
            state = r.EndState;
        }

        return true;
    }

    private IState GuessStartState(int lineNumber)
    {
        var (likelyRelevantLines, initialState) = FindLikelyRelevantLines(TextModel, lineNumber, this);

        initialState ??= TokenizationSupport.GetInitialState();

        var languageId = TextModel.GetLanguageId();
        var state = initialState;
        foreach (var line in likelyRelevantLines)
        {
            var r = SafeTokenize(LanguageIdCodec, languageId, TokenizationSupport, line, false, state);
            state = r.EndState;
        }

        return state;
    }

    /// <summary>The lines above whose indentation is smaller (the likely enclosing blocks), and a state to start them in.</summary>
    public static (List<string> LikelyRelevantLines, IState? InitialState) FindLikelyRelevantLines(
        TextModel model,
        int lineNumber,
        TokenizerWithStateStore? store = null)
    {
        var nonWhitespaceColumn = model.GetLineFirstNonWhitespaceColumn(lineNumber);
        var likelyRelevantLines = new List<string>();
        IState? initialState = null;
        for (var i = lineNumber - 1; nonWhitespaceColumn > 1 && i >= 1; i--)
        {
            var newNonWhitespaceIndex = model.GetLineFirstNonWhitespaceColumn(i);
            // Ignore lines full of whitespace
            if (newNonWhitespaceIndex == 0)
            {
                continue;
            }

            if (newNonWhitespaceIndex < nonWhitespaceColumn)
            {
                likelyRelevantLines.Add(model.GetLineContent(i));
                nonWhitespaceColumn = newNonWhitespaceIndex;
                initialState = store?.GetStartState(i);
                if (initialState is not null)
                {
                    break;
                }
            }
        }

        likelyRelevantLines.Reverse();
        return (likelyRelevantLines, initialState);
    }

    internal static EncodedTokenizationResult SafeTokenize(
        ILanguageIdCodec languageIdCodec,
        string languageId,
        ITokenizationSupport? tokenizationSupport,
        string text,
        bool hasEOL,
        IState state)
    {
        EncodedTokenizationResult? r = null;

        if (tokenizationSupport is not null)
        {
            try
            {
                r = tokenizationSupport.TokenizeEncoded(text, hasEOL, state.Clone());
            }
            catch (Exception e)
            {
                UnexpectedErrors.Report(e);
            }
        }

        if (r?.Tokens is null)
        {
            r = NullTokenize.TokenizeEncoded(languageIdCodec.EncodeLanguageId(languageId), state);
        }

        LineTokens.ConvertToEndOffset(r.Tokens, text.Length);
        return r;
    }
}

/// <summary>
/// The end states of the lines and which of them are invalid. Invariant: if the text model is retokenized from line 1 to
/// <see cref="GetFirstInvalidEndStateLineNumber"/>() - 1, then the recomputed end state for line l will be equal to
/// <see cref="GetEndState"/>(l).
/// </summary>
internal sealed class TrackingTokenizationStateStore
{
    private readonly TokenizationStateStore _tokenizationStateStore = new();
    private readonly RangePriorityQueueImpl _invalidEndStatesLineNumbers = new();
    private int _lineCount;

    public TrackingTokenizationStateStore(int lineCount)
    {
        _lineCount = lineCount;
        _invalidEndStatesLineNumbers.AddRange(new OffsetRange(1, lineCount + 1));
    }

    public IState? GetEndState(int lineNumber) => _tokenizationStateStore.GetEndState(lineNumber);

    /// <returns>If the end state has changed.</returns>
    public bool SetEndState(int lineNumber, IState state)
    {
        ArgumentNullException.ThrowIfNull(state);

        _invalidEndStatesLineNumbers.Delete(lineNumber);
        var r = _tokenizationStateStore.SetEndState(lineNumber, state);
        if (r && lineNumber < _lineCount)
        {
            // because the state changed, we cannot trust the next state anymore and have to invalidate it.
            _invalidEndStatesLineNumbers.AddRange(new OffsetRange(lineNumber + 1, lineNumber + 2));
        }

        return r;
    }

    public void AcceptChange(LineRange range, int newLineCount)
    {
        _lineCount += newLineCount - range.Length;
        _tokenizationStateStore.AcceptChange(range, newLineCount);
        _invalidEndStatesLineNumbers.AddRangeAndResize(new OffsetRange(range.StartLineNumber, range.EndLineNumberExclusive), newLineCount);
    }

    public void AcceptChanges(IReadOnlyList<ModelContentChange> changes)
    {
        foreach (var c in changes)
        {
            var eolCount = EolCounter.CountEOL(c.Text).Count;
            AcceptChange(new LineRange(c.Range.StartLineNumber, c.Range.EndLineNumber + 1), eolCount + 1);
        }
    }

    public void InvalidateEndStateRange(LineRange range) =>
        _invalidEndStatesLineNumbers.AddRange(new OffsetRange(range.StartLineNumber, range.EndLineNumberExclusive));

    public int? GetFirstInvalidEndStateLineNumber() => _invalidEndStatesLineNumbers.Min;

    public int GetFirstInvalidEndStateLineNumberOrMax()
    {
        // Upstream's `|| Number.MAX_SAFE_INTEGER` also turns a 0 into the maximum.
        var lineNumber = GetFirstInvalidEndStateLineNumber();
        return lineNumber is null or 0 ? int.MaxValue : lineNumber.Value;
    }

    public bool AllStatesValid() => _invalidEndStatesLineNumbers.Min is null;

    public IState? GetStartState(int lineNumber, IState initialState)
    {
        if (lineNumber == 1)
        {
            return initialState;
        }

        return GetEndState(lineNumber - 1);
    }

    public (int LineNumber, IState StartState)? GetFirstInvalidLine(IState initialState)
    {
        var lineNumber = GetFirstInvalidEndStateLineNumber();
        if (lineNumber is not { } invalidLineNumber)
        {
            return null;
        }

        var startState = GetStartState(invalidLineNumber, initialState)
            ?? throw new InvalidOperationException("Start state must be defined");

        return (invalidLineNumber, startState);
    }
}

/// <summary>The end state of each line.</summary>
internal sealed class TokenizationStateStore
{
    private readonly FixedArray<IState?> _lineEndStates = new(null);

    public IState? GetEndState(int lineNumber) => _lineEndStates.Get(lineNumber);

    public bool SetEndState(int lineNumber, IState state)
    {
        var oldState = _lineEndStates.Get(lineNumber);
        if (oldState is not null && oldState.Equals(state))
        {
            return false;
        }

        _lineEndStates.Set(lineNumber, state);
        return true;
    }

    public void AcceptChange(LineRange range, int newLineCount)
    {
        var length = range.Length;
        if (newLineCount > 0 && length > 0)
        {
            // Keep the last state, even though it is unrelated.
            // But if the new state happens to agree with this last state, then we know we can stop tokenizing.
            length--;
            newLineCount--;
        }

        _lineEndStates.Replace(range.StartLineNumber, length, newLineCount);
    }

    public void AcceptChanges(IReadOnlyList<ModelContentChange> changes)
    {
        foreach (var c in changes)
        {
            var eolCount = EolCounter.CountEOL(c.Text).Count;
            AcceptChange(new LineRange(c.Range.StartLineNumber, c.Range.EndLineNumber + 1), eolCount + 1);
        }
    }
}

/// <summary>A set of numbers kept as sorted disjoint ranges, which hands out its smallest member first.</summary>
internal sealed class RangePriorityQueueImpl
{
    private readonly List<OffsetRange> _ranges = new();

    public IReadOnlyList<OffsetRange> GetRanges() => _ranges;

    public int? Min => _ranges.Count == 0 ? null : _ranges[0].Start;

    public int? RemoveMin()
    {
        if (_ranges.Count == 0)
        {
            return null;
        }

        var range = _ranges[0];
        if (range.Start + 1 == range.EndExclusive)
        {
            _ranges.RemoveAt(0);
        }
        else
        {
            _ranges[0] = new OffsetRange(range.Start + 1, range.EndExclusive);
        }

        return range.Start;
    }

    public void Delete(int value)
    {
        var idx = _ranges.FindIndex(r => r.Contains(value));
        if (idx != -1)
        {
            var range = _ranges[idx];
            if (range.Start == value)
            {
                if (range.EndExclusive == value + 1)
                {
                    _ranges.RemoveAt(idx);
                }
                else
                {
                    _ranges[idx] = new OffsetRange(value + 1, range.EndExclusive);
                }
            }
            else
            {
                if (range.EndExclusive == value + 1)
                {
                    _ranges[idx] = new OffsetRange(range.Start, value);
                }
                else
                {
                    _ranges[idx] = new OffsetRange(range.Start, value);
                    _ranges.Insert(idx + 1, new OffsetRange(value + 1, range.EndExclusive));
                }
            }
        }
    }

    public void AddRange(OffsetRange range) => OffsetRange.AddRange(range, _ranges);

    public void AddRangeAndResize(OffsetRange range, int newLength)
    {
        var idxFirstMightBeIntersecting = 0;
        while (!(idxFirstMightBeIntersecting >= _ranges.Count || range.Start <= _ranges[idxFirstMightBeIntersecting].EndExclusive))
        {
            idxFirstMightBeIntersecting++;
        }

        var idxFirstIsAfter = idxFirstMightBeIntersecting;
        while (!(idxFirstIsAfter >= _ranges.Count || range.EndExclusive < _ranges[idxFirstIsAfter].Start))
        {
            idxFirstIsAfter++;
        }

        var delta = newLength - range.Length;

        for (var i = idxFirstIsAfter; i < _ranges.Count; i++)
        {
            _ranges[i] = _ranges[i].Delta(delta);
        }

        if (idxFirstMightBeIntersecting == idxFirstIsAfter)
        {
            var newRange = new OffsetRange(range.Start, range.Start + newLength);
            if (!newRange.IsEmpty)
            {
                _ranges.Insert(idxFirstMightBeIntersecting, newRange);
            }
        }
        else
        {
            var start = Math.Min(range.Start, _ranges[idxFirstMightBeIntersecting].Start);
            var endEx = Math.Max(range.EndExclusive, _ranges[idxFirstIsAfter - 1].EndExclusive);

            var newRange = new OffsetRange(start, endEx + delta);
            _ranges.RemoveRange(idxFirstMightBeIntersecting, idxFirstIsAfter - idxFirstMightBeIntersecting);
            if (!newRange.IsEmpty)
            {
                _ranges.Insert(idxFirstMightBeIntersecting, newRange);
            }
        }
    }

    public override string ToString() => string.Join(" + ", _ranges.Select(r => r.ToString()));
}

/// <summary>
/// Tokenizes a model's invalid lines in idle time slices on the model's owning thread (upstream's
/// DefaultBackgroundTokenizer), while the model is attached to an editor.
/// </summary>
internal sealed class DefaultBackgroundTokenizer : IBackgroundTokenizer
{
    private readonly TokenizerWithStateStoreAndTextModel _tokenizerWithStateStore;
    private readonly IBackgroundTokenizationStore _backgroundTokenStore;
    private readonly IdleScheduler _scheduler;
    private bool _isDisposed;
    private bool _isScheduled;
    private IDisposable? _pending;

    public DefaultBackgroundTokenizer(
        TokenizerWithStateStoreAndTextModel tokenizerWithStateStore,
        IBackgroundTokenizationStore backgroundTokenStore,
        IdleScheduler scheduler)
    {
        _tokenizerWithStateStore = tokenizerWithStateStore;
        _backgroundTokenStore = backgroundTokenStore;
        _scheduler = scheduler;
    }

    public void Dispose()
    {
        _isDisposed = true;
        // Upstream lets a pending idle callback run and return early; cancelling it releases the model sooner.
        _pending?.Dispose();
        _pending = null;
    }

    public void HandleChanges() => BeginBackgroundTokenization();

    private void BeginBackgroundTokenization()
    {
        if (_isScheduled || !_tokenizerWithStateStore.TextModel.IsAttachedToEditor() || !HasLinesToTokenize())
        {
            return;
        }

        _isScheduled = true;
        _pending = _scheduler.RunWhenIdle(deadline =>
        {
            _isScheduled = false;
            _pending = null;

            BackgroundTokenizeWithDeadline(deadline);
        });
    }

    /// <summary>Tokenize until the deadline occurs, but try to yield every 1-2ms.</summary>
    private void BackgroundTokenizeWithDeadline(IdleDeadline deadline)
    {
        // Read the time remaining from the `deadline` immediately because it is unclear
        // if the `deadline` object will be valid after execution leaves this function.
        var endTime = _scheduler.Now() + deadline.TimeRemaining();

        void Execute()
        {
            _pending = null;
            if (_isDisposed || !_tokenizerWithStateStore.TextModel.IsAttachedToEditor() || !HasLinesToTokenize())
            {
                // disposed in the meantime or detached or finished
                return;
            }

            BackgroundTokenizeForAtLeast1ms();

            if (_scheduler.Now() < endTime)
            {
                // There is still time before reaching the deadline, so yield to the browser and then
                // continue execution
                _pending = _scheduler.SetTimeout0(Execute);
            }
            else
            {
                // The deadline has been reached, so schedule a new idle callback if necessary
                BeginBackgroundTokenization();
            }
        }

        Execute();
    }

    /// <summary>Tokenize for at least 1ms.</summary>
    private void BackgroundTokenizeForAtLeast1ms()
    {
        var lineCount = _tokenizerWithStateStore.TextModel.GetLineCount();
        var builder = new ContiguousMultilineTokensBuilder();
        var sw = new IdleStopWatch(_scheduler);

        do
        {
            if (sw.Elapsed() > 1)
            {
                // the comparison is intentionally > 1 and not >= 1 to ensure that
                // a full millisecond has elapsed, given how microseconds are rounded
                // to milliseconds
                break;
            }

            var tokenizedLineNumber = TokenizeOneInvalidLine(builder);

            if (tokenizedLineNumber >= lineCount)
            {
                break;
            }
        }
        while (HasLinesToTokenize());

        _backgroundTokenStore.SetTokens(builder.FinalizeTokens());
        CheckFinished();
    }

    private bool HasLinesToTokenize() => !_tokenizerWithStateStore.Store.AllStatesValid();

    private int TokenizeOneInvalidLine(ContiguousMultilineTokensBuilder builder)
    {
        var firstInvalidLine = _tokenizerWithStateStore.GetFirstInvalidLine();
        if (firstInvalidLine is not { } line)
        {
            return _tokenizerWithStateStore.TextModel.GetLineCount() + 1;
        }

        _tokenizerWithStateStore.UpdateTokensUntilLine(builder, line.LineNumber);
        return line.LineNumber;
    }

    public void CheckFinished()
    {
        if (_isDisposed)
        {
            return;
        }

        if (_tokenizerWithStateStore.Store.AllStatesValid())
        {
            _backgroundTokenStore.BackgroundTokenizationFinished();
        }
    }

    public void RequestTokens(int startLineNumber, int endLineNumberExclusive) =>
        _tokenizerWithStateStore.Store.InvalidateEndStateRange(new LineRange(startLineNumber, endLineNumberExclusive));
}
