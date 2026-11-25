// Test helpers for tokenization (upstream's tests register ad hoc ITokenizationSupport objects with the global
// TokenizationRegistry; here they go into the model's own registry, which the model reads on registration).

using TKCode.Arc.Editor.Languages;
using TKCode.Arc.Editor.Model;

namespace TKCode.Arc.Editor.Tests;

/// <summary>Tokenization supports for tests.</summary>
internal static class TestTokenizationSupports
{
    /// <summary>
    /// Registers, for the model's language, a support that tokenizes each line by its line number: the state is the number
    /// of the next line, so line n gets <paramref name="tokenize"/>(n, text). The tokens are pairs of start offset and
    /// metadata, as EncodedTokenizationResult's.
    /// </summary>
    public static IDisposable RegisterByLine(TextModel model, Func<int, string, uint[]> tokenize) =>
        model.Tokenization.TokenizationRegistry.Register(model.GetLanguageId(), new ByLineSupport(tokenize));

    /// <summary>Registers, for the model's language, a support given by its functions.</summary>
    public static IDisposable Register(TextModel model, ITokenizationSupport support) =>
        model.Tokenization.TokenizationRegistry.Register(model.GetLanguageId(), support);

    private sealed class ByLineSupport(Func<int, string, uint[]> tokenize) : ITokenizationSupport
    {
        public IState GetInitialState() => new LineState(1);

        public TokenizationResult Tokenize(string line, bool hasEOL, IState state) => throw new NotSupportedException();

        public EncodedTokenizationResult TokenizeEncoded(string line, bool hasEOL, IState state)
        {
            var lineNumber = ((LineState)state).LineNumber;
            return new EncodedTokenizationResult(tokenize(lineNumber, line), new LineState(lineNumber + 1));
        }
    }

    private sealed class LineState(int lineNumber) : IState
    {
        public int LineNumber { get; } = lineNumber;

        public IState Clone() => this;

        public bool Equals(IState other) => other is LineState l && l.LineNumber == LineNumber;
    }
}

/// <summary>A support whose state is any record: equal records are equal states (as upstream's test states compare).</summary>
internal sealed class RecordStateTokenizationSupport<TState>(TState initialState, Func<string, TState, (uint[] Tokens, TState EndState)> tokenize) : ITokenizationSupport
    where TState : class
{
    public IState GetInitialState() => new Wrapped(initialState);

    public TokenizationResult Tokenize(string line, bool hasEOL, IState state) => throw new NotSupportedException();

    public EncodedTokenizationResult TokenizeEncoded(string line, bool hasEOL, IState state)
    {
        var (tokens, endState) = tokenize(line, ((Wrapped)state).Value);
        return new EncodedTokenizationResult(tokens, new Wrapped(endState));
    }

    private sealed class Wrapped(TState value) : IState
    {
        public TState Value { get; } = value;

        public IState Clone() => this;

        public bool Equals(IState other) => other is Wrapped w && EqualityComparer<TState>.Default.Equals(w.Value, Value);
    }
}
