using System.Collections.Generic;
using System.Globalization;

namespace TKCode.Arc;

/// <summary>
/// A section of the form: a group with its fields, or a field that has no group and stands alone. Sections are in the order
/// their first field appears in the schema; <see cref="Index"/> picks the series color (<see cref="ExtractionSchemaInfo.ToneOf(ExtractionField)"/>).
/// </summary>
/// <param name="Index">The position among the sections.</param>
/// <param name="Group">The group, or null for a field of its own.</param>
/// <param name="Fields">The fields in schema order.</param>
internal sealed record ExtractionSection(int Index, ExtractionGroup? Group, IReadOnlyList<ExtractionField> Fields);

/// <summary>
/// What the form needs to know about a schema, read once: the fields in order, the sections they fall into and the tone of
/// each, how a value path resolves to a field (and a table's row and column), and how many values there are to fill.
/// </summary>
/// <remarks>
/// The schema's collections are copied when the info is made; make a new one when the schema changes. Paths are a field key
/// (<c>vendor</c>) or, for a table cell, <c>key.row.column</c> with the row counted from 0 and written without leading zeros
/// (<c>lineItems.0.amount</c>). Tones follow spec 2.1: each section takes the next of the four series colors in turn
/// (<c>Index % 4 + 1</c>), so groups are colored in the order they appear and, in a schema without groups, every field has a
/// color of its own; a field whose group key is not declared counts as having no group, and a declared group without
/// fields takes no color.
/// </remarks>
internal sealed class ExtractionSchemaInfo
{
    private readonly ExtractionField[] _fields;
    private readonly Dictionary<string, ExtractionField> _byKey = new(StringComparer.Ordinal);
    private readonly Dictionary<ExtractionField, int> _sectionOf = new(ReferenceEqualityComparer.Instance);
    private readonly Dictionary<ExtractionField, int> _indexOf = new(ReferenceEqualityComparer.Instance);
    private readonly ExtractionSection[] _sections;

    /// <summary>Reads <paramref name="schema"/>.</summary>
    public ExtractionSchemaInfo(ExtractionSchema schema)
    {
        Schema = schema;
        _fields = schema.Fields.ToArray();
        var groups = new Dictionary<string, ExtractionGroup>(StringComparer.Ordinal);
        foreach (var group in schema.Groups)
        {
            groups.TryAdd(group.Key, group);
        }
        var sections = new List<(ExtractionGroup? Group, List<ExtractionField> Fields)>();
        var sectionOfGroup = new Dictionary<ExtractionGroup, int>(ReferenceEqualityComparer.Instance);
        for (var i = 0; i < _fields.Length; i++)
        {
            var field = _fields[i];
            _indexOf[field] = i;
            _byKey.TryAdd(field.Key, field);
            if (field is ExtractionTableField)
            {
                TableCount++;
            }
            else
            {
                ScalarCount++;
            }
            if (field.Group is { } key && groups.TryGetValue(key, out var group))
            {
                if (!sectionOfGroup.TryGetValue(group, out var section))
                {
                    section = sections.Count;
                    sectionOfGroup[group] = section;
                    sections.Add((group, new List<ExtractionField>()));
                }
                sections[section].Fields.Add(field);
                _sectionOf[field] = section;
            }
            else
            {
                _sectionOf[field] = sections.Count;
                sections.Add((null, new List<ExtractionField> { field }));
            }
        }
        _sections = new ExtractionSection[sections.Count];
        for (var i = 0; i < sections.Count; i++)
        {
            _sections[i] = new ExtractionSection(i, sections[i].Group, sections[i].Fields.ToArray());
        }
    }

    /// <summary>The schema this was read from.</summary>
    public ExtractionSchema Schema { get; }

    /// <summary>The fields in schema order.</summary>
    public IReadOnlyList<ExtractionField> Fields => _fields;

    /// <summary>The form's sections in order.</summary>
    public IReadOnlyList<ExtractionSection> Sections => _sections;

    /// <summary>How many fields are not tables.</summary>
    public int ScalarCount { get; }

    /// <summary>How many fields are tables.</summary>
    public int TableCount { get; }

    /// <summary>The field with <paramref name="key"/>, or null (the first one when keys repeat).</summary>
    public ExtractionField? FindField(string key) => _byKey.GetValueOrDefault(key);

    /// <summary>The position of <paramref name="field"/> in <see cref="Fields"/>, or -1.</summary>
    public int IndexOf(ExtractionField field) => _indexOf.GetValueOrDefault(field, -1);

    /// <summary>The section <paramref name="field"/> belongs to (its index in <see cref="Sections"/>), or -1 for a field of another schema.</summary>
    public int GroupIndexOf(ExtractionField field) => _sectionOf.GetValueOrDefault(field, -1);

    /// <summary>The group of <paramref name="field"/>, or null when it stands alone.</summary>
    public ExtractionGroup? GroupOf(ExtractionField field) => GroupIndexOf(field) is var i and >= 0 ? _sections[i].Group : null;

    /// <summary>The series tone (1 to 4) of <paramref name="field"/>: its section's index modulo four, counted from 1.</summary>
    public int ToneOf(ExtractionField field) => ToneOfSection(Math.Max(0, GroupIndexOf(field)));

    /// <summary>The tone of the field a path belongs to (a table key, a cell or a field key); 1 for an unknown path.</summary>
    public int ToneOf(string path) => FieldOfPath(path) is { } field ? ToneOf(field) : 1;

    /// <summary>The tone (1 to 4) of a section index.</summary>
    public static int ToneOfSection(int index) => index % 4 + 1;

    /// <summary>
    /// The field a path starts with: the field itself for a key, the table for <c>key.row.column</c> and also for
    /// <c>key.row</c> (a row of the JSON output); null when nothing matches.
    /// </summary>
    public ExtractionField? FieldOfPath(string path)
    {
        if (_byKey.TryGetValue(path, out var field))
        {
            return field;
        }
        var dot = path.IndexOf('.');
        return dot > 0 && _byKey.TryGetValue(path[..dot], out field) && field is ExtractionTableField ? field : null;
    }

    /// <summary>
    /// Resolves a value path: a field key gives that field (a table key gives the table, with no row and column); a
    /// <c>table.row.column</c> path gives the table, the row and the column. False for anything else (an unknown key or
    /// column, a row that is not a plain non-negative number, a cell path on a field that is not a table).
    /// </summary>
    public bool TryResolve(string path, out ExtractionField field, out ExtractionColumn? column, out int? row)
    {
        column = null;
        row = null;
        if (_byKey.TryGetValue(path, out var direct))
        {
            field = direct;
            return true;
        }
        field = null!;
        var first = path.IndexOf('.');
        var second = first < 0 ? -1 : path.IndexOf('.', first + 1);
        if (second < 0 || !_byKey.TryGetValue(path[..first], out var table) || table is not ExtractionTableField tableField)
        {
            return false;
        }
        var rowText = path.AsSpan(first + 1, second - first - 1);
        if (!TryParseRow(rowText, out var index))
        {
            return false;
        }
        var columnKey = path[(second + 1)..];
        foreach (var candidate in tableField.Columns)
        {
            if (string.Equals(candidate.Key, columnKey, StringComparison.Ordinal))
            {
                field = tableField;
                column = candidate;
                row = index;
                return true;
            }
        }
        return false;
    }

    /// <summary>The path of a table cell.</summary>
    public static string CellPath(string tableKey, int row, string columnKey) =>
        tableKey + "." + row.ToString(CultureInfo.InvariantCulture) + "." + columnKey;

    /// <summary>The path of a table row (as the JSON output's row objects are addressed).</summary>
    public static string RowPath(string tableKey, int row) => tableKey + "." + row.ToString(CultureInfo.InvariantCulture);

    /// <summary>
    /// The number of values to fill: one per field that is not a table, and for each table its cells (rows × columns), or 1
    /// while it has no rows (the table is then one expected value; the number of rows is not known before they arrive).
    /// </summary>
    /// <param name="rowCount">How many rows a table has now.</param>
    public int LeafCount(Func<ExtractionTableField, int> rowCount)
    {
        var count = ScalarCount;
        foreach (var field in _fields)
        {
            if (field is ExtractionTableField table)
            {
                var rows = rowCount(table);
                count += rows > 0 ? rows * table.Columns.Count : 1;
            }
        }
        return count;
    }

    private static bool TryParseRow(ReadOnlySpan<char> text, out int row)
    {
        row = 0;
        // Plain digits without leading zeros, so that every cell has exactly one path.
        if (text.Length == 0 || text.Length > 9 || (text.Length > 1 && text[0] == '0'))
        {
            return false;
        }
        foreach (var c in text)
        {
            if (c is < '0' or > '9')
            {
                return false;
            }
            row = row * 10 + (c - '0');
        }
        return true;
    }
}
