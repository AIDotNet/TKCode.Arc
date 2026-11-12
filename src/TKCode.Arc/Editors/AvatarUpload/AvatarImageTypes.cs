namespace TKCode.Arc;

/// <summary>
/// File type checks for <see cref="AvatarUpload"/>. A file is identified by its first bytes before its name, so a PDF renamed
/// to <c>.png</c> is refused as the PDF it is, and a picture with an odd extension is still accepted; nothing is decoded
/// until the type is accepted.
/// </summary>
internal static class AvatarImageTypes
{
    /// <summary>The types accepted when <see cref="AvatarUpload.Accept"/> is not set.</summary>
    public const string DefaultAccept = "image/png,image/jpeg,image/webp,image/gif,image/bmp";

    /// <summary>How many bytes <see cref="Sniff"/> needs.</summary>
    public const int SniffLength = 16;

    private static readonly byte[] s_pngSignature = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];

    /// <summary>The MIME type the first bytes of a file announce, or null when they are not one this knows.</summary>
    public static string? Sniff(ReadOnlySpan<byte> head)
    {
        // The PNG signature starts with the byte 0x89, which a UTF-8 literal would write as two bytes.
        if (head.StartsWith(s_pngSignature))
        {
            return "image/png";
        }
        if (head.Length >= 3 && head[0] == 0xFF && head[1] == 0xD8 && head[2] == 0xFF)
        {
            return "image/jpeg";
        }
        if (head.StartsWith("GIF87a"u8) || head.StartsWith("GIF89a"u8))
        {
            return "image/gif";
        }
        if (head.Length >= 12 && head[..4].SequenceEqual("RIFF"u8) && head.Slice(8, 4).SequenceEqual("WEBP"u8))
        {
            return "image/webp";
        }
        if (head.StartsWith("BM"u8))
        {
            return "image/bmp";
        }
        if (head.Length >= 12 && head.Slice(4, 4).SequenceEqual("ftyp"u8))
        {
            var brand = head.Slice(8, 4);
            if (brand.SequenceEqual("avif"u8) || brand.SequenceEqual("avis"u8))
            {
                return "image/avif";
            }
            if (brand.SequenceEqual("heic"u8) || brand.SequenceEqual("heix"u8) || brand.SequenceEqual("mif1"u8) || brand.SequenceEqual("msf1"u8))
            {
                return "image/heic";
            }
        }
        return head.StartsWith("%PDF-"u8) ? "application/pdf" : null;
    }

    /// <summary>The type of a file: what its first bytes say, else what its extension says (null when neither knows).</summary>
    public static string? Detect(ReadOnlySpan<byte> head, string? name) => Sniff(head) ?? FromName(name);

    /// <summary>The type an extension stands for, for the common picture and document types.</summary>
    public static string? FromName(string? name) => string.IsNullOrEmpty(name) ? null : FileDropzoneTypes.MimeOf(name);

    /// <summary>
    /// Whether a file of type <paramref name="mime"/> named <paramref name="name"/> matches <paramref name="accept"/>, a
    /// comma-separated list as in an HTML file input: <c>.ext</c>, <c>type/*</c> or <c>type/subtype</c>. Null or empty uses
    /// <see cref="DefaultAccept"/>.
    /// </summary>
    public static bool Accepts(string? accept, string? mime, string? name)
    {
        var accepted = FileDropzoneTypes.ParseAccept(string.IsNullOrWhiteSpace(accept) ? DefaultAccept : accept);
        var lower = (name ?? string.Empty).ToLowerInvariant();
        mime = mime?.ToLowerInvariant();
        foreach (var token in accepted)
        {
            if (token.StartsWith('.'))
            {
                if (lower.EndsWith(token, StringComparison.Ordinal) || (mime is not null && ExtensionsOf(mime).Contains(token)))
                {
                    return true;
                }
            }
            else if (token.EndsWith("/*", StringComparison.Ordinal))
            {
                if (mime is not null && mime.StartsWith(token[..^1], StringComparison.Ordinal))
                {
                    return true;
                }
            }
            else if (mime == token)
            {
                return true;
            }
        }
        return false;
    }

    private static string[] ExtensionsOf(string mime) => mime switch
    {
        "image/png" => [".png"],
        "image/jpeg" => [".jpg", ".jpeg"],
        "image/gif" => [".gif"],
        "image/webp" => [".webp"],
        "image/bmp" => [".bmp"],
        "image/avif" => [".avif"],
        "image/heic" => [".heic", ".heif"],
        _ => []
    };

    /// <summary>The accepted types in words: "PNG, JPEG or WebP".</summary>
    public static string Describe(string? accept)
    {
        var names = new List<string>();
        foreach (var token in FileDropzoneTypes.ParseAccept(string.IsNullOrWhiteSpace(accept) ? DefaultAccept : accept))
        {
            var name = token switch
            {
                "image/png" or ".png" => "PNG",
                "image/jpeg" or "image/jpg" or ".jpg" or ".jpeg" => "JPEG",
                "image/webp" or ".webp" => "WebP",
                "image/gif" or ".gif" => "GIF",
                "image/bmp" or ".bmp" => "BMP",
                "image/avif" or ".avif" => "AVIF",
                "image/heic" or ".heic" or ".heif" => "HEIC",
                "image/*" => "image",
                _ => token.StartsWith('.') ? token[1..].ToUpperInvariant() : token
            };
            if (!names.Contains(name))
            {
                names.Add(name);
            }
        }
        return names.Count switch
        {
            0 => "image",
            1 => names[0],
            _ => string.Join(", ", names.Take(names.Count - 1)) + " or " + names[^1]
        };
    }

    /// <summary>The extension (with the dot) that goes with <paramref name="contentType"/>.</summary>
    public static string ExtensionFor(string contentType) => contentType == "image/png" ? ".png" : ".jpg";

    /// <summary>A name for the saved image: <paramref name="originalName"/> without its extension plus the saved format's.</summary>
    public static string SavedName(string? originalName, string contentType)
    {
        var stem = string.IsNullOrEmpty(originalName) ? null : Path.GetFileNameWithoutExtension(originalName);
        return (string.IsNullOrWhiteSpace(stem) ? "avatar" : stem) + ExtensionFor(contentType);
    }
}
