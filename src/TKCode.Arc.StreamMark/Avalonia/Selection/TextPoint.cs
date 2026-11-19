namespace StreamMark.Avalonia.Selection;

/// <summary>
/// 选区中的一个位置：第几个文档（会话中的消息序号）、顶层块的 key（源码起始偏移）、
/// 块内第几个文本片段、片段内的字符偏移。按字典序比较即为阅读顺序。
/// 源码只追加，所以这些坐标在流式重解析之间保持有效。
/// </summary>
public readonly record struct TextPoint(int Entry, int BlockKey, int Segment, int Offset) : IComparable<TextPoint>
{
    public int CompareTo(TextPoint other)
    {
        var c = Entry.CompareTo(other.Entry);
        if (c != 0)
        {
            return c;
        }
        c = BlockKey.CompareTo(other.BlockKey);
        if (c != 0)
        {
            return c;
        }
        c = Segment.CompareTo(other.Segment);
        return c != 0 ? c : Offset.CompareTo(other.Offset);
    }

    public static bool operator <(TextPoint a, TextPoint b) => a.CompareTo(b) < 0;
    public static bool operator >(TextPoint a, TextPoint b) => a.CompareTo(b) > 0;
    public static bool operator <=(TextPoint a, TextPoint b) => a.CompareTo(b) <= 0;
    public static bool operator >=(TextPoint a, TextPoint b) => a.CompareTo(b) >= 0;
}

/// <summary>一个文本片段的身份。</summary>
public readonly record struct SegmentKey(int Entry, int BlockKey, int Segment)
{
    public TextPoint At(int offset) => new(Entry, BlockKey, Segment, offset);
}
