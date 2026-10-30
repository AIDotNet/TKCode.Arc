using System.Text;

namespace TKCode.Arc;

/// <summary>Pure text helpers of <see cref="ReasoningBlock"/>.</summary>
internal static class ReasoningBlockText
{
    /// <summary>
    /// The tail of the latest reasoning for the streaming preview: whitespace folded to single spaces, and when longer than
    /// <paramref name="max"/> characters the last ones from a word start, behind an ellipsis, so the newest words are what
    /// shows.
    /// </summary>
    public static string Tail(string? text, int max)
    {
        if (string.IsNullOrWhiteSpace(text) || max <= 0)
        {
            return string.Empty;
        }
        var folded = new StringBuilder(text.Length);
        var space = false;
        foreach (var c in text.AsSpan().Trim())
        {
            if (char.IsWhiteSpace(c))
            {
                space = true;
                continue;
            }
            if (space && folded.Length > 0)
            {
                folded.Append(' ');
            }
            space = false;
            folded.Append(c);
        }
        if (folded.Length <= max)
        {
            return folded.ToString();
        }
        var start = folded.Length - max;
        var whole = folded.ToString();
        var gap = whole.IndexOf(' ', start);
        return "…" + (gap < 0 ? whole[start..] : whole[(gap + 1)..]);
    }

    /// <summary>
    /// Splits streamed reasoning into steps: blank lines split paragraphs, and a line written as <c>**Heading**</c> or
    /// <c>## Heading</c> starts a new step. A heading still streaming in (an opening <c>**</c> with no closing one on the
    /// last line) is left out until it is complete, so raw asterisks never show.
    /// </summary>
    public static List<(string? Heading, string Text)> Parse(string? reasoning)
    {
        var steps = new List<(string? Heading, string Text)>();
        if (string.IsNullOrWhiteSpace(reasoning))
        {
            return steps;
        }
        var lines = reasoning.Replace("\r\n", "\n").Split('\n');
        string? heading = null;
        var body = new StringBuilder();
        var started = false;

        void Flush()
        {
            if (started || body.Length > 0)
            {
                steps.Add((heading, body.ToString().Trim()));
            }
            heading = null;
            body.Clear();
            started = false;
        }

        for (var i = 0; i < lines.Length; i++)
        {
            var line = lines[i].Trim();
            var isLast = i == lines.Length - 1;
            if (line.Length == 0)
            {
                if (body.Length > 0 && body[^1] != '\n')
                {
                    body.Append("\n\n");
                }
                continue;
            }
            if (TryHeading(line, out var title))
            {
                Flush();
                heading = title;
                started = true;
                continue;
            }
            if (isLast && line.StartsWith("**", StringComparison.Ordinal))
            {
                // A heading that has not finished streaming.
                Flush();
                started = false;
                continue;
            }
            if (body.Length > 0 && body[^1] != '\n')
            {
                body.Append(' ');
            }
            body.Append(line);
        }
        Flush();
        return steps;
    }

    /// <summary>The reasoning as plain text for the clipboard: each step's heading over its text, steps apart by a blank line.</summary>
    public static string PlainText(IEnumerable<ReasoningStep> steps)
    {
        var text = new StringBuilder();
        foreach (var step in steps)
        {
            if (text.Length > 0)
            {
                text.Append("\n\n");
            }
            if (!string.IsNullOrEmpty(step.Heading))
            {
                text.Append(step.Heading);
                if (!string.IsNullOrEmpty(step.Text))
                {
                    text.Append('\n');
                }
            }
            text.Append(step.Text);
        }
        return text.ToString();
    }

    private static bool TryHeading(string line, out string title)
    {
        title = string.Empty;
        if (line.StartsWith("## ", StringComparison.Ordinal))
        {
            title = line[3..].Trim();
            return title.Length > 0;
        }
        if (line.Length > 4 && line.StartsWith("**", StringComparison.Ordinal) && line.EndsWith("**", StringComparison.Ordinal))
        {
            title = line[2..^2].Trim();
            return title.Length > 0;
        }
        return false;
    }
}
