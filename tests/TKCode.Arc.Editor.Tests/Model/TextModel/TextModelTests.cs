// Ported from vscode (src/vs/editor/test/common/model/textModel.test.ts, commit b57d5c9e), MIT license.
// This file: the 'Editor Model - TextModel' suite. TextPosition and TextRange hold ints, so the positions upstream gives
// as floats are passed floored (what upstream's validation does first: -123.123 is -124, Number.MIN_VALUE is 0,
// Number.MAX_VALUE is int.MaxValue); the tests that pass NaN or undefined are left out. Upstream's expected tab size
// [n] ("only guessed when inserting spaces") is AssertGuessTabSizeOnlyWithSpaces.

using Shouldly;
using TKCode.Arc.Editor.Model;
using Xunit;

namespace TKCode.Arc.Editor.Tests;

public sealed class TextModelTests
{
    private static void TestGuessIndentation(
        bool defaultInsertSpaces,
        int defaultTabSize,
        bool expectedInsertSpaces,
        int expectedTabSize,
        string[] text,
        string? msg = null)
    {
        var m = TestTextModel.Create(
            string.Join('\n', text),
            null,
            TextModel.DefaultCreationOptions with
            {
                TabSize = defaultTabSize,
                InsertSpaces = defaultInsertSpaces,
                DetectIndentation = true,
            });
        var r = m.GetOptions();
        m.Dispose();

        r.InsertSpaces.ShouldBe(expectedInsertSpaces, msg);
        r.TabSize.ShouldBe(expectedTabSize, msg);
    }

    private static void AssertGuess(bool? expectedInsertSpaces, int? expectedTabSize, string[] text, string? msg = null)
    {
        if (expectedInsertSpaces is null)
        {
            // cannot guess insertSpaces
            if (expectedTabSize is null)
            {
                // cannot guess tabSize
                TestGuessIndentation(true, 13370, true, 13370, text, msg);
                TestGuessIndentation(false, 13371, false, 13371, text, msg);
            }
            else
            {
                // can guess tabSize
                TestGuessIndentation(true, 13370, true, expectedTabSize.Value, text, msg);
                TestGuessIndentation(false, 13371, false, expectedTabSize.Value, text, msg);
            }
        }
        else
        {
            // can guess insertSpaces
            if (expectedTabSize is null)
            {
                // cannot guess tabSize
                TestGuessIndentation(true, 13370, expectedInsertSpaces.Value, 13370, text, msg);
                TestGuessIndentation(false, 13371, expectedInsertSpaces.Value, 13371, text, msg);
            }
            else
            {
                // can guess tabSize
                TestGuessIndentation(true, 13370, expectedInsertSpaces.Value, expectedTabSize.Value, text, msg);
                TestGuessIndentation(false, 13371, expectedInsertSpaces.Value, expectedTabSize.Value, text, msg);
            }
        }
    }

    /// <summary>Upstream's assertGuess with an expected tab size of [n]: the tab size is only guessed when inserting spaces.</summary>
    private static void AssertGuessTabSizeOnlyWithSpaces(bool? expectedInsertSpaces, int expectedTabSize, string[] text, string? msg = null)
    {
        if (expectedInsertSpaces is null)
        {
            // can only guess tabSize when insertSpaces is true
            TestGuessIndentation(true, 13370, true, expectedTabSize, text, msg);
            TestGuessIndentation(false, 13371, false, 13371, text, msg);
        }
        else if (expectedInsertSpaces.Value)
        {
            // can only guess tabSize when insertSpaces is true
            TestGuessIndentation(true, 13370, expectedInsertSpaces.Value, expectedTabSize, text, msg);
            TestGuessIndentation(false, 13371, expectedInsertSpaces.Value, expectedTabSize, text, msg);
        }
        else
        {
            TestGuessIndentation(true, 13370, expectedInsertSpaces.Value, 13370, text, msg);
            TestGuessIndentation(false, 13371, expectedInsertSpaces.Value, 13371, text, msg);
        }
    }

    [Fact]
    public void TextModel_Does_Not_Use_Events_Internally()
    {
        // Make sure that all model parts receive text model events explicitly
        // to avoid that by any chance an outside listener receives events before
        // the parts and thus are able to access the text model in an inconsistent state.
        //
        // We simply check that there are no listeners attached to text model
        // after instantiation
        using var textModel = new TextModel(
            string.Empty,
            TestTextModel.PlaintextLanguageId,
            TextModel.DefaultCreationOptions,
            TestTextModel.CreateUndoRedoService());
        textModel.HasListeners().ShouldBeFalse();
    }

    [Fact]
    public void GetValueLengthInRange()
    {
        var m = TestTextModel.Create("My First Line\r\nMy Second Line\r\nMy Third Line");
        m.GetValueLengthInRange(new TextRange(1, 1, 1, 1)).ShouldBe("".Length);
        m.GetValueLengthInRange(new TextRange(1, 1, 1, 2)).ShouldBe("M".Length);
        m.GetValueLengthInRange(new TextRange(1, 2, 1, 3)).ShouldBe("y".Length);
        m.GetValueLengthInRange(new TextRange(1, 1, 1, 14)).ShouldBe("My First Line".Length);
        m.GetValueLengthInRange(new TextRange(1, 1, 2, 1)).ShouldBe("My First Line\r\n".Length);
        m.GetValueLengthInRange(new TextRange(1, 2, 2, 1)).ShouldBe("y First Line\r\n".Length);
        m.GetValueLengthInRange(new TextRange(1, 2, 2, 2)).ShouldBe("y First Line\r\nM".Length);
        m.GetValueLengthInRange(new TextRange(1, 2, 2, 1000)).ShouldBe("y First Line\r\nMy Second Line".Length);
        m.GetValueLengthInRange(new TextRange(1, 2, 3, 1)).ShouldBe("y First Line\r\nMy Second Line\r\n".Length);
        m.GetValueLengthInRange(new TextRange(1, 2, 3, 1000)).ShouldBe("y First Line\r\nMy Second Line\r\nMy Third Line".Length);
        m.GetValueLengthInRange(new TextRange(1, 1, 1000, 1000)).ShouldBe("My First Line\r\nMy Second Line\r\nMy Third Line".Length);
        m.Dispose();

        m = TestTextModel.Create("My First Line\nMy Second Line\nMy Third Line");
        m.GetValueLengthInRange(new TextRange(1, 1, 1, 1)).ShouldBe("".Length);
        m.GetValueLengthInRange(new TextRange(1, 1, 1, 2)).ShouldBe("M".Length);
        m.GetValueLengthInRange(new TextRange(1, 2, 1, 3)).ShouldBe("y".Length);
        m.GetValueLengthInRange(new TextRange(1, 1, 1, 14)).ShouldBe("My First Line".Length);
        m.GetValueLengthInRange(new TextRange(1, 1, 2, 1)).ShouldBe("My First Line\n".Length);
        m.GetValueLengthInRange(new TextRange(1, 2, 2, 1)).ShouldBe("y First Line\n".Length);
        m.GetValueLengthInRange(new TextRange(1, 2, 2, 2)).ShouldBe("y First Line\nM".Length);
        m.GetValueLengthInRange(new TextRange(1, 2, 2, 1000)).ShouldBe("y First Line\nMy Second Line".Length);
        m.GetValueLengthInRange(new TextRange(1, 2, 3, 1)).ShouldBe("y First Line\nMy Second Line\n".Length);
        m.GetValueLengthInRange(new TextRange(1, 2, 3, 1000)).ShouldBe("y First Line\nMy Second Line\nMy Third Line".Length);
        m.GetValueLengthInRange(new TextRange(1, 1, 1000, 1000)).ShouldBe("My First Line\nMy Second Line\nMy Third Line".Length);
        m.Dispose();
    }

    [Fact]
    public void GetValueLengthInRange_Different_EOL()
    {
        var m = TestTextModel.Create("My First Line\r\nMy Second Line\r\nMy Third Line");
        m.GetValueLengthInRange(new TextRange(1, 1, 2, 1), EndOfLinePreference.TextDefined).ShouldBe("My First Line\r\n".Length);
        m.GetValueLengthInRange(new TextRange(1, 1, 2, 1), EndOfLinePreference.CRLF).ShouldBe("My First Line\r\n".Length);
        m.GetValueLengthInRange(new TextRange(1, 1, 2, 1), EndOfLinePreference.LF).ShouldBe("My First Line\n".Length);
        m.GetValueLengthInRange(new TextRange(1, 1, 1000, 1000), EndOfLinePreference.TextDefined).ShouldBe("My First Line\r\nMy Second Line\r\nMy Third Line".Length);
        m.GetValueLengthInRange(new TextRange(1, 1, 1000, 1000), EndOfLinePreference.CRLF).ShouldBe("My First Line\r\nMy Second Line\r\nMy Third Line".Length);
        m.GetValueLengthInRange(new TextRange(1, 1, 1000, 1000), EndOfLinePreference.LF).ShouldBe("My First Line\nMy Second Line\nMy Third Line".Length);
        m.Dispose();

        m = TestTextModel.Create("My First Line\nMy Second Line\nMy Third Line");
        m.GetValueLengthInRange(new TextRange(1, 1, 2, 1), EndOfLinePreference.TextDefined).ShouldBe("My First Line\n".Length);
        m.GetValueLengthInRange(new TextRange(1, 1, 2, 1), EndOfLinePreference.LF).ShouldBe("My First Line\n".Length);
        m.GetValueLengthInRange(new TextRange(1, 1, 2, 1), EndOfLinePreference.CRLF).ShouldBe("My First Line\r\n".Length);
        m.GetValueLengthInRange(new TextRange(1, 1, 1000, 1000), EndOfLinePreference.TextDefined).ShouldBe("My First Line\nMy Second Line\nMy Third Line".Length);
        m.GetValueLengthInRange(new TextRange(1, 1, 1000, 1000), EndOfLinePreference.LF).ShouldBe("My First Line\nMy Second Line\nMy Third Line".Length);
        m.GetValueLengthInRange(new TextRange(1, 1, 1000, 1000), EndOfLinePreference.CRLF).ShouldBe("My First Line\r\nMy Second Line\r\nMy Third Line".Length);
        m.Dispose();
    }

    [Fact]
    public void Guess_Indentation_1()
    {
        AssertGuess(null, null, [
            "x",
            "x",
            "x",
            "x",
            "x",
            "x",
            "x"
        ], "no clues");

        AssertGuess(false, null, [
            "\tx",
            "x",
            "x",
            "x",
            "x",
            "x",
            "x"
        ], "no spaces, 1xTAB");

        AssertGuess(true, 2, [
            "  x",
            "x",
            "x",
            "x",
            "x",
            "x",
            "x"
        ], "1x2");

        AssertGuess(false, null, [
            "\tx",
            "\tx",
            "\tx",
            "\tx",
            "\tx",
            "\tx",
            "\tx"
        ], "7xTAB");

        AssertGuessTabSizeOnlyWithSpaces(null, 2, [
            "\tx",
            "  x",
            "\tx",
            "  x",
            "\tx",
            "  x",
            "\tx",
            "  x",
        ], "4x2, 4xTAB");
        AssertGuess(false, null, [
            "\tx",
            " x",
            "\tx",
            " x",
            "\tx",
            " x",
            "\tx",
            " x"
        ], "4x1, 4xTAB");
        AssertGuess(false, null, [
            "\tx",
            "\tx",
            "  x",
            "\tx",
            "  x",
            "\tx",
            "  x",
            "\tx",
            "  x",
        ], "4x2, 5xTAB");
        AssertGuess(false, null, [
            "\tx",
            "\tx",
            "x",
            "\tx",
            "x",
            "\tx",
            "x",
            "\tx",
            "  x",
        ], "1x2, 5xTAB");
        AssertGuess(false, null, [
            "\tx",
            "\tx",
            "x",
            "\tx",
            "x",
            "\tx",
            "x",
            "\tx",
            "    x",
        ], "1x4, 5xTAB");
        AssertGuess(false, null, [
            "\tx",
            "\tx",
            "x",
            "\tx",
            "x",
            "\tx",
            "  x",
            "\tx",
            "    x",
        ], "1x2, 1x4, 5xTAB");

        AssertGuess(null, null, [
            "x",
            " x",
            " x",
            " x",
            " x",
            " x",
            " x",
            " x"
        ], "7x1 - 1 space is never guessed as an indentation");
        AssertGuess(true, null, [
            "x",
            "          x",
            " x",
            " x",
            " x",
            " x",
            " x",
            " x"
        ], "1x10, 6x1");
        AssertGuess(null, null, [
            "",
            "  ",
            "    ",
            "      ",
            "        ",
            "          ",
            "            ",
            "              ",
        ], "whitespace lines don't count");
        AssertGuess(true, 3, [
            "x",
            "   x",
            "   x",
            "    x",
            "x",
            "   x",
            "   x",
            "    x",
            "x",
            "   x",
            "   x",
            "    x",
        ], "6x3, 3x4");
        AssertGuess(true, 5, [
            "x",
            "     x",
            "     x",
            "    x",
            "x",
            "     x",
            "     x",
            "    x",
            "x",
            "     x",
            "     x",
            "    x",
        ], "6x5, 3x4");
        AssertGuess(true, 7, [
            "x",
            "       x",
            "       x",
            "     x",
            "x",
            "       x",
            "       x",
            "    x",
            "x",
            "       x",
            "       x",
            "    x",
        ], "6x7, 1x5, 2x4");
        AssertGuess(true, 2, [
            "x",
            "  x",
            "  x",
            "  x",
            "  x",
            "x",
            "  x",
            "  x",
            "  x",
            "  x",
        ], "8x2");

        AssertGuess(true, 2, [
            "x",
            "  x",
            "  x",
            "x",
            "  x",
            "  x",
            "x",
            "  x",
            "  x",
            "x",
            "  x",
            "  x",
        ], "8x2");
        AssertGuess(true, 2, [
            "x",
            "  x",
            "    x",
            "x",
            "  x",
            "    x",
            "x",
            "  x",
            "    x",
            "x",
            "  x",
            "    x",
        ], "4x2, 4x4");
        AssertGuess(true, 2, [
            "x",
            "  x",
            "  x",
            "    x",
            "x",
            "  x",
            "  x",
            "    x",
            "x",
            "  x",
            "  x",
            "    x",
        ], "6x2, 3x4");
        AssertGuess(true, 2, [
            "x",
            "  x",
            "  x",
            "    x",
            "    x",
            "x",
            "  x",
            "  x",
            "    x",
            "    x",
        ], "4x2, 4x4");
        AssertGuess(true, 2, [
            "x",
            "  x",
            "    x",
            "    x",
            "x",
            "  x",
            "    x",
            "    x",
        ], "2x2, 4x4");
        AssertGuess(true, 4, [
            "x",
            "    x",
            "    x",
            "x",
            "    x",
            "    x",
            "x",
            "    x",
            "    x",
            "x",
            "    x",
            "    x",
        ], "8x4");
        AssertGuess(true, 2, [
            "x",
            "  x",
            "    x",
            "    x",
            "      x",
            "x",
            "  x",
            "    x",
            "    x",
            "      x",
        ], "2x2, 4x4, 2x6");
        AssertGuess(true, 2, [
            "x",
            "  x",
            "    x",
            "    x",
            "      x",
            "      x",
            "        x",
        ], "1x2, 2x4, 2x6, 1x8");
        AssertGuess(true, 4, [
            "x",
            "    x",
            "    x",
            "    x",
            "     x",
            "        x",
            "x",
            "    x",
            "    x",
            "    x",
            "     x",
            "        x",
        ], "6x4, 2x5, 2x8");
        AssertGuess(true, 4, [
            "x",
            "    x",
            "    x",
            "    x",
            "     x",
            "        x",
            "        x",
        ], "3x4, 1x5, 2x8");
        AssertGuess(true, 4, [
            "x",
            "x",
            "    x",
            "    x",
            "     x",
            "        x",
            "        x",
            "x",
            "x",
            "    x",
            "    x",
            "     x",
            "        x",
            "        x",
        ], "6x4, 2x5, 4x8");
        AssertGuess(true, 3, [
            "x",
            " x",
            " x",
            " x",
            " x",
            " x",
            "x",
            "   x",
            "    x",
            "    x",
        ], "5x1, 2x0, 1x3, 2x4");
        AssertGuess(false, null, [
            "\t x",
            " \t x",
            "\tx"
        ], "mixed whitespace 1");
        AssertGuess(false, null, [
            "\tx",
            "\t    x"
        ], "mixed whitespace 2");
    }

    [Fact]
    public void Issue_44991_Wrong_Indentation_Size_Auto_Detection()
    {
        AssertGuess(true, 4, [
            "a = 10             # 0 space indent",
            "b = 5              # 0 space indent",
            "if a > 10:         # 0 space indent",
            "    a += 1         # 4 space indent      delta 4 spaces",
            "    if b > 5:      # 4 space indent",
            "        b += 1     # 8 space indent      delta 4 spaces",
            "        b += 1     # 8 space indent",
            "        b += 1     # 8 space indent",
            "# comment line 1   # 0 space indent      delta 8 spaces",
            "# comment line 2   # 0 space indent",
            "# comment line 3   # 0 space indent",
            "        b += 1     # 8 space indent      delta 8 spaces",
            "        b += 1     # 8 space indent",
            "        b += 1     # 8 space indent",
        ]);
    }

    [Fact]
    public void Issue_55818_Broken_Indentation_Detection()
    {
        AssertGuess(true, 2, [
            "",
            "/* REQUIRE */",
            "",
            "const foo = require ( 'foo' ),",
            "      bar = require ( 'bar' );",
            "",
            "/* MY FN */",
            "",
            "function myFn () {",
            "",
            "  const asd = 1,",
            "        dsa = 2;",
            "",
            "  return bar ( foo ( asd ) );",
            "",
            "}",
            "",
            "/* EXPORT */",
            "",
            "module.exports = myFn;",
            "",
        ]);
    }

    [Fact]
    public void Issue_70832_Broken_Indentation_Detection()
    {
        AssertGuess(false, null, [
            "x",
            "x",
            "x",
            "x",
            "\tx",
            "\t\tx",
            "    x",
            "\t\tx",
            "\tx",
            "\t\tx",
            "\tx",
            "\tx",
            "\tx",
            "\tx",
            "x",
        ]);
    }

    [Fact]
    public void Issue_62143_Broken_Indentation_Detection()
    {
        // works before the fix
        AssertGuess(true, 2, [
            "x",
            "x",
            "  x",
            "  x"
        ]);

        // works before the fix
        AssertGuess(true, 2, [
            "x",
            "  - item2",
            "  - item3"
        ]);

        // works before the fix
        TestGuessIndentation(true, 2, true, 2, [
            "x x",
            "  x",
            "  x",
        ]);

        // fails before the fix
        // empty space inline breaks the indentation guess
        TestGuessIndentation(true, 2, true, 2, [
            "x x",
            "  x",
            "  x",
            "    x"
        ]);

        TestGuessIndentation(true, 2, true, 2, [
            "<!--test1.md -->",
            "- item1",
            "  - item2",
            "    - item3"
        ]);
    }

    [Fact]
    public void Issue_84217_Broken_Indentation_Detection()
    {
        AssertGuess(true, 4, [
            "def main():",
            "    print('hello')",
        ]);
        AssertGuess(true, 4, [
            "def main():",
            "    with open('foo') as fp:",
            "        print(fp.read())",
        ]);
    }

    [Fact]
    public void Issue_65668_YAML_File_Indented_With_2_Spaces()
    {
        // Full YAML file from the issue - should detect as 2 spaces
        AssertGuess(true, 2, [
            "version: 2",
            "",
            "jobs:",
            "  build:",
            "    docker:",
            "      - circleci/golang:1.11",
            "",
            "  environment:",
            "    TEST_RESULTS: /tmp/test-results",
            "",
            "  steps:",
            "    - checkout",
            "    - run: mkdir -p $TEST_RESULTS",
            "",
            "    - restore_cache:",
            "        keys:",
            "          - v1-pkg-cache",
            "",
            "    - run:",
            "        name: dep ensure",
            "        command: dep ensure -v",
            "",
            "    - run:",
            "        name: Run unit tests",
            "        command: |",
            "          trap \"go-junit-report <${TEST_RESULTS}/go-test.out > ${TEST_RESULTS}/go-test-report.xml\" EXIT",
            "          go test -v ./... | tee ${TEST_RESULTS}/go-test.out",
            "",
            "    - run:",
            "        name: Build",
            "        command: go build -v",
            "",
            "    - save_cache:",
            "        key: v1-pkg-cache",
            "        paths:",
            "          - \"/go/pkg\"",
            "",
            "    - store_artifacts:",
            "        path: /tmp/test-results",
            "        destination: raw-test-output",
            "",
            "    - store_test_results:",
            "        path: /tmp/test-results",
        ]);
    }

    [Fact]
    public void Issue_249040_4_Space_Indent_Should_Win_Over_2_Space_When_Predominant()
    {
        // File with mostly 4-space indents but some 2-space indents should detect as 4 spaces
        AssertGuess(true, 4, [
            "function foo() {",
            "    let a = 1;",
            "    let b = 2;",
            "    if (true) {",
            "        console.log(a);",
            "        console.log(b);",
            "    }",
            "    const obj = {",
            "      x: 1,",  // 2-space indent here
            "      y: 2",   // 2-space indent here
            "    };",
            "    return obj;",
            "}",
        ]);
    }

    [Fact]
    public void ValidatePosition()
    {
        var m = TestTextModel.Create("line one\nline two");

        m.ValidatePosition(new TextPosition(0, 0)).ShouldBe(new TextPosition(1, 1));
        m.ValidatePosition(new TextPosition(0, 1)).ShouldBe(new TextPosition(1, 1));

        m.ValidatePosition(new TextPosition(1, 1)).ShouldBe(new TextPosition(1, 1));
        m.ValidatePosition(new TextPosition(1, 2)).ShouldBe(new TextPosition(1, 2));
        m.ValidatePosition(new TextPosition(1, 30)).ShouldBe(new TextPosition(1, 9));

        m.ValidatePosition(new TextPosition(2, 0)).ShouldBe(new TextPosition(2, 1));
        m.ValidatePosition(new TextPosition(2, 1)).ShouldBe(new TextPosition(2, 1));
        m.ValidatePosition(new TextPosition(2, 2)).ShouldBe(new TextPosition(2, 2));
        m.ValidatePosition(new TextPosition(2, 30)).ShouldBe(new TextPosition(2, 9));

        m.ValidatePosition(new TextPosition(3, 0)).ShouldBe(new TextPosition(2, 9));
        m.ValidatePosition(new TextPosition(3, 1)).ShouldBe(new TextPosition(2, 9));
        m.ValidatePosition(new TextPosition(3, 30)).ShouldBe(new TextPosition(2, 9));

        m.ValidatePosition(new TextPosition(30, 30)).ShouldBe(new TextPosition(2, 9));

        // Upstream: (-123.123, -0.5), (Number.MIN_VALUE, Number.MIN_VALUE), floored.
        m.ValidatePosition(new TextPosition(-124, -1)).ShouldBe(new TextPosition(1, 1));
        m.ValidatePosition(new TextPosition(0, 0)).ShouldBe(new TextPosition(1, 1));

        // Upstream: (Number.MAX_VALUE, Number.MAX_VALUE), (123.23, 47.5), floored.
        m.ValidatePosition(new TextPosition(int.MaxValue, int.MaxValue)).ShouldBe(new TextPosition(2, 9));
        m.ValidatePosition(new TextPosition(123, 47)).ShouldBe(new TextPosition(2, 9));

        m.Dispose();
    }

    [Fact]
    public void ValidatePosition_Around_High_Low_Surrogate_Pairs_1()
    {
        var m = TestTextModel.Create("a📚b");

        m.ValidatePosition(new TextPosition(0, 0)).ShouldBe(new TextPosition(1, 1));
        m.ValidatePosition(new TextPosition(0, 1)).ShouldBe(new TextPosition(1, 1));
        m.ValidatePosition(new TextPosition(0, 7)).ShouldBe(new TextPosition(1, 1));

        m.ValidatePosition(new TextPosition(1, 1)).ShouldBe(new TextPosition(1, 1));
        m.ValidatePosition(new TextPosition(1, 2)).ShouldBe(new TextPosition(1, 2));
        m.ValidatePosition(new TextPosition(1, 3)).ShouldBe(new TextPosition(1, 2));
        m.ValidatePosition(new TextPosition(1, 4)).ShouldBe(new TextPosition(1, 4));
        m.ValidatePosition(new TextPosition(1, 5)).ShouldBe(new TextPosition(1, 5));
        m.ValidatePosition(new TextPosition(1, 30)).ShouldBe(new TextPosition(1, 5));

        m.ValidatePosition(new TextPosition(2, 0)).ShouldBe(new TextPosition(1, 5));
        m.ValidatePosition(new TextPosition(2, 1)).ShouldBe(new TextPosition(1, 5));
        m.ValidatePosition(new TextPosition(2, 2)).ShouldBe(new TextPosition(1, 5));
        m.ValidatePosition(new TextPosition(2, 30)).ShouldBe(new TextPosition(1, 5));

        // Upstream: (-123.123, -0.5), (Number.MIN_VALUE, Number.MIN_VALUE), floored.
        m.ValidatePosition(new TextPosition(-124, -1)).ShouldBe(new TextPosition(1, 1));
        m.ValidatePosition(new TextPosition(0, 0)).ShouldBe(new TextPosition(1, 1));

        // Upstream: (Number.MAX_VALUE, Number.MAX_VALUE), (123.23, 47.5), floored.
        m.ValidatePosition(new TextPosition(int.MaxValue, int.MaxValue)).ShouldBe(new TextPosition(1, 5));
        m.ValidatePosition(new TextPosition(123, 47)).ShouldBe(new TextPosition(1, 5));

        m.Dispose();
    }

    [Fact]
    public void ValidatePosition_Around_High_Low_Surrogate_Pairs_2()
    {
        var m = TestTextModel.Create("a📚📚b");

        m.ValidatePosition(new TextPosition(1, 1)).ShouldBe(new TextPosition(1, 1));
        m.ValidatePosition(new TextPosition(1, 2)).ShouldBe(new TextPosition(1, 2));
        m.ValidatePosition(new TextPosition(1, 3)).ShouldBe(new TextPosition(1, 2));
        m.ValidatePosition(new TextPosition(1, 4)).ShouldBe(new TextPosition(1, 4));
        m.ValidatePosition(new TextPosition(1, 5)).ShouldBe(new TextPosition(1, 4));
        m.ValidatePosition(new TextPosition(1, 6)).ShouldBe(new TextPosition(1, 6));
        m.ValidatePosition(new TextPosition(1, 7)).ShouldBe(new TextPosition(1, 7));

        m.Dispose();
    }

    [Fact]
    public void Issue_71480_ValidatePosition_Handle_Floats()
    {
        var m = TestTextModel.Create("line one\nline two");

        // Upstream: (0.2, 1), (1.2, 1), (1.5, 2), (1.8, 3), (1, 0.3), (2, 0.8), (1, 1.2), (2, 1.5), floored.
        m.ValidatePosition(new TextPosition(0, 1)).ShouldBe(new TextPosition(1, 1), "a");
        m.ValidatePosition(new TextPosition(1, 1)).ShouldBe(new TextPosition(1, 1), "b");
        m.ValidatePosition(new TextPosition(1, 2)).ShouldBe(new TextPosition(1, 2), "c");
        m.ValidatePosition(new TextPosition(1, 3)).ShouldBe(new TextPosition(1, 3), "d");
        m.ValidatePosition(new TextPosition(1, 0)).ShouldBe(new TextPosition(1, 1), "e");
        m.ValidatePosition(new TextPosition(2, 0)).ShouldBe(new TextPosition(2, 1), "f");
        m.ValidatePosition(new TextPosition(1, 1)).ShouldBe(new TextPosition(1, 1), "g");
        m.ValidatePosition(new TextPosition(2, 1)).ShouldBe(new TextPosition(2, 1), "h");

        m.Dispose();
    }

    [Fact]
    public void Issue_71480_ValidateRange_Handle_Floats()
    {
        var m = TestTextModel.Create("line one\nline two");

        // Upstream: (0.2, 1.5, 0.8, 2.5), (1.2, 1.7, 1.8, 2.2), floored.
        m.ValidateRange(new TextRange(0, 1, 0, 2)).ShouldBe(new TextRange(1, 1, 1, 1));
        m.ValidateRange(new TextRange(1, 1, 1, 2)).ShouldBe(new TextRange(1, 1, 1, 2));

        m.Dispose();
    }

    [Fact]
    public void ValidateRange_Around_High_Low_Surrogate_Pairs_1()
    {
        var m = TestTextModel.Create("a📚b");

        m.ValidateRange(new TextRange(0, 0, 0, 1)).ShouldBe(new TextRange(1, 1, 1, 1));
        m.ValidateRange(new TextRange(0, 0, 0, 7)).ShouldBe(new TextRange(1, 1, 1, 1));

        m.ValidateRange(new TextRange(1, 1, 1, 1)).ShouldBe(new TextRange(1, 1, 1, 1));
        m.ValidateRange(new TextRange(1, 1, 1, 2)).ShouldBe(new TextRange(1, 1, 1, 2));
        m.ValidateRange(new TextRange(1, 1, 1, 3)).ShouldBe(new TextRange(1, 1, 1, 4));
        m.ValidateRange(new TextRange(1, 1, 1, 4)).ShouldBe(new TextRange(1, 1, 1, 4));
        m.ValidateRange(new TextRange(1, 1, 1, 5)).ShouldBe(new TextRange(1, 1, 1, 5));

        m.ValidateRange(new TextRange(1, 2, 1, 2)).ShouldBe(new TextRange(1, 2, 1, 2));
        m.ValidateRange(new TextRange(1, 2, 1, 3)).ShouldBe(new TextRange(1, 2, 1, 4));
        m.ValidateRange(new TextRange(1, 2, 1, 4)).ShouldBe(new TextRange(1, 2, 1, 4));
        m.ValidateRange(new TextRange(1, 2, 1, 5)).ShouldBe(new TextRange(1, 2, 1, 5));

        m.ValidateRange(new TextRange(1, 3, 1, 3)).ShouldBe(new TextRange(1, 2, 1, 2));
        m.ValidateRange(new TextRange(1, 3, 1, 4)).ShouldBe(new TextRange(1, 2, 1, 4));
        m.ValidateRange(new TextRange(1, 3, 1, 5)).ShouldBe(new TextRange(1, 2, 1, 5));

        m.ValidateRange(new TextRange(1, 4, 1, 4)).ShouldBe(new TextRange(1, 4, 1, 4));
        m.ValidateRange(new TextRange(1, 4, 1, 5)).ShouldBe(new TextRange(1, 4, 1, 5));

        m.ValidateRange(new TextRange(1, 5, 1, 5)).ShouldBe(new TextRange(1, 5, 1, 5));

        m.Dispose();
    }

    [Fact]
    public void ValidateRange_Around_High_Low_Surrogate_Pairs_2()
    {
        var m = TestTextModel.Create("a📚📚b");

        m.ValidateRange(new TextRange(0, 0, 0, 1)).ShouldBe(new TextRange(1, 1, 1, 1));
        m.ValidateRange(new TextRange(0, 0, 0, 7)).ShouldBe(new TextRange(1, 1, 1, 1));

        m.ValidateRange(new TextRange(1, 1, 1, 1)).ShouldBe(new TextRange(1, 1, 1, 1));
        m.ValidateRange(new TextRange(1, 1, 1, 2)).ShouldBe(new TextRange(1, 1, 1, 2));
        m.ValidateRange(new TextRange(1, 1, 1, 3)).ShouldBe(new TextRange(1, 1, 1, 4));
        m.ValidateRange(new TextRange(1, 1, 1, 4)).ShouldBe(new TextRange(1, 1, 1, 4));
        m.ValidateRange(new TextRange(1, 1, 1, 5)).ShouldBe(new TextRange(1, 1, 1, 6));
        m.ValidateRange(new TextRange(1, 1, 1, 6)).ShouldBe(new TextRange(1, 1, 1, 6));
        m.ValidateRange(new TextRange(1, 1, 1, 7)).ShouldBe(new TextRange(1, 1, 1, 7));

        m.ValidateRange(new TextRange(1, 2, 1, 2)).ShouldBe(new TextRange(1, 2, 1, 2));
        m.ValidateRange(new TextRange(1, 2, 1, 3)).ShouldBe(new TextRange(1, 2, 1, 4));
        m.ValidateRange(new TextRange(1, 2, 1, 4)).ShouldBe(new TextRange(1, 2, 1, 4));
        m.ValidateRange(new TextRange(1, 2, 1, 5)).ShouldBe(new TextRange(1, 2, 1, 6));
        m.ValidateRange(new TextRange(1, 2, 1, 6)).ShouldBe(new TextRange(1, 2, 1, 6));
        m.ValidateRange(new TextRange(1, 2, 1, 7)).ShouldBe(new TextRange(1, 2, 1, 7));

        m.ValidateRange(new TextRange(1, 3, 1, 3)).ShouldBe(new TextRange(1, 2, 1, 2));
        m.ValidateRange(new TextRange(1, 3, 1, 4)).ShouldBe(new TextRange(1, 2, 1, 4));
        m.ValidateRange(new TextRange(1, 3, 1, 5)).ShouldBe(new TextRange(1, 2, 1, 6));
        m.ValidateRange(new TextRange(1, 3, 1, 6)).ShouldBe(new TextRange(1, 2, 1, 6));
        m.ValidateRange(new TextRange(1, 3, 1, 7)).ShouldBe(new TextRange(1, 2, 1, 7));

        m.ValidateRange(new TextRange(1, 4, 1, 4)).ShouldBe(new TextRange(1, 4, 1, 4));
        m.ValidateRange(new TextRange(1, 4, 1, 5)).ShouldBe(new TextRange(1, 4, 1, 6));
        m.ValidateRange(new TextRange(1, 4, 1, 6)).ShouldBe(new TextRange(1, 4, 1, 6));
        m.ValidateRange(new TextRange(1, 4, 1, 7)).ShouldBe(new TextRange(1, 4, 1, 7));

        m.ValidateRange(new TextRange(1, 5, 1, 5)).ShouldBe(new TextRange(1, 4, 1, 4));
        m.ValidateRange(new TextRange(1, 5, 1, 6)).ShouldBe(new TextRange(1, 4, 1, 6));
        m.ValidateRange(new TextRange(1, 5, 1, 7)).ShouldBe(new TextRange(1, 4, 1, 7));

        m.ValidateRange(new TextRange(1, 6, 1, 6)).ShouldBe(new TextRange(1, 6, 1, 6));
        m.ValidateRange(new TextRange(1, 6, 1, 7)).ShouldBe(new TextRange(1, 6, 1, 7));

        m.ValidateRange(new TextRange(1, 7, 1, 7)).ShouldBe(new TextRange(1, 7, 1, 7));

        m.Dispose();
    }

    [Fact]
    public void ModifyPosition()
    {
        var m = TestTextModel.Create("line one\nline two");
        m.ModifyPosition(new TextPosition(1, 1), 0).ShouldBe(new TextPosition(1, 1));
        m.ModifyPosition(new TextPosition(0, 0), 0).ShouldBe(new TextPosition(1, 1));
        m.ModifyPosition(new TextPosition(30, 1), 0).ShouldBe(new TextPosition(2, 9));

        m.ModifyPosition(new TextPosition(1, 1), 17).ShouldBe(new TextPosition(2, 9));
        m.ModifyPosition(new TextPosition(1, 1), 1).ShouldBe(new TextPosition(1, 2));
        m.ModifyPosition(new TextPosition(1, 1), 3).ShouldBe(new TextPosition(1, 4));
        m.ModifyPosition(new TextPosition(1, 2), 10).ShouldBe(new TextPosition(2, 3));
        m.ModifyPosition(new TextPosition(1, 5), 13).ShouldBe(new TextPosition(2, 9));
        m.ModifyPosition(new TextPosition(1, 2), 16).ShouldBe(new TextPosition(2, 9));

        m.ModifyPosition(new TextPosition(2, 9), -17).ShouldBe(new TextPosition(1, 1));
        m.ModifyPosition(new TextPosition(1, 2), -1).ShouldBe(new TextPosition(1, 1));
        m.ModifyPosition(new TextPosition(1, 4), -3).ShouldBe(new TextPosition(1, 1));
        m.ModifyPosition(new TextPosition(2, 3), -10).ShouldBe(new TextPosition(1, 2));
        m.ModifyPosition(new TextPosition(2, 9), -13).ShouldBe(new TextPosition(1, 5));
        m.ModifyPosition(new TextPosition(2, 9), -16).ShouldBe(new TextPosition(1, 2));

        m.ModifyPosition(new TextPosition(1, 2), 17).ShouldBe(new TextPosition(2, 9));
        m.ModifyPosition(new TextPosition(1, 2), 100).ShouldBe(new TextPosition(2, 9));

        m.ModifyPosition(new TextPosition(1, 2), -2).ShouldBe(new TextPosition(1, 1));
        m.ModifyPosition(new TextPosition(1, 2), -100).ShouldBe(new TextPosition(1, 1));
        m.ModifyPosition(new TextPosition(2, 2), -100).ShouldBe(new TextPosition(1, 1));
        m.ModifyPosition(new TextPosition(2, 9), -18).ShouldBe(new TextPosition(1, 1));

        m.Dispose();
    }

    [Fact]
    public void NormalizeIndentation_1()
    {
        var model = TestTextModel.Create(
            "",
            null,
            TextModel.DefaultCreationOptions with
            {
                InsertSpaces = false,
            });

        model.NormalizeIndentation("\t").ShouldBe("\t");
        model.NormalizeIndentation("    ").ShouldBe("\t");
        model.NormalizeIndentation("   ").ShouldBe("   ");
        model.NormalizeIndentation("  ").ShouldBe("  ");
        model.NormalizeIndentation(" ").ShouldBe(" ");
        model.NormalizeIndentation("").ShouldBe("");
        model.NormalizeIndentation(" \t    ").ShouldBe("\t\t");
        model.NormalizeIndentation(" \t   ").ShouldBe("\t   ");
        model.NormalizeIndentation(" \t  ").ShouldBe("\t  ");
        model.NormalizeIndentation(" \t ").ShouldBe("\t ");
        model.NormalizeIndentation(" \t").ShouldBe("\t");

        model.NormalizeIndentation("\ta").ShouldBe("\ta");
        model.NormalizeIndentation("    a").ShouldBe("\ta");
        model.NormalizeIndentation("   a").ShouldBe("   a");
        model.NormalizeIndentation("  a").ShouldBe("  a");
        model.NormalizeIndentation(" a").ShouldBe(" a");
        model.NormalizeIndentation("a").ShouldBe("a");
        model.NormalizeIndentation(" \t    a").ShouldBe("\t\ta");
        model.NormalizeIndentation(" \t   a").ShouldBe("\t   a");
        model.NormalizeIndentation(" \t  a").ShouldBe("\t  a");
        model.NormalizeIndentation(" \t a").ShouldBe("\t a");
        model.NormalizeIndentation(" \ta").ShouldBe("\ta");

        model.Dispose();
    }

    [Fact]
    public void NormalizeIndentation_2()
    {
        var model = TestTextModel.Create("");

        model.NormalizeIndentation("\ta").ShouldBe("    a");
        model.NormalizeIndentation("    a").ShouldBe("    a");
        model.NormalizeIndentation("   a").ShouldBe("   a");
        model.NormalizeIndentation("  a").ShouldBe("  a");
        model.NormalizeIndentation(" a").ShouldBe(" a");
        model.NormalizeIndentation("a").ShouldBe("a");
        model.NormalizeIndentation(" \t    a").ShouldBe("        a");
        model.NormalizeIndentation(" \t   a").ShouldBe("       a");
        model.NormalizeIndentation(" \t  a").ShouldBe("      a");
        model.NormalizeIndentation(" \t a").ShouldBe("     a");
        model.NormalizeIndentation(" \ta").ShouldBe("    a");

        model.Dispose();
    }

    [Fact]
    public void GetLineFirstNonWhitespaceColumn()
    {
        var model = TestTextModel.Create(string.Join('\n',
        [
            "asd",
            " asd",
            "\tasd",
            "  asd",
            "\t\tasd",
            " ",
            "  ",
            "\t",
            "\t\t",
            "  \tasd",
            "",
            "",
        ]));

        model.GetLineFirstNonWhitespaceColumn(1).ShouldBe(1, "1");
        model.GetLineFirstNonWhitespaceColumn(2).ShouldBe(2, "2");
        model.GetLineFirstNonWhitespaceColumn(3).ShouldBe(2, "3");
        model.GetLineFirstNonWhitespaceColumn(4).ShouldBe(3, "4");
        model.GetLineFirstNonWhitespaceColumn(5).ShouldBe(3, "5");
        model.GetLineFirstNonWhitespaceColumn(6).ShouldBe(0, "6");
        model.GetLineFirstNonWhitespaceColumn(7).ShouldBe(0, "7");
        model.GetLineFirstNonWhitespaceColumn(8).ShouldBe(0, "8");
        model.GetLineFirstNonWhitespaceColumn(9).ShouldBe(0, "9");
        model.GetLineFirstNonWhitespaceColumn(10).ShouldBe(4, "10");
        model.GetLineFirstNonWhitespaceColumn(11).ShouldBe(0, "11");
        model.GetLineFirstNonWhitespaceColumn(12).ShouldBe(0, "12");

        model.Dispose();
    }

    [Fact]
    public void GetLineLastNonWhitespaceColumn()
    {
        var model = TestTextModel.Create(string.Join('\n',
        [
            "asd",
            "asd ",
            "asd\t",
            "asd  ",
            "asd\t\t",
            " ",
            "  ",
            "\t",
            "\t\t",
            "asd  \t",
            "",
            "",
        ]));

        model.GetLineLastNonWhitespaceColumn(1).ShouldBe(4, "1");
        model.GetLineLastNonWhitespaceColumn(2).ShouldBe(4, "2");
        model.GetLineLastNonWhitespaceColumn(3).ShouldBe(4, "3");
        model.GetLineLastNonWhitespaceColumn(4).ShouldBe(4, "4");
        model.GetLineLastNonWhitespaceColumn(5).ShouldBe(4, "5");
        model.GetLineLastNonWhitespaceColumn(6).ShouldBe(0, "6");
        model.GetLineLastNonWhitespaceColumn(7).ShouldBe(0, "7");
        model.GetLineLastNonWhitespaceColumn(8).ShouldBe(0, "8");
        model.GetLineLastNonWhitespaceColumn(9).ShouldBe(0, "9");
        model.GetLineLastNonWhitespaceColumn(10).ShouldBe(4, "10");
        model.GetLineLastNonWhitespaceColumn(11).ShouldBe(0, "11");
        model.GetLineLastNonWhitespaceColumn(12).ShouldBe(0, "12");

        model.Dispose();
    }

    [Fact]
    public void Issue_168836_Updating_TabSize_Should_Also_Update_IndentSize_When_IndentSize_Is_Set_To_TabSize()
    {
        var m = TestTextModel.Create(
            "some text",
            null,
            TextModel.DefaultCreationOptions with
            {
                TabSize = 2,
                IndentSize = null, // upstream: indentSize: 'tabSize'
            });
        m.GetOptions().TabSize.ShouldBe(2);
        m.GetOptions().IndentSize.ShouldBe(2);
        m.GetOptions().OriginalIndentSize.ShouldBeNull(); // upstream: 'tabSize'
        m.UpdateOptions(new TextModelUpdateOptions
        {
            TabSize = 4,
        });
        m.GetOptions().TabSize.ShouldBe(4);
        m.GetOptions().IndentSize.ShouldBe(4);
        m.GetOptions().OriginalIndentSize.ShouldBeNull(); // upstream: 'tabSize'
        m.Dispose();
    }
}
