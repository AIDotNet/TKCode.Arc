using System.Globalization;

namespace TKCode.Arc;

/// <summary>
/// WebVTT file parser (W3C WebVTT, https://www.w3.org/TR/webvtt1/), simplified for captions: header, NOTE / STYLE / REGION
/// blocks skipped, cue identifier, timing line, the <c>line</c> / <c>position</c> / <c>size</c> / <c>align</c> settings
/// (<c>vertical</c> and <c>region</c> ignored with one warning each) and the payload (<see cref="CaptionCueText"/>).
/// </summary>
internal static class WebVttParser
{
    /// <summary>Parses normalized text (no BOM, <c>\n</c> line ends) that starts with <c>WEBVTT</c>.</summary>
    public static VideoCaptionParseResult Parse(string[] lines)
    {
        var cues = new List<VideoCue>();
        var warnings = new List<string>();
        var flags = new ParseContext();
        var i = 1;
        // The rest of the header block (metadata lines); a timing line ends it early.
        while (i < lines.Length && !IsBlank(lines[i]) && !lines[i].Contains("-->", StringComparison.Ordinal))
        {
            i++;
        }
        while (i < lines.Length)
        {
            if (IsBlank(lines[i]))
            {
                i++;
                continue;
            }
            var first = lines[i];
            if (first.Contains("-->", StringComparison.Ordinal))
            {
                i = ReadCue(lines, i, null, cues, warnings, flags);
                continue;
            }
            if (i + 1 < lines.Length && lines[i + 1].Contains("-->", StringComparison.Ordinal))
            {
                i = ReadCue(lines, i + 1, first.Trim(), cues, warnings, flags);
                continue;
            }
            if (IsBlockKeyword(first, "NOTE") || IsBlockKeyword(first, "STYLE"))
            {
                i = SkipBlock(lines, i);
                continue;
            }
            if (IsBlockKeyword(first, "REGION"))
            {
                if (!flags.Region)
                {
                    flags.Region = true;
                    warnings.Add($"line {i + 1}: regions are not supported; region blocks and cue region settings are ignored");
                }
                i = SkipBlock(lines, i);
                continue;
            }
            warnings.Add($"line {i + 1}: text outside a cue skipped");
            i = SkipBlock(lines, i);
        }
        return new VideoCaptionParseResult(cues, warnings);
    }

    private static int ReadCue(string[] lines, int timingLine, string? id, List<VideoCue> cues, List<string> warnings, ParseContext flags)
    {
        var payloadEnd = timingLine + 1;
        while (payloadEnd < lines.Length && !IsBlank(lines[payloadEnd]) && !lines[payloadEnd].Contains("-->", StringComparison.Ordinal))
        {
            payloadEnd++;
        }
        var lineNumber = timingLine + 1;
        var timing = lines[timingLine].AsSpan();
        var pos = 0;
        SkipWhitespace(timing, ref pos);
        if (!CaptionTimestamps.TryReadVtt(timing, ref pos, out var start))
        {
            warnings.Add($"line {lineNumber}: bad start time, cue skipped");
            return payloadEnd;
        }
        SkipWhitespace(timing, ref pos);
        if (!timing[pos..].StartsWith("-->", StringComparison.Ordinal))
        {
            warnings.Add($"line {lineNumber}: missing '-->', cue skipped");
            return payloadEnd;
        }
        pos += 3;
        SkipWhitespace(timing, ref pos);
        if (!CaptionTimestamps.TryReadVtt(timing, ref pos, out var end))
        {
            warnings.Add($"line {lineNumber}: bad end time, cue skipped");
            return payloadEnd;
        }
        if (pos < timing.Length && !IsWhitespace(timing[pos]))
        {
            warnings.Add($"line {lineNumber}: unexpected text after the end time, cue skipped");
            return payloadEnd;
        }
        if (end <= start)
        {
            warnings.Add($"line {lineNumber}: cue ends at or before its start, skipped");
            return payloadEnd;
        }
        var cue = new VideoCue { Id = string.IsNullOrEmpty(id) ? null : id, Start = start, End = end, Text = string.Empty };
        cue = ApplySettings(cue, timing[pos..], lineNumber, warnings, flags);
        var payload = string.Join('\n', lines, timingLine + 1, payloadEnd - timingLine - 1);
        var (text, runs) = CaptionCueText.Parse(payload, srt: false, flags.Buffers);
        cues.Add(cue with { Text = text, Runs = runs });
        return payloadEnd;
    }

    private static VideoCue ApplySettings(VideoCue cue, ReadOnlySpan<char> settings, int lineNumber, List<string> warnings, ParseContext flags)
    {
        var p = 0;
        while (true)
        {
            SkipWhitespace(settings, ref p);
            if (p >= settings.Length)
            {
                return cue;
            }
            var startAt = p;
            while (p < settings.Length && !IsWhitespace(settings[p]))
            {
                p++;
            }
            var token = settings[startAt..p];
            var colon = token.IndexOf(':');
            if (colon <= 0 || colon == token.Length - 1)
            {
                warnings.Add($"line {lineNumber}: malformed cue setting '{token}' ignored");
                continue;
            }
            var name = token[..colon];
            var value = token[(colon + 1)..];
            switch (name)
            {
                case "vertical":
                    if (!flags.Vertical)
                    {
                        flags.Vertical = true;
                        warnings.Add($"line {lineNumber}: vertical text is not supported; cues are drawn horizontally");
                    }
                    break;
                case "region":
                    if (!flags.Region)
                    {
                        flags.Region = true;
                        warnings.Add($"line {lineNumber}: regions are not supported; region blocks and cue region settings are ignored");
                    }
                    break;
                case "line":
                    if (TryLine(value, out var line, out var percent))
                    {
                        cue = cue with { Line = line, LineIsPercent = percent };
                    }
                    else
                    {
                        warnings.Add($"line {lineNumber}: invalid line '{value}' ignored");
                    }
                    break;
                case "position":
                {
                    var comma = value.IndexOf(',');
                    var number = comma >= 0 ? value[..comma] : value;
                    var alignOk = comma < 0 || value[(comma + 1)..] is "line-left" or "center" or "line-right";
                    if (alignOk && TryPercent(number, out var position))
                    {
                        cue = cue with { Position = position };
                    }
                    else
                    {
                        warnings.Add($"line {lineNumber}: invalid position '{value}' ignored");
                    }
                    break;
                }
                case "size":
                    if (TryPercent(value, out var size))
                    {
                        cue = cue with { Size = size };
                    }
                    else
                    {
                        warnings.Add($"line {lineNumber}: invalid size '{value}' ignored");
                    }
                    break;
                case "align":
                    VideoCueAlign? align = value switch
                    {
                        "start" => VideoCueAlign.Start,
                        "center" or "middle" => VideoCueAlign.Center,
                        "end" => VideoCueAlign.End,
                        "left" => VideoCueAlign.Left,
                        "right" => VideoCueAlign.Right,
                        _ => null,
                    };
                    if (align is { } a)
                    {
                        cue = cue with { Align = a };
                    }
                    else
                    {
                        warnings.Add($"line {lineNumber}: invalid align '{value}' ignored");
                    }
                    break;
                default:
                    warnings.Add($"line {lineNumber}: unknown cue setting '{name}' ignored");
                    break;
            }
        }
    }

    /// <summary>WebVTT <c>line</c>: <c>N%</c> or a (possibly negative) number, optionally <c>,start|center|end</c> (line alignment, not modeled).</summary>
    private static bool TryLine(ReadOnlySpan<char> value, out double line, out bool percent)
    {
        line = 0;
        percent = false;
        var comma = value.IndexOf(',');
        if (comma >= 0)
        {
            if (value[(comma + 1)..] is not ("start" or "center" or "end"))
            {
                return false;
            }
            value = value[..comma];
        }
        if (value.Length > 0 && value[^1] == '%')
        {
            percent = true;
            return TryPercent(value, out line);
        }
        var negative = value.Length > 0 && value[0] == '-';
        var digits = negative ? value[1..] : value;
        if (!IsDecimal(digits))
        {
            return false;
        }
        line = double.Parse(digits, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture) * (negative ? -1 : 1);
        return true;
    }

    /// <summary>A WebVTT percentage: digits with at most one '.', then '%', between 0 and 100.</summary>
    private static bool TryPercent(ReadOnlySpan<char> value, out double percent)
    {
        percent = 0;
        if (value.Length < 2 || value[^1] != '%' || !IsDecimal(value[..^1]))
        {
            return false;
        }
        percent = double.Parse(value[..^1], NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture);
        return percent is >= 0 and <= 100;
    }

    private static bool IsDecimal(ReadOnlySpan<char> s)
    {
        if (s.Length == 0 || s.Length > 16)
        {
            return false;
        }
        var digits = 0;
        var dots = 0;
        foreach (var c in s)
        {
            if (char.IsAsciiDigit(c))
            {
                digits++;
            }
            else if (c == '.' && dots == 0)
            {
                dots++;
            }
            else
            {
                return false;
            }
        }
        return digits > 0;
    }

    private static bool IsBlockKeyword(string line, string keyword) =>
        line.StartsWith(keyword, StringComparison.Ordinal) && (line.Length == keyword.Length || IsWhitespace(line[keyword.Length]));

    private static int SkipBlock(string[] lines, int i)
    {
        while (i < lines.Length && !IsBlank(lines[i]))
        {
            i++;
        }
        return i;
    }

    internal static bool IsBlank(string line) => string.IsNullOrWhiteSpace(line);

    private static bool IsWhitespace(char c) => c is ' ' or '\t' or '\f';

    private static void SkipWhitespace(ReadOnlySpan<char> s, ref int p)
    {
        while (p < s.Length && IsWhitespace(s[p]))
        {
            p++;
        }
    }

    private sealed class ParseContext
    {
        public bool Vertical;
        public bool Region;

        public readonly CaptionCueText.State Buffers = new();
    }
}
