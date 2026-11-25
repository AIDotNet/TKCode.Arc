// Ported from vscode (src/vs/editor/test/common/model/tokensStore.test.ts, commit b57d5c9e), MIT license.
// Left out: 'issue #147944' (needs the model's getWordAtPosition, which this port does not have yet).

using Shouldly;
using TKCode.Arc.Editor.Languages;
using TKCode.Arc.Editor.Model;
using TKCode.Arc.Editor.Tokens;
using Xunit;

namespace TKCode.Arc.Editor.Tests;

public sealed class TokensStoreTests
{
    private const ColorId SemanticColor = (ColorId)5;

    private static (string Text, SparseMultilineTokens Tokens) ParseTokensState(string[] state)
    {
        var text = new List<string>();
        var tokens = new List<uint>();
        var baseLine = 1;
        for (var i = 0; i < state.Length; i++)
        {
            var line = state[i];

            var startOffset = 0;
            var lineText = string.Empty;
            while (true)
            {
                var firstPipeOffset = line.IndexOf('|', startOffset);
                if (firstPipeOffset == -1)
                {
                    break;
                }

                var secondPipeOffset = line.IndexOf('|', firstPipeOffset + 1);
                if (secondPipeOffset == -1)
                {
                    break;
                }

                if (firstPipeOffset + 1 == secondPipeOffset)
                {
                    // skip ||
                    lineText += line[startOffset..(secondPipeOffset + 1)];
                    startOffset = secondPipeOffset + 1;
                    continue;
                }

                lineText += line[startOffset..firstPipeOffset];
                var tokenStartCharacter = lineText.Length;
                var tokenLength = secondPipeOffset - firstPipeOffset - 1;
                var metadata = ((uint)SemanticColor << MetadataConsts.ForegroundOffset) | MetadataConsts.SemanticUseForeground;

                if (tokens.Count == 0)
                {
                    baseLine = i + 1;
                }

                tokens.AddRange([(uint)(i + 1 - baseLine), (uint)tokenStartCharacter, (uint)(tokenStartCharacter + tokenLength), metadata]);

                lineText += line.Substring(firstPipeOffset + 1, tokenLength);
                startOffset = secondPipeOffset + 1;
            }

            lineText += line[startOffset..];

            text.Add(lineText);
        }

        return (string.Join('\n', text), SparseMultilineTokens.Create(baseLine, tokens.ToArray()));
    }

    private static string[] ExtractState(TextModel model)
    {
        var result = new List<string>();
        for (var lineNumber = 1; lineNumber <= model.GetLineCount(); lineNumber++)
        {
            var lineTokens = model.Tokenization.GetLineTokens(lineNumber);
            var lineContent = model.GetLineContent(lineNumber);

            var lineText = string.Empty;
            for (var i = 0; i < lineTokens.GetCount(); i++)
            {
                var tokenStartCharacter = lineTokens.GetStartOffset(i);
                var tokenEndCharacter = lineTokens.GetEndOffset(i);
                var metadata = lineTokens.GetMetadata(i);
                var color = TokenMetadata.GetForeground(metadata);
                var tokenText = lineContent[tokenStartCharacter..tokenEndCharacter];
                lineText += color == SemanticColor ? $"|{tokenText}|" : tokenText;
            }

            result.Add(lineText);
        }

        return result.ToArray();
    }

    private static void TestTokensAdjustment(string[] rawInitialState, SingleEditOperation[] edits, string[] rawFinalState)
    {
        var initialState = ParseTokensState(rawInitialState);
        using var model = TestTextModel.Create(initialState.Text);
        model.Tokenization.SetSemanticTokens([initialState.Tokens], true);

        model.ApplyEdits(edits);

        ExtractState(model).ShouldBe(rawFinalState);
    }

    private static SingleEditOperation Edit(int startLine, int startColumn, int endLine, int endColumn, string text) =>
        new(new TextRange(startLine, startColumn, endLine, endColumn), text);

    [Fact]
    public void Issue_86303_color_shifting_between_different_tokens()
    {
        TestTokensAdjustment(
            [
                "import { |URI| } from 'vs/base/common/uri';",
                "const foo = |URI|.parse('hey');",
            ],
            [Edit(2, 9, 2, 10, string.Empty)],
            [
                "import { |URI| } from 'vs/base/common/uri';",
                "const fo = |URI|.parse('hey');",
            ]);
    }

    [Fact]
    public void Deleting_a_newline()
    {
        TestTokensAdjustment(
            [
                "import { |URI| } from 'vs/base/common/uri';",
                "const foo = |URI|.parse('hey');",
            ],
            [Edit(1, 42, 2, 1, string.Empty)],
            ["import { |URI| } from 'vs/base/common/uri';const foo = |URI|.parse('hey');"]);
    }

    [Fact]
    public void Inserting_a_newline()
    {
        TestTokensAdjustment(
            ["import { |URI| } from 'vs/base/common/uri';const foo = |URI|.parse('hey');"],
            [Edit(1, 42, 1, 42, "\n")],
            [
                "import { |URI| } from 'vs/base/common/uri';",
                "const foo = |URI|.parse('hey');",
            ]);
    }

    [Fact]
    public void Deleting_a_newline_2()
    {
        TestTokensAdjustment(
            [
                "import { ",
                "    |URI| } from 'vs/base/common/uri';const foo = |URI|.parse('hey');",
            ],
            [Edit(1, 10, 2, 5, string.Empty)],
            ["import { |URI| } from 'vs/base/common/uri';const foo = |URI|.parse('hey');"]);
    }

    [Fact]
    public void Issue_179268_a_complex_edit()
    {
        TestTokensAdjustment(
            [
                "|export| |'interior_material_selector.dart'|;",
                "|export| |'mileage_selector.dart'|;",
                "|export| |'owners_selector.dart'|;",
                "|export| |'price_selector.dart'|;",
                "|export| |'seat_count_selector.dart'|;",
                "|export| |'year_selector.dart'|;",
                "|export| |'winter_options_selector.dart'|;|export| |'camera_selector.dart'|;",
            ],
            [
                Edit(1, 9, 1, 9, "camera_selector.dart';\nexport '"),
                Edit(6, 9, 7, 9, string.Empty),
                Edit(7, 39, 7, 39, "\n"),
                Edit(7, 47, 7, 48, "ye"),
                Edit(7, 49, 7, 51, string.Empty),
                Edit(7, 52, 7, 53, string.Empty),
            ],
            [
                "|export| |'|camera_selector.dart';",
                "export 'interior_material_selector.dart';",
                "|export| |'mileage_selector.dart'|;",
                "|export| |'owners_selector.dart'|;",
                "|export| |'price_selector.dart'|;",
                "|export| |'seat_count_selector.dart'|;",
                "|export| |'||winter_options_selector.dart'|;",
                "|export| |'year_selector.dart'|;",
            ]);
    }

    [Fact]
    public void Issue_91936_Semantic_token_color_highlighting_fails_on_line_with_selected_text()
    {
        using var model = TestTextModel.Create("                    else if ($s = 08) then '\\b'");
        model.Tokenization.SetSemanticTokens(
        [
            SparseMultilineTokens.Create(1,
            [
                0, 20, 24, 0b01111000000000010000,
                0, 25, 27, 0b01111000000000010000,
                0, 28, 29, 0b00001000000000010000,
                0, 29, 31, 0b10000000000000010000,
                0, 32, 33, 0b00001000000000010000,
                0, 34, 36, 0b00110000000000010000,
                0, 36, 37, 0b00001000000000010000,
                0, 38, 42, 0b01111000000000010000,
                0, 43, 47, 0b01011000000000010000,
            ]),
        ], true);
        var lineTokens = model.Tokenization.GetLineTokens(1);
        var decodedTokens = new List<uint>();
        for (int i = 0, len = lineTokens.GetCount(); i < len; i++)
        {
            decodedTokens.Add((uint)lineTokens.GetEndOffset(i));
            decodedTokens.Add(lineTokens.GetMetadata(i));
        }

        decodedTokens.ShouldBe(
        [
            20, 0b10000000001000010000000001,
            24, 0b10000001111000010000000001,
            25, 0b10000000001000010000000001,
            27, 0b10000001111000010000000001,
            28, 0b10000000001000010000000001,
            29, 0b10000000001000010000000001,
            31, 0b10000010000000010000000001,
            32, 0b10000000001000010000000001,
            33, 0b10000000001000010000000001,
            34, 0b10000000001000010000000001,
            36, 0b10000000110000010000000001,
            37, 0b10000000001000010000000001,
            38, 0b10000000001000010000000001,
            42, 0b10000001111000010000000001,
            43, 0b10000000001000010000000001,
            47, 0b10000001011000010000000001,
        ]);
    }

    private static LineTokens EnumLineTokens(LanguageIdCodec codec) => new([12, 1], "enum Enum1 {", codec);

    [Fact]
    public void Partial_tokens_1()
    {
        var codec = new LanguageIdCodec();
        var store = new SparseTokensStore(codec);

        store.SetPartial(new TextRange(1, 1, 31, 2),
        [
            SparseMultilineTokens.Create(5,
            [
                0, 5, 10, 1,
                5, 5, 10, 2,
                10, 5, 10, 3,
                15, 5, 10, 4,
                20, 5, 10, 5,
                25, 5, 10, 6,
            ]),
        ]);

        store.SetPartial(new TextRange(18, 1, 42, 1),
        [
            SparseMultilineTokens.Create(20,
            [
                0, 5, 10, 4,
                5, 5, 10, 5,
                10, 5, 10, 6,
                15, 5, 10, 7,
                20, 5, 10, 8,
            ]),
        ]);

        store.SetPartial(new TextRange(1, 1, 31, 2),
        [
            SparseMultilineTokens.Create(5,
            [
                0, 5, 10, 1,
                5, 5, 10, 2,
                10, 5, 10, 3,
                15, 5, 10, 4,
                20, 5, 10, 5,
                25, 5, 10, 6,
            ]),
        ]);

        var lineTokens = store.AddSparseTokens(10, EnumLineTokens(codec));
        lineTokens.GetCount().ShouldBe(3);
    }

    [Fact]
    public void Partial_tokens_2()
    {
        var codec = new LanguageIdCodec();
        var store = new SparseTokensStore(codec);

        store.SetPartial(new TextRange(1, 1, 31, 2),
        [
            SparseMultilineTokens.Create(5,
            [
                0, 5, 10, 1,
                5, 5, 10, 2,
                10, 5, 10, 3,
                15, 5, 10, 4,
                20, 5, 10, 5,
                25, 5, 10, 6,
            ]),
        ]);

        store.SetPartial(new TextRange(6, 1, 36, 2),
        [
            SparseMultilineTokens.Create(10,
            [
                0, 5, 10, 2,
                5, 5, 10, 3,
                10, 5, 10, 4,
                15, 5, 10, 5,
                20, 5, 10, 6,
            ]),
        ]);

        store.SetPartial(new TextRange(17, 1, 42, 1),
        [
            SparseMultilineTokens.Create(20,
            [
                0, 5, 10, 4,
                5, 5, 10, 5,
                10, 5, 10, 6,
                15, 5, 10, 7,
                20, 5, 10, 8,
            ]),
        ]);

        var lineTokens = store.AddSparseTokens(20, EnumLineTokens(codec));
        lineTokens.GetCount().ShouldBe(3);
    }

    [Fact]
    public void Partial_tokens_3()
    {
        var codec = new LanguageIdCodec();
        var store = new SparseTokensStore(codec);

        store.SetPartial(new TextRange(1, 1, 31, 2),
        [
            SparseMultilineTokens.Create(5,
            [
                0, 5, 10, 1,
                5, 5, 10, 2,
                10, 5, 10, 3,
                15, 5, 10, 4,
                20, 5, 10, 5,
                25, 5, 10, 6,
            ]),
        ]);

        store.SetPartial(new TextRange(11, 1, 16, 2),
        [
            SparseMultilineTokens.Create(10,
            [
                0, 5, 10, 3,
                5, 5, 10, 4,
            ]),
        ]);

        var lineTokens = store.AddSparseTokens(5, EnumLineTokens(codec));
        lineTokens.GetCount().ShouldBe(3);
    }

    [Fact]
    public void Issue_94133_Semantic_colors_stick_around_when_using_only_range_provider()
    {
        var codec = new LanguageIdCodec();
        var store = new SparseTokensStore(codec);

        store.SetPartial(new TextRange(1, 1, 1, 20), [SparseMultilineTokens.Create(1, [0, 9, 11, 1])]);

        store.SetPartial(new TextRange(1, 1, 1, 20), []);

        var lineTokens = store.AddSparseTokens(1, EnumLineTokens(codec));
        lineTokens.GetCount().ShouldBe(1);
    }

    [Fact]
    public void Bug()
    {
        static SparseMultilineTokens CreateTokens(string str)
        {
            str = str.StartsWith("[(", StringComparison.Ordinal) ? str[2..] : str;
            str = str.EndsWith(")]", StringComparison.Ordinal) ? str[..^2] : str;
            var strTokens = str.Split("),(");
            var result = new List<uint>();
            var firstLineNumber = 0;
            foreach (var strToken in strTokens)
            {
                var pieces = strToken.Split(',');
                var chars = pieces[1].Split('-');
                var lineNumber = int.Parse(pieces[0], System.Globalization.CultureInfo.InvariantCulture);
                var startChar = int.Parse(chars[0], System.Globalization.CultureInfo.InvariantCulture);
                var endChar = int.Parse(chars[1], System.Globalization.CultureInfo.InvariantCulture);
                if (firstLineNumber == 0)
                {
                    // this is the first line
                    firstLineNumber = lineNumber;
                }

                result.AddRange([(uint)(lineNumber - firstLineNumber), (uint)startChar, (uint)endChar, (uint)((lineNumber + startChar) % 13)]);
            }

            return SparseMultilineTokens.Create(firstLineNumber, result.ToArray());
        }

        var codec = new LanguageIdCodec();
        var store = new SparseTokensStore(codec);
        store.SetPartial(
            new TextRange(36446, 1, 36475, 115),
            [CreateTokens("[(36448,24-29),(36448,33-46),(36448,47-54),(36450,25-35),(36450,36-50),(36451,28-33),(36451,36-49),(36451,50-57),(36452,35-53),(36452,54-62),(36454,33-38),(36454,41-54),(36454,55-60),(36455,35-53),(36455,54-62),(36457,33-44),(36457,45-49),(36457,50-56),(36457,62-83),(36457,84-88),(36458,35-53),(36458,54-62),(36460,33-37),(36460,38-42),(36460,47-57),(36460,58-67),(36461,35-53),(36461,54-62),(36463,34-38),(36463,39-45),(36463,46-51),(36463,54-63),(36463,64-71),(36463,76-80),(36463,81-87),(36463,88-92),(36463,97-107),(36463,108-119),(36464,35-53),(36464,54-62),(36466,33-71),(36466,72-76),(36467,35-53),(36467,54-62),(36469,24-29),(36469,33-46),(36469,47-54),(36470,24-35),(36470,38-46),(36473,25-35),(36473,36-51),(36474,28-33),(36474,36-49),(36474,50-58),(36475,35-53),(36475,54-62)]")]);
        store.SetPartial(
            new TextRange(36436, 1, 36464, 142),
            [CreateTokens("[(36437,33-37),(36437,38-42),(36437,47-57),(36437,58-67),(36438,35-53),(36438,54-62),(36440,24-29),(36440,33-46),(36440,47-53),(36442,25-35),(36442,36-50),(36443,30-39),(36443,42-46),(36443,47-53),(36443,54-58),(36443,63-73),(36443,74-84),(36443,87-91),(36443,92-98),(36443,101-105),(36443,106-112),(36443,113-119),(36444,28-37),(36444,38-42),(36444,47-57),(36444,58-75),(36444,80-95),(36444,96-105),(36445,35-53),(36445,54-62),(36448,24-29),(36448,33-46),(36448,47-54),(36450,25-35),(36450,36-50),(36451,28-33),(36451,36-49),(36451,50-57),(36452,35-53),(36452,54-62),(36454,33-38),(36454,41-54),(36454,55-60),(36455,35-53),(36455,54-62),(36457,33-44),(36457,45-49),(36457,50-56),(36457,62-83),(36457,84-88),(36458,35-53),(36458,54-62),(36460,33-37),(36460,38-42),(36460,47-57),(36460,58-67),(36461,35-53),(36461,54-62),(36463,34-38),(36463,39-45),(36463,46-51),(36463,54-63),(36463,64-71),(36463,76-80),(36463,81-87),(36463,88-92),(36463,97-107),(36463,108-119),(36464,35-53),(36464,54-62)]")]);
        store.SetPartial(
            new TextRange(36457, 1, 36485, 140),
            [CreateTokens("[(36457,33-44),(36457,45-49),(36457,50-56),(36457,62-83),(36457,84-88),(36458,35-53),(36458,54-62),(36460,33-37),(36460,38-42),(36460,47-57),(36460,58-67),(36461,35-53),(36461,54-62),(36463,34-38),(36463,39-45),(36463,46-51),(36463,54-63),(36463,64-71),(36463,76-80),(36463,81-87),(36463,88-92),(36463,97-107),(36463,108-119),(36464,35-53),(36464,54-62),(36466,33-71),(36466,72-76),(36467,35-53),(36467,54-62),(36469,24-29),(36469,33-46),(36469,47-54),(36470,24-35),(36470,38-46),(36473,25-35),(36473,36-51),(36474,28-33),(36474,36-49),(36474,50-58),(36475,35-53),(36475,54-62),(36477,28-32),(36477,33-37),(36477,42-52),(36477,53-69),(36478,32-36),(36478,37-41),(36478,46-56),(36478,57-74),(36479,32-36),(36479,37-41),(36479,46-56),(36479,57-76),(36480,32-36),(36480,37-41),(36480,46-56),(36480,57-68),(36481,32-36),(36481,37-41),(36481,46-56),(36481,57-68),(36482,39-57),(36482,58-66),(36484,34-38),(36484,39-45),(36484,46-50),(36484,55-65),(36484,66-82),(36484,86-97),(36484,98-102),(36484,103-109),(36484,111-124),(36484,125-133),(36485,39-57),(36485,58-66)]")]);
        store.SetPartial(
            new TextRange(36441, 1, 36469, 56),
            [CreateTokens("[(36442,25-35),(36442,36-50),(36443,30-39),(36443,42-46),(36443,47-53),(36443,54-58),(36443,63-73),(36443,74-84),(36443,87-91),(36443,92-98),(36443,101-105),(36443,106-112),(36443,113-119),(36444,28-37),(36444,38-42),(36444,47-57),(36444,58-75),(36444,80-95),(36444,96-105),(36445,35-53),(36445,54-62),(36448,24-29),(36448,33-46),(36448,47-54),(36450,25-35),(36450,36-50),(36451,28-33),(36451,36-49),(36451,50-57),(36452,35-53),(36452,54-62),(36454,33-38),(36454,41-54),(36454,55-60),(36455,35-53),(36455,54-62),(36457,33-44),(36457,45-49),(36457,50-56),(36457,62-83),(36457,84-88),(36458,35-53),(36458,54-62),(36460,33-37),(36460,38-42),(36460,47-57),(36460,58-67),(36461,35-53),(36461,54-62),(36463,34-38),(36463,39-45),(36463,46-51),(36463,54-63),(36463,64-71),(36463,76-80),(36463,81-87),(36463,88-92),(36463,97-107),(36463,108-119),(36464,35-53),(36464,54-62),(36466,33-71),(36466,72-76),(36467,35-53),(36467,54-62),(36469,24-29),(36469,33-46),(36469,47-54),(36470,24-35)]")]);

        var lineTokens = store.AddSparseTokens(36451, new LineTokens([60, 1], "                        if (flags & ModifierFlags.Ambient) {", codec));
        lineTokens.GetCount().ShouldBe(7);
    }

    [Fact]
    public void Issue_95949_Identifiers_are_colored_in_bold_when_targetting_keywords()
    {
        static uint CreateTMMetadata(uint foreground, FontStyle fontStyle, uint languageId) =>
            (languageId << MetadataConsts.LanguageIdOffset)
            | ((uint)fontStyle << MetadataConsts.FontStyleOffset)
            | (foreground << MetadataConsts.ForegroundOffset);

        static List<uint> ToArr(LineTokens lineTokens)
        {
            var r = new List<uint>();
            for (var i = 0; i < lineTokens.GetCount(); i++)
            {
                r.Add((uint)lineTokens.GetEndOffset(i));
                r.Add(lineTokens.GetMetadata(i));
            }

            return r;
        }

        var codec = new LanguageIdCodec();
        var store = new SparseTokensStore(codec);

        store.Set([SparseMultilineTokens.Create(1, [0, 6, 11, (1u << MetadataConsts.ForegroundOffset) | MetadataConsts.SemanticUseForeground])], true);

        var lineTokens = store.AddSparseTokens(1, new LineTokens(
        [
            5, CreateTMMetadata(5, FontStyle.Bold, 53),
            14, CreateTMMetadata(1, FontStyle.None, 53),
            17, CreateTMMetadata(6, FontStyle.None, 53),
            18, CreateTMMetadata(1, FontStyle.None, 53),
        ], "const hello = 123;", codec));

        ToArr(lineTokens).ShouldBe(
        [
            5, CreateTMMetadata(5, FontStyle.Bold, 53),
            6, CreateTMMetadata(1, FontStyle.None, 53),
            11, CreateTMMetadata(1, FontStyle.None, 53),
            14, CreateTMMetadata(1, FontStyle.None, 53),
            17, CreateTMMetadata(6, FontStyle.None, 53),
            18, CreateTMMetadata(1, FontStyle.None, 53),
        ]);
    }

    [Fact]
    public void BUG_setPartial_with_startLineNumber_greater_than_1_and_token_removal_creates_invalid_state()
    {
        var codec = new LanguageIdCodec();
        var store = new SparseTokensStore(codec);

        // Set initial tokens on line 5
        store.Set([SparseMultilineTokens.Create(5, [0, 5, 10, 1])], false);

        store.IsEmpty().ShouldBeFalse();

        // Remove all tokens via setPartial
        store.SetPartial(new TextRange(5, 1, 5, 20), []);

        store.IsEmpty().ShouldBeTrue("Store should be empty after setPartial removes all tokens");
    }

    [Fact]
    public void BUG_setPartial_with_split_that_creates_empty_first_piece_with_invalid_line_numbers()
    {
        var codec = new LanguageIdCodec();
        var store = new SparseTokensStore(codec);

        // Set initial tokens - token is on line 11
        store.Set([SparseMultilineTokens.Create(1, [10, 5, 10, 1])], false);

        // setPartial with a range [1,1 -> 5,1] that will cause a split where the first piece is empty
        store.SetPartial(new TextRange(1, 1, 5, 1), []);

        store.IsEmpty().ShouldBeFalse("Store should still have the token on line 11");

        // The token at line 11 should be retrievable after the split
        var lineTokens = store.AddSparseTokens(11, new LineTokens([22, 1], "    test line text    ", codec));
        lineTokens.GetCount().ShouldBe(3, "Should have 3 tokens: base token start + semantic token from line 11 + base token end");
        lineTokens.GetStartOffset(1).ShouldBe(5, "Semantic token should start at offset 5");
        lineTokens.GetEndOffset(1).ShouldBe(10, "Semantic token should end at offset 10");
    }

    [Fact]
    public void AddSparseTokens_skips_overlapping_semantic_tokens_that_produce_backward_endOffsets()
    {
        var codec = new LanguageIdCodec();
        var store = new SparseTokensStore(codec);

        var semanticMeta1 = (1u << MetadataConsts.ForegroundOffset) | MetadataConsts.SemanticUseForeground;
        var semanticMeta2 = (2u << MetadataConsts.ForegroundOffset) | MetadataConsts.SemanticUseForeground;
        store.Set(
        [
            SparseMultilineTokens.Create(1,
            [
                // deltaLine, startChar, endChar, metadata
                0, 0, 1, semanticMeta1, // 'f' at (0,1)
                0, 1, 2, semanticMeta2, // '=' at (1,2)
                0, 2, 3, semanticMeta1, // '1' at (2,3)
                0, 3, 5, semanticMeta2, // '+a' at (3,5) - expanded after edit
                0, 4, 5, semanticMeta1, // overlapping: 'a' at (4,5) - stale position
            ]),
        ], true);

        var tmMeta = 3u << MetadataConsts.ForegroundOffset;
        var lineTokens = store.AddSparseTokens(1, new LineTokens([6, tmMeta], "f=1+a2", codec));

        // Verify endOffsets are monotonically increasing (no backward sequences)
        var endOffsets = new List<int>();
        for (var i = 0; i < lineTokens.GetCount(); i++)
        {
            endOffsets.Add(lineTokens.GetEndOffset(i));
        }

        for (var i = 1; i < endOffsets.Count; i++)
        {
            endOffsets[i].ShouldBeGreaterThan(endOffsets[i - 1], $"endOffset[{i}]={endOffsets[i]} should be > endOffset[{i - 1}]={endOffsets[i - 1]}");
        }

        // When used with injected text, the resulting LineTokens must not duplicate characters.
        var withInjected = lineTokens.WithInserted([new InsertedToken(0, "  ", LineTokens.DefaultTokenMetadata)]);
        withInjected.GetLineContent().ShouldBe("  f=1+a2", "withInserted must not duplicate characters when semantic tokens overlap");
    }

    [Fact]
    public void Piece_with_startLineNumber_0_and_endLineNumber_minus_1_after_encompassing_deletion()
    {
        var codec = new LanguageIdCodec();
        var store = new SparseTokensStore(codec);

        // Set initial tokens on lines 5-10
        var piece = SparseMultilineTokens.Create(5,
        [
            0, 0, 5, 1, // line 5, chars 0-5
            5, 0, 5, 2, // line 10, chars 0-5
        ]);

        store.Set([piece], false);

        // Verify initial state
        piece.StartLineNumber.ShouldBe(5);
        piece.EndLineNumber.ShouldBe(10);
        piece.IsEmpty().ShouldBeFalse();

        // Perform an edit that completely encompasses the token range
        store.AcceptEdit(new TextRange(1, 1, 20, 1), 0, 0, 0, 0);

        // After an encompassing deletion, the piece should be empty
        piece.IsEmpty().ShouldBeTrue("Piece should be empty after encompassing deletion");

        store.IsEmpty().ShouldBeTrue("Store should be empty after all tokens are deleted by encompassing edit");
    }
}
