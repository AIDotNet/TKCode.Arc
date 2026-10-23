using System.Collections.Generic;
using Avalonia.Collections;
using Avalonia.Threading;

namespace TKCode.Arc;

/// <summary>What changed in an <see cref="ExtractionStore"/> since the last <see cref="ExtractionStore.Changed"/>.</summary>
internal sealed class ExtractionStoreChange
{
    /// <summary>Creates a change.</summary>
    public ExtractionStoreChange(IReadOnlyCollection<string> paths, bool rowsChanged, bool cleared)
    {
        Paths = paths;
        RowsChanged = rowsChanged;
        Cleared = cleared;
    }

    /// <summary>The paths of the values that changed (cells and field keys; not table keys).</summary>
    public IReadOnlyCollection<string> Paths { get; }

    /// <summary>Whether table rows were added or removed.</summary>
    public bool RowsChanged { get; }

    /// <summary>Whether <see cref="ExtractionStore.Clear"/> ran.</summary>
    public bool Cleared { get; }
}

/// <summary>
/// The values of one extraction (spec 2.2–2.4, 8: "all values go through one reducer"): what the model found, what the
/// person changed, and everything derived from them (counts, average, invalid values, the typed output). Single-threaded:
/// call it on the UI thread.
/// </summary>
/// <remarks>
/// <para>
/// Every field that is not a table has an <see cref="ExtractionValue"/> from the start (status Pending, empty text). A table
/// gets its rows when their first cell arrives: the row is created with a Pending value for every column (and any missing
/// rows before it), so <see cref="TableRows"/> is always dense and rectangular. Rows numbered <see cref="MaxRows"/> or more
/// are refused like an unknown path.
/// </para>
/// <para>
/// Counting rules: <see cref="FoundCount"/> is the number of leaf values (fields and cells) that are not Pending;
/// <see cref="FieldCount"/> is the number of leaf values there are, where a table without rows counts as one expected value
/// (its size is unknown until rows arrive), so the invoice reads "0 of 10" before a run and "25 of 25" after it. Per-field
/// counts like Arc's status line ("3 of 10", a table being one field found once any of its cells is) are
/// <see cref="FoundFieldCount"/> of <see cref="TotalFieldCount"/>. <see cref="AverageConfidence"/> is the mean confidence of
/// the leaf values the model filled (cells included; values the person typed into empty fields have no confidence and do
/// not count; a model value keeps its confidence when edited).
/// </para>
/// <para>
/// Mutations change the values at once (their properties notify) and mark the summary stale; <see cref="Changed"/> is
/// raised once for all mutations of one dispatcher turn (through <see cref="Post"/>), or at once by <see cref="Flush"/>.
/// </para>
/// </remarks>
internal sealed class ExtractionStore
{
    /// <summary>The first row number that is refused (a guard against a stream that names absurd rows).</summary>
    public const int MaxRows = 10_000;

    private readonly Func<double> _reviewThreshold;
    private readonly Dictionary<string, Leaf> _scalars = new(StringComparer.Ordinal);
    private readonly Dictionary<string, Table> _tables = new(StringComparer.Ordinal);
    private readonly ExtractionValue[] _fieldValues;
    private readonly HashSet<string> _pendingPaths = new(StringComparer.Ordinal);
    private List<ExtractionValue>? _values;
    private DocumentExtractionLabels _labels = new();
    private bool _pendingRows;
    private bool _pendingCleared;
    private bool _hasPending;
    private bool _flushQueued;

    // The summary, computed when read after a mutation.
    private bool _summaryStale = true;
    private int _foundCount;
    private int _fieldCount;
    private int _checkCount;
    private int _foundFieldCount;
    private double _averageConfidence;
    private bool _allAccepted;
    private ExtractionValue[] _invalid = [];

    /// <summary>Creates the values of <paramref name="schema"/>.</summary>
    /// <param name="schema">The schema (read once; make a new store when it changes).</param>
    /// <param name="reviewThreshold">The current review threshold; values below it need a check.</param>
    public ExtractionStore(ExtractionSchema schema, Func<double> reviewThreshold)
    {
        _reviewThreshold = reviewThreshold;
        Info = new ExtractionSchemaInfo(schema);
        var fields = Info.Fields;
        _fieldValues = new ExtractionValue[fields.Count];
        for (var i = 0; i < fields.Count; i++)
        {
            var field = fields[i];
            if (field is ExtractionTableField table)
            {
                if (!_tables.TryGetValue(table.Key, out var entry))
                {
                    entry = new Table(table);
                    _tables[table.Key] = entry;
                }
                _fieldValues[i] = entry.Summary;
            }
            else
            {
                if (!_scalars.TryGetValue(field.Key, out var leaf))
                {
                    leaf = new Leaf(new ExtractionValue(field.Key), field, null, 0);
                    _scalars[field.Key] = leaf;
                }
                _fieldValues[i] = leaf.Value;
            }
        }
    }

    /// <summary>Raised after mutations, once per dispatcher turn (see <see cref="Post"/>), with what changed.</summary>
    public event Action<ExtractionStoreChange>? Changed;

    /// <summary>The schema as the form reads it.</summary>
    public ExtractionSchemaInfo Info { get; }

    /// <summary>
    /// Queues the batched <see cref="Changed"/> (by default after the work already queued on the UI thread at normal
    /// priority, so values that arrive in one turn are reported once). Tests may run the action at once.
    /// </summary>
    public Action<Action> Post { get; set; } = static action => Dispatcher.UIThread.Post(action, DispatcherPriority.Normal);

    /// <summary>The texts of type errors (<see cref="DocumentExtractionLabels.DateError"/> and the others); setting it rewrites the errors shown.</summary>
    public DocumentExtractionLabels Labels
    {
        get => _labels;
        set
        {
            _labels = value;
            foreach (var leaf in Leaves())
            {
                leaf.Value.Error = ErrorText(leaf.Error);
            }
        }
    }

    /// <summary>Every leaf value in schema order: fields, and each table's cells row by row where the table is.</summary>
    public IReadOnlyList<ExtractionValue> Values => _values ??= Leaves().Select(l => l.Value).ToList();

    /// <summary>
    /// One value per field in schema order (for the footer bars): a field's own value, or for a table a summary whose
    /// <see cref="ExtractionValue.Confidence"/> is the mean of its cells the model filled, Pending until a cell has a value,
    /// Accepted when every cell with a value is, Edited when the person typed every one, and
    /// <see cref="ExtractionValue.NeedsCheck"/> when any cell needs a check.
    /// </summary>
    public IReadOnlyList<ExtractionValue> FieldValues => _fieldValues;

    /// <summary>How many leaf values (fields and cells) have a value, that is are not Pending.</summary>
    public int FoundCount
    {
        get
        {
            EnsureSummary();
            return _foundCount;
        }
    }

    /// <summary>How many leaf values there are (fields, and cells so far; a table without rows counts as one).</summary>
    public int FieldCount
    {
        get
        {
            EnsureSummary();
            return _fieldCount;
        }
    }

    /// <summary>How many leaf values need a check.</summary>
    public int CheckCount
    {
        get
        {
            EnsureSummary();
            return _checkCount;
        }
    }

    /// <summary>How many fields have a value (a table once any of its cells has one).</summary>
    public int FoundFieldCount
    {
        get
        {
            EnsureSummary();
            return _foundFieldCount;
        }
    }

    /// <summary>How many fields the schema has (a table is one).</summary>
    public int TotalFieldCount => Info.Fields.Count;

    /// <summary>The mean confidence (0 to 1) of the values the model filled, cells included; 0 when there are none.</summary>
    public double AverageConfidence
    {
        get
        {
            EnsureSummary();
            return _averageConfidence;
        }
    }

    /// <summary>The leaf values whose text does not fit their type, in schema order.</summary>
    public IReadOnlyList<ExtractionValue> InvalidValues
    {
        get
        {
            EnsureSummary();
            return _invalid;
        }
    }

    /// <summary>Whether some value is found and every found value is accepted.</summary>
    public bool AllAccepted
    {
        get
        {
            EnsureSummary();
            return _allAccepted;
        }
    }

    /// <summary>The leaf value at <paramref name="path"/> (a field key or <c>table.row.column</c>), or null.</summary>
    public ExtractionValue? Find(string path) => TryLeaf(path, out var leaf) ? leaf.Value : null;

    /// <summary>The value of the field <paramref name="key"/> as in <see cref="FieldValues"/> (a table's summary), or null.</summary>
    public ExtractionValue? FieldValue(string key) =>
        _scalars.TryGetValue(key, out var leaf) ? leaf.Value : _tables.TryGetValue(key, out var table) ? table.Summary : null;

    /// <summary>The rows of the table <paramref name="key"/> (live: rows are added as they arrive and removed by <see cref="Clear"/>; do not change it), or null.</summary>
    public AvaloniaList<AvaloniaList<ExtractionValue>>? TableRows(string key) => _tables.TryGetValue(key, out var table) ? table.Rows : null;

    /// <summary>
    /// Takes a value the model found: sets its text, source, confidence and note, marks it Filled (needing a check below the
    /// review threshold) and parses it. Returns the value, or null when the path names nothing (the event is ignored).
    /// </summary>
    public ExtractionValue? ApplyModelValue(ExtractionValueEvent value)
    {
        if (!Info.TryResolve(value.Path, out var field, out var column, out var row))
        {
            return null;
        }
        Leaf leaf;
        Table? table = null;
        if (column is null)
        {
            if (!_scalars.TryGetValue(field.Key, out var scalar))
            {
                return null;
            }
            leaf = scalar;
        }
        else
        {
            table = _tables[field.Key];
            if (row is not { } index || index >= MaxRows)
            {
                return null;
            }
            EnsureRows(table, index + 1);
            leaf = table.Leaves[index][table.Field.Columns.IndexOf(column)];
        }
        var confidence = double.IsNaN(value.Confidence) ? 0 : Math.Clamp(value.Confidence, 0, 1);
        var target = leaf.Value;
        leaf.FromModel = true;
        leaf.Undo = null;
        target.SourceId = value.SourceId;
        target.Confidence = confidence;
        target.Note = value.Note;
        target.Status = ExtractionValueStatus.Filled;
        target.NeedsCheck = confidence < _reviewThreshold();
        target.Text = value.Value ?? string.Empty;
        Reparse(leaf);
        Touched(leaf, table);
        return target;
    }

    /// <summary>
    /// Takes text the person typed (call it after every edit, also when the part already wrote the text): the value becomes
    /// Edited, no longer needs a check, and is parsed again. Returns the value, or null for an unknown path.
    /// </summary>
    public ExtractionValue? EditByUser(string path, string text)
    {
        if (!TryLeaf(path, out var leaf, out var table))
        {
            return null;
        }
        var target = leaf.Value;
        leaf.Undo = null;
        target.Status = ExtractionValueStatus.Edited;
        target.NeedsCheck = false;
        target.Text = text;
        Reparse(leaf);
        Touched(leaf, table);
        return target;
    }

    /// <summary>"Looks right": a value that needs a check becomes Checked. False when it did not need one (or the path is unknown).</summary>
    public bool MarkChecked(string path)
    {
        if (!TryLeaf(path, out var leaf, out var table) || !leaf.Value.NeedsCheck)
        {
            return false;
        }
        leaf.Undo = null;
        leaf.Value.Status = ExtractionValueStatus.Checked;
        leaf.Value.NeedsCheck = false;
        Touched(leaf, table);
        return true;
    }

    /// <summary>Back to the start: every field Pending and empty, every table without rows.</summary>
    public void Clear()
    {
        foreach (var leaf in _scalars.Values)
        {
            Reset(leaf);
            _pendingPaths.Add(leaf.Value.Path);
        }
        foreach (var table in _tables.Values)
        {
            if (table.Rows.Count > 0)
            {
                foreach (var row in table.Leaves)
                {
                    foreach (var cell in row)
                    {
                        _pendingPaths.Add(cell.Value.Path);
                    }
                }
                table.Leaves.Clear();
                table.Rows.Clear();
                _values = null;
                _pendingRows = true;
            }
            UpdateSummary(table);
        }
        _pendingCleared = true;
        MarkStale();
    }

    /// <summary>
    /// Accept all: every value with a value (Filled, Checked, Edited) becomes Accepted and no longer needs a check; what it
    /// was is kept for <see cref="UndoAccept"/> until <see cref="CommitAccept"/>. The caller checks <see cref="InvalidValues"/>
    /// first. Returns how many values changed.
    /// </summary>
    public int AcceptAll()
    {
        var count = 0;
        foreach (var (leaf, table) in LeavesWithTables())
        {
            var value = leaf.Value;
            if (value.Status is ExtractionValueStatus.Pending or ExtractionValueStatus.Accepted)
            {
                continue;
            }
            leaf.Undo = value.Status;
            leaf.UndoNeedsCheck = value.NeedsCheck;
            value.Status = ExtractionValueStatus.Accepted;
            value.NeedsCheck = false;
            Touched(leaf, table);
            count++;
        }
        return count;
    }

    /// <summary>Undoes the last <see cref="AcceptAll"/> (the app refused it): values it accepted and nobody changed since go back. Returns how many.</summary>
    public int UndoAccept()
    {
        var count = 0;
        foreach (var (leaf, table) in LeavesWithTables())
        {
            if (leaf.Undo is not { } status || leaf.Value.Status != ExtractionValueStatus.Accepted)
            {
                leaf.Undo = null;
                continue;
            }
            leaf.Value.Status = status;
            leaf.Value.NeedsCheck = leaf.UndoNeedsCheck;
            leaf.Undo = null;
            Touched(leaf, table);
            count++;
        }
        return count;
    }

    /// <summary>The accept was saved: forgets what <see cref="UndoAccept"/> would restore.</summary>
    public void CommitAccept()
    {
        foreach (var leaf in Leaves())
        {
            leaf.Undo = null;
        }
    }

    /// <summary>Applies a changed review threshold to the values the model filled that nobody has checked, edited or accepted.</summary>
    public void RefreshThreshold()
    {
        var threshold = _reviewThreshold();
        foreach (var (leaf, table) in LeavesWithTables())
        {
            var value = leaf.Value;
            if (value.Status == ExtractionValueStatus.Filled && value.NeedsCheck != value.Confidence < threshold)
            {
                value.NeedsCheck = value.Confidence < threshold;
                Touched(leaf, table);
            }
        }
    }

    /// <summary>
    /// The typed output now: one entry per field in schema order; a date is ISO text, an amount or number a
    /// <see cref="decimal"/>, an integer a <see cref="long"/>, a currency, IBAN or text a string; an empty or invalid value is
    /// null; a table is a list of rows (<see cref="IReadOnlyDictionary{TKey,TValue}"/> in column order).
    /// </summary>
    public ExtractionOutput BuildOutput()
    {
        var fields = Info.Fields;
        var entries = new List<KeyValuePair<string, object?>>(fields.Count);
        foreach (var field in fields)
        {
            if (field is ExtractionTableField tableField)
            {
                var table = _tables[tableField.Key];
                var keys = tableField.Columns.Select(c => c.Key).ToArray();
                var rows = new List<IReadOnlyDictionary<string, object?>>(table.Leaves.Count);
                foreach (var row in table.Leaves)
                {
                    var cells = new object?[row.Length];
                    for (var c = 0; c < row.Length; c++)
                    {
                        cells[c] = OutputOf(row[c]);
                    }
                    rows.Add(new ExtractionOutputRow(keys, cells));
                }
                entries.Add(new KeyValuePair<string, object?>(field.Key, rows));
            }
            else
            {
                entries.Add(new KeyValuePair<string, object?>(field.Key, OutputOf(_scalars[field.Key])));
            }
        }
        return new ExtractionOutput(entries);
    }

    /// <summary>Raises a pending <see cref="Changed"/> now.</summary>
    public void Flush()
    {
        _flushQueued = false;
        if (!_hasPending)
        {
            return;
        }
        var change = new ExtractionStoreChange(_pendingPaths.ToArray(), _pendingRows, _pendingCleared);
        _pendingPaths.Clear();
        _pendingRows = false;
        _pendingCleared = false;
        _hasPending = false;
        Changed?.Invoke(change);
    }

    /// <summary>The text of an error kind in <see cref="Labels"/>; null for none.</summary>
    public string? ErrorText(ExtractionValueParser.ErrorKind kind) => kind switch
    {
        ExtractionValueParser.ErrorKind.None => null,
        ExtractionValueParser.ErrorKind.Date => _labels.DateError,
        ExtractionValueParser.ErrorKind.Iban => _labels.IbanError,
        ExtractionValueParser.ErrorKind.Currency => _labels.CurrencyError,
        _ => _labels.NumberError
    };

    private static object? OutputOf(Leaf leaf) =>
        leaf.Error != ExtractionValueParser.ErrorKind.None ? null : ExtractionValueParser.ToOutput(leaf.Value.TypedValue);

    private void Reparse(Leaf leaf)
    {
        var result = ExtractionValueParser.Parse(leaf.Type, leaf.Value.Text);
        leaf.Error = result.Error;
        leaf.Value.TypedValue = result.Value;
        leaf.Value.Error = ErrorText(result.Error);
    }

    private static void Reset(Leaf leaf)
    {
        var value = leaf.Value;
        leaf.FromModel = false;
        leaf.Undo = null;
        leaf.Error = ExtractionValueParser.ErrorKind.None;
        value.Status = ExtractionValueStatus.Pending;
        value.NeedsCheck = false;
        value.Text = string.Empty;
        value.SourceId = null;
        value.Confidence = 0;
        value.Note = null;
        value.TypedValue = null;
        value.Error = null;
    }

    /// <summary>Records a changed leaf: its table's summary follows at once, the summary and <see cref="Changed"/> later.</summary>
    private void Touched(Leaf leaf, Table? table)
    {
        if (table is not null)
        {
            UpdateSummary(table);
        }
        _pendingPaths.Add(leaf.Value.Path);
        MarkStale();
    }

    private void MarkStale()
    {
        _summaryStale = true;
        _hasPending = true;
        if (!_flushQueued)
        {
            _flushQueued = true;
            Post(Flush);
        }
    }

    private void EnsureRows(Table table, int count)
    {
        if (table.Rows.Count >= count)
        {
            return;
        }
        var columns = table.Field.Columns;
        while (table.Rows.Count < count)
        {
            var index = table.Rows.Count;
            var leaves = new Leaf[columns.Count];
            var row = new AvaloniaList<ExtractionValue>(columns.Count);
            for (var c = 0; c < columns.Count; c++)
            {
                leaves[c] = new Leaf(new ExtractionValue(ExtractionSchemaInfo.CellPath(table.Field.Key, index, columns[c].Key)), table.Field, columns[c], index);
                row.Add(leaves[c].Value);
            }
            table.Leaves.Add(leaves);
            table.Rows.Add(row);
        }
        _values = null;
        _pendingRows = true;
    }

    /// <summary>The mean of the table's cells as one value for the field's bar and header (see <see cref="FieldValues"/>).</summary>
    private static void UpdateSummary(Table table)
    {
        var filled = 0;
        var accepted = 0;
        var fromModel = 0;
        var needsCheck = false;
        var sum = 0.0;
        foreach (var row in table.Leaves)
        {
            foreach (var cell in row)
            {
                var value = cell.Value;
                if (value.Status == ExtractionValueStatus.Pending)
                {
                    continue;
                }
                filled++;
                accepted += value.Status == ExtractionValueStatus.Accepted ? 1 : 0;
                needsCheck |= value.NeedsCheck;
                if (cell.FromModel)
                {
                    fromModel++;
                    sum += value.Confidence;
                }
            }
        }
        var summary = table.Summary;
        summary.Confidence = fromModel > 0 ? sum / fromModel : 0;
        summary.NeedsCheck = needsCheck;
        summary.Status = filled == 0 ? ExtractionValueStatus.Pending
            : accepted == filled ? ExtractionValueStatus.Accepted
            : fromModel == 0 ? ExtractionValueStatus.Edited
            : ExtractionValueStatus.Filled;
    }

    private void EnsureSummary()
    {
        if (!_summaryStale)
        {
            return;
        }
        _summaryStale = false;
        var found = 0;
        var check = 0;
        var accepted = 0;
        var fromModel = 0;
        var sum = 0.0;
        var foundFields = 0;
        var invalid = new List<ExtractionValue>();
        foreach (var field in Info.Fields)
        {
            var any = false;
            foreach (var leaf in LeavesOf(field))
            {
                var value = leaf.Value;
                if (leaf.Error != ExtractionValueParser.ErrorKind.None)
                {
                    invalid.Add(value);
                }
                if (value.Status == ExtractionValueStatus.Pending)
                {
                    continue;
                }
                any = true;
                found++;
                check += value.NeedsCheck ? 1 : 0;
                accepted += value.Status == ExtractionValueStatus.Accepted ? 1 : 0;
                if (leaf.FromModel)
                {
                    fromModel++;
                    sum += value.Confidence;
                }
            }
            foundFields += any ? 1 : 0;
        }
        _foundCount = found;
        _checkCount = check;
        _foundFieldCount = foundFields;
        _averageConfidence = fromModel > 0 ? sum / fromModel : 0;
        _allAccepted = found > 0 && accepted == found;
        _invalid = invalid.ToArray();
        _fieldCount = Info.LeafCount(t => _tables.TryGetValue(t.Key, out var table) ? table.Rows.Count : 0);
    }

    private bool TryLeaf(string path, out Leaf leaf) => TryLeaf(path, out leaf, out _);

    private bool TryLeaf(string path, out Leaf leaf, out Table? table)
    {
        table = null;
        leaf = null!;
        if (_scalars.TryGetValue(path, out var scalar))
        {
            leaf = scalar;
            return true;
        }
        if (!Info.TryResolve(path, out var field, out var column, out var row) || column is null || row is not { } index ||
            !_tables.TryGetValue(field.Key, out table) || index >= table.Leaves.Count)
        {
            table = null;
            return false;
        }
        leaf = table.Leaves[index][table.Field.Columns.IndexOf(column)];
        return true;
    }

    private IEnumerable<Leaf> LeavesOf(ExtractionField field)
    {
        if (field is ExtractionTableField && _tables.TryGetValue(field.Key, out var table))
        {
            foreach (var row in table.Leaves)
            {
                foreach (var cell in row)
                {
                    yield return cell;
                }
            }
        }
        else if (_scalars.TryGetValue(field.Key, out var leaf))
        {
            yield return leaf;
        }
    }

    private IEnumerable<Leaf> Leaves()
    {
        foreach (var field in Info.Fields)
        {
            foreach (var leaf in LeavesOf(field))
            {
                yield return leaf;
            }
        }
    }

    private IEnumerable<(Leaf Leaf, Table? Table)> LeavesWithTables()
    {
        foreach (var field in Info.Fields)
        {
            var table = field is ExtractionTableField ? _tables.GetValueOrDefault(field.Key) : null;
            foreach (var leaf in LeavesOf(field))
            {
                yield return (leaf, table);
            }
        }
    }

    /// <summary>A field's value or a table cell, with what the store keeps beside it.</summary>
    private sealed class Leaf
    {
        public Leaf(ExtractionValue value, ExtractionField field, ExtractionColumn? column, int row)
        {
            Value = value;
            Field = field;
            Column = column;
            Row = row;
        }

        public ExtractionValue Value { get; }

        public ExtractionField Field { get; }

        public ExtractionColumn? Column { get; }

        public int Row { get; }

        /// <summary>The type the text is parsed by: the column's for a cell.</summary>
        public ExtractionFieldType Type => Column?.Type ?? Field.Type;

        public ExtractionValueParser.ErrorKind Error { get; set; }

        /// <summary>Whether the model filled it (its confidence counts in the average).</summary>
        public bool FromModel { get; set; }

        /// <summary>The status before Accept all, until it is committed or undone.</summary>
        public ExtractionValueStatus? Undo { get; set; }

        public bool UndoNeedsCheck { get; set; }
    }

    /// <summary>A table field: its rows of leaves, the same rows as values for the grid, and its summary value.</summary>
    private sealed class Table
    {
        public Table(ExtractionTableField field)
        {
            Field = field;
            Summary = new ExtractionValue(field.Key);
        }

        public ExtractionTableField Field { get; }

        public List<Leaf[]> Leaves { get; } = new();

        public AvaloniaList<AvaloniaList<ExtractionValue>> Rows { get; } = new();

        public ExtractionValue Summary { get; }
    }
}
