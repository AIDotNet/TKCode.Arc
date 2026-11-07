namespace TKCode.Arc;

/// <summary>
/// SubRip (SRT) parser: an optional index line, a <c>HH:MM:SS,mmm --> HH:MM:SS,mmm</c> timing line (a '.' separator and
/// trailing coordinates tolerated) and text lines up to a blank line; converted to the same <see cref="VideoCue"/> model as WebVTT.
/// </summary>
internal static class SrtParser
{
    /// <summary>Parses normalized text (no BOM, <c>\n</c> line ends).</summary>
    public static VideoCaptionParseResult Parse(string[] lines)
    {
        var cues = new List<VideoCue>();
        var warnings = new List<string>();
        var buffers = new CaptionCueText.State();
        var i = 0;
        while (i < lines.Length)
        {
            if (WebVttParser.IsBlank(lines[i]))
            {
                i++;
                continue;
            }
            int timingLine;
            string? id;
            if (lines[i].Contains("-->", StringComparison.Ordinal))
            {
                timingLine = i;
                id = null;
            }
            else if (i + 1 < lines.Length && lines[i + 1].Contains("-->", StringComparison.Ordinal))
            {
                timingLine = i + 1;
                id = lines[i].Trim();
            }
            else
            {
                warnings.Add($"line {i + 1}: expected a cue number or a timing line, block skipped");
                while (i < lines.Length && !WebVttParser.IsBlank(lines[i]))
                {
                    i++;
                }
                continue;
            }
            var textEnd = timingLine + 1;
            while (textEnd < lines.Length && !WebVttParser.IsBlank(lines[textEnd]) && !StartsNextCue(lines, textEnd))
            {
                textEnd++;
            }
            i = textEnd;
            if (!TryTiming(lines[timingLine], out var start, out var end))
            {
                warnings.Add($"line {timingLine + 1}: bad timing line, cue skipped");
                continue;
            }
            if (end <= start)
            {
                warnings.Add($"line {timingLine + 1}: cue ends at or before its start, skipped");
                continue;
            }
            var payload = string.Join('\n', lines, timingLine + 1, textEnd - timingLine - 1);
            var (text, runs) = CaptionCueText.Parse(payload, srt: true, buffers);
            cues.Add(new VideoCue { Id = string.IsNullOrEmpty(id) ? null : id, Start = start, End = end, Text = text, Runs = runs });
        }
        return new VideoCaptionParseResult(cues, warnings);
    }

    /// <summary>A text line that is a cue number followed by a timing line: the blank line between two cues is missing.</summary>
    private static bool StartsNextCue(string[] lines, int at)
    {
        if (at + 1 >= lines.Length || !lines[at + 1].Contains("-->", StringComparison.Ordinal))
        {
            return lines[at].Contains("-->", StringComparison.Ordinal);
        }
        foreach (var c in lines[at].AsSpan().Trim())
        {
            if (!char.IsAsciiDigit(c))
            {
                return false;
            }
        }
        return true;
    }

    private static bool TryTiming(string line, out TimeSpan start, out TimeSpan end)
    {
        end = default;
        var s = line.AsSpan();
        var p = 0;
        SkipSpaces(s, ref p);
        if (!CaptionTimestamps.TryReadSrt(s, ref p, out start))
        {
            return false;
        }
        SkipSpaces(s, ref p);
        if (!s[p..].StartsWith("-->", StringComparison.Ordinal))
        {
            return false;
        }
        p += 3;
        SkipSpaces(s, ref p);
        if (!CaptionTimestamps.TryReadSrt(s, ref p, out end))
        {
            return false;
        }
        // Anything after the end time (X1:... Y2:... coordinates) is ignored.
        return p >= s.Length || s[p] is ' ' or '\t';
    }

    private static void SkipSpaces(ReadOnlySpan<char> s, ref int p)
    {
        while (p < s.Length && s[p] is ' ' or '\t')
        {
            p++;
        }
    }
}
