// Ported from Arc's free file-upload block (MIT, Copyright (c) 2026 Elia Kuratli): file-upload.tsx (accept, formatSize, FileTypeIcon).
using System.Globalization;
using Avalonia.Media;
using Avalonia.Platform.Storage;

namespace TKCode.Arc;

/// <summary>
/// Accept matching like an HTML file input, with the MIME type taken from the file name (Avalonia's storage items carry no
/// MIME type), plus the picker filter, Arc's size format and the file icon.
/// </summary>
internal static class FileUploadTypes
{
    private static readonly Dictionary<string, string> s_mime = new(StringComparer.OrdinalIgnoreCase)
    {
        ["png"] = "image/png", ["jpg"] = "image/jpeg", ["jpeg"] = "image/jpeg", ["gif"] = "image/gif", ["webp"] = "image/webp",
        ["svg"] = "image/svg+xml", ["avif"] = "image/avif", ["heic"] = "image/heic", ["heif"] = "image/heif", ["bmp"] = "image/bmp",
        ["ico"] = "image/x-icon", ["tif"] = "image/tiff", ["tiff"] = "image/tiff",
        ["mp4"] = "video/mp4", ["mov"] = "video/quicktime", ["webm"] = "video/webm", ["m4v"] = "video/x-m4v",
        ["avi"] = "video/x-msvideo", ["mkv"] = "video/x-matroska", ["mpeg"] = "video/mpeg",
        ["mp3"] = "audio/mpeg", ["wav"] = "audio/wav", ["ogg"] = "audio/ogg", ["oga"] = "audio/ogg", ["m4a"] = "audio/mp4",
        ["flac"] = "audio/flac", ["aac"] = "audio/aac", ["opus"] = "audio/opus",
        ["pdf"] = "application/pdf", ["zip"] = "application/zip", ["gz"] = "application/gzip", ["tgz"] = "application/gzip",
        ["tar"] = "application/x-tar", ["rar"] = "application/vnd.rar", ["7z"] = "application/x-7z-compressed",
        ["bz2"] = "application/x-bzip2", ["json"] = "application/json", ["xml"] = "application/xml",
        ["csv"] = "text/csv", ["tsv"] = "text/tab-separated-values", ["txt"] = "text/plain", ["log"] = "text/plain",
        ["md"] = "text/markdown", ["html"] = "text/html", ["htm"] = "text/html", ["css"] = "text/css", ["js"] = "text/javascript",
        ["mjs"] = "text/javascript", ["ics"] = "text/calendar", ["rtf"] = "application/rtf", ["wasm"] = "application/wasm",
        ["doc"] = "application/msword", ["docx"] = "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
        ["xls"] = "application/vnd.ms-excel", ["xlsx"] = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
        ["ppt"] = "application/vnd.ms-powerpoint",
        ["pptx"] = "application/vnd.openxmlformats-officedocument.presentationml.presentation",
        ["odt"] = "application/vnd.oasis.opendocument.text", ["ods"] = "application/vnd.oasis.opendocument.spreadsheet",
        ["epub"] = "application/epub+zip", ["ttf"] = "font/ttf", ["otf"] = "font/otf", ["woff"] = "font/woff", ["woff2"] = "font/woff2",
    };

    /// <summary>The MIME type for the file name's extension, or an empty string when it is unknown (a browser reports "").</summary>
    public static string MimeOf(string name)
    {
        var extension = Path.GetExtension(name).TrimStart('.');
        return s_mime.TryGetValue(extension, out var mime) ? mime : string.Empty;
    }

    /// <summary>Arc's split: comma separated, trimmed, lower case, empty entries dropped.</summary>
    public static List<string> ParseAccept(string? accept) =>
        (accept ?? string.Empty).Split(',').Select(value => value.Trim().ToLowerInvariant()).Where(value => value.Length > 0).ToList();

    /// <summary>Arc's rule: ".ext" matches the end of the name, "type/*" the MIME prefix, anything else the exact MIME type.</summary>
    public static bool Matches(string name, IReadOnlyList<string> accepted)
    {
        if (accepted.Count == 0)
        {
            return true;
        }
        var lower = name.ToLowerInvariant();
        var mime = MimeOf(name);
        foreach (var type in accepted)
        {
            if (type.StartsWith('.')
                    ? lower.EndsWith(type, StringComparison.Ordinal)
                    : type.EndsWith("/*", StringComparison.Ordinal)
                        ? mime.StartsWith(type[..^1], StringComparison.Ordinal)
                        : mime == type)
            {
                return true;
            }
        }
        return false;
    }

    /// <summary>The picker filter for <paramref name="accept"/>: extensions as patterns, MIME types expanded through the table.</summary>
    public static IReadOnlyList<FilePickerFileType>? PickerFilter(string? accept)
    {
        var accepted = ParseAccept(accept);
        if (accepted.Count == 0)
        {
            return null;
        }
        var patterns = new List<string>();
        var mimes = new List<string>();
        foreach (var type in accepted)
        {
            if (type.StartsWith('.'))
            {
                patterns.Add("*" + type);
                continue;
            }
            mimes.Add(type);
            var prefix = type.EndsWith("/*", StringComparison.Ordinal) ? type[..^1] : null;
            foreach (var (extension, mime) in s_mime)
            {
                if (prefix is not null ? mime.StartsWith(prefix, StringComparison.Ordinal) : mime == type)
                {
                    patterns.Add("*." + extension);
                }
            }
        }
        return [new FilePickerFileType("Accepted files") { Patterns = patterns.Distinct().ToList(), MimeTypes = mimes }];
    }

    /// <summary>Arc's size text: "512 B", "48 KB", "2.4 MB", "18 MB" (1024 steps; one decimal under 10 MB).</summary>
    public static string FormatSize(long bytes)
    {
        if (bytes < 1024)
        {
            return $"{bytes} B";
        }
        if (bytes < 1024 * 1024)
        {
            return $"{Math.Round(bytes / 1024.0, MidpointRounding.AwayFromZero).ToString(CultureInfo.InvariantCulture)} KB";
        }
        var mb = bytes / (1024.0 * 1024.0);
        var shown = mb >= 10 ? Math.Round(mb, MidpointRounding.AwayFromZero) : Math.Round(mb, 1, MidpointRounding.AwayFromZero);
        return $"{shown.ToString(CultureInfo.InvariantCulture)} MB";
    }

    /// <summary>Arc's file icon: image, text, archive (a "zip" MIME type or ".gz"), else a plain file.</summary>
    public static Geometry IconOf(string name)
    {
        var mime = MimeOf(name);
        if (mime.StartsWith("image/", StringComparison.Ordinal))
        {
            return ArcIcons.FileImage;
        }
        if (mime.StartsWith("text/", StringComparison.Ordinal))
        {
            return ArcIcons.FileText;
        }
        if (mime.Contains("zip", StringComparison.Ordinal) || name.EndsWith(".gz", StringComparison.Ordinal))
        {
            return ArcIcons.FileArchive;
        }
        return ArcIcons.File;
    }
}
