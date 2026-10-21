using System.Globalization;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;

namespace TKCode.Arc;

/// <summary>Type of a JSON value shown by <see cref="JsonViewer"/>.</summary>
public enum JsonViewerValueType
{
    Object,
    Array,
    String,
    Number,
    Boolean,
    Null
}

/// <summary>
/// A JSON value in the viewer's tree. Children are created lazily (only as far as a branch is shown), so a large
/// document costs nodes for what is open, not for everything it contains.
/// </summary>
internal sealed class JsonViewerNode
{
    private List<JsonViewerNode>? _children;
    private JsonElement.ObjectEnumerator _objects;
    private bool _objectsStarted;
    private int _count = -1;

    public JsonViewerNode(JsonElement element, string path, string? key, int index, JsonViewerNode? parent, int level)
    {
        Element = element;
        Path = path;
        Key = key;
        Index = index;
        Parent = parent;
        Level = level;
        Type = TypeOf(element);
    }

    public JsonElement Element { get; }
    public string Path { get; }

    /// <summary>Property name, or null for array items and the root.</summary>
    public string? Key { get; }

    /// <summary>Array index, or -1.</summary>
    public int Index { get; }

    public JsonViewerNode? Parent { get; }

    /// <summary>1-based depth (the root is 1).</summary>
    public int Level { get; }

    public JsonViewerValueType Type { get; }

    public bool IsBranch => Type is JsonViewerValueType.Object or JsonViewerValueType.Array;

    public bool IsRoot => Parent is null;

    /// <summary>Number of children of a branch.</summary>
    public int Count
    {
        get
        {
            if (_count >= 0)
            {
                return _count;
            }
            _count = Type switch
            {
                JsonViewerValueType.Array => Element.GetArrayLength(),
                JsonViewerValueType.Object => CountProperties(Element),
                _ => 0
            };
            return _count;
        }
    }

    private static int CountProperties(JsonElement element)
    {
        var count = 0;
        foreach (var _ in element.EnumerateObject())
        {
            count++;
        }
        return count;
    }

    /// <summary>The first <paramref name="limit"/> children (created on demand).</summary>
    public IReadOnlyList<JsonViewerNode> Children(int limit)
    {
        limit = Math.Min(limit, Count);
        _children ??= new List<JsonViewerNode>(Math.Min(limit, 64));
        if (Type == JsonViewerValueType.Array)
        {
            for (var i = _children.Count; i < limit; i++)
            {
                _children.Add(new JsonViewerNode(Element[i], JsonViewerPaths.Child(Path, i), null, i, this, Level + 1));
            }
        }
        else if (Type == JsonViewerValueType.Object)
        {
            if (!_objectsStarted)
            {
                _objects = Element.EnumerateObject();
                _objectsStarted = true;
            }
            while (_children.Count < limit && _objects.MoveNext())
            {
                var property = _objects.Current;
                _children.Add(new JsonViewerNode(property.Value, JsonViewerPaths.Child(Path, property.Name), property.Name, -1, this, Level + 1));
            }
        }
        return _children;
    }

    /// <summary>Arc's <c>primitiveText</c>.</summary>
    public string PrimitiveText => JsonViewerPaths.PrimitiveText(Element);

    public static JsonViewerValueType TypeOf(JsonElement element) => element.ValueKind switch
    {
        JsonValueKind.Object => JsonViewerValueType.Object,
        JsonValueKind.Array => JsonViewerValueType.Array,
        JsonValueKind.String => JsonViewerValueType.String,
        JsonValueKind.Number => JsonViewerValueType.Number,
        JsonValueKind.True or JsonValueKind.False => JsonViewerValueType.Boolean,
        _ => JsonViewerValueType.Null
    };
}

/// <summary>Path, text and type-label helpers (Arc's <c>childPath</c>, <c>copyText</c>, <c>countLabel</c>).</summary>
internal static class JsonViewerPaths
{
    private static readonly JavaScriptEncoder s_encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping;

    public static string Child(string parent, int index) => string.Create(CultureInfo.InvariantCulture, $"{parent}[{index}]");

    public static string Child(string parent, string key) => IsIdentifier(key)
        ? string.Concat(parent, ".", key)
        : string.Concat(parent, "[\"", JsonEncodedText.Encode(key, s_encoder).Value, "\"]");

    /// <summary><c>/^[A-Za-z_$][\w$]*$/</c></summary>
    private static bool IsIdentifier(string key)
    {
        if (key.Length == 0 || !(char.IsAsciiLetter(key[0]) || key[0] is '_' or '$'))
        {
            return false;
        }
        foreach (var c in key.AsSpan(1))
        {
            if (!(char.IsAsciiLetterOrDigit(c) || c is '_' or '$'))
            {
                return false;
            }
        }
        return true;
    }

    /// <summary>Whether <paramref name="id"/> is <paramref name="ancestor"/> or lies inside it.</summary>
    public static bool Within(string ancestor, string id) => id == ancestor
        || (id.Length > ancestor.Length && id.StartsWith(ancestor, StringComparison.Ordinal) && id[ancestor.Length] is '.' or '[');

    public static string PrimitiveText(JsonElement element) => element.ValueKind switch
    {
        JsonValueKind.String => element.GetString() ?? string.Empty,
        JsonValueKind.True => "true",
        JsonValueKind.False => "false",
        JsonValueKind.Null => "null",
        JsonValueKind.Undefined => "undefined",
        _ => element.GetRawText()
    };

    /// <summary>Copied text: strings raw, branches as two-space indented JSON, others as their literal.</summary>
    public static string CopyText(JsonElement element)
    {
        if (element.ValueKind is JsonValueKind.Object or JsonValueKind.Array)
        {
            using var stream = new MemoryStream();
            using (var writer = new Utf8JsonWriter(stream, new JsonWriterOptions { Indented = true, Encoder = s_encoder }))
            {
                element.WriteTo(writer);
            }
            return Encoding.UTF8.GetString(stream.GetBuffer(), 0, (int)stream.Length);
        }
        return PrimitiveText(element);
    }

    public static string TypeName(JsonViewerValueType type) => type switch
    {
        JsonViewerValueType.Object => "object",
        JsonViewerValueType.Array => "array",
        JsonViewerValueType.String => "string",
        JsonViewerValueType.Number => "number",
        JsonViewerValueType.Boolean => "boolean",
        _ => "null"
    };

    public static string CountLabel(JsonViewerValueType type, int count) => type == JsonViewerValueType.Array
        ? string.Create(CultureInfo.InvariantCulture, $"{count} {(count == 1 ? "item" : "items")}")
        : string.Create(CultureInfo.InvariantCulture, $"{count} {(count == 1 ? "key" : "keys")}");

    /// <summary>Collapsed preview: <c>[…]</c> or <c>{ a, b, c, … }</c>.</summary>
    public static string Preview(JsonViewerNode node)
    {
        if (node.Type == JsonViewerValueType.Array)
        {
            return "[…]";
        }
        var builder = new StringBuilder("{ ");
        var shown = 0;
        foreach (var property in node.Element.EnumerateObject())
        {
            if (shown == 3)
            {
                builder.Append(", …");
                break;
            }
            if (shown > 0)
            {
                builder.Append(", ");
            }
            builder.Append(property.Name);
            shown++;
        }
        builder.Append(" }");
        return builder.ToString();
    }

    /// <summary>Every branch path (expand all).</summary>
    public static List<string> AllBranches(JsonElement root, string rootName, int maxDepth = int.MaxValue)
    {
        var output = new List<string>();
        Visit(root, rootName, 0);
        return output;

        void Visit(JsonElement element, string path, int level)
        {
            if (level >= maxDepth || element.ValueKind is not (JsonValueKind.Object or JsonValueKind.Array))
            {
                return;
            }
            output.Add(path);
            if (element.ValueKind == JsonValueKind.Array)
            {
                var i = 0;
                foreach (var item in element.EnumerateArray())
                {
                    if (item.ValueKind is JsonValueKind.Object or JsonValueKind.Array)
                    {
                        Visit(item, Child(path, i), level + 1);
                    }
                    i++;
                }
            }
            else
            {
                foreach (var property in element.EnumerateObject())
                {
                    if (property.Value.ValueKind is JsonValueKind.Object or JsonValueKind.Array)
                    {
                        Visit(property.Value, Child(path, property.Name), level + 1);
                    }
                }
            }
        }
    }
}

/// <summary>Result of a search: matching paths (document order, at most 2000), the branches holding them and how far each page must reach.</summary>
internal sealed class JsonViewerSearch
{
    public const int MaxMatches = 2000;

    private JsonViewerSearch(string needle) => Needle = needle;

    public string Needle { get; }
    public List<string> Matches { get; } = new();
    public HashSet<string> MatchSet { get; } = new(StringComparer.Ordinal);
    public HashSet<string> Ancestors { get; } = new(StringComparer.Ordinal);
    public Dictionary<string, int> Needed { get; } = new(StringComparer.Ordinal);

    private sealed class Frame
    {
        public Frame? Parent;
        public string? Path;
        public string? Key;
        public int Index = -1;
        public int ChildIndex;

        public string GetPath(string rootName)
        {
            if (Path is not null)
            {
                return Path;
            }
            Path = Parent is null ? rootName : Key is not null ? JsonViewerPaths.Child(Parent.GetPath(rootName), Key) : JsonViewerPaths.Child(Parent.GetPath(rootName), Index);
            return Path;
        }
    }

    /// <summary>Finds keys and primitive values containing <paramref name="needle"/> (already trimmed, case-insensitive).</summary>
    public static JsonViewerSearch Run(JsonElement root, string rootName, string needle)
    {
        var search = new JsonViewerSearch(needle);
        search.Visit(root, new Frame { Path = rootName }, null, rootName);
        return search;
    }

    private void Visit(JsonElement element, Frame frame, string? name, string rootName)
    {
        if (Matches.Count >= MaxMatches)
        {
            return;
        }
        var kind = element.ValueKind;
        var branch = kind is JsonValueKind.Object or JsonValueKind.Array;
        var keyHit = name is not null && name.Contains(Needle, StringComparison.OrdinalIgnoreCase);
        var valueHit = !branch && JsonViewerPaths.PrimitiveText(element).Contains(Needle, StringComparison.OrdinalIgnoreCase);
        if (keyHit || valueHit)
        {
            var path = frame.GetPath(rootName);
            Matches.Add(path);
            MatchSet.Add(path);
            for (var ancestor = frame.Parent; ancestor is not null; ancestor = ancestor.Parent)
            {
                var ancestorPath = ancestor.GetPath(rootName);
                Ancestors.Add(ancestorPath);
            }
            // How far each page must reach: the child index (within each ancestor) on the way down.
            var child = frame;
            for (var ancestor = frame.Parent; ancestor is not null; ancestor = ancestor.Parent)
            {
                var ancestorPath = ancestor.GetPath(rootName);
                var reach = child.ChildIndex + 1;
                Needed[ancestorPath] = Needed.TryGetValue(ancestorPath, out var current) ? Math.Max(current, reach) : reach;
                child = ancestor;
            }
        }
        if (!branch)
        {
            return;
        }
        var index = 0;
        if (kind == JsonValueKind.Array)
        {
            foreach (var item in element.EnumerateArray())
            {
                Visit(item, new Frame { Parent = frame, Index = index, ChildIndex = index }, null, rootName);
                index++;
                if (Matches.Count >= MaxMatches)
                {
                    return;
                }
            }
        }
        else
        {
            foreach (var property in element.EnumerateObject())
            {
                Visit(property.Value, new Frame { Parent = frame, Key = property.Name, ChildIndex = index }, property.Name, rootName);
                index++;
                if (Matches.Count >= MaxMatches)
                {
                    return;
                }
            }
        }
    }
}
