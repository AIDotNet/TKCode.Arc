// Ported from vscode (src/vs/editor/test/common/viewModel/monospaceLineBreaksComputer.test.ts, commit b57d5c9e),
// MIT license. The injected texts of the tests are built with the Injected helper (upstream: new LineInjectedText(0, 1,
// column, { content, widthInEm }, order)); deepStrictEqual of object literals is one assertion per member.

using Shouldly;
using TKCode.Arc.Editor.Config;
using TKCode.Arc.Editor.Model;
using TKCode.Arc.Editor.ViewModel;
using Xunit;

namespace TKCode.Arc.Editor.Tests;

public sealed class MonospaceLineBreaksComputerTests
{
    private static (string Text, int[] Indices) ParseAnnotatedText(string annotatedText)
    {
        var text = new System.Text.StringBuilder();
        var currentLineIndex = 0;
        var indices = new List<int>();
        for (int i = 0, len = annotatedText.Length; i < len; i++)
        {
            if (annotatedText[i] == '|')
            {
                currentLineIndex++;
            }
            else
            {
                text.Append(annotatedText[i]);
                indices.Add(currentLineIndex);
            }
        }

        return (text.ToString(), indices.ToArray());
    }

    private static string ToAnnotatedText(string text, ModelLineProjectionData? lineBreakData)
    {
        // Insert line break markers again, according to algorithm
        var actualAnnotatedText = new System.Text.StringBuilder();
        if (lineBreakData is not null)
        {
            var previousLineIndex = 0;
            for (int i = 0, len = text.Length; i < len; i++)
            {
                var r = lineBreakData.TranslateToOutputPosition(i);
                if (previousLineIndex != r.OutputLineIndex)
                {
                    previousLineIndex = r.OutputLineIndex;
                    actualAnnotatedText.Append('|');
                }

                actualAnnotatedText.Append(text[i]);
            }
        }
        else
        {
            // No wrapping
            actualAnnotatedText.Append(text);
        }

        return actualAnnotatedText.ToString();
    }

    private sealed class Context(string text, IReadOnlyList<LineInjectedText>? injectedText) : ILineBreaksComputerContext
    {
        public string GetLineContent(int lineNumber) => text;

        public IReadOnlyList<LineInjectedText>? GetLineInjectedText(int lineNumber) => injectedText;
    }

    private static ModelLineProjectionData? GetLineBreakData(
        ILineBreaksComputerFactory factory,
        int tabSize,
        int breakAfter,
        double columnsForFullWidthChar,
        WrappingIndent wrappingIndent,
        WordBreak wordBreak,
        bool wrapOnEscapedLineFeeds,
        string text,
        ModelLineProjectionData? previousLineBreakData,
        IReadOnlyList<LineInjectedText>? injectedText = null,
        bool useTwoCellFullwidthCharacters = false)
    {
        var fontInfo = new FontInfo(
            pixelRatio: 1,
            fontFamily: "testFontFamily",
            fontWeight: "normal",
            fontSize: 14,
            fontFeatureSettings: "",
            fontVariationSettings: "",
            lineHeight: 19,
            letterSpacing: 0,
            isMonospace: true,
            typicalHalfwidthCharacterWidth: 7,
            typicalFullwidthCharacterWidth: 7 * columnsForFullWidthChar,
            canUseHalfwidthRightwardsArrow: true,
            spaceWidth: 7,
            middotWidth: 7,
            wsmiddotWidth: 7,
            maxDigitWidth: 7,
            isTrusted: false);
        var context = new Context(text, injectedText);
        var lineBreaksComputer = factory.CreateLineBreaksComputer(context, fontInfo, tabSize, breakAfter, wrappingIndent, wordBreak, wrapOnEscapedLineFeeds, useTwoCellFullwidthCharacters);
        var previousLineBreakDataClone = previousLineBreakData is not null
            ? new ModelLineProjectionData(null, null, [.. previousLineBreakData.BreakOffsets], [.. previousLineBreakData.BreakOffsetsVisibleColumn], previousLineBreakData.WrappedTextIndentLength)
            : null;
        lineBreaksComputer.AddRequest(1, previousLineBreakDataClone);
        return lineBreaksComputer.Finalize()[0];
    }

    private static ModelLineProjectionData? AssertLineBreaks(ILineBreaksComputerFactory factory, int tabSize, int breakAfter, string annotatedText, WrappingIndent wrappingIndent = WrappingIndent.None, WordBreak wordBreak = WordBreak.Normal)
    {
        // Create version of `annotatedText` with line break markers removed
        var text = ParseAnnotatedText(annotatedText).Text;
        var lineBreakData = GetLineBreakData(factory, tabSize, breakAfter, 2, wrappingIndent, wordBreak, false, text, null);
        var actualAnnotatedText = ToAnnotatedText(text, lineBreakData);

        actualAnnotatedText.ShouldBe(annotatedText);

        return lineBreakData;
    }

    /// <summary>Upstream's <c>new LineInjectedText(0, 1, column, { content, widthInEm }, order)</c>.</summary>
    private static LineInjectedText Injected(int column, string content, double widthInEm, int order = 0) =>
        new(0, 1, column, new ModelDecorationInjectedTextOptions(content) { WidthInEm = widthInEm }, order);

    private static void AssertBreaks(ModelLineProjectionData? lineBreakData, int[] breakOffsets, double[] breakOffsetsVisibleColumn)
    {
        lineBreakData.ShouldNotBeNull();
        lineBreakData.BreakOffsets.ShouldBe(breakOffsets);
        lineBreakData.BreakOffsetsVisibleColumn.ShouldBe(breakOffsetsVisibleColumn);
    }

    [Fact]
    public void MonospaceLineBreaksComputer()
    {
        var factory = new MonospaceLineBreaksComputerFactory("(", "\t).");

        // Empty string
        AssertLineBreaks(factory, 4, 5, "");

        // No wrapping if not necessary
        AssertLineBreaks(factory, 4, 5, "aaa");
        AssertLineBreaks(factory, 4, 5, "aaaaa");
        AssertLineBreaks(factory, 4, -1, "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa");

        // Acts like hard wrapping if no char found
        AssertLineBreaks(factory, 4, 5, "aaaaa|a");

        // Honors wrapping character
        AssertLineBreaks(factory, 4, 5, "aaaaa|.");
        AssertLineBreaks(factory, 4, 5, "aaaaa|a.|aaa.|aa");
        AssertLineBreaks(factory, 4, 5, "aaaaa|a..|aaa.|aa");
        AssertLineBreaks(factory, 4, 5, "aaaaa|a...|aaa.|aa");
        AssertLineBreaks(factory, 4, 5, "aaaaa|a....|aaa.|aa");

        // Honors tabs when computing wrapping position
        AssertLineBreaks(factory, 4, 5, "\t");
        AssertLineBreaks(factory, 4, 5, "\t|aaa");
        AssertLineBreaks(factory, 4, 5, "\t|a\t|aa");
        AssertLineBreaks(factory, 4, 5, "aa\ta");
        AssertLineBreaks(factory, 4, 5, "aa\t|aa");

        // Honors wrapping before characters (& gives it priority)
        AssertLineBreaks(factory, 4, 5, "aaa.|aa");
        AssertLineBreaks(factory, 4, 5, "aaa(.|aa");

        // Honors wrapping after characters (& gives it priority)
        AssertLineBreaks(factory, 4, 5, "aaa))|).aaa");
        AssertLineBreaks(factory, 4, 5, "aaa))|).|aaaa");
        AssertLineBreaks(factory, 4, 5, "aaa)|().|aaa");
        AssertLineBreaks(factory, 4, 5, "aaa|(().|aaa");
        AssertLineBreaks(factory, 4, 5, "aa.|(().|aaa");
        AssertLineBreaks(factory, 4, 5, "aa.|(.).|aaa");
    }


    [Fact]
    public void Accounts_For_Fixed_Injected_Text_Width_When_Wrapping()
    {
        var factory = new MonospaceLineBreaksComputerFactory("", "");
        var lineBreakData = GetLineBreakData(factory, 4, 5, 2, WrappingIndent.None, WordBreak.Normal, false, "abcdef", null, [
            Injected(5, "\u00a0", 1),
        ]);

        AssertBreaks(lineBreakData, [4, 7], [4, 7]);
    }

    [Fact]
    public void Treats_Multi_Character_Fixed_Width_Injected_Text_As_An_Atomic_Span()
    {
        var factory = new MonospaceLineBreaksComputerFactory("", "");
        var lineBreakData = GetLineBreakData(factory, 4, 5, 2, WrappingIndent.None, WordBreak.Normal, false, "abcdef", null, [
            Injected(4, "hello", 1),
        ]);

        AssertBreaks(lineBreakData, [8, 11], [8, 11]);
    }

    [Fact]
    public void Wraps_After_An_Escaped_Line_Feed_Followed_By_Fixed_Width_Injected_Text()
    {
        var factory = new MonospaceLineBreaksComputerFactory("", "");
        var lineBreakData = GetLineBreakData(factory, 4, 100, 2, WrappingIndent.None, WordBreak.Normal, true, "\"a\\nb\"", null, [
            Injected(5, "hint", 1),
        ]);

        lineBreakData.ShouldNotBeNull();
        lineBreakData.BreakOffsets.ShouldBe([4, 10]);
    }

    [Fact]
    public void Treats_Adjacent_Fixed_Width_Injected_Texts_As_Separate_Atomic_Spans()
    {
        var factory = new MonospaceLineBreaksComputerFactory("", "");
        var lineBreakData = GetLineBreakData(factory, 4, 5, 2, WrappingIndent.None, WordBreak.Normal, false, "abcdef", null, [
            Injected(4, "x", 0.5),
            Injected(4, "y", 1, 1),
        ]);

        AssertBreaks(lineBreakData, [4, 8], [4, 8]);
    }

    [Fact]
    public void Keeps_Fixed_Width_Injected_Text_Wider_Than_The_Wrap_Column_Atomic()
    {
        var factory = new MonospaceLineBreaksComputerFactory("", "");
        var lineBreakData = GetLineBreakData(factory, 4, 5, 2, WrappingIndent.None, WordBreak.Normal, false, "abcdef", null, [
            Injected(4, "x", 3),
        ]);

        AssertBreaks(lineBreakData, [3, 4, 7], [3, 4, 7]);
    }

    [Fact]
    public void Wraps_Before_A_Tab_That_No_Longer_Fits_After_Fixed_Width_Injected_Text()
    {
        var factory = new MonospaceLineBreaksComputerFactory("", "");
        var lineBreakData = GetLineBreakData(factory, 4, 4, 2, WrappingIndent.None, WordBreak.Normal, false, "ab\tcd", null, [
            Injected(3, "\u00a0", 0.75),
        ]);

        // `ab` plus a 0.75em wide injection fills 2.75 columns worth of pixels, so the tab (which
        // expands from character column 3 to character column 4) no longer fits and the line wraps
        // right before it.
        AssertBreaks(lineBreakData, [3, 6], [3, 6]);
    }

    [Fact]
    public void Expands_A_Tab_Following_Fixed_Width_Injected_Text_From_The_Character_Column()
    {
        var factory = new MonospaceLineBreaksComputerFactory("", "");
        var lineBreakData = GetLineBreakData(factory, 4, 10, 2, WrappingIndent.None, WordBreak.Normal, false, "ab\tcdefgh", null, [
            Injected(3, "\u00a0", 1.5),
        ]);

        // The injection counts as a single character column, so the tab at character column 3 expands
        // to character column 4 and the whole line spans 10 columns. Its 1.5em is spent on the wrapping
        // decision alone, which runs out of room at `g`. Were that width to leak into the column
        // accumulator, the injection would count as 3 columns, the tab would expand to character
        // column 8, and the line would already run out of room at `e`.
        AssertBreaks(lineBreakData, [8, 10], [8, 10]);
    }

    [Fact]
    public void Reserves_Width_For_A_Spacing_Only_Injection_At_The_Beginning_Of_A_Line()
    {
        var factory = new MonospaceLineBreaksComputerFactory("", "");
        var lineBreakData = GetLineBreakData(factory, 4, 6, 2, WrappingIndent.None, WordBreak.Normal, false, "abcdef", null, [
            Injected(1, "", 1),
        ]);

        // 1em is two columns wide here, so the spacer pushes the last two characters onto a second
        // line. It consumes no character, so the offsets still refer to the original text.
        AssertBreaks(lineBreakData, [4, 6], [4, 6]);
    }

    [Fact]
    public void Does_Not_Create_Empty_Lines_For_Oversized_Spacing_Only_Injected_Text()
    {
        var factory = new MonospaceLineBreaksComputerFactory("", "");
        var lineBreakData = GetLineBreakData(factory, 4, 2, 2, WrappingIndent.None, WordBreak.Normal, false, "abc", null, [
            Injected(1, "", 2),
        ]);

        // The spacer alone already overflows the wrap width, but there is no character before it to
        // wrap, so it must share the first line with `a` rather than emit an empty leading line.
        lineBreakData.ShouldNotBeNull();
        lineBreakData.BreakOffsets.ShouldBe([1, 3]);
    }

    [Fact]
    public void Lets_A_Spacing_Only_Injection_In_The_Middle_Of_A_Line_Inherit_Surrounding_Break_Opportunities()
    {
        var factory = new MonospaceLineBreaksComputerFactory("", " ");
        var lineBreakData = GetLineBreakData(factory, 4, 8, 2, WrappingIndent.None, WordBreak.Normal, false, "ab cdef", null, [
            Injected(4, "", 1),
        ]);

        // The spacer is transparent for break opportunities: it neither creates nor suppresses one, so
        // the break after the space is still recorded even though the spacer sits on top of it, and the
        // line breaks there rather than at `f` where it runs out of room. Its 1em is what makes the line
        // run out of room at all: without it the seven characters fit in the eight available columns.
        AssertBreaks(lineBreakData, [3, 7], [3, 7]);
    }

    [Fact]
    public void Ignores_A_Spacing_Only_Injection_At_The_End_Of_A_Line()
    {
        var factory = new MonospaceLineBreaksComputerFactory("", "");
        var lineBreakData = GetLineBreakData(factory, 4, 6, 2, WrappingIndent.None, WordBreak.Normal, false, "abcdef", null, [
            Injected(7, "", 1),
        ]);

        // The line is exactly full, and there is no character after the spacer that could be moved to a
        // following line, so the spacer cannot introduce a break.
        AssertBreaks(lineBreakData, [6], [6]);
    }

    [Fact]
    public void Expands_A_Tab_Following_A_Spacing_Only_Injection_From_The_Character_Column()
    {
        var factory = new MonospaceLineBreaksComputerFactory("", "");
        var lineBreakData = GetLineBreakData(factory, 4, 6, 2, WrappingIndent.None, WordBreak.Normal, false, "ab\tcd", null, [
            Injected(3, "", 1),
        ]);

        // The spacer occupies no character column, so the tab still expands from character column 2 to
        // character column 4. Were its 1em width to leak into the column accumulator, the tab would
        // start at character column 4 and expand to character column 8 instead.
        AssertBreaks(lineBreakData, [3, 5], [4, 6]);
    }

    [Fact]
    public void Does_Not_Use_Fixed_Width_Injected_Whitespace_As_Continuation_Indentation()
    {
        var factory = new MonospaceLineBreaksComputerFactory("", "");
        var lineBreakData = GetLineBreakData(factory, 4, 5, 2, WrappingIndent.Same, WordBreak.Normal, false, "abcdef", null, [
            Injected(1, " ", 0.75),
        ]);

        lineBreakData.ShouldNotBeNull();
        lineBreakData.BreakOffsets.ShouldBe([4, 7]);
        lineBreakData.WrappedTextIndentLength.ShouldBe(0);
    }

    private static void AssertLineBreakDataEqual(ModelLineProjectionData? a, ModelLineProjectionData? b)
    {
        if (a is null || b is null)
        {
            a.ShouldBe(b);
            return;
        }

        a.BreakOffsets.ShouldBe(b.BreakOffsets);
        a.WrappedTextIndentLength.ShouldBe(b.WrappedTextIndentLength);
        for (var i = 0; i < a.BreakOffsetsVisibleColumn.Length; i++)
        {
            var diff = Math.Abs(a.BreakOffsetsVisibleColumn[i] - b.BreakOffsetsVisibleColumn[i]);
            (diff < 0.001).ShouldBeTrue();
        }
    }

    private static void AssertIncrementalLineBreaks(ILineBreaksComputerFactory factory, string text, int tabSize, int breakAfter1, string annotatedText1, int breakAfter2, string annotatedText2, WrappingIndent wrappingIndent = WrappingIndent.None, double columnsForFullWidthChar = 2, bool useTwoCellFullwidthCharacters = false)
    {
        // sanity check the test
        ParseAnnotatedText(annotatedText1).Text.ShouldBe(text);
        ParseAnnotatedText(annotatedText2).Text.ShouldBe(text);

        // check that the direct mapping is ok for 1
        var directLineBreakData1 = GetLineBreakData(factory, tabSize, breakAfter1, columnsForFullWidthChar, wrappingIndent, WordBreak.Normal, false, text, null, null, useTwoCellFullwidthCharacters);
        ToAnnotatedText(text, directLineBreakData1).ShouldBe(annotatedText1);

        // check that the direct mapping is ok for 2
        var directLineBreakData2 = GetLineBreakData(factory, tabSize, breakAfter2, columnsForFullWidthChar, wrappingIndent, WordBreak.Normal, false, text, null, null, useTwoCellFullwidthCharacters);
        ToAnnotatedText(text, directLineBreakData2).ShouldBe(annotatedText2);

        // check that going from 1 to 2 is ok
        var lineBreakData2from1 = GetLineBreakData(factory, tabSize, breakAfter2, columnsForFullWidthChar, wrappingIndent, WordBreak.Normal, false, text, directLineBreakData1, null, useTwoCellFullwidthCharacters);
        ToAnnotatedText(text, lineBreakData2from1).ShouldBe(annotatedText2);
        AssertLineBreakDataEqual(lineBreakData2from1, directLineBreakData2);

        // check that going from 2 to 1 is ok
        var lineBreakData1from2 = GetLineBreakData(factory, tabSize, breakAfter1, columnsForFullWidthChar, wrappingIndent, WordBreak.Normal, false, text, directLineBreakData2, null, useTwoCellFullwidthCharacters);
        ToAnnotatedText(text, lineBreakData1from2).ShouldBe(annotatedText1);
        AssertLineBreakDataEqual(lineBreakData1from2, directLineBreakData1);
    }

    [Fact]
    public void MonospaceLineBreaksComputer_Incremental_1()
    {
        var factory = new MonospaceLineBreaksComputerFactory(WrappingOptions.WordWrapBreakBeforeCharacters, WrappingOptions.WordWrapBreakAfterCharacters);

        AssertIncrementalLineBreaks(
            factory, "just some text and more", 4,
            10, "just some |text and |more",
            15, "just some text |and more"
        );

        AssertIncrementalLineBreaks(
            factory, "Cu scripserit suscipiantur eos, in affert pericula contentiones sed, cetero sanctus et pro. Ius vidit magna regione te, sit ei elaboraret liberavisse. Mundi verear eu mea, eam vero scriptorem in, vix in menandri assueverit. Natum definiebas cu vim. Vim doming vocibus efficiantur id. In indoctum deseruisse voluptatum vim, ad debitis verterem sed.", 4,
            47, "Cu scripserit suscipiantur eos, in affert |pericula contentiones sed, cetero sanctus et |pro. Ius vidit magna regione te, sit ei |elaboraret liberavisse. Mundi verear eu mea, |eam vero scriptorem in, vix in menandri |assueverit. Natum definiebas cu vim. Vim |doming vocibus efficiantur id. In indoctum |deseruisse voluptatum vim, ad debitis verterem |sed.",
            142, "Cu scripserit suscipiantur eos, in affert pericula contentiones sed, cetero sanctus et pro. Ius vidit magna regione te, sit ei elaboraret |liberavisse. Mundi verear eu mea, eam vero scriptorem in, vix in menandri assueverit. Natum definiebas cu vim. Vim doming vocibus efficiantur |id. In indoctum deseruisse voluptatum vim, ad debitis verterem sed."
        );

        AssertIncrementalLineBreaks(
            factory, "An his legere persecuti, oblique delicata efficiantur ex vix, vel at graecis officiis maluisset. Et per impedit voluptua, usu discere maiorum at. Ut assum ornatus temporibus vis, an sea melius pericula. Ea dicunt oblique phaedrum nam, eu duo movet nobis. His melius facilis eu, vim malorum temporibus ne. Nec no sale regione, meliore civibus placerat id eam. Mea alii fabulas definitionem te, agam volutpat ad vis, et per bonorum nonumes repudiandae.", 4,
            57, "An his legere persecuti, oblique delicata efficiantur ex |vix, vel at graecis officiis maluisset. Et per impedit |voluptua, usu discere maiorum at. Ut assum ornatus |temporibus vis, an sea melius pericula. Ea dicunt |oblique phaedrum nam, eu duo movet nobis. His melius |facilis eu, vim malorum temporibus ne. Nec no sale |regione, meliore civibus placerat id eam. Mea alii |fabulas definitionem te, agam volutpat ad vis, et per |bonorum nonumes repudiandae.",
            58, "An his legere persecuti, oblique delicata efficiantur ex |vix, vel at graecis officiis maluisset. Et per impedit |voluptua, usu discere maiorum at. Ut assum ornatus |temporibus vis, an sea melius pericula. Ea dicunt oblique |phaedrum nam, eu duo movet nobis. His melius facilis eu, |vim malorum temporibus ne. Nec no sale regione, meliore |civibus placerat id eam. Mea alii fabulas definitionem |te, agam volutpat ad vis, et per bonorum nonumes |repudiandae."
        );

        AssertIncrementalLineBreaks(
            factory, "\t\t\"owner\": \"vscode\",", 4,
            14, "\t\t\"owner|\": |\"vscod|e\",",
            16, "\t\t\"owner\":| |\"vscode\"|,",
            WrappingIndent.Same
        );

        AssertIncrementalLineBreaks(
            factory, "🐇👬🌖🌞🏇🍼🐇👬🌖🌞🏇🍼🐇👬🌖🌞🏇🍼🐇👬🌖🌞🏇🍼🐇&👬🌖🌞🏇🍼🐇👬🌖🌞🏇🍼🐇👬🌖🌞🏇🍼🐇👬🌖🌞🏇🍼🐇👬", 4,
            51, "🐇👬🌖🌞🏇🍼🐇👬🌖🌞🏇🍼🐇👬🌖🌞🏇🍼🐇👬🌖🌞🏇🍼🐇&|👬🌖🌞🏇🍼🐇👬🌖🌞🏇🍼🐇👬🌖🌞🏇🍼🐇👬🌖🌞🏇🍼🐇👬",
            50, "🐇👬🌖🌞🏇🍼🐇👬🌖🌞🏇🍼🐇👬🌖🌞🏇🍼🐇👬🌖🌞🏇🍼🐇|&|👬🌖🌞🏇🍼🐇👬🌖🌞🏇🍼🐇👬🌖🌞🏇🍼🐇👬🌖🌞🏇🍼🐇👬",
            WrappingIndent.Same
        );

        AssertIncrementalLineBreaks(
            factory, "🐇👬&🌞🌖", 4,
            5, "🐇👬&|🌞🌖",
            4, "🐇👬|&|🌞🌖",
            WrappingIndent.Same
        );

        AssertIncrementalLineBreaks(
            factory, "\t\tfunc('🌞🏇🍼🌞🏇🍼🐇&👬🌖🌞👬🌖🌞🏇🍼🐇👬', WrappingIndent.Same);", 4,
            26, "\t\tfunc|('🌞🏇🍼🌞🏇🍼🐇&|👬🌖🌞👬🌖🌞🏇🍼🐇|👬', |WrappingIndent.|Same);",
            27, "\t\tfunc|('🌞🏇🍼🌞🏇🍼🐇&|👬🌖🌞👬🌖🌞🏇🍼🐇|👬', |WrappingIndent.|Same);",
            WrappingIndent.Same
        );

        AssertIncrementalLineBreaks(
            factory, "factory, \"xtxtfunc(x\"🌞🏇🍼🌞🏇🍼🐇&👬🌖🌞👬🌖🌞🏇🍼🐇👬x\"", 4,
            16, "factory, |\"xtxtfunc|(x\"🌞🏇🍼🌞🏇🍼|🐇&|👬🌖🌞👬🌖🌞🏇🍼|🐇👬x\"",
            17, "factory, |\"xtxtfunc|(x\"🌞🏇🍼🌞🏇🍼🐇|&👬🌖🌞👬🌖🌞🏇🍼|🐇👬x\"",
            WrappingIndent.Same
        );
    }

    [Fact]
    public void Issue_95686_CRITICAL_Loop_Forever_On_The_MonospaceLineBreaksComputer()
    {
        var factory = new MonospaceLineBreaksComputerFactory(WrappingOptions.WordWrapBreakBeforeCharacters, WrappingOptions.WordWrapBreakAfterCharacters);
        AssertIncrementalLineBreaks(
            factory,
            "\t\t\t\t\t\t<tr dmx-class:table-danger=\"(alt <= 50)\" dmx-class:table-warning=\"(alt <= 200)\" dmx-class:table-primary=\"(alt <= 400)\" dmx-class:table-info=\"(alt <= 800)\" dmx-class:table-success=\"(alt >= 400)\">",
            4,
            179, "\t\t\t\t\t\t<tr dmx-class:table-danger=\"(alt <= 50)\" dmx-class:table-warning=\"(alt <= 200)\" dmx-class:table-primary=\"(alt <= 400)\" dmx-class:table-info=\"(alt <= 800)\" |dmx-class:table-success=\"(alt >= 400)\">",
            1, "\t|\t|\t|\t|\t|\t|<|t|r| |d|m|x|-|c|l|a|s|s|:|t|a|b|l|e|-|d|a|n|g|e|r|=|\"|(|a|l|t| |<|=| |5|0|)|\"| |d|m|x|-|c|l|a|s|s|:|t|a|b|l|e|-|w|a|r|n|i|n|g|=|\"|(|a|l|t| |<|=| |2|0|0|)|\"| |d|m|x|-|c|l|a|s|s|:|t|a|b|l|e|-|p|r|i|m|a|r|y|=|\"|(|a|l|t| |<|=| |4|0|0|)|\"| |d|m|x|-|c|l|a|s|s|:|t|a|b|l|e|-|i|n|f|o|=|\"|(|a|l|t| |<|=| |8|0|0|)|\"| |d|m|x|-|c|l|a|s|s|:|t|a|b|l|e|-|s|u|c|c|e|s|s|=|\"|(|a|l|t| |>|=| |4|0|0|)|\"|>",
            WrappingIndent.Same
        );
    }

    [Fact]
    public void Issue_110392_Occasional_Crash_When_Resize_With_Panel_On_The_Right()
    {
        var factory = new MonospaceLineBreaksComputerFactory(WrappingOptions.WordWrapBreakBeforeCharacters, WrappingOptions.WordWrapBreakAfterCharacters);
        AssertIncrementalLineBreaks(
            factory,
            "你好 **hello** **hello** **hello-world** hey there!",
            4,
            15, "你好 **hello** |**hello** |**hello-world**| hey there!",
            1, "你|好| |*|*|h|e|l|l|o|*|*| |*|*|h|e|l|l|o|*|*| |*|*|h|e|l|l|o|-|w|o|r|l|d|*|*| |h|e|y| |t|h|e|r|e|!",
            WrappingIndent.Same,
            1.6605405405405405
        );
    }

    [Fact]
    public void MonospaceLineBreaksComputer_CJK_And_Kinsoku_Shori()
    {
        var factory = new MonospaceLineBreaksComputerFactory("(", "\t)");
        AssertLineBreaks(factory, 4, 5, "aa \u5b89|\u5b89");
        AssertLineBreaks(factory, 4, 5, "\u3042 \u5b89|\u5b89");
        AssertLineBreaks(factory, 4, 5, "\u3042\u3042|\u5b89\u5b89");
        AssertLineBreaks(factory, 4, 5, "aa |\u5b89)\u5b89|\u5b89");
        AssertLineBreaks(factory, 4, 5, "aa \u3042|\u5b89\u3042)|\u5b89");
        AssertLineBreaks(factory, 4, 5, "aa |(\u5b89aa|\u5b89");
    }


    [Fact]
    public void MonospaceLineBreaksComputer_Two_Cell_Full_Width_Character_Width()
    {
        var factory = new MonospaceLineBreaksComputerFactory("(", "\t)");
        var text = "\u3042\u3042\u3042\u3042";
        string WrapWith(double columnsForFullWidthChar, bool useTwoCellFullwidthCharacters) =>
            ToAnnotatedText(text, GetLineBreakData(factory, 4, 5, columnsForFullWidthChar, WrappingIndent.None, WordBreak.Normal, false, text, null, null, useTwoCellFullwidthCharacters));

        WrapWith(3, false).ShouldBe("\u3042|\u3042|\u3042|\u3042"); // lyingFont
        WrapWith(3, true).ShouldBe("\u3042\u3042|\u3042\u3042"); // lyingFontTwoCells
        WrapWith(2, false).ShouldBe("\u3042\u3042|\u3042\u3042"); // gridAlignedFont
    }

    [Fact]
    public void MonospaceLineBreaksComputer_Two_Cell_Width_Determines_Whether_Continuation_Indentation_Fits()
    {
        var factory = new MonospaceLineBreaksComputerFactory("(", "\t)");
        var text = "    \u3042\u3042\u3042";
        int? WrappedIndentWith(string lineText, int breakAfter, double columnsForFullWidthChar, bool useTwoCellFullwidthCharacters) =>
            GetLineBreakData(factory, 4, breakAfter, columnsForFullWidthChar, WrappingIndent.Same, WordBreak.Normal, false, lineText, null, null, useTwoCellFullwidthCharacters)?.WrappedTextIndentLength;

        WrappedIndentWith(text, 6, 3, false).ShouldBe(0); // wideNatural
        WrappedIndentWith(text, 6, 3, true).ShouldBe(4); // wideTwoCells
        WrappedIndentWith(text, 5, 1, false).ShouldBe(4); // narrowNatural
        WrappedIndentWith(text, 5, 1, true).ShouldBe(0); // narrowTwoCells
    }

    [Fact]
    public void MonospaceLineBreaksComputer_Two_Cell_Width_Uses_Shared_Full_Width_Classification()
    {
        var factory = new MonospaceLineBreaksComputerFactory("(", "\t)");
        string WrapWith(string text, bool useTwoCellFullwidthCharacters) =>
            ToAnnotatedText(text, GetLineBreakData(factory, 4, 5, 3, WrappingIndent.None, WordBreak.Normal, false, text, null, null, useTwoCellFullwidthCharacters));
        var fullwidthAscii = new string('\uFF21', 4);
        var vaiSyllables = new string('\uA500', 4);

        WrapWith(fullwidthAscii, false).ShouldBe("\uFF21|\uFF21|\uFF21|\uFF21");
        WrapWith(fullwidthAscii, true).ShouldBe("\uFF21\uFF21|\uFF21\uFF21");
        WrapWith(vaiSyllables, false).ShouldBe("\uA500|\uA500|\uA500|\uA500");
        WrapWith(vaiSyllables, true).ShouldBe("\uA500\uA500|\uA500\uA500");
    }

    [Fact]
    public void MonospaceLineBreaksComputer_Two_Cell_Width_Preserves_Natural_Control_Character_Metrics()
    {
        var factory = new MonospaceLineBreaksComputerFactory("(", "\t)");
        string WrapWith(string text, bool useTwoCellFullwidthCharacters) =>
            ToAnnotatedText(text, GetLineBreakData(factory, 4, 4, 3, WrappingIndent.None, WordBreak.Normal, false, text, null, null, useTwoCellFullwidthCharacters));
        var controlCharacters = "\u0001\u0001";
        var controlCharacterWithTab = "\u0001\t";

        WrapWith(controlCharacters, false).ShouldBe("\u0001|\u0001");
        WrapWith(controlCharacters, true).ShouldBe("\u0001|\u0001");
        GetLineBreakData(factory, 4, 3, 3, WrappingIndent.None, WordBreak.Normal, false, controlCharacterWithTab, null, null, false)?.BreakOffsetsVisibleColumn.ShouldBe([3, 4]);
        GetLineBreakData(factory, 4, 3, 3, WrappingIndent.None, WordBreak.Normal, false, controlCharacterWithTab, null, null, true)?.BreakOffsetsVisibleColumn.ShouldBe([3, 4]);
    }

    [Fact]
    public void MonospaceLineBreaksComputer_Two_Cell_Width_Treats_Combining_Marks_Independently()
    {
        var factory = new MonospaceLineBreaksComputerFactory("(", "\t)");
        string WrapWith(string text, bool useTwoCellFullwidthCharacters) =>
            ToAnnotatedText(text, GetLineBreakData(factory, 4, 4, 3, WrappingIndent.None, WordBreak.Normal, false, text, null, null, useTwoCellFullwidthCharacters));
        var combinedCharacter = "\u3042\u0301aa";

        WrapWith(combinedCharacter, false).ShouldBe("\u3042|\u0301aa");
        WrapWith(combinedCharacter, true).ShouldBe("\u3042|\u0301aa");
    }

    [Fact]
    public void MonospaceLineBreaksComputer_Two_Cell_Width_Supports_Incremental_Line_Breaks()
    {
        var factory = new MonospaceLineBreaksComputerFactory(WrappingOptions.WordWrapBreakBeforeCharacters, WrappingOptions.WordWrapBreakAfterCharacters);

        // A font that lies about its full-width ratio: each ideograph still occupies exactly two cells.
        AssertIncrementalLineBreaks(
            factory, "ああああああ", 4,
            5, "ああ|ああ|ああ",
            7, "あああ|あああ",
            WrappingIndent.None,
            3,
            true
        );

        AssertIncrementalLineBreaks(
            factory, "    ああああ", 4,
            8, "    ああ|ああ",
            10, "    あああ|あ",
            WrappingIndent.Same,
            3,
            true
        );

        AssertIncrementalLineBreaks(
            factory, "你好 **hello** **hello** **hello-world** hey there!", 4,
            15, "你好 **hello** |**hello** |**hello-world**| hey there!",
            1, "你|好| |*|*|h|e|l|l|o|*|*| |*|*|h|e|l|l|o|*|*| |*|*|h|e|l|l|o|-|w|o|r|l|d|*|*| |h|e|y| |t|h|e|r|e|!",
            WrappingIndent.Same,
            1.6605405405405405,
            true
        );
    }

    [Fact]
    public void MonospaceLineBreaksComputer_WrappingIndent_Same()
    {
        var factory = new MonospaceLineBreaksComputerFactory("", "\t ");
        AssertLineBreaks(factory, 4, 38, " *123456789012345678901234567890123456|7890", WrappingIndent.Same);
    }

    [Fact]
    public void Issue_16332_Scroll_Bar_Overlaying_On_Top_Of_Text()
    {
        var factory = new MonospaceLineBreaksComputerFactory("", "\t ");
        AssertLineBreaks(factory, 4, 24, "a/ very/long/line/of/tex|t/that/expands/beyon|d/your/typical/line/|of/code/", WrappingIndent.Indent);
    }

    [Fact]
    public void Issue_35162_WrappingIndent_Not_Consistently_Working()
    {
        var factory = new MonospaceLineBreaksComputerFactory("", "\t ");
        var mapper = AssertLineBreaks(factory, 4, 24, "                t h i s |i s |a l |o n |g l |i n |e", WrappingIndent.Indent);
        mapper!.WrappedTextIndentLength.ShouldBe("                    ".Length);
    }

    [Fact]
    public void Issue_75494_Surrogate_Pairs()
    {
        var factory = new MonospaceLineBreaksComputerFactory("\t", " ");
        AssertLineBreaks(factory, 4, 49, "🐇👬🌖🌞🏇🍼🐇👬🌖🌞🏇🍼🐇👬🌖🌞🏇🍼🐇👬🌖🌞🏇🍼|🐇👬🌖🌞🏇🍼🐇👬🌖🌞🏇🍼🐇👬🌖🌞🏇🍼🐇👬🌖🌞🏇🍼|🐇👬", WrappingIndent.Same);
    }

    [Fact]
    public void Issue_75494_Surrogate_Pairs_Overrun_1()
    {
        var factory = new MonospaceLineBreaksComputerFactory(WrappingOptions.WordWrapBreakBeforeCharacters, WrappingOptions.WordWrapBreakAfterCharacters);
        AssertLineBreaks(factory, 4, 4, "🐇👬|&|🌞🌖", WrappingIndent.Same);
    }

    [Fact]
    public void Issue_75494_Surrogate_Pairs_Overrun_2()
    {
        var factory = new MonospaceLineBreaksComputerFactory(WrappingOptions.WordWrapBreakBeforeCharacters, WrappingOptions.WordWrapBreakAfterCharacters);
        AssertLineBreaks(factory, 4, 17, "factory, |\"xtxtfunc|(x\"🌞🏇🍼🌞🏇🍼🐇|&👬🌖🌞👬🌖🌞🏇🍼|🐇👬x\"", WrappingIndent.Same);
    }

    [Fact]
    public void MonospaceLineBreaksComputer_WrappingIndent_DeepIndent()
    {
        var factory = new MonospaceLineBreaksComputerFactory("", "\t ");
        var mapper = AssertLineBreaks(factory, 4, 26, "        W e A r e T e s t |i n g D e |e p I n d |e n t a t |i o n", WrappingIndent.DeepIndent);
        mapper!.WrappedTextIndentLength.ShouldBe("                ".Length);
    }

    [Fact]
    public void Issue_33366_Word_Wrap_Algorithm_Behaves_Differently_Around_Punctuation()
    {
        var factory = new MonospaceLineBreaksComputerFactory(WrappingOptions.WordWrapBreakBeforeCharacters, WrappingOptions.WordWrapBreakAfterCharacters);
        AssertLineBreaks(factory, 4, 23, "this is a line of |text, text that sits |on a line", WrappingIndent.Same);
    }

    [Fact]
    public void Issue_152773_Word_Wrap_Algorithm_Behaves_Differently_With_Bracket_Followed_By_Comma()
    {
        var factory = new MonospaceLineBreaksComputerFactory(WrappingOptions.WordWrapBreakBeforeCharacters, WrappingOptions.WordWrapBreakAfterCharacters);
        AssertLineBreaks(factory, 4, 24, "this is a line of |(text), text that sits |on a line", WrappingIndent.Same);
    }

    [Fact]
    public void Issue_112382_Word_Wrap_Doesn_T_Work_Well_With_Control_Characters()
    {
        var factory = new MonospaceLineBreaksComputerFactory(WrappingOptions.WordWrapBreakBeforeCharacters, WrappingOptions.WordWrapBreakAfterCharacters);
        AssertLineBreaks(factory, 4, 6, "\u0006\u0006\u0006|\u0006\u0006\u0006", WrappingIndent.Same);
    }

    [Fact]
    public void Word_Break_Work_Well_With_Chinese_Japanese_Korean_CJK_Text_When_Setting_Normal()
    {
        var factory = new MonospaceLineBreaksComputerFactory(WrappingOptions.WordWrapBreakBeforeCharacters, WrappingOptions.WordWrapBreakAfterCharacters);
        AssertLineBreaks(factory, 4, 5, "你好|1111", WrappingIndent.Same, WordBreak.Normal);
    }

    [Fact]
    public void Word_Break_Work_Well_With_Chinese_Japanese_Korean_CJK_Text_When_Setting_KeepAll()
    {
        var factory = new MonospaceLineBreaksComputerFactory(WrappingOptions.WordWrapBreakBeforeCharacters, WrappingOptions.WordWrapBreakAfterCharacters);
        AssertLineBreaks(factory, 4, 8, "你好1111", WrappingIndent.Same, WordBreak.KeepAll);
    }


    [Fact]
    public void Issue_258022_WrapOnEscapedLineFeeds_Should_Work_Correctly_After_Editor_Resize()
    {
        var factory = new MonospaceLineBreaksComputerFactory(WrappingOptions.WordWrapBreakBeforeCharacters, WrappingOptions.WordWrapBreakAfterCharacters);

        // Test text with escaped line feeds - simulates a JSON string with \n
        // The \n should trigger a soft wrap when wrapOnEscapedLineFeeds is enabled
        var text = "\"Short text with\\nescaped newline and an escaped\\\\nbackslash\"";

        // First, compute line breaks with wrapOnEscapedLineFeeds enabled at initial width
        var initialBreakData = GetLineBreakData(factory, 4, 30, 2, WrappingIndent.None, WordBreak.Normal, true, text, null);
        var initialAnnotatedText = ToAnnotatedText(text, initialBreakData);

        // Verify the escaped \n triggers a wrap in the initial case
        initialAnnotatedText.ShouldContain("with\\n", Case.Sensitive, "Initial case should wrap at escaped line feeds");

        // Now simulate editor resize by computing line breaks with different width using previous data
        // This triggers createLineBreaksFromPreviousLineBreaks which has the bug
        var resizedBreakData = GetLineBreakData(factory, 4, 35, 2, WrappingIndent.None, WordBreak.Normal, true, text, initialBreakData);
        var resizedAnnotatedText = ToAnnotatedText(text, resizedBreakData);

        // Compute fresh line breaks at the new width (without using previous data)
        // This uses createLineBreaks which correctly handles wrapOnEscapedLineFeeds
        var freshBreakData = GetLineBreakData(factory, 4, 35, 2, WrappingIndent.None, WordBreak.Normal, true, text, null);
        var freshAnnotatedText = ToAnnotatedText(text, freshBreakData);

        // Fresh computation should still wrap at escaped line feeds
        freshAnnotatedText.ShouldContain("with\\n", Case.Sensitive, "Fresh computation should wrap at escaped line feeds");

        // BUG DEMONSTRATION: Incremental computation after resize doesn't handle escaped line feeds
        // The two results should be identical, but they're not due to the bug
        resizedAnnotatedText.ShouldBe(
            freshAnnotatedText,
            "Bug: Incremental and fresh computations differ for escaped line feeds.\n"
            + $"Incremental (resize): {resizedAnnotatedText}\n"
            + $"Fresh computation:   {freshAnnotatedText}\n"
            + "The incremental path (createLineBreaksFromPreviousLineBreaks) doesn't handle wrapOnEscapedLineFeeds");
    }
}
