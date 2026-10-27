using System.Text.Json;

namespace TKCode.Terminal.Core.Interop;

/// <summary>
/// GhosttyCell 的位布局，启动时从 ghostty_type_json() 解析（02 §4.2、§6.5：位置不受 ABI 保护，不能硬编码）。
/// </summary>
internal sealed class GhosttyCellLayout
{
    public required int TagShift { get; init; }
    public required int ContentShift { get; init; }
    public required int StyleShift { get; init; }
    public required int WideShift { get; init; }
    public required int HyperlinkShift { get; init; }
    public required int SemanticShift { get; init; }

    private static readonly Lazy<GhosttyCellLayout> Instance = new(() => Parse(GhosttyRuntime.TypeManifestJson()));

    public static GhosttyCellLayout Current => Instance.Value;

    public static GhosttyCellLayout Parse(string manifestJson)
    {
        using var doc = JsonDocument.Parse(manifestJson);
        var cell = doc.RootElement.GetProperty("types").GetProperty("GhosttyCell");
        if (cell.GetProperty("kind").GetString() != "packed" || cell.GetProperty("size").GetInt32() != 8)
        {
            throw new GhosttyAbiMismatchException("GhosttyCell is not a packed u64");
        }
        var bits = cell.GetProperty("bits");
        int Lsb(string field, int width)
        {
            var f = bits.GetProperty(field);
            if (f.GetProperty("width").GetInt32() != width)
            {
                throw new GhosttyAbiMismatchException($"GhosttyCell.{field} width changed");
            }
            return f.GetProperty("lsb").GetInt32();
        }
        var codepoint = bits.GetProperty("content").GetProperty("arms").GetProperty("CODEPOINT").GetProperty("bits").GetProperty("codepoint");
        if (codepoint.GetProperty("lsb").GetInt32() != 0 || codepoint.GetProperty("width").GetInt32() != 21)
        {
            throw new GhosttyAbiMismatchException("GhosttyCell codepoint arm changed");
        }
        return new GhosttyCellLayout
        {
            TagShift = Lsb("content_tag", 2),
            ContentShift = Lsb("content", 24),
            StyleShift = Lsb("style_id", 16),
            WideShift = Lsb("wide", 2),
            HyperlinkShift = Lsb("hyperlink", 1),
            SemanticShift = Lsb("semantic_content", 2),
        };
    }
}

/// <summary>原生库与绑定不匹配（布局或枚举变化）。pty host 拒绝启动会话，Desktop 显示"终端组件与原生库不匹配"。</summary>
public sealed class GhosttyAbiMismatchException(string message) : Exception(message);
