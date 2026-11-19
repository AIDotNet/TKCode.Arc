using System.Collections.ObjectModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using StreamMark.Avalonia.Selection;

namespace StreamMark.Avalonia.Transcript;

/// <summary>
/// 长会话视图：滚动容器 + <see cref="TranscriptPanel"/>。沿用宿主主题中 <see cref="ScrollViewer"/> 的模板。
/// 向 <see cref="Entries"/> 添加 <see cref="MarkdownEntry"/> / <see cref="ContentEntry"/> 即可。
/// </summary>
public class TranscriptView : ScrollViewer
{
    public TranscriptView()
    {
        Panel = new TranscriptPanel();
        Panel.SetEntries(Entries);
        Content = Panel;
        HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled;
        VerticalScrollBarVisibility = ScrollBarVisibility.Auto;
    }

    protected override Type StyleKeyOverride => typeof(ScrollViewer);

    public ObservableCollection<TranscriptEntry> Entries { get; } = [];

    public TranscriptPanel Panel { get; }

    public SelectionModel Selection => Panel.Selection;

    public SelectionController SelectionController => Panel.SelectionController;

    /// <summary>会话内查找（高亮全部结果，<c>Next</c> / <c>Previous</c> 跳转）。</summary>
    public SearchModel Search => Panel.Search;

    /// <summary>所有已解析消息的目录。</summary>
    public List<OutlineItem> GetOutline() => Panel.GetOutline();

    /// <summary>滚动到选区坐标所在位置。</summary>
    public void ScrollToPoint(TextPoint point) => Panel.ScrollToPoint(point);

    public bool IsPinnedToBottom => Panel.IsPinnedToBottom;

    /// <summary>滚动到底部并恢复贴底跟随。</summary>
    public new void ScrollToEnd() => Panel.ScrollToEnd();

    /// <summary>内容列的最大宽度（居中）。</summary>
    public double MaxContentWidth
    {
        get => Panel.MaxContentWidth;
        set => Panel.MaxContentWidth = value;
    }

    public double EntrySpacing
    {
        get => Panel.EntrySpacing;
        set => Panel.EntrySpacing = value;
    }
}
