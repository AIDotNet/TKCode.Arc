using StreamMark.Core.Model;

namespace StreamMark.Core.Parsing;

/// <summary>扁平行内节点（值类型，存在池化数组内并通过下标双向链表 O(1) 插入/删除，零中间堆对象分配）。</summary>
internal enum InlineNodeKind : byte
{
    Removed,
    Text,
    Code,
    SoftBreak,
    HardBreak,
    Math,
    Image,
    ComponentLeaf,
    HtmlTag,
    Autolink,
    FootnoteRef,
    EmphasisOpen,
    EmphasisClose,
    LinkOpen,
    LinkClose,
    ComponentOpen,
    ComponentClose,
}

internal struct InlineNode
{
    public InlineNodeKind Kind;
    public InlineStyle Style;
    public int Prev;
    public int Next;
    /// <summary>在 <c>_textPool</c> 中的起点，或对象/链接/组件表下标。</summary>
    public int Start;
    /// <summary>在 <c>_textPool</c> 中的长度，或附加索引（如组件名 / 链接下标）。</summary>
    public int Length;
    /// <summary>扩展整数载荷（例如 LinkOpen 的链接索引或 IncompleteLink 哨兵、ComponentOpen 的样式/索引）。</summary>
    public int Extra;
    public bool Flag;
}

/// <summary>CommonMark §6.2 强调/删除线分隔符栈条目（值类型，零堆分配）。</summary>
internal struct DelimiterEntry
{
    public int NodeIndex;
    public char Char;
    public int Count;
    public int OriginalCount;
    public bool CanOpen;
    public bool CanClose;
    public bool Active;
}

/// <summary>CommonMark §6.5–6.6 链接/图片方括号栈条目（值类型）。</summary>
internal struct BracketEntry
{
    public int NodeIndex;
    public int TextOffset;
    public int SourcePosition;
    public int DelimiterStackIndex;
    public bool IsImage;
    public bool Active;
}

/// <summary>CommonMark §6.2 + CJK-Friendly + GFM 删除线分隔符栈处理逻辑。</summary>
internal static class InlineDelimiterProcessor
{
    /// <summary>按 CommonMark §6.2 与 CJK-Friendly 规则计算一串连续分隔符（* / _ / ~）的开闭能力。</summary>
    public static void ClassifyFlanking(
        ReadOnlySpan<char> input,
        int runStart,
        int runLength,
        char delimChar,
        bool cjkFriendly,
        out bool canOpen,
        out bool canClose)
    {
        var before = UnicodeClassification.CodePointBefore(input, runStart);
        var after = UnicodeClassification.CodePointAt(input, runStart + runLength);

        var beforeWs = UnicodeClassification.IsUnicodeWhitespace(before);
        var afterWs = UnicodeClassification.IsUnicodeWhitespace(after);
        var beforePunct = !beforeWs && UnicodeClassification.IsUnicodePunctuation(before);
        var afterPunct = !afterWs && UnicodeClassification.IsUnicodePunctuation(after);

        // CJK-Friendly Emphasis (remark-cjk-friendly):
        // 当强调符号紧邻 CJK 标点（如「中文。」后的 **、「前**（中文）**后」）或 CJK 字符时，
        // 将该侧标点视作普通非标点字符（不可视为空白，否则会反转左右侧翼判定），使 CJK 强调正常开闭。
        var effectiveBeforePunct = beforePunct;
        var effectiveAfterPunct = afterPunct;
        if (cjkFriendly && delimChar is '*' or '~')
        {
            if (beforePunct && (UnicodeClassification.IsCjkPunctuation(before) || UnicodeClassification.IsCjkOrFullwidth(after)))
            {
                effectiveBeforePunct = false;
            }
            if (afterPunct && (UnicodeClassification.IsCjkPunctuation(after) || UnicodeClassification.IsCjkOrFullwidth(before)))
            {
                effectiveAfterPunct = false;
            }
        }

        var leftFlanking = !afterWs && (!effectiveAfterPunct || beforeWs || beforePunct);
        var rightFlanking = !beforeWs && (!effectiveBeforePunct || afterWs || afterPunct);

        if (delimChar == '_')
        {
            canOpen = leftFlanking && (!rightFlanking || beforePunct);
            canClose = rightFlanking && (!leftFlanking || afterPunct);
        }
        else if (delimChar == '~')
        {
            // StreamMark 只支持严格的双波浪删除线 ~~text~~，单 ~ 与 3+ ~ 不作为强调分隔符
            canOpen = runLength == 2 && leftFlanking;
            canClose = runLength == 2 && rightFlanking;
        }
        else
        {
            canOpen = leftFlanking;
            canClose = rightFlanking;
        }
    }

    /// <summary>
    /// 执行 CommonMark §6.2 ProcessEmphasis：在 <paramref name="delims"/>[<paramref name="stackBottom"/>..] 范围内
    /// 自左向右扫描潜在关闭符并回溯匹配开启符，原地将匹配区间改写为 <see cref="InlineNodeKind.EmphasisOpen"/> / <see cref="InlineNodeKind.EmphasisClose"/>。
    /// </summary>
    public static void ProcessEmphasis(
        InlineTape tape,
        DelimiterEntry[] delims,
        ref int delimCount,
        int stackBottom)
    {
        // openers_bottom 按 (分隔符类别, closer.OriginalCount % 3, closer.CanOpen) 缓存，确保最坏情况 O(N)
        // 0..5: '*' (mod 0..2 x canOpen 0..1), 6..11: '_', 12..13: '~'
        Span<int> openersBottom = stackalloc int[14];
        openersBottom.Fill(stackBottom);

        var closerIdx = stackBottom;
        while (closerIdx < delimCount)
        {
            if (!delims[closerIdx].Active || !delims[closerIdx].CanClose || delims[closerIdx].Count == 0)
            {
                closerIdx++;
                continue;
            }

            var ch = delims[closerIdx].Char;
            var bottomKey = ch switch
            {
                '*' => (delims[closerIdx].OriginalCount % 3) * 2 + (delims[closerIdx].CanOpen ? 1 : 0),
                '_' => 6 + (delims[closerIdx].OriginalCount % 3) * 2 + (delims[closerIdx].CanOpen ? 1 : 0),
                _ => 12 + (delims[closerIdx].CanOpen ? 1 : 0),
            };
            var bottom = openersBottom[bottomKey];

            var openerIdx = closerIdx - 1;
            var found = false;
            while (openerIdx >= bottom)
            {
                ref var op = ref delims[openerIdx];
                if (op.Active && op.CanOpen && op.Count > 0 && op.Char == ch)
                {
                    // CommonMark §6.2 Rule of 3: 若任一侧同时可开可闭，则两者原长度之和不能为 3 的倍数（除非各自都是 3 的倍数）
                    var oddMatch = ch != '~'
                        && (op.CanClose || delims[closerIdx].CanOpen)
                        && (op.OriginalCount + delims[closerIdx].OriginalCount) % 3 == 0
                        && (op.OriginalCount % 3 != 0 || delims[closerIdx].OriginalCount % 3 != 0);
                    if (!oddMatch)
                    {
                        found = true;
                        break;
                    }
                }
                openerIdx--;
            }

            if (!found)
            {
                openersBottom[bottomKey] = closerIdx;
                if (!delims[closerIdx].CanOpen)
                {
                    delims[closerIdx].Active = false;
                }
                closerIdx++;
                continue;
            }

            ref var opener = ref delims[openerIdx];
            ref var closer = ref delims[closerIdx];
            int use;
            InlineStyle style;
            if (ch == '~')
            {
                use = 2;
                style = InlineStyle.Strikethrough;
            }
            else if (opener.Count >= 2 && closer.Count >= 2)
            {
                use = 2;
                style = InlineStyle.Bold;
            }
            else
            {
                use = 1;
                style = InlineStyle.Italic;
            }

            opener.Count -= use;
            closer.Count -= use;

            // 从开启节点右侧缩减长度，紧跟其后插入 EmphasisOpen
            tape.ShrinkTextRight(opener.NodeIndex, use);
            tape.InsertAfter(opener.NodeIndex, new InlineNode
            {
                Kind = InlineNodeKind.EmphasisOpen,
                Style = style,
            });

            // 从关闭节点左侧缩减长度，紧挨其前插入 EmphasisClose
            tape.ShrinkTextLeft(closer.NodeIndex, use);
            tape.InsertBefore(closer.NodeIndex, new InlineNode
            {
                Kind = InlineNodeKind.EmphasisClose,
                Style = style,
            });

            // 使两者之间的所有分隔符失效（CommonMark 禁止交叉嵌套）
            for (var k = openerIdx + 1; k < closerIdx; k++)
            {
                delims[k].Active = false;
            }

            if (opener.Count == 0)
            {
                tape.Remove(opener.NodeIndex);
                opener.Active = false;
            }
            if (closer.Count == 0)
            {
                tape.Remove(closer.NodeIndex);
                closer.Active = false;
                closerIdx++;
            }
        }

        // 截断本次栈帧之上的分隔符
        delimCount = stackBottom;
    }
}

/// <summary>可复用的扁平侵入式双向链表容器（零堆分配行内构建）。</summary>
internal sealed class InlineTape
{
    public InlineNode[] Nodes = new InlineNode[64];
    public int Count;
    public int Head = -1;
    public int Tail = -1;

    private char[] _textPool = new char[256];
    private int _textLen;

    public void Reset()
    {
        Count = 0;
        Head = -1;
        Tail = -1;
        _textLen = 0;
    }

    public ReadOnlySpan<char> GetText(int start, int length) =>
        length <= 0 ? ReadOnlySpan<char>.Empty : _textPool.AsSpan(start, length);

    public (int Start, int Length) StoreText(ReadOnlySpan<char> text)
    {
        if (text.IsEmpty)
        {
            return (0, 0);
        }
        EnsureText(_textLen + text.Length);
        var start = _textLen;
        text.CopyTo(_textPool.AsSpan(start));
        _textLen += text.Length;
        return (start, text.Length);
    }

    public (int Start, int Length) StoreChar(char c)
    {
        EnsureText(_textLen + 1);
        var start = _textLen;
        _textPool[_textLen++] = c;
        return (start, 1);
    }

    public int Append(InlineNode node)
    {
        var idx = Alloc(node);
        Nodes[idx].Prev = Tail;
        Nodes[idx].Next = -1;
        if (Tail >= 0)
        {
            Nodes[Tail].Next = idx;
        }
        else
        {
            Head = idx;
        }
        Tail = idx;
        return idx;
    }

    public int AppendText(ReadOnlySpan<char> text)
    {
        if (text.IsEmpty)
        {
            return -1;
        }
        var (start, len) = StoreText(text);
        return Append(new InlineNode { Kind = InlineNodeKind.Text, Start = start, Length = len });
    }

    public int InsertAfter(int target, InlineNode node)
    {
        var idx = Alloc(node);
        var next = Nodes[target].Next;
        Nodes[idx].Prev = target;
        Nodes[idx].Next = next;
        Nodes[target].Next = idx;
        if (next >= 0)
        {
            Nodes[next].Prev = idx;
        }
        else
        {
            Tail = idx;
        }
        return idx;
    }

    public int InsertBefore(int target, InlineNode node)
    {
        var idx = Alloc(node);
        var prev = Nodes[target].Prev;
        Nodes[idx].Prev = prev;
        Nodes[idx].Next = target;
        Nodes[target].Prev = idx;
        if (prev >= 0)
        {
            Nodes[prev].Next = idx;
        }
        else
        {
            Head = idx;
        }
        return idx;
    }

    public void Remove(int idx)
    {
        ref var n = ref Nodes[idx];
        if (n.Kind == InlineNodeKind.Removed)
        {
            return;
        }
        var prev = n.Prev;
        var next = n.Next;
        if (prev >= 0)
        {
            Nodes[prev].Next = next;
        }
        else
        {
            Head = next;
        }
        if (next >= 0)
        {
            Nodes[next].Prev = prev;
        }
        else
        {
            Tail = prev;
        }
        n.Kind = InlineNodeKind.Removed;
        n.Prev = -1;
        n.Next = -1;
    }

    public void ShrinkTextRight(int idx, int amount)
    {
        Nodes[idx].Length = Math.Max(0, Nodes[idx].Length - amount);
    }

    public void ShrinkTextLeft(int idx, int amount)
    {
        var take = Math.Min(Nodes[idx].Length, amount);
        Nodes[idx].Start += take;
        Nodes[idx].Length -= take;
    }

    private int Alloc(InlineNode node)
    {
        if (Count >= Nodes.Length)
        {
            Array.Resize(ref Nodes, Nodes.Length * 2);
        }
        var idx = Count++;
        Nodes[idx] = node;
        return idx;
    }

    private void EnsureText(int required)
    {
        if (required <= _textPool.Length)
        {
            return;
        }
        Array.Resize(ref _textPool, Math.Max(required, _textPool.Length * 2));
    }
}
