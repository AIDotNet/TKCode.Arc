using System.Collections.Immutable;
using System.Diagnostics;
using StreamMark.Core.Highlighting;
using TextMateSharp.Grammars;
using TextMateSharp.Internal.Grammars;
using TmFontStyle = TextMateSharp.Themes.FontStyle;

namespace StreamMark.Highlighting.TextMate;

/// <summary>
/// 一个代码块的增量着色会话：缓存每行的结束语法状态与片段，只重新分词变化处之后的行；
/// 若变化处之后某个完整行的结束状态与旧缓存一致且其后的文本未变，则直接复用旧结果。
/// </summary>
internal sealed class TextMateHighlightSession : IHighlightSession
{
    /// <summary>单行分词的时间上限，超时则该行余下部分不着色。</summary>
    internal static readonly TimeSpan LineTimeLimit = TimeSpan.FromMilliseconds(50);

    /// <summary>超过该长度的行不着色（并保持语法状态不变）。</summary>
    internal const int MaxLineLength = 4000;

    /// <summary>单行超时后的重试上限（覆盖语法规则的冷编译）。</summary>
    internal static readonly TimeSpan RetryTimeLimit = TimeSpan.FromSeconds(1);

    private readonly ThemeContext? _context;
    private readonly string? _scopeName;
    private IGrammar? _grammar;
    private bool _grammarResolved;

    private string _code = string.Empty;
    private List<LineInfo> _lines = [];
    private ImmutableArray<HighlightToken>[]? _result;
    private bool _disposed;

    public TextMateHighlightSession(ThemeContext? context, string? scopeName)
    {
        _context = context;
        _scopeName = scopeName;
        _grammarResolved = context is null || scopeName is null;
    }

    /// <summary>累计调用 TextMate 分词的行数（测试用：验证追加时不会重新分词前面的行）。</summary>
    internal int TokenizedLineCount { get; private set; }

    /// <summary>当前是否有可用语法（首次 <see cref="Update"/> 后才确定）。</summary>
    internal bool HasGrammar => _grammar is not null;

    public IReadOnlyList<ImmutableArray<HighlightToken>> Update(string code)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        code ??= string.Empty;

        if (_result is not null && string.Equals(code, _code, StringComparison.Ordinal))
        {
            return _result;
        }

        string old = _code;
        int prefix = old.AsSpan().CommonPrefixLength(code);

        // 保留结束换行符完全落在公共前缀里的旧行（行文本、换行、起始状态都没变）
        int keep = _lines.Count;
        while (keep > 0 && (_lines[keep - 1].Newline < 0 || _lines[keep - 1].Newline >= prefix))
        {
            keep--;
        }

        bool pureAppend = prefix == old.Length;
        List<LineInfo> oldLines = _lines;
        List<LineInfo> lines;
        int suffix = 0;
        if (pureAppend)
        {
            lines = _lines;
            lines.RemoveRange(keep, lines.Count - keep);
        }
        else
        {
            lines = new List<LineInfo>(Math.Max(16, oldLines.Count + 8));
            for (int i = 0; i < keep; i++)
            {
                lines.Add(oldLines[i]);
            }

            int max = Math.Min(old.Length, code.Length) - prefix;
            while (suffix < max && old[old.Length - 1 - suffix] == code[code.Length - 1 - suffix])
            {
                suffix++;
            }
        }

        int delta = code.Length - old.Length;
        int suffixStart = code.Length - suffix; // 新文本中公共后缀的起点

        if (!_grammarResolved || _grammar is not null)
        {
            lock (_context!.Gate)
            {
                if (!_grammarResolved)
                {
                    _grammar = _context.GetGrammarLocked(_scopeName!);
                    _grammarResolved = true;
                }

                Retokenize(code, lines, keep, pureAppend ? null : oldLines, delta, suffixStart);
            }
        }
        else
        {
            Retokenize(code, lines, keep, null, delta, suffixStart);
        }

        _code = code;
        _lines = lines;

        var result = new ImmutableArray<HighlightToken>[lines.Count];
        for (int i = 0; i < result.Length; i++)
        {
            result[i] = lines[i].Tokens;
        }

        _result = result;
        return result;
    }

    private void Retokenize(string code, List<LineInfo> lines, int keep, List<LineInfo>? oldLines, int delta, int suffixStart)
    {
        int pos = keep == 0 ? 0 : lines[keep - 1].Newline + 1;
        IStateStack? state = keep == 0 ? null : lines[keep - 1].EndState;

        while (true)
        {
            int nl = code.IndexOf('\n', pos);
            int end = nl < 0 ? code.Length : nl;
            (ImmutableArray<HighlightToken> tokens, IStateStack? endState) = TokenizeLine(code, pos, end, state);
            lines.Add(new LineInfo(pos, nl, tokens, endState));
            if (nl < 0)
            {
                return;
            }

            state = endState;
            pos = nl + 1;

            // 提前结束：换行符已进入公共后缀 ⇒ 之后的文本与旧文本相同；若此处语法状态也相同，旧结果可整体复用
            if (oldLines is not null && nl >= suffixStart && endState is not null)
            {
                int m = FindLineByNewline(oldLines, nl - delta);
                if (m >= 0 && endState.Equals(oldLines[m].EndState))
                {
                    for (int k = m + 1; k < oldLines.Count; k++)
                    {
                        LineInfo o = oldLines[k];
                        lines.Add(new LineInfo(o.Start + delta, o.Newline < 0 ? -1 : o.Newline + delta, o.Tokens, o.EndState));
                    }

                    return;
                }
            }
        }
    }

    private (ImmutableArray<HighlightToken> Tokens, IStateStack? EndState) TokenizeLine(string code, int start, int end, IStateStack? state)
    {
        int contentEnd = end > start && code[end - 1] == '\r' ? end - 1 : end;
        int length = contentEnd - start;
        if (_grammar is null || length > MaxLineLength)
        {
            return (ImmutableArray<HighlightToken>.Empty, state);
        }

        // 行尾正好是 '\n'（且无 '\r'）时直接把它一起传入，TextMateSharp 就不必再复制一份加换行的缓冲区
        ReadOnlyMemory<char> text = contentEnd == end && end < code.Length
            ? code.AsMemory(start, length + 1)
            : code.AsMemory(start, length);

        ITokenizeLineResult2 result;
        bool stoppedEarly;
        try
        {
            result = TokenizeTimed(text, state, LineTimeLimit, out stoppedEarly);
            if (stoppedEarly)
            {
                // 首次用到某些规则时 TextMateSharp 才编译正则（以及首次加载 libonigwrap），冷启动可能远超单行上限；
                // 规则编译完之后重试通常很快。仍超时则说明这一行确实病态。
                result = TokenizeTimed(text, state, RetryTimeLimit, out stoppedEarly);
            }
        }
        catch (Exception)
        {
            return (ImmutableArray<HighlightToken>.Empty, state);
        }

        TokenizedLineCount++;
        int[] raw = result.Tokens;
        // 与 VS Code 一致：分词被截断时沿用上一行的状态，避免把行中间的半截状态带到后续行
        IStateStack? endState = stoppedEarly ? state : result.RuleStack ?? state;
        if (raw is null || raw.Length < 2 || length == 0)
        {
            return (ImmutableArray<HighlightToken>.Empty, endState);
        }

        ImmutableArray<HighlightToken>.Builder builder = ImmutableArray.CreateBuilder<HighlightToken>(raw.Length / 2);
        int count = raw.Length / 2;
        for (int i = 0; i < count; i++)
        {
            int tokenStart = raw[2 * i];
            if (tokenStart >= length)
            {
                break;
            }

            int tokenEnd = i + 1 < count ? Math.Min(raw[2 * i + 2], length) : length;
            if (tokenEnd <= tokenStart)
            {
                continue;
            }

            int metadata = raw[2 * i + 1];
            uint foreground = _context!.ResolveForegroundLocked(EncodedTokenAttributes.GetForeground(metadata));
            HighlightFontStyle style = MapFontStyle(EncodedTokenAttributes.GetFontStyle(metadata));

            if (builder.Count > 0)
            {
                HighlightToken last = builder[^1];
                if (last.Foreground == foreground && last.Style == style && last.Start + last.Length == tokenStart)
                {
                    builder[^1] = last with { Length = tokenEnd - last.Start };
                    continue;
                }
            }

            builder.Add(new HighlightToken(tokenStart, tokenEnd - tokenStart, foreground, style));
        }

        return (builder.Count == builder.Capacity ? builder.MoveToImmutable() : builder.ToImmutable(), endState);
    }

    private ITokenizeLineResult2 TokenizeTimed(ReadOnlyMemory<char> text, IStateStack? state, TimeSpan limit, out bool stoppedEarly)
    {
        // TokenizeLineResult2.StoppedEarly 在 TextMateSharp 2.0.4 中是 internal，只能用耗时判断（误判只会多重试一次）
        long started = Stopwatch.GetTimestamp();
        ITokenizeLineResult2 result = _grammar!.TokenizeLine2(text, state, limit);
        stoppedEarly = Stopwatch.GetElapsedTime(started) >= limit;
        return result;
    }

    private static HighlightFontStyle MapFontStyle(TmFontStyle style)
    {
        if (style <= TmFontStyle.None)
        {
            return HighlightFontStyle.None;
        }

        HighlightFontStyle result = HighlightFontStyle.None;
        if ((style & TmFontStyle.Bold) != 0)
        {
            result |= HighlightFontStyle.Bold;
        }

        if ((style & TmFontStyle.Italic) != 0)
        {
            result |= HighlightFontStyle.Italic;
        }

        if ((style & TmFontStyle.Underline) != 0)
        {
            result |= HighlightFontStyle.Underline;
        }

        if ((style & TmFontStyle.Strikethrough) != 0)
        {
            result |= HighlightFontStyle.Strikethrough;
        }

        return result;
    }

    private static int FindLineByNewline(List<LineInfo> lines, int newline)
    {
        int lo = 0, hi = lines.Count - 1;
        while (lo <= hi)
        {
            int mid = (lo + hi) >>> 1;
            int value = lines[mid].Newline;
            if (value < 0 || value > newline)
            {
                hi = mid - 1; // 最后一行（无换行）排在最后
            }
            else if (value < newline)
            {
                lo = mid + 1;
            }
            else
            {
                return mid;
            }
        }

        return -1;
    }

    public void Dispose()
    {
        _disposed = true;
        _lines = [];
        _result = null;
        _code = string.Empty;
        _grammar = null;
    }

    /// <param name="Start">行首在代码中的偏移。</param>
    /// <param name="Newline">结束本行的 '\n' 的偏移；最后一个未完成的行为 -1。</param>
    /// <param name="Tokens">着色片段（相对行首）。</param>
    /// <param name="EndState">本行结束时的语法状态（无语法时为 null）。</param>
    private readonly record struct LineInfo(int Start, int Newline, ImmutableArray<HighlightToken> Tokens, IStateStack? EndState);
}
