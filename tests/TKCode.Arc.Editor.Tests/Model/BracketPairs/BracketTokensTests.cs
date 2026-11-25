// Ported from vscode (src/vs/editor/test/common/model/bracketPairColorizer/brackets.test.ts and tokenizer.test.ts,
// commit b57d5c9e), MIT license.
// Differences: the tokenization support goes into the model's own registry and the language into a codec given to the
// model (upstream: the global registry and the language service).

using Shouldly;
using TKCode.Arc.Editor.Languages;
using TKCode.Arc.Editor.Model;
using TKCode.Arc.Editor.Model.BracketPairs;
using TKCode.Arc.Editor.Tokens;
using Xunit;
using static TKCode.Arc.Editor.Model.BracketPairs.LengthHelpers;
using Token = TKCode.Arc.Editor.Model.BracketPairs.Token;

namespace TKCode.Arc.Editor.Tests;

public sealed class BracketPairColorizerBracketsTests
{
    [Fact]
    public void Basic()
    {
        const string languageId = "testMode1";
        var denseKeyProvider = new DenseKeyProvider<string>();
        SmallImmutableSet<string> GetImmutableSet(string[] elements)
        {
            var newSet = SmallImmutableSet<string>.GetEmpty();
            foreach (var x in elements)
            {
                newSet = newSet.Add($"{languageId}:::{x}", denseKeyProvider);
            }

            return newSet;
        }

        int GetKey(string value) => denseKeyProvider.GetKey($"{languageId}:::{value}");

        using var languageConfigService = new LanguageConfigurationService();
        using var registration = languageConfigService.Register(languageId, new LanguageConfiguration
        {
            Brackets =
            [
                ("{", "}"), ("[", "]"), ("(", ")"),
                ("begin", "end"), ("case", "endcase"), ("casez", "endcase"), // Verilog
                ("\\left(", "\\right)"), ("\\left(", "\\right."), ("\\left.", "\\right)"), // LaTeX Parentheses
                ("\\left[", "\\right]"), ("\\left[", "\\right."), ("\\left.", "\\right]"), // LaTeX Brackets
            ],
        });

        var brackets = new LanguageAgnosticBracketTokens(denseKeyProvider, languageConfigService.GetLanguageConfiguration);
        (string Text, int Length, TokenKind Kind, int BracketId, SmallImmutableSet<string> BracketIds)[] bracketsExpected =
        [
            ("{", 1, TokenKind.OpeningBracket, GetKey("{"), GetImmutableSet(["{"])),
            ("[", 1, TokenKind.OpeningBracket, GetKey("["), GetImmutableSet(["["])),
            ("(", 1, TokenKind.OpeningBracket, GetKey("("), GetImmutableSet(["("])),
            ("begin", 5, TokenKind.OpeningBracket, GetKey("begin"), GetImmutableSet(["begin"])),
            ("case", 4, TokenKind.OpeningBracket, GetKey("case"), GetImmutableSet(["case"])),
            ("casez", 5, TokenKind.OpeningBracket, GetKey("casez"), GetImmutableSet(["casez"])),
            ("\\left(", 6, TokenKind.OpeningBracket, GetKey("\\left("), GetImmutableSet(["\\left("])),
            ("\\left.", 6, TokenKind.OpeningBracket, GetKey("\\left."), GetImmutableSet(["\\left."])),
            ("\\left[", 6, TokenKind.OpeningBracket, GetKey("\\left["), GetImmutableSet(["\\left["])),

            ("}", 1, TokenKind.ClosingBracket, GetKey("{"), GetImmutableSet(["{"])),
            ("]", 1, TokenKind.ClosingBracket, GetKey("["), GetImmutableSet(["["])),
            (")", 1, TokenKind.ClosingBracket, GetKey("("), GetImmutableSet(["("])),
            ("end", 3, TokenKind.ClosingBracket, GetKey("begin"), GetImmutableSet(["begin"])),
            ("endcase", 7, TokenKind.ClosingBracket, GetKey("case"), GetImmutableSet(["case", "casez"])),
            ("\\right)", 7, TokenKind.ClosingBracket, GetKey("\\left("), GetImmutableSet(["\\left(", "\\left."])),
            ("\\right.", 7, TokenKind.ClosingBracket, GetKey("\\left("), GetImmutableSet(["\\left(", "\\left["])),
            ("\\right]", 7, TokenKind.ClosingBracket, GetKey("\\left["), GetImmutableSet(["\\left[", "\\left."])),
        ];

        foreach (var expected in bracketsExpected)
        {
            var token = brackets.GetToken(expected.Text, languageId).ShouldNotBeNull(expected.Text);
            token.Length.ShouldBe(ToLength(0, expected.Length), expected.Text);
            token.Kind.ShouldBe(expected.Kind, expected.Text);
            token.BracketId.ShouldBe(expected.BracketId, expected.Text);
            // the sets hold dense keys: compare them through the key provider, as upstream's deepStrictEqual compares the bits
            denseKeyProvider.ReverseLookupSet(ToStringSet(token.BracketIds))
                .ShouldBe(denseKeyProvider.ReverseLookupSet(expected.BracketIds), expected.Text);
        }

        SmallImmutableSet<string> ToStringSet(SmallImmutableSet<int> ids)
        {
            var set = SmallImmutableSet<string>.GetEmpty();
            foreach (var key in denseKeyProvider.Keys())
            {
                if (ids.Has(denseKeyProvider.GetKey(key), IdentityKeyProvider.Instance))
                {
                    set = set.Add(key, denseKeyProvider);
                }
            }

            return set;
        }
    }
}

public sealed class BracketPairColorizerTokenizerTests
{
    [Fact]
    public void Basic()
    {
        const string mode1 = "testMode1";
        var codec = new LanguageIdCodec();
        codec.Register(mode1);
        var encodedMode1 = codec.EncodeLanguageId(mode1);

        var denseKeyProvider = new DenseKeyProvider<string>();

        TokenInfo TStandard(string text) => new(text, encodedMode1, StandardTokenType.Other, true);
        TokenInfo TComment(string text) => new(text, encodedMode1, StandardTokenType.Comment, true);
        var document = new TokenizedDocument(
        [
            TStandard(" { } "), TStandard("be"), TStandard("gin end"), TStandard("\n"),
            TStandard("hello"), TComment("{"), TStandard("}"),
        ]);

        using var languageConfigurationService = new LanguageConfigurationService();
        using var configRegistration = languageConfigurationService.Register(mode1, new LanguageConfiguration
        {
            Brackets = [("{", "}"), ("[", "]"), ("(", ")"), ("begin", "end")],
        });

        using var model = new TextModel(document.GetText(), mode1, TextModel.DefaultCreationOptions, TestTextModel.CreateUndoRedoService(), null, codec);
        using var registration = TestTokenizationSupports.Register(model, document.GetTokenizationSupport());
        model.Tokenization.ForceTokenization(model.GetLineCount());

        var brackets = new LanguageAgnosticBracketTokens(denseKeyProvider, languageConfigurationService.GetLanguageConfiguration);

        var tokens = ReadAllTokens(new TextBufferTokenizer(new TextModelTokenizerSource(model), brackets));

        ToArr(tokens, model, denseKeyProvider).ShouldBe(
        [
            (" ", null, "", TokenKind.Text),
            ("{", "testMode1:::{", "testMode1:::{", TokenKind.OpeningBracket),
            (" ", null, "", TokenKind.Text),
            ("}", "testMode1:::{", "testMode1:::{", TokenKind.ClosingBracket),
            (" ", null, "", TokenKind.Text),
            ("begin", "testMode1:::begin", "testMode1:::begin", TokenKind.OpeningBracket),
            (" ", null, "", TokenKind.Text),
            ("end", "testMode1:::begin", "testMode1:::begin", TokenKind.ClosingBracket),
            ("\nhello{", null, "", TokenKind.Text),
            ("}", "testMode1:::{", "testMode1:::{", TokenKind.ClosingBracket),
        ]);
    }

    private static List<Token> ReadAllTokens(ITokenizer tokenizer)
    {
        var tokens = new List<Token>();
        while (tokenizer.Read() is { } token)
        {
            tokens.Add(token);
        }

        return tokens;
    }

    private static List<(string Text, string? BracketId, string BracketIds, TokenKind Kind)> ToArr(List<Token> tokens, TextModel model, DenseKeyProvider<string> keyProvider)
    {
        var result = new List<(string, string?, string, TokenKind)>();
        var offset = Length.Zero;
        foreach (var token in tokens)
        {
            var end = LengthAdd(offset, token.Length);
            var ids = new List<string>();
            foreach (var key in keyProvider.Keys())
            {
                if (token.BracketIds.Has(keyProvider.GetKey(key), IdentityKeyProvider.Instance))
                {
                    ids.Add(key);
                }
            }

            result.Add((
                model.GetValueInRange(LengthsToRange(offset, end)),
                token.BracketId >= 0 ? keyProvider.ReverseLookup(token.BracketId) : null,
                string.Join(",", ids),
                token.Kind));
            offset = end;
        }

        return result;
    }
}

/// <summary>Upstream's TokenizedDocument: a text given as tokens, and a tokenization support that returns them.</summary>
internal sealed class TokenizedDocument
{
    private readonly List<List<TokenInfo>> _tokensByLine;

    public TokenizedDocument(TokenInfo[] tokens)
    {
        var tokensByLine = new List<List<TokenInfo>>();
        var curLine = new List<TokenInfo>();

        foreach (var token in tokens)
        {
            var lines = token.Text.Split('\n').ToList();
            var first = true;
            while (lines.Count > 0)
            {
                if (!first)
                {
                    tokensByLine.Add(curLine);
                    curLine = new List<TokenInfo>();
                }
                else
                {
                    first = false;
                }

                if (lines[0].Length > 0)
                {
                    curLine.Add(token.WithText(lines[0]));
                }

                lines.RemoveAt(lines.Count - 1);
            }
        }

        tokensByLine.Add(curLine);

        _tokensByLine = tokensByLine;
    }

    public string GetText() => string.Join('\n', _tokensByLine.Select(t => string.Concat(t.Select(i => i.Text))));

    public ITokenizationSupport GetTokenizationSupport() => new Support(this);

    private sealed class State(int lineNumber) : IState
    {
        public int LineNumber { get; } = lineNumber;

        public IState Clone() => new State(LineNumber);

        public bool Equals(IState other) => LineNumber == ((State)other).LineNumber;
    }

    private sealed class Support(TokenizedDocument document) : ITokenizationSupport
    {
        public IState GetInitialState() => new State(0);

        public TokenizationResult Tokenize(string line, bool hasEOL, IState state) => throw new NotSupportedException("Method not implemented.");

        public EncodedTokenizationResult TokenizeEncoded(string line, bool hasEOL, IState state)
        {
            var state2 = (State)state;
            var tokens = document._tokensByLine[state2.LineNumber];
            var arr = new List<uint>();
            var offset = 0;
            foreach (var t in tokens)
            {
                arr.Add((uint)offset);
                arr.Add(t.GetMetadata());
                offset += t.Text.Length;
            }

            return new EncodedTokenizationResult([.. arr], new State(state2.LineNumber + 1));
        }
    }
}

internal sealed record TokenInfo(string Text, LanguageId LanguageId, StandardTokenType TokenType, bool HasBalancedBrackets)
{
    public uint GetMetadata() =>
        ((uint)LanguageId << MetadataConsts.LanguageIdOffset)
        | ((uint)TokenType << MetadataConsts.TokenTypeOffset)
        | (HasBalancedBrackets ? MetadataConsts.BalancedBracketsMask : 0);

    public TokenInfo WithText(string text) => this with { Text = text };
}
