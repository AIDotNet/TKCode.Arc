using StreamMark.Core.Model;
using Xunit;

namespace StreamMark.Core.Tests.Parsing;

public sealed class SourceTextTests
{
    [Theory]
    [InlineData("abc\ndef", 0, 0, 0)]
    [InlineData("abc\ndef", 3, 0, 3)]
    [InlineData("abc\ndef", 4, 1, 0)]
    [InlineData("abc\ndef", 7, 1, 3)]
    [InlineData("abc\r\ndef", 5, 1, 0)]
    // \r\n 是一个换行符但占 2 个单位：偏移在 \r 与 \n 之间算作行尾
    [InlineData("abc\r\ndef", 4, 0, 3)]
    [InlineData("a\rb\nc", 2, 1, 0)]
    [InlineData("a\rb\nc", 4, 2, 0)]
    // 中文每个字 1 个单位，代理对（emoji）2 个单位
    [InlineData("中文\n段落", 3, 1, 0)]
    [InlineData("😀x\ny", 2, 0, 2)]
    [InlineData("😀x\ny", 4, 1, 0)]
    public void ToLocation_FollowsDocumentedConvention(string text, int offset, int line, int column)
    {
        Assert.Equal(new SourceLocation(line, column), SourceText.ToLocation(text, offset));
    }

    [Fact]
    public void ToLocation_ClampsOutOfRangeOffsets()
    {
        Assert.Equal(new SourceLocation(0, 0), SourceText.ToLocation("abc", -5));
        Assert.Equal(new SourceLocation(0, 3), SourceText.ToLocation("abc", 99));
        Assert.Equal(new SourceLocation(1, 0), SourceText.ToLocation("abc\n", 99));
    }

    [Theory]
    [InlineData("abc\ndef", 0, 0, 0)]
    [InlineData("abc\ndef", 1, 2, 6)]
    [InlineData("abc\r\ndef\r\nghi", 2, 1, 11)]
    [InlineData("a\rb\nc", 1, 0, 2)]
    [InlineData("中文\n段落", 1, 1, 4)]
    [InlineData("😀x\ny", 0, 2, 2)]
    public void ToOffset_FollowsDocumentedConvention(string text, int line, int column, int expected)
    {
        Assert.Equal(expected, SourceText.ToOffset(text, new SourceLocation(line, column)));
    }

    [Fact]
    public void ToOffset_ClampsColumnToLineEnd_AndLineToTextEnd()
    {
        // 列超过行长度时钳到行尾（换行符之前），不会跳到下一行
        Assert.Equal(3, SourceText.ToOffset("abc\r\ndef", new SourceLocation(0, 99)));
        Assert.Equal(8, SourceText.ToOffset("abc\r\ndef", new SourceLocation(9, 0)));
        Assert.Equal(0, SourceText.ToOffset("abc", new SourceLocation(-1, -1)));
    }

    [Theory]
    [InlineData("# T\r\n\r\n中文 😀 para\r\n\r\n- a\r\n- b")]
    [InlineData("one\rtwo\nthree\r\nfour")]
    public void RoundTrip_EveryOffsetOutsideLineBreaks(string text)
    {
        for (var offset = 0; offset <= text.Length; offset++)
        {
            // 偏移在 \r\n 中间时会被归到行尾，不满足往返
            if (offset > 0 && offset < text.Length && text[offset - 1] == '\r' && text[offset] == '\n')
            {
                continue;
            }
            Assert.Equal(offset, SourceText.ToOffset(text, SourceText.ToLocation(text, offset)));
        }
    }
}
