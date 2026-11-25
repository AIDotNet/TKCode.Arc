// Tests for the builder/factory result, which upstream's pieceTreeTextBuffer.test.ts only exercises indirectly through
// createTextBuffer. Ported from vscode's pieceTreeTextBufferBuilder.ts (commit b57d5c9e), MIT licensed.

using TKCode.Arc.Editor.Model;

namespace TKCode.Arc.Editor.Tests;

public sealed class PieceTreeTextBufferBuilderTests
{
    [Fact(DisplayName = "factory detects and strips the UTF-8 BOM")]
    public void FactoryDetectsBom()
    {
        var builder = new PieceTreeTextBufferBuilder();
        var bom = ((char)0xFEFF).ToString();
        builder.AcceptChunk(bom + "abc");

        var result = builder.Finish().Create(DefaultEndOfLine.LF);

        result.Bom.ShouldBe(bom);
        result.PieceTree.GetLinesRawContent().ShouldBe("abc");
    }

    [Fact(DisplayName = "factory picks CRLF and does not rewrite content that already uses it")]
    public void FactoryPicksCrlf()
    {
        var builder = new PieceTreeTextBufferBuilder();
        builder.AcceptChunk("a\r\nb\r\nc");

        var result = builder.Finish().Create(DefaultEndOfLine.LF);

        result.Eol.ShouldBe("\r\n");
        result.Crlf.ShouldBe(2);
        result.PieceTree.GetEol().ShouldBe("\r\n");
        result.PieceTree.GetLinesRawContent().ShouldBe("a\r\nb\r\nc");
    }

    [Fact(DisplayName = "factory reports non-basic ASCII, RTL and unusual line terminators")]
    public void FactoryReportsFlags()
    {
        var builder = new PieceTreeTextBufferBuilder();
        builder.AcceptChunk("a" + (char)0x2028 + "b" + (char)0x0627);

        var result = builder.Finish(false).Create(DefaultEndOfLine.LF);

        result.IsBasicAscii.ShouldBeFalse();
        result.ContainsRtl.ShouldBeTrue();
        result.ContainsUnusualLineTerminators.ShouldBeTrue();
    }

    [Fact(DisplayName = "factory normalizes mixed line endings when asked")]
    public void FactoryNormalizesMixedLineEndings()
    {
        var builder = new PieceTreeTextBufferBuilder();
        builder.AcceptChunk("a\r\nb\nc\nd");

        var result = builder.Finish(true).Create(DefaultEndOfLine.LF);

        result.Eol.ShouldBe("\n");
        result.PieceTree.GetEol().ShouldBe("\n");
        result.PieceTree.GetLinesRawContent().ShouldBe("a\nb\nc\nd");
    }

    [Theory(DisplayName = "a text that is one held-back character is not doubled")]
    [InlineData(0x000D)]
    [InlineData(0xD83D)] // a lone high surrogate; passed as a number because attribute strings cannot hold one
    public void FactoryKeepsALoneHeldBackCharacterOnce(int charCode)
    {
        var text = ((char)charCode).ToString();
        var builder = new PieceTreeTextBufferBuilder();
        builder.AcceptChunk(text);

        var result = builder.Finish(normalizeEol: false).Create(DefaultEndOfLine.LF);

        result.PieceTree.GetLength().ShouldBe(1);
        result.PieceTree.GetLinesRawContent().ShouldBe(text);
    }

    [Fact(DisplayName = "a CRLF split across two chunks stays one line ending")]
    public void FactoryJoinsACrlfSplitAcrossChunks()
    {
        var builder = new PieceTreeTextBufferBuilder();
        builder.AcceptChunk("a\r");
        builder.AcceptChunk("\nb");

        var result = builder.Finish(normalizeEol: false).Create(DefaultEndOfLine.LF);

        result.Crlf.ShouldBe(1);
        result.Cr.ShouldBe(0);
        result.PieceTree.GetLineCount().ShouldBe(2);
        result.PieceTree.GetLinesRawContent().ShouldBe("a\r\nb");
    }
}
