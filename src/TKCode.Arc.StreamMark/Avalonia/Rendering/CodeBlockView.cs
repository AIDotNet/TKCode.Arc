using System.Collections.Immutable;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Input.Platform;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using Avalonia.Media.TextFormatting;
using Avalonia.Threading;
using Avalonia.Utilities;
using StreamMark.Avalonia.Selection;
using StreamMark.Core.Highlighting;
using StreamMark.Core.Model;

namespace StreamMark.Avalonia.Rendering;

/// <summary>
/// 代码块：头部（语言 / 路径、插件操作、换行切换、复制）+ 代码区。代码按行排版并缓存；
/// 切行与增量高亮放在块共享状态 <see cref="CodeBlockState"/> 中，回收后重建或分段显示都不重复计算。
/// 会话视图中的超长代码会拆成多段（<see cref="SetChunk"/>），只有第一段显示头部，所有分段共用一个选区片段。
/// </summary>
public sealed class CodeBlockView : BlockView
{
    private const double HeaderHeight = 34;
    private const double Padding = 14;
    private const string WrapState = "code.wrap";
    internal const string ExpandedState = "code.expanded";
    private const double FooterHeight = 30;

    private readonly IconButton _copy = new() { Icon = MarkdownIcons.Copy };
    private readonly IconButton _wrap = new() { Icon = MarkdownIcons.Wrap };
    private readonly IconButton _download = new() { Icon = MarkdownIcons.Download };
    private readonly IconButton _expand = new() { Icon = MarkdownIcons.Chevron, IsVisible = false };
    private readonly CodeTextView _code = new();
    private readonly BlockActionBar _actions;
    private readonly List<IconButton> _headerButtons = [];
    private TextLayout? _title;
    private string? _parsedInfo;
    private int _parsedFirstLine = 1;
    private DispatcherTimer? _copiedTimer;
    private int _chunk;
    private int _chunkCount = 1;
    private int _linesPerChunk = int.MaxValue;
    // 折叠：超长的已完成代码块只显示前若干行，底部"显示全部"
    private bool _collapsible;
    private bool _collapsed;
    private int _lineCount;
    // 行号
    private bool _lineNumbers;
    private int _firstLineNumber = 1;
    private int _lineNumberStart;
    private int _lineNumberDigits;
    private MarkdownStyle? _lineNumbersStyle;
    private readonly List<TextLayout> _numbers = [];
    private double _gutter;
    private SegmentKey _segmentKey;
    // Avalonia 在块的 Bounds 变化（哪怕只是位置）时重跑 Render：上方内容展开时块每帧都在移动。
    // 边框笔、分段外形与折叠渐隐按输入缓存，不在每次 Render 里新建（Pen 与画刷的构造要走属性系统）。
    private IPen? _framePen;
    private IBrush? _framePenBrush;
    private StreamGeometry? _chunkGeometry;
    private (Size Size, double Top, double Bottom) _chunkGeometryKey;
    private IBrush? _fadeBrush;
    private Color _fadeColor;

    public CodeBlockView()
    {
        _actions = new BlockActionBar(this, AddChild, RemoveChild);
        AddChild(_code);
        AddChild(_download);
        AddChild(_wrap);
        AddChild(_copy);
        AddChild(_expand);
        RebuildHeaderButtons();
        global::Avalonia.Automation.AutomationProperties.SetName(_copy, "Copy");
        global::Avalonia.Automation.AutomationProperties.SetName(_wrap, "Wrap lines");
        global::Avalonia.Automation.AutomationProperties.SetName(_download, "Download");
        _copy.Click += async (_, _) => await CopyAsync();
        _download.Click += async (_, _) => await DownloadAsync();
        _expand.Click += (_, _) => ToggleExpanded();
        _wrap.Click += (_, _) =>
        {
            Wrap = !Wrap;
            Context?.State.Set(TopKey, WrapState, Wrap);
            _wrap.InvalidateVisual();
            InvalidateMeasure();
        };
    }

    public string Code => _code.Text;

    public bool Wrap
    {
        get => _code.Wrap;
        set
        {
            _code.Wrap = value;
            _wrap.IsActive = value;
        }
    }

    /// <summary>当前分段（0 起）与分段总数。</summary>
    public (int Index, int Count) Chunk => (_chunk, _chunkCount);

    private bool IsFirstChunk => _chunk == 0;

    private bool IsLastChunk => _chunk == _chunkCount - 1;

    /// <summary>作为超长代码的第 <paramref name="index"/> 段（每段 <paramref name="linesPerChunk"/> 行）显示。</summary>
    public void SetChunk(int index, int count, int linesPerChunk)
    {
        _chunk = index;
        _chunkCount = Math.Max(1, count);
        _linesPerChunk = count <= 1 ? int.MaxValue : linesPerChunk;
        for (var i = 0; i < _headerButtons.Count; i++)
        {
            _headerButtons[i].IsVisible = IsFirstChunk;
        }
        InvalidateMeasure();
        InvalidateVisual();
    }

    // 流式超长代码块在尾部新增分段时，前面已满的分段只同步最新模型与总分段数，不触发测量或重绘
    internal void SyncChunkMetadata(CodeBlock block, int count)
    {
        ReplaceBlockReference(block);
        _chunkCount = Math.Max(1, count);
        _code.SyncText(block.Code);
    }

    private double TopHeight => IsFirstChunk ? HeaderHeight : 0;

    private double BottomHeight => IsLastChunk ? (_collapsible ? FooterHeight : Padding) : 0;

    /// <summary>是否处于折叠状态。</summary>
    public bool IsCollapsed => _collapsed;

    /// <summary>折叠状态变化（会话视图据此重新分段）。</summary>
    public static readonly global::Avalonia.Interactivity.RoutedEvent<global::Avalonia.Interactivity.RoutedEventArgs> ExpandedChangedEvent =
        global::Avalonia.Interactivity.RoutedEvent.Register<CodeBlockView, global::Avalonia.Interactivity.RoutedEventArgs>("ExpandedChanged", global::Avalonia.Interactivity.RoutingStrategies.Bubble);

    /// <summary>按选项与块状态判断代码块是否应折叠（会话视图分段时也用）。</summary>
    internal static bool ShouldCollapse(CodeBlock code, RenderContext context, int key, out int lines)
    {
        var existing = context.State.Get<CodeBlockState?>(key, "code.state", null);
        lines = existing is not null && ReferenceEquals(existing.Code, code.Code)
            ? existing.LineCount
            : code.GetLineCount();
        var options = context.Services.CodeBlocks;
        return IsCollapsible(code, options, lines) && !context.State.Get(key, ExpandedState, false);
    }

    private static bool ShouldCollapse(CodeBlock code, RenderContext context, int key, int lines)
    {
        var options = context.Services.CodeBlocks;
        return IsCollapsible(code, options, lines) && !context.State.Get(key, ExpandedState, false);
    }

    private static bool IsCollapsible(CodeBlock code, Extensions.CodeBlockOptions options, int lines) =>
        options.CollapseAfterLines > 0 && !code.IsOpen && lines > options.CollapseAfterLines && lines > options.CollapsedLines;

    /// <summary>展开 / 折叠（状态保存在块状态存储中，回收后保留）。</summary>
    public void ToggleExpanded()
    {
        if (Context is null || Block is not CodeBlock code || !_collapsible)
        {
            return;
        }
        Context.State.Set(TopKey, ExpandedState, _collapsed);
        var segment = _segmentKey.Segment;
        Update(Context, code, TopKey, ref segment);
        RaiseEvent(new global::Avalonia.Interactivity.RoutedEventArgs(ExpandedChangedEvent, this));
    }

    /// <summary>下载：交给 <see cref="Extensions.CodeBlockOptions.Save"/>，默认弹出保存对话框写入文件。</summary>
    public async Task DownloadAsync()
    {
        if (Context is null || Block is not CodeBlock code)
        {
            return;
        }
        var fence = Plugins.CodeFenceInfo.Parse(code.Info);
        var request = new Extensions.CodeSaveRequest(Extensions.CodeFileNames.Suggest(code.Language, fence.Path), code.Code, code.Language, fence.Path);
        if (Context.Services.CodeBlocks.Save is { } save)
        {
            await save(request);
            return;
        }
        if (TopLevel.GetTopLevel(this)?.StorageProvider is not { CanSave: true } storage)
        {
            return;
        }
        var file = await storage.SaveFilePickerAsync(new global::Avalonia.Platform.Storage.FilePickerSaveOptions { SuggestedFileName = request.SuggestedFileName });
        if (file is null)
        {
            return;
        }
        await using var stream = await file.OpenWriteAsync();
        await using var writer = new StreamWriter(stream, new System.Text.UTF8Encoding(false));
        await writer.WriteAsync(request.Code);
    }

    protected override void OnUpdate(RenderContext context, MarkdownBlock? previous, MarkdownBlock block, int topKey, ref int segment, bool contextChanged)
    {
        var code = (CodeBlock)block;
        var style = context.Style;
        if (contextChanged || previous is not CodeBlock old || old.Info != code.Info)
        {
            // 头部标题：语言，信息串带路径时显示 "语言 · 路径#L起-止"
            var fence = Plugins.CodeFenceInfo.Parse(code.Info);
            _parsedInfo = code.Info;
            _parsedFirstLine = fence.StartLine ?? 1;
            var title = string.IsNullOrEmpty(code.Language) ? "text" : code.Language;
            if (fence.Path is { } path)
            {
                title += " · " + path + (fence.StartLine is { } start ? $"#L{start}" + (fence.EndLine is { } end && end != start ? $"-{end}" : "") : "");
            }
            _title = new TextLayout(title, new Typeface(style.MonospaceFontFamily), style.CodeFontSize * 0.92, style.SecondaryForeground,
                textTrimming: TextTrimming.CharacterEllipsis, maxWidth: 480);
            _actions.Rebuild(context, Plugins.BlockActionTargets.Code, code, IsFirstChunk);
            RebuildHeaderButtons();
            for (var i = 0; i < _headerButtons.Count; i++)
            {
                var button = _headerButtons[i];
                button.Foreground = style.SecondaryForeground;
                button.HoverBackground = style.HoverBackground;
                button.InvalidateVisual();
            }
        }
        Wrap = context.State.Get(topKey, WrapState, false);
        var shared = CodeBlockState.For(context, topKey, code.Language);
        shared.Update(code.Code, code.IsOpen);
        var options = context.Services.CodeBlocks;
        if (code.IsOpen)
        {
            // 看着流式写出来的代码块完成后不折叠（避免突然收起）
            context.State.Set(topKey, ExpandedState, true);
        }
        var oldCollapsed = _collapsed;
        var oldCollapsible = _collapsible;
        var oldLineCount = _lineCount;
        _lineCount = shared.LineCount;
        _collapsed = ShouldCollapse(code, context, topKey, _lineCount) && _chunkCount == 1;
        _collapsible = _chunkCount == 1 && IsCollapsible(code, options, _lineCount) || _chunkCount > 1 && IsLastChunk && IsCollapsible(code, options, _lineCount);
        _expand.IsVisible = _collapsible;
        _expand.Foreground = style.SecondaryForeground;
        _expand.HoverBackground = style.HoverBackground;
        _expand.Icon = _collapsed ? MarkdownIcons.Chevron : MarkdownIcons.ChevronUp;
        _expand.Label = _collapsed ? options.ExpandText(_lineCount) : options.CollapseText();
        _download.IsVisible = options.ShowDownload && IsFirstChunk;
        var lineStart = _linesPerChunk == int.MaxValue ? 0 : _chunk * _linesPerChunk;
        _segmentKey = KeyFor(context, topKey, ref segment);
        var codeChanged = _code.Set(context, _segmentKey, shared, lineStart, _collapsed ? options.CollapsedLines : _linesPerChunk);
        var numbersChanged = UpdateLineNumbers(context, code, shared, lineStart);
        var frameChanged = contextChanged || previous is null || numbersChanged || oldCollapsed != _collapsed || oldCollapsible != _collapsible;
        if (frameChanged || codeChanged || oldLineCount != _lineCount)
        {
            InvalidateMeasure();
        }
        if (frameChanged)
        {
            InvalidateVisual();
        }
    }

    private static int DigitCount(int n)
    {
        var digits = 1;
        while (n >= 10)
        {
            n /= 10;
            digits++;
        }
        return digits;
    }

    private bool UpdateLineNumbers(RenderContext context, CodeBlock code, CodeBlockState shared, int lineStart)
    {
        var show = context.Services.CodeBlocks.ShowLineNumbers;
        if (!show)
        {
            var wasShowing = _lineNumbers;
            _lineNumbers = false;
            _numbers.Clear();
            _gutter = 0;
            _lineNumberDigits = 0;
            _lineNumbersStyle = null;
            return wasShowing;
        }
        if (!ReferenceEquals(_parsedInfo, code.Info) && _parsedInfo != code.Info)
        {
            _parsedInfo = code.Info;
            _parsedFirstLine = Plugins.CodeFenceInfo.Parse(code.Info).StartLine ?? 1;
        }
        var firstLine = _parsedFirstLine;
        var style = context.Style;
        var last = firstLine + shared.LineCount - 1;
        var digits = DigitCount(Math.Max(1, last));
        var count = _code.VisibleLineCount;
        var rebuild = !_lineNumbers || !ReferenceEquals(_lineNumbersStyle, style) || _firstLineNumber != firstLine || _lineNumberStart != lineStart || _lineNumberDigits != digits;
        _lineNumbers = true;
        _firstLineNumber = firstLine;
        _lineNumberStart = lineStart;
        _lineNumbersStyle = style;
        var typeface = new Typeface(style.MonospaceFontFamily);
        if (rebuild)
        {
            _lineNumberDigits = digits;
            var probe = new TextLayout(new string('0', digits), typeface, style.CodeFontSize, style.SecondaryForeground);
            _gutter = Math.Ceiling(probe.WidthIncludingTrailingWhitespace) + 14;
            _numbers.Clear();
        }
        if (_numbers.Count == count && !rebuild)
        {
            return false;
        }
        if (_numbers.Count > count)
        {
            _numbers.RemoveRange(count, _numbers.Count - count);
        }
        for (var i = _numbers.Count; i < count; i++)
        {
            // 与代码行相同的行高，保证基线对齐
            _numbers.Add(new TextLayout((_firstLineNumber + lineStart + i).ToString(), typeface, style.CodeFontSize, style.SecondaryForeground,
                TextAlignment.Right, maxWidth: _gutter - 14, lineHeight: style.CodeLineHeight));
        }
        return true;
    }

    private async Task CopyAsync()
    {
        if (TopLevel.GetTopLevel(this)?.Clipboard is not { } clipboard)
        {
            return;
        }
        await clipboard.SetTextAsync(_code.Text);
        _copy.Icon = MarkdownIcons.Check;
        _copiedTimer?.Stop();
        _copiedTimer = new DispatcherTimer(TimeSpan.FromSeconds(1.5), DispatcherPriority.Normal, (_, _) =>
        {
            _copy.Icon = MarkdownIcons.Copy;
            _copiedTimer?.Stop();
        });
        _copiedTimer.Start();
    }

    // 插件操作（例如"应用到文件""在终端运行"）排在内置按钮左边
    private void RebuildHeaderButtons()
    {
        _headerButtons.Clear();
        _headerButtons.Add(_copy);
        _headerButtons.Add(_wrap);
        _headerButtons.Add(_download);
        foreach (var button in _actions.Buttons)
        {
            _headerButtons.Add(button);
        }
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        for (var i = 0; i < _headerButtons.Count; i++)
        {
            _headerButtons[i].Measure(Size.Infinity);
        }
        _expand.Measure(Size.Infinity);
        var inner = Math.Max(0, availableSize.Width - Padding * 2 - _gutter);
        _code.Measure(new Size(inner, double.PositiveInfinity));
        var width = double.IsFinite(availableSize.Width) ? availableSize.Width : _code.DesiredSize.Width + Padding * 2;
        return new Size(width, TopHeight + _code.DesiredSize.Height + BottomHeight);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        var x = finalSize.Width - 6;
        for (var i = 0; i < _headerButtons.Count; i++)
        {
            var button = _headerButtons[i];
            if (!button.IsVisible)
            {
                continue;
            }
            x -= button.DesiredSize.Width;
            button.Arrange(new Rect(x, (HeaderHeight - button.DesiredSize.Height) / 2, button.DesiredSize.Width, button.DesiredSize.Height));
            x -= 2;
        }
        _code.Arrange(new Rect(Padding + _gutter, TopHeight, Math.Max(0, finalSize.Width - Padding * 2 - _gutter), _code.DesiredSize.Height));
        if (_expand.IsVisible)
        {
            _expand.Arrange(new Rect(Padding - 6, finalSize.Height - FooterHeight + (FooterHeight - _expand.DesiredSize.Height) / 2, _expand.DesiredSize.Width, _expand.DesiredSize.Height));
        }
        return finalSize;
    }

    public override void Render(DrawingContext context)
    {
        if (Context is null)
        {
            return;
        }
        var style = Context.Style;
        var r = style.CornerRadius.TopLeft;
        var bounds = new Rect(Bounds.Size).Deflate(0.5);
        if (_chunkCount == 1)
        {
            if (_framePen is null || !ReferenceEquals(_framePenBrush, style.CodeBorderBrush))
            {
                _framePenBrush = style.CodeBorderBrush;
                _framePen = new Pen(style.CodeBorderBrush, 1);
            }
            context.DrawRectangle(style.CodeBackground, _framePen, bounds, r, r);
        }
        else
        {
            // 分段：首段只有上圆角，末段只有下圆角，中间段直角；各段背景首尾相接
            var top = IsFirstChunk ? r : 0;
            var bottom = IsLastChunk ? r : 0;
            var key = (Bounds.Size, top, bottom);
            var geometry = _chunkGeometry;
            if (geometry is null || _chunkGeometryKey != key)
            {
                geometry = new StreamGeometry();
                using (var g = geometry.Open())
                {
                    g.BeginFigure(new Point(bounds.Left + top, bounds.Top), true);
                    g.LineTo(new Point(bounds.Right - top, bounds.Top));
                    if (top > 0)
                    {
                        g.ArcTo(new Point(bounds.Right, bounds.Top + top), new Size(top, top), 0, false, SweepDirection.Clockwise);
                    }
                    g.LineTo(new Point(bounds.Right, bounds.Bottom - bottom));
                    if (bottom > 0)
                    {
                        g.ArcTo(new Point(bounds.Right - bottom, bounds.Bottom), new Size(bottom, bottom), 0, false, SweepDirection.Clockwise);
                    }
                    g.LineTo(new Point(bounds.Left + bottom, bounds.Bottom));
                    if (bottom > 0)
                    {
                        g.ArcTo(new Point(bounds.Left, bounds.Bottom - bottom), new Size(bottom, bottom), 0, false, SweepDirection.Clockwise);
                    }
                    g.LineTo(new Point(bounds.Left, bounds.Top + top));
                    if (top > 0)
                    {
                        g.ArcTo(new Point(bounds.Left + top, bounds.Top), new Size(top, top), 0, false, SweepDirection.Clockwise);
                    }
                    g.EndFigure(true);
                }
                _chunkGeometry = geometry;
                _chunkGeometryKey = key;
            }
            context.DrawGeometry(style.CodeBackground, null, geometry);
        }
        if (IsFirstChunk)
        {
            _title?.Draw(context, new Point(Padding, (HeaderHeight - _title.Height) / 2));
        }
        if (_lineNumbers)
        {
            var tops = _code.LineTops;
            for (var i = 0; i < _numbers.Count && i < tops.Count; i++)
            {
                _numbers[i].Draw(context, new Point(Padding, TopHeight + tops[i]));
            }
        }
        if (_collapsed && style.CodeBackground is ISolidColorBrush { Color: var bg })
        {
            // 折叠：代码区底部渐隐到背景色
            var fadeHeight = Math.Min(36, _code.Bounds.Height);
            var top = _code.Bounds.Bottom - fadeHeight;
            if (_fadeBrush is null || _fadeColor != bg)
            {
                _fadeColor = bg;
                _fadeBrush = new LinearGradientBrush
                {
                    StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative),
                    EndPoint = new RelativePoint(0, 1, RelativeUnit.Relative),
                    GradientStops = { new GradientStop(Color.FromArgb(0, bg.R, bg.G, bg.B), 0), new GradientStop(bg, 1) },
                };
            }
            context.FillRectangle(_fadeBrush, new Rect(1, top, Bounds.Width - 2, fadeHeight));
        }
    }

    public override IEnumerable<ISelectableSegment> Segments() => [_code];
}

/// <summary>
/// 代码文本：等宽、逐行排版并缓存，支持不换行时的横向滚动。可只显示一个行窗口（长代码分段），
/// 偏移始终是整个代码块中的全局偏移，因此各分段共用一个选区片段。
/// </summary>
public sealed class CodeTextView : Control, ISelectableSegment
{
    private const int LinesPerStrip = 16;
    private static readonly Cursor IbeamCursor = new(StandardCursorType.Ibeam);

    private sealed class Line
    {
        public required string Text;
        public ImmutableArray<HighlightToken> Tokens;
        public TextLayout? Layout;
        public double Width;
    }

    /// <summary>每 16 行代码共享一个轻量子视觉，流式追加或异步高亮着色时只重录变化的分条，避免重绘整块代码。</summary>
    private sealed class CodeStripVisual(CodeTextView owner) : Visual
    {
        public int StripIndex { get; set; }

        public bool SetBounds(Rect bounds)
        {
            if (Bounds == bounds)
            {
                return false;
            }
            Bounds = bounds;
            return true;
        }

        public override void Render(DrawingContext context) => owner.RenderStrip(context, StripIndex);
    }

    private readonly List<Line> _lines = [];
    private readonly List<int> _lineStarts = [];
    private readonly List<double> _lineTops = [];
    private readonly List<CodeStripVisual> _strips = [];
    private readonly Dictionary<(uint Foreground, HighlightFontStyle Style), GenericTextRunProperties> _runProps = [];
    private RenderContext? _context;
    private CodeBlockState? _state;
    private string _text = "";
    private int _windowStart;
    private int _windowLines = int.MaxValue;
    private bool _wrap;
    private double _layoutWidth = double.NaN;
    private double _offsetX;
    private double _contentWidth;
    private bool _hover;
    private bool _allStripsDirty = true;
    private int _dirtyLineStart = int.MaxValue;
    private int _dirtyLineEnd = -1;

    /// <summary>窗口内各行相对自身的顶部位置（行号用，测量后有效）。</summary>
    internal IReadOnlyList<double> LineTops => _lineTops;

    internal int VisibleLineCount => _lines.Count;

    /// <summary>当前代码分条视觉数（测试与诊断用）。</summary>
    internal int StripCount => _strips.Count;

    public SegmentKey Key { get; private set; }

    /// <summary>整个代码块的文本（选区与复制按全局偏移计算）。</summary>
    public string Text => _text;

    public Control View => this;

    public bool Wrap
    {
        get => _wrap;
        set
        {
            if (_wrap != value)
            {
                _wrap = value;
                _offsetX = 0;
                foreach (var line in _lines)
                {
                    line.Layout = null;
                }
                _layoutWidth = double.NaN;
                _allStripsDirty = true;
                InvalidateMeasure();
                InvalidateVisual();
            }
        }
    }

    public CodeTextView()
    {
        ClipToBounds = true;
    }

    internal void SyncText(string code) => _text = code;

    protected override global::Avalonia.Automation.Peers.AutomationPeer OnCreateAutomationPeer() => new Automation.TextSegmentAutomationPeer(this, () => _text);

    internal bool Set(RenderContext context, SegmentKey key, CodeBlockState state, int lineStart, int lineCount)
    {
        var contextChanged = !ReferenceEquals(_context, context);
        if (_context is not null && contextChanged)
        {
            _context.Selection.Changed -= InvalidateVisual;
        }
        if (contextChanged && VisualRoot is not null)
        {
            context.Selection.Changed += InvalidateVisual;
        }
        var styleChanged = _context is null || !ReferenceEquals(_context.Style, context.Style);
        if (styleChanged)
        {
            _runProps.Clear();
        }
        _context = context;
        Key = key;
        var reset = false;
        if (!ReferenceEquals(_state, state))
        {
            if (_state is not null)
            {
                _state.HighlightUpdated -= OnHighlightUpdated;
            }
            _state = state;
            if (VisualRoot is not null)
            {
                state.HighlightUpdated += OnHighlightUpdated;
            }
            _lines.Clear();
            reset = true;
        }
        if (styleChanged || lineStart != _windowStart || lineCount != _windowLines)
        {
            _lines.Clear();
            reset = true;
        }
        _windowStart = lineStart;
        _windowLines = lineCount;
        _text = state.Code;
        return ApplyWindow(forceChanged: reset || contextChanged, highlightOnly: false);
    }

    // 只为窗口内的行建立排版对象；文本与着色未变的行保留缓存。
    // 异步高亮仅着色且不换行、尺寸未变时只重录受影响的分条，完全跳过 Measure。
    private bool ApplyWindow(bool forceChanged = false, bool highlightOnly = false)
    {
        var state = _state!;
        var end = (int)Math.Min((long)_windowStart + _windowLines, state.LineCount);
        _lineStarts.Clear();
        var textOrCountChanged = forceChanged;
        var tokensChanged = false;
        var localDirtyStart = int.MaxValue;
        var localDirtyEnd = -1;
        var index = 0;
        for (var i = _windowStart; i < end; i++, index++)
        {
            var text = state.LineText(i);
            _lineStarts.Add(state.LineStarts[i]);
            var tokens = state.TokensFor(i, text);
            if (index < _lines.Count)
            {
                var line = _lines[index];
                var textChanged = !ReferenceEquals(line.Text, text) && line.Text != text;
                var tokenChanged = !SameTokens(line.Tokens, tokens);
                if (textChanged || tokenChanged)
                {
                    line.Text = text;
                    line.Tokens = tokens;
                    line.Layout = null;
                    if (textChanged)
                    {
                        textOrCountChanged = true;
                    }
                    else
                    {
                        tokensChanged = true;
                    }
                    if (index < localDirtyStart)
                    {
                        localDirtyStart = index;
                    }
                    localDirtyEnd = index;
                }
            }
            else
            {
                _lines.Add(new Line { Text = text, Tokens = tokens });
                textOrCountChanged = true;
                if (index < localDirtyStart)
                {
                    localDirtyStart = index;
                }
                localDirtyEnd = index;
            }
        }
        if (_lines.Count > index)
        {
            _lines.RemoveRange(index, _lines.Count - index);
            textOrCountChanged = true;
            if (index < localDirtyStart)
            {
                localDirtyStart = index;
            }
            localDirtyEnd = Math.Max(localDirtyEnd, index);
        }
        if (!textOrCountChanged && !tokensChanged)
        {
            return false;
        }
        if (forceChanged)
        {
            _allStripsDirty = true;
        }
        else if (localDirtyStart <= localDirtyEnd)
        {
            _dirtyLineStart = Math.Min(_dirtyLineStart, localDirtyStart);
            _dirtyLineEnd = Math.Max(_dirtyLineEnd, localDirtyEnd);
        }

        // 高亮到达且不换行、行数和文本都未变时：只需重建着色行的 TextLayout 并刷新对应分条
        if (highlightOnly && !textOrCountChanged && !_wrap && _context is not null && !double.IsNaN(_layoutWidth)
            && _strips.Count > 0 && _lineTops.Count == _lines.Count)
        {
            var oldScrollbar = double.IsFinite(_layoutWidth) && _contentWidth > _layoutWidth;
            var newContentWidth = 0.0;
            for (var i = 0; i < _lines.Count; i++)
            {
                var line = _lines[i];
                if (line.Layout is null)
                {
                    LayoutFor(line, _layoutWidth);
                }
                if (line.Width > newContentWidth)
                {
                    newContentWidth = line.Width;
                }
            }
            var newScrollbar = double.IsFinite(_layoutWidth) && newContentWidth > _layoutWidth;
            var oldOffsetX = _offsetX;
            _contentWidth = newContentWidth;
            ClampOffset(double.IsFinite(_layoutWidth) ? _layoutWidth : newContentWidth);
            if (oldScrollbar == newScrollbar && oldOffsetX.Equals(_offsetX))
            {
                InvalidateDirtyStripsNow();
                if (_hover)
                {
                    InvalidateVisual();
                }
                return false;
            }
            if (!oldOffsetX.Equals(_offsetX))
            {
                _allStripsDirty = true;
            }
        }

        InvalidateMeasure();
        if (forceChanged || _context?.Selection is { IsEmpty: false } or { HasHighlights: true })
        {
            InvalidateVisual();
        }
        return true;
    }

    private void InvalidateDirtyStripsNow()
    {
        if (_allStripsDirty)
        {
            InvalidateAllStrips();
            _allStripsDirty = false;
            _dirtyLineStart = int.MaxValue;
            _dirtyLineEnd = -1;
            return;
        }
        if (_dirtyLineStart <= _dirtyLineEnd && _strips.Count > 0)
        {
            var firstStrip = Math.Clamp(_dirtyLineStart / LinesPerStrip, 0, _strips.Count - 1);
            var lastStrip = Math.Clamp(_dirtyLineEnd / LinesPerStrip, 0, _strips.Count - 1);
            for (var s = firstStrip; s <= lastStrip; s++)
            {
                _strips[s].InvalidateVisual();
            }
        }
        _dirtyLineStart = int.MaxValue;
        _dirtyLineEnd = -1;
    }

    private void InvalidateAllStrips()
    {
        for (var s = 0; s < _strips.Count; s++)
        {
            _strips[s].InvalidateVisual();
        }
    }

    private static bool SameTokens(ImmutableArray<HighlightToken> a, ImmutableArray<HighlightToken> b)
    {
        if (a == b)
        {
            return true;
        }
        if (a.IsDefault || b.IsDefault || a.Length != b.Length)
        {
            return false;
        }
        for (var i = 0; i < a.Length; i++)
        {
            if (a[i] != b[i])
            {
                return false;
            }
        }
        return true;
    }

    private void OnHighlightUpdated()
    {
        if (_state is not null)
        {
            ApplyWindow(forceChanged: false, highlightOnly: true);
        }
    }

    private double LineHeight => _context?.Style.CodeLineHeight ?? 20;

    private GenericTextRunProperties RunPropertiesFor(MarkdownStyle style, uint foreground, HighlightFontStyle fontStyle)
    {
        var key = (foreground, fontStyle);
        if (!_runProps.TryGetValue(key, out var props))
        {
            var brush = foreground == 0 ? style.Foreground : BrushCache.Get(foreground);
            var face = new Typeface(style.MonospaceFontFamily,
                fontStyle.HasFlag(HighlightFontStyle.Italic) ? FontStyle.Italic : FontStyle.Normal,
                fontStyle.HasFlag(HighlightFontStyle.Bold) ? FontWeight.Bold : FontWeight.Normal);
            TextDecorationCollection? decorations = fontStyle.HasFlag(HighlightFontStyle.Underline) ? TextDecorations.Underline : null;
            props = new GenericTextRunProperties(face, style.CodeFontSize, decorations, brush);
            _runProps[key] = props;
        }
        return props;
    }

    private TextLayout LayoutFor(Line line, double maxWidth)
    {
        if (line.Layout is not null)
        {
            return line.Layout;
        }
        var style = _context!.Style;
        var typeface = new Typeface(style.MonospaceFontFamily);
        List<ValueSpan<TextRunProperties>>? overrides = null;
        if (!line.Tokens.IsDefaultOrEmpty)
        {
            overrides = new List<ValueSpan<TextRunProperties>>(line.Tokens.Length);
            foreach (var token in line.Tokens)
            {
                if (token.Start >= line.Text.Length)
                {
                    break;
                }
                var length = Math.Min(token.Length, line.Text.Length - token.Start);
                if (token.Foreground == 0 && token.Style == HighlightFontStyle.None)
                {
                    continue;
                }
                overrides.Add(new ValueSpan<TextRunProperties>(token.Start, length, RunPropertiesFor(style, token.Foreground, token.Style)));
            }
        }
        line.Layout = new TextLayout(line.Text.Length == 0 ? " " : line.Text, typeface, style.CodeFontSize, style.Foreground,
            textWrapping: _wrap ? TextWrapping.Wrap : TextWrapping.NoWrap, maxWidth: _wrap ? maxWidth : double.PositiveInfinity,
            lineHeight: LineHeight, textStyleOverrides: overrides);
        line.Width = line.Layout.WidthIncludingTrailingWhitespace;
        return line.Layout;
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        if (_context is null)
        {
            return default;
        }
        var maxWidth = double.IsFinite(availableSize.Width) ? Math.Max(availableSize.Width, 1) : double.PositiveInfinity;
        if (_wrap && !_layoutWidth.Equals(maxWidth))
        {
            foreach (var line in _lines)
            {
                line.Layout = null;
            }
            _allStripsDirty = true;
        }
        _layoutWidth = maxWidth;
        double height = 0, width = 0;
        _lineTops.Clear();
        for (var i = 0; i < _lines.Count; i++)
        {
            var line = _lines[i];
            var layout = LayoutFor(line, maxWidth);
            _lineTops.Add(height);
            height += _wrap ? layout.Height : LineHeight;
            if (line.Width > width)
            {
                width = line.Width;
            }
        }
        var oldOffsetX = _offsetX;
        _contentWidth = width;
        var viewportWidth = double.IsFinite(availableSize.Width) ? availableSize.Width : width;
        ClampOffset(viewportWidth);
        if (!oldOffsetX.Equals(_offsetX))
        {
            _allStripsDirty = true;
        }
        SyncStrips(Math.Max(1, viewportWidth), height);
        var scrollbar = !_wrap && double.IsFinite(availableSize.Width) && width > availableSize.Width ? 8 : 0;
        return new Size(double.IsFinite(availableSize.Width) ? Math.Min(width, availableSize.Width) : width, height + scrollbar);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        var width = Math.Max(1, finalSize.Width);
        for (var s = 0; s < _strips.Count; s++)
        {
            var b = _strips[s].Bounds;
            if (!b.Width.Equals(width))
            {
                _strips[s].SetBounds(new Rect(b.X, b.Y, width, b.Height));
            }
        }
        return finalSize;
    }

    private void SyncStrips(double width, double totalHeight)
    {
        var stripCount = (_lines.Count + LinesPerStrip - 1) / LinesPerStrip;
        while (_strips.Count > stripCount)
        {
            VisualChildren.Remove(_strips[^1]);
            _strips.RemoveAt(_strips.Count - 1);
        }
        while (_strips.Count < stripCount)
        {
            var strip = new CodeStripVisual(this) { StripIndex = _strips.Count };
            _strips.Add(strip);
            VisualChildren.Add(strip);
        }
        for (var s = 0; s < stripCount; s++)
        {
            var firstLine = s * LinesPerStrip;
            var lastLine = Math.Min(_lines.Count, firstLine + LinesPerStrip) - 1;
            var stripTop = _lineTops[firstLine];
            var stripBottom = lastLine + 1 < _lines.Count ? _lineTops[lastLine + 1] : totalHeight;
            var strip = _strips[s];
            strip.StripIndex = s;
            var boundsChanged = strip.SetBounds(new Rect(0, stripTop, width, Math.Max(1, stripBottom - stripTop)));
            if (_allStripsDirty || boundsChanged || (lastLine >= _dirtyLineStart && firstLine <= _dirtyLineEnd))
            {
                strip.InvalidateVisual();
            }
        }
        _allStripsDirty = false;
        _dirtyLineStart = int.MaxValue;
        _dirtyLineEnd = -1;
    }

    internal void RenderStrip(DrawingContext context, int stripIndex)
    {
        if (_context is null)
        {
            return;
        }
        var first = stripIndex * LinesPerStrip;
        if (first >= _lines.Count || first >= _lineTops.Count)
        {
            return;
        }
        var end = Math.Min(_lines.Count, first + LinesPerStrip);
        var stripTop = _lineTops[first];
        using (context.PushTransform(Matrix.CreateTranslation(-_offsetX, 0)))
        {
            for (var i = first; i < end; i++)
            {
                var layout = LayoutFor(_lines[i], _layoutWidth);
                layout.Draw(context, new Point(0, _lineTops[i] - stripTop));
            }
        }
    }

    private void ClampOffset(double viewport) => _offsetX = Math.Clamp(_offsetX, 0, Math.Max(0, _contentWidth - viewport));

    public override void Render(DrawingContext context)
    {
        if (_context is null)
        {
            return;
        }
        // 文字由各分条视觉绘制；这里只画可命中区域、选区 / 查找高亮（位于文字下方）与横向滚动条
        context.FillRectangle(Brushes.Transparent, new Rect(Bounds.Size));

        var style = _context.Style;
        var selected = _context.Selection.GetSegmentRange(Key, _text.Length);
        List<(int Start, int End, bool Current)>? highlights = null;
        if (_context.Selection.HasHighlights)
        {
            foreach (var h in _context.Selection.GetHighlights(Key, _text.Length))
            {
                (highlights ??= []).Add(h);
            }
        }
        if (highlights is not null || selected is not null)
        {
            using (context.PushTransform(Matrix.CreateTranslation(-_offsetX, 0)))
            {
                for (var i = 0; i < _lines.Count; i++)
                {
                    var line = _lines[i];
                    var y = i < _lineTops.Count ? _lineTops[i] : i * LineHeight;
                    var lineStart = _lineStarts[i];
                    var lineEnd = lineStart + line.Text.Length;
                    TextLayout? layout = null;
                    if (highlights is not null)
                    {
                        foreach (var (hs, he, current) in highlights)
                        {
                            var s = Math.Max(hs, lineStart) - lineStart;
                            var e = Math.Min(he, lineEnd) - lineStart;
                            if (e > s)
                            {
                                layout ??= LayoutFor(line, _layoutWidth);
                                var brush = current ? style.SearchCurrentBackground : style.SearchHighlightBackground;
                                foreach (var rect in layout.HitTestTextRange(s, e - s))
                                {
                                    context.FillRectangle(brush, rect.Translate(new Vector(0, y)), 2);
                                }
                            }
                        }
                    }
                    if (selected is { } range)
                    {
                        var s = Math.Max(range.Start, lineStart);
                        var e = Math.Min(range.End, lineEnd + 1);
                        if (e > s)
                        {
                            layout ??= LayoutFor(line, _layoutWidth);
                            var hasNewline = range.End > lineEnd && lineEnd < _text.Length;
                            var textEnd = Math.Min(e, lineEnd);
                            foreach (var rect in layout.HitTestTextRange(s - lineStart, Math.Max(0, textEnd - s)))
                            {
                                context.FillRectangle(style.SelectionBackground, rect.Translate(new Vector(0, y)));
                            }
                            if (hasNewline)
                            {
                                var endRect = layout.HitTestTextPosition(line.Text.Length);
                                context.FillRectangle(style.SelectionBackground, new Rect(endRect.X, y + endRect.Y, style.CodeFontSize * 0.5, endRect.Height));
                            }
                        }
                    }
                }
            }
        }

        // 横向滚动条（悬停时显示）
        if (!_wrap && _contentWidth > Bounds.Width + 0.5 && _hover)
        {
            var ratio = Bounds.Width / _contentWidth;
            var thumbWidth = Math.Max(24, Bounds.Width * ratio);
            var x = (_offsetX / (_contentWidth - Bounds.Width)) * (Bounds.Width - thumbWidth);
            context.FillRectangle(style.HoverBackground, new Rect(x, Bounds.Height - 6, thumbWidth, 4), 2);
        }
    }

    protected override void OnPointerEntered(PointerEventArgs e)
    {
        base.OnPointerEntered(e);
        _hover = true;
        Cursor = IbeamCursor;
        InvalidateVisual();
    }

    protected override void OnPointerExited(PointerEventArgs e)
    {
        base.OnPointerExited(e);
        _hover = false;
        InvalidateVisual();
    }

    protected override void OnPointerWheelChanged(PointerWheelEventArgs e)
    {
        if (_wrap || _contentWidth <= Bounds.Width)
        {
            base.OnPointerWheelChanged(e);
            return;
        }
        // 横向滚动：触控板横向手势，或按住 Shift 的纵向滚轮
        var dx = e.Delta.X != 0 ? -e.Delta.X : e.KeyModifiers.HasFlag(KeyModifiers.Shift) ? -e.Delta.Y : 0;
        if (dx == 0)
        {
            base.OnPointerWheelChanged(e);
            return;
        }
        var oldOffsetX = _offsetX;
        _offsetX += dx * 40;
        ClampOffset(Bounds.Width);
        if (!oldOffsetX.Equals(_offsetX))
        {
            InvalidateVisual();
            InvalidateAllStrips();
        }
        e.Handled = true;
    }

    public int HitTest(Point point)
    {
        if (_lines.Count == 0)
        {
            return 0;
        }
        var p = new Point(point.X + _offsetX, point.Y);
        double y = 0;
        for (var i = 0; i < _lines.Count; i++)
        {
            var layout = LayoutFor(_lines[i], _layoutWidth);
            var height = _wrap ? layout.Height : LineHeight;
            if (p.Y < y + height || i == _lines.Count - 1)
            {
                var hit = layout.HitTestPoint(new Point(p.X, Math.Max(0, p.Y - y)));
                return _lineStarts[i] + Math.Clamp(hit.TextPosition, 0, _lines[i].Text.Length);
            }
            y += height;
        }
        return 0;
    }

    public Rect GetCharacterBounds(int offset)
    {
        double y = 0;
        for (var i = 0; i < _lines.Count; i++)
        {
            var layout = LayoutFor(_lines[i], _layoutWidth);
            var height = _wrap ? layout.Height : LineHeight;
            var lineEnd = _lineStarts[i] + _lines[i].Text.Length;
            if (offset <= lineEnd || i == _lines.Count - 1)
            {
                var rect = layout.HitTestTextPosition(Math.Clamp(offset - _lineStarts[i], 0, _lines[i].Text.Length));
                return rect.Translate(new Vector(-_offsetX, y));
            }
            y += height;
        }
        return default;
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        if (_context is not null)
        {
            _context.Selection.Changed += InvalidateVisual;
        }
        if (_state is not null)
        {
            _state.HighlightUpdated += OnHighlightUpdated;
            // 回收期间可能已有新的着色结果
            ApplyWindow();
        }
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        if (_context is not null)
        {
            _context.Selection.Changed -= InvalidateVisual;
        }
        if (_state is not null)
        {
            _state.HighlightUpdated -= OnHighlightUpdated;
        }
    }
}

/// <summary>按 ARGB 复用画刷。</summary>
internal static class BrushCache
{
    private static readonly Dictionary<uint, IBrush> Brushes = [];

    public static IBrush Get(uint argb)
    {
        lock (Brushes)
        {
            if (!Brushes.TryGetValue(argb, out var brush))
            {
                brush = new ImmutableSolidColorBrush(Color.FromUInt32(argb));
                Brushes[argb] = brush;
            }
            return brush;
        }
    }
}
