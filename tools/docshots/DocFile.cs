using System.Text.RegularExpressions;
using TKCode.Arc.Tests.Gallery;

namespace TKCode.Arc.DocShots;

/// <summary>A docs page as written: <c>Docs/&lt;Category&gt;/&lt;Component&gt;/&lt;language&gt;.md</c>.</summary>
internal sealed partial class DocFile
{
    public static readonly string[] Sections =
        ["## When to use", "## When not to use", "## Usage", "## API reference", "## Accessibility", "## Motion", "## Related"];

    private DocFile(string path, string text)
    {
        Path = path;
        Text = text;
        Language = System.IO.Path.GetFileNameWithoutExtension(path);
        ClassName = System.IO.Path.GetFileName(System.IO.Path.GetDirectoryName(path)) ?? string.Empty;
        Folder = System.IO.Path.GetDirectoryName(path) ?? string.Empty;
        Lines = text.Split('\n');
    }

    public string Path { get; }

    public string Text { get; }

    public string[] Lines { get; }

    public string Language { get; }

    public string ClassName { get; }

    /// <summary>The page's folder; screenshots go to <c>images/</c> in it.</summary>
    public string Folder { get; }

    public string ImagesFolder => System.IO.Path.Combine(Folder, "images");

    public bool IsSource => Language == "en-US";

    /// <summary>A page of the sidebar's Docs section (<c>Guides/&lt;Page&gt;/</c>) rather than a component page.</summary>
    public bool IsGuide => Folder.Replace('\\', '/').Contains("/Guides/", StringComparison.Ordinal);

    public static DocFile Load(string path) => new(path, File.ReadAllText(path).Replace("\r\n", "\n"));

    /// <summary>The <c>## </c> headings outside code blocks.</summary>
    public List<string> Headings => OutsideFences().Where(l => l.StartsWith("## ", StringComparison.Ordinal)).ToList();

    public List<DocSnippets.Block> Blocks => DocSnippets.Blocks(Text);

    public List<string> ApiTypes => ApiDirective().Matches(Text).Select(m => m.Groups[1].Value).ToList();

    public List<string> Demos => DemoTag().Matches(Text).Select(m => m.Groups[1].Value).ToList();

    /// <summary>
    /// Every <c>![caption](images/Name.png)</c> line with the code block under it (null when the next non-blank line does
    /// not open an <c>xml</c> block).
    /// </summary>
    public List<Screenshot> Screenshots
    {
        get
        {
            var shots = new List<Screenshot>();
            var blocks = Blocks;
            var inside = false;
            for (var i = 0; i < Lines.Length; i++)
            {
                if (Lines[i].StartsWith("```", StringComparison.Ordinal))
                {
                    inside = !inside;
                    continue;
                }
                if (inside || ImageLine().Match(Lines[i]) is not { Success: true } match)
                {
                    continue;
                }
                var next = i + 1;
                while (next < Lines.Length && Lines[next].Trim().Length == 0)
                {
                    next++;
                }
                // Blocks record the 1-based line of their opening fence.
                var scene = blocks.FirstOrDefault(b => b.Line == next + 1 && b.IsXaml);
                shots.Add(new Screenshot(match.Groups[2].Value, match.Groups[1].Value, i + 1, scene));
            }
            return shots;
        }
    }

    /// <summary>Image references that are not a screenshot line (another folder, another format, inline).</summary>
    public List<(int Line, string Text)> StrayImages
    {
        get
        {
            var stray = new List<(int, string)>();
            var inside = false;
            for (var i = 0; i < Lines.Length; i++)
            {
                if (Lines[i].StartsWith("```", StringComparison.Ordinal))
                {
                    inside = !inside;
                }
                else if (!inside && Lines[i].Contains("![", StringComparison.Ordinal) && !ImageLine().IsMatch(Lines[i]))
                {
                    stray.Add((i + 1, Lines[i].Trim()));
                }
            }
            return stray;
        }
    }

    public IEnumerable<string> OutsideFences()
    {
        var inside = false;
        foreach (var line in Lines)
        {
            if (line.StartsWith("```", StringComparison.Ordinal))
            {
                inside = !inside;
            }
            else if (!inside)
            {
                yield return line;
            }
        }
    }

    [GeneratedRegex(@"^[ \t]*<!--[ \t]*api:[ \t]*([A-Za-z_][A-Za-z0-9_]*)[ \t]*-->[ \t]*$", RegexOptions.Multiline)]
    private static partial Regex ApiDirective();

    [GeneratedRegex(@"^[ \t]*<Demo[ \t]+name=""([A-Za-z_][A-Za-z0-9_]*(?:\.[A-Za-z_][A-Za-z0-9_]*)?)""[ \t]*/>[ \t]*$", RegexOptions.Multiline)]
    private static partial Regex DemoTag();

    // The same pattern as GalleryDocs.ScreenshotTag.
    [GeneratedRegex(@"^[ \t]*!\[([^\]\n]*)\]\(images/([A-Za-z0-9_-]+)\.png\)[ \t]*$")]
    private static partial Regex ImageLine();
}

/// <param name="Line">The 1-based line of the image.</param>
/// <param name="Scene">The XAML block captured for it.</param>
internal sealed record Screenshot(string Name, string Caption, int Line, DocSnippets.Block? Scene);
