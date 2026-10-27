using System.Globalization;
using System.Text;
using System.Text.Json;

namespace TKCode.Terminal.Core.Testing;

/// <summary>asciicast 事件（11-testing-benchmarks.md §2.1）。时间只用于按真实节奏回放，回放测试不使用。</summary>
public abstract record CastEvent(double Time);

/// <summary><c>o</c>（UTF-8 文本）或 <c>ob</c>（TKCode 扩展：base64 原始字节）。</summary>
public sealed record CastOutput(double Time, byte[] Data) : CastEvent(Time);

/// <summary><c>r</c>："COLSxROWS"，或 TKCode 扩展 "COLSxROWS@CWxCH"。</summary>
public sealed record CastResize(double Time, int Columns, int Rows, int CellWidth, int CellHeight) : CastEvent(Time);

/// <summary><c>m</c>：快照点。</summary>
public sealed record CastMarker(double Time, string Name) : CastEvent(Time);

/// <summary><c>xr</c>：TKCode 扩展，Reset（"hard" / "soft"）。</summary>
public sealed record CastReset(double Time, bool Hard) : CastEvent(Time);

public sealed record CastHeader(int Width, int Height, int? Scrollback, WidthPolicy? Width2027, string? Source);

/// <summary>asciicast v2 读写（读取同时支持 v3）。未知事件码按规范忽略（<c>i</c>、<c>xc</c> 等）。AOT 安全。</summary>
public static class Cast
{
    public static (CastHeader Header, List<CastEvent> Events) Parse(string text)
    {
        var lines = text.Split('\n');
        using var head = JsonDocument.Parse(lines[0]);
        var root = head.RootElement;
        var version = root.GetProperty("version").GetInt32();
        int width, height;
        if (version >= 3)
        {
            var term = root.GetProperty("term");
            (width, height) = (term.GetProperty("cols").GetInt32(), term.GetProperty("rows").GetInt32());
        }
        else
        {
            (width, height) = (root.GetProperty("width").GetInt32(), root.GetProperty("height").GetInt32());
        }
        int? scrollback = null;
        WidthPolicy? widthMode = null;
        string? source = null;
        if (root.TryGetProperty("tkcode", out var ext))
        {
            if (ext.TryGetProperty("scrollback", out var s)) scrollback = s.GetInt32();
            if (ext.TryGetProperty("widthMode", out var w)) widthMode = w.GetString() == "grapheme" ? WidthPolicy.Grapheme : WidthPolicy.Compat;
            if (ext.TryGetProperty("source", out var src)) source = src.GetString();
        }

        var events = new List<CastEvent>();
        var time = 0.0;
        for (var i = 1; i < lines.Length; i++)
        {
            var line = lines[i].Trim();
            if (line.Length == 0 || line[0] == '#')
            {
                continue;
            }
            using var doc = JsonDocument.Parse(line);
            var e = doc.RootElement;
            var t = e[0].GetDouble();
            time = version >= 3 ? time + t : t;  // v3 是相对间隔
            var data = e[2].GetString() ?? string.Empty;
            switch (e[1].GetString())
            {
                case "o":
                    events.Add(new CastOutput(time, Encoding.UTF8.GetBytes(data)));
                    break;
                case "ob":
                    events.Add(new CastOutput(time, Convert.FromBase64String(data)));
                    break;
                case "r":
                    events.Add(ParseResize(time, data));
                    break;
                case "m":
                    events.Add(new CastMarker(time, data));
                    break;
                case "xr":
                    events.Add(new CastReset(time, data != "soft"));
                    break;
            }
        }
        return (new CastHeader(width, height, scrollback, widthMode, source), events);
    }

    private static CastResize ParseResize(double time, string data)
    {
        var at = data.Split('@');
        var grid = at[0].Split('x');
        var cell = at.Length > 1 ? at[1].Split('x') : ["8", "16"];
        return new CastResize(time, Int(grid[0]), Int(grid[1]), Int(cell[0]), Int(cell[1]));
        static int Int(string s) => int.Parse(s, CultureInfo.InvariantCulture);
    }

    /// <summary>按夹具头部创建核心（cell 8×16）。</summary>
    public static TerminalCore CreateCore(CastHeader header, ITerminalEffects? effects = null)
    {
        var options = new TerminalCoreOptions(new TerminalSize(header.Width, header.Height, 8, 16))
        {
            Width = header.Width2027 ?? WidthPolicy.Compat,
        };
        if (header.Scrollback is { } lines)
        {
            options = options with { ScrollbackMaxLines = lines };
        }
        return TerminalCore.Create(options, effects);
    }

    /// <summary>
    /// 回放事件。<paramref name="chunk"/> 为 null 时每个输出事件整块写入；否则按固定大小切块（0 表示随机 1–4096），
    /// 用于分块不变性检查（UTF-8 与转义序列跨块截断，§1.1.1）。每个标记调用一次 <paramref name="onMarker"/>。
    /// </summary>
    public static void Replay(TerminalCore core, IEnumerable<CastEvent> events, int? chunk = null, Random? random = null,
        Action<string>? onMarker = null)
    {
        foreach (var e in events)
        {
            switch (e)
            {
                case CastOutput o when chunk is null:
                    core.Write(o.Data);
                    break;
                case CastOutput o:
                    for (var offset = 0; offset < o.Data.Length;)
                    {
                        var size = chunk.Value > 0 ? chunk.Value : (random ?? Random.Shared).Next(1, 4097);
                        size = Math.Min(size, o.Data.Length - offset);
                        core.Write(o.Data.AsSpan(offset, size));
                        offset += size;
                    }
                    break;
                case CastResize r:
                    core.Resize(new TerminalSize(r.Columns, r.Rows, r.CellWidth, r.CellHeight));
                    break;
                case CastReset:
                    core.Reset();
                    break;
                case CastMarker m:
                    onMarker?.Invoke(m.Name);
                    break;
            }
        }
    }
}
