using Avalonia;
using Avalonia.Controls;

namespace StreamMark.Avalonia.Selection;

/// <summary>可参与选区的文本片段视图（段落文本、代码文本、公式源码等）。</summary>
public interface ISelectableSegment
{
    SegmentKey Key { get; }

    /// <summary>片段文本（复制时 U+FFFC 占位对象会被替换为其源码或替代文字）。</summary>
    string Text { get; }

    /// <summary>片段对应的控件，用于坐标换算。</summary>
    Control View { get; }

    /// <summary>本地坐标 → 字符偏移。</summary>
    int HitTest(Point point);

    /// <summary>字符偏移 → 本地矩形。</summary>
    Rect GetCharacterBounds(int offset);
}
