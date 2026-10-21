using System.Buffers.Binary;
using System.Text;

namespace TKCode.Arc.Tests.Gallery;

/// <summary>
/// The Chinese font of the WebAssembly host (gallery/TKCode.Arc.Gallery.Browser/wwwroot/fonts): a browser has no system fonts
/// for Skia, so a character the font lacks is drawn as a box.
/// </summary>
public class GalleryBrowserFontTests
{
    private static string RepositoryRoot()
    {
        for (var folder = new DirectoryInfo(AppContext.BaseDirectory); folder is not null; folder = folder.Parent)
        {
            if (File.Exists(Path.Combine(folder.FullName, "TKCode.Arc.slnx")))
            {
                return folder.FullName;
            }
        }
        throw new InvalidOperationException("TKCode.Arc.slnx was not found above the test folder");
    }

    [Fact]
    public void Every_Chinese_Character_The_Gallery_Draws_Is_In_The_Browser_Font()
    {
        var root = RepositoryRoot();
        var font = File.ReadAllBytes(Path.Combine(root, "gallery", "TKCode.Arc.Gallery.Browser", "wwwroot", "fonts", "NotoSansSC-Subset.otf"));
        var covered = Codepoints(font);
        covered.ShouldContain(0x4E2D, "the font has no Chinese at all");

        var missing = new SortedDictionary<int, string>();
        foreach (var path in TextFiles(root))
        {
            var text = File.ReadAllText(path, Encoding.UTF8);
            for (var i = 0; i < text.Length; i++)
            {
                var c = text[i];
                var drawn = c is >= '㐀' and <= '鿿' or >= '豈' and <= '﫿' or >= '　' and <= '〿' or >= '＀' and <= '￯';
                if (drawn && !covered.Contains(c))
                {
                    missing.TryAdd(c, Path.GetRelativePath(root, path));
                }
            }
        }
        // Run: /tmp/ftvenv/bin/python tools/fonts/subset-noto-sans-sc.py (see the script for the setup)
        missing.ShouldBeEmpty("characters the browser font lacks (a box in the WebAssembly host); run tools/fonts/subset-noto-sans-sc.py:\n" +
                              string.Join("\n", missing.Select(m => $"{(char)m.Key} U+{m.Key:X4} in {m.Value}")));
    }

    /// <summary>The Markdown and XLIFF texts of the gallery and the libraries, and the catalogs that hold Chinese titles.</summary>
    private static IEnumerable<string> TextFiles(string root)
    {
        foreach (var folder in new[] { "gallery", "src" })
        {
            foreach (var path in Directory.EnumerateFiles(Path.Combine(root, folder), "*", SearchOption.AllDirectories))
            {
                var normalized = path.Replace('\\', '/');
                if (normalized.Contains("/obj/") || normalized.Contains("/bin/") || normalized.Contains("/GeneratedFiles/"))
                {
                    continue;
                }
                if (path.EndsWith(".md", StringComparison.Ordinal) || path.EndsWith(".xlf", StringComparison.Ordinal) ||
                    Path.GetFileName(path).Contains("Catalog", StringComparison.Ordinal) && path.EndsWith(".cs", StringComparison.Ordinal))
                {
                    yield return path;
                }
            }
        }
    }

    /// <summary>The code points of an OpenType font's character map (formats 4 and 12).</summary>
    private static HashSet<int> Codepoints(byte[] font)
    {
        ushort U16(int at) => BinaryPrimitives.ReadUInt16BigEndian(font.AsSpan(at));
        uint U32(int at) => BinaryPrimitives.ReadUInt32BigEndian(font.AsSpan(at));

        var tables = U16(4);
        var cmap = -1;
        for (var i = 0; i < tables; i++)
        {
            var entry = 12 + 16 * i;
            if (Encoding.ASCII.GetString(font, entry, 4) == "cmap")
            {
                cmap = (int)U32(entry + 8);
            }
        }
        cmap.ShouldBeGreaterThan(0, "the font has no cmap table");

        var result = new HashSet<int>();
        var subtables = U16(cmap + 2);
        for (var i = 0; i < subtables; i++)
        {
            var start = cmap + (int)U32(cmap + 4 + 8 * i + 4);
            switch (U16(start))
            {
                case 4:
                    var segments = U16(start + 6) / 2;
                    var ends = start + 14;
                    var starts = ends + segments * 2 + 2;
                    for (var s = 0; s < segments; s++)
                    {
                        for (int code = U16(starts + 2 * s), end = U16(ends + 2 * s); code <= end && code < 0xFFFF; code++)
                        {
                            result.Add(code);
                        }
                    }
                    break;
                case 12:
                    var groups = (int)U32(start + 12);
                    for (var g = 0; g < groups; g++)
                    {
                        var group = start + 16 + 12 * g;
                        for (var code = U32(group); code <= U32(group + 4); code++)
                        {
                            result.Add((int)code);
                        }
                    }
                    break;
            }
        }
        return result;
    }
}
