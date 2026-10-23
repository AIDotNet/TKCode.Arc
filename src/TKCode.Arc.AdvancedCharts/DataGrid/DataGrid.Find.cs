using System.Text;
using Avalonia;
using TKCode.Arc.Localization;

namespace TKCode.Arc;

public partial class DataGrid
{
    /// <summary>Shows the find bar (Ctrl+F; Ctrl+H adds the replace row). Not available with a <see cref="DataSource"/>, where Ctrl+F focuses the toolbar search.</summary>
    public static readonly StyledProperty<bool> IsFindOpenProperty =
        AvaloniaProperty.Register<DataGrid, bool>(nameof(IsFindOpen));

    /// <summary>The text the find bar looks for in the cells' display text; matching cells are tinted while the bar is open.</summary>
    public static readonly StyledProperty<string> FindTextProperty =
        AvaloniaProperty.Register<DataGrid, string>(nameof(FindText), string.Empty);

    /// <summary>Matches only the same letter case (default false).</summary>
    public static readonly StyledProperty<bool> FindMatchCaseProperty =
        AvaloniaProperty.Register<DataGrid, bool>(nameof(FindMatchCase));

    /// <summary>Matches only cells whose whole text equals <see cref="FindText"/> (default false).</summary>
    public static readonly StyledProperty<bool> FindMatchWholeCellProperty =
        AvaloniaProperty.Register<DataGrid, bool>(nameof(FindMatchWholeCell));

    private List<(int Row, int Column)> _matches = new();
    private HashSet<long> _matchSet = new();
    private int _matchKey = int.MinValue;

    /// <inheritdoc cref="IsFindOpenProperty"/>
    public bool IsFindOpen { get => GetValue(IsFindOpenProperty); set => SetValue(IsFindOpenProperty, value); }

    /// <inheritdoc cref="FindTextProperty"/>
    public string FindText { get => GetValue(FindTextProperty); set => SetValue(FindTextProperty, value); }

    /// <inheritdoc cref="FindMatchCaseProperty"/>
    public bool FindMatchCase { get => GetValue(FindMatchCaseProperty); set => SetValue(FindMatchCaseProperty, value); }

    /// <inheritdoc cref="FindMatchWholeCellProperty"/>
    public bool FindMatchWholeCell { get => GetValue(FindMatchWholeCellProperty); set => SetValue(FindMatchWholeCellProperty, value); }

    /// <summary>How many cells match <see cref="FindText"/> now.</summary>
    public int FindMatchCount => FindMatches().Count;

    private StringComparison FindComparison => FindMatchCase ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;

    /// <summary>Opens the find bar and focuses it; <paramref name="replace"/> shows the replace row too.</summary>
    public void OpenFind(bool replace = false)
    {
        if (HasDataSource)
        {
            _toolbar?.SearchBox.Focus();
            return;
        }
        IsFindOpen = true;
        Viewport.FindBar.Open(replace);
    }

    /// <summary>Closes the find bar and returns focus to the grid.</summary>
    public void CloseFind()
    {
        if (IsFindOpen)
        {
            IsFindOpen = false;
            Viewport.FocusGrid();
        }
    }

    private void OnFindChanged()
    {
        _matchKey = int.MinValue;
        Viewport.InvalidateLayers();
        Viewport.FindBar.Sync();
    }

    /// <summary>Whether a cell (display row, visible column) matches while the find bar is open.</summary>
    internal bool IsFindMatch(int row, int column)
    {
        FindMatches();
        return _matchSet.Count > 0 && _matchSet.Contains(((long)row << 24) | (uint)column);
    }

    /// <summary>True while matches should be tinted.</summary>
    internal bool IsFindHighlighting => IsFindOpen && FindText.Length > 0 && !HasDataSource;

    private bool TextMatches(string text)
    {
        var find = FindText;
        if (find.Length == 0)
        {
            return false;
        }
        return FindMatchWholeCell ? string.Equals(text, find, FindComparison) : text.Contains(find, FindComparison);
    }

    private List<(int Row, int Column)> FindMatches()
    {
        if (!IsFindHighlighting && !IsFindOpen && FindText.Length == 0)
        {
            return _matches = new List<(int Row, int Column)>();
        }
        EnsureView();
        var key = HashCode.Combine(_viewVersion, _editVersion, _columnsVersion, FindText, FindMatchCase, FindMatchWholeCell, HasDataSource);
        if (key == _matchKey)
        {
            return _matches;
        }
        _matchKey = key;
        _matches = new List<(int Row, int Column)>();
        _matchSet = new HashSet<long>();
        if (HasDataSource || FindText.Length == 0)
        {
            return _matches;
        }
        var visible = VisibleAccessors;
        for (var row = 0; row < DisplayCount && _matches.Count < 200_000; row++)
        {
            for (var column = 0; column < visible.Length; column++)
            {
                if (TextMatches(CellText(row, visible[column])))
                {
                    _matches.Add((row, column));
                    _matchSet.Add(((long)row << 24) | (uint)column);
                }
            }
        }
        return _matches;
    }

    /// <summary>The 1-based position of the active cell among the matches (0 when it is not one) and how many there are.</summary>
    internal (int Position, int Count) FindPosition()
    {
        var matches = FindMatches();
        // The matches are in row-major order, so the active cell is found by bisection (up to 200k matches).
        var index = HasCells ? matches.BinarySearch((Anchor.Row, Anchor.Column)) : -1;
        var position = index >= 0 ? index + 1 : 0;
        return (position, matches.Count);
    }

    /// <summary>Selects the next matching cell after the active one (wrapping round); false when nothing matches.</summary>
    public bool FindNext() => FindStep(forward: true, inclusive: false);

    /// <summary>Selects the previous matching cell before the active one (wrapping round); false when nothing matches.</summary>
    public bool FindPrevious() => FindStep(forward: false, inclusive: false);

    internal bool FindStep(bool forward, bool inclusive)
    {
        var matches = FindMatches();
        if (matches.Count == 0)
        {
            return false;
        }
        var current = HasCells ? (Anchor.Row, Anchor.Column) : (-1, -1);
        int index;
        if (forward)
        {
            index = matches.FindIndex(m => inclusive ? m.CompareTo(current) >= 0 : m.CompareTo(current) > 0);
            if (index < 0)
            {
                index = 0;
            }
        }
        else
        {
            index = matches.FindLastIndex(m => inclusive ? m.CompareTo(current) <= 0 : m.CompareTo(current) < 0);
            if (index < 0)
            {
                index = matches.Count - 1;
            }
        }
        Select(new DataGridCellAddress(matches[index].Row, matches[index].Column));
        Viewport.FindBar.Sync();
        return true;
    }

    /// <summary>
    /// Replaces <see cref="FindText"/> in the active cell with <paramref name="replacement"/> when it is a match, then moves to the
    /// next match; otherwise just moves to the next match. Returns whether a cell was replaced.
    /// </summary>
    public bool ReplaceCurrent(string replacement)
    {
        EnsureView();
        if (HasCells && IsFindMatch(Anchor.Row, Anchor.Column))
        {
            var replaced = ReplaceCells([(Anchor.Row, Anchor.Column)], replacement, out _);
            FindStep(forward: true, inclusive: false);
            return replaced > 0;
        }
        FindStep(forward: true, inclusive: true);
        return false;
    }

    /// <summary>Replaces <see cref="FindText"/> in every matching cell as one undo step; returns how many cells changed.</summary>
    public int ReplaceAll(string replacement)
    {
        EnsureView();
        var matches = FindMatches().ToList();
        if (matches.Count == 0)
        {
            Say(DataGridText.Get(DataGridLangResourceKind.NothingToReplace));
            return 0;
        }
        var replaced = ReplaceCells(matches, replacement, out var skipped);
        var message = DataGridText.Count(DataGridLangResourceKind.ReplacedCellsOne, DataGridLangResourceKind.ReplacedCellsOther, replaced, EffectiveCulture);
        if (skipped > 0)
        {
            message += DataGridText.Count(DataGridLangResourceKind.PasteSkippedOne, DataGridLangResourceKind.PasteSkippedOther, skipped, EffectiveCulture);
        }
        Say(message);
        Viewport.FindBar.Sync();
        return replaced;
    }

    /// <summary>Replaces text in the given cells: the new display text is read back as the column reads typed text; cells it does not parse for are skipped.</summary>
    private int ReplaceCells(IEnumerable<(int Row, int Column)> cells, string replacement, out int skipped)
    {
        skipped = 0;
        var visible = VisibleAccessors;
        var writes = new List<(int Source, int Accessor, object? Value)>();
        foreach (var (row, column) in cells)
        {
            var accessor = visible[column];
            var source = SourceOfRow(row);
            var text = CellText(row, accessor);
            if (source < 0 || ReplaceInText(text, replacement) is not { } updated)
            {
                continue;
            }
            var definition = ColumnOf(accessor);
            if (DataGridValueParser.TryParse(updated, definition.CellFormat, EffectiveCulture, definition.Type == DataGridColumnType.Select ? OptionsOf(accessor) : null,
                    out var value))
            {
                writes.Add((source, accessor, value));
            }
            else
            {
                skipped++;
            }
        }
        if (writes.Count == 0)
        {
            return 0;
        }
        var written = WriteCells(writes, null, DataGridChangeReason.Replace, flash: false);
        skipped += writes.Count - written;
        return written;
    }

    private string? ReplaceInText(string text, string replacement)
    {
        var find = FindText;
        if (find.Length == 0)
        {
            return null;
        }
        if (FindMatchWholeCell)
        {
            return string.Equals(text, find, FindComparison) ? replacement : null;
        }
        var at = text.IndexOf(find, FindComparison);
        if (at < 0)
        {
            return null;
        }
        var builder = new StringBuilder();
        var position = 0;
        while (at >= 0)
        {
            builder.Append(text, position, at - position).Append(replacement);
            position = at + find.Length;
            at = text.IndexOf(find, position, FindComparison);
        }
        return builder.Append(text, position, text.Length - position).ToString();
    }
}
