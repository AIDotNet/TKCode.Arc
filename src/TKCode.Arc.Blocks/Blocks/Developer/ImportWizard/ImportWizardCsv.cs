using System.Text;

namespace TKCode.Arc;

/// <summary>Why a CSV text could not be used.</summary>
internal enum ImportCsvProblem
{
    /// <summary>The text parsed.</summary>
    None,

    /// <summary>No text, or only blank lines.</summary>
    Empty,

    /// <summary>A header row but no data rows.</summary>
    NoRows,

    /// <summary>A quoted field is never closed.</summary>
    UnclosedQuote,

    /// <summary>More data rows than the limit.</summary>
    TooManyRows
}

/// <summary>A parsed CSV text: the header row and the data rows, every row padded or cut to the header's length.</summary>
internal sealed class ImportCsvTable
{
    public static readonly ImportCsvTable None = new(Array.Empty<string>(), Array.Empty<string[]>(), ',', ImportCsvProblem.Empty, 0);

    public ImportCsvTable(IReadOnlyList<string> headers, IReadOnlyList<string[]> rows, char delimiter, ImportCsvProblem problem, int totalRows)
    {
        Headers = headers;
        Rows = rows;
        Delimiter = delimiter;
        Problem = problem;
        TotalRows = totalRows;
    }

    /// <summary>The header row, trimmed; blank headers become "Column N".</summary>
    public IReadOnlyList<string> Headers { get; }

    /// <summary>The data rows (blank lines skipped), each exactly <see cref="Headers"/> long.</summary>
    public IReadOnlyList<string[]> Rows { get; }

    /// <summary>The delimiter that was detected (comma, semicolon or tab).</summary>
    public char Delimiter { get; }

    /// <summary>What was wrong, or <see cref="ImportCsvProblem.None"/>.</summary>
    public ImportCsvProblem Problem { get; }

    /// <summary>How many data rows the text has (also when <see cref="ImportCsvProblem.TooManyRows"/>).</summary>
    public int TotalRows { get; }

    /// <summary>True when the table can be mapped.</summary>
    public bool IsUsable => Problem == ImportCsvProblem.None;
}

/// <summary>
/// An RFC 4180 CSV reader: quoted fields with doubled quotes, line breaks inside quotes, CRLF / LF / CR row ends, a UTF-8
/// byte order mark, and the delimiter detected from the header row (comma, semicolon or tab). Fields are trimmed of
/// surrounding spaces outside quotes. Blank lines are skipped.
/// </summary>
internal static class ImportWizardCsv
{
    /// <summary>Parses <paramref name="text"/>; more than <paramref name="maxRows"/> data rows is <see cref="ImportCsvProblem.TooManyRows"/>.</summary>
    public static ImportCsvTable Parse(string? text, int maxRows = 5000)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return ImportCsvTable.None;
        }
        if (text[0] == '﻿')
        {
            text = text[1..];
        }
        var delimiter = DetectDelimiter(text);
        var records = new List<string[]>();
        var problem = ReadRecords(text, delimiter, records);
        if (problem != ImportCsvProblem.None)
        {
            return new ImportCsvTable(Array.Empty<string>(), Array.Empty<string[]>(), delimiter, problem, 0);
        }
        if (records.Count == 0)
        {
            return ImportCsvTable.None;
        }
        var header = records[0];
        var headers = new string[header.Length];
        for (var i = 0; i < header.Length; i++)
        {
            headers[i] = string.IsNullOrWhiteSpace(header[i]) ? $"Column {i + 1}" : header[i].Trim();
        }
        var rows = new List<string[]>(Math.Max(0, records.Count - 1));
        for (var r = 1; r < records.Count; r++)
        {
            var source = records[r];
            var row = new string[headers.Length];
            for (var c = 0; c < row.Length; c++)
            {
                row[c] = c < source.Length ? source[c] : string.Empty;
            }
            rows.Add(row);
        }
        if (rows.Count == 0)
        {
            return new ImportCsvTable(headers, rows, delimiter, ImportCsvProblem.NoRows, 0);
        }
        if (rows.Count > maxRows)
        {
            return new ImportCsvTable(headers, Array.Empty<string[]>(), delimiter, ImportCsvProblem.TooManyRows, rows.Count);
        }
        return new ImportCsvTable(headers, rows, delimiter, ImportCsvProblem.None, rows.Count);
    }

    /// <summary>The delimiter that occurs most often outside quotes in the first line (comma on a tie or none).</summary>
    internal static char DetectDelimiter(string text)
    {
        int commas = 0, semicolons = 0, tabs = 0;
        var quoted = false;
        foreach (var ch in text)
        {
            if (ch == '"')
            {
                quoted = !quoted;
                continue;
            }
            if (quoted)
            {
                continue;
            }
            if (ch is '\n' or '\r')
            {
                break;
            }
            switch (ch)
            {
                case ',':
                    commas++;
                    break;
                case ';':
                    semicolons++;
                    break;
                case '\t':
                    tabs++;
                    break;
            }
        }
        if (tabs > commas && tabs >= semicolons)
        {
            return '\t';
        }
        if (semicolons > commas)
        {
            return ';';
        }
        return ',';
    }

    private static ImportCsvProblem ReadRecords(string text, char delimiter, List<string[]> records)
    {
        var fields = new List<string>();
        var field = new StringBuilder();
        var quoted = false;
        var wasQuoted = false;
        var i = 0;
        var n = text.Length;
        while (i < n)
        {
            var ch = text[i];
            if (quoted)
            {
                if (ch == '"')
                {
                    if (i + 1 < n && text[i + 1] == '"')
                    {
                        field.Append('"');
                        i += 2;
                        continue;
                    }
                    quoted = false;
                    i++;
                    continue;
                }
                field.Append(ch);
                i++;
                continue;
            }
            if (ch == '"' && (field.Length == 0 || IsBlank(field)))
            {
                field.Clear();
                quoted = true;
                wasQuoted = true;
                i++;
                continue;
            }
            if (ch == delimiter)
            {
                fields.Add(Finish(field, wasQuoted));
                field.Clear();
                wasQuoted = false;
                i++;
                continue;
            }
            if (ch is '\r' or '\n')
            {
                fields.Add(Finish(field, wasQuoted));
                field.Clear();
                wasQuoted = false;
                AddRecord(records, fields);
                fields.Clear();
                i += ch == '\r' && i + 1 < n && text[i + 1] == '\n' ? 2 : 1;
                continue;
            }
            field.Append(ch);
            i++;
        }
        if (quoted)
        {
            return ImportCsvProblem.UnclosedQuote;
        }
        if (field.Length > 0 || fields.Count > 0 || wasQuoted)
        {
            fields.Add(Finish(field, wasQuoted));
            AddRecord(records, fields);
        }
        return ImportCsvProblem.None;
    }

    private static void AddRecord(List<string[]> records, List<string> fields)
    {
        // A blank line (one empty field) is not a record.
        if (fields.Count == 1 && fields[0].Length == 0)
        {
            return;
        }
        records.Add(fields.ToArray());
    }

    private static string Finish(StringBuilder field, bool wasQuoted)
    {
        // Text after a closing quote is kept (lenient); unquoted fields are trimmed.
        return wasQuoted ? field.ToString() : field.ToString().Trim();
    }

    private static bool IsBlank(StringBuilder field)
    {
        for (var i = 0; i < field.Length; i++)
        {
            if (!char.IsWhiteSpace(field[i]))
            {
                return false;
            }
        }
        return true;
    }
}
