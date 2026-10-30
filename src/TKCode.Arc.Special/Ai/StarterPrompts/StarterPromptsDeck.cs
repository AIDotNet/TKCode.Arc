namespace TKCode.Arc;

/// <summary>
/// The pure logic behind <see cref="StarterPrompts"/>: which tabs show, which starters each tab holds and which of them are
/// on screen, and what a shuffle brings next. A tab starts on its first page in the given order; a shuffle moves to the
/// next unseen page of a shuffled order, and when the pool runs out it reshuffles with the cards on screen placed last, so
/// a shuffle always shows something new while the pool holds more than one page.
/// </summary>
internal sealed class StarterPromptsDeck
{
    /// <summary>The id of the All tab.</summary>
    public const string AllId = "all";

    private readonly Random _random;
    private readonly Dictionary<string, Pool> _pools = new(StringComparer.Ordinal);
    private IReadOnlyList<StarterPrompt> _prompts = [];
    private IReadOnlyList<StarterPromptCategory> _categories = [];
    private List<StarterPromptCategory> _tabs = [];

    public StarterPromptsDeck(int? seed = null)
    {
        _random = seed is { } s ? new Random(s) : new Random();
    }

    /// <summary>The tabs after All, in order: the categories that hold at least one starter.</summary>
    public IReadOnlyList<StarterPromptCategory> Tabs => _tabs;

    /// <summary>Replaces the starters and the categories; every tab goes back to its first page.</summary>
    public void Reset(IReadOnlyList<StarterPrompt> prompts, IReadOnlyList<StarterPromptCategory> categories)
    {
        _prompts = prompts;
        _categories = categories;
        _pools.Clear();
        _tabs = [.. categories.Where(c => c.Id != AllId && prompts.Any(p => p.Category == c.Id))];
    }

    /// <summary>Whether <paramref name="id"/> is a tab on screen (All always is).</summary>
    public bool HasTab(string? id) => id == AllId || _tabs.Any(t => t.Id == id);

    /// <summary>The category a starter belongs to, or null when no tab holds it.</summary>
    public StarterPromptCategory? CategoryOf(StarterPrompt prompt) => _categories.FirstOrDefault(c => c.Id == prompt.Category);

    /// <summary>
    /// The starters of a tab in their first order. All interleaves the categories (one of each in turn) so its first page
    /// shows the range; starters in no category follow.
    /// </summary>
    public IReadOnlyList<StarterPrompt> PoolOf(string tab) => GetPool(tab).Items;

    /// <summary>The starters on screen for a tab: up to <paramref name="count"/> of them.</summary>
    public IReadOnlyList<StarterPrompt> Page(string tab, int count)
    {
        var pool = GetPool(tab);
        var take = Math.Min(Math.Max(0, count), pool.Order.Count);
        var page = new List<StarterPrompt>(take);
        for (var i = 0; i < take; i++)
        {
            page.Add(pool.Order[(pool.Cursor + i) % pool.Order.Count]);
        }
        return page;
    }

    /// <summary>Whether a shuffle of the tab can bring a card that is not on screen.</summary>
    public bool CanShuffle(string tab, int count) => GetPool(tab).Items.Count > Math.Max(0, count);

    /// <summary>Moves the tab to its next page and returns it. Does nothing when <see cref="CanShuffle"/> is false.</summary>
    public IReadOnlyList<StarterPrompt> Shuffle(string tab, int count)
    {
        var pool = GetPool(tab);
        if (!CanShuffle(tab, count))
        {
            return Page(tab, count);
        }
        if (!pool.Shuffled || pool.Cursor + 2 * count > pool.Order.Count)
        {
            // A fresh order: the unseen starters first in random order, the ones on screen last.
            var onScreen = new HashSet<StarterPrompt>(Page(tab, count));
            var rest = pool.Items.Where(p => !onScreen.Contains(p)).ToList();
            Mix(rest);
            var shown = onScreen.ToList();
            Mix(shown);
            pool.Order = [.. rest, .. shown];
            pool.Cursor = 0;
            pool.Shuffled = true;
        }
        else
        {
            pool.Cursor += count;
        }
        return Page(tab, count);
    }

    private void Mix(List<StarterPrompt> list)
    {
        for (var i = list.Count - 1; i > 0; i--)
        {
            var j = _random.Next(i + 1);
            (list[i], list[j]) = (list[j], list[i]);
        }
    }

    private Pool GetPool(string tab)
    {
        if (_pools.TryGetValue(tab, out var pool))
        {
            return pool;
        }
        List<StarterPrompt> items;
        if (tab == AllId)
        {
            items = Interleave();
        }
        else
        {
            items = [.. _prompts.Where(p => p.Category == tab)];
        }
        pool = new Pool(items);
        _pools[tab] = pool;
        return pool;
    }

    private List<StarterPrompt> Interleave()
    {
        var queues = _tabs.Select(t => new Queue<StarterPrompt>(_prompts.Where(p => p.Category == t.Id))).ToList();
        var result = new List<StarterPrompt>(_prompts.Count);
        var any = true;
        while (any)
        {
            any = false;
            foreach (var queue in queues)
            {
                if (queue.Count > 0)
                {
                    result.Add(queue.Dequeue());
                    any = true;
                }
            }
        }
        var placed = new HashSet<StarterPrompt>(result);
        result.AddRange(_prompts.Where(p => !placed.Contains(p)));
        return result;
    }

    private sealed class Pool(List<StarterPrompt> items)
    {
        public List<StarterPrompt> Items { get; } = items;

        public List<StarterPrompt> Order { get; set; } = [.. items];

        public int Cursor { get; set; }

        public bool Shuffled { get; set; }
    }
}
