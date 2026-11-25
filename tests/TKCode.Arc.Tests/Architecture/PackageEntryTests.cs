using System.Text.RegularExpressions;

namespace TKCode.Arc.Tests.Architecture;

/// <summary>
/// Every package is enabled through <c>UseArc(...)</c> (#151): it has a public <c>UseArcXxx(this ArcBuilder)</c> entry, and
/// package ids are named constants, not string literals at the call. The exceptions need no setup or cannot have one.
/// </summary>
public partial class PackageEntryTests
{
    private static readonly Dictionary<string, string> s_exceptions = new(StringComparer.Ordinal)
    {
        ["TKCode.Arc.Foundation"] = "defines UseArc(...) itself",
        ["TKCode.Arc.Foundation.Generators"] = "an analyzer",
        ["TKCode.Arc.Icons.Lucide"] = "icons need no setup; its generator registers them from a module initializer",
        ["TKCode.Arc.Icons.Lucide.Generators"] = "an analyzer",
        ["TKCode.Arc.Audio"] = "the abstraction alone; the backends (Audio.Native, Audio.Browser) have the entries",
        ["TKCode.Arc.Mobile.iOS"] = "a platform host: derive the app delegate from ArcAppDelegate<TApp>",
        ["TKCode.Arc.Terminal"] = "a standalone terminal library on Avalonia alone (#150, ADR 0052): place a TerminalView",
        ["TKCode.Arc.StreamMark"] = "a standalone Markdown library on Avalonia alone (native incremental parser): place a MarkdownView (TKCode.Arc.Markdown has the Arc entry)",
        ["TKCode.Arc.StreamMark.TextMate"] = "a StreamMark plugin: add TextMatePlugin to MarkdownServices.Plugins",
        ["TKCode.Arc.StreamMark.RaTeX"] = "a StreamMark plugin: add RaTeXPlugin to MarkdownServices.Plugins",
        ["TKCode.Arc.StreamMark.Mermaid"] = "a StreamMark plugin: add MermaidPlugin to MarkdownServices.Plugins",
        ["TKCode.Arc.Editor"] = "a code editor engine without UI dependency (ADR 0056); TKCode.Arc.Editor.Controls has the entry",
        ["TKCode.Arc.Editor.Lsp"] = "a language server client for the editor engine, without UI dependency: create a LanguageClient",
        ["TKCode.Arc.Graphics3D"] = "a 3D engine without UI dependency (ADR 0051); TKCode.Arc.Graphics3D.Controls has the entry",
        ["TKCode.Arc.Graphics3D.WebGpu"] = "a rendering backend of the 3D engine, without UI dependency: create it with WgpuDevice.Create",
    };

    [GeneratedRegex(@"public\s+static\s+ArcBuilder\s+UseArc\w*\s*\(\s*this\s+ArcBuilder\b")]
    private static partial Regex Entry();

    [GeneratedRegex(@"\.UsePackage\(\s*""")]
    private static partial Regex LiteralId();

    [Fact]
    public void Every_Package_Has_A_UseArc_Entry()
    {
        var missing = new List<string>();
        foreach (var folder in Directory.GetDirectories(Path.Combine(RepositoryRoot(), "src")))
        {
            var name = Path.GetFileName(folder);
            if (name == "Shared" || s_exceptions.ContainsKey(name) || !File.Exists(Path.Combine(folder, name + ".csproj")))
            {
                continue;
            }
            if (!Sources(folder).Any(file => Entry().IsMatch(File.ReadAllText(file))))
            {
                missing.Add(name);
            }
        }
        missing.ShouldBeEmpty();
    }

    [Fact]
    public void Package_Ids_Are_Not_String_Literals()
    {
        var literals = Sources(Path.Combine(RepositoryRoot(), "src"))
            .Where(file => LiteralId().IsMatch(File.ReadAllText(file)))
            .Select(file => Path.GetRelativePath(RepositoryRoot(), file))
            .ToList();
        literals.ShouldBeEmpty();
    }

    private static IEnumerable<string> Sources(string folder) =>
        Directory.EnumerateFiles(folder, "*.cs", SearchOption.AllDirectories)
            .Where(file => !file.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
                           && !file.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
                           && !file.Contains("GeneratedFiles", StringComparison.Ordinal));

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
}
