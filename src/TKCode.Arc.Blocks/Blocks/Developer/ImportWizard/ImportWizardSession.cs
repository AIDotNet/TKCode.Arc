using System.Text;

namespace TKCode.Arc;

/// <summary>One column of the loaded file and the field it goes to.</summary>
internal sealed class ImportColumnState
{
    public ImportColumnState(int index, string header, string samples, ImportColumnMatch proposal)
    {
        Index = index;
        Header = header;
        Samples = samples;
        Proposal = proposal;
        Target = proposal.Target;
    }

    /// <summary>The column's position in the file.</summary>
    public int Index { get; }

    /// <summary>The header text.</summary>
    public string Header { get; }

    /// <summary>The first two non-empty values joined with ", ".</summary>
    public string Samples { get; }

    /// <summary>What the matcher proposed.</summary>
    public ImportColumnMatch Proposal { get; }

    /// <summary>The chosen target index, or -1 for "Don't import".</summary>
    public int Target { get; internal set; }

    /// <summary>True when the person chose a target the matcher did not propose.</summary>
    public bool IsChosen => Target >= 0 && Target != Proposal.Target;

    /// <summary>True when the column is not imported.</summary>
    public bool IsIgnored => Target < 0;

    /// <summary>The confidence shown: the proposal's, 100 for a hand-picked field, 0 when ignored.</summary>
    public int Confidence => Target < 0 ? 0 : IsChosen ? 100 : Proposal.Confidence;

    /// <summary>True once the mapping step revealed this column's result (the reveal plays once per file).</summary>
    public bool IsRevealed { get; internal set; }
}

/// <summary>One data row in the review step.</summary>
internal sealed class ImportReviewRow
{
    public ImportReviewRow(int index, int lineNumber, int fieldCount)
    {
        Index = index;
        LineNumber = lineNumber;
        Values = new string[fieldCount];
        Problems = new string?[fieldCount];
        Fixed = new bool[fieldCount];
    }

    /// <summary>The row's index among the data rows.</summary>
    public int Index { get; }

    /// <summary>The line in the file (the header is line 1).</summary>
    public int LineNumber { get; }

    /// <summary>The current value per mapped field (in <see cref="ImportWizardSession.Fields"/> order).</summary>
    public string[] Values { get; }

    /// <summary>The validation message per mapped field, or null.</summary>
    public string?[] Problems { get; }

    /// <summary>True for a field whose problem was fixed by clean-up or an edit (the review step glows it once).</summary>
    public bool[] Fixed { get; }

    /// <summary>True when the person left the row out.</summary>
    public bool IsRemoved { get; internal set; }

    /// <summary>True when any mapped field has a problem.</summary>
    public bool HasProblems
    {
        get
        {
            foreach (var problem in Problems)
            {
                if (problem is not null)
                {
                    return true;
                }
            }
            return false;
        }
    }
}

/// <summary>How a cell changed (for <see cref="ImportWizardSession.CellChanged"/>).</summary>
internal enum ImportCellChange
{
    /// <summary>Clean-up applied a safe fix.</summary>
    CleanedUp,

    /// <summary>The person edited the value.</summary>
    Edited
}

/// <summary>
/// The state behind <see cref="ImportWizard"/>'s steps: the parsed file, the column mapping, the review rows with their
/// problems, and the import plan. Pure (no controls); the step views read it and call its methods, and listen to its
/// events to animate.
/// </summary>
internal sealed class ImportWizardSession
{
    private readonly List<ImportColumnState> _columns = new();
    private readonly List<ImportReviewRow> _rows = new();
    private readonly List<int> _fields = new();
    private string? _reviewSignature;

    public ImportWizardSession(IReadOnlyList<ImportTarget> targets, ISet<string>? existingKeys)
    {
        Targets = targets;
        ExistingKeys = existingKeys;
        KeyTarget = -1;
        for (var t = 0; t < targets.Count; t++)
        {
            if (targets[t].IsKey)
            {
                KeyTarget = t;
                break;
            }
        }
    }

    /// <summary>The target fields.</summary>
    public IReadOnlyList<ImportTarget> Targets { get; }

    /// <summary>Keys of existing records (compared trimmed and without case).</summary>
    public ISet<string>? ExistingKeys { get; }

    /// <summary>The index of the key target, or -1.</summary>
    public int KeyTarget { get; }

    /// <summary>The loaded file's name, or null.</summary>
    public string? FileName { get; private set; }

    /// <summary>The loaded file's size in bytes.</summary>
    public long FileSize { get; private set; }

    /// <summary>The parsed file (<see cref="ImportCsvTable.None"/> before one is loaded).</summary>
    public ImportCsvTable Table { get; private set; } = ImportCsvTable.None;

    /// <summary>True once a usable file is loaded.</summary>
    public bool HasFile => FileName is not null && Table.IsUsable;

    /// <summary>The columns of the loaded file.</summary>
    public IReadOnlyList<ImportColumnState> Columns => _columns;

    /// <summary>The mapped target indexes in target order (the review table's columns).</summary>
    public IReadOnlyList<int> Fields => _fields;

    /// <summary>The review rows (including the removed ones).</summary>
    public IReadOnlyList<ImportReviewRow> Rows => _rows;

    /// <summary>Raised when the mapping changed (a select, a reset).</summary>
    public event Action? MappingChanged;

    /// <summary>Raised when a cell's value changed: row, field position, how.</summary>
    public event Action<ImportReviewRow, int, ImportCellChange>? CellChanged;

    /// <summary>Raised when a row was left out.</summary>
    public event Action<ImportReviewRow>? RowRemoved;

    /// <summary>Raised after any change of the review counts.</summary>
    public event Action? CountsChanged;

    // ---- File ----

    /// <summary>Loads a parsed table; an unusable one clears the file and returns false.</summary>
    public bool Load(string fileName, long size, ImportCsvTable table)
    {
        _columns.Clear();
        _rows.Clear();
        _fields.Clear();
        _reviewSignature = null;
        if (!table.IsUsable || table.Headers.Count < 2)
        {
            FileName = null;
            FileSize = 0;
            Table = ImportCsvTable.None;
            MappingChanged?.Invoke();
            return false;
        }
        FileName = fileName;
        FileSize = size;
        Table = table;
        var proposals = ImportWizardMatcher.Match(table.Headers, table.Rows, Targets);
        for (var c = 0; c < table.Headers.Count; c++)
        {
            _columns.Add(new ImportColumnState(c, table.Headers[c], SamplesOf(table.Rows, c), proposals[c]));
        }
        MappingChanged?.Invoke();
        return true;
    }

    /// <summary>Forgets the file.</summary>
    public void Clear()
    {
        FileName = null;
        FileSize = 0;
        Table = ImportCsvTable.None;
        _columns.Clear();
        _rows.Clear();
        _fields.Clear();
        _reviewSignature = null;
        MappingChanged?.Invoke();
    }

    private static string SamplesOf(IReadOnlyList<string[]> rows, int column)
    {
        var found = new List<string>(2);
        foreach (var row in rows)
        {
            if (column < row.Length && !string.IsNullOrWhiteSpace(row[column]))
            {
                found.Add(row[column].Trim().Replace('\n', ' ').Replace('\r', ' '));
                if (found.Count == 2)
                {
                    break;
                }
            }
        }
        return string.Join(", ", found);
    }

    // ---- Mapping ----

    /// <summary>Chooses <paramref name="target"/> (-1 for "Don't import") for column <paramref name="column"/>.</summary>
    public void SetTarget(int column, int target)
    {
        if (column < 0 || column >= _columns.Count)
        {
            return;
        }
        target = target >= 0 && target < Targets.Count ? target : -1;
        if (_columns[column].Target == target)
        {
            return;
        }
        _columns[column].Target = target;
        MappingChanged?.Invoke();
    }

    /// <summary>How many columns go to a field.</summary>
    public int MappedCount
    {
        get
        {
            var count = 0;
            foreach (var column in _columns)
            {
                if (!column.IsIgnored)
                {
                    count++;
                }
            }
            return count;
        }
    }

    /// <summary>True when <paramref name="column"/>'s field is also chosen for another column.</summary>
    public bool IsDuplicate(int column)
    {
        var target = _columns[column].Target;
        if (target < 0)
        {
            return false;
        }
        for (var c = 0; c < _columns.Count; c++)
        {
            if (c != column && _columns[c].Target == target)
            {
                return true;
            }
        }
        return false;
    }

    /// <summary>The first target that is chosen for two columns, or -1.</summary>
    public int FirstDuplicateTarget()
    {
        for (var c = 0; c < _columns.Count; c++)
        {
            if (IsDuplicate(c))
            {
                return _columns[c].Target;
            }
        }
        return -1;
    }

    /// <summary>The first required target without a column, or -1.</summary>
    public int FirstMissingRequired()
    {
        for (var t = 0; t < Targets.Count; t++)
        {
            if (!Targets[t].IsRequired)
            {
                continue;
            }
            var found = false;
            foreach (var column in _columns)
            {
                if (column.Target == t)
                {
                    found = true;
                    break;
                }
            }
            if (!found)
            {
                return t;
            }
        }
        return -1;
    }

    /// <summary>True when the mapping can go to review (every required field has one column, no field has two).</summary>
    public bool IsMappingValid => HasFile && FirstMissingRequired() < 0 && FirstDuplicateTarget() < 0;

    // ---- Review ----

    /// <summary>
    /// Builds the review rows from the file and the mapping. Kept as they are (with fixes, edits and removed rows) when the
    /// mapping did not change since the last build.
    /// </summary>
    public void BuildReview()
    {
        var signature = string.Join(",", _columns.Select(c => c.Target));
        if (signature == _reviewSignature && _rows.Count > 0)
        {
            return;
        }
        _reviewSignature = signature;
        _fields.Clear();
        for (var t = 0; t < Targets.Count; t++)
        {
            foreach (var column in _columns)
            {
                if (column.Target == t)
                {
                    _fields.Add(t);
                    break;
                }
            }
        }
        var sourceOf = new int[_fields.Count];
        for (var f = 0; f < _fields.Count; f++)
        {
            sourceOf[f] = _columns.First(c => c.Target == _fields[f]).Index;
        }
        _rows.Clear();
        for (var r = 0; r < Table.Rows.Count; r++)
        {
            var source = Table.Rows[r];
            var row = new ImportReviewRow(r, r + 2, _fields.Count);
            for (var f = 0; f < _fields.Count; f++)
            {
                row.Values[f] = sourceOf[f] < source.Length ? source[sourceOf[f]] : string.Empty;
                row.Problems[f] = Validate(_fields[f], row.Values[f]);
            }
            _rows.Add(row);
        }
        CountsChanged?.Invoke();
    }

    private string? Validate(int target, string value)
    {
        try
        {
            return Targets[target].Validate?.Invoke(value);
        }
        catch (Exception exception)
        {
            return exception.Message;
        }
    }

    private string? SafeFix(int target, string value)
    {
        try
        {
            return Targets[target].Fix?.Invoke(value);
        }
        catch (Exception)
        {
            return null;
        }
    }

    /// <summary>Rows not left out.</summary>
    public int TotalRows => _rows.Count(r => !r.IsRemoved);

    /// <summary>Rows not left out that have a problem.</summary>
    public int IssueRows => _rows.Count(r => !r.IsRemoved && r.HasProblems);

    /// <summary>Rows not left out without problems.</summary>
    public int ReadyRows => _rows.Count(r => !r.IsRemoved && !r.HasProblems);

    /// <summary>Rows that will not be imported: left out by hand, or with a problem.</summary>
    public int SkippedRows => _rows.Count(r => r.IsRemoved || r.HasProblems);

    /// <summary>Cells with a problem whose safe fix passes validation (what "Clean up" fixes).</summary>
    public int FixableCells => FixableCellList().Count;

    /// <summary>The fixable cells in row then field order.</summary>
    public List<(ImportReviewRow Row, int Field)> FixableCellList()
    {
        var list = new List<(ImportReviewRow, int)>();
        foreach (var row in _rows)
        {
            if (row.IsRemoved)
            {
                continue;
            }
            for (var f = 0; f < _fields.Count; f++)
            {
                if (row.Problems[f] is not null && FixedValue(row, f) is not null)
                {
                    list.Add((row, f));
                }
            }
        }
        return list;
    }

    /// <summary>The safe fix of a cell when it exists and passes validation, else null.</summary>
    public string? FixedValue(ImportReviewRow row, int field)
    {
        var target = _fields[field];
        var fix = SafeFix(target, row.Values[field]);
        if (fix is null || fix == row.Values[field] || Validate(target, fix) is not null)
        {
            return null;
        }
        return fix;
    }

    /// <summary>Applies the safe fix of one cell; false when it has none.</summary>
    public bool CleanUp(ImportReviewRow row, int field)
    {
        if (row.IsRemoved || row.Problems[field] is null || FixedValue(row, field) is not { } fix)
        {
            return false;
        }
        row.Values[field] = fix;
        row.Problems[field] = null;
        row.Fixed[field] = true;
        CellChanged?.Invoke(row, field, ImportCellChange.CleanedUp);
        CountsChanged?.Invoke();
        return true;
    }

    /// <summary>Sets a cell's value and validates it again; returns true when it had a problem and now has none.</summary>
    public bool Edit(ImportReviewRow row, int field, string value)
    {
        if (row.IsRemoved)
        {
            return false;
        }
        var hadProblem = row.Problems[field] is not null;
        row.Values[field] = value;
        row.Problems[field] = Validate(_fields[field], value);
        var fixedNow = hadProblem && row.Problems[field] is null;
        if (fixedNow)
        {
            row.Fixed[field] = true;
        }
        CellChanged?.Invoke(row, field, ImportCellChange.Edited);
        CountsChanged?.Invoke();
        return fixedNow;
    }

    /// <summary>Leaves a row out of the import.</summary>
    public void Remove(ImportReviewRow row)
    {
        if (row.IsRemoved)
        {
            return;
        }
        row.IsRemoved = true;
        RowRemoved?.Invoke(row);
        CountsChanged?.Invoke();
    }

    // ---- Import ----

    /// <summary>The rows to import (ready, not left out), in file order.</summary>
    public List<ImportRecord> BuildRecords()
    {
        var records = new List<ImportRecord>();
        foreach (var row in _rows)
        {
            if (row.IsRemoved || row.HasProblems)
            {
                continue;
            }
            var values = new Dictionary<string, string>(StringComparer.Ordinal);
            var isUpdate = false;
            for (var f = 0; f < _fields.Count; f++)
            {
                var target = _fields[f];
                values[Targets[target].Key] = row.Values[f];
                if (target == KeyTarget && ExistingKeys is not null && ExistingKeys.Contains(row.Values[f].Trim()))
                {
                    isUpdate = true;
                }
            }
            records.Add(new ImportRecord(row.LineNumber, values, isUpdate));
        }
        return records;
    }

    /// <summary>The rows that are not imported (problems or left out) as CSV with the mapped fields' labels as headers.</summary>
    public string SkippedRowsCsv()
    {
        var builder = new StringBuilder();
        builder.Append(Quote("Line"));
        foreach (var target in _fields)
        {
            builder.Append(',').Append(Quote(Targets[target].Label));
        }
        builder.Append(',').Append(Quote("Problem")).Append('\n');
        foreach (var row in _rows)
        {
            if (!row.IsRemoved && !row.HasProblems)
            {
                continue;
            }
            builder.Append(row.LineNumber.ToString(System.Globalization.CultureInfo.InvariantCulture));
            string? problem = null;
            for (var f = 0; f < _fields.Count; f++)
            {
                builder.Append(',').Append(Quote(row.Values[f]));
                problem ??= row.Problems[f];
            }
            builder.Append(',').Append(Quote(row.IsRemoved ? "Left out" : problem ?? string.Empty)).Append('\n');
        }
        return builder.ToString();
    }

    private static string Quote(string value)
    {
        if (value.IndexOfAny([',', '"', '\n', '\r']) < 0 && value.Trim() == value)
        {
            return value;
        }
        return "\"" + value.Replace("\"", "\"\"", StringComparison.Ordinal) + "\"";
    }
}
