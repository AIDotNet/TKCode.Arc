using TKCode.Terminal.Core.Interop;

namespace TKCode.Terminal.Core;

/// <summary>
/// render state → <see cref="FrameSnapshot"/>（02 §8）。只在 UI 侧核心（View 角色）上使用；每个终端只能有一个
/// render state，因为 update 会"消费"终端的 dirty 状态。
/// 增量：只重建脏行，其余行沿用上一帧同一位置的 <see cref="RowSnapshot"/> 对象；内容哈希不变时连对象都复用。
/// </summary>
internal sealed unsafe class FrameCapturer : IDisposable
{
    private readonly GhosttyCellLayout _layout = GhosttyCellLayout.Current;
    private readonly Dictionary<(ulong, ulong), RowSnapshot> _rowsById = [];
    private readonly TerminalCore _core;
    private readonly Dictionary<(ushort Sid, uint Bg, int Link), int> _styleIndex = [];
    private readonly Dictionary<string, int> _linkIds = new(StringComparer.Ordinal);
    private readonly List<string> _links = [];
    private string[] _linksPublished = [];
    private readonly List<CellSpan> _spanBuffer = [];
    private readonly List<ResolvedStyle> _styles = [];
    private readonly Dictionary<int, int[]> _graphemes = [];
    private readonly uint[] _graphemeBuffer = new uint[64];
    private nint _state;
    private nint _rows;
    private nint _cells;
    private FrameSnapshot? _previous;
    private TerminalPalette? _palette;
    private ulong _nextRowId;
    private long _generation;

    /// <summary>超链接表上限：超出后新地址不再分配 id（HyperlinkId = 0，视图回退到 <see cref="TerminalCore.HyperlinkAt"/>）。</summary>
    public const int MaxLinks = 65535;

    public FrameCapturer(TerminalCore core)
    {
        _core = core;
        nint state, rows, cells;
        TerminalCore.Check(GhosttyNative.RenderStateNew(null, &state));
        TerminalCore.Check(GhosttyNative.RenderStateRowIteratorNew(null, &rows));
        TerminalCore.Check(GhosttyNative.RenderStateRowCellsNew(null, &cells));
        (_state, _rows, _cells) = (state, rows, cells);
    }

    /// <summary>返回新的一帧；没有任何变化时返回 null（除非 <paramref name="force"/>）。</summary>
    public FrameSnapshot? Capture(nint terminal, bool force)
    {
        TerminalCore.Check(GhosttyNative.RenderStateUpdate(_state, terminal));
        GhosttyRenderStateDirty dirty;
        ushort cols, rows;
        TerminalCore.Check(GhosttyNative.RenderStateGet(_state, GhosttyRenderStateData.Dirty, &dirty));
        TerminalCore.Check(GhosttyNative.RenderStateGet(_state, GhosttyRenderStateData.Cols, &cols));
        TerminalCore.Check(GhosttyNative.RenderStateGet(_state, GhosttyRenderStateData.Rows, &rows));
        var cursor = ReadCursor();
        var scrollbar = ReadScrollbar(terminal);

        var selection = ReadSelection(rows, cols);
        var search = _core.ActiveSearch?.Highlights(_core, rows, cols);

        var previous = _previous;
        var resized = previous is null || previous.Columns != cols || previous.Rows.Length != rows;
        var full = resized || dirty == GhosttyRenderStateDirty.Full;
        if (!full && dirty == GhosttyRenderStateDirty.False && !force && previous!.Cursor == cursor && previous.Scrollbar == scrollbar &&
            previous.Selection.AsSpan().SequenceEqual(selection) && SameSearch(previous.Search, search))
        {
            return null;
        }
        if (full || _palette is null)
        {
            _palette = ReadPalette();
        }

        var result = full ? new RowSnapshot[rows] : (RowSnapshot[])previous!.Rows.Clone();
        var iterator = _rows;
        TerminalCore.Check(GhosttyNative.RenderStateGet(_state, GhosttyRenderStateData.RowIterator, &iterator));
        if (full)
        {
            for (var y = 0; y < rows && GhosttyNative.RenderStateRowIteratorNext(_rows); y++)
            {
                result[y] = CaptureRow(cols, y);
            }
            for (var y = 0; y < rows; y++)
            {
                result[y] ??= EmptyRow(cols);
            }
        }
        else
        {
            ushort y;
            while (GhosttyNative.RenderStateRowIteratorNextDirty(_rows, &y))
            {
                if (y < rows)
                {
                    result[y] = CaptureRow(cols, y);
                }
            }
        }
        TerminalCore.Check(GhosttyNative.RenderStateClean(_state));
        ForgetRowsNotIn(result);

        var frame = new FrameSnapshot
        {
            Generation = ++_generation,
            Columns = cols,
            Rows = result,
            Cursor = cursor,
            Palette = _palette,
            Scrollbar = scrollbar,
            Selection = previous is not null && previous.Selection.AsSpan().SequenceEqual(selection) ? previous.Selection : selection,
            Search = previous is not null && SameSearch(previous.Search, search) ? previous.Search : search,
            Links = PublishLinks(),
        };
        _previous = frame;
        return frame;
    }

    private RowSnapshot CaptureRow(int cols, int y)
    {
        GhosttyRenderStateRowId id;
        GhosttyCellsView view;
        ulong rawRow;
        TerminalCore.Check(GhosttyNative.RenderStateRowGet(_rows, GhosttyRenderStateRowData.Id, &id));
        TerminalCore.Check(GhosttyNative.RenderStateRowGet(_rows, GhosttyRenderStateRowData.CellsRaw, &view));
        TerminalCore.Check(GhosttyNative.RenderStateRowGet(_rows, GhosttyRenderStateRowData.Raw, &rawRow));
        var cellsHandle = _cells;
        TerminalCore.Check(GhosttyNative.RenderStateRowGet(_rows, GhosttyRenderStateRowData.Cells, &cellsHandle));

        _styleIndex.Clear();
        _styles.Clear();
        _styles.Add(ResolvedStyle.Default);
        _graphemes.Clear();
        var rowHasLink = RowBool(rawRow, GhosttyRowData.Hyperlink);

        var cells = new ulong[cols];
        var count = (int)Math.Min((ulong)cols, view.Len);
        var L = _layout;
        for (var x = 0; x < count; x++)
        {
            var g = view.Ptr[x];
            var tag = (uint)(g >> L.TagShift) & 3;
            var content = (uint)(g >> L.ContentShift) & 0xFFFFFF;
            var sid = (ushort)(g >> L.StyleShift);
            var wide = (CellWidth)((g >> L.WideShift) & 3);
            var link = ((g >> L.HyperlinkShift) & 1) != 0;
            var semantic = (SemanticContent)((g >> L.SemanticShift) & 3);

            uint cp = 0, bg = 0;
            var grapheme = false;
            switch (tag)
            {
                case 0:
                    cp = content & 0x1FFFFF;
                    break;
                case 1:
                    cp = content & 0x1FFFFF;
                    grapheme = CaptureGrapheme(x);
                    break;
                case 2:
                    bg = TerminalColor.Palette((byte)content).Raw;
                    break;
                default:
                    bg = TerminalColor.Rgb((byte)content, (byte)(content >> 8), (byte)(content >> 16)).Raw;
                    break;
            }
            var linkId = link && rowHasLink ? LinkId(x, y) : 0;
            var style = sid == 0 && bg == 0 && linkId == 0 ? 0 : InternStyle(x, sid, bg, linkId);
            cells[x] = TCell.Pack(cp, wide, grapheme, style, semantic, link, cp == 0x10EEEE);
        }

        var flags = RowFlags.None;
        if (RowBool(rawRow, GhosttyRowData.Wrap)) flags |= RowFlags.SoftWrapped;
        if (RowBool(rawRow, GhosttyRowData.WrapContinuation)) flags |= RowFlags.WrapContinuation;
        if (rowHasLink) flags |= RowFlags.HasHyperlink;
        if (RowBool(rawRow, GhosttyRowData.KittyVirtualPlaceholder)) flags |= RowFlags.KittyPlaceholder;
        if (RowPrompt(rawRow)) flags |= RowFlags.PromptLine;

        var styles = _styles.ToArray();
        var graphemes = _graphemes.Count == 0 ? null : new Dictionary<int, int[]>(_graphemes);
        var hash = Hash(cells, styles, graphemes, flags);

        var key = (id.Bits[0], id.Bits[1]);
        if (_rowsById.TryGetValue(key, out var existing))
        {
            if (existing.ContentHash == hash && existing.Cells.Length == cols)
            {
                return existing;  // "保守标脏"导致的无效重建在这里被吸收（02 §8.3）
            }
            var updated = new RowSnapshot
            {
                RowId = existing.RowId, Version = existing.Version + 1, ContentHash = hash, Cells = cells, Styles = styles, Graphemes = graphemes, Flags = flags,
            };
            _rowsById[key] = updated;
            return updated;
        }
        var row = new RowSnapshot
        {
            RowId = ++_nextRowId, Version = 1, ContentHash = hash, Cells = cells, Styles = styles, Graphemes = graphemes, Flags = flags,
        };
        _rowsById[key] = row;
        return row;
    }

    private int InternStyle(int x, ushort sid, uint bgOverride, int linkId)
    {
        if (_styleIndex.TryGetValue((sid, bgOverride, linkId), out var index))
        {
            return index;
        }
        var style = ResolvedStyle.Default;
        if (sid != 0)
        {
            TerminalCore.Check(GhosttyNative.RenderStateRowCellsSelect(_cells, (ushort)x));
            var s = GhosttyStyle.Create();
            TerminalCore.Check(GhosttyNative.RenderStateRowCellsGet(_cells, GhosttyRenderStateRowCellsData.Style, &s));
            var flags = StyleFlags.None;
            if (s.Bold) flags |= StyleFlags.Bold;
            if (s.Italic) flags |= StyleFlags.Italic;
            if (s.Faint) flags |= StyleFlags.Faint;
            if (s.Blink) flags |= StyleFlags.Blink;
            if (s.Inverse) flags |= StyleFlags.Inverse;
            if (s.Invisible) flags |= StyleFlags.Invisible;
            if (s.Strikethrough) flags |= StyleFlags.Strikethrough;
            if (s.Overline) flags |= StyleFlags.Overline;
            style = new ResolvedStyle(Color(s.FgColor), Color(s.BgColor), Color(s.UnderlineColor), flags, (UnderlineStyle)Math.Clamp(s.Underline, 0, 5));
        }
        if (bgOverride != 0)
        {
            style = style with { Background = new TerminalColor(bgOverride) };
        }
        if (linkId != 0)
        {
            style = style with { HyperlinkId = linkId };
        }
        index = _styles.Count;
        _styles.Add(style);
        _styleIndex[(sid, bgOverride, linkId)] = index;
        return index;
    }

    /// <summary>OSC 8 地址 → 帧内超链接 id（从 1 开始，0 = 无）。同一地址跨帧 id 不变，行快照可以复用。</summary>
    private int LinkId(int x, int y)
    {
        var p = new GhosttyPoint { Tag = GhosttyPointTag.Viewport };
        p.Value.Coordinate = new GhosttyPointCoordinate { X = (ushort)x, Y = (uint)y };
        var gridRef = GhosttyGridRef.Create();
        if (GhosttyNative.TerminalGridRef(_core.Handle, p, &gridRef) != GhosttyResult.Success ||
            TerminalCore.HyperlinkOf(&gridRef) is not { } uri)
        {
            return 0;
        }
        if (_linkIds.TryGetValue(uri, out var id))
        {
            return id;
        }
        if (_links.Count >= MaxLinks)
        {
            return 0;
        }
        _links.Add(uri);
        id = _links.Count;
        _linkIds[uri] = id;
        return id;
    }

    private string[] PublishLinks()
    {
        if (_linksPublished.Length != _links.Count)
        {
            _linksPublished = _links.ToArray();
        }
        return _linksPublished;
    }

    private CellSpan[] ReadSelection(int rows, int cols)
    {
        var selection = GhosttySelection.Create();
        if (GhosttyNative.TerminalGet(_core.Handle, GhosttyTerminalData.Selection, &selection) != GhosttyResult.Success)
        {
            return [];
        }
        _spanBuffer.Clear();
        _core.SelectionSpans(&selection, rows, cols, _spanBuffer);
        return _spanBuffer.Count == 0 ? [] : _spanBuffer.ToArray();
    }

    private static bool SameSearch(SearchHighlights? a, SearchHighlights? b) =>
        ReferenceEquals(a, b) || (a is not null && b is not null && a.TotalMatches == b.TotalMatches && a.SelectedIndex == b.SelectedIndex &&
                                  a.Matches.AsSpan().SequenceEqual(b.Matches) && a.Selected.AsSpan().SequenceEqual(b.Selected));

    private bool CaptureGrapheme(int x)
    {
        TerminalCore.Check(GhosttyNative.RenderStateRowCellsSelect(_cells, (ushort)x));
        uint length;
        TerminalCore.Check(GhosttyNative.RenderStateRowCellsGet(_cells, GhosttyRenderStateRowCellsData.GraphemesLen, &length));
        if (length <= 1 || length > _graphemeBuffer.Length)
        {
            return false;
        }
        fixed (uint* buffer = _graphemeBuffer)
        {
            TerminalCore.Check(GhosttyNative.RenderStateRowCellsGet(_cells, GhosttyRenderStateRowCellsData.GraphemesBuf, buffer));
        }
        var extra = new int[length - 1];
        for (var i = 1; i < length; i++)
        {
            extra[i - 1] = (int)_graphemeBuffer[i];
        }
        _graphemes[x] = extra;
        return true;
    }

    private static TerminalColor Color(in GhosttyStyleColor c) => c.Tag switch
    {
        GhosttyStyleColorTag.Palette => TerminalColor.Palette(c.Value.Palette),
        GhosttyStyleColorTag.Rgb => TerminalColor.Rgb(c.Value.Rgb.R, c.Value.Rgb.G, c.Value.Rgb.B),
        _ => TerminalColor.Default,
    };

    private static bool RowBool(ulong row, GhosttyRowData data)
    {
        bool value;
        return GhosttyNative.RowGet(row, data, &value) == GhosttyResult.Success && value;
    }

    private static bool RowPrompt(ulong row)
    {
        int value;
        return GhosttyNative.RowGet(row, GhosttyRowData.SemanticPrompt, &value) == GhosttyResult.Success && value != 0;
    }

    private CursorState ReadCursor()
    {
        var c = GhosttyRenderStateCursor.Create();
        TerminalCore.Check(GhosttyNative.RenderStateGet(_state, GhosttyRenderStateData.Cursor, &c));
        return new CursorState(c.ViewportX, c.ViewportY, c.ViewportHasValue && c.Visible, (CursorStyle)(int)c.VisualStyle,
            c.Blinking, c.WideTail, c.PasswordInput);
    }

    private static ScrollbarState ReadScrollbar(nint terminal)
    {
        GhosttyTerminalScrollbar s;
        return GhosttyNative.TerminalGet(terminal, GhosttyTerminalData.Scrollbar, &s) == GhosttyResult.Success
            ? new ScrollbarState((long)s.Total, (long)s.Offset, (long)s.Len)
            : default;
    }

    private TerminalPalette ReadPalette()
    {
        var c = GhosttyRenderStateColors.Create();
        TerminalCore.Check(GhosttyNative.RenderStateGet(_state, GhosttyRenderStateData.Colors, &c));
        var colors = new TerminalRgb[256];
        var raw = (GhosttyColorRgb*)c.PaletteRaw;
        for (var i = 0; i < 256; i++)
        {
            colors[i] = new TerminalRgb(raw[i].R, raw[i].G, raw[i].B);
        }
        static TerminalRgb Rgb(GhosttyColorRgb v) => new(v.R, v.G, v.B);
        var palette = new TerminalPalette(Rgb(c.Foreground), Rgb(c.Background), c.CursorHasValue ? Rgb(c.Cursor) : null, colors);
        // 调色板没变时沿用旧对象，渲染层可以按引用判断
        return _palette is { } old && old.Foreground == palette.Foreground && old.Background == palette.Background &&
               old.Cursor == palette.Cursor && old.Colors.AsSpan().SequenceEqual(colors) ? old : palette;
    }

    private RowSnapshot EmptyRow(int cols) => new()
    {
        RowId = ++_nextRowId, Version = 1, ContentHash = 0, Cells = new ulong[cols], Styles = [ResolvedStyle.Default], Graphemes = null, Flags = RowFlags.None,
    };

    private void ForgetRowsNotIn(RowSnapshot[] rows)
    {
        if (_rowsById.Count <= rows.Length * 2)
        {
            return;
        }
        var alive = new HashSet<RowSnapshot>(rows, ReferenceEqualityComparer.Instance);
        foreach (var key in _rowsById.Where(p => !alive.Contains(p.Value)).Select(p => p.Key).ToList())
        {
            _rowsById.Remove(key);
        }
    }

    /// <summary>64 位内容哈希（FNV-1a 风格的逐 ulong 混合；只在进程内使用，不持久化）。</summary>
    private static ulong Hash(ulong[] cells, ResolvedStyle[] styles, Dictionary<int, int[]>? graphemes, RowFlags flags)
    {
        const ulong prime = 0x100000001B3UL;
        var h = 0xCBF29CE484222325UL ^ (ulong)flags;
        foreach (var c in cells)
        {
            h = (h ^ c) * prime;
            h ^= h >> 29;
        }
        foreach (var s in styles)
        {
            h = (h ^ s.Foreground.Raw ^ ((ulong)s.Background.Raw << 32)) * prime;
            h = (h ^ s.UnderlineColor.Raw ^ ((ulong)s.Flags << 32) ^ ((ulong)s.Underline << 40) ^ ((ulong)(uint)s.HyperlinkId << 44)) * prime;
        }
        if (graphemes is not null)
        {
            foreach (var (col, extra) in graphemes.OrderBy(p => p.Key))
            {
                h = (h ^ (ulong)col) * prime;
                foreach (var cp in extra)
                {
                    h = (h ^ (ulong)cp) * prime;
                }
            }
        }
        return h;
    }

    public void Dispose()
    {
        if (_state == 0)
        {
            return;
        }
        GhosttyNative.RenderStateRowCellsFree(_cells);
        GhosttyNative.RenderStateRowIteratorFree(_rows);
        GhosttyNative.RenderStateFree(_state);
        _state = 0;
    }
}
