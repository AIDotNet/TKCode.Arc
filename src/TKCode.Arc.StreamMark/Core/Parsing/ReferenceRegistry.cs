using System.Collections.Immutable;
using StreamMark.Core.Model;

namespace StreamMark.Core.Parsing;

/// <summary>链接引用定义（<c>[label]: url "title"</c>）。</summary>
internal readonly record struct LinkReference(string Url, string? Title);

/// <summary>脚注定义条目（文档级汇总，生成文末 <see cref="FootnotesBlock"/>）。</summary>
internal sealed class FootnoteEntry(string rawLabel, string normalizedLabel, int order)
{
    public string RawLabel { get; } = rawLabel;

    public string NormalizedLabel { get; } = normalizedLabel;

    public int Order { get; set; } = order;

    public SourceRange Range { get; set; }

    public ImmutableArray<MarkdownBlock> Children { get; set; } = [];
}

/// <summary>
/// 文档级的引用、脚注与标题 Slug 注册表：
/// 记录已定义的链接引用与脚注，并维护「标签 → 依赖该未解析标签的顶层块 key」反向索引；
/// 当新定义到达时只需定向重算受影响块的行内内容，避免 <c>Complete()</c> 整篇重解析。
/// </summary>
internal sealed class ReferenceRegistry
{
    private readonly Dictionary<string, LinkReference> _links = new(StringComparer.Ordinal);
    private readonly List<string> _linkKeyHistory = [];
    private readonly Dictionary<string, FootnoteEntry> _footnotes = new(StringComparer.Ordinal);
    private readonly List<FootnoteEntry> _footnoteOrder = [];
    private readonly Dictionary<string, HashSet<int>> _unresolvedLinks = new(StringComparer.Ordinal);
    private readonly Dictionary<string, HashSet<int>> _unresolvedFootnotes = new(StringComparer.Ordinal);
    private readonly Dictionary<string, int> _headingSlugs = new(StringComparer.Ordinal);
    private FootnotesBlock? _cachedFootnotes;
    private int _nextFootnoteOrder = 1;

    public readonly record struct Checkpoint(int LinkCount, int FootnoteCount, int NextFootnoteOrder);

    public Checkpoint Save() => new(_linkKeyHistory.Count, _footnoteOrder.Count, _nextFootnoteOrder);

    public void Restore(Checkpoint cp)
    {
        while (_linkKeyHistory.Count > cp.LinkCount)
        {
            var key = _linkKeyHistory[^1];
            _linkKeyHistory.RemoveAt(_linkKeyHistory.Count - 1);
            _links.Remove(key);
        }
        while (_footnoteOrder.Count > cp.FootnoteCount)
        {
            var entry = _footnoteOrder[^1];
            _footnoteOrder.RemoveAt(_footnoteOrder.Count - 1);
            _footnotes.Remove(entry.NormalizedLabel);
            FootnoteVersion++;
            _cachedFootnotes = null;
        }
        _nextFootnoteOrder = cp.NextFootnoteOrder;
    }

    public bool HasLinkDefinitions => _links.Count > 0;

    public bool HasFootnotes => _footnoteOrder.Count > 0;

    public int FootnoteVersion { get; private set; }

    public void Clear()
    {
        _links.Clear();
        _linkKeyHistory.Clear();
        _footnotes.Clear();
        _footnoteOrder.Clear();
        _unresolvedLinks.Clear();
        _unresolvedFootnotes.Clear();
        _headingSlugs.Clear();
        _cachedFootnotes = null;
        _nextFootnoteOrder = 1;
        FootnoteVersion = 0;
    }

    /// <summary>登记一条链接引用定义（CommonMark：先定义者优先）。若命中了早先未解析的引用，把受影响的顶层块 key 加入 <paramref name="affectedTopKeys"/>。</summary>
    public bool AddLinkDefinition(string normalizedLabel, string url, string? title, HashSet<int>? affectedTopKeys)
    {
        if (normalizedLabel.Length == 0 || _links.ContainsKey(normalizedLabel))
        {
            return false;
        }
        _links[normalizedLabel] = new LinkReference(url, string.IsNullOrEmpty(title) ? null : title);
        _linkKeyHistory.Add(normalizedLabel);
        if (affectedTopKeys is not null && _unresolvedLinks.Remove(normalizedLabel, out var dependents))
        {
            foreach (var key in dependents)
            {
                affectedTopKeys.Add(key);
            }
        }
        return true;
    }

    public bool TryGetLink(string normalizedLabel, out LinkReference reference) =>
        _links.TryGetValue(normalizedLabel, out reference);

    /// <summary>记录顶层块 <paramref name="topKey"/> 在行内解析时遇到了尚未定义的链接引用标签。</summary>
    public void RecordUnresolvedLink(string normalizedLabel, int topKey)
    {
        if (normalizedLabel.Length == 0 || topKey < 0)
        {
            return;
        }
        if (!_unresolvedLinks.TryGetValue(normalizedLabel, out var set))
        {
            _unresolvedLinks[normalizedLabel] = set = [];
        }
        set.Add(topKey);
    }

    /// <summary>登记或更新一条脚注定义。</summary>
    public FootnoteEntry UpsertFootnote(string rawLabel, SourceRange range, ImmutableArray<MarkdownBlock> children, HashSet<int>? affectedTopKeys)
    {
        var normalized = UnicodeClassification.NormalizeLabel(rawLabel.AsSpan());
        var added = false;
        if (!_footnotes.TryGetValue(normalized, out var entry))
        {
            entry = new FootnoteEntry(rawLabel, normalized, _nextFootnoteOrder++);
            _footnotes[normalized] = entry;
            _footnoteOrder.Add(entry);
            added = true;
            if (affectedTopKeys is not null && _unresolvedFootnotes.Remove(normalized, out var dependents))
            {
                foreach (var key in dependents)
                {
                    affectedTopKeys.Add(key);
                }
            }
        }
        if (added || entry.Range != range || !SameBlocks(entry.Children, children))
        {
            entry.Range = range;
            entry.Children = children;
            FootnoteVersion++;
            _cachedFootnotes = null;
        }
        return entry;
    }

    private static bool SameBlocks(ImmutableArray<MarkdownBlock> a, ImmutableArray<MarkdownBlock> b)
    {
        if (a == b)
        {
            return true;
        }
        if (a.IsDefault || b.IsDefault || a.Length != b.Length)
        {
            return false;
        }
        for (var i = 0; i < a.Length; i++)
        {
            if (!ReferenceEquals(a[i], b[i]))
            {
                return false;
            }
        }
        return true;
    }

    public bool TryGetFootnote(string normalizedLabel, out FootnoteEntry entry) =>
        _footnotes.TryGetValue(normalizedLabel, out entry!);

    public void RecordUnresolvedFootnote(string normalizedLabel, int topKey)
    {
        if (normalizedLabel.Length == 0 || topKey < 0)
        {
            return;
        }
        if (!_unresolvedFootnotes.TryGetValue(normalizedLabel, out var set))
        {
            _unresolvedFootnotes[normalizedLabel] = set = [];
        }
        set.Add(topKey);
    }

    /// <summary>按引用/定义顺序构建文末 <see cref="FootnotesBlock"/>（若无脚注返回 null）。</summary>
    public FootnotesBlock? BuildFootnotesBlock()
    {
        if (_footnoteOrder.Count == 0)
        {
            return null;
        }
        if (_cachedFootnotes is not null)
        {
            return _cachedFootnotes;
        }
        var builder = ImmutableArray.CreateBuilder<FootnoteDefinition>(_footnoteOrder.Count);
        var minStart = int.MaxValue;
        var maxEnd = 0;
        for (var i = 0; i < _footnoteOrder.Count; i++)
        {
            var entry = _footnoteOrder[i];
            if (entry.Range.Start < minStart)
            {
                minStart = entry.Range.Start;
            }
            if (entry.Range.End > maxEnd)
            {
                maxEnd = entry.Range.End;
            }
            // 与 Markdig 保持兼容：标签带 "^"（如 "^1"），链接为 "#fn:^1"
            var label = entry.RawLabel.StartsWith('^') ? entry.RawLabel : "^" + entry.RawLabel;
            builder.Add(new FootnoteDefinition(label, entry.Order, entry.Children));
        }
        var range = minStart == int.MaxValue ? default : new SourceRange(minStart, Math.Max(0, maxEnd - minStart));
        _cachedFootnotes = new FootnotesBlock(builder.MoveToImmutable()) { Range = range };
        return _cachedFootnotes;
    }

    /// <summary>为标题分配文档内唯一的 GitHub 风格锚点 Id。</summary>
    public string? AllocateHeadingId(ReadOnlySpan<char> plainText)
    {
        var baseSlug = UnicodeClassification.ToGitHubSlug(plainText);
        if (baseSlug.Length == 0)
        {
            return null;
        }
        if (!_headingSlugs.TryGetValue(baseSlug, out var count))
        {
            _headingSlugs[baseSlug] = 1;
            return baseSlug;
        }
        while (true)
        {
            var candidate = $"{baseSlug}-{count}";
            count++;
            if (!_headingSlugs.ContainsKey(candidate))
            {
                _headingSlugs[baseSlug] = count;
                _headingSlugs[candidate] = 1;
                return candidate;
            }
        }
    }

    /// <summary>标题 Slug 计数快照（用于回滚末尾半行推测标题时防止重复计数）。</summary>
    public void ResetHeadingSlugs() => _headingSlugs.Clear();
}
