using Avalonia;
using Avalonia.Controls;

namespace TKCode.Arc;

/// <summary>
/// The vertical stack of a <see cref="TreeTable"/>'s rows. Reconciling keeps a row per line key: new rows enter, rows
/// whose line is gone stay in place while they leave (Arc's <c>AnimatePresence</c>) and are removed when done, and after
/// a sort every row that moved springs from its old place to its new one.
/// </summary>
internal sealed class TreeTableRowsHost : StackPanel
{
    // Same caps as TablesTreeRowMotion: rows of one opened or closed run animate up to AnimatedRunCap, and at most
    // AnimatedUpdateCap rows per reconcile; the rest land at once. Every animated row springs its own height and
    // re-measures the stack each frame, so an uncapped "expand all" of a large tree measured thousands of rows per frame.
    private const int AnimatedRunCap = 48;
    private const int AnimatedUpdateCap = 96;

    private readonly TreeTable _owner;
    private readonly Dictionary<object, TreeTableRow> _rows = new();
    private Dictionary<object, double>? _tops;

    public TreeTableRowsHost(TreeTable owner)
    {
        _owner = owner;
        Orientation = Avalonia.Layout.Orientation.Vertical;
    }

    internal int RowCount => _rows.Count;

    internal TreeTableRow? RowFor(object key) => _rows.GetValueOrDefault(key);

    internal IEnumerable<TreeTableRow> Rows => Children.OfType<TreeTableRow>();

    public void Reconcile(IReadOnlyList<TreeTableLine> lines, ISet<object> handoff, bool animate, bool flip, bool rebuildCells)
    {
        var previous = Children.OfType<TreeTableRow>().ToList();
        if (flip)
        {
            _tops = new Dictionary<object, double>();
            foreach (var row in previous)
            {
                if (!row.IsExiting)
                {
                    _tops[row.Key] = row.Bounds.Y;
                }
            }
        }
        var wanted = new HashSet<object>(lines.Count);
        foreach (var line in lines)
        {
            wanted.Add(line.Key);
        }

        // Rows without a line leave; a loading line whose children just arrived goes at once.
        var budget = AnimatedUpdateCap;
        var run = 0;
        foreach (var row in previous)
        {
            if (wanted.Contains(row.Key))
            {
                run = 0;
                continue;
            }
            if (row.IsExiting)
            {
                continue;
            }
            var instant = row.Line.Kind != TreeTableLineKind.Row && row.Line.ParentKey is { } parent && handoff.Contains(parent);
            var animated = animate && !instant && run < AnimatedRunCap && budget > 0;
            if (animated)
            {
                run++;
                budget--;
            }
            row.Exit(!animated, Remove);
        }

        var present = new List<TreeTableRow>(lines.Count);
        var entering = new List<(TreeTableRow Row, bool TakeOver, int Position, int Index)>();
        for (var index = 0; index < lines.Count; index++)
        {
            var line = lines[index];
            if (_rows.TryGetValue(line.Key, out var row))
            {
                var wasExiting = row.IsExiting;
                var kindChanged = row.Line.Kind != line.Kind;
                row.Bind(line, rebuildCells || kindChanged);
                if (wasExiting)
                {
                    row.Revive(animate);
                }
            }
            else
            {
                row = new TreeTableRow(_owner, line.Key);
                _rows[line.Key] = row;
                row.Bind(line, rebuild: true);
                var takeOver = line.Kind == TreeTableLineKind.Row && line.Position == 1 && line.ParentKey is { } parent && handoff.Contains(parent);
                entering.Add((row, takeOver, line.Position, index));
            }
            present.Add(row);
        }

        // Exiting rows keep their place: each sits after the nearest earlier row of the old order.
        var order = new List<TreeTableRow>(present);
        var presentSet = new HashSet<TreeTableRow>(present);
        TreeTableRow? anchor = null;
        foreach (var row in previous)
        {
            if (presentSet.Contains(row))
            {
                anchor = row;
                continue;
            }
            if (!_rows.ContainsKey(row.Key))
            {
                continue;
            }
            order.Insert(anchor is null ? 0 : order.IndexOf(anchor) + 1, row);
            anchor = row;
        }

        for (var i = 0; i < order.Count; i++)
        {
            if (i < Children.Count && ReferenceEquals(Children[i], order[i]))
            {
                continue;
            }
            var at = Children.IndexOf(order[i]);
            if (at >= 0)
            {
                Children.RemoveAt(at);
            }
            Children.Insert(i, order[i]);
        }
        while (Children.Count > order.Count)
        {
            Children.RemoveAt(Children.Count - 1);
        }

        Restripe(lines);

        // Rows start their enter once they are in the tree, where the motion level is known. Consecutive new lines form
        // a run; rows past the caps appear at once (a single expand of up to 48 rows is unchanged).
        run = 0;
        var lastIndex = -2;
        foreach (var (row, takeOver, position, index) in entering)
        {
            if (index != lastIndex + 1)
            {
                run = 0;
            }
            lastIndex = index;
            var animated = animate && run < AnimatedRunCap && budget > 0;
            if (animated)
            {
                run++;
                budget--;
            }
            row.Enter(animated, takeOver, position);
        }

        if (_tops is not null)
        {
            LayoutUpdated += OnLayoutAfterSort;
            InvalidateMeasure();
        }
    }

    /// <summary>Marks every second row of <paramref name="lines"/> (loading and retry lines are skipped and not counted).</summary>
    internal void Restripe(IReadOnlyList<TreeTableLine> lines)
    {
        var index = 0;
        foreach (var line in lines)
        {
            if (line.Kind != TreeTableLineKind.Row)
            {
                continue;
            }
            if (_rows.TryGetValue(line.Key, out var row))
            {
                row.SetStriped(_owner.IsStriped && (index & 1) == 1);
            }
            index++;
        }
    }

    private void OnLayoutAfterSort(object? sender, EventArgs e)
    {
        LayoutUpdated -= OnLayoutAfterSort;
        var tops = _tops;
        _tops = null;
        if (tops is null)
        {
            return;
        }
        foreach (var row in Children.OfType<TreeTableRow>())
        {
            if (!row.IsExiting && tops.TryGetValue(row.Key, out var old))
            {
                row.Slide(old - row.Bounds.Y);
            }
        }
    }

    private void Remove(TreeTableRow row)
    {
        if (_rows.TryGetValue(row.Key, out var current) && ReferenceEquals(current, row) && row.IsExiting)
        {
            _rows.Remove(row.Key);
            Children.Remove(row);
        }
    }

    internal void RefreshPalette()
    {
        foreach (var row in Rows)
        {
            row.RefreshPalette();
        }
    }
}
