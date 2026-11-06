using System.Text;
using System.Text.Json;
using Avalonia;
using Avalonia.Layout;

namespace TKCode.Arc;

/// <summary>A node of a saved layout, read before any pane is touched (see <see cref="DockLayoutSerializer"/>).</summary>
internal abstract class DockNodeData
{
    public double Size { get; set; } = 1;
}

internal sealed class DockGroupData : DockNodeData
{
    public List<string> Panes { get; } = [];

    public string? Active { get; set; }
}

internal sealed class DockSplitData : DockNodeData
{
    public Orientation Orientation { get; set; }

    public List<DockNodeData> Children { get; } = [];
}

internal sealed class DockDocumentsData : DockNodeData
{
    public DockNodeData? Root { get; set; }
}

internal sealed class DockFloatData
{
    public Rect Bounds { get; set; }

    public bool Maximized { get; set; }

    public DockNodeData? Root { get; set; }
}

internal sealed class DockLayoutData
{
    public string? Active { get; set; }

    public DockNodeData? Root { get; set; }

    public Dictionary<DockSide, List<string>> Hidden { get; } = new();

    public List<DockFloatData> Floats { get; } = [];

    /// <summary>Ids of known panes that were closed when the layout was saved: they are not put back on load.</summary>
    public List<string> Closed { get; } = [];
}

/// <summary>
/// Reads and writes the saved layout format (<c>tkcode-arc-dock</c>, version 1; spec section 4.6) by hand with
/// <see cref="Utf8JsonWriter"/> and <see cref="Utf8JsonReader"/>: no reflection, no source generator. Panes are stored by
/// <see cref="DockPane.Id"/> only; their content comes from the host.
/// </summary>
internal static class DockLayoutSerializer
{
    public const string FormatName = "tkcode-arc-dock";

    public const int Version = 1;

    private static readonly (DockSide Side, string Name)[] s_sides =
        [(DockSide.Left, "left"), (DockSide.Right, "right"), (DockSide.Top, "top"), (DockSide.Bottom, "bottom")];

    #region Writing

    public static string Write(DockWorkspace workspace, string? activeId, IEnumerable<string>? closedIds = null)
    {
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream, new JsonWriterOptions { Indented = true }))
        {
            writer.WriteStartObject();
            writer.WriteString("format", FormatName);
            writer.WriteNumber("version", Version);
            if (activeId is not null)
            {
                writer.WriteString("active", activeId);
            }
            writer.WritePropertyName("root");
            WriteNode(writer, workspace.Root, writeSize: true);
            writer.WritePropertyName("hidden");
            writer.WriteStartObject();
            foreach (var (side, name) in s_sides)
            {
                writer.WritePropertyName(name);
                WriteIds(writer, workspace.AutoHidden(side).Select(p => p.Id));
            }
            writer.WriteEndObject();
            writer.WritePropertyName("floats");
            writer.WriteStartArray();
            foreach (var dock in workspace.Floats)
            {
                writer.WriteStartObject();
                writer.WritePropertyName("bounds");
                writer.WriteStartArray();
                writer.WriteNumberValue(Round(dock.Bounds.X));
                writer.WriteNumberValue(Round(dock.Bounds.Y));
                writer.WriteNumberValue(Round(dock.Bounds.Width));
                writer.WriteNumberValue(Round(dock.Bounds.Height));
                writer.WriteEndArray();
                writer.WriteBoolean("maximized", dock.IsMaximized);
                writer.WritePropertyName("root");
                WriteNode(writer, dock.Root, writeSize: false);
                writer.WriteEndObject();
            }
            writer.WriteEndArray();
            if (closedIds is not null)
            {
                var closed = closedIds.ToList();
                if (closed.Count > 0)
                {
                    writer.WritePropertyName("closed");
                    WriteIds(writer, closed);
                }
            }
            writer.WriteEndObject();
        }
        return Encoding.UTF8.GetString(stream.ToArray());
    }

    private static void WriteNode(Utf8JsonWriter writer, DockNode? node, bool writeSize)
    {
        switch (node)
        {
            case null:
                writer.WriteNullValue();
                return;
            case DockSplit split:
                writer.WriteStartObject();
                writer.WriteString("split", split.Orientation == Orientation.Vertical ? "v" : "h");
                WriteSize(writer, split, writeSize);
                writer.WritePropertyName("children");
                writer.WriteStartArray();
                foreach (var child in split.Children)
                {
                    WriteNode(writer, child, writeSize: true);
                }
                writer.WriteEndArray();
                writer.WriteEndObject();
                return;
            case DockGroup group:
                writer.WriteStartObject();
                writer.WritePropertyName("group");
                WriteIds(writer, group.Panes.Select(p => p.Id));
                if (group.ActivePane is { } active)
                {
                    writer.WriteString("active", active.Id);
                }
                WriteSize(writer, group, writeSize);
                writer.WriteEndObject();
                return;
            case DockDocumentArea area:
                writer.WriteStartObject();
                writer.WritePropertyName("documents");
                WriteNode(writer, area.Root, writeSize: false);
                WriteSize(writer, area, writeSize);
                writer.WriteEndObject();
                return;
        }
    }

    private static void WriteSize(Utf8JsonWriter writer, DockNode node, bool writeSize)
    {
        if (writeSize)
        {
            writer.WriteNumber("size", Round(node.Size));
        }
    }

    private static void WriteIds(Utf8JsonWriter writer, IEnumerable<string> ids)
    {
        writer.WriteStartArray();
        foreach (var id in ids)
        {
            writer.WriteStringValue(id);
        }
        writer.WriteEndArray();
    }

    private static double Round(double value) => double.IsFinite(value) ? Math.Round(value, 4) : 0;

    #endregion

    #region Reading

    /// <summary>
    /// Parses a saved layout. False when the text is not JSON, not this format, a version above <see cref="Version"/>, or
    /// a node is malformed; unknown fields are skipped.
    /// </summary>
    public static bool TryRead(string json, out DockLayoutData data)
    {
        data = new DockLayoutData();
        if (string.IsNullOrWhiteSpace(json))
        {
            return false;
        }
        try
        {
            var reader = new Utf8JsonReader(Encoding.UTF8.GetBytes(json),
                new JsonReaderOptions { CommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true });
            if (!reader.Read() || reader.TokenType != JsonTokenType.StartObject)
            {
                return false;
            }
            var formatOk = false;
            var versionOk = false;
            while (Next(ref reader) is { } name)
            {
                switch (name)
                {
                    case "format":
                        formatOk = reader.TokenType == JsonTokenType.String && reader.GetString() == FormatName;
                        break;
                    case "version":
                        if (reader.TokenType != JsonTokenType.Number || !reader.TryGetInt32(out var version) || version < 1 || version > Version)
                        {
                            return false;
                        }
                        versionOk = true;
                        break;
                    case "active":
                        data.Active = reader.TokenType == JsonTokenType.String ? reader.GetString() : null;
                        break;
                    case "root":
                        data.Root = ReadNode(ref reader);
                        break;
                    case "hidden":
                        ReadHidden(ref reader, data);
                        break;
                    case "floats":
                        ReadFloats(ref reader, data);
                        break;
                    case "closed":
                        data.Closed.AddRange(ReadIds(ref reader));
                        break;
                    default:
                        reader.Skip();
                        break;
                }
            }
            // A workspace has at most one document area.
            return formatOk && versionOk && CountDocuments(data.Root) <= 1;
        }
        catch (JsonException)
        {
            return false;
        }
        catch (FormatException)
        {
            return false;
        }
        catch (InvalidOperationException)
        {
            return false;
        }
    }

    /// <summary>Moves to the next property of the current object and onto its value; null at the end of the object.</summary>
    private static string? Next(ref Utf8JsonReader reader)
    {
        if (!reader.Read())
        {
            throw new FormatException("Unexpected end of the layout.");
        }
        if (reader.TokenType == JsonTokenType.EndObject)
        {
            return null;
        }
        if (reader.TokenType != JsonTokenType.PropertyName)
        {
            throw new FormatException("Expected a property.");
        }
        var name = reader.GetString();
        if (!reader.Read())
        {
            throw new FormatException("Unexpected end of the layout.");
        }
        return name ?? string.Empty;
    }

    private static DockNodeData? ReadNode(ref Utf8JsonReader reader)
    {
        if (reader.TokenType == JsonTokenType.Null)
        {
            return null;
        }
        if (reader.TokenType != JsonTokenType.StartObject)
        {
            throw new FormatException("A layout node must be an object.");
        }
        Orientation? orientation = null;
        List<string>? panes = null;
        string? active = null;
        var hasDocuments = false;
        DockNodeData? documents = null;
        List<DockNodeData>? children = null;
        var size = 1.0;
        while (Next(ref reader) is { } name)
        {
            switch (name)
            {
                case "split":
                    orientation = reader.GetString() switch
                    {
                        "h" => Orientation.Horizontal,
                        "v" => Orientation.Vertical,
                        _ => throw new FormatException("A split is \"h\" or \"v\".")
                    };
                    break;
                case "group":
                    panes = ReadIds(ref reader);
                    break;
                case "documents":
                    hasDocuments = true;
                    documents = ReadNode(ref reader);
                    break;
                case "children":
                    if (reader.TokenType != JsonTokenType.StartArray)
                    {
                        throw new FormatException("Children must be an array.");
                    }
                    children = [];
                    while (reader.Read() && reader.TokenType != JsonTokenType.EndArray)
                    {
                        if (ReadNode(ref reader) is { } child)
                        {
                            children.Add(child);
                        }
                    }
                    break;
                case "active":
                    active = reader.TokenType == JsonTokenType.String ? reader.GetString() : null;
                    break;
                case "size":
                    if (reader.TokenType == JsonTokenType.Number && reader.TryGetDouble(out var value) && double.IsFinite(value) && value >= 0)
                    {
                        size = value;
                    }
                    break;
                default:
                    reader.Skip();
                    break;
            }
        }
        var kinds = (orientation is not null ? 1 : 0) + (panes is not null ? 1 : 0) + (hasDocuments ? 1 : 0);
        if (kinds != 1)
        {
            throw new FormatException("A layout node is exactly one of split, group and documents.");
        }
        if (orientation is { } o)
        {
            var split = new DockSplitData { Orientation = o, Size = size };
            split.Children.AddRange(children ?? []);
            return split;
        }
        if (panes is not null)
        {
            var group = new DockGroupData { Active = active, Size = size };
            group.Panes.AddRange(panes);
            return group;
        }
        if (documents is DockDocumentsData)
        {
            throw new FormatException("A document area cannot contain a document area.");
        }
        return new DockDocumentsData { Root = documents, Size = size };
    }

    private static int CountDocuments(DockNodeData? node) => node switch
    {
        DockDocumentsData => 1,
        DockSplitData split => split.Children.Sum(CountDocuments),
        _ => 0
    };

    private static List<string> ReadIds(ref Utf8JsonReader reader)
    {
        if (reader.TokenType != JsonTokenType.StartArray)
        {
            throw new FormatException("Expected an array of pane ids.");
        }
        var ids = new List<string>();
        while (reader.Read() && reader.TokenType != JsonTokenType.EndArray)
        {
            if (reader.TokenType == JsonTokenType.String && reader.GetString() is { Length: > 0 } id)
            {
                ids.Add(id);
            }
            else
            {
                reader.Skip();
            }
        }
        return ids;
    }

    private static void ReadHidden(ref Utf8JsonReader reader, DockLayoutData data)
    {
        if (reader.TokenType == JsonTokenType.Null)
        {
            return;
        }
        if (reader.TokenType != JsonTokenType.StartObject)
        {
            throw new FormatException("Hidden must be an object.");
        }
        while (Next(ref reader) is { } name)
        {
            DockSide? side = name switch
            {
                "left" => DockSide.Left,
                "right" => DockSide.Right,
                "top" => DockSide.Top,
                "bottom" => DockSide.Bottom,
                _ => null
            };
            if (side is { } s)
            {
                data.Hidden[s] = ReadIds(ref reader);
            }
            else
            {
                reader.Skip();
            }
        }
    }

    private static void ReadFloats(ref Utf8JsonReader reader, DockLayoutData data)
    {
        if (reader.TokenType == JsonTokenType.Null)
        {
            return;
        }
        if (reader.TokenType != JsonTokenType.StartArray)
        {
            throw new FormatException("Floats must be an array.");
        }
        while (reader.Read() && reader.TokenType != JsonTokenType.EndArray)
        {
            if (reader.TokenType != JsonTokenType.StartObject)
            {
                throw new FormatException("A floating window must be an object.");
            }
            var dock = new DockFloatData();
            while (Next(ref reader) is { } name)
            {
                switch (name)
                {
                    case "bounds":
                        dock.Bounds = ReadBounds(ref reader);
                        break;
                    case "maximized":
                        dock.Maximized = reader.TokenType == JsonTokenType.True;
                        break;
                    case "root":
                        dock.Root = ReadNode(ref reader);
                        break;
                    default:
                        reader.Skip();
                        break;
                }
            }
            if (dock.Root is DockDocumentsData)
            {
                throw new FormatException("The document area cannot float.");
            }
            data.Floats.Add(dock);
        }
    }

    private static Rect ReadBounds(ref Utf8JsonReader reader)
    {
        if (reader.TokenType != JsonTokenType.StartArray)
        {
            throw new FormatException("Bounds must be [x, y, width, height].");
        }
        Span<double> values = stackalloc double[4];
        var count = 0;
        while (reader.Read() && reader.TokenType != JsonTokenType.EndArray)
        {
            if (reader.TokenType != JsonTokenType.Number || count >= 4)
            {
                throw new FormatException("Bounds must be [x, y, width, height].");
            }
            values[count++] = reader.GetDouble();
        }
        if (count != 4)
        {
            throw new FormatException("Bounds must be [x, y, width, height].");
        }
        return new Rect(values[0], values[1], Math.Max(0, values[2]), Math.Max(0, values[3]));
    }

    #endregion

    /// <summary>
    /// Pulls a floating window back into <paramref name="workArea"/> when too little of it is left on screen to grab
    /// (its display was unplugged): it keeps its size up to the work area's and moves to the nearest place inside.
    /// </summary>
    public static Rect ClampToWorkArea(Rect bounds, Rect workArea)
    {
        if (workArea.Width <= 0 || workArea.Height <= 0)
        {
            return bounds;
        }
        var visible = bounds.Intersect(workArea);
        if (visible.Width >= Math.Min(48, bounds.Width) && visible.Height >= Math.Min(24, bounds.Height) && bounds.Y >= workArea.Y)
        {
            return bounds;
        }
        var width = Math.Min(bounds.Width, workArea.Width);
        var height = Math.Min(bounds.Height, workArea.Height);
        var x = Math.Clamp(bounds.X, workArea.X, workArea.Right - width);
        var y = Math.Clamp(bounds.Y, workArea.Y, workArea.Bottom - height);
        return new Rect(x, y, width, height);
    }
}
