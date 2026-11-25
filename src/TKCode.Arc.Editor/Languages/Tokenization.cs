// Ported from vscode (src/vs/editor/common/languages.ts: Token, TokenizationResult, EncodedTokenizationResult,
// ITokenizationSupport, IState, ITokenizationSupportChangedEvent, ILazyTokenizationSupport; languages/nullTokenize.ts,
// commit b57d5c9e), MIT license.
//
// Differences:
// - The background tokenizer hooks of ITokenizationSupport (createBackgroundTokenizer, IBackgroundTokenizer,
//   IBackgroundTokenizationStore) belong to the model's tokens store and are declared with it.
// - EncodedTokenizationResult has no font tokens (IFontToken): upstream fills them only from tree-sitter and its
//   experimental font decorations.
// - Lazy supports resolve through a Task instead of a Promise.

using TKCode.Arc.Editor.Tokens;

namespace TKCode.Arc.Editor.Languages;

/// <summary>
/// The state of the tokenizer between two lines. It is useful to store flags such as in multiline comment, etc. The model
/// will clone the previous line's state and pass it in to tokenize the next line.
/// </summary>
internal interface IState
{
    IState Clone();

    bool Equals(IState other);
}

/// <summary>A token: where it starts in the line, its type (such as <c>keyword.cs</c>) and its language.</summary>
internal sealed record Token(int Offset, string Type, string Language)
{
    /// <inheritdoc />
    public override string ToString() => "(" + Offset + ", " + Type + ")";
}

/// <summary>The tokens of a line as text and the state at its end.</summary>
internal sealed class TokenizationResult(IReadOnlyList<Token> tokens, IState endState)
{
    public IReadOnlyList<Token> Tokens { get; } = tokens;

    public IState EndState { get; } = endState;
}

/// <summary>
/// The tokens of a line in binary format and the state at its end. Each token occupies two array indices; for token i,
/// at 2*i its start index and at 2*i + 1 its metadata (<see cref="MetadataConsts"/>).
/// </summary>
internal sealed class EncodedTokenizationResult(uint[] tokens, IState endState)
{
    public uint[] Tokens { get; } = tokens;

    public IState EndState { get; } = endState;
}

/// <summary>A tokenizer for one language (upstream's ITokenizationSupport).</summary>
internal interface ITokenizationSupport
{
    IState GetInitialState();

    TokenizationResult Tokenize(string line, bool hasEOL, IState state);

    EncodedTokenizationResult TokenizeEncoded(string line, bool hasEOL, IState state);
}

/// <summary>What a change of the registry touched: some languages' tokenizers, and/or the color map.</summary>
internal sealed record TokenizationSupportChangedEvent(IReadOnlyList<string> ChangedLanguages, bool ChangedColorMap);

/// <summary>A tokenizer that is created when first asked for (upstream's ILazyTokenizationSupport).</summary>
internal interface ILazyTokenizationSupport<TSupport>
    where TSupport : class
{
    Task<TSupport?> GetTokenizationSupportAsync();
}

/// <summary>Creates its tokenizer once, on first request (upstream's LazyTokenizationSupport).</summary>
internal sealed class LazyTokenizationSupport<TSupport>(Func<Task<TSupport?>> createSupport) : ILazyTokenizationSupport<TSupport>, IDisposable
    where TSupport : class
{
    private Task<TSupport?>? _tokenizationSupport;

    public Task<TSupport?> GetTokenizationSupportAsync() => _tokenizationSupport ??= createSupport();

    public void Dispose()
    {
        if (_tokenizationSupport is { IsCompletedSuccessfully: true, Result: IDisposable disposable })
        {
            disposable.Dispose();
        }
        else
        {
            _tokenizationSupport?.ContinueWith(
                static t => (t.Result as IDisposable)?.Dispose(),
                CancellationToken.None,
                TaskContinuationOptions.OnlyOnRanToCompletion,
                TaskScheduler.Default);
        }
    }
}

/// <summary>The state of a language without a tokenizer: one shared instance, equal only to itself.</summary>
internal sealed class NullState : IState
{
    public static readonly NullState Instance = new();

    private NullState()
    {
    }

    public IState Clone() => this;

    public bool Equals(IState other) => ReferenceEquals(this, other);
}

/// <summary>The tokens of a language without a tokenizer: one token over the whole line (upstream's nullTokenize.ts).</summary>
internal static class NullTokenize
{
    public static TokenizationResult Tokenize(string languageId, IState state) =>
        new([new Token(0, string.Empty, languageId)], state);

    public static EncodedTokenizationResult TokenizeEncoded(LanguageId languageId, IState? state)
    {
        var tokens = new uint[2];
        tokens[0] = 0;
        tokens[1] = ((uint)languageId << MetadataConsts.LanguageIdOffset)
            | ((uint)StandardTokenType.Other << MetadataConsts.TokenTypeOffset)
            | ((uint)FontStyle.None << MetadataConsts.FontStyleOffset)
            | ((uint)ColorId.DefaultForeground << MetadataConsts.ForegroundOffset)
            | ((uint)ColorId.DefaultBackground << MetadataConsts.BackgroundOffset);
        return new EncodedTokenizationResult(tokens, state ?? NullState.Instance);
    }
}
