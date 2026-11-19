using StreamMark.Core.Model;

namespace StreamMark.Avalonia.Selection;

/// <summary>源码位置与渲染位置的对应精度。目前只有块级：不细分到块内的行或字符。</summary>
public enum SourcePrecision
{
    /// <summary>偏移落在返回的块区间内（块级精度）。</summary>
    Block,

    /// <summary>偏移落在块与块之间（空行等）或文档末尾之后，结果是它之后最近的块（末尾之后则为最后一块）。</summary>
    Nearest,
}

/// <summary>
/// 源码偏移解析出的渲染位置：<see cref="Point"/> 可直接交给 <c>ScrollToPoint</c>；
/// <see cref="Range"/> 是命中块（容器内取最深的块）的源码区间。
/// </summary>
public readonly record struct SourceTarget(TextPoint Point, SourceRange Range, SourcePrecision Precision);

public enum SourceNavigationStatus
{
    /// <summary>已滚动到目标。</summary>
    Navigated,

    /// <summary>还没有应用任何快照（刚设置 <c>Markdown</c> 时就是这样），或文档没有块。</summary>
    NoDocument,

    /// <summary>偏移为负，或超过当前快照的源码长度。</summary>
    OutOfRange,

    /// <summary>目标块的视图还没有完成排版，没有滚动；等下一帧再试。</summary>
    LayoutPending,
}

public readonly record struct SourceNavigationResult(SourceNavigationStatus Status, SourceTarget? Target = null);

/// <summary>
/// 阅读锚点：重新排版（宽度、字体、主题变化）后回到同一处阅读位置所需的信息。
/// 位置以文本坐标记录（<see cref="BlockKey"/> 是块的源码起始偏移），所以内容重新折行后仍然指向同一个字；
/// <see cref="Displacement"/> 是该位置相对可视区域顶部的像素位移。
/// </summary>
/// <param name="DocumentId">捕获时 <see cref="MarkdownView.DocumentId"/> 的值；不相等的视图文档不会恢复。</param>
/// <param name="BlockKey">顶层块的源码起始偏移。</param>
/// <param name="Segment">块内文本片段序号；块里没有文本片段时为 -1。</param>
/// <param name="Offset">片段内的字符偏移（不是源码偏移）。</param>
/// <param name="Displacement">锚点位置的顶边到可视区域顶边的距离，向下为正；位置在可视区域上方时为负。</param>
public sealed record ReadingAnchor(int DocumentId, int BlockKey, int Segment, int Offset, double Displacement)
{
    /// <summary>锚点所在块在源码中的起始偏移（等于 <see cref="BlockKey"/>）。</summary>
    public int SourceOffset => BlockKey;
}

public enum AnchorRestoreStatus
{
    /// <summary>已恢复到捕获时的位置。</summary>
    Restored,

    /// <summary>锚点所在的块已不存在，改为滚动到它之后最近的块（没有则最后一块）。</summary>
    Approximate,

    /// <summary>锚点来自另一个文档（<see cref="MarkdownView.DocumentId"/> 不同），没有恢复。</summary>
    WrongDocument,

    /// <summary>还没有应用任何快照，或文档没有块。</summary>
    NoDocument,

    /// <summary>找不到外层滚动容器（<c>ScrollViewer</c>），没有恢复。</summary>
    NoViewport,

    /// <summary>目标块的视图还没有完成排版，没有恢复；等下一帧再试。</summary>
    LayoutPending,
}
