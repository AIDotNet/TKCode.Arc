// Ported from vscode (src/vs/editor/contrib/codelens/browser/codeLensCache.ts, commit b57d5c9e), MIT license.
// Differences from upstream:
//  - not a singleton service: an instance per editor by default, which a host may share between editors;
//  - there is no storage service: Serialize and Deserialize give and take the cache as JSON (upstream's codelens/cache2
//    value), for a host that keeps it across sessions; written and read with Utf8JsonWriter / JsonDocument (no
//    reflection);
//  - LRUCache(20, 0.75) is a small linked list with a map: over 20 entries it trims to the 15 most recently used.

using System.Text;
using System.Text.Json;
using TKCode.Arc.Editor.Languages;
using TKCode.Arc.Editor.Model;

namespace TKCode.Arc.Editor.Contrib.CodeLens;

/// <summary>
/// The last code lenses of recently shown models, with the titles of their commands but not the commands, so that an
/// editor can show the lens rows at once while the providers are asked again (upstream's CodeLensCache).
/// </summary>
internal sealed class CodeLensCache
{
    private const int Limit = 20;
    private const double Ratio = 0.75;

    private readonly ICodeLensProvider _fakeProvider = new FakeProvider();
    private readonly Dictionary<string, LinkedListNode<(string Key, CacheItem Item)>> _map = new(StringComparer.Ordinal);
    private readonly LinkedList<(string Key, CacheItem Item)> _order = new();

    /// <summary>The number of cached models.</summary>
    public int Count => _map.Count;

    /// <summary>Caches a copy of <paramref name="data"/> for <paramref name="model"/>: the ranges and command titles, without command ids.</summary>
    public void Put(TextModel model, CodeLensModel data)
    {
        // a copy of the model without command ids but with the command labels
        var copyItems = data.Lenses.Select(item => new Languages.CodeLens(item.Symbol.Range)
        {
            Command = item.Symbol.Command is { } command ? new LanguageCommand(string.Empty, command.Title) : null,
        }).ToList();
        var copyModel = new CodeLensModel();
        copyModel.Add(new CodeLensList(copyItems), _fakeProvider);

        Set(model.Uri.ToString(), new CacheItem(model.GetLineCount(), copyModel));
    }

    /// <summary>The cached lenses of <paramref name="model"/>, when it still has the line count they were cached with.</summary>
    public CodeLensModel? Get(TextModel model)
    {
        if (!_map.TryGetValue(model.Uri.ToString(), out var node))
        {
            return null;
        }

        // touch: most recently used
        _order.Remove(node);
        _order.AddLast(node);
        var item = node.Value.Item;
        return item.LineCount == model.GetLineCount() ? item.Data : null;
    }

    public void Delete(TextModel model)
    {
        var key = model.Uri.ToString();
        if (_map.Remove(key, out var node))
        {
            _order.Remove(node);
        }
    }

    /// <summary>The cache as JSON: for each model URI its line count and the lines that have lenses.</summary>
    public string Serialize()
    {
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream))
        {
            writer.WriteStartObject();
            foreach (var (key, value) in _order)
            {
                writer.WriteStartObject(key);
                writer.WriteNumber("lineCount", value.LineCount);
                writer.WriteStartArray("lines");
                foreach (var line in value.Data.Lenses.Select(static d => d.Symbol.Range.StartLineNumber).Distinct())
                {
                    writer.WriteNumberValue(line);
                }

                writer.WriteEndArray();
                writer.WriteEndObject();
            }

            writer.WriteEndObject();
        }

        return Encoding.UTF8.GetString(stream.ToArray());
    }

    /// <summary>Restores what <see cref="Serialize"/> wrote: a placeholder lens on each cached line; bad data is ignored.</summary>
    public void Deserialize(string raw)
    {
        try
        {
            using var document = JsonDocument.Parse(raw);
            foreach (var property in document.RootElement.EnumerateObject())
            {
                var element = property.Value;
                var lenses = new List<Languages.CodeLens>();
                foreach (var line in element.GetProperty("lines").EnumerateArray())
                {
                    var lineNumber = line.GetInt32();
                    lenses.Add(new Languages.CodeLens(new TextRange(lineNumber, 1, lineNumber, 11)));
                }

                var model = new CodeLensModel();
                model.Add(new CodeLensList(lenses), _fakeProvider);
                Set(property.Name, new CacheItem(element.GetProperty("lineCount").GetInt32(), model));
            }
        }
        catch (Exception error) when (error is JsonException or InvalidOperationException or KeyNotFoundException or FormatException)
        {
            // ignore
        }
    }

    private void Set(string key, CacheItem item)
    {
        if (_map.Remove(key, out var existing))
        {
            _order.Remove(existing);
        }

        _map[key] = _order.AddLast((key, item));
        if (_map.Count > Limit)
        {
            // trim to the most recently used
            var keep = (int)Math.Round(Limit * Ratio);
            while (_map.Count > keep && _order.First is { } oldest)
            {
                _order.RemoveFirst();
                _map.Remove(oldest.Value.Key);
            }
        }
    }

    private sealed record CacheItem(int LineCount, CodeLensModel Data);

    private sealed class FakeProvider : ICodeLensProvider
    {
        public Task<CodeLensList?> ProvideCodeLenses(TextModel model, CancellationToken token) =>
            throw new NotSupportedException("not supported");
    }
}
