using System.Globalization;

namespace TKCode.Arc;

/// <summary>A group row: a column value shared by its rows, with nested groups or rows below it.</summary>
internal sealed class DataGridGroupNode
{
    public required string Id { get; init; }

    public required string ColumnKey { get; init; }

    public required int Accessor { get; init; }

    public object? Value { get; init; }

    public int Level { get; init; }

    /// <summary>Source indices of every row in the group, in view order.</summary>
    public required int[] Rows { get; init; }

    public DataGridGroupNode[]? Children { get; set; }

    /// <summary>Subtotals by accessor index, computed when the group row is drawn; cleared by edits.</summary>
    public Dictionary<int, decimal?>? Subtotals { get; set; }

    public int SubtotalVersion { get; set; } = -1;
}

/// <summary>One drawn line of the body: a data row (its display index) or a group row.</summary>
internal readonly record struct DataGridLine(int Row, DataGridGroupNode? Group)
{
    public bool IsGroup => Group is not null;
}

/// <summary>What the view is built from.</summary>
internal sealed class DataGridViewSpec
{
    public required IReadOnlyList<DataGridSort> Sort { get; init; }

    public required IReadOnlyDictionary<string, DataGridFilter> Filters { get; init; }

    public required string Search { get; init; }

    /// <summary>Accessor indices of the visible columns (search scope).</summary>
    public required int[] VisibleAccessors { get; init; }

    public required IReadOnlyList<string> GroupBy { get; init; }

    public required CultureInfo Culture { get; init; }

    public required Func<int, IReadOnlyList<string>> OptionsOf { get; init; }

    public required Func<int, DataGridCellFormat> FormatOf { get; init; }
}

/// <summary>
/// The rows a grid shows: filtered, sorted and grouped source indices, the data rows outside collapsed groups (the
/// selection's row space) and the drawn lines (group rows plus data rows). Rebuilt only when the sort, filters, search,
/// visible columns, grouping or the row set change; edits never rebuild it, so rows stay where they are.
/// </summary>
internal sealed class DataGridView
{
    public static readonly DataGridView Empty = new();

    /// <summary>Every row that passes the filters, in view order (collapsed groups included).</summary>
    public int[] Filtered { get; private set; } = [];

    /// <summary>Data rows outside collapsed groups, in view order: display index → source index.</summary>
    public int[] Display { get; private set; } = [];

    public DataGridLine[] Lines { get; private set; } = [];

    /// <summary>Display index → line index.</summary>
    public int[] LineOfRow { get; private set; } = [];

    /// <summary>Line index → nearest display index (a group row maps to the row after it).</summary>
    public int[] RowOfLine { get; private set; } = [];

    /// <summary>Source index → display index (−1 when hidden).</summary>
    public int[] DisplayOfSource { get; private set; } = [];

    public DataGridGroupNode[]? Groups { get; private set; }

    public bool IsGrouped => Groups is not null;

    public int StoreVersion { get; private set; } = -1;

    /// <summary>Builds the filtered, sorted and grouped rows.</summary>
    public static DataGridView Build(DataGridStore store, DataGridViewSpec spec, IReadOnlySet<string> collapsed)
    {
        var view = new DataGridView { StoreVersion = store.Version };
        var matcher = CreateMatcher(store, spec);
        var rows = new List<int>(store.Count);
        for (var i = 0; i < store.Count; i++)
        {
            if (matcher is null || matcher(i))
            {
                rows.Add(i);
            }
        }
        var sorted = rows.ToArray();
        var keys = spec.Sort.Where(s => store.AccessorIndex(s.Key) >= 0).ToList();
        if (keys.Count > 0)
        {
            var values = new object?[keys.Count][];
            var orders = new IReadOnlyDictionary<string, int>?[keys.Count];
            for (var k = 0; k < keys.Count; k++)
            {
                var accessor = store.AccessorIndex(keys[k].Key);
                values[k] = store.Column(accessor);
                orders[k] = store.Accessors[accessor].Type == DataGridColumnType.Select
                    ? DataGridSorting.OptionOrder(spec.OptionsOf(accessor))
                    : null;
            }
            DataGridSorting.SortIndices(sorted, values, keys, orders, spec.Culture);
        }
        var groupAccessors = spec.GroupBy.Select(store.AccessorIndex).Where(a => a >= 0).Distinct().ToArray();
        if (groupAccessors.Length > 0)
        {
            view.Groups = BuildGroups(store, spec, sorted, groupAccessors, 0, string.Empty);
            view.Filtered = view.Groups.SelectMany(Leaves).ToArray();
        }
        else
        {
            view.Filtered = sorted;
        }
        view.Expand(store.Count, collapsed);
        return view;
    }

    /// <summary>Recomputes the display rows and lines after groups were collapsed or expanded.</summary>
    public DataGridView WithCollapsed(int sourceCount, IReadOnlySet<string> collapsed)
    {
        var view = new DataGridView
        {
            StoreVersion = StoreVersion,
            Filtered = Filtered,
            Groups = Groups
        };
        view.Expand(sourceCount, collapsed);
        return view;
    }

    private void Expand(int sourceCount, IReadOnlySet<string> collapsed)
    {
        var display = new List<int>(Filtered.Length);
        var lines = new List<DataGridLine>(Filtered.Length);
        var lineOfRow = new List<int>(Filtered.Length);
        if (Groups is null)
        {
            foreach (var row in Filtered)
            {
                lineOfRow.Add(lines.Count);
                lines.Add(new DataGridLine(display.Count, null));
                display.Add(row);
            }
        }
        else
        {
            void Visit(DataGridGroupNode node)
            {
                lines.Add(new DataGridLine(display.Count, node));
                if (collapsed.Contains(node.Id))
                {
                    return;
                }
                if (node.Children is { } children)
                {
                    foreach (var child in children)
                    {
                        Visit(child);
                    }
                    return;
                }
                foreach (var row in node.Rows)
                {
                    lineOfRow.Add(lines.Count);
                    lines.Add(new DataGridLine(display.Count, null));
                    display.Add(row);
                }
            }
            foreach (var group in Groups)
            {
                Visit(group);
            }
        }
        Display = display.ToArray();
        Lines = lines.ToArray();
        LineOfRow = lineOfRow.ToArray();
        var last = Math.Max(0, Display.Length - 1);
        RowOfLine = Lines.Select(l => Math.Min(l.Row, last)).ToArray();
        var displayOfSource = new int[sourceCount];
        Array.Fill(displayOfSource, -1);
        for (var d = 0; d < Display.Length; d++)
        {
            if (Display[d] < sourceCount)
            {
                displayOfSource[Display[d]] = d;
            }
        }
        DisplayOfSource = displayOfSource;
    }

    public IEnumerable<DataGridGroupNode> AllGroups()
    {
        if (Groups is null)
        {
            yield break;
        }
        var stack = new Stack<DataGridGroupNode>(Enumerable.Reverse(Groups));
        while (stack.Count > 0)
        {
            var node = stack.Pop();
            yield return node;
            if (node.Children is { } children)
            {
                for (var i = children.Length - 1; i >= 0; i--)
                {
                    stack.Push(children[i]);
                }
            }
        }
    }

    private static IEnumerable<int> Leaves(DataGridGroupNode node) => node.Rows;

    private static DataGridGroupNode[] BuildGroups(DataGridStore store, DataGridViewSpec spec, int[] rows, int[] accessors, int level, string parentId)
    {
        var accessor = accessors[level];
        var values = store.Column(accessor);
        var buckets = new Dictionary<string, List<int>>(StringComparer.Ordinal);
        var order = new List<string>();
        var firstValue = new Dictionary<string, object?>(StringComparer.Ordinal);
        foreach (var row in rows)
        {
            var value = values[row];
            var key = DataGridSorting.TextOf(value);
            if (!buckets.TryGetValue(key, out var list))
            {
                list = new List<int>();
                buckets[key] = list;
                order.Add(key);
                firstValue[key] = value;
            }
            list.Add(row);
        }
        var columnKey = store.Accessors[accessor].Key;
        var direction = spec.Sort.FirstOrDefault(s => s.Key == columnKey) is { Key: not null } sort ? sort.Direction : DataGridSortDirection.Ascending;
        var optionOrder = store.Accessors[accessor].Type == DataGridColumnType.Select
            ? DataGridSorting.OptionOrder(spec.OptionsOf(accessor))
            : null;
        var keys = order.ToArray();
        var groupValues = new object?[][] { keys.Select(k => firstValue[k]).ToArray() };
        var indices = Enumerable.Range(0, keys.Length).ToArray();
        DataGridSorting.SortIndices(indices, groupValues, [new DataGridSort(columnKey, direction)], [optionOrder], spec.Culture);
        var nodes = new DataGridGroupNode[indices.Length];
        for (var i = 0; i < indices.Length; i++)
        {
            var key = keys[indices[i]];
            var members = buckets[key].ToArray();
            var id = parentId + "\u001f" + key;
            var node = new DataGridGroupNode
            {
                Id = id,
                ColumnKey = columnKey,
                Accessor = accessor,
                Value = firstValue[key],
                Level = level,
                Rows = members
            };
            if (level + 1 < accessors.Length)
            {
                node.Children = BuildGroups(store, spec, members, accessors, level + 1, id);
                node = new DataGridGroupNode
                {
                    Id = id,
                    ColumnKey = columnKey,
                    Accessor = accessor,
                    Value = node.Value,
                    Level = level,
                    Rows = node.Children.SelectMany(c => c.Rows).ToArray(),
                    Children = node.Children
                };
            }
            nodes[i] = node;
        }
        return nodes;
    }

    /// <summary>One predicate for the filters and the quick search; null when nothing filters.</summary>
    private static Func<int, bool>? CreateMatcher(DataGridStore store, DataGridViewSpec spec)
    {
        var tests = new List<Func<int, bool>>();
        foreach (var (key, filter) in spec.Filters)
        {
            var accessor = store.AccessorIndex(key);
            if (accessor < 0 || filter is null)
            {
                continue;
            }
            var values = store.Column(accessor);
            var format = spec.FormatOf(accessor);
            if (filter.Values is { } keep)
            {
                var set = new HashSet<string>(keep, StringComparer.Ordinal);
                tests.Add(row => set.Contains(DataGridSorting.TextOf(values[row])));
                continue;
            }
            var expression = filter.Expression ?? string.Empty;
            if (format.IsNumeric)
            {
                if (DataGridFilterExpression.TryParseNumber(expression, spec.Culture, out var number) && number is not null)
                {
                    tests.Add(row => values[row] is decimal d && number(d));
                }
            }
            else if (format.Type == DataGridColumnType.Date)
            {
                if (DataGridFilterExpression.TryParseDate(expression, spec.Culture, out var date) && date is not null)
                {
                    tests.Add(row => values[row] is DateOnly d && date(d));
                }
            }
            else if (DataGridFilterExpression.CreateText(expression, spec.Culture) is { } text)
            {
                tests.Add(row => text(DataGridSorting.TextOf(values[row])));
            }
        }
        var words = spec.Search.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (words.Length > 0 && spec.VisibleAccessors.Length > 0)
        {
            var columns = spec.VisibleAccessors.Select(a => (Values: store.Column(a), Format: spec.FormatOf(a))).ToArray();
            var compare = spec.Culture.CompareInfo;
            const System.Globalization.CompareOptions options = CompareOptions.IgnoreCase | CompareOptions.IgnoreNonSpace |
                                                                CompareOptions.IgnoreWidth | CompareOptions.IgnoreKanaType;
            tests.Add(row =>
            {
                var haystack = string.Join(' ', columns.Select(c =>
                {
                    var value = c.Values[row];
                    if (value is null)
                    {
                        return string.Empty;
                    }
                    var display = DataGridValueFormat.Format(value, c.Format, spec.Culture);
                    var raw = DataGridSorting.TextOf(value);
                    return display == raw ? raw : display + " " + raw;
                }));
                foreach (var word in words)
                {
                    if (compare.IndexOf(haystack, word, options) < 0)
                    {
                        return false;
                    }
                }
                return true;
            });
        }
        if (tests.Count == 0)
        {
            return null;
        }
        var all = tests.ToArray();
        return row =>
        {
            foreach (var test in all)
            {
                if (!test(row))
                {
                    return false;
                }
            }
            return true;
        };
    }
}
