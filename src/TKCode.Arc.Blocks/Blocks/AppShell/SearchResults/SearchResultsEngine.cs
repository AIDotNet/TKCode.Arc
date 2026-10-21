using Avalonia.Collections;

namespace TKCode.Arc;

/// <summary>A filter prepared for matching: the per-item value of a <see cref="SearchValueFilter"/> or day of a <see cref="SearchDateFilter"/>.</summary>
internal sealed class SearchFilterPlan
{
    public const int NoValue = -1;
    public const int NoDay = int.MinValue;

    public required SearchFilter Source { get; init; }

    public SearchValueFilter? Values { get; init; }

    public SearchDateFilter? Date { get; init; }

    public bool IsDate => Date is not null;

    /// <summary>Value filters: the distinct values (index = position in <see cref="SearchValueFilter.Values"/>).</summary>
    public Dictionary<string, int> ValueIndex { get; } = new(StringComparer.OrdinalIgnoreCase);

    public int ValueCount { get; set; }

    /// <summary>Per source item: the index of its value, or <see cref="NoValue"/>.</summary>
    public int[] ItemValue { get; set; } = [];

    /// <summary>Per value: ticked.</summary>
    public bool[] Selected { get; set; } = [];

    public int SelectedCount { get; set; }

    /// <summary>Per value: the results it would show (output of <see cref="SearchResultsEngine.RunFilters"/>).</summary>
    public int[] Counts { get; set; } = [];

    /// <summary>Date filters: per source item, the day (days since 0001-01-01) or <see cref="NoDay"/>.</summary>
    public int[] ItemDay { get; set; } = [];

    /// <summary>Date filters: per source item, whether the range concerns its kind.</summary>
    public bool[] ItemApplies { get; set; } = [];

    /// <summary>Date filters: the available range in days; usable when <see cref="MaxDay"/> is after <see cref="MinDay"/>.</summary>
    public int MinDay { get; set; }

    public int MaxDay { get; set; }

    public int StartDay { get; set; }

    public int EndDay { get; set; }

    public bool DateActive { get; set; }

    public bool HasRange => IsDate && MaxDay > MinDay;
}

/// <summary>
/// The search itself, free of any control: matching the query, narrowing by filters, counting the values of each
/// filter (the counts of a value ignore that filter's own ticks, as in faceted search), and sorting. A keystroke that only
/// appends to the query searches the previous matches instead of every item.
/// </summary>
internal sealed class SearchResultsEngine
{
    private static readonly Comparison<SearchResultsHit> s_byRank = static (a, b) => a.Tier != b.Tier
        ? a.Tier.CompareTo(b.Tier)
        : a.Order.CompareTo(b.Order);

    private static int s_warmed;

    /// <summary>
    /// The first culture-aware comparison of a process loads the collation data (on macOS the ICU tables take
    /// 100–900ms). Sorting A to Z would otherwise stall the UI thread on its first use, so the data is loaded once on a
    /// pool thread when the first search page is created.
    /// </summary>
    public static void WarmUpCollation()
    {
        if (Interlocked.Exchange(ref s_warmed, 1) == 0)
        {
            _ = Task.Run(static () => string.Compare("a", "b", StringComparison.CurrentCultureIgnoreCase));
        }
    }

    private readonly Comparison<SearchResultsHit> _byNewest;
    private readonly Comparison<SearchResultsHit> _byTitle;

    public SearchResultsEngine()
    {
        _byNewest = (a, b) =>
        {
            var byDate = _ticks[b.Order].CompareTo(_ticks[a.Order]);
            return byDate != 0 ? byDate : a.Order.CompareTo(b.Order);
        };
        _byTitle = (a, b) =>
        {
            var byTitle = string.Compare(_titles[a.Order], _titles[b.Order], StringComparison.CurrentCultureIgnoreCase);
            return byTitle != 0 ? byTitle : a.Order.CompareTo(b.Order);
        };
    }

    private List<SearchResultsHit> _queryHits = new();
    private List<SearchResultsHit> _scratch = new();
    private string? _lastQuery;
    private bool _lastFiltering;
    private IReadOnlyList<SearchResultItem>? _lastItems;
    private int _lastCount = -1;
    private int _version;
    private string[] _titles = [];
    private long[] _ticks = [];

    /// <summary>The filters, in order (at most 64).</summary>
    public List<SearchFilterPlan> Plans { get; } = new();

    /// <summary>Items that match the query (source order), before the filters.</summary>
    public IReadOnlyList<SearchResultsHit> QueryHits => _queryHits;

    /// <summary>Items that match the query and every filter, sorted.</summary>
    public List<SearchResultsHit> Hits { get; } = new();

    /// <summary>Number of items searched with the last query (diagnostics, tests).</summary>
    public int LastCandidateCount { get; private set; }

    /// <summary>Forgets what was searched: the items or the filters were replaced.</summary>
    public void Invalidate()
    {
        _version++;
        _lastQuery = null;
    }

    /// <summary>
    /// Prepares <paramref name="filters"/> for <paramref name="items"/>. A value filter without values lists the
    /// values it finds (written into <see cref="SearchValueFilter.Values"/>, in order of first appearance).
    /// </summary>
    public void Compile(IReadOnlyList<SearchResultItem> items, IReadOnlyList<SearchFilter> filters)
    {
        Plans.Clear();
        var count = items.Count;
        _titles = new string[count];
        _ticks = new long[count];
        for (var i = 0; i < count; i++)
        {
            var item = items[i];
            _titles[i] = item.Title;
            _ticks[i] = item.Date is { } date ? date.Date.Ticks : long.MinValue;
        }
        foreach (var filter in filters)
        {
            if (Plans.Count >= 64)
            {
                break;
            }
            switch (filter)
            {
                case SearchValueFilter values:
                    Plans.Add(CompileValues(values, items));
                    break;
                case SearchDateFilter date:
                    Plans.Add(CompileDate(date, items));
                    break;
            }
        }
        Invalidate();
    }

    private static SearchFilterPlan CompileValues(SearchValueFilter filter, IReadOnlyList<SearchResultItem> items)
    {
        var plan = new SearchFilterPlan { Source = filter, Values = filter };
        var key = filter.Key;
        // A filter that listed its own values keeps adding the ones new items bring.
        var auto = filter.Values.Count == 0 || filter.IsAutoPopulated;
        if (filter.Values.Count == 0)
        {
            filter.IsAutoPopulated = true;
        }
        for (var v = 0; v < filter.Values.Count; v++)
        {
            plan.ValueIndex.TryAdd(filter.Values[v].Value, v);
        }
        plan.ItemValue = new int[items.Count];
        for (var i = 0; i < items.Count; i++)
        {
            var raw = items[i].ValueOf(key);
            if (string.IsNullOrEmpty(raw))
            {
                plan.ItemValue[i] = SearchFilterPlan.NoValue;
                continue;
            }
            if (!plan.ValueIndex.TryGetValue(raw, out var index))
            {
                if (!auto)
                {
                    plan.ItemValue[i] = SearchFilterPlan.NoValue;
                    continue;
                }
                index = filter.Values.Count;
                filter.Values.Add(new SearchFilterValue(raw));
                plan.ValueIndex[raw] = index;
            }
            plan.ItemValue[i] = index;
        }
        plan.ValueCount = filter.Values.Count;
        plan.Selected = new bool[plan.ValueCount];
        plan.Counts = new int[plan.ValueCount];
        return plan;
    }

    private static SearchFilterPlan CompileDate(SearchDateFilter filter, IReadOnlyList<SearchResultItem> items)
    {
        var plan = new SearchFilterPlan { Source = filter, Date = filter };
        plan.ItemDay = new int[items.Count];
        plan.ItemApplies = new bool[items.Count];
        var min = int.MaxValue;
        var max = int.MinValue;
        for (var i = 0; i < items.Count; i++)
        {
            var item = items[i];
            plan.ItemApplies[i] = filter.Applies(item.Kind);
            if (item.Date is { } date)
            {
                var day = DayOf(date);
                plan.ItemDay[i] = day;
                if (plan.ItemApplies[i])
                {
                    min = Math.Min(min, day);
                    max = Math.Max(max, day);
                }
            }
            else
            {
                plan.ItemDay[i] = SearchFilterPlan.NoDay;
            }
        }
        plan.MinDay = filter.Minimum is { } lower ? DayOf(lower) : min == int.MaxValue ? 0 : min;
        plan.MaxDay = filter.Maximum is { } upper ? DayOf(upper) : max == int.MinValue ? 0 : max;
        return plan;
    }

    /// <summary>Days since 0001-01-01 of the date part of <paramref name="date"/>.</summary>
    public static int DayOf(DateTimeOffset date) => (int)(date.Date.Ticks / TimeSpan.TicksPerDay);

    /// <summary>The date of <paramref name="day"/> (days since 0001-01-01), at midnight local to <paramref name="offset"/>.</summary>
    public static DateTimeOffset DateOfDay(int day, TimeSpan offset) => new(new DateTime(day * TimeSpan.TicksPerDay), offset);

    /// <summary>Reads the ticked values and chosen ranges of the filters into the plans.</summary>
    public void ReadSelections()
    {
        foreach (var plan in Plans)
        {
            if (plan.Values is { } filter)
            {
                plan.SelectedCount = 0;
                var count = Math.Min(plan.Selected.Length, filter.Values.Count);
                for (var v = 0; v < plan.Selected.Length; v++)
                {
                    var on = v < count && filter.Values[v].IsSelected;
                    plan.Selected[v] = on;
                    if (on)
                    {
                        plan.SelectedCount++;
                    }
                }
            }
            else if (plan.Date is { } date)
            {
                plan.StartDay = date.Start is { } start ? Math.Max(plan.MinDay, DayOf(start)) : plan.MinDay;
                plan.EndDay = date.End is { } end ? Math.Min(plan.MaxDay, DayOf(end)) : plan.MaxDay;
                plan.DateActive = plan.HasRange && (plan.StartDay > plan.MinDay || plan.EndDay < plan.MaxDay);
            }
        }
    }

    /// <summary>
    /// Finds the items that match <paramref name="query"/> (every white-space separated word, anywhere in the searched
    /// text). With <paramref name="filtering"/> false every item is kept in source order (a server searched).
    /// </summary>
    public void RunQuery(IReadOnlyList<SearchResultItem> items, string query, List<string> terms, bool filtering)
    {
        SearchResultsMatcher.Tokenize(query, terms);
        if (string.Equals(_lastQuery, query, StringComparison.Ordinal) && _lastFiltering == filtering && ReferenceEquals(_lastItems, items) &&
            _lastCount == items.Count)
        {
            // The same search again (a filter was ticked, the page grew): the matches stand.
            return;
        }
        var refine = _lastQuery is { Length: > 0 } last && filtering && _lastFiltering && ReferenceEquals(_lastItems, items) &&
                     _lastCount == items.Count && query.StartsWith(last, StringComparison.Ordinal);
        _scratch.Clear();
        if (!filtering || terms.Count == 0)
        {
            LastCandidateCount = items.Count;
            for (var order = 0; order < items.Count; order++)
            {
                _scratch.Add(new SearchResultsHit(items[order], SearchResultsMatcher.TierTitleStart, order));
            }
        }
        else if (refine)
        {
            LastCandidateCount = _queryHits.Count;
            foreach (var hit in _queryHits)
            {
                if (SearchResultsMatcher.MatchesItem(hit.Item, terms))
                {
                    _scratch.Add(new SearchResultsHit(hit.Item, SearchResultsMatcher.Tier(hit.Item.Title, terms[0]), hit.Order));
                }
            }
        }
        else
        {
            LastCandidateCount = items.Count;
            for (var order = 0; order < items.Count; order++)
            {
                var item = items[order];
                if (SearchResultsMatcher.MatchesItem(item, terms))
                {
                    _scratch.Add(new SearchResultsHit(item, SearchResultsMatcher.Tier(item.Title, terms[0]), order));
                }
            }
        }
        (_queryHits, _scratch) = (_scratch, _queryHits);
        _lastQuery = query;
        _lastFiltering = filtering;
        _lastItems = items;
        _lastCount = items.Count;
    }

    /// <summary>
    /// Narrows <see cref="QueryHits"/> by the filters into <see cref="Hits"/>, sorted by <paramref name="sort"/>, and
    /// (when <paramref name="count"/>) fills the <see cref="SearchFilterPlan.Counts"/> of the value filters.
    /// </summary>
    public void RunFilters(bool applyFilters, bool count, SearchSortOrder sort, bool hasTerms)
    {
        Hits.Clear();
        foreach (var plan in Plans)
        {
            if (!plan.IsDate)
            {
                Array.Clear(plan.Counts);
            }
        }
        var plans = Plans;
        if (!applyFilters || plans.Count == 0)
        {
            Hits.AddRange(_queryHits);
        }
        else
        {
            foreach (var hit in _queryHits)
            {
                var i = hit.Order;
                ulong fail = 0;
                for (var p = 0; p < plans.Count; p++)
                {
                    var plan = plans[p];
                    bool pass;
                    if (plan.IsDate)
                    {
                        pass = !plan.DateActive || !plan.ItemApplies[i] ||
                               (plan.ItemDay[i] != SearchFilterPlan.NoDay && plan.ItemDay[i] >= plan.StartDay && plan.ItemDay[i] <= plan.EndDay);
                    }
                    else
                    {
                        pass = plan.SelectedCount == 0 || (plan.ItemValue[i] >= 0 && plan.Selected[plan.ItemValue[i]]);
                    }
                    if (!pass)
                    {
                        fail |= 1UL << p;
                    }
                }
                if (count && fail.PopCount() <= 1)
                {
                    for (var p = 0; p < plans.Count; p++)
                    {
                        var plan = plans[p];
                        if (plan.IsDate || plan.ItemValue[i] < 0)
                        {
                            continue;
                        }
                        // Everything passes, or only this filter's own ticks exclude the item.
                        if (fail == 0 || fail == 1UL << p)
                        {
                            plan.Counts[plan.ItemValue[i]]++;
                        }
                    }
                }
                if (fail == 0)
                {
                    Hits.Add(hit);
                }
            }
        }
        if (applyFilters)
        {
            // A server already ordered what it sent.
            Sort(sort, hasTerms);
        }
    }

    private void Sort(SearchSortOrder sort, bool hasTerms)
    {
        if (Hits.Count < 2)
        {
            return;
        }
        switch (sort)
        {
            case SearchSortOrder.BestMatch:
                if (hasTerms)
                {
                    Hits.Sort(s_byRank);
                }
                break;
            case SearchSortOrder.Newest:
                Hits.Sort(_byNewest);
                break;
            case SearchSortOrder.Title:
                Hits.Sort(_byTitle);
                break;
        }
    }
}

internal static class SearchResultsBits
{
    /// <summary>Number of set bits.</summary>
    public static int PopCount(this ulong value) => System.Numerics.BitOperations.PopCount(value);
}
