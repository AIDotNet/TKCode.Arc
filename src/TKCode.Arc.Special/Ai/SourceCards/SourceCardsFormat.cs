using System.Globalization;

namespace TKCode.Arc;

/// <summary>What one cell of the <see cref="SourceCards"/> grid shows.</summary>
internal enum SourceCardsCellKind
{
    /// <summary>The card of an arrived source.</summary>
    Card,

    /// <summary>A reserved slot for a source still on its way.</summary>
    Skeleton,

    /// <summary>The stack of the sources past <see cref="SourceCards.Max"/>.</summary>
    Stack
}

/// <summary>One cell of the grid: its kind and, for a card or a slot, the 0-based source index.</summary>
internal readonly record struct SourceCardsCell(SourceCardsCellKind Kind, int Index);

/// <summary>
/// The pure rules of <see cref="SourceCards"/>: the domain and the lettered mark of an address, the time format, the
/// columns at a width and which cell shows what. No state, no UI.
/// </summary>
internal static class SourceCardsFormat
{
    /// <summary>Below this width (px) the grid has two columns (Arc's container query).</summary>
    public const double NarrowWidth = 540;

    /// <summary>The host of <paramref name="url"/> without "www.", or the text itself when it is not an address.</summary>
    public static string Domain(string? url)
    {
        if (string.IsNullOrWhiteSpace(url))
        {
            return string.Empty;
        }
        var text = url.Trim();
        if (!Uri.TryCreate(text, UriKind.Absolute, out var uri) || string.IsNullOrEmpty(uri.Host))
        {
            Uri.TryCreate("https://" + text, UriKind.Absolute, out uri);
        }
        var host = uri?.Host;
        if (string.IsNullOrEmpty(host))
        {
            return text;
        }
        return host.StartsWith("www.", StringComparison.OrdinalIgnoreCase) ? host[4..] : host;
    }

    /// <summary>
    /// The letter of the disc drawn for a source without an icon: the first letter of the site's name part of the domain
    /// ("developer.webref.example" gives W, "edgehost.example" gives E), or of the title when there is no domain.
    /// </summary>
    public static string Monogram(string? domain, string? title = null)
    {
        if (!string.IsNullOrEmpty(domain))
        {
            var parts = domain.Split('.', StringSplitOptions.RemoveEmptyEntries);
            var name = parts.Length >= 2 ? parts[^2] : parts.Length == 1 ? parts[0] : string.Empty;
            foreach (var c in name)
            {
                if (char.IsLetterOrDigit(c))
                {
                    return char.ToUpperInvariant(c).ToString();
                }
            }
        }
        foreach (var c in title ?? string.Empty)
        {
            if (char.IsLetterOrDigit(c))
            {
                return char.ToUpperInvariant(c).ToString();
            }
        }
        return "?";
    }

    /// <summary>Search time: tenths of a second under a minute ("2.1s"), then minutes and seconds ("1m 5s").</summary>
    public static string Duration(TimeSpan time)
    {
        if (time < TimeSpan.Zero)
        {
            time = TimeSpan.Zero;
        }
        var tenths = AiDuration.TenthsOfMilliseconds(AiDuration.WholeMilliseconds(time));
        if (tenths < AiDuration.TenthsPerMinute)
        {
            return AiDuration.Tenths(tenths);
        }
        return AiDuration.MinutesSeconds(AiDuration.WholeSecondsRounded(time));
    }

    /// <summary>The columns at <paramref name="width"/>: <paramref name="max"/> + 1, or two below <see cref="NarrowWidth"/>.</summary>
    public static int Columns(double width, int max, double narrowWidth = NarrowWidth)
    {
        var wide = Math.Max(1, max) + 1;
        return width < narrowWidth ? Math.Min(2, wide) : wide;
    }

    /// <summary>
    /// What each cell shows. <paramref name="arrived"/> sources are here and <paramref name="reserved"/> slots are held for
    /// the total (the larger of the two counts). Collapsed with more slots than <paramref name="max"/>, the first
    /// <paramref name="max"/> cells show their card or slot and the next cell the stack once a source past them has
    /// arrived (a slot until then). Expanded, or within <paramref name="max"/>, every slot has its cell.
    /// </summary>
    public static List<SourceCardsCell> Plan(int arrived, int reserved, int max, bool expanded)
    {
        max = Math.Max(1, max);
        arrived = Math.Max(0, arrived);
        var slots = Math.Max(arrived, reserved);
        var cells = new List<SourceCardsCell>(Math.Min(slots, 64) + 1);
        var shown = expanded || slots <= max ? slots : max;
        for (var i = 0; i < shown; i++)
        {
            cells.Add(new SourceCardsCell(i < arrived ? SourceCardsCellKind.Card : SourceCardsCellKind.Skeleton, i));
        }
        if (shown < slots)
        {
            cells.Add(arrived > max
                ? new SourceCardsCell(SourceCardsCellKind.Stack, max)
                : new SourceCardsCell(SourceCardsCellKind.Skeleton, max));
        }
        return cells;
    }

    /// <summary>The number of rows <paramref name="cells"/> cells take in <paramref name="columns"/> columns.</summary>
    public static int Rows(int cells, int columns) => cells <= 0 ? 0 : (cells + Math.Max(1, columns) - 1) / Math.Max(1, columns);

    /// <summary>Joins up to three site names for the stack ("WebRef, DevAnswers, Edgehost Docs"), with "…" past them.</summary>
    public static string Sites(IEnumerable<string> names)
    {
        var list = names.Where(n => !string.IsNullOrWhiteSpace(n)).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        var text = string.Join(", ", list.Take(3));
        return list.Count > 3 ? text + ", …" : text;
    }

    /// <summary>Invariant integer text.</summary>
    public static string Number(int value) => value.ToString(CultureInfo.InvariantCulture);

    /// <summary>
    /// The citation number of a word of a simulated answer ("[3]", "[6].", "[4];"), with the punctuation after it in
    /// <paramref name="trailing"/>, or null for a plain word.
    /// </summary>
    public static int? CitationOf(string token, out string trailing)
    {
        trailing = string.Empty;
        if (token.Length < 3 || token[0] != '[')
        {
            return null;
        }
        var close = token.IndexOf(']');
        if (close < 2 || !int.TryParse(token.AsSpan(1, close - 1), NumberStyles.None, CultureInfo.InvariantCulture, out var n))
        {
            return null;
        }
        trailing = token[(close + 1)..];
        return n;
    }
}
