using System.Collections.Immutable;
using System.Globalization;
using Avalonia;
using Avalonia.Automation.Peers;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Media.TextFormatting;
using StreamMark.Avalonia.Selection;
using StreamMark.Core.Model;

namespace StreamMark.Avalonia.Rendering;

/// <summary>引用：左侧竖线。</summary>
public sealed class QuoteBlockView : StackBlockView
{
    protected override Thickness ContentPadding => new(14, 0, 0, 0);

    protected override void OnUpdate(RenderContext context, MarkdownBlock? previous, MarkdownBlock block, int topKey, ref int segment, bool contextChanged) =>
        UpdateChildren(context, ((QuoteBlock)block).Children, topKey, ref segment);

    public override void Render(DrawingContext context)
    {
        if (Context is not null)
        {
            context.FillRectangle(Context.Style.QuoteBorderBrush, new Rect(0, 0, 3, Bounds.Height), 1.5f);
        }
    }
}

/// <summary>GitHub 提示块：彩色竖线 + 标题。</summary>
public sealed class AlertBlockView : StackBlockView
{
    private TextLayout? _title;

    protected override Thickness ContentPadding => new(14, 2, 0, 2);

    protected override double HeaderHeight => _title is null ? 0 : _title.Height + 6;

    protected override void OnUpdate(RenderContext context, MarkdownBlock? previous, MarkdownBlock block, int topKey, ref int segment, bool contextChanged)
    {
        var alert = (AlertBlock)block;
        if (contextChanged || previous is not AlertBlock old || old.Kind != alert.Kind)
        {
            var title = alert.Kind switch
            {
                "NOTE" => "ⓘ  Note",
                "TIP" => "💡 Tip",
                "IMPORTANT" => "❗ Important",
                "WARNING" => "⚠  Warning",
                "CAUTION" => "⛔ Caution",
                _ => CultureInfo.InvariantCulture.TextInfo.ToTitleCase(alert.Kind.ToLowerInvariant()),
            };
            _title = new TextLayout(title, new Typeface(context.Style.FontFamily, FontStyle.Normal, FontWeight.SemiBold), context.Style.FontSize,
                context.Style.AlertBrush(alert.Kind));
            InvalidateMeasure();
            InvalidateVisual();
        }
        UpdateChildren(context, alert.Children, topKey, ref segment);
    }

    public override void Render(DrawingContext context)
    {
        if (Context is null || Block is not AlertBlock alert)
        {
            return;
        }
        context.FillRectangle(Context.Style.AlertBrush(alert.Kind), new Rect(0, 0, 3, Bounds.Height), 1.5f);
        _title?.Draw(context, new Point(ContentPadding.Left, ContentPadding.Top));
    }
}

/// <summary><c>:::</c> 自定义容器：带标题的边框盒。</summary>
public sealed class ContainerBlockView : StackBlockView
{
    private TextLayout? _title;
    private IPen? _borderPen;
    private IBrush? _borderPenBrush;

    protected override Thickness ContentPadding => new(12, 10, 12, 10);

    protected override double HeaderHeight => _title is null ? 0 : _title.Height + 6;

    protected override void OnUpdate(RenderContext context, MarkdownBlock? previous, MarkdownBlock block, int topKey, ref int segment, bool contextChanged)
    {
        var container = (CustomContainerBlock)block;
        if (contextChanged || previous is not CustomContainerBlock old || old.Info != container.Info || old.Arguments != container.Arguments)
        {
            var hasInfo = !string.IsNullOrWhiteSpace(container.Info);
            var hasArgs = !string.IsNullOrWhiteSpace(container.Arguments);
            var title = hasInfo && hasArgs ? $"{container.Info} {container.Arguments}"
                : hasInfo ? container.Info!
                : hasArgs ? container.Arguments!
                : string.Empty;
            _title = title.Length == 0
                ? null
                : new TextLayout(title, new Typeface(context.Style.FontFamily, FontStyle.Normal, FontWeight.SemiBold), context.Style.FontSize, context.Style.SecondaryForeground);
            InvalidateMeasure();
            InvalidateVisual();
        }
        UpdateChildren(context, container.Children, topKey, ref segment);
    }

    public override void Render(DrawingContext context)
    {
        if (Context is null)
        {
            return;
        }
        var brush = Context.Style.TableBorderBrush;
        if (_borderPen is null || !ReferenceEquals(_borderPenBrush, brush))
        {
            _borderPenBrush = brush;
            _borderPen = new Pen(brush, 1);
        }
        var r = Context.Style.CornerRadius.TopLeft;
        context.DrawRectangle(Context.Style.InlineCodeBackground, _borderPen, new Rect(Bounds.Size).Deflate(0.5), r, r);
        _title?.Draw(context, new Point(ContentPadding.Left, ContentPadding.Top));
    }
}

/// <summary>有序 / 无序 / 任务列表。</summary>
public sealed class ListBlockView : BlockView
{
    private readonly List<ListItemBlockView> _items = [];
    private ListBlock? _list;

    protected override void OnUpdate(RenderContext context, MarkdownBlock? previous, MarkdownBlock block, int topKey, ref int segment, bool contextChanged)
    {
        var oldList = _list;
        _list = (ListBlock)block;
        var items = _list.Items;
        var existingCount = _items.Count;
        var structureChanged = contextChanged || oldList is null || oldList.IsLoose != _list.IsLoose || existingCount != items.Length;
        var sameOrder = oldList is not null && oldList.IsOrdered == _list.IsOrdered && (!_list.IsOrdered || oldList.StartNumber == _list.StartNumber);
        var sameLoose = oldList is not null && oldList.IsLoose == _list.IsLoose;
        var unorderedMarker = _list.IsOrdered ? null : UnorderedMarker();
        for (var i = 0; i < items.Length; i++)
        {
            if (i >= _items.Count)
            {
                var view = new ListItemBlockView();
                _items.Add(view);
                AddChild(view);
                structureChanged = true;
            }
            var itemView = _items[i];
            // 已封存列表项在模型引用、序号与起始片段号均未变时直接 O(1) 跳过子树更新
            if (!contextChanged && sameOrder && sameLoose && i < existingCount
                && ReferenceEquals(itemView.CachedItem, items[i])
                && itemView.CachedFirstSegment == segment)
            {
                segment += itemView.CachedSegmentCount;
                continue;
            }
            if (!_list.IsOrdered)
            {
                itemView.Marker = unorderedMarker!;
            }
            else if (i >= existingCount || !sameOrder)
            {
                itemView.Marker = (_list.StartNumber + i).ToString(CultureInfo.InvariantCulture) + ".";
            }
            itemView.Task = items[i].Task;
            itemView.Tight = !_list.IsLoose;
            var startSegment = segment;
            itemView.Update(context, items[i], topKey, ref segment);
            itemView.CachedItem = items[i];
            itemView.CachedFirstSegment = startSegment;
            itemView.CachedSegmentCount = segment - startSegment;
        }
        while (_items.Count > items.Length)
        {
            RemoveChild(_items[^1]);
            _items.RemoveAt(_items.Count - 1);
            structureChanged = true;
        }
        if (structureChanged)
        {
            InvalidateMeasure();
        }
    }

    private string UnorderedMarker()
    {
        // 嵌套层级越深，项目符号越小
        var depth = 0;
        for (var parent = Parent; parent is not null; parent = parent.Parent)
        {
            if (parent is ListBlockView)
            {
                depth++;
            }
        }
        return (depth % 3) switch
        {
            0 => "•",
            1 => "◦",
            _ => "▪",
        };
    }

    // 嵌套层级在挂到可视树后才确定，届时刷新项目符号
    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        if (_list is { IsOrdered: false })
        {
            var marker = UnorderedMarker();
            for (var i = 0; i < _items.Count; i++)
            {
                _items[i].Marker = marker;
            }
        }
    }

    private double ItemSpacing => _list is { IsLoose: true } && Context is not null ? Context.Style.BlockSpacing : 4;

    protected override Size MeasureOverride(Size availableSize)
    {
        double height = 0, width = 0;
        for (var i = 0; i < _items.Count; i++)
        {
            _items[i].Measure(new Size(availableSize.Width, double.PositiveInfinity));
            height += _items[i].DesiredSize.Height + (i > 0 ? ItemSpacing : 0);
            width = Math.Max(width, _items[i].DesiredSize.Width);
        }
        return new Size(width, height);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        double y = 0;
        for (var i = 0; i < _items.Count; i++)
        {
            if (i > 0)
            {
                y += ItemSpacing;
            }
            _items[i].Arrange(new Rect(0, y, finalSize.Width, _items[i].DesiredSize.Height));
            y += _items[i].DesiredSize.Height;
        }
        return finalSize;
    }

    public override TextSegmentView? LastSegment() => _items.Count > 0 ? _items[^1].LastSegment() : null;

    public override IEnumerable<ISelectableSegment> Segments() => _items.SelectMany(i => i.Segments());
}

/// <summary>
/// 列表项：左侧绘制项目符号 / 序号 / 复选框，右侧是子块。
/// 上下文带有 <see cref="RenderContext.TaskToggle"/> 时任务项的复选框可交互（点击、聚焦后按空格或回车），
/// 只是向宿主发出"请求"，视图自己的勾选状态仍然只由快照决定。
/// </summary>
public sealed class ListItemBlockView : StackBlockView
{
    private static readonly Cursor HandCursor = new(StandardCursorType.Hand);

    private TextLayout? _marker;
    private string _markerText = "";
    private bool? _task;
    private bool _tight;
    private Rect _taskBox;
    private bool _pressedOnBox;
    private bool _hasBaseline;
    private IPen? _focusPen;
    private IBrush? _focusPenBrush;
    private CheckboxRun? _checkboxRun;
    private bool _checkboxChecked;
    private double _checkboxFontSize;
    private IBrush? _checkboxSecondary;
    private IBrush? _checkboxLink;
    private FontFamily? _checkboxFamily;

    internal ListItemBlock? CachedItem;
    internal int CachedFirstSegment;
    internal int CachedSegmentCount;

    public ListItemBlockView()
    {
        // 聚焦环随焦点出现 / 消失
        GotFocus += (_, _) => InvalidateVisual();
        LostFocus += (_, _) => InvalidateVisual();
    }

    public string Marker
    {
        get => _markerText;
        set
        {
            if (_markerText != value)
            {
                var lengthCrossed = (_markerText.Length > 2) != (value.Length > 2);
                _markerText = value;
                _marker = null;
                if (lengthCrossed)
                {
                    InvalidateMeasure();
                }
                InvalidateVisual();
            }
        }
    }

    public bool? Task
    {
        get => _task;
        set
        {
            if (_task != value)
            {
                _task = value;
                InvalidateVisual();
            }
        }
    }

    public bool Tight
    {
        get => _tight;
        set
        {
            if (_tight != value)
            {
                _tight = value;
                InvalidateMeasure();
            }
        }
    }

    /// <summary>复选框是否可交互：任务项，且宿主启用了任务交互。</summary>
    internal bool IsTaskInteractive => _task is not null && Context?.TaskToggle is not null;

    private double Indent => Math.Round((Context?.Style.FontSize ?? 14) * (_markerText.Length > 2 ? 2.0 : 1.6));

    protected override Thickness ContentPadding => new(Indent, 0, 0, 0);

    protected override double ChildSpacing => _tight ? 4 : base.ChildSpacing;

    protected override void OnUpdate(RenderContext context, MarkdownBlock? previous, MarkdownBlock block, int topKey, ref int segment, bool contextChanged)
    {
        if (contextChanged)
        {
            _marker = null;
        }
        // 只有可交互的任务项才是 Tab 停靠点
        var wasFocusable = Focusable;
        Focusable = IsTaskInteractive;
        if (!IsTaskInteractive)
        {
            Cursor = null;
            _pressedOnBox = false;
        }
        var oldFirst = ChildViews.Count > 0 ? ChildViews[0] : null;
        UpdateChildren(context, ((ListItemBlock)block).Children, topKey, ref segment);
        var newFirst = ChildViews.Count > 0 ? ChildViews[0] : null;
        if (contextChanged || previous is null || wasFocusable != Focusable || !_hasBaseline || !ReferenceEquals(oldFirst, newFirst))
        {
            InvalidateVisual();
        }
    }

    /// <summary>向宿主请求切换勾选状态；没有启用交互、不是任务项或宿主没有接收时返回 false。</summary>
    internal bool RequestToggle() => IsTaskInteractive && Block is ListItemBlock item && Context!.TaskToggle!(item);

    // 命中区域比画出来的复选框大一圈，方便点击
    private bool HitsBox(Point point) => _taskBox.Width > 0 && _taskBox.Inflate(3).Contains(point);

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        if (IsTaskInteractive && e.GetCurrentPoint(this).Properties.IsLeftButtonPressed && HitsBox(e.GetPosition(this)))
        {
            _pressedOnBox = true;
            e.Pointer.Capture(this);
            e.Handled = true;
        }
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        Cursor = IsTaskInteractive && HitsBox(e.GetPosition(this)) ? HandCursor : null;
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        if (!_pressedOnBox)
        {
            return;
        }
        _pressedOnBox = false;
        e.Pointer.Capture(null);
        e.Handled = true;
        if (HitsBox(e.GetPosition(this)))
        {
            RequestToggle();
        }
    }

    protected override void OnPointerCaptureLost(PointerCaptureLostEventArgs e)
    {
        base.OnPointerCaptureLost(e);
        _pressedOnBox = false;
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (!e.Handled && e.KeyModifiers == KeyModifiers.None && e.Key is Key.Space or Key.Enter && IsTaskInteractive)
        {
            e.Handled = true;
            RequestToggle();
        }
    }

    protected override AutomationPeer OnCreateAutomationPeer() => new Automation.TaskItemAutomationPeer(this);

    private static TextSegmentView? FirstTextSegment(BlockView view)
    {
        foreach (var seg in view.Segments())
        {
            return seg as TextSegmentView;
        }
        return null;
    }

    public override void Render(DrawingContext context)
    {
        if (Context is null)
        {
            return;
        }
        var style = Context.Style;
        // 与第一行文字的基线对齐
        var first = ChildViews.Count > 0 ? FirstTextSegment(ChildViews[0]) : null;
        var baseline = style.FontSize;
        if (first?.Layout is { TextLines.Count: > 0 } layout)
        {
            var origin = first.TranslatePoint(default, this) ?? default;
            baseline = origin.Y + layout.TextLines[0].Baseline;
            _hasBaseline = true;
        }

        if (_task is { } isChecked)
        {
            var box = Math.Round(style.FontSize * 0.95);
            if (_checkboxRun is null
                || _checkboxChecked != isChecked
                || !_checkboxFontSize.Equals(style.FontSize)
                || !ReferenceEquals(_checkboxSecondary, style.SecondaryForeground)
                || !ReferenceEquals(_checkboxLink, style.LinkForeground)
                || !ReferenceEquals(_checkboxFamily, style.FontFamily))
            {
                _checkboxChecked = isChecked;
                _checkboxFontSize = style.FontSize;
                _checkboxSecondary = style.SecondaryForeground;
                _checkboxLink = style.LinkForeground;
                _checkboxFamily = style.FontFamily;
                _checkboxRun = new CheckboxRun(isChecked, style.FontSize, style.SecondaryForeground, style.LinkForeground,
                    new GenericTextRunProperties(new Typeface(style.FontFamily), style.FontSize));
            }
            var run = _checkboxRun;
            var boxOrigin = new Point(Indent - box - style.FontSize * 0.45, baseline - run.Baseline);
            run.Draw(context, boxOrigin);
            _taskBox = new Rect(boxOrigin, new Size(box, box));
            if (IsTaskInteractive)
            {
                // 透明填充让复选框一带能接收指针（没有绘制内容的区域不参与命中）
                context.FillRectangle(Brushes.Transparent, _taskBox.Inflate(3));
                if (IsFocused)
                {
                    if (_focusPen is null || !ReferenceEquals(_focusPenBrush, style.LinkForeground))
                    {
                        _focusPenBrush = style.LinkForeground;
                        _focusPen = new Pen(style.LinkForeground, 1.5);
                    }
                    context.DrawRectangle(_focusPen, _taskBox.Inflate(2), 3);
                }
            }
            return;
        }

        _taskBox = default;
        _marker ??= new TextLayout(_markerText, new Typeface(style.FontFamily), _markerText.Length == 1 ? style.FontSize * 1.1 : style.FontSize,
            style.ListMarkerForeground, TextAlignment.Right);
        var markerBaseline = _marker.TextLines.Count > 0 ? _marker.TextLines[0].Baseline : _marker.Height;
        var x = Indent - _marker.WidthIncludingTrailingWhitespace - style.FontSize * 0.45;
        _marker.Draw(context, new Point(x, baseline - markerBaseline));
    }
}

/// <summary>文末脚注：分隔线 + 编号列表。</summary>
public sealed class FootnotesBlockView : BlockView
{
    private readonly ListBlockView _list = new();
    private IPen? _rulePen;
    private IBrush? _rulePenBrush;

    public FootnotesBlockView()
    {
        AddChild(_list);
    }

    protected override void OnUpdate(RenderContext context, MarkdownBlock? previous, MarkdownBlock block, int topKey, ref int segment, bool contextChanged)
    {
        var footnotes = (FootnotesBlock)block;
        var items = footnotes.Footnotes.Select(f => new ListItemBlock(null, f.Children) { Range = block.Range }).ToImmutableArray();
        var list = new ListBlock(true, 1, '.', false, items) { Range = block.Range };
        _list.Update(context, list, topKey, ref segment);
        if (contextChanged || previous is null)
        {
            InvalidateMeasure();
        }
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        _list.Measure(new Size(availableSize.Width, double.PositiveInfinity));
        return new Size(_list.DesiredSize.Width, _list.DesiredSize.Height + 14);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        _list.Arrange(new Rect(0, 14, finalSize.Width, _list.DesiredSize.Height));
        return finalSize;
    }

    public override void Render(DrawingContext context)
    {
        if (Context is not null)
        {
            var brush = Context.Style.TableBorderBrush;
            if (_rulePen is null || !ReferenceEquals(_rulePenBrush, brush))
            {
                _rulePenBrush = brush;
                _rulePen = new Pen(brush, 1);
            }
            context.DrawLine(_rulePen, new Point(0, 4.5), new Point(Math.Min(Bounds.Width, 160), 4.5));
        }
    }

    public override TextSegmentView? LastSegment() => _list.LastSegment();

    public override IEnumerable<ISelectableSegment> Segments() => _list.Segments();
}

/// <summary>定义列表：术语加粗，释义缩进。</summary>
public sealed class DefinitionListBlockView : BlockView
{
    private readonly List<(List<TextSegmentView> Terms, DefinitionBodyView Body)> _items = [];

    protected override void OnUpdate(RenderContext context, MarkdownBlock? previous, MarkdownBlock block, int topKey, ref int segment, bool contextChanged)
    {
        var list = (DefinitionListBlock)block;
        var changed = contextChanged || previous is null || _items.Count != list.Items.Length;
        for (var i = 0; i < list.Items.Length; i++)
        {
            if (i >= _items.Count)
            {
                var body = new DefinitionBodyView();
                _items.Add(([], body));
                AddChild(body);
                changed = true;
            }
            var (terms, bodyView) = _items[i];
            var item = list.Items[i];
            while (terms.Count < item.Terms.Length)
            {
                var term = new TextSegmentView();
                terms.Add(term);
                AddChild(term);
                changed = true;
            }
            while (terms.Count > item.Terms.Length)
            {
                RemoveChild(terms[^1]);
                terms.RemoveAt(terms.Count - 1);
                changed = true;
            }
            for (var t = 0; t < item.Terms.Length; t++)
            {
                if (terms[t].SetTracked(context, KeyFor(context, topKey, ref segment), item.Terms[t], context.Style.FontSize, FontWeight.SemiBold))
                {
                    changed = true;
                }
            }
            bodyView.Update(context, new QuoteBlock(item.Definitions) { Range = block.Range }, topKey, ref segment);
        }
        while (_items.Count > list.Items.Length)
        {
            var (terms, body) = _items[^1];
            terms.ForEach(RemoveChild);
            RemoveChild(body);
            _items.RemoveAt(_items.Count - 1);
            changed = true;
        }
        if (changed)
        {
            InvalidateMeasure();
        }
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        double height = 0, width = 0;
        var spacing = Context?.Style.BlockSpacing ?? 12;
        foreach (var (terms, body) in _items)
        {
            foreach (var term in terms)
            {
                term.Measure(new Size(availableSize.Width, double.PositiveInfinity));
                height += term.DesiredSize.Height;
                width = Math.Max(width, term.DesiredSize.Width);
            }
            body.Measure(new Size(availableSize.Width, double.PositiveInfinity));
            height += body.DesiredSize.Height + 4 + spacing;
            width = Math.Max(width, body.DesiredSize.Width);
        }
        return new Size(width, Math.Max(0, height - spacing));
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        double y = 0;
        var spacing = Context?.Style.BlockSpacing ?? 12;
        foreach (var (terms, body) in _items)
        {
            foreach (var term in terms)
            {
                term.Arrange(new Rect(0, y, finalSize.Width, term.DesiredSize.Height));
                y += term.DesiredSize.Height;
            }
            y += 4;
            body.Arrange(new Rect(0, y, finalSize.Width, body.DesiredSize.Height));
            y += body.DesiredSize.Height + spacing;
        }
        return finalSize;
    }

    public override TextSegmentView? LastSegment() => _items.Count > 0 ? _items[^1].Body.LastSegment() ?? _items[^1].Terms.LastOrDefault() : null;

    public override IEnumerable<ISelectableSegment> Segments()
    {
        foreach (var (terms, body) in _items)
        {
            foreach (var term in terms)
            {
                yield return term;
            }
            foreach (var s in body.Segments())
            {
                yield return s;
            }
        }
    }

    public sealed class DefinitionBodyView : StackBlockView
    {
        protected override Thickness ContentPadding => new(24, 0, 0, 0);

        protected override void OnUpdate(RenderContext context, MarkdownBlock? previous, MarkdownBlock block, int topKey, ref int segment, bool contextChanged) =>
            UpdateChildren(context, ((QuoteBlock)block).Children, topKey, ref segment);
    }
}
