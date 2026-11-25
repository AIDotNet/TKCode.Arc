// Ported from vscode (src/vs/editor/common/model/tokens/tokenizerSyntaxTokenBackend.ts, commit b57d5c9e), MIT license.
//
// Differences from upstream:
//  - the tokenizers come from the model's TokenizationRegistry instance, not the global TokenizationRegistry;
//  - the default background tokenizer and the attached views' debounce run on the model's IdleScheduler;
//  - createBackgroundTokenizer is IBackgroundTokenizerFactory, which a support may implement;
//  - the debugging aid that verifies a support's own background tokenizer against the default one
//    (backgroundTokenizerShouldOnlyVerifyTokens, _debugBackgroundTokens, reportMismatchingTokens) is left out, and so are
//    font tokens (setFontInfo);
//  - a support that throws from getInitialState is reported to UnexpectedErrors, as upstream's onUnexpectedError.

using TKCode.Arc.Editor.Languages;
using TKCode.Arc.Editor.Tokens;
using TKCode.Arc.Editor.ViewModel;

namespace TKCode.Arc.Editor.Model;

/// <summary>The syntax tokens of a model from a line tokenizer (upstream: "For TextMate"; here Monarch).</summary>
internal sealed class TokenizerSyntaxTokenBackend : AbstractSyntaxTokenBackend, IBackgroundTokenizationStore
{
    private readonly Func<string> _getLanguageId;
    private readonly TokenizationRegistry<ITokenizationSupport> _registry;
    private readonly IdleScheduler _scheduler;
    private readonly AttachedViews _attachedViews;
    private readonly ContiguousTokensStore _tokens;
    private readonly Dictionary<IAttachedView, AttachedViewHandler> _attachedViewStates = new();
    private TokenizerWithStateStoreAndTextModel? _tokenizer;
    private DefaultBackgroundTokenizer? _defaultBackgroundTokenizer;
    private IBackgroundTokenizer? _backgroundTokenizer;
    private bool _isDisposed;

    public TokenizerSyntaxTokenBackend(
        ILanguageIdCodec languageIdCodec,
        TextModel textModel,
        Func<string> getLanguageId,
        AttachedViews attachedViews,
        TokenizationRegistry<ITokenizationSupport> registry,
        IdleScheduler scheduler)
        : base(languageIdCodec, textModel)
    {
        _getLanguageId = getLanguageId;
        _registry = registry;
        _scheduler = scheduler;
        _attachedViews = attachedViews;
        _tokens = new ContiguousTokensStore(languageIdCodec);

        _registry.OnDidChange += HandleRegistryChange;

        ResetTokenization();

        _attachedViews.DidChangeVisibleRanges += HandleVisibleRangesChange;
    }

    public override bool HasTokens => _tokens.HasTokens;

    private void HandleRegistryChange(TokenizationSupportChangedEvent e)
    {
        var languageId = _getLanguageId();
        if (!e.ChangedLanguages.Contains(languageId))
        {
            return;
        }

        ResetTokenization();
    }

    private void HandleVisibleRangesChange((IAttachedView View, AttachedViewState? State) change)
    {
        if (change.State is { } state)
        {
            if (!_attachedViewStates.TryGetValue(change.View, out var existing))
            {
                AttachedViewHandler? handler = null;
                handler = new AttachedViewHandler(() => RefreshRanges(handler!.LineRanges), _scheduler);
                existing = handler;
                _attachedViewStates[change.View] = existing;
            }

            existing.HandleStateChange(state);
        }
        else if (_attachedViewStates.Remove(change.View, out var removed))
        {
            removed.Dispose();
        }
    }

    /// <summary>Upstream's todo_resetTokenization: drops every token and starts again with the language's tokenizer.</summary>
    public override void ResetTokenization(bool fireTokenChangeEvent = true)
    {
        _tokens.Flush();
        if (fireTokenChangeEvent)
        {
            OnDidChangeTokensEmitter.Fire(new ModelTokensChangedEvent(
                false,
                [new ModelTokensChangedRange(1, TextModel.GetLineCount())]));
        }

        var (tokenizationSupport, initialState) = InitializeTokenization();
        _tokenizer = tokenizationSupport is not null && initialState is not null
            ? new TokenizerWithStateStoreAndTextModel(TextModel.GetLineCount(), tokenizationSupport, TextModel, LanguageIdCodec)
            : null;

        _backgroundTokenizer?.Dispose();
        _backgroundTokenizer = null;
        _defaultBackgroundTokenizer = null;
        if (_tokenizer is not null)
        {
            if (tokenizationSupport is IBackgroundTokenizerFactory factory)
            {
                _backgroundTokenizer = factory.CreateBackgroundTokenizer(TextModel, this);
            }

            if (_backgroundTokenizer is null && !TextModel.IsTooLargeForTokenization())
            {
                _backgroundTokenizer = _defaultBackgroundTokenizer = new DefaultBackgroundTokenizer(_tokenizer, this, _scheduler);
                _defaultBackgroundTokenizer.HandleChanges();
            }
        }

        RefreshAllVisibleLineTokens();
    }

    private (ITokenizationSupport? Support, IState? InitialState) InitializeTokenization()
    {
        if (TextModel.IsTooLargeForTokenization())
        {
            return (null, null);
        }

        var tokenizationSupport = _registry.Get(_getLanguageId());
        if (tokenizationSupport is null)
        {
            return (null, null);
        }

        try
        {
            return (tokenizationSupport, tokenizationSupport.GetInitialState());
        }
        catch (Exception e)
        {
            UnexpectedErrors.Report(e);
            return (null, null);
        }
    }

    // ---- IBackgroundTokenizationStore

    void IBackgroundTokenizationStore.SetTokens(IReadOnlyList<ContiguousMultilineTokens> tokens) => SetTokens(tokens);

    void IBackgroundTokenizationStore.BackgroundTokenizationFinished()
    {
        if (BackgroundTokenizationState == BackgroundTokenizationState.Completed)
        {
            // We already did a full tokenization and don't go back to progressing.
            return;
        }

        BackgroundTokenizationState = BackgroundTokenizationState.Completed;
        OnDidChangeBackgroundTokenizationStateEmitter.Fire(true);
    }

    void IBackgroundTokenizationStore.SetEndState(int lineNumber, IState state)
    {
        if (_tokenizer is null)
        {
            return;
        }

        var firstInvalidEndStateLineNumber = _tokenizer.Store.GetFirstInvalidEndStateLineNumber();
        // Don't accept states for definitely valid states, the renderer is ahead of the worker!
        if (firstInvalidEndStateLineNumber is { } first && lineNumber >= first)
        {
            _tokenizer.Store.SetEndState(lineNumber, state);
        }
    }

    // ----

    public override void HandleDidChangeAttached() => _defaultBackgroundTokenizer?.HandleChanges();

    public override void HandleDidChangeContent(ModelContentChangedEvent e)
    {
        if (e.IsFlush)
        {
            // Don't fire the event, as the view might not have got the text change event yet
            ResetTokenization(false);
        }
        else if (!e.IsEolChange)
        {
            // We don't have to do anything on an EOL change
            foreach (var c in e.Changes)
            {
                var eol = EolCounter.CountEOL(c.Text);
                _tokens.AcceptEdit(c.Range, eol.Count, eol.FirstLineLength);
            }

            _tokenizer?.Store.AcceptChanges(e.Changes);
            _defaultBackgroundTokenizer?.HandleChanges();
        }
    }

    private List<ModelTokensChangedRange> SetTokens(IReadOnlyList<ContiguousMultilineTokens> tokens)
    {
        var changes = _tokens.SetMultilineTokens(tokens, TextModel);
        if (changes.Count > 0)
        {
            OnDidChangeTokensEmitter.Fire(new ModelTokensChangedEvent(false, changes));
        }

        return changes;
    }

    private void RefreshAllVisibleLineTokens()
    {
        var ranges = LineRange.JoinMany(_attachedViewStates.Values.Select(s => s.LineRanges));
        RefreshRanges(ranges);
    }

    private void RefreshRanges(IReadOnlyList<LineRange> ranges)
    {
        foreach (var range in ranges)
        {
            RefreshRange(range.StartLineNumber, range.EndLineNumberExclusive - 1);
        }
    }

    private void RefreshRange(int startLineNumber, int endLineNumber)
    {
        if (_tokenizer is null || _isDisposed)
        {
            return;
        }

        startLineNumber = Math.Max(1, Math.Min(TextModel.GetLineCount(), startLineNumber));
        endLineNumber = Math.Min(TextModel.GetLineCount(), endLineNumber);

        var builder = new ContiguousMultilineTokensBuilder();
        var heuristicTokens = _tokenizer.TokenizeHeuristically(builder, startLineNumber, endLineNumber);
        var changedTokens = SetTokens(builder.FinalizeTokens());

        if (heuristicTokens)
        {
            // We overrode tokens with heuristically computed ones.
            // Because old states might get reused (thus stopping invalidation),
            // we have to explicitly request the tokens for the changed ranges again.
            foreach (var c in changedTokens)
            {
                _backgroundTokenizer?.RequestTokens(c.FromLineNumber, c.ToLineNumber + 1);
            }
        }

        _defaultBackgroundTokenizer?.CheckFinished();
    }

    public override void ForceTokenization(int lineNumber)
    {
        var builder = new ContiguousMultilineTokensBuilder();
        _tokenizer?.UpdateTokensUntilLine(builder, lineNumber);
        SetTokens(builder.FinalizeTokens());
        _defaultBackgroundTokenizer?.CheckFinished();
    }

    public override bool HasAccurateTokensForLine(int lineNumber) =>
        _tokenizer is null || _tokenizer.HasAccurateTokensForLine(lineNumber);

    public override bool IsCheapToTokenize(int lineNumber) =>
        _tokenizer is null || _tokenizer.IsCheapToTokenize(lineNumber);

    public override LineTokens GetLineTokens(int lineNumber)
    {
        var lineText = TextModel.GetLineContent(lineNumber);
        return _tokens.GetTokens(TextModel.GetLanguageId(), lineNumber - 1, lineText);
    }

    public override StandardTokenType GetTokenTypeIfInsertingCharacter(int lineNumber, int column, string character)
    {
        if (_tokenizer is null)
        {
            return StandardTokenType.Other;
        }

        var position = TextModel.ValidatePosition(new TextPosition(lineNumber, column));
        ForceTokenization(position.LineNumber);
        return _tokenizer.GetTokenTypeIfInsertingCharacter(position, character);
    }

    public override IReadOnlyList<LineTokens>? TokenizeLinesAt(int lineNumber, IReadOnlyList<string> lines)
    {
        if (_tokenizer is null)
        {
            return null;
        }

        ForceTokenization(lineNumber);
        return _tokenizer.TokenizeLinesAt(lineNumber, lines);
    }

    public override void Dispose()
    {
        _isDisposed = true;
        _registry.OnDidChange -= HandleRegistryChange;
        _attachedViews.DidChangeVisibleRanges -= HandleVisibleRangesChange;
        _backgroundTokenizer?.Dispose();
        _backgroundTokenizer = null;
        _defaultBackgroundTokenizer = null;
        foreach (var handler in _attachedViewStates.Values)
        {
            handler.Dispose();
        }

        _attachedViewStates.Clear();
        base.Dispose();
    }
}
