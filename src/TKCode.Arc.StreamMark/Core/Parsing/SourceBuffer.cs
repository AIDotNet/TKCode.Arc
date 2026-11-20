using System.Buffers;
using System.Text;

namespace StreamMark.Core.Parsing;

/// <summary>源码中一行的偏移元数据。</summary>
internal readonly record struct LineSpan(int Start, int ContentLength, byte NewlineLength)
{
    /// <summary>不含换行符（已去除 \r）的结束偏移（开区间）。</summary>
    public int ContentEnd => Start + ContentLength;

    /// <summary>包含换行符在内的整行结束偏移（开区间）。</summary>
    public int FullEnd => Start + ContentLength + NewlineLength;

    /// <summary>是否已见到换行符（false 表示流式末尾的未结束半行）。</summary>
    public bool IsComplete => NewlineLength > 0;
}

/// <summary>
/// 仅追加的源码缓冲（16 KB char[] Slab 链）+ 零拷贝静态字符串包装，配合增量 <see cref="LineSpan"/> 行索引。
/// 写端通过外部锁追加，并维护只增不减的长度；解析端读取时无需拷贝整段尾部字符串。
/// </summary>
internal sealed class SourceBuffer
{
    private const int SlabShift = 14; // 16384 chars = 32 KB per slab
    private const int SlabSize = 1 << SlabShift;
    private const int SlabMask = SlabSize - 1;

    private readonly List<char[]> _slabs = [];
    private string? _staticSource;
    private int _length;

    public int Length => _length;

    /// <summary>一次性包装静态字符串（零拷贝）。</summary>
    public void LoadStatic(string text)
    {
        _slabs.Clear();
        _staticSource = text;
        _length = text.Length;
    }

    /// <summary>清空缓冲并准备重新流式追加。</summary>
    public void Clear()
    {
        _staticSource = null;
        _length = 0;
        // 保留首块 Slab 避免频繁重建，释放多余块
        if (_slabs.Count > 1)
        {
            _slabs.RemoveRange(1, _slabs.Count - 1);
        }
    }

    /// <summary>追加一段字符到末尾。</summary>
    public void Append(ReadOnlySpan<char> text)
    {
        if (text.IsEmpty)
        {
            return;
        }
        if (_staticSource is not null)
        {
            var prev = _staticSource;
            _staticSource = null;
            _length = 0;
            Append(prev.AsSpan());
        }
        var offset = 0;
        while (offset < text.Length)
        {
            var slabIndex = _length >> SlabShift;
            var slabOffset = _length & SlabMask;
            if (slabIndex >= _slabs.Count)
            {
                _slabs.Add(new char[SlabSize]);
            }
            var copy = Math.Min(SlabSize - slabOffset, text.Length - offset);
            text.Slice(offset, copy).CopyTo(_slabs[slabIndex].AsSpan(slabOffset, copy));
            _length += copy;
            offset += copy;
        }
    }

    public char this[int index]
    {
        get
        {
            if (_staticSource is not null)
            {
                return _staticSource[index];
            }
            return _slabs[index >> SlabShift][index & SlabMask];
        }
    }

    /// <summary>
    /// 取得区间 [<paramref name="start"/>, <paramref name="start"/> + <paramref name="length"/>) 的只读 Span。
    /// 若完全位于单个 Slab 或静态字符串内则零拷贝返回；跨 Slab 边界时拷贝进 <paramref name="scratch"/>。
    /// </summary>
    public ReadOnlySpan<char> GetSpan(int start, int length, ref char[] scratch)
    {
        if (length <= 0)
        {
            return ReadOnlySpan<char>.Empty;
        }
        if (_staticSource is not null)
        {
            return _staticSource.AsSpan(start, length);
        }
        var firstSlab = start >> SlabShift;
        var firstOffset = start & SlabMask;
        if (firstOffset + length <= SlabSize)
        {
            return _slabs[firstSlab].AsSpan(firstOffset, length);
        }
        if (scratch.Length < length)
        {
            scratch = new char[Math.Max(length, Math.Max(256, scratch.Length * 2))];
        }
        CopyTo(start, length, scratch.AsSpan(0, length));
        return scratch.AsSpan(0, length);
    }

    public void CopyTo(int start, int length, Span<char> destination)
    {
        if (length <= 0)
        {
            return;
        }
        if (_staticSource is not null)
        {
            _staticSource.AsSpan(start, length).CopyTo(destination);
            return;
        }
        var pos = start;
        var written = 0;
        while (written < length)
        {
            var slabIndex = pos >> SlabShift;
            var slabOffset = pos & SlabMask;
            var take = Math.Min(SlabSize - slabOffset, length - written);
            _slabs[slabIndex].AsSpan(slabOffset, take).CopyTo(destination.Slice(written, take));
            pos += take;
            written += take;
        }
    }

    public string GetString(int start, int length)
    {
        start = Math.Clamp(start, 0, _length);
        length = Math.Clamp(length, 0, _length - start);
        if (length == 0)
        {
            return string.Empty;
        }
        if (_staticSource is not null)
        {
            return start == 0 && length == _staticSource.Length ? _staticSource : _staticSource.Substring(start, length);
        }
        var firstSlab = start >> SlabShift;
        var firstOffset = start & SlabMask;
        if (firstOffset + length <= SlabSize)
        {
            return new string(_slabs[firstSlab], firstOffset, length);
        }
        return string.Create(length, (this, start), static (span, state) => state.Item1.CopyTo(state.start, span.Length, span));
    }

    /// <summary>增量扫描 [<paramref name="indexedPos"/>, <paramref name="limit"/>) 并更新行索引。</summary>
    public void IndexLines(List<LineSpan> lines, ref int indexedPos, int limit)
    {
        if (limit <= indexedPos)
        {
            return;
        }
        // 若上一条是未结束半行，从该行起点继续扫描，避免拆断
        var lineStart = indexedPos;
        if (lines.Count > 0 && !lines[^1].IsComplete)
        {
            lineStart = lines[^1].Start;
            lines.RemoveAt(lines.Count - 1);
        }
        var pos = lineStart;
        while (pos < limit)
        {
            var nl = IndexOfNewline(pos, limit);
            if (nl < 0)
            {
                // 末尾半行（尚未收到 \n）：注意若以单个 \r 结尾，先保留原长待下一字符确认是否为 \r\n
                var len = limit - pos;
                lines.Add(new LineSpan(pos, len, 0));
                indexedPos = limit;
                return;
            }
            var contentEnd = nl;
            byte nlLen = 1;
            if (contentEnd > pos && this[contentEnd - 1] == '\r')
            {
                contentEnd--;
                nlLen = 2;
            }
            lines.Add(new LineSpan(pos, contentEnd - pos, nlLen));
            pos = nl + 1;
        }
        indexedPos = limit;
    }

    private int IndexOfNewline(int start, int limit)
    {
        if (_staticSource is not null)
        {
            var idx = _staticSource.AsSpan(start, limit - start).IndexOf('\n');
            return idx < 0 ? -1 : start + idx;
        }
        var pos = start;
        while (pos < limit)
        {
            var slabIndex = pos >> SlabShift;
            var slabOffset = pos & SlabMask;
            var take = Math.Min(SlabSize - slabOffset, limit - pos);
            var idx = _slabs[slabIndex].AsSpan(slabOffset, take).IndexOf('\n');
            if (idx >= 0)
            {
                return pos + idx;
            }
            pos += take;
        }
        return -1;
    }
}

/// <summary>
/// 开放文本叶块（围栏代码、缩进代码、数学块、HTML 块、原样组件）的增量行追加缓冲：
/// 已完整到达的行只剥离前缀并追加一次；流式末尾半行可 O(1) 回滚并重写，消除每帧整块重建。
/// </summary>
internal sealed class OpenBlockBuffer
{
    private char[] _buffer = [];
    private int _length;
    private int _lineCount;
    private string? _cachedString;

    public int LineCount => _lineCount;

    public int Length => _length;

    public readonly record struct Checkpoint(int Length, int LineCount);

    public Checkpoint Save() => new(_length, _lineCount);

    public void Restore(Checkpoint cp)
    {
        if (_length != cp.Length || _lineCount != cp.LineCount)
        {
            _length = cp.Length;
            _lineCount = cp.LineCount;
            _cachedString = null;
        }
    }

    public void Clear()
    {
        _length = 0;
        _lineCount = 0;
        _cachedString = null;
        if (_buffer.Length > 64 * 1024)
        {
            _buffer = [];
        }
    }

    public void AppendLine(ReadOnlySpan<char> line)
    {
        var need = line.Length + (_lineCount > 0 ? 1 : 0);
        EnsureCapacity(_length + need);
        if (_lineCount > 0)
        {
            _buffer[_length++] = '\n';
        }
        line.CopyTo(_buffer.AsSpan(_length));
        _length += line.Length;
        _lineCount++;
        _cachedString = null;
    }

    /// <summary>以追加方式附加一行（去除行尾 \r 与最多 <paramref name="maxIndentStrip"/> 个前导空格，或展开 Tab）。</summary>
    public void AppendFencedLine(ReadOnlySpan<char> line, int maxIndentStrip)
    {
        if (line.Length > 0 && line[^1] == '\r')
        {
            line = line[..^1];
        }
        var strip = 0;
        while (strip < maxIndentStrip && strip < line.Length && line[strip] == ' ')
        {
            strip++;
        }
        AppendLine(line[strip..]);
    }

    public ReadOnlySpan<char> AsSpan() => _length == 0 ? ReadOnlySpan<char>.Empty : _buffer.AsSpan(0, _length);

    public string GetString()
    {
        if (_cachedString is not null)
        {
            return _cachedString;
        }
        _cachedString = _length == 0 ? string.Empty : new string(_buffer, 0, _length);
        return _cachedString;
    }

    /// <summary>去除末尾多余的单个空格与换行（与 OpenFenceCache / Remend 流式代码行为一致），并返回精确行数。</summary>
    public string GetTrimmedStreamingCode(out int lineCount)
    {
        var len = _length;
        if (len == 0)
        {
            lineCount = 1;
            return string.Empty;
        }
        var span = _buffer.AsSpan(0, len);
        if (span.Length >= 1 && span[^1] == ' ' && (span.Length < 2 || span[^2] != ' '))
        {
            span = span[..^1];
        }
        var trimmedNewline = false;
        if (span.Length >= 1 && span[^1] == '\n')
        {
            span = span[..^1];
            trimmedNewline = true;
        }
        lineCount = Math.Max(1, _lineCount - (trimmedNewline ? 1 : 0));
        if (span.Length == _length && _cachedString is not null)
        {
            return _cachedString;
        }
        return span.IsEmpty ? string.Empty : new string(span);
    }

    /// <summary>缩进代码块：去掉首尾纯空白行后生成字符串。</summary>
    public string GetIndentedCode()
    {
        var span = AsSpan();
        // 去掉头部空行
        while (!span.IsEmpty)
        {
            var nl = span.IndexOf('\n');
            var line = nl < 0 ? span : span[..nl];
            if (!line.IsWhiteSpace())
            {
                break;
            }
            if (nl < 0)
            {
                return string.Empty;
            }
            span = span[(nl + 1)..];
        }
        // 去掉尾部空行
        while (!span.IsEmpty)
        {
            var nl = span.LastIndexOf('\n');
            var line = nl < 0 ? span : span[(nl + 1)..];
            if (!line.IsWhiteSpace())
            {
                break;
            }
            if (nl < 0)
            {
                return string.Empty;
            }
            span = span[..nl];
        }
        return span.IsEmpty ? string.Empty : new string(span);
    }

    private void EnsureCapacity(int required)
    {
        if (required <= _buffer.Length)
        {
            return;
        }
        Array.Resize(ref _buffer, Math.Max(required, Math.Max(256, _buffer.Length * 2)));
    }
}
