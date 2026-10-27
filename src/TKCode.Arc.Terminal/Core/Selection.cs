using TKCode.Terminal.Core.Interop;

namespace TKCode.Terminal.Core;

/// <summary>视口滚动（02 §6.2）。</summary>
public readonly record struct ViewportScroll(ViewportScrollKind Kind, long Amount = 0)
{
    public static readonly ViewportScroll Top = new(ViewportScrollKind.Top);
    public static readonly ViewportScroll Bottom = new(ViewportScrollKind.Bottom);

    /// <summary>按行滚动：负数向上（回看历史）。</summary>
    public static ViewportScroll Lines(long delta) => new(ViewportScrollKind.Delta, delta);

    /// <summary>滚到第 <paramref name="row"/> 行（与 <see cref="ScrollbarState.Offset"/> 同一行空间）。</summary>
    public static ViewportScroll ToRow(long row) => new(ViewportScrollKind.Row, row);

    /// <summary>按 OSC 133 提示符跳转：-1 上一条命令，1 下一条。</summary>
    public static ViewportScroll Prompts(long delta) => new(ViewportScrollKind.Prompt, delta);
}

public enum ViewportScrollKind
{
    Top,
    Bottom,
    Delta,
    Row,
    Prompt,
}

/// <summary>视口中的一个格子（列、行，从 0 开始）。</summary>
public readonly record struct ViewportPoint(int Column, int Row);

public enum SelectionKind
{
    /// <summary>两点之间（线性，或 <see cref="SelectionRequest.Rectangle"/> 时矩形）。</summary>
    Range,
    Word,
    Line,
    All,

    /// <summary>某条命令的输出（依赖 OSC 133 语义标记）。</summary>
    CommandOutput,
}

public readonly record struct SelectionRequest(SelectionKind Kind, ViewportPoint Start, ViewportPoint End = default, bool Rectangle = false)
{
    public static SelectionRequest Range(ViewportPoint start, ViewportPoint end, bool rectangle = false) => new(SelectionKind.Range, start, end, rectangle);
    public static SelectionRequest Word(ViewportPoint at) => new(SelectionKind.Word, at);
    public static SelectionRequest Line(ViewportPoint at) => new(SelectionKind.Line, at);
    public static SelectionRequest All() => new(SelectionKind.All, default);
    public static SelectionRequest CommandOutput(ViewportPoint at) => new(SelectionKind.CommandOutput, at);
}

public enum TextFormat
{
    Plain,
    Vt,
    Html,
}

/// <summary>视口内的一个高亮区间（含两端）：选区与搜索匹配都用它表示。</summary>
public readonly record struct CellSpan(int Row, int StartColumn, int EndColumn);

/// <summary>视口内的搜索高亮：所有匹配与当前选中的匹配。</summary>
public sealed record SearchHighlights(CellSpan[] Matches, CellSpan[] Selected, long TotalMatches, long? SelectedIndex);

/// <summary>
/// 终端搜索（02 §6.2，覆盖主屏、备用屏与回滚）。<see cref="Update"/> 需要终端访问（actor 线程）；
/// 匹配结果在下一次终端变更前有效，所以视口高亮在 <see cref="TerminalCore.CaptureFrame"/> 内换算。
/// </summary>
public sealed unsafe class TerminalSearch : IDisposable
{
    private readonly TerminalCore _core;
    private nint _search;
    private string _needle = string.Empty;

    internal TerminalSearch(TerminalCore core)
    {
        _core = core;
        nint search;
        TerminalCore.Check(GhosttyNative.SearchNew(null, &search, core.Handle));
        _search = search;
    }

    internal nint Handle => _search;

    public string Needle
    {
        get => _needle;
        set
        {
            if (_needle == value)
            {
                return;
            }
            _needle = value;
            var bytes = System.Text.Encoding.UTF8.GetBytes(value);
            fixed (byte* p = bytes)
            {
                var s = new GhosttyString { Ptr = p, Len = (nuint)bytes.Length };
                TerminalCore.Check(GhosttyNative.SearchSet(_search, GhosttySearchOption.Needle, value.Length == 0 ? null : &s));
            }
        }
    }

    /// <summary>与终端同步并完成搜索（feed + tick 直到完成）。终端每批写入之后、捕获帧之前调用。</summary>
    public void Update()
    {
        if (_needle.Length == 0)
        {
            return;
        }
        GhosttySearchStatus status;
        for (var i = 0; i < 1_000_000; i++)
        {
            TerminalCore.Check(GhosttyNative.SearchTick(_search, &status));
            if (status == GhosttySearchStatus.Complete)
            {
                return;
            }
            if (status == GhosttySearchStatus.FeedRequired)
            {
                TerminalCore.Check(GhosttyNative.SearchFeed(_search));
            }
        }
    }

    public long TotalMatches
    {
        get
        {
            nuint total;
            return GhosttyNative.SearchGet(_search, GhosttySearchData.TotalMatches, &total) == GhosttyResult.Success ? (long)total : 0;
        }
    }

    /// <summary>选中匹配的序号（0 = 最新的匹配），没有选中时为 null。</summary>
    public long? SelectedIndex
    {
        get
        {
            nuint index;
            return GhosttyNative.SearchGet(_search, GhosttySearchData.SelectedIndex, &index) == GhosttyResult.Success ? (long)index : null;
        }
    }

    /// <summary>选中下一个（更旧的）匹配，必要时滚动视口。</summary>
    public void Next() => TerminalCore.Check(GhosttyNative.SearchSet(_search, GhosttySearchOption.SelectNext, null));

    /// <summary>选中上一个（更新的）匹配。</summary>
    public void Previous() => TerminalCore.Check(GhosttyNative.SearchSet(_search, GhosttySearchOption.SelectPrev, null));

    internal SearchHighlights? Highlights(TerminalCore core, int rows, int cols)
    {
        if (_needle.Length == 0)
        {
            return null;
        }
        var matches = new List<CellSpan>();
        var buffer = new GhosttySelectionBuffer();
        var r = GhosttyNative.SearchGet(_search, GhosttySearchData.ViewportMatches, &buffer);
        if (r == GhosttyResult.OutOfSpace && buffer.Len > 0)
        {
            var items = new GhosttySelection[(int)buffer.Len];
            fixed (GhosttySelection* p = items)
            {
                buffer.Ptr = p;
                buffer.Cap = (ulong)items.Length;
                TerminalCore.Check(GhosttyNative.SearchGet(_search, GhosttySearchData.ViewportMatches, &buffer));
                for (var i = 0; i < (int)buffer.Len; i++)
                {
                    core.SelectionSpans(&p[i], rows, cols, matches);
                }
            }
        }
        var selected = new List<CellSpan>();
        var current = GhosttySelection.Create();
        if (GhosttyNative.SearchGet(_search, GhosttySearchData.SelectedMatch, &current) == GhosttyResult.Success)
        {
            core.SelectionSpans(&current, rows, cols, selected);
        }
        return new SearchHighlights(matches.ToArray(), selected.ToArray(), TotalMatches, SelectedIndex);
    }

    public void Dispose()
    {
        if (_search != 0)
        {
            // 必须与终端访问串行（search.h），由 actor 线程调用
            GhosttyNative.SearchFree(_search);
            _search = 0;
        }
    }
}
