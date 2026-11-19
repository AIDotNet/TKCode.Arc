using System.Text.Json.Serialization;

namespace StreamMark.RaTeX;

/// <summary>
/// RaTeX 显示列表（协议 v1）。所有坐标以 em 为单位，Y 轴向下，基线位于 y = <see cref="Height"/>。
/// 条目使用扁平结构而不是多态类型，以便忽略未来新增的条目类型和字段。
/// </summary>
public sealed class DisplayList
{
    public int Version { get; init; }
    public double Width { get; init; }
    public double Height { get; init; }
    public double Depth { get; init; }
    public IReadOnlyList<DisplayItem> Items { get; init; } = [];
}

public sealed class DisplayItem
{
    /// <summary>GlyphPath、Line、Rect、Path；未知类型由绘制端跳过。</summary>
    public string Type { get; init; } = "";
    public double X { get; init; }
    public double Y { get; init; }
    public double Scale { get; init; } = 1;
    public string? Font { get; init; }
    public uint CharCode { get; init; }
    public double Width { get; init; }
    public double Height { get; init; }
    public double Thickness { get; init; }
    public bool? Dashed { get; init; }
    public bool Fill { get; init; }
    public DisplayColor? Color { get; init; }
    public IReadOnlyList<PathCommand>? Commands { get; init; }
}

public sealed class PathCommand
{
    /// <summary>MoveTo、LineTo、CubicTo、QuadTo、Close。</summary>
    public string Type { get; init; } = "";
    public double X { get; init; }
    public double Y { get; init; }
    public double X1 { get; init; }
    public double Y1 { get; init; }
    public double X2 { get; init; }
    public double Y2 { get; init; }
}

public readonly record struct DisplayColor(float R, float G, float B, float A);

[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.SnakeCaseLower)]
[JsonSerializable(typeof(DisplayList))]
internal sealed partial class DisplayListJsonContext : JsonSerializerContext;
