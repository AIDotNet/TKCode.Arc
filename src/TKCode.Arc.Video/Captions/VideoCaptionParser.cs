using System.Globalization;
using System.Text;

namespace TKCode.Arc;

/// <summary>The cues and warnings from parsing a captions file.</summary>
internal sealed record VideoCaptionParseResult(IReadOnlyList<VideoCue> Cues, IReadOnlyList<string> Warnings);

/// <summary>
/// Parses WebVTT and SRT text into <see cref="VideoCue"/>s (design/components/video/video.md section 9).
/// </summary>
internal static class VideoCaptionParser
{
    private static readonly UTF8Encoding s_strictUtf8 = new(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);

    /// <summary>Parses WebVTT (text starting with <c>WEBVTT</c>) or SRT; never throws, skips bad cues with a warning.</summary>
    public static VideoCaptionParseResult Parse(string text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return new VideoCaptionParseResult([], []);
        }
        try
        {
            var normalized = Normalize(text);
            var lines = normalized.Split('\n');
            var result = IsWebVtt(normalized) ? WebVttParser.Parse(lines) : SrtParser.Parse(lines);
            return result with { Cues = SortByStart(result.Cues) };
        }
        catch (Exception exception) when (exception is not OutOfMemoryException)
        {
            // Defensive: the parsers are written not to throw; a bug must not take the player down with it.
            return new VideoCaptionParseResult([], [$"line 1: the file could not be parsed ({exception.GetType().Name})"]);
        }
    }

    /// <summary>Decodes a captions file's bytes: by BOM, else UTF-8, else a legacy encoding chosen by the current culture.</summary>
    public static string Decode(ReadOnlySpan<byte> bytes) => Decode(bytes, CultureInfo.CurrentUICulture);

    /// <summary>
    /// Decodes with an explicit culture for the legacy fallback: a UTF-8 / UTF-16 BOM wins; else strict UTF-8; when that
    /// fails, GB18030 (zh-CN, zh-SG, zh-Hans), Big5 (zh-TW, zh-HK, zh-MO, zh-Hant), Shift_JIS (ja), code page 949 (ko) or
    /// Windows-1252 (everything else).
    /// </summary>
    internal static string Decode(ReadOnlySpan<byte> bytes, CultureInfo culture)
    {
        if (bytes.StartsWith((ReadOnlySpan<byte>)[0xEF, 0xBB, 0xBF]))
        {
            return Encoding.UTF8.GetString(bytes[3..]);
        }
        if (bytes.StartsWith((ReadOnlySpan<byte>)[0xFF, 0xFE]))
        {
            return Encoding.Unicode.GetString(bytes[2..]);
        }
        if (bytes.StartsWith((ReadOnlySpan<byte>)[0xFE, 0xFF]))
        {
            return Encoding.BigEndianUnicode.GetString(bytes[2..]);
        }
        try
        {
            return s_strictUtf8.GetString(bytes);
        }
        catch (DecoderFallbackException)
        {
            return LegacyEncoding(culture).GetString(bytes);
        }
    }

    /// <summary>The legacy code page for <paramref name="culture"/>; Latin-1 when the code pages are unavailable.</summary>
    internal static Encoding LegacyEncoding(CultureInfo culture)
    {
        var codePage = LegacyCodePage(culture);
        // CodePagesEncodingProvider ships in the shared framework (net8+, all RIDs, trimming and AOT safe). It is used
        // directly instead of Encoding.RegisterProvider so loading a captions file does not change the process-wide encodings.
        return System.Text.CodePagesEncodingProvider.Instance.GetEncoding(codePage) ?? Encoding.Latin1;
    }

    /// <summary>The Windows code page the culture's legacy subtitle files usually use.</summary>
    internal static int LegacyCodePage(CultureInfo culture)
    {
        for (var c = culture; !string.IsNullOrEmpty(c.Name); c = c.Parent)
        {
            switch (c.Name)
            {
                case "zh-CN" or "zh-SG" or "zh-Hans":
                    return 54936;
                case "zh-TW" or "zh-HK" or "zh-MO" or "zh-Hant":
                    return 950;
                case "zh":
                    // Plain Chinese without a script or region: mainland files are the common case.
                    return 54936;
                case "ja":
                    return 932;
                case "ko":
                    return 949;
            }
            if (ReferenceEquals(c, c.Parent))
            {
                break;
            }
        }
        return 1252;
    }

    private static string Normalize(string text)
    {
        if (text[0] == '\uFEFF')
        {
            text = text[1..];
        }
        if (text.Contains('\r'))
        {
            text = text.Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n');
        }
        return text.Contains('\0') ? text.Replace('\0', '\uFFFD') : text;
    }

    private static bool IsWebVtt(string text) =>
        text.StartsWith("WEBVTT", StringComparison.Ordinal) && (text.Length == 6 || text[6] is ' ' or '\t' or '\n');

    /// <summary>Stable sort by start time (file order breaks ties, which is WebVTT's rendering order).</summary>
    private static VideoCue[] SortByStart(IReadOnlyList<VideoCue> cues)
    {
        var sorted = new VideoCue[cues.Count];
        var sortedAlready = true;
        for (var i = 0; i < cues.Count; i++)
        {
            sorted[i] = cues[i];
            if (i > 0 && cues[i].Start < cues[i - 1].Start)
            {
                sortedAlready = false;
            }
        }
        return sortedAlready ? sorted : cues.OrderBy(static c => c.Start).ToArray();
    }
}
