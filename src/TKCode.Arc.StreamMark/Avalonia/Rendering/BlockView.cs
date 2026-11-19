using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Threading;
using StreamMark.Avalonia.Selection;
using StreamMark.Core.Model;

namespace StreamMark.Avalonia.Rendering;

/// <summary>
/// 块视图基类。视图按块模型更新自身；同类型的新模型复用同一个视图（流式中尾部块每帧更新）。
/// 文本片段编号在构建时按阅读顺序分配，必须与 <see cref="BlockText"/> 的遍历顺序一致。
/// </summary>
public abstract class BlockView : Control
{
    public MarkdownBlock? Block { get; private set; }

    protected RenderContext? Context { get; private set; }

    /// <summary>当前使用的渲染上下文。</summary>
    public RenderContext? CurrentContext => Context;

    /// <summary>所属顶层块的 key。</summary>
    public int TopKey { get; private set; }

    internal void Update(RenderContext context, MarkdownBlock block, int topKey, ref int segment)
    {
        var contextChanged = !ReferenceEquals(Context, context);
        Context = context;
        TopKey = topKey;
        var previous = Block;
        Block = block;
        OnUpdate(context, previous, block, topKey, ref segment, contextChanged);
    }

    protected abstract void OnUpdate(RenderContext context, MarkdownBlock? previous, MarkdownBlock block, int topKey, ref int segment, bool contextChanged);

    protected override global::Avalonia.Automation.Peers.AutomationPeer OnCreateAutomationPeer() => new Automation.BlockAutomationPeer(this);

    /// <summary>深度优先的最后一个文本片段（用于流式渐显和光标）。</summary>
    public virtual TextSegmentView? LastSegment() => null;

    /// <summary>按阅读顺序列出所有文本片段视图（用于选区命中）。</summary>
    public virtual IEnumerable<ISelectableSegment> Segments() => [];

    protected static SegmentKey KeyFor(RenderContext context, int topKey, ref int segment) => new(context.Entry, topKey, segment++);

    protected void AddChild(Control child)
    {
        LogicalChildren.Add(child);
        VisualChildren.Add(child);
    }

    protected void RemoveChild(Control child)
    {
        LogicalChildren.Remove(child);
        VisualChildren.Remove(child);
    }

    private BackgroundJob? _job;

    /// <summary>
    /// 在后台执行慢工作（渲染、解析数据等），结果回到界面线程交给 <paramref name="apply"/>。
    /// <list type="bullet">
    /// <item>同一 <paramref name="key"/> 正在执行或已经应用过：什么都不做（流式中内容没变的块每帧调用也没有开销）。</item>
    /// <item>新的 key：取消上一个任务，过期的结果不会再应用。</item>
    /// <item>视图离开可视树时取消未完成的任务，重新挂上时自动按最后一次请求重跑。</item>
    /// <item>工作抛出的异常（取消除外）交给 <paramref name="failed"/>；不传时丢弃。</item>
    /// </list>
    /// key 通常取 <see cref="MarkdownBlock.ComputeFingerprint"/>（加上影响结果的环境）。指纹每次调用都会重新计算，
    /// 只在 <c>OnUpdate</c> 的 previous 与 block 不是同一实例时再算，流式中没变的块就不付这个成本。
    /// 只能在界面线程调用。
    /// </summary>
    protected void RunInBackground<T>(ulong key, Func<CancellationToken, Task<T>> work, Action<T> apply, Action<Exception>? failed = null)
    {
        if (_job is { Abandoned: false } current && current.Key == key)
        {
            return;
        }
        _job?.Cancellation.Cancel();
        var job = new BackgroundJob(key, () => RunInBackground(key, work, apply, failed));
        _job = job;
        var token = job.Cancellation.Token;
        _ = Task.Run(async () =>
        {
            T result;
            try
            {
                result = await work(token).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (token.IsCancellationRequested)
            {
                return;
            }
            catch (Exception e)
            {
                Dispatcher.UIThread.Post(() =>
                {
                    if (ReferenceEquals(_job, job) && !token.IsCancellationRequested)
                    {
                        job.Completed = true;
                        failed?.Invoke(e);
                    }
                });
                return;
            }
            Dispatcher.UIThread.Post(() =>
            {
                if (ReferenceEquals(_job, job) && !token.IsCancellationRequested)
                {
                    job.Completed = true;
                    apply(result);
                }
            });
        }, CancellationToken.None);
    }

    /// <summary>取消 <see cref="RunInBackground"/> 启动的任务；之后用任何 key 调用都会重新执行。</summary>
    protected void CancelBackgroundWork()
    {
        _job?.Cancellation.Cancel();
        _job = null;
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        if (_job is { Completed: false, Abandoned: false } job)
        {
            job.Cancellation.Cancel();
            job.Abandoned = true;
        }
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        if (_job is { Abandoned: true } job)
        {
            _job = null;
            job.Restart();
        }
    }

    private sealed class BackgroundJob(ulong key, Action restart)
    {
        public ulong Key { get; } = key;

        public Action Restart { get; } = restart;

        public CancellationTokenSource Cancellation { get; } = new();

        public bool Completed { get; set; }

        /// <summary>离开可视树时被取消，等重新挂上时重跑。</summary>
        public bool Abandoned { get; set; }
    }
}

/// <summary>
/// 纵向排列子块的容器视图（引用、提示块、列表项、脚注等）。子视图按位置复用：类型相同则更新，否则替换。
/// </summary>
public abstract class StackBlockView : BlockView
{
    private readonly List<BlockView> _children = [];

    protected IReadOnlyList<BlockView> ChildViews => _children;

    /// <summary>内容区相对于自身的内边距（左侧放引用竖线、列表标记等）。</summary>
    protected virtual Thickness ContentPadding => default;

    /// <summary>内容区上方的额外高度（例如提示块标题）。</summary>
    protected virtual double HeaderHeight => 0;

    protected virtual double ChildSpacing => Context?.Style.BlockSpacing ?? 12;

    protected void UpdateChildren(RenderContext context, IReadOnlyList<MarkdownBlock> blocks, int topKey, ref int segment)
    {
        for (var i = 0; i < blocks.Count; i++)
        {
            var block = blocks[i];
            if (i < _children.Count && BlockViewFactory.CanReuse(_children[i], block, context))
            {
                _children[i].Update(context, block, topKey, ref segment);
                continue;
            }
            var view = BlockViewFactory.Create(block, context);
            view.Update(context, block, topKey, ref segment);
            if (i < _children.Count)
            {
                RemoveChild(_children[i]);
                _children[i] = view;
            }
            else
            {
                _children.Add(view);
            }
            AddChild(view);
        }
        while (_children.Count > blocks.Count)
        {
            RemoveChild(_children[^1]);
            _children.RemoveAt(_children.Count - 1);
        }
        InvalidateMeasure();
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        var padding = ContentPadding;
        var inner = availableSize.Deflate(padding);
        double height = 0, width = 0;
        for (var i = 0; i < _children.Count; i++)
        {
            var child = _children[i];
            child.Measure(new Size(inner.Width, double.PositiveInfinity));
            if (i > 0)
            {
                height += ChildSpacing;
            }
            height += child.DesiredSize.Height;
            width = Math.Max(width, child.DesiredSize.Width);
        }
        return new Size(width + padding.Left + padding.Right, height + padding.Top + padding.Bottom + HeaderHeight);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        var padding = ContentPadding;
        var y = padding.Top + HeaderHeight;
        var width = Math.Max(0, finalSize.Width - padding.Left - padding.Right);
        for (var i = 0; i < _children.Count; i++)
        {
            var child = _children[i];
            if (i > 0)
            {
                y += ChildSpacing;
            }
            child.Arrange(new Rect(padding.Left, y, width, child.DesiredSize.Height));
            y += child.DesiredSize.Height;
        }
        return finalSize;
    }

    public override TextSegmentView? LastSegment()
    {
        for (var i = _children.Count - 1; i >= 0; i--)
        {
            if (_children[i].LastSegment() is { } segment)
            {
                return segment;
            }
        }
        return null;
    }

    public override IEnumerable<ISelectableSegment> Segments() => _children.SelectMany(c => c.Segments());
}

/// <summary>段落、标题、HTML 块：一个文本片段。</summary>
public sealed class TextBlockView : BlockView
{
    private readonly TextSegmentView _text = new();

    public TextBlockView()
    {
        AddChild(_text);
    }

    protected override void OnUpdate(RenderContext context, MarkdownBlock? previous, MarkdownBlock block, int topKey, ref int segment, bool contextChanged)
    {
        var style = context.Style;
        var key = KeyFor(context, topKey, ref segment);
        switch (block)
        {
            case HeadingBlock heading:
                _text.Set(context, key, heading.Content, style.HeadingFontSize(heading.Level), heading.Level <= 4 ? FontWeight.SemiBold : FontWeight.Bold);
                _level = heading.Level;
                // 读屏按标题级别导航
                global::Avalonia.Automation.AutomationProperties.SetHeadingLevel(_text, heading.Level);
                break;
            case ParagraphBlock paragraph:
                _text.Set(context, key, paragraph.Content, style.FontSize);
                _level = 0;
                global::Avalonia.Automation.AutomationProperties.SetHeadingLevel(_text, 0);
                break;
            case HtmlBlock html:
                _text.Set(context, key, new InlineContent(html.Html, [new InlineSpan(0, html.Html.Length, InlineStyle.Code)], [], []), style.FontSize, foreground: style.SecondaryForeground);
                _level = 0;
                break;
        }
        InvalidateMeasure();
        InvalidateVisual();
    }

    private int _level;

    // H1 / H2 下方有分隔线
    private double RuleSpace => _level is 1 or 2 ? 9 : 0;

    protected override Size MeasureOverride(Size availableSize)
    {
        _text.Measure(new Size(availableSize.Width, double.PositiveInfinity));
        var top = _level > 0 ? Math.Round((Context?.Style.FontSize ?? 14) * 0.4) : 0;
        return new Size(_text.DesiredSize.Width, _text.DesiredSize.Height + top + RuleSpace);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        var top = _level > 0 ? Math.Round((Context?.Style.FontSize ?? 14) * 0.4) : 0;
        _text.Arrange(new Rect(0, top, finalSize.Width, _text.DesiredSize.Height));
        return finalSize;
    }

    public override void Render(DrawingContext context)
    {
        if (RuleSpace > 0 && Context is not null)
        {
            var y = Bounds.Height - 1.5;
            context.DrawLine(new Pen(Context.Style.TableBorderBrush, 1), new Point(0, y), new Point(Bounds.Width, y));
        }
    }

    public override TextSegmentView? LastSegment() => _text;

    public override IEnumerable<ISelectableSegment> Segments() => [_text];
}

/// <summary>分隔线。</summary>
public sealed class ThematicBreakBlockView : BlockView
{
    protected override void OnUpdate(RenderContext context, MarkdownBlock? previous, MarkdownBlock block, int topKey, ref int segment, bool contextChanged) =>
        InvalidateVisual();

    protected override Size MeasureOverride(Size availableSize) => new(0, 17);

    public override void Render(DrawingContext context)
    {
        if (Context is null)
        {
            return;
        }
        var y = Math.Round(Bounds.Height / 2) + 0.5;
        context.DrawLine(new Pen(Context.Style.TableBorderBrush, 1), new Point(0, y), new Point(Bounds.Width, y));
    }
}
