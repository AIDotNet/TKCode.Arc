using System.Buffers;
using System.Collections.Concurrent;
using System.Text;
using Avalonia.Media.Fonts.Tables.Cmap;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Media.TextFormatting;
using StreamMark.Avalonia.Selection;
using StreamMark.Avalonia.Extensions;
using StreamMark.Core.Model;

namespace StreamMark.Avalonia.Rendering;

/// <summary>
/// 一个文本片段（段落、标题、表格单元格等）：用一个 <see cref="TextLayout"/> 自绘，
/// 行内样式通过自定义 <see cref="ITextSource"/> 提供，公式 / 图片 / 复选框以 <see cref="DrawableTextRun"/> 嵌入。
/// 选区高亮只绘制矩形，不触发重新排版。
/// </summary>
public sealed class TextSegmentView : Control, ISelectableSegment
{
    private InlineContent _content = InlineContent.Empty;
    private RenderContext? _context;
    private double _fontSize = 14;
    private FontWeight _fontWeight = FontWeight.Normal;
    private IBrush? _foreground;
    private TextAlignment _alignment = TextAlignment.Left;
    private TextWrapping _wrapping = TextWrapping.Wrap;

    private SegmentLayout? _layout;
    // 内容变化后需要重新排版的起点（int.MaxValue 表示排版有效）
    private int _dirtyFrom = int.MaxValue;
    private readonly List<SegmentLineVisual> _lines = [];
    private readonly TailFade _fade;
    private bool _isDocumentTail;
#pragma warning disable CS0618 // TextRunCache 是 Avalonia 12 的预览 API（[Unstable]），只在这里使用
    // 已整形文本段的缓存：流式追加时只为变化处之后的文本重新整形
    private readonly TextRunCache _runCache = new();
#pragma warning restore CS0618

    /// <summary>是否使用整形缓存（测试与基准对比用）。</summary>
    internal static bool UseRunCache { get; set; } = true;
    private int _hoverLink = -1;
    private bool _hoverClickable;

    public TextSegmentView()
    {
        ClipToBounds = false;
        _fade = new TailFade(this);
    }

    /// <summary>片段身份（选区坐标）。</summary>
    public SegmentKey Key { get; private set; }

    /// <summary>是否是文档最后一个片段（流式时绘制渐显和光标）。</summary>
    public bool IsDocumentTail
    {
        get => _isDocumentTail;
        set
        {
            if (_isDocumentTail == value)
            {
                return;
            }
            _isDocumentTail = value;
            InvalidateVisual();
            // 不再是末尾时，正在渐显的文字继续渐显完（到时由定时器结束）
            if (value)
            {
                RefreshFade();
            }
        }
    }

    public InlineContent Content => _content;

    string ISelectableSegment.Text => _content.Text;

    Control ISelectableSegment.View => this;

    /// <summary>当前排版（逐行）。</summary>
    public SegmentLayout? Layout => _layout;

    /// <summary>链接点击（冒泡）。</summary>
    public static readonly RoutedEvent<LinkClickedEventArgs> LinkClickedEvent =
        RoutedEvent.Register<TextSegmentView, LinkClickedEventArgs>(nameof(LinkClicked), RoutingStrategies.Bubble);

    public event EventHandler<LinkClickedEventArgs>? LinkClicked
    {
        add => AddHandler(LinkClickedEvent, value);
        remove => RemoveHandler(LinkClickedEvent, value);
    }

    /// <summary>已加载的图片被点击（冒泡）。未处理时打开全屏查看。</summary>
    public static readonly RoutedEvent<LinkHoveredEventArgs> LinkHoveredEvent =
        RoutedEvent.Register<TextSegmentView, LinkHoveredEventArgs>(nameof(LinkHovered), RoutingStrategies.Bubble);

    public event EventHandler<LinkHoveredEventArgs>? LinkHovered
    {
        add => AddHandler(LinkHoveredEvent, value);
        remove => RemoveHandler(LinkHoveredEvent, value);
    }

    public static readonly RoutedEvent<ImageClickedEventArgs> ImageClickedEvent =
        RoutedEvent.Register<TextSegmentView, ImageClickedEventArgs>(nameof(ImageClicked), RoutingStrategies.Bubble);

    public event EventHandler<ImageClickedEventArgs>? ImageClicked
    {
        add => AddHandler(ImageClickedEvent, value);
        remove => RemoveHandler(ImageClickedEvent, value);
    }

    /// <summary>可点击的行内组件被点击（冒泡），例如引用标签。</summary>
    public static readonly RoutedEvent<ComponentClickedEventArgs> ComponentClickedEvent =
        RoutedEvent.Register<TextSegmentView, ComponentClickedEventArgs>(nameof(ComponentClicked), RoutingStrategies.Bubble);

    public event EventHandler<ComponentClickedEventArgs>? ComponentClicked
    {
        add => AddHandler(ComponentClickedEvent, value);
        remove => RemoveHandler(ComponentClickedEvent, value);
    }

    protected override global::Avalonia.Automation.Peers.AutomationPeer OnCreateAutomationPeer() =>
        new Automation.TextSegmentAutomationPeer(this, () => BlockText.PlainText(_content, 0, int.MaxValue, o => _context?.Plugins.FindInlineComponent(o.Value)?.ToPlainText(o)));

    /// <summary>设置内容与外观；内容或外观未变时不重新排版。</summary>
    public void Set(RenderContext context, SegmentKey key, InlineContent content, double fontSize, FontWeight weight = default,
        IBrush? foreground = null, TextAlignment alignment = TextAlignment.Left, TextWrapping wrapping = TextWrapping.Wrap)
    {
        if (weight == default)
        {
            weight = FontWeight.Normal;
        }
        // 外层 HTML 盒子继承的对齐、字号、粗细与颜色
        if (context.Inherited is { } inherited)
        {
            if (alignment == TextAlignment.Left && inherited.Align != TextAlign.Inherit)
            {
                alignment = inherited.Align switch
                {
                    TextAlign.Center => TextAlignment.Center,
                    TextAlign.Right => TextAlignment.Right,
                    TextAlign.Justify => TextAlignment.Justify,
                    _ => TextAlignment.Left,
                };
            }
            // 绝对字号按与正文字号的比例缩放（标题保持相对大小）
            if (inherited.FontSize is { } px)
            {
                fontSize *= px / context.Style.FontSize;
            }
            else if (inherited.FontScale is { } scale)
            {
                fontSize *= scale;
            }
            if (inherited.Bold == true && weight < FontWeight.Bold)
            {
                weight = FontWeight.Bold;
            }
            foreground ??= HtmlPaint.Foreground(context, inherited.Foreground);
        }
        var keyChanged = Key != key;
        Key = key;
        var changed = !ReferenceEquals(_context, context) || !ReferenceEquals(_content, content) && _content != content ||
                      _fontSize != fontSize || _fontWeight != weight || !Equals(_foreground, foreground) || _alignment != alignment || _wrapping != wrapping;
        if (!changed)
        {
            if (keyChanged)
            {
                InvalidateVisual();
            }
            return;
        }
        if (_context is not null && !ReferenceEquals(_context.Selection, context.Selection))
        {
            _context.Selection.Changed -= OnSelectionChanged;
            if (IsAttached)
            {
                context.Selection.Changed += OnSelectionChanged;
            }
        }
        else if (_context is null && IsAttached)
        {
            context.Selection.Changed += OnSelectionChanged;
        }
        // 只追加且样式不变时，保留差异点之前的整形结果
        var appendOnly = ReferenceEquals(_context, context) && _fontSize == fontSize && _fontWeight == weight && Equals(_foreground, foreground);
        var from = appendOnly ? FirstDifference(_content, content) : 0;
#pragma warning disable CS0618
        if (from <= 0)
        {
            _runCache.Invalidate();
            _fullRebuild = true;
        }
        else
        {
            // 缓存以"段落"（换行符分隔）为单位、按段落起点存放整段的整形结果；InvalidateFrom 只丢弃起点不早于给定位置的条目。
            // 所以必须从差异点所在段落的起点失效，否则旧段落（连同它的段落结束）被复用，追加的文字另起一段，变成硬换行
            var paragraphStart = from > 0 ? content.Text.AsSpan(0, Math.Min(from, content.Text.Length)).LastIndexOfAny('\n', '\r', '\u2029') + 1 : 0;
            _runCache.InvalidateFrom(paragraphStart);
        }
#pragma warning restore CS0618
        var restyled = _alignment != alignment || _wrapping != wrapping;
        _context = context;
        _content = content;
        _fontSize = fontSize;
        _fontWeight = weight;
        _foreground = foreground;
        _alignment = alignment;
        _wrapping = wrapping;
        InvalidateLayout(restyled ? 0 : from);
    }

    private bool IsAttached => VisualRoot is not null;

    // 新旧内容第一个不同的位置：文本公共前缀，且该位置之前的样式区间、对象完全相同
    private static int FirstDifference(InlineContent old, InlineContent next)
    {
        var limit = old.Text.AsSpan().CommonPrefixLength(next.Text);
        foreach (var span in old.Spans)
        {
            if (span.Start >= limit)
            {
                break;
            }
            var match = next.Spans.FirstOrDefault(s => s.Start == span.Start);
            if (match != span)
            {
                // 区间被延长（如粗体继续增长）时从区间开头重新整形
                limit = Math.Min(limit, span.Start);
            }
        }
        foreach (var span in next.Spans)
        {
            if (span.Start < limit && !old.Spans.Contains(span))
            {
                limit = Math.Min(limit, span.Start);
            }
        }
        foreach (var obj in next.Objects)
        {
            if (obj.Offset < limit && !old.Objects.Contains(obj))
            {
                limit = Math.Min(limit, obj.Offset);
            }
        }
        return limit;
    }

    // 从 from 处起重新排版（0 = 整段）；只有变化的行会重新录制
    private void InvalidateLayout(int from = 0)
    {
        _dirtyFrom = Math.Min(_dirtyFrom, Math.Max(0, from));
        InvalidateMeasure();
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        if (_context is not null)
        {
            _context.Selection.Changed += OnSelectionChanged;
        }
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        if (_context is not null)
        {
            _context.Selection.Changed -= OnSelectionChanged;
        }
        _cardTimer?.Stop();
        CloseCard();
        _fade.Detach();
        // 归还图片句柄；重新挂载时排版会重新取得（通常命中内存缓存）
        if (_images.Count > 0)
        {
            foreach (var slot in _images.Values)
            {
                slot.Dispose();
            }
            _images.Clear();
            InvalidateLayout();
        }
    }

    private void OnSelectionChanged()
    {
        InvalidateVisual();
        // 带底色的行（行内代码等）把选区画在自己的行视觉里，需要跟着重录
        if (_layout is { } layout)
        {
            for (var i = 0; i < _lines.Count && i < layout.LineCount; i++)
            {
                if (HasBackgroundSpan(layout.LineStart(i), layout.LineStart(i) + layout.LineLength(i)))
                {
                    _lines[i].InvalidateVisual();
                }
            }
        }
    }

    private static bool HasBackground(InlineStyle style) => (style & (InlineStyle.Code | InlineStyle.Keyboard | InlineStyle.Mark)) != 0;

    private bool HasBackgroundSpan(int lineStart, int lineEnd)
    {
        foreach (var span in _content.Spans)
        {
            if (HasBackground(span.Style) && span.End > lineStart && span.Start < lineEnd)
            {
                return true;
            }
        }
        return false;
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        var layout = EnsureLayout(availableSize.Width);
        if (layout is null)
        {
            return default;
        }
        var width = _wrapping == TextWrapping.Wrap && double.IsFinite(availableSize.Width)
            ? Math.Min(layout.WidthIncludingTrailingWhitespace, availableSize.Width)
            : layout.WidthIncludingTrailingWhitespace;
        return new Size(Math.Ceiling(width), Math.Ceiling(layout.Height));
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        // 对齐方式需要按最终宽度排版
        if (_alignment != TextAlignment.Left && _wrapping == TextWrapping.Wrap)
        {
            EnsureLayout(finalSize.Width);
        }
        if (IsDocumentTail)
        {
            RefreshFade();
        }
        return finalSize;
    }

    private SegmentLayout? EnsureLayout(double width)
    {
        if (_context is null)
        {
            return null;
        }
        var maxWidth = _wrapping == TextWrapping.Wrap && double.IsFinite(width) ? Math.Max(width, 1) : double.PositiveInfinity;
        var widthChanged = _layout is null || !_layout.MaxWidth.Equals(maxWidth);
        if (_layout is not null && _dirtyFrom == int.MaxValue && (!widthChanged || _wrapping == TextWrapping.NoWrap))
        {
            return _layout;
        }
        // 图片按可用宽度缩放：宽度变化时缓存的图片 run 失效
        if (widthChanged && !_runCacheWidth.Equals(maxWidth) && HasWidthDependentObjects())
        {
#pragma warning disable CS0618
            _runCache.Invalidate();
#pragma warning restore CS0618
            _fullRebuild = true;
        }
        var from = widthChanged && _wrapping == TextWrapping.Wrap ? 0 : _dirtyFrom == int.MaxValue ? 0 : _dirtyFrom;
        if (from == 0)
        {
            _usedImages.Clear();
        }
        var source = new InlineTextSource(this, _context, _content, _fontSize, _fontWeight, _foreground ?? _context.Style.Foreground, maxWidth);
        var fixedHeight = new GenericTextParagraphProperties(source.DefaultProperties, _alignment, _wrapping,
            lineHeight: _context.Style.LineHeight * (_fontSize / _context.Style.FontSize));
        var natural = new GenericTextParagraphProperties(source.DefaultProperties, _alignment, _wrapping);
        _layout ??= new SegmentLayout();
#pragma warning disable CS0618
        var changed = _layout.Format(source, _content.Text.Length, maxWidth, fixedHeight, natural, from, UseRunCache ? _runCache : null);
#pragma warning restore CS0618
        _dirtyFrom = int.MaxValue;
        // 只有所有 run 都重新生成时，"本次用到的图片"才完整；部分复用缓存时不释放
        if (from == 0 && (_fullRebuild || !UseRunCache))
        {
            ReleaseUnusedImages();
        }
        _fullRebuild = false;
        _runCacheWidth = maxWidth;
        SyncLines(changed);
        return _layout;
    }

    // 行视觉与排版的行一一对应；changed 之前的行对象未变，不重新录制
    private void SyncLines(int changed)
    {
        var layout = _layout!;
        while (_lines.Count > layout.LineCount)
        {
            VisualChildren.Remove(_lines[^1]);
            _lines.RemoveAt(_lines.Count - 1);
        }
        while (_lines.Count < layout.LineCount)
        {
            var visual = new SegmentLineVisual(this) { Index = _lines.Count };
            _lines.Add(visual);
            VisualChildren.Add(visual);
        }
        for (var i = Math.Min(changed, _lines.Count); i < _lines.Count; i++)
        {
            var line = layout.LineAt(i);
            _lines[i].Index = i;
            _lines[i].SetBounds(new Rect(0, layout.LineTop(i), Math.Max(1, line.Start + line.WidthIncludingTrailingWhitespace), line.Height));
            _lines[i].InvalidateVisual();
        }
    }

    /// <summary>行视觉的绘制：文字（渐显中的部分裁掉）与删除线。</summary>
    internal void RenderLine(DrawingContext context, int index, double? clipRight)
    {
        if (_layout is not { } layout || index >= layout.LineCount || layout.IsEmptyLine(index))
        {
            return;
        }
        var line = layout.LineAt(index);
        if (clipRight is { } right)
        {
            using (context.PushClip(new Rect(-10000, -line.Height, right + 10000, line.Height * 3)))
            {
                line.Draw(context, default);
            }
        }
        else
        {
            line.Draw(context, default);
        }
        DrawStrikethrough(context, layout, index);
        DrawSelectionOverBackgrounds(context, layout, index);
    }

    // 选区在片段视图里画在文字下方，会被行内代码等 run 底色盖住；底色范围内的选中部分在这里补画一层，
    // 让选中的代码和普通文字一样呈现选区色
    private void DrawSelectionOverBackgrounds(DrawingContext context, SegmentLayout layout, int index)
    {
        if (_context is null || _context.Selection.GetSegmentRange(Key, _content.Text.Length) is not { } selected)
        {
            return;
        }
        var lineStart = layout.LineStart(index);
        var lineEnd = lineStart + layout.LineLength(index);
        var top = layout.LineTop(index);
        foreach (var span in _content.Spans)
        {
            if (!HasBackground(span.Style))
            {
                continue;
            }
            var start = Math.Max(Math.Max(span.Start, selected.Start), lineStart);
            var end = Math.Min(Math.Min(span.End, selected.End), lineEnd);
            if (end <= start)
            {
                continue;
            }
            foreach (var rect in layout.HitTestTextRange(start, end - start))
            {
                context.FillRectangle(_context.Style.SelectionBackground, rect.Translate(new Vector(0, -top)));
            }
        }
    }

    /// <summary>重新计算末尾渐显（排版、末尾状态变化或渐显结束时）。</summary>
    internal void RefreshFade() =>
        _fade.Update(_layout, _lines, IsDocumentTail ? _context?.Streaming : null, _content.Text.Length, _fontSize, Bounds.Size);

    // 已整形 run 缓存是否整体失效（下一次排版会对每个 run 调用 GetTextRun）
    private bool _fullRebuild = true;

    // 缓存中的 run 按哪个可用宽度生成
    private double _runCacheWidth = double.NaN;

    private bool HasWidthDependentObjects()
    {
        foreach (var obj in _content.Objects)
        {
            if (obj.Kind is InlineObjectKind.Image or InlineObjectKind.Component)
            {
                return true;
            }
        }
        return false;
    }

    /// <summary>图片等异步资源就绪后重新排版（缓存的 run 仍是旧状态，整体失效）。</summary>
    internal void OnResourceLoaded()
    {
#pragma warning disable CS0618
        _runCache.Invalidate();
#pragma warning restore CS0618
        _fullRebuild = true;
        InvalidateLayout();
    }

    // 本片段中图片的加载状态与缓存句柄（按解析后的地址）
    private readonly Dictionary<string, ImageSlot> _images = [];

    /// <summary>已加载完成的图片数（测试与诊断用）。</summary>
    internal int LoadedImageCount => _images.Values.Count(s => s.Image is not null);

    /// <summary>加载失败或被策略拒绝的图片数（测试与诊断用）。</summary>
    internal int FailedImageCount => _images.Values.Count(s => s.Status is Images.ImageLoadStatus.Failed or Images.ImageLoadStatus.Blocked);

    internal IReadOnlyCollection<ImageSlot> ImageSlots => _images.Values;

    internal IEnumerable<string> DescribeImages() => _images.Select(p => $"{p.Key} {p.Value.Status} {p.Value.Reason}");

    private readonly HashSet<string> _usedImages = [];

    internal ImageSlot SlotFor(RenderContext context, string url, Size? declared, double maxWidth)
    {
        var resolved = Extensions.MarkdownServices.ResolveImageUrl(url, context.ImageBaseUri);
        if (!_images.TryGetValue(resolved, out var slot))
        {
            slot = new ImageSlot(OnResourceLoaded);
            _images[resolved] = slot;
        }
        _usedImages.Add(resolved);
        // 解码宽度：显示宽度 × 渲染缩放（Retina 为 2 倍），避免把大图原尺寸常驻内存；宿主可用解码密度上限进一步压缩
        var scaling = TopLevel.GetTopLevel(this)?.RenderScaling ?? 1;
        if (context.Services.Images.DecodeDensity is { } density)
        {
            scaling = Math.Min(scaling, density);
        }
        var displayWidth = declared?.Width ?? (double.IsFinite(maxWidth) ? maxWidth : 1024);
        slot.Ensure(context.Services.Images, resolved, (int)Math.Ceiling(Math.Max(1, displayWidth) * scaling));
        return slot;
    }

    // 文档内是按显示宽度缩小解码的图：查看器按原始尺寸打开，先画缩小图，原图加载完成后替换；
    // 关闭时把原图移出内存缓存，不让它常驻
    internal ImageViewer? ShowViewer(ImageSlot slot, IImage loaded, string? caption)
    {
        var preview = slot.AcquireLease();
        var natural = slot.PixelSize is { Width: > 0, Height: > 0 } pixels ? new Size(pixels.Width, pixels.Height) : loaded.Size;
        var current = preview?.Image ?? loaded;
        var viewer = ImageViewer.Show(this, natural, (context, rect) => context.DrawImage(current, new Rect(current.Size), rect), null, caption);
        if (viewer is null)
        {
            preview?.Dispose();
            return null;
        }
        Images.ImageLease? original = null;
        var closed = false;
        viewer.Closed += (_, _) =>
        {
            closed = true;
            preview?.Dispose();
            original?.Evict();
            original?.Dispose();
        };
        if (slot.IsDownscaled && slot.Pipeline is { } pipeline)
        {
            LoadOriginal(pipeline, slot.Url);
        }
        return viewer;

        async void LoadOriginal(Images.ImagePipeline pipeline, string url)
        {
            Images.ImageLoadResult result;
            try
            {
                result = await pipeline.LoadAsync(url, 0);
            }
            catch (Exception)
            {
                return;
            }
            if (result.Lease is not { } lease)
            {
                return;
            }
            if (closed)
            {
                lease.Evict();
                lease.Dispose();
                return;
            }
            original = lease;
            current = lease.Image;
            viewer.InvalidateVisual();
        }
    }

    private void ReleaseUnusedImages()
    {
        foreach (var url in _images.Keys.Where(u => !_usedImages.Contains(u)).ToList())
        {
            _images[url].Dispose();
            _images.Remove(url);
        }
    }

    private ImageSlot? ImageAt(Point point, out InlineObject? image)
    {
        image = null;
        if (_layout is null || _context is null)
        {
            return null;
        }
        var hit = _layout.HitTestPoint(point);
        if (!hit.IsInside)
        {
            return null;
        }
        foreach (var obj in _content.Objects)
        {
            if (obj.Kind == InlineObjectKind.Image && (obj.Offset == hit.TextPosition || obj.Offset == hit.TextPosition - 1 && hit.IsTrailing))
            {
                image = obj;
                var resolved = Extensions.MarkdownServices.ResolveImageUrl(obj.Value, _context.ImageBaseUri);
                return _images.GetValueOrDefault(resolved);
            }
        }
        return null;
    }

    public override void Render(DrawingContext context)
    {
        var layout = _layout;
        if (layout is null || _context is null)
        {
            return;
        }
        // 文字由各行视觉绘制；这里只画可命中区域、选区 / 查找高亮（在文字下方）与流式光标
        context.FillRectangle(Brushes.Transparent, new Rect(Bounds.Size));

        foreach (var (start, end, current) in _context.Selection.GetHighlights(Key, _content.Text.Length))
        {
            var brush = current ? _context.Style.SearchCurrentBackground : _context.Style.SearchHighlightBackground;
            foreach (var rect in layout.HitTestTextRange(start, end - start))
            {
                context.FillRectangle(brush, rect, 2);
            }
        }
        if (_context.Selection.GetSegmentRange(Key, _content.Text.Length) is { } selected)
        {
            foreach (var rect in layout.HitTestTextRange(selected.Start, selected.End - selected.Start))
            {
                context.FillRectangle(_context.Style.SelectionBackground, rect);
            }
        }

        if (IsDocumentTail && _context.Streaming is { IsActive: true, ShowCaret: true })
        {
            var end = layout.HitTestTextPosition(_content.Text.Length);
            var height = Math.Min(end.Height, _fontSize * 1.25);
            var caret = new Rect(end.Right + 2, end.Bottom - height - (end.Height - height) / 2, Math.Max(2, _fontSize * 0.45), height);
            context.FillRectangle(_foreground ?? _context.Style.Foreground, caret);
        }
    }

    private void DrawStrikethrough(DrawingContext context, SegmentLayout layout, int index)
    {
        Pen? pen = null;
        var lineStart = layout.LineStart(index);
        var lineEnd = lineStart + layout.LineLength(index);
        var top = layout.LineTop(index);
        foreach (var span in _content.Spans)
        {
            if (!span.Style.HasFlag(InlineStyle.Strikethrough) || span.End <= lineStart || span.Start >= lineEnd)
            {
                continue;
            }
            pen ??= new Pen(_foreground ?? _context!.Style.Foreground, Math.Max(1, _fontSize / 14));
            var start = Math.Max(span.Start, lineStart);
            foreach (var rect in layout.HitTestTextRange(start, Math.Min(span.End, lineEnd) - start))
            {
                var y = Math.Round(rect.Top - top + rect.Height * 0.54) + 0.5;
                context.DrawLine(pen, new Point(rect.Left, y), new Point(rect.Right, y));
            }
        }
    }

    /// <summary>本地坐标 → 字符偏移。</summary>
    public int HitTest(Point point)
    {
        if (_layout is null)
        {
            return 0;
        }
        var result = _layout.HitTestPoint(point);
        return Math.Clamp(result.TextPosition, 0, _content.Text.Length);
    }

    /// <summary>字符偏移 → 本地矩形。</summary>
    public Rect GetCharacterBounds(int offset) => _layout?.HitTestTextPosition(Math.Clamp(offset, 0, _content.Text.Length)) ?? default;

    /// <summary>指针处可点击的组件（成对组件的文字或自闭合组件对象）。</summary>
    private (string Name, ComponentAttributes Attributes, int Start, int Length)? ComponentAt(Point point)
    {
        if (_layout is null || _context is null || (_content.Components.IsEmpty && _content.Objects.IsEmpty))
        {
            return null;
        }
        var hit = _layout.HitTestPoint(point);
        if (!hit.IsInside)
        {
            return null;
        }
        var index = hit.TextPosition;
        foreach (var obj in _content.Objects)
        {
            if (obj.Kind == InlineObjectKind.Component && (obj.Offset == index || obj.Offset == index - 1 && hit.IsTrailing)
                && _context.Plugins.FindInlineComponent(obj.Value) is { } renderer && renderer.IsClickable(obj.Attributes))
            {
                return (obj.Value, obj.Attributes, obj.Offset, 1);
            }
        }
        foreach (var span in _content.Spans)
        {
            if (span.ComponentIndex >= 0 && index >= span.Start && index < span.End)
            {
                var component = _content.Components[span.ComponentIndex];
                if (_context.Plugins.FindInlineComponent(component.Name) is { } renderer && renderer.IsClickable(component.Attributes))
                {
                    return (component.Name, component.Attributes, span.Start, span.Length);
                }
            }
        }
        return null;
    }

    private int LinkAt(Point point)
    {
        if (_layout is null || _content.Links.IsEmpty)
        {
            return -1;
        }
        var hit = _layout.HitTestPoint(point);
        if (!hit.IsInside)
        {
            return -1;
        }
        var index = hit.TextPosition;
        foreach (var span in _content.Spans)
        {
            if (span.LinkIndex >= 0 && index >= span.Start && index < span.End)
            {
                return LinkDecisionFor(span.LinkIndex) == LinkDecision.Block ? -1 : span.LinkIndex;
            }
        }
        return -1;
    }

    private string? ComponentUrl(string name, ComponentAttributes attributes)
    {
        if (name == "cite" && attributes["href"] is { Length: > 0 } href)
        {
            return href;
        }
        if (name == "citation")
        {
            return attributes["href"] ?? (attributes["id"] is { } id ? _context?.Services.Sources?.GetSource(id)?.Url : null);
        }
        return null;
    }

    // 按链接策略打开地址（被禁止的地址不触发事件）
    private void OpenUrl(string url, string? title)
    {
        if (_context is null)
        {
            return;
        }
        var decision = _context.Services.Links.Evaluate(url);
        if (decision != LinkDecision.Block)
        {
            RaiseEvent(new LinkClickedEventArgs(LinkClickedEvent, this, url, title, decision == LinkDecision.Confirm));
        }
    }

    // ---- 引用来源卡片 ----

    private SourceCard? _card;
    private (int Start, int Length)? _cardRange;
    private global::Avalonia.Threading.DispatcherTimer? _cardTimer;

    /// <summary>悬停显示来源卡片的延迟。</summary>
    internal static TimeSpan CardDelay { get; set; } = TimeSpan.FromMilliseconds(350);

    private void UpdateCard(Point position)
    {
        var component = ComponentAt(position);
        CitationSource? source = component is { Name: "citation" } c && c.Attributes["id"] is { } id ? _context?.Services.Sources?.GetSource(id) : null;
        if (source is null || component is null)
        {
            ScheduleCard(null, null);
            return;
        }
        var range = (component.Value.Start, component.Value.Length);
        if (_cardRange == range && (_card is not null || _cardTimer is { IsEnabled: true }))
        {
            return;
        }
        ScheduleCard(range, source);
    }

    private void ScheduleCard((int Start, int Length)? range, CitationSource? source)
    {
        _cardTimer?.Stop();
        if (range is null || source is null)
        {
            if (_card is null)
            {
                _cardRange = null;
                return;
            }
            // 给指针一点时间移进卡片
            _cardTimer = new global::Avalonia.Threading.DispatcherTimer(TimeSpan.FromMilliseconds(200), global::Avalonia.Threading.DispatcherPriority.Normal, (_, _) =>
            {
                _cardTimer?.Stop();
                if (_card is { IsPointerInside: false })
                {
                    CloseCard();
                }
            });
            _cardTimer.Start();
            return;
        }
        CloseCard();
        _cardRange = range;
        _cardTimer = new global::Avalonia.Threading.DispatcherTimer(CardDelay, global::Avalonia.Threading.DispatcherPriority.Normal, (_, _) =>
        {
            _cardTimer?.Stop();
            ShowCard(range.Value, source);
        });
        _cardTimer.Start();
    }

    /// <summary>立即显示某个范围的来源卡片（测试与键盘访问用）。</summary>
    internal SourceCard? ShowCard((int Start, int Length) range, CitationSource source)
    {
        if (_layout is null || _context is null)
        {
            return null;
        }
        CloseCard();
        var rects = _layout.HitTestTextRange(range.Start, range.Length).ToList();
        if (rects.Count == 0)
        {
            return null;
        }
        var anchor = rects.Aggregate((a, b) => a.Union(b));
        var url = source.Url;
        _cardRange = range;
        _card = SourceCard.Show(this, anchor, source, _context.Style, url is null ? null : () => OpenUrl(url, source.Title));
        return _card;
    }

    private void CloseCard()
    {
        _card?.Close();
        _card = null;
        _cardRange = null;
    }

    // 各链接的策略判断（内容或上下文变化时重算）
    private LinkDecision[]? _linkDecisions;
    private InlineContent? _linkDecisionsFor;
    private RenderContext? _linkDecisionsContext;

    internal LinkDecision LinkDecisionFor(int link)
    {
        if (_context is null || link < 0 || link >= _content.Links.Length)
        {
            return LinkDecision.Block;
        }
        if (_linkDecisions is null || !ReferenceEquals(_linkDecisionsFor, _content) || !ReferenceEquals(_linkDecisionsContext, _context))
        {
            var policy = _context.Services.Links;
            var decisions = new LinkDecision[_content.Links.Length];
            for (var i = 0; i < decisions.Length; i++)
            {
                var target = _content.Links[i];
                decisions[i] = policy.Evaluate(target.Url, target.IsAutoLink ? null : LinkText(i));
            }
            _linkDecisions = decisions;
            _linkDecisionsFor = _content;
            _linkDecisionsContext = _context;
        }
        return _linkDecisions[link];
    }

    private string LinkText(int link)
    {
        var sb = new StringBuilder();
        foreach (var span in _content.Spans)
        {
            if (span.LinkIndex == link)
            {
                sb.Append(_content.Text.AsSpan(span.Start, span.Length));
            }
        }
        return sb.ToString().Replace(InlineObject.Placeholder.ToString(), "", StringComparison.Ordinal);
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        var position = e.GetPosition(this);
        var link = LinkAt(position);
        var clickable = link >= 0 || ComponentAt(position) is not null || ImageAt(position, out _) is { Status: not Images.ImageLoadStatus.Blocked };
        if (_context?.Services.Sources is not null)
        {
            UpdateCard(position);
        }
        if (link != _hoverLink)
        {
            RaiseEvent(new LinkHoveredEventArgs(LinkHoveredEvent, this, link >= 0 ? _content.Links[link].Url : null));
        }
        if (link != _hoverLink || clickable != _hoverClickable)
        {
            _hoverLink = link;
            _hoverClickable = clickable;
            Cursor = clickable ? new Cursor(StandardCursorType.Hand) : Cursor.Default;
        }
    }

    protected override void OnPointerExited(PointerEventArgs e)
    {
        base.OnPointerExited(e);
        if (_card is not null || _cardTimer is not null)
        {
            ScheduleCard(null, null);
        }
        if (_hoverLink >= 0)
        {
            RaiseEvent(new LinkHoveredEventArgs(LinkHoveredEvent, this, null));
        }
        _hoverLink = -1;
        _hoverClickable = false;
        Cursor = Cursor.Default;
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        if (e.InitialPressMouseButton != MouseButton.Left)
        {
            return;
        }
        // 拖拽选择时不触发链接 / 组件
        if (_context is not { Selection.IsEmpty: true })
        {
            return;
        }
        var position = e.GetPosition(this);
        if (ImageAt(position, out var imageObject) is { } slot)
        {
            switch (slot.Status)
            {
                case Images.ImageLoadStatus.Failed:
                    slot.Retry();
                    break;
                case Images.ImageLoadStatus.NeedsConsent:
                    slot.Approve();
                    break;
                case Images.ImageLoadStatus.Loaded when LinkAt(position) is >= 0 and var imageLink:
                    {
                        // 链接中的图片（徽章、赞助商标志）：打开链接
                        var target = _content.Links[imageLink];
                        RaiseEvent(new LinkClickedEventArgs(LinkClickedEvent, this, target.Url, target.Title, LinkDecisionFor(imageLink) == LinkDecision.Confirm));
                        break;
                    }
                case Images.ImageLoadStatus.Loaded when slot.Image is { } loaded:
                    var args = new ImageClickedEventArgs(ImageClickedEvent, this, slot.Url, imageObject?.Alt, loaded);
                    RaiseEvent(args);
                    if (!args.Handled)
                    {
                        ShowViewer(slot, loaded, imageObject?.Alt);
                    }
                    break;
            }
            e.Handled = true;
            return;
        }
        if (ComponentAt(position) is { } component)
        {
            var clicked = new ComponentClickedEventArgs(ComponentClickedEvent, this, component.Name, component.Attributes);
            RaiseEvent(clicked);
            // 宿主未处理时：引用打开来源地址，<cite href> 打开链接
            if (!clicked.Handled && ComponentUrl(component.Name, component.Attributes) is { } url)
            {
                OpenUrl(url, null);
            }
            e.Handled = true;
            return;
        }
        var link = LinkAt(position);
        if (link >= 0)
        {
            var target = _content.Links[link];
            RaiseEvent(new LinkClickedEventArgs(LinkClickedEvent, this, target.Url, target.Title, LinkDecisionFor(link) == LinkDecision.Confirm));
            e.Handled = true;
        }
    }

    /// <summary>为 <see cref="TextLayout"/> 提供带样式的文本段和嵌入对象。</summary>
    private sealed class InlineTextSource : ITextSource
    {
        private readonly TextSegmentView _owner;
        private readonly RenderContext _context;
        private readonly InlineContent _content;
        private readonly double _fontSize;
        private readonly FontWeight _weight;
        private readonly IBrush _foreground;
        private readonly double _maxWidth;
        private readonly Dictionary<(InlineStyle, int, int), TextRunProperties> _properties = [];

        public InlineTextSource(TextSegmentView owner, RenderContext context, InlineContent content, double fontSize, FontWeight weight, IBrush foreground, double maxWidth)
        {
            _owner = owner;
            _context = context;
            _content = content;
            _fontSize = fontSize;
            _weight = weight;
            _foreground = foreground;
            _maxWidth = maxWidth;
            DefaultProperties = PropertiesFor(InlineStyle.None);
        }

        public TextRunProperties DefaultProperties { get; }

        public TextRun? GetTextRun(int textSourceIndex)
        {
            var text = _content.Text;
            if (textSourceIndex >= text.Length)
            {
                return null;
            }

            var begin = 0;
            var end = text.Length;
            foreach (var obj in _content.Objects)
            {
                if (obj.Offset < textSourceIndex)
                {
                    begin = obj.Offset + 1;
                }
                if (obj.Offset == textSourceIndex)
                {
                    return CreateObjectRun(obj);
                }
                if (obj.Offset > textSourceIndex)
                {
                    end = Math.Min(end, obj.Offset);
                    break;
                }
            }

            var style = InlineStyle.None;
            var component = -1;
            var textStyle = -1;
            foreach (var span in _content.Spans)
            {
                if (span.End <= textSourceIndex)
                {
                    begin = Math.Max(begin, span.End);
                    continue;
                }
                if (span.Start > textSourceIndex)
                {
                    end = Math.Min(end, span.Start);
                    break;
                }
                begin = Math.Max(begin, span.Start);
                style = span.Style;
                // 被链接策略禁止的链接显示为次要颜色的普通文字
                if (span.LinkIndex >= 0 && style.HasFlag(InlineStyle.Link) && _owner.LinkDecisionFor(span.LinkIndex) == LinkDecision.Block)
                {
                    style = (style & ~InlineStyle.Link) | InlineStyle.IncompleteLink;
                }
                component = span.ComponentIndex;
                textStyle = span.TextStyleIndex;
                end = Math.Min(end, span.End);
                break;
            }

            var properties = PropertiesFor(style, component, textStyle);
            return CreateCharacters(text, begin, textSourceIndex, end, properties, style, component, textStyle);
        }

        // 回退字体：Avalonia 在为缺字的文字找回退字体时，会优先复用"上一段"的回退字体而不检查字重 / 字形，
        // 导致"中文 **粗体**"中的粗体沿用常规字重的回退字体（反之亦然）。这里自己按本段的字重、字形解析回退字体，
        // 让每段文字的主字体就能覆盖其字符，从而不进入那条复用路径。
        // 回退字体按整段缺字文字的第一个字匹配，与从哪个位置开始取 run 无关：逐行排版会从行首重新取 run，
        // 若按行首的字匹配（如"。"与"首"在 Windows 上可能匹配到不同字体），增量结果就会与整段排版不同。
        // begin 是同一样式范围（span / 内嵌对象之间）的起点，向前找缺字段的起点不越过它。
        private TextRun CreateCharacters(string text, int begin, int start, int end, TextRunProperties properties, InlineStyle style, int component, int textStyle)
        {
            var map = GlyphMap(properties);
            var first = Rune.GetRuneAt(text, start);
            if (!Covered(map, first))
            {
                var length = 0;
                while (start + length < end)
                {
                    var rune = Rune.GetRuneAt(text, start + length);
                    if (Covered(map, rune))
                    {
                        break;
                    }
                    length += rune.Utf16SequenceLength;
                }
                var head = first;
                for (var i = start; i > begin;)
                {
                    if (Rune.DecodeLastFromUtf16(text.AsSpan(begin, i - begin), out var previous, out var consumed) != OperationStatus.Done
                        || Covered(map, previous))
                    {
                        break;
                    }
                    head = previous;
                    i -= consumed;
                }
                if (MatchFallback(head.Value, properties.Typeface) is { } fallback)
                {
                    return new TextCharacters(text.AsMemory(start, length), FallbackProperties(properties, style, component, textStyle, fallback));
                }
                return new TextCharacters(text.AsMemory(start, length), properties);
            }
            // 主字体能覆盖的前缀：截到第一个缺字处，缺字部分由下一次调用按上面的规则处理
            var covered = first.Utf16SequenceLength;
            while (start + covered < end)
            {
                var rune = Rune.GetRuneAt(text, start + covered);
                if (!Covered(map, rune))
                {
                    break;
                }
                covered += rune.Utf16SequenceLength;
            }
            return new TextCharacters(text.AsMemory(start, covered), properties);
        }

        // Avalonia 匹配回退字体时优先复用之前匹配到的字体：同一个"。"在匹配过韩文之后会落到 Apple SD Gothic Neo 而不是苹方。
        // 结果依赖进程内的匹配历史，会让先排的行与后来整段重排的行用不同字体。这里记住每个字第一次的结果，之后一律沿用。
        private static readonly ConcurrentDictionary<(int, FontStyle, FontWeight, FontStretch, string), Typeface?> MatchedFallbacks = new();

        private static Typeface? MatchFallback(int codepoint, Typeface typeface) =>
            MatchedFallbacks.GetOrAdd((codepoint, typeface.Style, typeface.Weight, typeface.Stretch, typeface.FontFamily.Name), key =>
                FontManager.Current.TryMatchCharacter(key.Item1, key.Item2, key.Item3, key.Item4, typeface.FontFamily, null, out var match) ? match : null);

        private static bool Covered(CharacterToGlyphMap? map, Rune rune) =>
            map is null || Rune.IsWhiteSpace(rune) || rune.Value < 0x20 || rune.Value == InlineObject.Placeholder || map.Value.ContainsGlyph(rune.Value);

        private readonly Dictionary<TextRunProperties, CharacterToGlyphMap?> _maps = [];
        private readonly Dictionary<(InlineStyle, int, int, string), TextRunProperties> _fallbacks = [];

        private CharacterToGlyphMap? GlyphMap(TextRunProperties properties)
        {
            if (!_maps.TryGetValue(properties, out var map))
            {
                try
                {
                    map = properties.Typeface.GlyphTypeface.CharacterToGlyphMap;
                }
                catch (Exception)
                {
                    map = null;
                }
                _maps[properties] = map;
            }
            return map;
        }

        // 回退字体只用字体族里真实存在的字面。系统字体缺少所需字重或字形时（如苹方最粗只有中粗 600、
        // Apple Color Emoji 只有常规体），Avalonia 会合成粗体 / 斜体，合成时把整份字体文件复制进内存并常驻
        // （Apple Color Emoji 约 600MB）。所以不能用 GlyphTypeface 去试探不存在的字重：
        // 粗体取不低于 500 的最粗真实字面，没有就用常规字重。
        // 回退字体一律用正体，不请求 Italic：FamilyTypefaces 在 Windows 上会列出并不存在的斜体字面
        // （按字重拆开的族如 "Yu Gothic UI Semibold"），Avalonia 只能取同族已缓存的最近字面合成斜体
        // （整份字体复制一份并常驻，还会被缓存在这个族里，之后同族的正体请求也可能取到它）。
        // 也不能先加载一下再看有没有被合成：加载本身就会创建并缓存合成字面。CJK、表情、符号字体基本没有真斜体
        private static Typeface RealFace(FontFamily family, Typeface requested)
        {
            IReadOnlyList<Typeface> faces;
            try
            {
                faces = family.FamilyTypefaces;
            }
            catch (Exception)
            {
                faces = [];
            }
            if (faces.Count == 0)
            {
                return new Typeface(family, FontStyle.Normal, requested.Weight, requested.Stretch);
            }
            var weights = faces.Where(f => f.Style == FontStyle.Normal).Select(f => (int)f.Weight).ToList();
            if (weights.Count == 0)
            {
                weights = faces.Select(f => (int)f.Weight).ToList();
            }
            int weight;
            if ((int)requested.Weight >= 600)
            {
                // 优先离请求最近的不低于 600 的字重，其次最粗的中等字重（CI 镜像里的苹方可能只到 500）
                var heavy = weights.Where(w => w >= 600).OrderBy(w => Math.Abs(w - (int)requested.Weight)).ThenByDescending(w => w).ToList();
                weight = heavy.Count > 0 ? heavy[0] : weights.Where(w => w >= 500).DefaultIfEmpty(400).Max();
            }
            else
            {
                weight = weights.OrderBy(w => Math.Abs(w - (int)requested.Weight)).First();
            }
            return new Typeface(family, FontStyle.Normal, (FontWeight)weight, requested.Stretch);
        }

        private TextRunProperties FallbackProperties(TextRunProperties properties, InlineStyle style, int component, int textStyle, Typeface fallback)
        {
            var key = (style, component, textStyle, fallback.FontFamily.Name);
            if (!_fallbacks.TryGetValue(key, out var result))
            {
                result = new GenericTextRunProperties(RealFace(fallback.FontFamily, properties.Typeface),
                    properties.FontRenderingEmSize, properties.TextDecorations, properties.ForegroundBrush, properties.BackgroundBrush, properties.BaselineAlignment);
                _fallbacks[key] = result;
            }
            return result;
        }

        private TextRun CreateObjectRun(InlineObject obj)
        {
            var style = _context.Style;
            var properties = DefaultProperties;
            switch (obj.Kind)
            {
                case InlineObjectKind.Math:
                    {
                        if (_context.Services.Registry.Math?.Layout(obj.Value, obj.Flag, out _) is { } layout)
                        {
                            return new MathRun(layout, _fontSize * 1.05, _foreground, properties);
                        }
                        // 无法排版：显示源码
                        var code = PropertiesFor(InlineStyle.Code);
                        return new TextObjectRun(obj.Flag ? $"\\[{obj.Value}\\]" : $"\\({obj.Value}\\)", code, style.InlineCodeBackground, 3);
                    }
                case InlineObjectKind.Image:
                    {
                        var declared = DeclaredSize(obj.Attributes);
                        if (Core.Remend.Remend.IncompleteImageUrl == obj.Value)
                        {
                            // 流式中地址还没写完：骨架占位
                            return new ImageRun(null, obj.Alt ?? "", declared, _maxWidth, properties, style);
                        }
                        var slot = _owner.SlotFor(_context, obj.Value, declared, _maxWidth);
                        return new ImageRun(slot, obj.Alt ?? "", declared, _maxWidth, properties, style);
                    }
                case InlineObjectKind.TaskCheckbox:
                    return new CheckboxRun(obj.Flag, _fontSize, style.SecondaryForeground, style.LinkForeground, properties);
                case InlineObjectKind.Component:
                    {
                        var renderer = _context.Plugins.FindInlineComponent(obj.Value);
                        var context = new Plugins.InlineComponentContext(style, _fontSize, _foreground, properties, _maxWidth);
                        return renderer?.CreateRun(obj, context) ?? new TextObjectRun("", properties, null);
                    }
                default:
                    return new TextCharacters(InlineObject.Placeholder.ToString(), properties);
            }
        }

        private static Size? DeclaredSize(ComponentAttributes attributes) =>
            double.TryParse(attributes["width"]?.TrimEnd('p', 'x'), System.Globalization.CultureInfo.InvariantCulture, out var w) && w > 0
                ? new Size(w, double.TryParse(attributes["height"]?.TrimEnd('p', 'x'), System.Globalization.CultureInfo.InvariantCulture, out var h) && h > 0 ? h : double.NaN)
                : null;

        private TextRunProperties PropertiesFor(InlineStyle style, int component = -1, int textStyle = -1)
        {
            if (_properties.TryGetValue((style, component, textStyle), out var cached))
            {
                return cached;
            }
            var s = _context.Style;
            // HTML 行内样式（span style、font color 等）
            var html = textStyle >= 0 && textStyle < _content.TextStyles.Length ? _content.TextStyles[textStyle] : null;
            var isCode = style.HasFlag(InlineStyle.Code) || style.HasFlag(InlineStyle.Keyboard);
            var family = isCode ? s.MonospaceFontFamily : s.FontFamily;
            var weight = style.HasFlag(InlineStyle.Bold) || html?.Bold == true ? FontWeight.Bold : html?.Bold == false ? FontWeight.Normal : _weight;
            var italic = style.HasFlag(InlineStyle.Italic) || (html?.Italic ?? _context.Inherited?.Italic) == true;
            var fontStyle = italic ? FontStyle.Italic : FontStyle.Normal;
            var size = _fontSize;
            if (html?.FontSize is { } px)
            {
                size = Math.Clamp(px, Core.Html.CssStyle.MinFontSize, Core.Html.CssStyle.MaxFontSize);
            }
            else if (html?.FontScale is { } scale)
            {
                size *= scale;
            }
            if (isCode)
            {
                size *= 0.9;
            }
            var baseline = BaselineAlignment.Baseline;
            if (style.HasFlag(InlineStyle.Superscript))
            {
                size *= 0.75;
                baseline = BaselineAlignment.Superscript;
            }
            else if (style.HasFlag(InlineStyle.Subscript))
            {
                size *= 0.75;
                baseline = BaselineAlignment.Subscript;
            }

            // 行内代码 / 按键使用独立的文字色（未配置时等于正文色）；链接样式优先
            IBrush foreground = isCode && ReferenceEquals(_foreground, s.Foreground) ? s.InlineCodeForeground : _foreground;
            TextDecorationCollection? decorations = null;
            if (style.HasFlag(InlineStyle.Link) || style.HasFlag(InlineStyle.FootnoteReference))
            {
                foreground = s.LinkForeground;
                if (!style.HasFlag(InlineStyle.FootnoteReference))
                {
                    decorations = TextDecorations.Underline;
                }
            }
            else if (style.HasFlag(InlineStyle.IncompleteLink))
            {
                foreground = s.SecondaryForeground;
            }
            // 删除线在 Render 中自行绘制：部分字体（及无头环境的回退字体）缺少删除线度量
            if (style.HasFlag(InlineStyle.Underline))
            {
                decorations = TextDecorations.Underline;
            }
            IBrush? background = isCode ? s.InlineCodeBackground : style.HasFlag(InlineStyle.Mark) ? s.MarkBackground : null;

            if (html is not null)
            {
                var htmlBackground = HtmlPaint.Background(_context, html.Background);
                foreground = HtmlPaint.Foreground(_context, html.Foreground, html.Background) ?? foreground;
                background = htmlBackground ?? background;
            }

            // 成对行内组件（如 <cite>）的样式覆盖
            if (component >= 0 && component < _content.Components.Length &&
                _context.Plugins.FindInlineComponent(_content.Components[component].Name)?.GetStyle(_content.Components[component], s) is { } custom)
            {
                foreground = custom.Foreground ?? foreground;
                background = custom.Background ?? background;
                weight = custom.Weight ?? weight;
                if (custom.Underline)
                {
                    decorations = TextDecorations.Underline;
                }
            }

            var properties = new GenericTextRunProperties(
                new Typeface(family, fontStyle, weight),
                size,
                decorations,
                foreground,
                background,
                baseline);
            _properties[(style, component, textStyle)] = properties;
            return properties;
        }
    }
}

public sealed class LinkClickedEventArgs(RoutedEvent routedEvent, object source, string url, string? title, bool requiresConfirmation = false) : RoutedEventArgs(routedEvent, source)
{
    public string Url { get; } = url;

    public string? Title { get; } = title;

    /// <summary>链接策略要求宿主先请用户确认（<see cref="LinkPolicy.RequireConfirmation"/> 或链接文字与地址不符）。</summary>
    public bool RequiresConfirmation { get; } = requiresConfirmation;
}

/// <summary>指针进入 / 离开链接。离开时 <see cref="Url"/> 为 null。宿主可在状态栏显示真实地址。</summary>
public sealed class LinkHoveredEventArgs(RoutedEvent routedEvent, object source, string? url) : RoutedEventArgs(routedEvent, source)
{
    public string? Url { get; } = url;
}

public sealed class ComponentClickedEventArgs(RoutedEvent routedEvent, object source, string name, ComponentAttributes attributes) : RoutedEventArgs(routedEvent, source)
{
    public string Name { get; } = name;

    public ComponentAttributes Attributes { get; } = attributes;
}

public sealed class ImageClickedEventArgs(RoutedEvent routedEvent, object source, string url, string? alt, IImage image) : RoutedEventArgs(routedEvent, source)
{
    public string Url { get; } = url;

    public string? Alt { get; } = alt;

    public IImage Image { get; } = image;
}
