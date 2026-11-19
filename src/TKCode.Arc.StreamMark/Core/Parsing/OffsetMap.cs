namespace StreamMark.Core.Parsing;

/// <summary>
/// 把 remend 修补后文本中的位置映射回原始源码位置。
/// remend 只会在文本中插入少量字符（转义反斜杠、零宽空格），以及改写/截断末尾，
/// 所以用双指针贪心对齐即可：相同字符同时前进，多出的字符记为插入点；
/// 第一次无法对齐的位置之后（末尾被改写的部分）全部映射到原始文本末尾附近。
/// </summary>
internal readonly struct OffsetMap
{
    private readonly int _base;
    private readonly int _rawLength;
    private readonly int[]? _insertions;
    private readonly int _divergence;
    private readonly string? _raw;

    private OffsetMap(int @base, int rawLength, int[]? insertions, int divergence, string? raw)
    {
        _base = @base;
        _rawLength = rawLength;
        _insertions = insertions;
        _divergence = divergence;
        _raw = raw;
    }

    /// <summary>原文未经修补时的恒等映射。</summary>
    public static OffsetMap Identity(int @base, int length) => new(@base, length, null, int.MaxValue, null);

    /// <param name="base">被解析文本在整篇源码中的起点。</param>
    /// <param name="raw">交给 remend 的文本。</param>
    /// <param name="healed">remend 的输出（Markdig 实际解析的文本）。</param>
    /// <param name="source">
    /// <see cref="SourceText"/> 取字的原文，必须与 <paramref name="raw"/> 等长、逐位对应（例如 <paramref name="raw"/> 是遮盖过的原文）；默认就是 <paramref name="raw"/>。
    /// </param>
    public static OffsetMap Create(int @base, string raw, string healed, string? source = null)
    {
        source ??= raw;
        if (ReferenceEquals(raw, healed) || raw == healed)
        {
            return new OffsetMap(@base, raw.Length, null, int.MaxValue, source);
        }
        List<int>? insertions = null;
        int i = 0, j = 0;
        while (i < raw.Length && j < healed.Length)
        {
            if (raw[i] == healed[j])
            {
                i++;
                j++;
            }
            else if (healed[j] is '\\' or '​')
            {
                (insertions ??= []).Add(j);
                j++;
            }
            else
            {
                break;
            }
        }
        var divergence = i < raw.Length && j < healed.Length ? j : int.MaxValue;
        return new OffsetMap(@base, raw.Length, insertions?.ToArray(), divergence, source);
    }

    /// <summary>被解析文本对应的原始源码末尾（绝对位置）。</summary>
    public int SourceEnd => _base + _rawLength;

    /// <summary>
    /// 修补后文本的区间 [<paramref name="healedStart"/>, <paramref name="healedEnd"/>) 对应的原始源码文字：
    /// 不含 remend 插入的转义与末尾补全。没有原文（<see cref="Identity"/>）时返回 null。
    /// </summary>
    public string? SourceText(int healedStart, int healedEnd)
    {
        if (_raw is null)
        {
            return null;
        }
        var start = ToSource(healedStart) - _base;
        var end = ToSource(healedEnd) - _base;
        return end > start ? _raw.Substring(start, end - start) : "";
    }

    /// <summary>修补后文本中的位置 → 整篇源码中的绝对位置。</summary>
    public int ToSource(int healedIndex)
    {
        var inserted = 0;
        if (_insertions is { } insertions)
        {
            var index = Array.BinarySearch(insertions, healedIndex);
            inserted = index >= 0 ? index : ~index;
        }
        var raw = healedIndex - inserted;
        if (healedIndex > _divergence)
        {
            raw = Math.Min(raw, _rawLength);
        }
        return _base + Math.Clamp(raw, 0, _rawLength);
    }
}
