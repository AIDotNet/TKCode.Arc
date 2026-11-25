// Ported from vscode (src/vs/editor/contrib/suggest/browser/suggestMemory.ts, commit b57d5c9e), MIT license.
// Differences from upstream:
//  - memories live in memory only: there is no storage service, so toJSON / fromJSON and the persisting scheduler are
//    left out, and the service is an instance (one per editor) instead of a process-wide singleton;
//  - the mode comes from SuggestOptions.SuggestSelection, passed with each call (upstream reads the configuration);
//  - LRUCache(300, 0.66) is a small LRU list here (MemoryLru); TernarySearchTree.forStrings with findSubstr is a
//    dictionary searched by ever shorter prefixes of the key;
//  - the word of PrefixMemory uses the model's language's word definition when a LanguageConfigurationService is given.

using TKCode.Arc.Editor.Languages;
using TKCode.Arc.Editor.Model;

namespace TKCode.Arc.Editor.Contrib.Suggest;

/// <summary>A remembered completion (upstream's MemItem).</summary>
internal sealed class MemItem
{
    public CompletionItemKind Type { get; set; }

    public string InsertText { get; set; } = string.Empty;

    public int Touch { get; set; }
}

/// <summary>Picks the item to select from what was chosen before (upstream's Memory).</summary>
internal abstract class Memory(SuggestSelection name)
{
    public SuggestSelection Name { get; } = name;

    /// <summary>The index of the item to select: the first preselected item among the top scores, else 0.</summary>
    public virtual int Select(TextModel model, TextPosition pos, IReadOnlyList<SuggestCompletionItem> items)
    {
        if (items.Count == 0)
        {
            return 0;
        }
        var topScore = items[0].Score.Score;
        for (var i = 0; i < items.Count; i++)
        {
            var score = items[i].Score;
            var suggestion = items[i].Completion;
            if (score.Score != topScore)
            {
                // stop when leaving the group of top matches
                break;
            }
            if (suggestion.Preselect)
            {
                // stop when seeing an auto-select-item
                return i;
            }
        }
        return 0;
    }

    /// <summary>Remembers that <paramref name="item"/> was accepted at <paramref name="pos"/>.</summary>
    public abstract void Memorize(TextModel model, TextPosition pos, SuggestCompletionItem item);
}

/// <summary>Remembers nothing (upstream's NoMemory, mode "first").</summary>
internal sealed class NoMemory() : Memory(SuggestSelection.First)
{
    public override void Memorize(TextModel model, TextPosition pos, SuggestCompletionItem item)
    {
        // no-op
    }
}

/// <summary>Selects the most recently used item among the top matches (upstream's LRUMemory, mode "recentlyUsed").</summary>
internal sealed class LRUMemory() : Memory(SuggestSelection.RecentlyUsed)
{
    private readonly MemoryLru _cache = new(300, 0.66);
    private int _seq;

    public override void Memorize(TextModel model, TextPosition pos, SuggestCompletionItem item)
    {
        var key = $"{model.GetLanguageId()}/{item.TextLabel}";
        _cache.Set(key, new MemItem
        {
            Touch = _seq++,
            Type = item.Completion.Kind,
            InsertText = item.Completion.InsertText,
        });
    }

    public override int Select(TextModel model, TextPosition pos, IReadOnlyList<SuggestCompletionItem> items)
    {
        if (items.Count == 0)
        {
            return 0;
        }

        var lineSuffix = JsSubstr(model.GetLineContent(pos.LineNumber), pos.Column - 10, pos.Column - 1);
        if (lineSuffix.Length > 0 && char.IsWhiteSpace(lineSuffix[^1]))
        {
            return base.Select(model, pos, items);
        }

        var topScore = items[0].Score.Score;
        var indexPreselect = -1;
        var indexRecency = -1;
        var seq = -1;
        for (var i = 0; i < items.Count; i++)
        {
            if (items[i].Score.Score != topScore)
            {
                // consider only top items
                break;
            }
            var key = $"{model.GetLanguageId()}/{items[i].TextLabel}";
            var item = _cache.Peek(key);
            if (item is not null && item.Touch > seq && item.Type == items[i].Completion.Kind && item.InsertText == items[i].Completion.InsertText)
            {
                seq = item.Touch;
                indexRecency = i;
            }
            if (items[i].Completion.Preselect && indexPreselect == -1)
            {
                // stop when seeing an auto-select-item
                return i;
            }
        }
        if (indexRecency != -1)
        {
            return indexRecency;
        }
        if (indexPreselect != -1)
        {
            return indexPreselect;
        }
        return 0;
    }

    // JavaScript's String.prototype.substr(start, length): a negative start counts from the end
    private static string JsSubstr(string s, int start, int length)
    {
        if (start < 0)
        {
            start = Math.Max(s.Length + start, 0);
        }
        if (start >= s.Length || length <= 0)
        {
            return string.Empty;
        }
        return s.Substring(start, Math.Min(length, s.Length - start));
    }
}

/// <summary>Selects the item last accepted after the same prefix (upstream's PrefixMemory, mode "recentlyUsedByPrefix").</summary>
internal sealed class PrefixMemory(LanguageConfigurationService? languageConfigurationService = null) : Memory(SuggestSelection.RecentlyUsedByPrefix)
{
    private readonly Dictionary<string, MemItem> _trie = new(StringComparer.Ordinal);
    private int _seq;

    public override void Memorize(TextModel model, TextPosition pos, SuggestCompletionItem item)
    {
        var word = SuggestWords.GetWordUntilPosition(model, pos, languageConfigurationService).Word;
        var key = $"{model.GetLanguageId()}/{word}";
        _trie[key] = new MemItem
        {
            Type = item.Completion.Kind,
            InsertText = item.Completion.InsertText,
            Touch = _seq++,
        };
    }

    public override int Select(TextModel model, TextPosition pos, IReadOnlyList<SuggestCompletionItem> items)
    {
        var word = SuggestWords.GetWordUntilPosition(model, pos, languageConfigurationService).Word;
        if (word.Length == 0)
        {
            return base.Select(model, pos, items);
        }
        var key = $"{model.GetLanguageId()}/{word}";
        if (!_trie.TryGetValue(key, out var item))
        {
            item = FindSubstr(key);
        }
        if (item is not null)
        {
            for (var i = 0; i < items.Count; i++)
            {
                var completion = items[i].Completion;
                if (completion.Kind == item.Type && completion.InsertText == item.InsertText)
                {
                    return i;
                }
            }
        }
        return base.Select(model, pos, items);
    }

    // TernarySearchTree.findSubstr: the value of the longest key that is a prefix of the given one
    private MemItem? FindSubstr(string key)
    {
        for (var length = key.Length - 1; length > 0; length--)
        {
            if (_trie.TryGetValue(key[..length], out var item))
            {
                return item;
            }
        }
        return null;
    }
}

/// <summary>The memory of accepted completions of one editor (upstream's SuggestMemoryService, in memory only).</summary>
internal sealed class SuggestMemoryService(LanguageConfigurationService? languageConfigurationService = null)
{
    private Memory? _strategy;

    /// <summary>Remembers that <paramref name="item"/> was accepted.</summary>
    public void Memorize(TextModel model, TextPosition pos, SuggestCompletionItem item, SuggestSelection mode) =>
        WithStrategy(mode).Memorize(model, pos, item);

    /// <summary>The index of the item to select when the list shows.</summary>
    public int Select(TextModel model, TextPosition pos, IReadOnlyList<SuggestCompletionItem> items, SuggestSelection mode) =>
        WithStrategy(mode).Select(model, pos, items);

    private Memory WithStrategy(SuggestSelection mode)
    {
        if (_strategy?.Name != mode)
        {
            _strategy = mode switch
            {
                SuggestSelection.RecentlyUsedByPrefix => new PrefixMemory(languageConfigurationService),
                SuggestSelection.RecentlyUsed => new LRUMemory(),
                _ => new NoMemory(),
            };
        }
        return _strategy;
    }
}

/// <summary>Upstream's LRUCache(limit, ratio): when it holds more than the limit, it keeps the newest limit × ratio entries.</summary>
internal sealed class MemoryLru(int limit, double ratio)
{
    private readonly Dictionary<string, LinkedListNode<(string Key, MemItem Value)>> _map = new(StringComparer.Ordinal);
    private readonly LinkedList<(string Key, MemItem Value)> _list = new();

    public int Count => _map.Count;

    /// <summary>The value without touching it.</summary>
    public MemItem? Peek(string key) => _map.TryGetValue(key, out var node) ? node.Value.Value : null;

    public void Set(string key, MemItem value)
    {
        if (_map.TryGetValue(key, out var node))
        {
            _list.Remove(node);
        }
        _map[key] = _list.AddLast((key, value));
        if (_map.Count > limit)
        {
            var keep = (int)Math.Round(limit * ratio);
            while (_map.Count > keep && _list.First is { } oldest)
            {
                _list.RemoveFirst();
                _map.Remove(oldest.Value.Key);
            }
        }
    }
}
