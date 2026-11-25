// Ported from vscode (src/vs/editor/test/browser/commands/trimTrailingWhitespaceCommand.test.ts, commit b57d5c9e), MIT
// license. Differences from upstream: the tokenization support of 'skips strings and regex if configured' is the
// model's test tokenizer (TestTokenizationSupports.RegisterByLine), which answers by line content as upstream's does.

using Shouldly;
using TKCode.Arc.Editor.Commands;
using TKCode.Arc.Editor.Model;
using TKCode.Arc.Editor.Tokens;
using Xunit;

namespace TKCode.Arc.Editor.Tests;

public sealed class TrimTrailingWhitespaceCommandTests
{
    /// <summary>Create single edit operation</summary>
    private static SingleEditOperation CreateInsertDeleteSingleEditOp(string? text, int positionLineNumber, int positionColumn, int selectionLineNumber, int selectionColumn)
    {
        return new SingleEditOperation(new TextRange(selectionLineNumber, selectionColumn, positionLineNumber, positionColumn), text);
    }

    /// <summary>Create single edit operation</summary>
    private static SingleEditOperation CreateSingleEditOp(string? text, int positionLineNumber, int positionColumn, int selectionLineNumber, int selectionColumn)
    {
        return new SingleEditOperation(new TextRange(selectionLineNumber, selectionColumn, positionLineNumber, positionColumn), text, false);
    }

    private static void AssertTrimTrailingWhitespaceCommand(string[] text, SingleEditOperation[] expected)
    {
        TestTextModel.WithEditorModel(text, model =>
        {
            var op = new TrimTrailingWhitespaceCommand(new TextSelection(1, 1, 1, 1), [], true);
            var actual = TestCommand.GetEditOperation(model, op);
            actual.ShouldBe(expected);
        });
    }

    private static void AssertTrimTrailingWhitespace(string[] text, TextPosition[] cursors, SingleEditOperation[] expected)
    {
        TestTextModel.WithEditorModel(text, model =>
        {
            var actual = TrimTrailingWhitespaceCommand.TrimTrailingWhitespace(model, cursors, true);
            actual.ShouldBe(expected);
        });
    }
    [Fact]
    public void Remove_Trailing_Whitespace()
    {
        AssertTrimTrailingWhitespaceCommand([""], []);
        AssertTrimTrailingWhitespaceCommand(["text"], []);
        AssertTrimTrailingWhitespaceCommand(["text   "], [CreateSingleEditOp(null, 1, 5, 1, 8)]);
        AssertTrimTrailingWhitespaceCommand(["text\t   "], [CreateSingleEditOp(null, 1, 5, 1, 9)]);
        AssertTrimTrailingWhitespaceCommand(["\t   "], [CreateSingleEditOp(null, 1, 1, 1, 5)]);
        AssertTrimTrailingWhitespaceCommand(["text\t"], [CreateSingleEditOp(null, 1, 5, 1, 6)]);
        AssertTrimTrailingWhitespaceCommand([
            "some text\t",
            "some more text",
            "\t  ",
            "even more text  ",
            "and some mixed\t   \t"
        ], [
            CreateSingleEditOp(null, 1, 10, 1, 11),
            CreateSingleEditOp(null, 3, 1, 3, 4),
            CreateSingleEditOp(null, 4, 15, 4, 17),
            CreateSingleEditOp(null, 5, 15, 5, 20)
        ]);


        AssertTrimTrailingWhitespace(["text   "], [new TextPosition(1, 1), new TextPosition(1, 2), new TextPosition(1, 3)], [CreateInsertDeleteSingleEditOp(null, 1, 5, 1, 8)]);
        AssertTrimTrailingWhitespace(["text   "], [new TextPosition(1, 1), new TextPosition(1, 5)], [CreateInsertDeleteSingleEditOp(null, 1, 5, 1, 8)]);
        AssertTrimTrailingWhitespace(["text   "], [new TextPosition(1, 1), new TextPosition(1, 5), new TextPosition(1, 6)], [CreateInsertDeleteSingleEditOp(null, 1, 6, 1, 8)]);
        AssertTrimTrailingWhitespace([
            "some text\t",
            "some more text",
            "\t  ",
            "even more text  ",
            "and some mixed\t   \t"
        ], [], [
            CreateInsertDeleteSingleEditOp(null, 1, 10, 1, 11),
            CreateInsertDeleteSingleEditOp(null, 3, 1, 3, 4),
            CreateInsertDeleteSingleEditOp(null, 4, 15, 4, 17),
            CreateInsertDeleteSingleEditOp(null, 5, 15, 5, 20)
        ]);
        AssertTrimTrailingWhitespace([
            "some text\t",
            "some more text",
            "\t  ",
            "even more text  ",
            "and some mixed\t   \t"
        ], [new TextPosition(1, 11), new TextPosition(3, 2), new TextPosition(5, 1), new TextPosition(4, 1), new TextPosition(5, 10)], [
            CreateInsertDeleteSingleEditOp(null, 3, 2, 3, 4),
            CreateInsertDeleteSingleEditOp(null, 4, 15, 4, 17),
            CreateInsertDeleteSingleEditOp(null, 5, 15, 5, 20)
        ]);
    }

    [Fact]
    public void Skips_Strings_And_Regex_If_Configured()
    {
        const string languageId = "testLanguageId";
        using var model = TestTextModel.Create(
            string.Join('\n',
            [
                "const a = `  ",
                "  a string  ",
                "`;  ",
            ]),
            languageId);
        var encodedLanguageId = (uint)model.Tokenization.LanguageIdCodec.EncodeLanguageId(languageId);

        var otherMetadata =
            (encodedLanguageId << MetadataConsts.LanguageIdOffset)
            | ((uint)StandardTokenType.Other << MetadataConsts.TokenTypeOffset)
            | MetadataConsts.BalancedBracketsMask;
        var stringMetadata =
            (encodedLanguageId << MetadataConsts.LanguageIdOffset)
            | ((uint)StandardTokenType.String << MetadataConsts.TokenTypeOffset)
            | MetadataConsts.BalancedBracketsMask;

        TestTokenizationSupports.RegisterByLine(model, (lineNumber, line) =>
        {
            switch (line)
            {
                case "const a = `  ":
                    return [0, otherMetadata, 10, stringMetadata];
                case "  a string  ":
                    return [0, stringMetadata];
                case "`;  ":
                    return [0, stringMetadata, 1, otherMetadata];
            }
            throw new InvalidOperationException("Unexpected");
        });

        model.Tokenization.ForceTokenization(1);
        model.Tokenization.ForceTokenization(2);
        model.Tokenization.ForceTokenization(3);

        var op = new TrimTrailingWhitespaceCommand(new TextSelection(1, 1, 1, 1), [], false);
        var actual = TestCommand.GetEditOperation(model, op);
        actual.ShouldBe([CreateSingleEditOp(null, 3, 3, 3, 5)]);
    }
}
