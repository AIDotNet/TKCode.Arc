using Avalonia;
using Avalonia.Media;

namespace StreamMark.Avalonia.Rendering;

/// <summary>
/// 文本片段的一行：只录制这一行的文字与删除线。流式追加时只有变化的行重新录制、产生脏区。
/// 不是输入元素：命中测试由所属的 <see cref="TextSegmentView"/> 处理。
/// </summary>
internal sealed class SegmentLineVisual(TextSegmentView owner) : Visual
{
    private double? _clipRight;

    public int Index { get; set; }

    /// <summary>只绘制这个横坐标左侧的文字（其右侧正在渐显，由渐显层绘制）；null 表示整行绘制。</summary>
    public double? ClipRight
    {
        get => _clipRight;
        set
        {
            if (_clipRight != value)
            {
                _clipRight = value;
                InvalidateVisual();
            }
        }
    }

    public void SetBounds(Rect bounds)
    {
        if (Bounds != bounds)
        {
            Bounds = bounds;
        }
    }

    public override void Render(DrawingContext context) => owner.RenderLine(context, Index, _clipRight);
}
