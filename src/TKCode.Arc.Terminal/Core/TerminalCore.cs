using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using TKCode.Terminal.Core.Interop;

namespace TKCode.Terminal.Core;

/// <summary>
/// 一个终端状态机（libghostty-vt）。非线程安全：只能由其所属的 actor 线程调用
/// （docs/terminal/01-architecture.md §6.1、02-core-ghostty.md §6、§14）。
/// 镜像核心（pty host）与 UI 侧核心使用同一个类型，区别只在传入的 <see cref="ITerminalEffects"/>（02 §7.2）。
/// </summary>
public sealed unsafe class TerminalCore : IDisposable
{
    private nint _terminal;
    private GCHandle<TerminalCore> _self;
    private readonly ITerminalEffects _effects;
    private readonly CommandTracker _commands;
    private TerminalReplyProfile _replies;
    private readonly ConformanceMode? _conformance;
    private ColorScheme _scheme;
    private TerminalSize _size;
    private bool _inCallback;
    private FrameCapturer? _capturer;
    private TerminalSearch? _search;
    private System.Buffers.IBufferWriter<byte>? _pasteCapture;
    private nint _xtversion;
    private nint _terminfoName;

    private TerminalCore(nint terminal, ITerminalEffects effects, in TerminalCoreOptions options)
    {
        _terminal = terminal;
        _effects = effects;
        _replies = options.Replies;
        _conformance = options.Conformance;
        _scheme = options.Replies.Scheme;
        _size = options.Size;
        _self = new GCHandle<TerminalCore>(this);
        _commands = new CommandTracker(this);
        InstallCallbacks();
    }

    public static TerminalCore Create(in TerminalCoreOptions options, ITerminalEffects? effects = null)
    {
        GhosttyRuntime.EnsureInitialized();
        nint terminal;
        Check(GhosttyNative.TerminalNew(null, &terminal, checked((ushort)options.Size.Columns), checked((ushort)options.Size.Rows)));
        var core = new TerminalCore(terminal, effects ?? NullTerminalEffects.Instance, options);
        core.Configure(options, applyModeDefaults: true);
        return core;
    }

    /// <summary>从 <see cref="EncodeSnapshot"/> 的结果恢复（同一 libghostty 构建之间使用）。</summary>
    public static TerminalCore Restore(ReadOnlySpan<byte> snapshot, in TerminalCoreOptions options, ITerminalEffects? effects = null)
    {
        GhosttyRuntime.EnsureInitialized();
        nint decoder, terminal = 0;
        fixed (byte* data = snapshot)
        {
            Check(GhosttyNative.SnapshotDecoderNewBuf(null, &decoder, data, (nuint)snapshot.Length));
            try
            {
                // 恢复后继续跟踪未完成的输入，保证可以再次编码快照（重连后再重连）
                nuint maxContinuation = (nuint)options.ContinuationMaxBytes;
                bool retain = true;
                Check(GhosttyNative.SnapshotDecoderSet(decoder, GhosttySnapshotDecoderOption.MaxContinuationBytes, &maxContinuation));
                Check(GhosttyNative.SnapshotDecoderSet(decoder, GhosttySnapshotDecoderOption.RetainContinuation, &retain));
                Check(GhosttyNative.SnapshotDecoderDecode(decoder, &terminal));
            }
            finally
            {
                GhosttyNative.SnapshotDecoderFree(decoder);
            }
        }
        // 快照恢复终端状态（含模式与回滚上限），不恢复宿主回调与只属于本进程的选项，所以重新配置；模式默认值已在快照里
        var core = new TerminalCore(terminal, effects ?? NullTerminalEffects.Instance, options);
        core.Configure(options, applyModeDefaults: false);
        return core;
    }

    public TerminalSize Size
    {
        get
        {
            ushort cols, rows;
            Check(GhosttyNative.TerminalGet(Handle, GhosttyTerminalData.Cols, &cols));
            Check(GhosttyNative.TerminalGet(Handle, GhosttyTerminalData.Rows, &rows));
            return new TerminalSize(cols, rows, _size.CellWidth, _size.CellHeight);
        }
    }

    public (int Column, int Row) Cursor
    {
        get
        {
            ushort x, y;
            Check(GhosttyNative.TerminalGet(Handle, GhosttyTerminalData.CursorX, &x));
            Check(GhosttyNative.TerminalGet(Handle, GhosttyTerminalData.CursorY, &y));
            return (x, y);
        }
    }

    /// <summary>解析器是否处于 ground 状态（没有未完成的转义序列）。host 只在 ground 时追加 Clear 记录。</summary>
    public bool IsParserGround
    {
        get
        {
            bool ground;
            Check(GhosttyNative.TerminalGet(Handle, GhosttyTerminalData.VtGround, &ground));
            return ground;
        }
    }

    /// <summary>光标是否在 shell 提示符或输入区（依赖 OSC 133；备用屏时为 false）。关闭确认用它判断"空闲"。</summary>
    public bool CursorAtPrompt
    {
        get
        {
            bool atPrompt;
            Check(GhosttyNative.TerminalGet(Handle, GhosttyTerminalData.CursorAtPrompt, &atPrompt));
            return atPrompt;
        }
    }

    /// <summary>OSC 0/2 设置的标题；没有时为空字符串。</summary>
    public string Title => ReadString(GhosttyTerminalData.Title);

    /// <summary>OSC 7 / 9;9 / 1337 设置的工作目录原文（file:// URI 或路径，不可信输入）。</summary>
    public string WorkingDirectoryRaw => ReadString(GhosttyTerminalData.Pwd);

    /// <summary>OSC 133 / 633 命令记录（镜像核心）。</summary>
    public CommandTracker Commands => _commands;

    public void Write(ReadOnlySpan<byte> data)
    {
        EnsureNotInCallback();
        fixed (byte* p = data)
        {
            GhosttyNative.TerminalVtWrite(Handle, p, (nuint)data.Length);
        }
    }

    public void Resize(TerminalSize size)
    {
        EnsureNotInCallback();
        _size = size;
        Check(GhosttyNative.TerminalResize(Handle, checked((ushort)size.Columns), checked((ushort)size.Rows),
            (uint)size.CellWidth, (uint)size.CellHeight));
    }

    public void Reset()
    {
        EnsureNotInCallback();
        GhosttyNative.TerminalReset(Handle);
    }

    /// <summary>
    /// ⌘K 清屏（04 的 Clear 记录，02 §6.6）：C API 没有清屏函数，注入等价序列。只能在解析器处于 ground 时调用，
    /// 否则会切断程序的半截序列；注入的字节不经过 PTY，也没有应答。
    /// </summary>
    public void Clear(ClearTarget target)
    {
        EnsureNotInCallback();
        if (!IsParserGround)
        {
            throw new InvalidOperationException("Clear must be applied at a parser ground boundary.");
        }
        var bytes = target switch
        {
            ClearTarget.Scrollback => "\x1b[3J"u8,
            ClearTarget.Screen => "\x1b[H\x1b[2J"u8,
            _ => "\x1b[H\x1b[2J\x1b[3J"u8,
        };
        fixed (byte* p = bytes)
        {
            GhosttyNative.TerminalVtWrite(Handle, p, (nuint)bytes.Length);
        }
    }

    /// <summary>默认颜色与明暗（日志中的 SetColors 记录，01 §3.2 规则 5）。OSC 4/10/11 查询与 996 / 2031 以此作答。</summary>
    public void SetDefaultColors(TerminalPalette palette, ColorScheme scheme)
    {
        EnsureNotInCallback();
        _scheme = scheme;
        var fg = new GhosttyColorRgb { R = palette.Foreground.R, G = palette.Foreground.G, B = palette.Foreground.B };
        var bg = new GhosttyColorRgb { R = palette.Background.R, G = palette.Background.G, B = palette.Background.B };
        Check(GhosttyNative.TerminalSet(Handle, GhosttyTerminalOption.ColorForeground, &fg));
        Check(GhosttyNative.TerminalSet(Handle, GhosttyTerminalOption.ColorBackground, &bg));
        if (palette.Cursor is { } c)
        {
            var cursor = new GhosttyColorRgb { R = c.R, G = c.G, B = c.B };
            Check(GhosttyNative.TerminalSet(Handle, GhosttyTerminalOption.ColorCursor, &cursor));
        }
        else
        {
            Check(GhosttyNative.TerminalSet(Handle, GhosttyTerminalOption.ColorCursor, null));
        }
        if (palette.Colors.Length != 256)
        {
            throw new ArgumentException("The palette must have 256 colors.", nameof(palette));
        }
        var colors = stackalloc GhosttyColorRgb[256];
        for (var i = 0; i < 256; i++)
        {
            colors[i] = new GhosttyColorRgb { R = palette.Colors[i].R, G = palette.Colors[i].G, B = palette.Colors[i].B };
        }
        Check(GhosttyNative.TerminalSet(Handle, GhosttyTerminalOption.ColorPalette, colors));
    }

    /// <summary>
    /// 补发一次延后的剪贴板读取应答（O3，01 §3.2 规则 6）：<paramref name="data"/> 为 null 表示拒绝（不应答）。
    /// 应答直接经 <see cref="ITerminalEffects.WriteToPty"/> 写回，使用与请求相同的终止符。
    /// </summary>
    public void CompleteClipboardRead(in PendingClipboardRead request, ReadOnlySpan<byte> data)
    {
        EnsureNotInCallback();
        var kind = request.Target switch { ClipboardTarget.Selection => 's', ClipboardTarget.Primary => 'p', _ => 'c' };
        var reply = $"\x1b]52;{kind};{Convert.ToBase64String(data)}{(request.BelTerminator ? "\a" : "\x1b\\")}";
        _effects.WriteToPty(Encoding.ASCII.GetBytes(reply));
    }

    /// <summary>滚动视口（UI 侧核心；不改变终端内容，不需要同步到镜像核心）。</summary>
    public void ScrollViewport(ViewportScroll scroll)
    {
        EnsureNotInCallback();
        var behavior = new GhosttyTerminalScrollViewport
        {
            Tag = scroll.Kind switch
            {
                ViewportScrollKind.Top => GhosttyTerminalScrollViewportTag.Top,
                ViewportScrollKind.Bottom => GhosttyTerminalScrollViewportTag.Bottom,
                ViewportScrollKind.Row => GhosttyTerminalScrollViewportTag.Row,
                ViewportScrollKind.Prompt => GhosttyTerminalScrollViewportTag.DeltaPrompt,
                _ => GhosttyTerminalScrollViewportTag.Delta,
            },
        };
        if (scroll.Kind == ViewportScrollKind.Row)
        {
            behavior.Value.Row = (ulong)Math.Max(0, scroll.Amount);
        }
        else
        {
            behavior.Value.Delta = scroll.Amount;
        }
        GhosttyNative.TerminalScrollViewport(Handle, behavior);
    }

    /// <summary>设置选区（UI 侧核心）。点击位置没有可选内容时返回 false（例如 CommandOutput 没有语义标记）。</summary>
    public bool Select(in SelectionRequest request)
    {
        EnsureNotInCallback();
        var selection = GhosttySelection.Create();
        GhosttyResult r;
        switch (request.Kind)
        {
            case SelectionKind.All:
                r = GhosttyNative.TerminalSelectAll(Handle, &selection);
                break;
            case SelectionKind.Word:
            {
                var options = GhosttyTerminalSelectWordOptions.Create();
                options.Ref = GridRef(request.Start);
                r = GhosttyNative.TerminalSelectWord(Handle, &options, &selection);
                break;
            }
            case SelectionKind.Line:
            {
                var options = GhosttyTerminalSelectLineOptions.Create();
                options.Ref = GridRef(request.Start);
                options.SemanticPromptBoundary = true;
                r = GhosttyNative.TerminalSelectLine(Handle, &options, &selection);
                break;
            }
            case SelectionKind.CommandOutput:
                r = GhosttyNative.TerminalSelectOutput(Handle, GridRef(request.Start), &selection);
                break;
            default:
                selection.Start = GridRef(request.Start);
                selection.End = GridRef(request.End);
                selection.Rectangle = request.Rectangle;
                r = GhosttyResult.Success;
                break;
        }
        if (r != GhosttyResult.Success)
        {
            return false;
        }
        Check(GhosttyNative.TerminalSet(Handle, GhosttyTerminalOption.Selection, &selection));
        return true;
    }

    public void ClearSelection()
    {
        EnsureNotInCallback();
        Check(GhosttyNative.TerminalSet(Handle, GhosttyTerminalOption.Selection, null));
    }

    public bool HasSelection
    {
        get
        {
            var selection = GhosttySelection.Create();
            return GhosttyNative.TerminalGet(Handle, GhosttyTerminalData.Selection, &selection) == GhosttyResult.Success;
        }
    }

    /// <summary>当前选区的文本；没有选区时返回 null。软换行合并、去掉行尾空白。</summary>
    public string? GetSelectionText(TextFormat format = TextFormat.Plain)
    {
        EnsureNotInCallback();
        var selection = GhosttySelection.Create();
        if (GhosttyNative.TerminalGet(Handle, GhosttyTerminalData.Selection, &selection) != GhosttyResult.Success)
        {
            return null;
        }
        var options = GhosttyTerminalSelectionFormatOptions.Create();
        options.Emit = (GhosttyFormatterFormat)(int)format;
        options.Unwrap = true;
        options.Trim = true;
        options.Selection = &selection;
        byte* output;
        nuint length;
        Check(GhosttyNative.TerminalSelectionFormatAlloc(Handle, null, options, &output, &length));
        return TakeString(output, length);
    }

    /// <summary>创建搜索；同时作为 <see cref="CaptureFrame"/> 的高亮来源（一个核心同时最多一个活动搜索）。</summary>
    public TerminalSearch CreateSearch()
    {
        EnsureNotInCallback();
        _search?.Dispose();
        _search = new TerminalSearch(this);
        return _search;
    }

    /// <summary>结束搜索并释放（必须在 actor 线程）。</summary>
    public void EndSearch()
    {
        _search?.Dispose();
        _search = null;
    }

    /// <summary>视口某格的 OSC 8 超链接地址；没有时返回 null。</summary>
    public string? HyperlinkAt(ViewportPoint point)
    {
        EnsureNotInCallback();
        var gridRef = GridRef(point);
        return HyperlinkOf(&gridRef);
    }

    internal static string? HyperlinkOf(GhosttyGridRef* gridRef)
    {
        Span<byte> buffer = stackalloc byte[2048];
        fixed (byte* p = buffer)
        {
            nuint length;
            var r = GhosttyNative.GridRefHyperlinkUri(gridRef, p, (nuint)buffer.Length, &length);
            if (r == GhosttyResult.OutOfSpace)
            {
                var large = new byte[(int)length];
                fixed (byte* q = large)
                {
                    r = GhosttyNative.GridRefHyperlinkUri(gridRef, q, length, &length);
                    return r == GhosttyResult.Success && length > 0 ? Encoding.UTF8.GetString(q, (int)length) : null;
                }
            }
            return r == GhosttyResult.Success && length > 0 ? Encoding.UTF8.GetString(p, (int)length) : null;
        }
    }

    internal GhosttyGridRef GridRef(ViewportPoint point)
    {
        var p = new GhosttyPoint { Tag = GhosttyPointTag.Viewport };
        p.Value.Coordinate = new GhosttyPointCoordinate { X = (ushort)Math.Max(0, point.Column), Y = (uint)Math.Max(0, point.Row) };
        var gridRef = GhosttyGridRef.Create();
        Check(GhosttyNative.TerminalGridRef(Handle, p, &gridRef));
        return gridRef;
    }

    /// <summary>把一个选区（未跟踪的 grid ref）换算成视口内每行的高亮区间（02 §8.6）。</summary>
    internal void SelectionSpans(GhosttySelection* selection, int rows, int cols, List<CellSpan> spans)
    {
        var ordered = GhosttySelection.Create();
        if (GhosttyNative.TerminalSelectionOrdered(Handle, selection, GhosttySelectionOrder.Forward, &ordered) != GhosttyResult.Success)
        {
            return;
        }
        GhosttyPointCoordinate start, end;
        if (GhosttyNative.TerminalPointFromGridRef(Handle, &ordered.Start, GhosttyPointTag.Screen, &start) != GhosttyResult.Success ||
            GhosttyNative.TerminalPointFromGridRef(Handle, &ordered.End, GhosttyPointTag.Screen, &end) != GhosttyResult.Success)
        {
            return;
        }
        GhosttyTerminalScrollbar scrollbar;
        Check(GhosttyNative.TerminalGet(Handle, GhosttyTerminalData.Scrollbar, &scrollbar));
        var top = (long)scrollbar.Offset;
        var firstRow = (long)start.Y - top;
        var lastRow = (long)end.Y - top;
        for (var row = Math.Max(0, firstRow); row <= Math.Min(rows - 1, lastRow); row++)
        {
            int from, to;
            if (ordered.Rectangle)
            {
                (from, to) = (Math.Min(start.X, end.X), Math.Max(start.X, end.X));
            }
            else
            {
                from = row == firstRow ? start.X : 0;
                to = row == lastRow ? end.X : cols - 1;
            }
            spans.Add(new CellSpan((int)row, from, Math.Min(to, cols - 1)));
        }
    }

    /// <summary>当前活动的搜索（没有时为 null）。帧里的高亮来自它。</summary>
    public TerminalSearch? ActiveSearch => _search;

    /// <summary>编码输入所需的模式（02 §13.1）。视图 actor 每处理完一批 Write 调用一次，变化才发布。</summary>
    public InputModes ReadInputModes()
    {
        byte kitty;
        bool alt;
        Check(GhosttyNative.TerminalGet(Handle, GhosttyTerminalData.KittyKeyboardFlags, &kitty));
        GhosttyTerminalScreen screen;
        Check(GhosttyNative.TerminalGet(Handle, GhosttyTerminalData.ActiveScreen, &screen));
        alt = screen != GhosttyTerminalScreen.Primary;
        var tracking = GetMode(1003) ? MouseTracking.Any : GetMode(1002) ? MouseTracking.Button : GetMode(1000) ? MouseTracking.Normal
            : GetMode(9) ? MouseTracking.X10 : MouseTracking.None;
        var format = GetMode(1016) ? MouseFormat.SgrPixels : GetMode(1006) ? MouseFormat.Sgr : GetMode(1015) ? MouseFormat.Urxvt
            : GetMode(1005) ? MouseFormat.Utf8 : MouseFormat.X10;
        return new InputModes
        {
            CursorKeyApplication = GetMode(1),
            KeypadApplication = GetMode(66),
            IgnoreKeypadWithNumLock = GetMode(1035),
            AltEscPrefix = GetMode(1036),
            BackarrowSendsBackspace = GetMode(67),
            ModifyOtherKeys2 = ModifyOtherKeys2,
            KittyFlags = kitty,
            MouseTracking = tracking,
            MouseFormat = format,
            AlternateScroll = GetMode(1007),
            AlternateScreen = alt,
            BracketedPaste = GetMode(2004),
            FocusEvents = GetMode(1004),
            GraphemeCluster = GetMode(2027),
            Win32Input = GetMode(9001),
            CursorAtPrompt = CursorAtPrompt,
        };
    }

    /// <summary>
    /// 编码一次粘贴（02 §13.3，在 UI 侧核心上执行）：按终端当前状态包裹（2004）或剥离危险控制字符，
    /// 输出经 write_pty 进入捕获作用域写到 <paramref name="output"/>。内容可能注入命令且 <paramref name="allowUnsafe"/> 为 false 时，
    /// 返回 <see cref="PasteOutcome.RequiresConfirmation"/>，什么也不写。
    /// </summary>
    public PasteOutcome EncodePaste(string text, bool allowUnsafe, System.Buffers.IBufferWriter<byte> output)
    {
        EnsureNotInCallback();
        var bytes = Encoding.UTF8.GetBytes(text);
        var mime = "text/plain"u8;
        fixed (byte* data = bytes)
        fixed (byte* mimePtr = mime)
        {
            var source = new PasteSource { Data = data, Length = bytes.Length };
            var mimeString = new GhosttyString { Ptr = mimePtr, Len = (nuint)mime.Length };
            var paste = GhosttyPaste.Create();
            paste.Location = GhosttyClipboardLocation.Standard;
            paste.Source = GhosttyPasteSource.Text;
            paste.Mimes = &mimeString;
            paste.MimesLen = 1;
            paste.Reader.Read = (nint)(delegate* unmanaged[Cdecl]<void*, GhosttyString, GhosttyWriter, byte>)&Callbacks.PasteRead;
            paste.Reader.Userdata = &source;
            paste.AllowUnsafe = allowUnsafe;
            _pasteCapture = output;
            try
            {
                bool written;
                var r = GhosttyNative.TerminalPaste(Handle, &paste, &written);
                if (r == GhosttyResult.Rejected)
                {
                    return PasteOutcome.RequiresConfirmation;
                }
                Check(r);
                return written ? PasteOutcome.Written : PasteOutcome.Empty;
            }
            finally
            {
                _pasteCapture = null;
            }
        }
    }

    private struct PasteSource
    {
        public byte* Data;
        public int Length;
    }

    /// <summary>DEC 私有模式（<paramref name="ansi"/> 为 false）或 ANSI 模式的当前值；未知模式返回 false。</summary>
    public bool GetMode(int mode, bool ansi = false)
    {
        var config = new GhosttyTerminalModeConfig { Mode = GhosttyHelpers.ModeNew(checked((ushort)mode), ansi) };
        var r = GhosttyNative.TerminalGet(Handle, GhosttyTerminalData.Mode, &config);
        return r == GhosttyResult.Success && config.Value;
    }

    /// <summary>xterm modifyOtherKeys 2 是否开启（overlay O4）。</summary>
    public bool ModifyOtherKeys2 => GhosttyNative.TkcodeModifyOtherKeys2(Handle);

    /// <summary>当前屏幕（含回滚）的纯文本：软换行合并，去掉行尾空白。</summary>
    public string FormatPlainText()
    {
        EnsureNotInCallback();
        var options = GhosttyHelpers.FormatterOptions(GhosttyFormatterFormat.Plain);
        options.Unwrap = true;
        options.Trim = true;
        return Format(options);
    }

    /// <summary>
    /// VT 兼容格式（04 §8.5、§10）：回滚 + 屏幕的 VT 文本，附带恢复光标、样式、超链接、模式、调色板、滚动区域、制表位与 pwd 的序列。
    /// 跨 libghostty 版本可用，精度低于二进制快照（解析到一半的序列、Kitty 图片无法还原）。
    /// </summary>
    public string FormatVt()
    {
        EnsureNotInCallback();
        var options = GhosttyHelpers.FormatterOptions(GhosttyFormatterFormat.Vt);
        options.Extra.Palette = true;
        options.Extra.Modes = true;
        options.Extra.ScrollingRegion = true;
        options.Extra.Tabstops = true;
        options.Extra.Pwd = true;
        options.Extra.Keyboard = true;
        options.Extra.Screen.Cursor = true;
        options.Extra.Screen.Style = true;
        options.Extra.Screen.Hyperlink = true;
        options.Extra.Screen.Protection = true;
        options.Extra.Screen.KittyKeyboard = true;
        options.Extra.Screen.Charsets = true;
        return Format(options);
    }

    private string Format(GhosttyFormatterTerminalOptions options)
    {
        nint formatter;
        Check(GhosttyNative.FormatterTerminalNew(null, &formatter, Handle, options));
        try
        {
            byte* output;
            nuint length;
            Check(GhosttyNative.FormatterFormatAlloc(formatter, null, &output, &length));
            return TakeString(output, length);
        }
        finally
        {
            GhosttyNative.FormatterFree(formatter);
        }
    }

    /// <summary>
    /// 捕获当前视口（02 §8）：只重建脏行，未变化的行与上一帧共享对象；没有任何变化时返回 null。
    /// 只在 UI 侧核心上使用（每个终端只能有一个 render state）。
    /// </summary>
    public FrameSnapshot? CaptureFrame(bool force = false)
    {
        EnsureNotInCallback();
        _capturer ??= new FrameCapturer(this);
        return _capturer.Capture(Handle, force);
    }

    /// <summary>用一个临时的 render state 全量捕获（差分测试的"全量重建"对照，02 §15）。不能与 <see cref="CaptureFrame"/> 用在同一个核心上。</summary>
    internal FrameSnapshot CaptureFresh()
    {
        EnsureNotInCallback();
        using var capturer = new FrameCapturer(this);
        return capturer.Capture(Handle, force: true)!;
    }

    /// <summary>完整快照（包括未完成的解析器输入），用于 pty host 重连与持久化（04 §8、§10）。</summary>
    public byte[] EncodeSnapshot()
    {
        EnsureNotInCallback();
        byte* output;
        nuint length;
        Check(GhosttyNative.SnapshotEncodeAlloc(Handle, null, &output, &length));
        try
        {
            return new ReadOnlySpan<byte>(output, checked((int)length)).ToArray();
        }
        finally
        {
            GhosttyNative.Free(null, output, length);
        }
    }

    public void Dispose()
    {
        if (_terminal == 0)
        {
            return;
        }
        // 子对象先于终端释放（02 §4.6）
        _commands.Dispose();
        _search?.Dispose();
        _search = null;
        _capturer?.Dispose();
        _capturer = null;
        // 先释放终端，再释放 GCHandle：否则最后一次回调可能拿到已释放的句柄（02 §4.5）。
        GhosttyNative.TerminalFree(_terminal);
        _terminal = 0;
        _self.Dispose();
        FreeNative(ref _xtversion);
        FreeNative(ref _terminfoName);
    }

    internal nint Handle
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get
        {
            ObjectDisposedException.ThrowIf(_terminal == 0, this);
            return _terminal;
        }
    }

    private void Configure(in TerminalCoreOptions options, bool applyModeDefaults)
    {
        // Ghostty 自有的字形协议注册的字形不进快照，开启会导致重连后两份核心不一致（02 §3）。
        bool off = false;
        Check(GhosttyNative.TerminalSet(_terminal, GhosttyTerminalOption.GlyphProtocol, &off));
        // 标题回报会把程序可控的字符串注入输入流（10 §6.2），保持关闭
        Check(GhosttyNative.TerminalSet(_terminal, GhosttyTerminalOption.TitleReport, &off));
        // 库默认的回滚字节上限很小，必须显式设置（research/04 §0.8）。
        nuint maxBytes = (nuint)options.ScrollbackMaxBytes;
        Check(GhosttyNative.TerminalSet(_terminal, GhosttyTerminalOption.ScrollbackMaxBytes, &maxBytes));
        if (options.ScrollbackMaxLines is { } lines)
        {
            nuint maxLines = (nuint)lines;
            Check(GhosttyNative.TerminalSet(_terminal, GhosttyTerminalOption.ScrollbackMaxLines, &maxLines));
        }
        if (options.Size.CellWidth > 0 || options.Size.CellHeight > 0)
        {
            Resize(options.Size);
        }
        GhosttyNative.TkcodeSetClipboardReadDeferred(_terminal, options.DeferClipboardRead);
        // 跟踪未完成的转义序列 / UTF-8，快照因此可以在任意字节位置编码（02 §9）
        nuint continuation = (nuint)options.ContinuationMaxBytes;
        Check(GhosttyNative.TerminalSet(_terminal, GhosttyTerminalOption.ContinuationMaxBytes, &continuation));
        // OSC 633 等未知序列交给我们（02 §12.3）
        nuint unknownMax = 4096;
        Check(GhosttyNative.TerminalSet(_terminal, GhosttyTerminalOption.UnknownMaxBytes, &unknownMax));

        _terminfoName = AllocString(options.TerminfoName);
        var tn = new GhosttyString { Ptr = (byte*)_terminfoName, Len = (nuint)Encoding.UTF8.GetByteCount(options.TerminfoName) };
        Check(GhosttyNative.TerminalSet(_terminal, GhosttyTerminalOption.TerminfoName, &tn));
        _xtversion = AllocString(options.Replies.XtVersion);

        var cursorStyle = (GhosttyTerminalCursorStyle)(int)options.DefaultCursorStyle;
        var blink = options.DefaultCursorBlink;
        Check(GhosttyNative.TerminalSet(_terminal, GhosttyTerminalOption.DefaultCursorStyle, &cursorStyle));
        Check(GhosttyNative.TerminalSet(_terminal, GhosttyTerminalOption.DefaultCursorBlink, &blink));
        if (options.Conformance is { } conformance)
        {
            var on = true;
            var extension = conformance.XtChecksumExtension;
            Check(GhosttyNative.TerminalSet(_terminal, GhosttyTerminalOption.XtChecksumReport, &on));
            Check(GhosttyNative.TerminalSet(_terminal, GhosttyTerminalOption.XtChecksumExtension, &extension));
            // 标题回报（CSI 21 t 等）在产品中关闭（10 §6.2）；一致性测试按 xterm 开启
            Check(GhosttyNative.TerminalSet(_terminal, GhosttyTerminalOption.TitleReport, &on));
        }

        if (applyModeDefaults)
        {
            // 宽度策略：mode 2027 的复位默认值（RIS 后也保持）；当前值同步设置
            var grapheme = new GhosttyTerminalModeConfig { Mode = GhosttyHelpers.ModeNew(2027, false), Value = options.Width == WidthPolicy.Grapheme };
            Check(GhosttyNative.TerminalSet(_terminal, GhosttyTerminalOption.ModeDefault, &grapheme));
            Check(GhosttyNative.TerminalSet(_terminal, GhosttyTerminalOption.Mode, &grapheme));
        }
    }

    private void InstallCallbacks()
    {
        var t = _terminal;
        Check(GhosttyNative.TerminalSet(t, GhosttyTerminalOption.Userdata, (void*)GCHandle<TerminalCore>.ToIntPtr(_self)));
        Check(GhosttyNative.TerminalSet(t, GhosttyTerminalOption.WritePty, (delegate* unmanaged[Cdecl]<nint, nint, byte*, nuint, void>)&Callbacks.WritePty));
        Check(GhosttyNative.TerminalSet(t, GhosttyTerminalOption.Bell, (delegate* unmanaged[Cdecl]<nint, nint, void>)&Callbacks.Bell));
        Check(GhosttyNative.TerminalSet(t, GhosttyTerminalOption.TitleChanged, (delegate* unmanaged[Cdecl]<nint, nint, void>)&Callbacks.TitleChanged));
        Check(GhosttyNative.TerminalSet(t, GhosttyTerminalOption.PwdChanged, (delegate* unmanaged[Cdecl]<nint, nint, void>)&Callbacks.PwdChanged));
        Check(GhosttyNative.TerminalSet(t, GhosttyTerminalOption.Xtversion, (delegate* unmanaged[Cdecl]<nint, nint, GhosttyString>)&Callbacks.XtVersion));
        Check(GhosttyNative.TerminalSet(t, GhosttyTerminalOption.DeviceAttributes,
            (delegate* unmanaged[Cdecl]<nint, nint, GhosttyDeviceAttributes*, byte>)&Callbacks.DeviceAttributes));
        Check(GhosttyNative.TerminalSet(t, GhosttyTerminalOption.Size, (delegate* unmanaged[Cdecl]<nint, nint, GhosttySizeReportSize*, byte>)&Callbacks.SizeReport));
        Check(GhosttyNative.TerminalSet(t, GhosttyTerminalOption.ColorScheme, (delegate* unmanaged[Cdecl]<nint, nint, GhosttyColorScheme*, byte>)&Callbacks.Scheme));
        Check(GhosttyNative.TerminalSet(t, GhosttyTerminalOption.ClipboardWrite,
            (delegate* unmanaged[Cdecl]<nint, nint, GhosttyClipboardWrite*, void>)&Callbacks.ClipboardWrite));
        Check(GhosttyNative.TerminalSet(t, GhosttyTerminalOption.ClipboardRead,
            (delegate* unmanaged[Cdecl]<nint, nint, GhosttyClipboardRead*, void>)&Callbacks.ClipboardRead));
        Check(GhosttyNative.TerminalSet(t, GhosttyTerminalOption.DesktopNotification,
            (delegate* unmanaged[Cdecl]<nint, nint, GhosttyTerminalDesktopNotification*, void>)&Callbacks.Notification));
        Check(GhosttyNative.TerminalSet(t, GhosttyTerminalOption.ProgressReport,
            (delegate* unmanaged[Cdecl]<nint, nint, GhosttyTerminalProgressReport*, void>)&Callbacks.Progress));
        Check(GhosttyNative.TerminalSet(t, GhosttyTerminalOption.SemanticPrompt,
            (delegate* unmanaged[Cdecl]<nint, nint, GhosttyTerminalSemanticPrompt*, void>)&Callbacks.SemanticPrompt));
        Check(GhosttyNative.TerminalSet(t, GhosttyTerminalOption.RenderHold, (delegate* unmanaged[Cdecl]<nint, nint, byte, void>)&Callbacks.RenderHold));
        Check(GhosttyNative.TerminalSet(t, GhosttyTerminalOption.Reset, (delegate* unmanaged[Cdecl]<nint, nint, void>)&Callbacks.Reset));
        Check(GhosttyNative.TerminalSet(t, GhosttyTerminalOption.UnknownSequence,
            (delegate* unmanaged[Cdecl]<nint, nint, GhosttyTerminalUnknownSequence*, void>)&Callbacks.UnknownSequence));
        if (_conformance is not null)
        {
            Check(GhosttyNative.TerminalSet(t, GhosttyTerminalOption.ResizeWindow,
                (delegate* unmanaged[Cdecl]<nint, nint, GhosttyTerminalResizeWindow*, void>)&Callbacks.ResizeWindow));
        }
    }

    private void EnsureNotInCallback()
    {
        if (_inCallback)
        {
            throw new InvalidOperationException("TerminalCore cannot be re-entered from an effect callback.");
        }
    }

    private string ReadString(GhosttyTerminalData data)
    {
        GhosttyString s;
        var r = GhosttyNative.TerminalGet(Handle, data, &s);
        return r != GhosttyResult.Success || s.Len == 0 ? string.Empty : Encoding.UTF8.GetString(s.Ptr, checked((int)s.Len));
    }

    internal static string Decode(in GhosttyString s) => s.Len == 0 || s.Ptr == null ? string.Empty : Encoding.UTF8.GetString(s.Ptr, checked((int)s.Len));

    internal static string TakeString(byte* output, nuint length)
    {
        if (output == null || length == 0)
        {
            return string.Empty;  // 空结果时库返回空指针
        }
        try
        {
            return Encoding.UTF8.GetString(output, checked((int)length));
        }
        finally
        {
            GhosttyNative.Free(null, output, length);
        }
    }

    private static nint AllocString(string value)
    {
        var count = Encoding.UTF8.GetByteCount(value);
        var p = (byte*)NativeMemory.Alloc((nuint)count + 1);
        Encoding.UTF8.GetBytes(value, new Span<byte>(p, count));
        p[count] = 0;
        return (nint)p;
    }

    private static void FreeNative(ref nint p)
    {
        if (p != 0)
        {
            NativeMemory.Free((void*)p);
            p = 0;
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void Check(GhosttyResult result, [CallerArgumentExpression(nameof(result))] string expression = "")
    {
        if (result != GhosttyResult.Success)
        {
            Throw(result, expression);
        }
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void Throw(GhosttyResult result, string expression) => throw result switch
    {
        GhosttyResult.OutOfMemory => new InsufficientMemoryException($"libghostty-vt out of memory: {expression}"),
        _ => new GhosttyException(result, expression),
    };

    /// <summary>effect 回调中抛出的异常次数（被吞掉并记录，不穿过原生帧）。</summary>
    public int CallbackFaults { get; private set; }

    public Exception? LastCallbackFault { get; private set; }

    /// <summary>在回调中执行托管代码：标记重入、吞掉异常（异常绝不能穿过原生帧，否则进程直接终止，02 §4.5）。</summary>
    private bool Guard<TState>(TState state, delegate*<TerminalCore, TState, void> action) where TState : allows ref struct
    {
        _inCallback = true;
        try
        {
            action(this, state);
            return true;
        }
        catch (Exception ex)
        {
            CallbackFaults++;
            LastCallbackFault = ex;
            return false;
        }
        finally
        {
            _inCallback = false;
        }
    }

    private static TerminalCore From(nint userdata) => GCHandle<TerminalCore>.FromIntPtr(userdata).Target;

    private static class Callbacks
    {
        [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
        public static void WritePty(nint terminal, nint userdata, byte* data, nuint length) =>
            From(userdata).Guard(new ReadOnlySpan<byte>(data, checked((int)length)), &OnWritePty);

        private static void OnWritePty(TerminalCore c, ReadOnlySpan<byte> data)
        {
            // 粘贴的捕获作用域：UI 侧核心的 EncodePaste 期间，输出交给调用方而不是 effects（02 §7.2）
            if (c._pasteCapture is { } capture)
            {
                System.Buffers.BuffersExtensions.Write(capture, data);
                return;
            }
            c._effects.WriteToPty(data);
        }

        /// <summary>粘贴内容的 MIME 读取器：把整段文本写给库提供的 writer。</summary>
        [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
        public static byte PasteRead(void* userdata, GhosttyString mime, GhosttyWriter writer)
        {
            var source = (PasteSource*)userdata;
            var write = (delegate* unmanaged[Cdecl]<void*, byte*, nuint, byte>)writer.Write;
            return write(writer.Userdata, source->Data, (nuint)source->Length);
        }

        [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
        public static void Bell(nint terminal, nint userdata) => From(userdata).Guard(0, &OnBell);

        private static void OnBell(TerminalCore c, int _) => c._effects.Bell();

        [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
        public static void TitleChanged(nint terminal, nint userdata) => From(userdata).Guard(0, &OnTitle);

        // 回调内只允许只读查询（头文件只禁止重入 vt_write）
        private static void OnTitle(TerminalCore c, int _) => c._effects.TitleChanged(c.ReadString(GhosttyTerminalData.Title));

        [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
        public static void PwdChanged(nint terminal, nint userdata) => From(userdata).Guard(0, &OnPwd);

        private static void OnPwd(TerminalCore c, int _) => c._effects.WorkingDirectoryChanged(c.ReadString(GhosttyTerminalData.Pwd));

        [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
        public static GhosttyString XtVersion(nint terminal, nint userdata)
        {
            var c = From(userdata);
            return new GhosttyString { Ptr = (byte*)c._xtversion, Len = (nuint)Encoding.UTF8.GetByteCount(c._replies.XtVersion) };
        }

        /// <summary>DA1 <c>?62;22;52c</c>（不允许 OSC 52 写时去掉 52）、DA2 <c>&gt;1;10;0c</c>（与 Ghostty 一致）、DA3 单元 0（10 §2）。</summary>
        [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
        public static byte DeviceAttributes(nint terminal, nint userdata, GhosttyDeviceAttributes* attrs)
        {
            var c = From(userdata);
            if (c._conformance is not null)
            {
                // esctest 最高测到 VT520：DA1 ?65;22c，DA2 >64;10;0c
                attrs->Primary.ConformanceLevel = 65;
                attrs->Primary.Features[0] = 22;
                attrs->Primary.NumFeatures = 1;
                attrs->Secondary.DeviceType = 64;
                attrs->Secondary.FirmwareVersion = 10;
                attrs->Secondary.RomCartridge = 0;
                return 1;
            }
            attrs->Primary.ConformanceLevel = 62;
            attrs->Primary.Features[0] = 22;
            attrs->Primary.NumFeatures = 1;
            if (c._replies.AdvertiseOsc52)
            {
                attrs->Primary.Features[1] = 52;
                attrs->Primary.NumFeatures = 2;
            }
            attrs->Secondary.DeviceType = 1;
            attrs->Secondary.FirmwareVersion = 10;
            attrs->Secondary.RomCartridge = 0;
            return 1;
        }

        /// <summary>XTWINOPS 14/16/18/19 t 与 2048：核心最近一次 Resize 的尺寸（cell 像素）。</summary>
        [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
        public static byte SizeReport(nint terminal, nint userdata, GhosttySizeReportSize* size)
        {
            var c = From(userdata);
            ushort cols, rows;
            GhosttyNative.TerminalGet(terminal, GhosttyTerminalData.Cols, &cols);
            GhosttyNative.TerminalGet(terminal, GhosttyTerminalData.Rows, &rows);
            size->Columns = cols;
            size->Rows = rows;
            size->CellWidth = (uint)Math.Max(0, c._size.CellWidth);
            size->CellHeight = (uint)Math.Max(0, c._size.CellHeight);
            return 1;
        }

        [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
        public static byte Scheme(nint terminal, nint userdata, GhosttyColorScheme* scheme)
        {
            *scheme = From(userdata)._scheme == ColorScheme.Light ? GhosttyColorScheme.Light : GhosttyColorScheme.Dark;
            return 1;
        }

        [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
        public static void ClipboardWrite(nint terminal, nint userdata, GhosttyClipboardWrite* write)
        {
            var c = From(userdata);
            var result = ClipboardWriteResult.Denied;
            c._inCallback = true;
            try
            {
                for (var i = 0; i < (int)write->ContentsLen && result == ClipboardWriteResult.Denied; i++)
                {
                    var content = write->Contents[i];
                    var mime = Decode(content.Mime);
                    if (mime is "text/plain" or "")
                    {
                        result = c._effects.ClipboardWrite(Target(write->Location), mime,
                            new ReadOnlySpan<byte>(content.Data.Ptr, checked((int)content.Data.Len)));
                    }
                }
            }
            catch (Exception ex)
            {
                c.CallbackFaults++;
                c.LastCallbackFault = ex;
            }
            finally
            {
                c._inCallback = false;
            }
            var reply = GhosttyClipboardWriteReply.Create();
            reply.Result = result == ClipboardWriteResult.Success ? GhosttyClipboardWriteResult.Success : GhosttyClipboardWriteResult.Denied;
            ((delegate* unmanaged[Cdecl]<GhosttyClipboardWrite*, GhosttyClipboardWriteReply*, void>)write->Reply)(write, &reply);
        }

        [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
        public static void ClipboardRead(nint terminal, nint userdata, GhosttyClipboardRead* read)
        {
            var c = From(userdata);
            // O3：库记录了请求的终止符；Deferred 时不调用 reply，由 CompleteClipboardRead 稍后写回
            var request = new PendingClipboardRead(Target(read->Location), GhosttyNative.TkcodeClipboardReadTerminator(terminal) == 1);
            var decision = ClipboardReadDecision.Denied;
            c._inCallback = true;
            try
            {
                decision = c._effects.ClipboardRead(request);
            }
            catch (Exception ex)
            {
                c.CallbackFaults++;
                c.LastCallbackFault = ex;
            }
            finally
            {
                c._inCallback = false;
            }
            if (decision == ClipboardReadDecision.Denied)
            {
                var reply = GhosttyClipboardReadReply.Create();
                reply.Result = GhosttyClipboardReadResult.Denied;
                ((delegate* unmanaged[Cdecl]<GhosttyClipboardRead*, GhosttyClipboardReadReply*, void>)read->Reply)(read, &reply);
            }
        }

        private static ClipboardTarget Target(GhosttyClipboardLocation location) => location switch
        {
            GhosttyClipboardLocation.Selection => ClipboardTarget.Selection,
            GhosttyClipboardLocation.Primary => ClipboardTarget.Primary,
            _ => ClipboardTarget.Standard,
        };

        [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
        public static void Notification(nint terminal, nint userdata, GhosttyTerminalDesktopNotification* n) =>
            From(userdata).Guard((Title: Decode(n->Title), Body: Decode(n->Body)), &OnNotification);

        private static void OnNotification(TerminalCore c, (string Title, string Body) n) => c._effects.Notification(n.Title, n.Body);

        [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
        public static void Progress(nint terminal, nint userdata, GhosttyTerminalProgressReport* report) =>
            From(userdata).Guard(((ProgressState)(int)report->State, report->Progress < 0 ? (int?)null : report->Progress), &OnProgress);

        private static void OnProgress(TerminalCore c, (ProgressState State, int? Percent) p) => c._effects.Progress(p.State, p.Percent);

        [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
        public static void SemanticPrompt(nint terminal, nint userdata, GhosttyTerminalSemanticPrompt* e)
        {
            if (e->Kind == GhosttySemanticPromptKind.Invalid)
            {
                return;
            }
            var ev = new SemanticPromptEvent(
                e->Kind switch
                {
                    GhosttySemanticPromptKind.PromptStart => SemanticPromptKind.PromptStart,
                    GhosttySemanticPromptKind.InputStart => SemanticPromptKind.InputStart,
                    GhosttySemanticPromptKind.OutputStart => SemanticPromptKind.OutputStart,
                    _ => SemanticPromptKind.CommandEnd,
                },
                e->PromptKind == GhosttySemanticPromptPromptKind.Primary,
                e->HasExitCode ? e->ExitCode : null,
                e->Command.Len > 0 ? Decode(e->Command) : null,
                e->Error.Len > 0 ? Decode(e->Error) : null);
            From(userdata).Guard(ev, &OnSemanticPrompt);
        }

        private static void OnSemanticPrompt(TerminalCore c, SemanticPromptEvent e)
        {
            c._commands.OnSemanticPrompt(e, fromOsc633: false);
            c._effects.SemanticPrompt(e);
        }

        [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
        public static void RenderHold(nint terminal, nint userdata, byte held) => From(userdata).Guard(held != 0, &OnRenderHold);

        private static void OnRenderHold(TerminalCore c, bool held) => c._effects.RenderHold(held);

        [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
        public static void ResizeWindow(nint terminal, nint userdata, GhosttyTerminalResizeWindow* request)
        {
            if (request->Tag == GhosttyTerminalResizeWindowTag.Grid)
            {
                From(userdata).Guard((request->Value.Grid.Columns, request->Value.Grid.Rows), &OnResizeWindow);
            }
        }

        private static void OnResizeWindow(TerminalCore c, (ushort Columns, ushort Rows) r) => c._effects.ResizeWindowRequested(r.Columns, r.Rows);

        [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
        public static void Reset(nint terminal, nint userdata) => From(userdata).Guard(0, &OnReset);

        private static void OnReset(TerminalCore c, int _)
        {
            c._commands.Clear();
            c._effects.FullReset();
        }

        [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
        public static void UnknownSequence(nint terminal, nint userdata, GhosttyTerminalUnknownSequence* sequence)
        {
            if (sequence->Tag != GhosttyTerminalUnknownSequenceTag.Osc)
            {
                return;
            }
            var osc = sequence->Value.Osc;
            From(userdata).Guard((Data: (nint)osc.Content.Ptr, Length: (int)osc.Content.Len, osc.Truncated), &OnUnknownOsc);
        }

        private static void OnUnknownOsc(TerminalCore c, (nint Data, int Length, bool Truncated) osc)
        {
            var payload = new ReadOnlySpan<byte>((byte*)osc.Data, osc.Length);
            if (!osc.Truncated)
            {
                c._commands.OnOsc633(payload);
            }
            c._effects.UnknownOsc(payload, osc.Truncated);
        }
    }
}

public enum ClearTarget
{
    Screen,
    Scrollback,
    All,
}
