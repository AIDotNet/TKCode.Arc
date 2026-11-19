// 思路参考 LiveMarkdown.Avalonia 的 BackslashMathExtension（Apache-2.0），按 StreamMark 的需要重写。
using Markdig;
using Markdig.Extensions.Mathematics;
using Markdig.Helpers;
using Markdig.Parsers;
using Markdig.Renderers;
using Markdig.Syntax;

namespace StreamMark.Core.Parsing;

/// <summary>
/// 支持大模型常用的反斜杠数学定界符：行内 <c>\(…\)</c>、块级 <c>\[…\]</c>。
/// Markdig 自带的数学扩展只认 <c>$…$</c> 和 <c>$$…$$</c>。
/// </summary>
public sealed class BackslashMathExtension : IMarkdownExtension
{
    public void Setup(MarkdownPipelineBuilder pipeline)
    {
        if (!pipeline.BlockParsers.Contains<BackslashMathBlockParser>())
        {
            pipeline.BlockParsers.Insert(0, new BackslashMathBlockParser());
        }
        // 必须排在转义解析器之前，否则 "\(" 会被当作转义的 "("
        if (!pipeline.InlineParsers.Contains<BackslashMathInlineParser>())
        {
            pipeline.InlineParsers.Insert(0, new BackslashMathInlineParser());
        }
    }

    public void Setup(MarkdownPipeline pipeline, IMarkdownRenderer renderer)
    {
    }
}

/// <summary>反斜杠定界的行内公式；<see cref="IsDisplay"/> 为 true 表示同一行内的 <c>\[…\]</c>。</summary>
public sealed class BackslashMathInline : MathInline
{
    public bool IsDisplay { get; init; }
}

internal sealed class BackslashMathInlineParser : InlineParser
{
    public BackslashMathInlineParser()
    {
        OpeningCharacters = ['\\'];
    }

    public override bool Match(InlineProcessor processor, ref StringSlice slice)
    {
        var closing = slice.PeekChar(1) switch
        {
            '(' => ')',
            '[' => ']',
            _ => '\0',
        };
        if (closing == '\0')
        {
            return false;
        }

        var start = slice.Start;
        var contentStart = start + 2;
        var text = slice.Text;
        var closingStart = -1;
        for (var i = contentStart; i < slice.End; i++)
        {
            var c = text[i];
            if (c is '\r' or '\n' && closing == ')')
            {
                // 行内 \( 不跨行；\[ 允许在段落内跨行（软换行）
                return false;
            }
            if (c == '\\' && text[i + 1] == closing)
            {
                closingStart = i;
                break;
            }
        }
        if (closingStart < 0)
        {
            return false;
        }

        processor.Inline = new BackslashMathInline
        {
            Delimiter = '\\',
            DelimiterCount = closing == ']' ? 2 : 1,
            IsDisplay = closing == ']',
            Content = contentStart <= closingStart - 1 ? new StringSlice(text, contentStart, closingStart - 1) : StringSlice.Empty,
            Span = new SourceSpan(processor.GetSourcePosition(start, out var line, out var column), processor.GetSourcePosition(closingStart + 1)),
            Line = line,
            Column = column,
        };
        slice.Start = closingStart + 2;
        return true;
    }
}

/// <summary>以 <c>\[</c> 开头的行开启块级公式，直到出现 <c>\]</c>。</summary>
internal sealed class BackslashMathBlockParser : BlockParser
{
    public BackslashMathBlockParser()
    {
        OpeningCharacters = ['\\'];
    }

    public override bool CanInterrupt(BlockProcessor processor, Block block) => IsOpeningLine(processor);

    public override BlockState TryOpen(BlockProcessor processor)
    {
        if (!IsOpeningLine(processor))
        {
            return BlockState.None;
        }

        var block = new MathBlock(this)
        {
            FencedChar = '\\',
            OpeningFencedCharCount = 2,
            Line = processor.LineIndex,
            Column = processor.Column,
            Span = new SourceSpan(processor.Start, processor.Line.End),
        };
        processor.NewBlocks.Push(block);
        return AppendUntilClosing(processor, block, processor.Start + 2);
    }

    public override BlockState TryContinue(BlockProcessor processor, Block block) =>
        block is MathBlock { FencedChar: '\\' } math
            ? AppendUntilClosing(processor, math, processor.Line.Start)
            : BlockState.None;

    private static bool IsOpeningLine(BlockProcessor processor)
    {
        if (processor.IsCodeIndent)
        {
            return false;
        }
        var line = processor.Line;
        return line.Start + 1 <= line.End && line.Text[line.Start] == '\\' && line.Text[line.Start + 1] == '[';
    }

    private static BlockState AppendUntilClosing(BlockProcessor processor, MathBlock block, int start)
    {
        var line = processor.Line;
        var closing = -1;
        for (var i = start; i < line.End; i++)
        {
            if (line.Text[i] == '\\' && line.Text[i + 1] == ']')
            {
                closing = i;
                break;
            }
        }

        if (closing >= 0)
        {
            AppendLine(processor, block, start, closing - 1);
            block.ClosingFencedCharCount = 2;
            block.UpdateSpanEnd(closing + 1);
            return BlockState.BreakDiscard;
        }

        AppendLine(processor, block, start, line.End);
        block.UpdateSpanEnd(line.End);
        return BlockState.ContinueDiscard;
    }

    private static void AppendLine(BlockProcessor processor, MathBlock block, int start, int end)
    {
        var line = processor.Line;
        if (start > end)
        {
            return;
        }
        var allWhitespace = true;
        for (var i = start; i <= end; i++)
        {
            if (!char.IsWhiteSpace(line.Text[i]))
            {
                allWhitespace = false;
                break;
            }
        }
        if (allWhitespace)
        {
            return;
        }
        var slice = new StringSlice(line.Text, start, end);
        block.AppendLine(ref slice, processor.Column, processor.LineIndex, processor.CurrentLineStartPosition + start - line.Start, processor.TrackTrivia);
    }
}
