// Ported from vscode (src/vs/editor/common/model/tokens/tokenizationTextModelPart.ts, commit b57d5c9e), MIT license.
//
// Differences from upstream:
//  - one backend, the TokenizerSyntaxTokenBackend: tree-sitter is not ported, so upstream's derived `tokens` observable is
//    a field that is recreated when the language changes (upstream: _languageIdObs feeding the derived backend, followed by
//    todo_resetTokenization);
//  - the language id stays in the TextModel (its SetLanguage fires DidChangeLanguage, as it did before this part existed)
//    and calls HandleLanguageChanged here; onDidChangeLanguageConfiguration stays the language configuration service's
//    event, which the view model listens to;
//  - the tokenizers come from a TokenizationRegistry instance and deferred work runs on an IdleScheduler, both from the
//    model's creator;
//  - the bracket pairs part is the model's (TextModel.GetBracketPairs), told of token changes through the model; font
//    tokens are not ported;
//  - getWordAtPosition / getWordUntilPosition stay where they were (the cursor's word operations);
//  - the language service's requestRichLanguageFeatures, which upstream calls when a model gets its language, is done
//    here: a pending lazy tokenizer of the language is created.

using TKCode.Arc.Editor.Languages;
using TKCode.Arc.Editor.Tokens;
using TKCode.Arc.Editor.ViewModel;

namespace TKCode.Arc.Editor.Model;

/// <summary>The tokens of a model's lines (syntax and semantic) and the language at a position.</summary>
internal sealed class TokenizationTextModelPart : IDisposable
{
    private readonly TextModel _textModel;
    private readonly SparseTokensStore _semanticTokens;
    private readonly Emitter<ModelTokensChangedEvent> _onDidChangeTokens = new();
    private readonly Emitter<bool> _onDidChangeBackgroundTokenizationState = new();
    private readonly AttachedViews _attachedViews = new();
    private TokenizerSyntaxTokenBackend _tokens;

    internal TokenizationTextModelPart(
        TextModel textModel,
        ILanguageIdCodec languageIdCodec,
        TokenizationRegistry<ITokenizationSupport> tokenizationRegistry,
        IdleScheduler scheduler)
    {
        _textModel = textModel;
        LanguageIdCodec = languageIdCodec;
        TokenizationRegistry = tokenizationRegistry;
        Scheduler = scheduler;
        _semanticTokens = new SparseTokensStore(languageIdCodec);
        _tokens = CreateBackend();
    }

    public ILanguageIdCodec LanguageIdCodec { get; }

    /// <summary>Where the model finds its language's tokenizer (and those of embedded languages).</summary>
    public TokenizationRegistry<ITokenizationSupport> TokenizationRegistry { get; }

    /// <summary>Runs the background tokenization and the visible lines' debounce on the model's owning thread.</summary>
    public IdleScheduler Scheduler { get; }

    /// <summary>The tokens of some lines changed (syntax or semantic).</summary>
    public event Action<ModelTokensChangedEvent> DidChangeTokens
    {
        add => _onDidChangeTokens.Add(value);
        remove => _onDidChangeTokens.Remove(value);
    }

    /// <summary>The background tokenization went through the whole text (bracket pair colorization waits for it).</summary>
    public event Action DidChangeBackgroundTokenizationState
    {
        add => _onDidChangeBackgroundTokenizationState.Add(_ => value(), value);
        remove => _onDidChangeBackgroundTokenizationState.Remove(value);
    }

    internal bool HasListeners => _onDidChangeTokens.HasListeners;

    private TokenizerSyntaxTokenBackend CreateBackend()
    {
        var tokens = new TokenizerSyntaxTokenBackend(
            LanguageIdCodec, _textModel, _textModel.GetLanguageId, _attachedViews, TokenizationRegistry, Scheduler);
        tokens.DidChangeTokens += EmitModelTokensChangedEvent;
        tokens.DidChangeBackgroundTokenizationState += HandleBackgroundTokenizationStateChanged;
        RequestRichLanguageFeatures(_textModel.GetLanguageId());
        return tokens;
    }

    // upstream's languageService.requestRichLanguageFeatures (on model creation and language change): a lazy tokenizer of
    // the language is created now; its registration fires the registry's change event, and the backend starts again.
    private void RequestRichLanguageFeatures(string languageId)
    {
        if (TokenizationRegistry.Get(languageId) is null && !TokenizationRegistry.IsResolved(languageId))
        {
            _ = TokenizationRegistry.GetOrCreateAsync(languageId);
        }
    }

    private void HandleBackgroundTokenizationStateChanged()
    {
        _textModel.HandleDidChangeBackgroundTokenizationStateForBracketPairs();
        _onDidChangeBackgroundTokenizationState.Fire(true);
    }

    /// <summary>A view starts showing the model: it reports its visible lines through the result.</summary>
    internal IAttachedView AttachView() => _attachedViews.AttachView();

    internal void DetachView(IAttachedView view) => _attachedViews.DetachView(view);

    internal void HandleDidChangeContent(ModelContentChangedEvent e)
    {
        if (e.IsFlush)
        {
            _semanticTokens.Flush();
        }
        else if (!e.IsEolChange)
        {
            // We don't have to do anything on an EOL change
            foreach (var c in e.Changes)
            {
                var eol = EolCounter.CountEOL(c.Text);
                _semanticTokens.AcceptEdit(
                    c.Range,
                    eol.Count,
                    eol.FirstLineLength,
                    eol.LastLineLength,
                    c.Text.Length > 0 ? c.Text[0] : 0);
            }
        }

        _tokens.HandleDidChangeContent(e);
    }

    internal void HandleDidChangeAttached() => _tokens.HandleDidChangeAttached();

    /// <summary>The model's language changed: a new backend for the new language (upstream's derived tokens).</summary>
    internal void HandleLanguageChanged()
    {
        _tokens.DidChangeTokens -= EmitModelTokensChangedEvent;
        _tokens.DidChangeBackgroundTokenizationState -= HandleBackgroundTokenizationStateChanged;
        _tokens.Dispose();
        _tokens = CreateBackend();
        // the new token provider otherwise won't have a chance to provide tokens until some action happens in the editor
        _tokens.ResetTokenization();
    }

    /// <summary>Includes grammar and semantic tokens.</summary>
    public LineTokens GetLineTokens(int lineNumber)
    {
        ValidateLineNumber(lineNumber);
        var syntacticTokens = _tokens.GetLineTokens(lineNumber);
        return _semanticTokens.AddSparseTokens(lineNumber, syntacticTokens);
    }

    private void EmitModelTokensChangedEvent(ModelTokensChangedEvent e)
    {
        if (!_textModel.IsDisposing)
        {
            _textModel.HandleDidChangeTokensForBracketPairs(e);
            _onDidChangeTokens.Fire(e);
        }
    }

    private void ValidateLineNumber(int lineNumber)
    {
        if (lineNumber < 1 || lineNumber > _textModel.GetLineCount())
        {
            throw new ArgumentOutOfRangeException(nameof(lineNumber), "Illegal value for lineNumber");
        }
    }

    // ---- grammar tokens

    public bool HasTokens => _tokens.HasTokens;

    public void ResetTokenization() => _tokens.ResetTokenization();

    public BackgroundTokenizationState BackgroundTokenizationState => _tokens.BackgroundTokenizationState;

    public void ForceTokenization(int lineNumber)
    {
        ValidateLineNumber(lineNumber);
        _tokens.ForceTokenization(lineNumber);
    }

    public bool HasAccurateTokensForLine(int lineNumber)
    {
        ValidateLineNumber(lineNumber);
        return _tokens.HasAccurateTokensForLine(lineNumber);
    }

    public bool IsCheapToTokenize(int lineNumber)
    {
        ValidateLineNumber(lineNumber);
        return _tokens.IsCheapToTokenize(lineNumber);
    }

    public void TokenizeIfCheap(int lineNumber)
    {
        ValidateLineNumber(lineNumber);
        _tokens.TokenizeIfCheap(lineNumber);
    }

    /// <summary>The type the token at a position would have if <paramref name="character"/> were inserted there.</summary>
    public StandardTokenType GetTokenTypeIfInsertingCharacter(int lineNumber, int column, string character) =>
        _tokens.GetTokenTypeIfInsertingCharacter(lineNumber, column, character);

    public IReadOnlyList<LineTokens>? TokenizeLinesAt(int lineNumber, IReadOnlyList<string> lines) =>
        _tokens.TokenizeLinesAt(lineNumber, lines);

    // ---- semantic tokens

    public void SetSemanticTokens(IReadOnlyList<SparseMultilineTokens>? tokens, bool isComplete)
    {
        _semanticTokens.Set(tokens, isComplete);
        EmitModelTokensChangedEvent(new ModelTokensChangedEvent(
            tokens is not null,
            [new ModelTokensChangedRange(1, _textModel.GetLineCount())]));
    }

    public bool HasCompleteSemanticTokens() => _semanticTokens.IsComplete();

    public bool HasSomeSemanticTokens() => !_semanticTokens.IsEmpty();

    public void SetPartialSemanticTokens(TextRange range, IReadOnlyList<SparseMultilineTokens> tokens)
    {
        if (HasCompleteSemanticTokens())
        {
            return;
        }

        var changedRange = _textModel.ValidateRange(_semanticTokens.SetPartial(range, tokens));
        EmitModelTokensChangedEvent(new ModelTokensChangedEvent(
            true,
            [new ModelTokensChangedRange(changedRange.StartLineNumber, changedRange.EndLineNumber)]));
    }

    // ---- language

    public string GetLanguageId() => _textModel.GetLanguageId();

    /// <summary>The language of the token at a position (validated first, as upstream does).</summary>
    public string GetLanguageIdAtPosition(int lineNumber, int column)
    {
        var position = _textModel.ValidatePosition(new TextPosition(lineNumber, column));
        var lineTokens = GetLineTokens(position.LineNumber);
        return lineTokens.GetLanguageId(lineTokens.FindTokenIndexAtOffset(position.Column - 1));
    }

    public void Dispose()
    {
        _tokens.DidChangeTokens -= EmitModelTokensChangedEvent;
        _tokens.DidChangeBackgroundTokenizationState -= HandleBackgroundTokenizationStateChanged;
        _tokens.Dispose();
        _attachedViews.Dispose();
        _onDidChangeTokens.Clear();
        _onDidChangeBackgroundTokenizationState.Clear();
    }
}
