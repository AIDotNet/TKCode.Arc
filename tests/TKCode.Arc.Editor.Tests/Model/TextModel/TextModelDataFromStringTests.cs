// Ported from vscode (src/vs/editor/test/common/model/textModel.test.ts, commit b57d5c9e), MIT license.
// This file: the 'TextModelData.fromString' suite. Upstream's createTextBuffer is the buffer factory's Create here, and
// the fields of the expected data are compared one by one.

using Shouldly;
using TKCode.Arc.Editor.Model;
using Xunit;

namespace TKCode.Arc.Editor.Tests;

public sealed class TextModelDataFromStringTests
{
    private sealed record TextBufferData(string EOL, string[] Lines, bool ContainsRTL, bool IsBasicASCII);

    private static void TestTextModelDataFromString(string text, TextBufferData expected)
    {
        var textBuffer = new PieceTreeTextBuffer(
            TextModel.CreateTextBufferFactory(text).Create(TextModel.DefaultCreationOptions.DefaultEOL));
        var actual = new TextBufferData(
            textBuffer.GetEOL(),
            textBuffer.GetLinesContent(),
            textBuffer.MightContainRTL(),
            !textBuffer.MightContainNonBasicASCII());

        actual.EOL.ShouldBe(expected.EOL);
        actual.Lines.ShouldBe(expected.Lines);
        actual.ContainsRTL.ShouldBe(expected.ContainsRTL);
        actual.IsBasicASCII.ShouldBe(expected.IsBasicASCII);
    }

    [Fact]
    public void One_Line_Text()
    {
        TestTextModelDataFromString("Hello world!",
            new TextBufferData(
                EOL: "\n",
                Lines:
                [
                    "Hello world!",
                ],
                ContainsRTL: false,
                IsBasicASCII: true));
    }

    [Fact]
    public void Multiline_Text()
    {
        TestTextModelDataFromString("Hello,\r\ndear friend\nHow\rare\r\nyou?",
            new TextBufferData(
                EOL: "\r\n",
                Lines:
                [
                    "Hello,",
                    "dear friend",
                    "How",
                    "are",
                    "you?",
                ],
                ContainsRTL: false,
                IsBasicASCII: true));
    }

    [Fact]
    public void Non_Basic_ASCII_1()
    {
        TestTextModelDataFromString("Hello,\nZürich",
            new TextBufferData(
                EOL: "\n",
                Lines:
                [
                    "Hello,",
                    "Zürich",
                ],
                ContainsRTL: false,
                IsBasicASCII: false));
    }

    [Fact]
    public void ContainsRTL_1()
    {
        TestTextModelDataFromString("Hello,\nזוהי עובדה מבוססת שדעתו",
            new TextBufferData(
                EOL: "\n",
                Lines:
                [
                    "Hello,",
                    "זוהי עובדה מבוססת שדעתו",
                ],
                ContainsRTL: true,
                IsBasicASCII: false));
    }

    [Fact]
    public void ContainsRTL_2()
    {
        TestTextModelDataFromString("Hello,\nهناك حقيقة مثبتة منذ زمن طويل",
            new TextBufferData(
                EOL: "\n",
                Lines:
                [
                    "Hello,",
                    "هناك حقيقة مثبتة منذ زمن طويل",
                ],
                ContainsRTL: true,
                IsBasicASCII: false));
    }
}
