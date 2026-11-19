using StreamMark.Core.Model;
using StreamMark.Core.Streaming;

namespace StreamMark.Avalonia.Rendering;

/// <summary>
/// 把一个文档的快照序列应用到一组顶层块视图上：按 <see cref="SnapshotDiff"/> 插入、更新、删除，
/// 并维护"文档末尾片段"标记（流式渐显与光标）。
/// </summary>
internal sealed class DocumentPresenter
{
    private readonly List<BlockView> _views = [];
    private readonly Action<int, BlockView> _insert;
    private readonly Action<int, BlockView> _remove;
    private TextSegmentView? _tail;

    public DocumentPresenter(Action<int, BlockView> insert, Action<int, BlockView> remove)
    {
        _insert = insert;
        _remove = remove;
    }

    public IReadOnlyList<BlockView> Views => _views;

    public DocumentSnapshot? Applied { get; private set; }

    public RenderContext? Context { get; private set; }

    /// <summary>应用新快照；返回是否有任何变化。</summary>
    public bool Apply(DocumentSnapshot snapshot, RenderContext context) =>
        Apply(SnapshotDiff.Compute(Applied, snapshot), snapshot, context);

    /// <summary>
    /// 应用编辑前后两份快照的对齐结果（宿主整篇替换文本）：被平移的块原地更新到新的源码位置，而不是删掉重建，
    /// 所以视图、排版缓存与界面状态都保留。<paramref name="edit"/> 必须是以当前已应用的快照为起点算出的。
    /// </summary>
    public bool ApplyEdit(SnapshotEdit edit, RenderContext context) => Apply(edit.Ops, edit.Next, context);

    private bool Apply(IReadOnlyList<DiffOp> ops, DocumentSnapshot snapshot, RenderContext context)
    {
        var contextChanged = !ReferenceEquals(Context, context);
        Context = context;
        foreach (var op in ops)
        {
            switch (op.Kind)
            {
                case DiffKind.Insert:
                    {
                        var view = BlockViewFactory.Create(op.Block!, context);
                        var segment = 0;
                        view.Update(context, op.Block!, op.Block!.Key, ref segment);
                        _views.Insert(op.Index, view);
                        _insert(op.Index, view);
                        break;
                    }
                case DiffKind.Update:
                    {
                        var view = _views[op.Index];
                        if (BlockViewFactory.CanReuse(view, op.Block!, context))
                        {
                            var segment = 0;
                            view.Update(context, op.Block!, op.Block!.Key, ref segment);
                        }
                        else
                        {
                            _remove(op.Index, view);
                            var replacement = BlockViewFactory.Create(op.Block!, context);
                            var segment = 0;
                            replacement.Update(context, op.Block!, op.Block!.Key, ref segment);
                            _views[op.Index] = replacement;
                            _insert(op.Index, replacement);
                        }
                        break;
                    }
                case DiffKind.Remove:
                    _remove(op.Index, _views[op.Index]);
                    _views.RemoveAt(op.Index);
                    break;
            }
        }
        if (contextChanged)
        {
            Restyle(snapshot, context);
        }
        Applied = snapshot;
        UpdateTail(context);
        return ops.Count > 0 || contextChanged;
    }

    // 样式 / 服务变化：所有视图按原模型重新应用（视图类型可能变化，例如新增了图表渲染器）
    private void Restyle(DocumentSnapshot snapshot, RenderContext context)
    {
        for (var i = 0; i < _views.Count && i < snapshot.Count; i++)
        {
            var block = snapshot[i];
            var segment = 0;
            if (BlockViewFactory.CanReuse(_views[i], block, context))
            {
                _views[i].Update(context, block, block.Key, ref segment);
                continue;
            }
            _remove(i, _views[i]);
            var replacement = BlockViewFactory.Create(block, context);
            replacement.Update(context, block, block.Key, ref segment);
            _views[i] = replacement;
            _insert(i, replacement);
        }
    }

    private void UpdateTail(RenderContext context)
    {
        var tail = context.Streaming is null || _views.Count == 0 ? null : _views[^1].LastSegment();
        if (ReferenceEquals(tail, _tail))
        {
            tail?.InvalidateVisual();
            return;
        }
        if (_tail is not null)
        {
            _tail.IsDocumentTail = false;
            _tail.InvalidateVisual();
        }
        _tail = tail;
        if (_tail is not null)
        {
            _tail.IsDocumentTail = true;
            _tail.InvalidateVisual();
        }
    }

    public void Clear()
    {
        for (var i = _views.Count - 1; i >= 0; i--)
        {
            _remove(i, _views[i]);
        }
        _views.Clear();
        Applied = null;
        _tail = null;
    }
}
