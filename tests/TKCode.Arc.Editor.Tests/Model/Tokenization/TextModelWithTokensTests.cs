// Ported from vscode (src/vs/editor/test/common/model/textModelWithTokens.test.ts, commit b57d5c9e), MIT license.
// This file: the suites 'TextModelWithTokens', 'TextModelWithTokens - bracket matching', 'TextModelWithTokens 2' and
// 'TextModelWithTokens regression tests'. The suite 'TextModel.getLineIndentGuide' is in Model/GuidesTextModelPartTests.cs.
// Differences: the tokenization supports go into the model's own registry; the languages are registered with the
// model's codec (upstream: the language service); model.bracketPairs is model.GetBracketPairs(service); a found
// bracket's info is compared by its text and kind (upstream compares the objects deeply).

using Shouldly;
using TKCode.Arc.Editor.Languages;
using TKCode.Arc.Editor.Model;
using TKCode.Arc.Editor.Model.BracketPairs;
using TKCode.Arc.Editor.Tokens;
using Xunit;

namespace TKCode.Arc.Editor.Tests;

public sealed class TextModelWithTokensRegressionTests
{
    private sealed class Support(Func<uint[]> tokens) : ITokenizationSupport
    {
        public IState GetInitialState() => NullState.Instance;

        public TokenizationResult Tokenize(string line, bool hasEOL, IState state) => throw new NotSupportedException();

        public EncodedTokenizationResult TokenizeEncoded(string line, bool hasEOL, IState state) =>
            new(tokens(), state);
    }

    [Fact]
    public void Monaco_editor_122_Unhandled_Exception_TypeError_Unable_to_get_property_replace_of_undefined_or_null_reference()
    {
        static void AssertViewLineTokens(TextModel model, int lineNumber, bool forceTokenization, (int EndIndex, int Foreground)[] expected)
        {
            if (forceTokenization)
            {
                model.Tokenization.ForceTokenization(lineNumber);
            }

            var actual = model.Tokenization.GetLineTokens(lineNumber).Inflate();
            var actualTokens = new List<(int EndIndex, int Foreground)>();
            for (int i = 0, len = actual.GetCount(); i < len; i++)
            {
                actualTokens.Add((actual.GetEndOffset(i), (int)actual.GetForeground(i)));
            }

            actualTokens.ShouldBe(expected);
        }

        var tokenId = 10u;
        const string langId1 = "indicisiveMode1";
        const string langId2 = "indicisiveMode2";

        var tokenizationSupport = new Support(() => [0, ++tokenId << MetadataConsts.ForegroundOffset]);

        using var model = TestTextModel.Create("A model with\ntwo lines");
        using var registration1 = model.Tokenization.TokenizationRegistry.Register(langId1, tokenizationSupport);
        using var registration2 = model.Tokenization.TokenizationRegistry.Register(langId2, tokenizationSupport);

        AssertViewLineTokens(model, 1, true, [(12, 1)]);
        AssertViewLineTokens(model, 2, true, [(9, 1)]);

        model.SetLanguage(langId1);

        AssertViewLineTokens(model, 1, true, [(12, 11)]);
        AssertViewLineTokens(model, 2, true, [(9, 12)]);

        model.SetLanguage(langId2);

        AssertViewLineTokens(model, 1, false, [(12, 1)]);
        AssertViewLineTokens(model, 2, false, [(9, 1)]);
    }

    [Fact]
    public void Issue_63822_Wrong_embedded_language_detected_for_empty_lines()
    {
        const string outerMode = "outerMode";
        const string innerMode = "innerMode";

        var codec = new LanguageIdCodec();
        codec.Register(outerMode);
        codec.Register(innerMode);
        var encodedInnerMode = (uint)codec.EncodeLanguageId(innerMode);

        var tokenizationSupport = new Support(() => [0, encodedInnerMode << MetadataConsts.LanguageIdOffset]);

        using var model = new TextModel(
            "A model with one line", outerMode, TextModel.DefaultCreationOptions, TestTextModel.CreateUndoRedoService(), null, codec);
        using var registration = model.Tokenization.TokenizationRegistry.Register(outerMode, tokenizationSupport);

        model.Tokenization.ForceTokenization(1);
        model.GetLanguageIdAtPosition(1, 1).ShouldBe(innerMode);
    }
}

internal static class BracketTestHelpers
{
    public static void AssertIsNotBracket(BracketPairsTextModelPart bracketPairs, int lineNumber, int column)
    {
        var match = bracketPairs.MatchBracket(new TextPosition(lineNumber, column));
        match.ShouldBeNull($"is not matching brackets at {lineNumber}, {column}");
    }

    public static void AssertIsBracket(BracketPairsTextModelPart bracketPairs, TextPosition testPosition, TextRange expected1, TextRange expected2)
    {
        var expected = new[] { expected1, expected2 }.OrderBy(r => r, Comparer<TextRange>.Create(TextRange.CompareRangesUsingStarts)).ToArray();
        var actual = bracketPairs.MatchBracket(testPosition);
        actual.ShouldNotBeNull($"matches brackets at {testPosition}");
        new[] { actual.Value.Open, actual.Value.Close }.OrderBy(r => r, Comparer<TextRange>.Create(TextRange.CompareRangesUsingStarts)).ToArray()
            .ShouldBe(expected, $"matches brackets at {testPosition}");
    }

    public static (TextModel Model, BracketPairsTextModelPart BracketPairs, LanguageConfigurationService Service) CreateTextModelWithBrackets(
        string text, (string Open, string Close)[] brackets, string languageId = "bracketMode2")
    {
        var service = new LanguageConfigurationService();
        service.Register(languageId, new LanguageConfiguration { Brackets = brackets.Select(b => (CharacterPair)b).ToList() });
        var model = TestTextModel.Create(text, languageId);
        return (model, model.GetBracketPairs(service), service);
    }
}

public sealed class TextModelWithTokensBracketsTests
{
    private static void TestBrackets(string[] contents, (string Open, string Close)[] brackets)
    {
        const string languageId = "testMode";
        var (model, bracketPairs, service) = BracketTestHelpers.CreateTextModelWithBrackets(string.Join('\n', contents), brackets, languageId);
        using var _ = model;
        using var __ = service;

        static (string Range, string Text, bool IsOpening)? ToRelaxedFoundBracket(FoundBracket? a) =>
            a is null ? null : (a.Range.ToString(), a.BracketInfo.BracketText, a.BracketInfo.IsOpeningBracket);

        var charIsBracket = new HashSet<char>();
        foreach (var (open, close) in brackets)
        {
            charIsBracket.Add(open[0]);
            charIsBracket.Add(close[0]);
        }

        var expectedBrackets = new List<FoundBracket>();
        for (var lineIndex = 0; lineIndex < contents.Length; lineIndex++)
        {
            var lineText = contents[lineIndex];
            for (var charIndex = 0; charIndex < lineText.Length; charIndex++)
            {
                var ch = lineText[charIndex];
                if (charIsBracket.Contains(ch))
                {
                    expectedBrackets.Add(new FoundBracket(
                        new TextRange(lineIndex + 1, charIndex + 1, lineIndex + 1, charIndex + 2),
                        service.GetLanguageConfiguration(languageId).BracketsNew.GetBracketInfo(ch.ToString())!));
                }
            }
        }

        // findPrevBracket
        {
            var expectedBracketIndex = expectedBrackets.Count - 1;
            var currentExpectedBracket = expectedBracketIndex >= 0 ? expectedBrackets[expectedBracketIndex] : null;
            for (var lineNumber = contents.Length; lineNumber >= 1; lineNumber--)
            {
                var lineText = contents[lineNumber - 1];

                for (var column = lineText.Length + 1; column >= 1; column--)
                {
                    if (currentExpectedBracket is not null
                        && lineNumber == currentExpectedBracket.Range.StartLineNumber
                        && column < currentExpectedBracket.Range.EndColumn)
                    {
                        expectedBracketIndex--;
                        currentExpectedBracket = expectedBracketIndex >= 0 ? expectedBrackets[expectedBracketIndex] : null;
                    }

                    var actual = bracketPairs.FindPrevBracket(new TextPosition(lineNumber, column));
                    ToRelaxedFoundBracket(actual).ShouldBe(ToRelaxedFoundBracket(currentExpectedBracket), $"findPrevBracket of {lineNumber}, {column}");
                }
            }
        }

        // findNextBracket
        {
            var expectedBracketIndex = 0;
            var currentExpectedBracket = expectedBracketIndex < expectedBrackets.Count ? expectedBrackets[expectedBracketIndex] : null;
            for (var lineNumber = 1; lineNumber <= contents.Length; lineNumber++)
            {
                var lineText = contents[lineNumber - 1];

                for (var column = 1; column <= lineText.Length + 1; column++)
                {
                    if (currentExpectedBracket is not null
                        && lineNumber == currentExpectedBracket.Range.StartLineNumber
                        && column > currentExpectedBracket.Range.StartColumn)
                    {
                        expectedBracketIndex++;
                        currentExpectedBracket = expectedBracketIndex < expectedBrackets.Count ? expectedBrackets[expectedBracketIndex] : null;
                    }

                    var actual = bracketPairs.FindNextBracket(new TextPosition(lineNumber, column));
                    ToRelaxedFoundBracket(actual).ShouldBe(ToRelaxedFoundBracket(currentExpectedBracket), $"findNextBracket of {lineNumber}, {column}");
                }
            }
        }
    }

    [Fact]
    public void Brackets1() =>
        TestBrackets(["if (a == 3) { return (7 * (a + 5)); }"], [("{", "}"), ("[", "]"), ("(", ")")]);
}

public sealed class TextModelWithTokensBracketMatchingTests
{
    private static (TextModel Model, BracketPairsTextModelPart BracketPairs, LanguageConfigurationService Service) Create(string text) =>
        BracketTestHelpers.CreateTextModelWithBrackets(text, [("{", "}"), ("[", "]"), ("(", ")")], "bracketMode1");

    [Fact]
    public void Bracket_matching_1()
    {
        var (model, bp, service) = Create(")]}{[(" + "\n" + ")]}{[(");
        using var _ = model;
        using var __ = service;

        BracketTestHelpers.AssertIsNotBracket(bp, 1, 1);
        BracketTestHelpers.AssertIsNotBracket(bp, 1, 2);
        BracketTestHelpers.AssertIsNotBracket(bp, 1, 3);
        BracketTestHelpers.AssertIsBracket(bp, new TextPosition(1, 4), new TextRange(1, 4, 1, 5), new TextRange(2, 3, 2, 4));
        BracketTestHelpers.AssertIsBracket(bp, new TextPosition(1, 5), new TextRange(1, 5, 1, 6), new TextRange(2, 2, 2, 3));
        BracketTestHelpers.AssertIsBracket(bp, new TextPosition(1, 6), new TextRange(1, 6, 1, 7), new TextRange(2, 1, 2, 2));
        BracketTestHelpers.AssertIsBracket(bp, new TextPosition(1, 7), new TextRange(1, 6, 1, 7), new TextRange(2, 1, 2, 2));

        BracketTestHelpers.AssertIsBracket(bp, new TextPosition(2, 1), new TextRange(2, 1, 2, 2), new TextRange(1, 6, 1, 7));
        BracketTestHelpers.AssertIsBracket(bp, new TextPosition(2, 2), new TextRange(2, 2, 2, 3), new TextRange(1, 5, 1, 6));
        BracketTestHelpers.AssertIsBracket(bp, new TextPosition(2, 3), new TextRange(2, 3, 2, 4), new TextRange(1, 4, 1, 5));
        BracketTestHelpers.AssertIsBracket(bp, new TextPosition(2, 4), new TextRange(2, 3, 2, 4), new TextRange(1, 4, 1, 5));
        BracketTestHelpers.AssertIsNotBracket(bp, 2, 5);
        BracketTestHelpers.AssertIsNotBracket(bp, 2, 6);
        BracketTestHelpers.AssertIsNotBracket(bp, 2, 7);
    }

    [Fact]
    public void Bracket_matching_2()
    {
        var (model, bp, service) = Create(
            "var bar = {" + "\n" +
            "foo: {" + "\n" +
            "}, bar: {hallo: [{" + "\n" +
            "}, {" + "\n" +
            "}]}}");
        using var _ = model;
        using var __ = service;

        (TextPosition Position, TextRange B1, TextRange B2)[] brackets =
        [
            (new(1, 11), new(1, 11, 1, 12), new(5, 4, 5, 5)),
            (new(1, 12), new(1, 11, 1, 12), new(5, 4, 5, 5)),

            (new(2, 6), new(2, 6, 2, 7), new(3, 1, 3, 2)),
            (new(2, 7), new(2, 6, 2, 7), new(3, 1, 3, 2)),

            (new(3, 1), new(3, 1, 3, 2), new(2, 6, 2, 7)),
            (new(3, 2), new(3, 1, 3, 2), new(2, 6, 2, 7)),
            (new(3, 9), new(3, 9, 3, 10), new(5, 3, 5, 4)),
            (new(3, 10), new(3, 9, 3, 10), new(5, 3, 5, 4)),
            (new(3, 17), new(3, 17, 3, 18), new(5, 2, 5, 3)),
            (new(3, 18), new(3, 18, 3, 19), new(4, 1, 4, 2)),
            (new(3, 19), new(3, 18, 3, 19), new(4, 1, 4, 2)),

            (new(4, 1), new(4, 1, 4, 2), new(3, 18, 3, 19)),
            (new(4, 2), new(4, 1, 4, 2), new(3, 18, 3, 19)),
            (new(4, 4), new(4, 4, 4, 5), new(5, 1, 5, 2)),
            (new(4, 5), new(4, 4, 4, 5), new(5, 1, 5, 2)),

            (new(5, 1), new(5, 1, 5, 2), new(4, 4, 4, 5)),
            (new(5, 2), new(5, 2, 5, 3), new(3, 17, 3, 18)),
            (new(5, 3), new(5, 3, 5, 4), new(3, 9, 3, 10)),
            (new(5, 4), new(5, 4, 5, 5), new(1, 11, 1, 12)),
            (new(5, 5), new(5, 4, 5, 5), new(1, 11, 1, 12)),
        ];

        var isABracket = new HashSet<(int, int)>();
        foreach (var (testPos, b1, b2) in brackets)
        {
            BracketTestHelpers.AssertIsBracket(bp, testPos, b1, b2);
            isABracket.Add((testPos.LineNumber, testPos.Column));
        }

        for (var i = 1; i <= model.GetLineCount(); i++)
        {
            var line = model.GetLineContent(i);
            for (var j = 1; j <= line.Length + 1; j++)
            {
                if (!isABracket.Contains((i, j)))
                {
                    BracketTestHelpers.AssertIsNotBracket(bp, i, j);
                }
            }
        }
    }
}

public sealed class TextModelWithTokens2Tests
{
    [Fact]
    public void Bracket_matching_3()
    {
        var text = string.Join('\n',
            "begin",
            "    loop",
            "        if then",
            "        end if;",
            "    end loop;",
            "end;",
            "",
            "begin",
            "    loop",
            "        if then",
            "        end ifa;",
            "    end loop;",
            "end;");

        var (model, bp, service) = BracketTestHelpers.CreateTextModelWithBrackets(text, [("if", "end if"), ("loop", "end loop"), ("begin", "end")]);
        using var _ = model;
        using var __ = service;

        // <if> ... <end ifa> is not matched
        BracketTestHelpers.AssertIsNotBracket(bp, 10, 9);

        // <if> ... <end if> is matched
        BracketTestHelpers.AssertIsBracket(bp, new TextPosition(3, 9), new TextRange(3, 9, 3, 11), new TextRange(4, 9, 4, 15));
        BracketTestHelpers.AssertIsBracket(bp, new TextPosition(4, 9), new TextRange(4, 9, 4, 15), new TextRange(3, 9, 3, 11));

        // <loop> ... <end loop> is matched
        BracketTestHelpers.AssertIsBracket(bp, new TextPosition(2, 5), new TextRange(2, 5, 2, 9), new TextRange(5, 5, 5, 13));
        BracketTestHelpers.AssertIsBracket(bp, new TextPosition(5, 5), new TextRange(5, 5, 5, 13), new TextRange(2, 5, 2, 9));

        // <begin> ... <end> is matched
        BracketTestHelpers.AssertIsBracket(bp, new TextPosition(1, 1), new TextRange(1, 1, 1, 6), new TextRange(6, 1, 6, 4));
        BracketTestHelpers.AssertIsBracket(bp, new TextPosition(6, 1), new TextRange(6, 1, 6, 4), new TextRange(1, 1, 1, 6));
    }

    [Fact]
    public void Bracket_matching_4()
    {
        var text = string.Join('\n', "recordbegin", "  simplerecordbegin", "  endrecord", "endrecord");

        var (model, bp, service) = BracketTestHelpers.CreateTextModelWithBrackets(text, [("recordbegin", "endrecord"), ("simplerecordbegin", "endrecord")]);
        using var _ = model;
        using var __ = service;

        // <recordbegin> ... <endrecord> is matched
        BracketTestHelpers.AssertIsBracket(bp, new TextPosition(1, 1), new TextRange(1, 1, 1, 12), new TextRange(4, 1, 4, 10));
        BracketTestHelpers.AssertIsBracket(bp, new TextPosition(4, 1), new TextRange(4, 1, 4, 10), new TextRange(1, 1, 1, 12));

        // <simplerecordbegin> ... <endrecord> is matched
        BracketTestHelpers.AssertIsBracket(bp, new TextPosition(2, 3), new TextRange(2, 3, 2, 20), new TextRange(3, 3, 3, 12));
        BracketTestHelpers.AssertIsBracket(bp, new TextPosition(3, 3), new TextRange(3, 3, 3, 12), new TextRange(2, 3, 2, 20));
    }

    private sealed class LineSwitchSupport(Func<string, uint[]> tokensFor) : ITokenizationSupport
    {
        public IState GetInitialState() => NullState.Instance;

        public TokenizationResult Tokenize(string line, bool hasEOL, IState state) => throw new NotSupportedException();

        public EncodedTokenizationResult TokenizeEncoded(string line, bool hasEOL, IState state) => new(tokensFor(line), state);
    }

    [Fact]
    public void Issue_95843_Highlighting_of_closing_braces_is_indicating_wrong_brace_when_cursor_is_behind_opening_brace()
    {
        const string mode1 = "testMode1";
        const string mode2 = "testMode2";
        var codec = new LanguageIdCodec();
        codec.Register(mode1);
        codec.Register(mode2);
        var encodedMode1 = (uint)codec.EncodeLanguageId(mode1);
        var encodedMode2 = (uint)codec.EncodeLanguageId(mode2);

        var otherMetadata1 = (encodedMode1 << MetadataConsts.LanguageIdOffset) | ((uint)StandardTokenType.Other << MetadataConsts.TokenTypeOffset) | MetadataConsts.BalancedBracketsMask;
        var otherMetadata2 = (encodedMode2 << MetadataConsts.LanguageIdOffset) | ((uint)StandardTokenType.Other << MetadataConsts.TokenTypeOffset) | MetadataConsts.BalancedBracketsMask;

        var support = new LineSwitchSupport(line => line switch
        {
            "function f() {" =>
            [
                0, otherMetadata1, 8, otherMetadata1, 9, otherMetadata1, 10, otherMetadata1, 11, otherMetadata1, 12, otherMetadata1, 13, otherMetadata1,
            ],
            "  return <p>{true}</p>;" =>
            [
                0, otherMetadata1, 2, otherMetadata1, 8, otherMetadata1, 9, otherMetadata2, 10, otherMetadata2, 11, otherMetadata2,
                12, otherMetadata2, 13, otherMetadata1, 17, otherMetadata2, 18, otherMetadata2, 20, otherMetadata2, 21, otherMetadata2,
                22, otherMetadata2,
            ],
            "}" => [0, otherMetadata1],
            _ => throw new InvalidOperationException("Unexpected"),
        });

        using var service = new LanguageConfigurationService();
        service.Register(mode1, new LanguageConfiguration { Brackets = [("{", "}"), ("[", "]"), ("(", ")")] });
        service.Register(mode2, new LanguageConfiguration { Brackets = [("{", "}"), ("[", "]"), ("(", ")")] });

        using var model = new TextModel(
            string.Join('\n', "function f() {", "  return <p>{true}</p>;", "}"),
            mode1, TextModel.DefaultCreationOptions, TestTextModel.CreateUndoRedoService(), null, codec);
        using var registration = TestTokenizationSupports.Register(model, support);

        model.Tokenization.ForceTokenization(1);
        model.Tokenization.ForceTokenization(2);
        model.Tokenization.ForceTokenization(3);

        var match = model.GetBracketPairs(service).MatchBracket(new TextPosition(2, 14));
        match.ShouldNotBeNull();
        match.Value.Open.ShouldBe(new TextRange(2, 13, 2, 14));
        match.Value.Close.ShouldBe(new TextRange(2, 18, 2, 19));
    }

    [Fact]
    public void Issue_88075_TypeScript_brace_matching_is_incorrect_in_template_strings()
    {
        const string mode = "testMode";
        var codec = new LanguageIdCodec();
        codec.Register(mode);
        var encodedMode = (uint)codec.EncodeLanguageId(mode);

        var otherMetadata = (encodedMode << MetadataConsts.LanguageIdOffset) | ((uint)StandardTokenType.Other << MetadataConsts.TokenTypeOffset);
        var stringMetadata = (encodedMode << MetadataConsts.LanguageIdOffset) | ((uint)StandardTokenType.String << MetadataConsts.TokenTypeOffset);

        var support = new LineSwitchSupport(line => line switch
        {
            "function hello() {" => [0, otherMetadata],
            "    console.log(`${100}`);" => [0, otherMetadata, 16, stringMetadata, 19, otherMetadata, 22, stringMetadata, 24, otherMetadata],
            "}" => [0, otherMetadata],
            _ => throw new InvalidOperationException("Unexpected"),
        });

        using var service = new LanguageConfigurationService();
        service.Register(mode, new LanguageConfiguration { Brackets = [("{", "}"), ("[", "]"), ("(", ")")] });

        using var model = new TextModel(
            string.Join('\n', "function hello() {", "    console.log(`${100}`);", "}"),
            mode, TextModel.DefaultCreationOptions, TestTextModel.CreateUndoRedoService(), null, codec);
        using var registration = TestTokenizationSupports.Register(model, support);

        model.Tokenization.ForceTokenization(1);
        model.Tokenization.ForceTokenization(2);
        model.Tokenization.ForceTokenization(3);

        var bp = model.GetBracketPairs(service);
        bp.MatchBracket(new TextPosition(2, 23)).ShouldBeNull();
        bp.MatchBracket(new TextPosition(2, 20)).ShouldBeNull();
    }
}

public sealed class TextModelWithTokensBracketRegressionTests
{
    [Fact]
    public void Monaco_editor_133_Error_Cannot_read_property_modeId_of_undefined()
    {
        var text = string.Join('\n',
            "Imports System",
            "Imports System.Collections.Generic",
            "",
            "Module m1",
            "",
            "\tSub Main()",
            "\tEnd Sub",
            "",
            "End Module");
        var (model, bp, service) = BracketTestHelpers.CreateTextModelWithBrackets(text, [("module", "end module"), ("sub", "end sub")]);
        using var _ = model;
        using var __ = service;

        var actual = bp.MatchBracket(new TextPosition(4, 1));
        actual.ShouldNotBeNull();
        actual.Value.Open.ShouldBe(new TextRange(4, 1, 4, 7));
        actual.Value.Close.ShouldBe(new TextRange(9, 1, 9, 11));
    }

    [Fact]
    public void Issue_11856_Bracket_matching_does_not_work_as_expected_if_the_opening_brace_symbol_is_contained_in_the_closing_brace_symbol()
    {
        var text = string.Join('\n',
            "sequence \"outer\"",
            "     sequence \"inner\"",
            "     endsequence",
            "endsequence");
        var (model, bp, service) = BracketTestHelpers.CreateTextModelWithBrackets(text, [("sequence", "endsequence"), ("feature", "endfeature")]);
        using var _ = model;
        using var __ = service;

        var actual = bp.MatchBracket(new TextPosition(3, 9));
        actual.ShouldNotBeNull();
        actual.Value.Open.ShouldBe(new TextRange(2, 6, 2, 14));
        actual.Value.Close.ShouldBe(new TextRange(3, 6, 3, 17));
    }
}
