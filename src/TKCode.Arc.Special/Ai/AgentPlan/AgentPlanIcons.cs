using Avalonia.Media;

namespace TKCode.Arc;

/// <summary>
/// Lucide glyphs the plan needs that Core <see cref="ArcIcons"/> does not ship (24×24, stroked). Path data is
/// lucide-static 1.59.0 as converted by <c>tools/lucide/generate_lucide.py</c>, parsed once through
/// <see cref="ArcIcons.Parse"/>.
/// </summary>
internal static class AgentPlanIcons
{
    // Lucide file-pen, ISC.
    private const string FilePenData =
        "M12.659 22H18a2 2 0 0 0 2-2V8a2.4 2.4 0 0 0-.706-1.706l-3.588-3.588A2.4 2.4 0 0 0 14 2H6a2 2 0 0 0-2 2v9.34 " +
        "M14 2v5a1 1 0 0 0 1 1h5 " +
        "M10.378 12.622a1 1 0 0 1 3 3.003L8.36 20.637a2 2 0 0 1-.854.506l-2.867.837a.5.5 0 0 1-.62-.62l.836-2.869a2 2 0 0 1 .506-.853z";

    // Lucide square-terminal, ISC.
    private const string SquareTerminalData =
        "M7 11l2-2-2-2 M11 13h4 M5 3h14a2 2 0 0 1 2 2v14a2 2 0 0 1 -2 2h-14a2 2 0 0 1 -2 -2v-14a2 2 0 0 1 2 -2z";

    /// <summary>A page with a pen: a file was edited.</summary>
    public static Geometry FilePen => ArcIcons.Parse(FilePenData);

    /// <summary>A terminal prompt in a square: a command ran.</summary>
    public static Geometry SquareTerminal => ArcIcons.Parse(SquareTerminalData);
}

/// <summary>
/// Picks the 12px icon of an activity line from its first word: Read (a page), Searched and Found (a magnifier), Created
/// (a page with a plus), Edited (a page with a pen), Ran (a terminal), Measured (a gauge). Other verbs get a pulse line.
/// Register more verbs with <see cref="Set"/> before the plan is shown; matching ignores case.
/// </summary>
public static class AgentPlanActivityIcons
{
    private static readonly Dictionary<string, Geometry> s_icons = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Read"] = ArcIcons.FileText,
        ["Searched"] = ArcIcons.Search,
        ["Found"] = ArcIcons.Search,
        ["Created"] = ArcIcons.FilePlus,
        ["Edited"] = AgentPlanIcons.FilePen,
        ["Ran"] = AgentPlanIcons.SquareTerminal,
        ["Measured"] = ArcIcons.Gauge
    };

    /// <summary>The icon of lines whose first word has none registered.</summary>
    public static Geometry Fallback => ArcIcons.Activity;

    /// <summary>The icon for <paramref name="activity"/>, chosen by its first word.</summary>
    public static Geometry Get(string? activity)
    {
        var verb = FirstWord(activity);
        return verb.Length > 0 && s_icons.TryGetValue(verb, out var icon) ? icon : Fallback;
    }

    /// <summary>Registers (or replaces) the icon of lines that start with <paramref name="verb"/>; a 24 × 24 stroked glyph.</summary>
    public static void Set(string verb, Geometry icon)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(verb);
        ArgumentNullException.ThrowIfNull(icon);
        s_icons[verb.Trim()] = icon;
    }

    private static string FirstWord(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return string.Empty;
        }
        var span = text.AsSpan().TrimStart();
        var end = 0;
        while (end < span.Length && char.IsLetter(span[end]))
        {
            end++;
        }
        return span[..end].ToString();
    }
}
