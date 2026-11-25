// Ported from vscode (src/vs/editor/test/common/model/bracketPairColorizer/getBracketPairsInRange.test.ts, commit
// b57d5c9e), MIT license.
// Differences: the model's bracket pairs are model.GetBracketPairs(service), with the service the language's
// configuration is registered in; tokenization supports go into the model's own registry and languages into a codec
// given to the model; the expected objects are tuples.

using Shouldly;
using TKCode.Arc.Editor.Languages;
using TKCode.Arc.Editor.Model;
using TKCode.Arc.Editor.Model.BracketPairs;
using TKCode.Arc.Editor.Tokens;
using Xunit;

namespace TKCode.Arc.Editor.Tests;

public sealed class GetBracketPairsInRangeTests : IDisposable
{
    private readonly LanguageConfigurationService _languageConfigurationService = new();
    private readonly List<IDisposable> _store = new();

    public void Dispose()
    {
        foreach (var d in _store)
        {
            d.Dispose();
        }

        _languageConfigurationService.Dispose();
    }

    private (TextModel Model, BracketPairsTextModelPart BracketPairs) CreateTextModelWithColorizedBracketPairs(string text)
    {
        const string languageId = "testLanguage";
        var codec = new LanguageIdCodec();
        codec.Register(languageId);
        var encodedMode1 = codec.EncodeLanguageId(languageId);
        var document = new TokenizedDocument([new TokenInfo(text, encodedMode1, StandardTokenType.Other, true)]);

        _store.Add(_languageConfigurationService.Register(languageId, new LanguageConfiguration
        {
            Brackets = [("<", ">")],
            ColorizedBracketPairs = [("{", "}"), ("[", "]"), ("(", ")")],
        }));
        var textModel = new TextModel(text, languageId, TextModel.DefaultCreationOptions, TestTextModel.CreateUndoRedoService(), null, codec);
        _store.Add(textModel);
        _store.Add(TestTokenizationSupports.Register(textModel, document.GetTokenizationSupport()));
        return (textModel, textModel.GetBracketPairs(_languageConfigurationService));
    }

    private static (int Level, string Range, string OpenRange, string? CloseRange) BracketPairToJSON(BracketPairInfo pair) =>
        (pair.NestingLevel, pair.OpeningBracketRange.ToString(), pair.OpeningBracketRange.ToString(), pair.ClosingBracketRange?.ToString());

    [Fact]
    public void Basic_1()
    {
        var doc = new AnnotatedDocument("{ ( [] ¹ ) [ ² { } ] () } []");
        var (model, bracketPairs) = CreateTextModelWithColorizedBracketPairs(doc.Text);
        model.Tokenization.GetLineTokens(1).GetLanguageId(0);
        bracketPairs.GetBracketPairsInRange(doc.Range(1, 2)).Map(BracketPairToJSON).ToArray().ShouldBe(
        [
            (0, "[1,1 -> 1,2]", "[1,1 -> 1,2]", "[1,23 -> 1,24]"),
            (1, "[1,3 -> 1,4]", "[1,3 -> 1,4]", "[1,9 -> 1,10]"),
            (1, "[1,11 -> 1,12]", "[1,11 -> 1,12]", "[1,18 -> 1,19]"),
        ]);
    }

    [Fact]
    public void Basic_2()
    {
        var doc = new AnnotatedDocument("{ ( [] ¹ ²) [  { } ] () } []");
        var (_, bracketPairs) = CreateTextModelWithColorizedBracketPairs(doc.Text);
        bracketPairs.GetBracketPairsInRange(doc.Range(1, 2)).Map(BracketPairToJSON).ToArray().ShouldBe(
        [
            (0, "[1,1 -> 1,2]", "[1,1 -> 1,2]", "[1,23 -> 1,24]"),
            (1, "[1,3 -> 1,4]", "[1,3 -> 1,4]", "[1,9 -> 1,10]"),
        ]);
    }

    [Fact]
    public void Basic_Empty()
    {
        var doc = new AnnotatedDocument("¹ ² { ( [] ) [  { } ] () } []");
        var (_, bracketPairs) = CreateTextModelWithColorizedBracketPairs(doc.Text);
        bracketPairs.GetBracketPairsInRange(doc.Range(1, 2)).Map(BracketPairToJSON).ToArray().ShouldBeEmpty();
    }

    [Fact]
    public void Unmatched_closing_brackets_after_position()
    {
        const string languageId = "testLanguage";
        _store.Add(_languageConfigurationService.Register(languageId, new LanguageConfiguration { Brackets = [("{", "}"), ("[", "]")] }));
        var model = TestTextModel.Create("} text }", languageId);
        _store.Add(model);
        var bracketPairs = model.GetBracketPairs(_languageConfigurationService);

        new[]
        {
            bracketPairs.HasUnmatchedClosingBracketAfter(new TextPosition(1, 1), "{"),
            bracketPairs.HasUnmatchedClosingBracketAfter(new TextPosition(1, 2), "{"),
            bracketPairs.HasUnmatchedClosingBracketAfter(new TextPosition(1, 9), "{"),
            bracketPairs.HasUnmatchedClosingBracketAfter(new TextPosition(1, 1), "["),
        }.ShouldBe([true, true, false, false]);
    }

    private sealed class SupportWithOwnBackgroundTokenizer(ITokenizationSupport inner) : ITokenizationSupport, IBackgroundTokenizerFactory
    {
        public IState GetInitialState() => inner.GetInitialState();

        public TokenizationResult Tokenize(string line, bool hasEOL, IState state) => inner.Tokenize(line, hasEOL, state);

        public EncodedTokenizationResult TokenizeEncoded(string line, bool hasEOL, IState state) => inner.TokenizeEncoded(line, hasEOL, state);

        public IBackgroundTokenizer? CreateBackgroundTokenizer(TextModel textModel, IBackgroundTokenizationStore store) => new NoBackgroundTokenizer();

        private sealed class NoBackgroundTokenizer : IBackgroundTokenizer
        {
            public void Dispose()
            {
            }

            public void RequestTokens(int startLineNumber, int endLineNumberExclusive)
            {
            }
        }
    }

    [Fact]
    public void Unmatched_closing_bracket_query_waits_for_background_tokenization()
    {
        const string languageId = "testLanguage";
        _store.Add(_languageConfigurationService.Register(languageId, new LanguageConfiguration { Brackets = [("{", "}")] }));

        var codec = new LanguageIdCodec();
        codec.Register(languageId);
        var encodedLanguageId = codec.EncodeLanguageId(languageId);
        var document = new TokenizedDocument(
        [
            new TokenInfo("\"}\"", encodedLanguageId, StandardTokenType.String, true),
            new TokenInfo("}", encodedLanguageId, StandardTokenType.Other, true),
        ]);
        var model = new TextModel(document.GetText(), languageId, TextModel.DefaultCreationOptions, TestTextModel.CreateUndoRedoService(), null, codec);
        _store.Add(model);
        _store.Add(TestTokenizationSupports.Register(model, new SupportWithOwnBackgroundTokenizer(document.GetTokenizationSupport())));
        var bracketPairs = model.GetBracketPairs(_languageConfigurationService);

        bracketPairs.GetBracketsInRange(model.GetFullModelRange()).ToArray();
        var beforeTokenization = bracketPairs.HasUnmatchedClosingBracketAfter(new TextPosition(1, 1), "{");
        model.Tokenization.ForceTokenization(model.GetLineCount());
        var afterTokenization = bracketPairs.HasUnmatchedClosingBracketAfter(new TextPosition(1, 1), "{");
        model.SetValue("x" + document.GetText());
        new[]
        {
            beforeTokenization,
            afterTokenization,
            bracketPairs.HasUnmatchedClosingBracketAfter(new TextPosition(1, 1), "{"),
        }.ShouldBe([false, true, false]);
    }

    [Fact]
    public void Unmatched_closing_brackets_are_language_scoped()
    {
        const string outerLanguageId = "outerLanguage";
        const string innerLanguageId = "innerLanguage";
        _store.Add(_languageConfigurationService.Register(outerLanguageId, new LanguageConfiguration { Brackets = [("{", "}")] }));
        _store.Add(_languageConfigurationService.Register(innerLanguageId, new LanguageConfiguration { Brackets = [("{", "}")] }));

        var codec = new LanguageIdCodec();
        codec.Register(outerLanguageId);
        codec.Register(innerLanguageId);
        var document = new TokenizedDocument(
        [
            new TokenInfo("x", codec.EncodeLanguageId(outerLanguageId), StandardTokenType.Other, true),
            new TokenInfo("}", codec.EncodeLanguageId(innerLanguageId), StandardTokenType.Other, true),
        ]);
        var model = new TextModel(document.GetText(), outerLanguageId, TextModel.DefaultCreationOptions, TestTextModel.CreateUndoRedoService(), null, codec);
        _store.Add(model);
        _store.Add(TestTokenizationSupports.Register(model, document.GetTokenizationSupport()));
        model.Tokenization.ForceTokenization(model.GetLineCount());
        var bracketPairs = model.GetBracketPairs(_languageConfigurationService);
        bracketPairs.GetBracketsInRange(model.GetFullModelRange()).ToArray();

        new[]
        {
            bracketPairs.HasUnmatchedClosingBracketAfter(new TextPosition(1, 1), "{"),
            bracketPairs.HasUnmatchedClosingBracketAfter(new TextPosition(1, 2), "{"),
        }.ShouldBe([false, true]);
    }

    [Fact]
    public void Basic_All()
    {
        var doc = new AnnotatedDocument("¹ { ( [] ) [  { } ] () } [] ²");
        var (_, bracketPairs) = CreateTextModelWithColorizedBracketPairs(doc.Text);
        bracketPairs.GetBracketPairsInRange(doc.Range(1, 2)).Map(BracketPairToJSON).ToArray().ShouldBe(
        [
            (0, "[1,2 -> 1,3]", "[1,2 -> 1,3]", "[1,23 -> 1,24]"),
            (1, "[1,4 -> 1,5]", "[1,4 -> 1,5]", "[1,9 -> 1,10]"),
            (2, "[1,6 -> 1,7]", "[1,6 -> 1,7]", "[1,7 -> 1,8]"),
            (1, "[1,11 -> 1,12]", "[1,11 -> 1,12]", "[1,18 -> 1,19]"),
            (2, "[1,14 -> 1,15]", "[1,14 -> 1,15]", "[1,16 -> 1,17]"),
            (1, "[1,20 -> 1,21]", "[1,20 -> 1,21]", "[1,21 -> 1,22]"),
            (0, "[1,25 -> 1,26]", "[1,25 -> 1,26]", "[1,26 -> 1,27]"),
        ]);
    }

    [Fact]
    public void GetBracketsInRange()
    {
        var doc = new AnnotatedDocument("¹ { [ ( [ [ (  ) ] ] ) ] } { } ²");
        var (_, bracketPairs) = CreateTextModelWithColorizedBracketPairs(doc.Text);
        bracketPairs.GetBracketsInRange(doc.Range(1, 2))
            .Map(b => (b.NestingLevel, b.NestingLevelOfEqualBracketType, b.Range.ToString()))
            .ToArray().ShouldBe(
        [
            (0, 0, "[1,2 -> 1,3]"),
            (1, 0, "[1,4 -> 1,5]"),
            (2, 0, "[1,6 -> 1,7]"),
            (3, 1, "[1,8 -> 1,9]"),
            (4, 2, "[1,10 -> 1,11]"),
            (5, 1, "[1,12 -> 1,13]"),
            (5, 1, "[1,15 -> 1,16]"),
            (4, 2, "[1,17 -> 1,18]"),
            (3, 1, "[1,19 -> 1,20]"),
            (2, 0, "[1,21 -> 1,22]"),
            (1, 0, "[1,23 -> 1,24]"),
            (0, 0, "[1,25 -> 1,26]"),
            (0, 0, "[1,27 -> 1,28]"),
            (0, 0, "[1,29 -> 1,30]"),
        ]);
    }

    [Fact]
    public void Test_Error_Brackets()
    {
        var doc = new AnnotatedDocument("¹ { () ] ² ");
        var (_, bracketPairs) = CreateTextModelWithColorizedBracketPairs(doc.Text);
        bracketPairs.GetBracketsInRange(doc.Range(1, 2))
            .Map(b => (b.NestingLevel, b.Range.ToString(), b.IsInvalid))
            .ToArray().ShouldBe(
        [
            (0, "[1,2 -> 1,3]", true),
            (1, "[1,4 -> 1,5]", false),
            (1, "[1,5 -> 1,6]", false),
            (0, "[1,7 -> 1,8]", true),
        ]);
    }

    [Fact]
    public void ColorizedBracketsVSBrackets()
    {
        var doc = new AnnotatedDocument("¹ {} [<()>] <{>} ²");
        var (_, bracketPairs) = CreateTextModelWithColorizedBracketPairs(doc.Text);
        bracketPairs.GetBracketsInRange(doc.Range(1, 2), true)
            .Map(b => (b.NestingLevel, b.NestingLevelOfEqualBracketType, b.Range.ToString()))
            .ToArray().ShouldBe(
        [
            (0, 0, "[1,2 -> 1,3]"),
            (0, 0, "[1,3 -> 1,4]"),
            (0, 0, "[1,5 -> 1,6]"),
            (1, 0, "[1,7 -> 1,8]"),
            (1, 0, "[1,8 -> 1,9]"),
            (0, 0, "[1,10 -> 1,11]"),
            (0, 0, "[1,13 -> 1,14]"),
            (-1, 0, "[1,15 -> 1,16]"),
        ]);

        bracketPairs.GetBracketsInRange(doc.Range(1, 2), false)
            .Map(b => (b.NestingLevel, b.NestingLevelOfEqualBracketType, b.Range.ToString()))
            .ToArray().ShouldBe(
        [
            (0, 0, "[1,2 -> 1,3]"),
            (0, 0, "[1,3 -> 1,4]"),
            (0, 0, "[1,5 -> 1,6]"),
            (1, 0, "[1,6 -> 1,7]"),
            (2, 0, "[1,7 -> 1,8]"),
            (2, 0, "[1,8 -> 1,9]"),
            (1, 0, "[1,9 -> 1,10]"),
            (0, 0, "[1,10 -> 1,11]"),
            (0, 0, "[1,12 -> 1,13]"),
            (1, 0, "[1,13 -> 1,14]"),
            (0, 0, "[1,14 -> 1,15]"),
            (-1, 0, "[1,15 -> 1,16]"),
        ]);
    }

    private sealed class AnnotatedDocument
    {
        private readonly Dictionary<int, TextPosition> _positions = new();

        public AnnotatedDocument(string src)
        {
            const string numbers = "⁰¹²³⁴⁵⁶⁷⁸⁹";

            var text = new System.Text.StringBuilder();
            var offsetPositions = new Dictionary<int, int>();

            var offset = 0;
            foreach (var ch in src)
            {
                var idx = numbers.IndexOf(ch);
                if (idx >= 0)
                {
                    offsetPositions[idx] = offset;
                }
                else
                {
                    text.Append(ch);
                    offset++;
                }
            }

            Text = text.ToString();

            var lineStarts = new List<int> { 0 };
            for (var i = 0; i < Text.Length; i++)
            {
                if (Text[i] == '\n')
                {
                    lineStarts.Add(i + 1);
                }
            }

            foreach (var (idx, o) in offsetPositions)
            {
                // upstream's getPosition: findIndex of the first line start <= offset
                var lineIndex = lineStarts.FindIndex(lineStart => lineStart <= o);
                _positions[idx] = new TextPosition(lineIndex + 1, o - lineStarts[lineIndex] + 1);
            }
        }

        public string Text { get; }

        public TextRange Range(int start, int end)
        {
            var s = _positions[start];
            var e = _positions[end];
            return new TextRange(s.LineNumber, s.Column, e.LineNumber, e.Column);
        }
    }
}
