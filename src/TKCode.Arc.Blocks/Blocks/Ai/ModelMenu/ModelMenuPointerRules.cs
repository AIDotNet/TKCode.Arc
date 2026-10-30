using Avalonia;

namespace TKCode.Arc;

/// <summary>
/// Menu typeahead: typed letters build a search that resets after a pause; the next row whose name starts with it gets
/// the highlight, searching from the current row onward and wrapping. Repeating one letter cycles through the rows that
/// start with it instead of searching for "aa". Pure logic with an injectable clock.
/// </summary>
internal sealed class ModelMenuTypeahead
{
    /// <summary>A pause this long starts a new search.</summary>
    public static readonly TimeSpan ResetAfter = TimeSpan.FromSeconds(1);

    private readonly Func<DateTime> _now;
    private string _buffer = string.Empty;
    private DateTime _last;

    public ModelMenuTypeahead(Func<DateTime>? now = null)
    {
        _now = now ?? (static () => DateTime.UtcNow);
    }

    /// <summary>The search typed so far (empty after a pause).</summary>
    public string Buffer => _buffer;

    /// <summary>Forgets the search (the menu closed or changed page).</summary>
    public void Reset() => _buffer = string.Empty;

    /// <summary>
    /// Adds <paramref name="text"/> to the search and returns the index of the row to highlight, or null when nothing
    /// matches (the highlight stays).
    /// </summary>
    /// <param name="text">The typed character(s).</param>
    /// <param name="names">The rows' names in order (null for a row that cannot be found by name).</param>
    /// <param name="current">The highlighted row, or −1.</param>
    public int? Next(string text, IReadOnlyList<string?> names, int current)
    {
        if (string.IsNullOrEmpty(text) || names.Count == 0)
        {
            return null;
        }
        var now = _now();
        _buffer = now - _last > ResetAfter ? text : _buffer + text;
        _last = now;
        var search = IsOneLetterRepeated(_buffer) ? _buffer[..1] : _buffer;
        // A one-letter search moves on from the current row; a longer one may stay on it while it still matches.
        var start = current < 0 ? 0 : search.Length == 1 ? current + 1 : current;
        for (var step = 0; step < names.Count; step++)
        {
            var index = (start + step) % names.Count;
            if (names[index] is { } name && name.StartsWith(search, StringComparison.CurrentCultureIgnoreCase))
            {
                return index;
            }
        }
        return null;
    }

    private static bool IsOneLetterRepeated(string text)
    {
        for (var i = 1; i < text.Length; i++)
        {
            if (char.ToUpperInvariant(text[i]) != char.ToUpperInvariant(text[0]))
            {
                return false;
            }
        }
        return true;
    }
}
