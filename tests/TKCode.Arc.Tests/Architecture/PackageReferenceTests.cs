using System.Xml.Linq;

namespace TKCode.Arc.Tests.Architecture;

/// <summary>
/// Each package's project and package references, pinned (#147): a new dependency, which every app using the package then
/// carries, is a deliberate change of this list, not a side effect. Analyzer and build-order references (the generators) are left out.
/// </summary>
public class PackageReferenceTests
{
    private static readonly Dictionary<string, string[]> s_allowed = new(StringComparer.Ordinal)
    {
        ["TKCode.Arc.Foundation"] = ["Avalonia"],
        ["TKCode.Arc.Foundation.Generators"] = ["Microsoft.CodeAnalysis.Analyzers", "Microsoft.CodeAnalysis.CSharp"],
        ["TKCode.Arc.Core"] = ["TKCode.Arc.Foundation"],
        ["TKCode.Arc"] = ["TKCode.Arc.Core"],
        // ADR 0050: StreamMark, Markdig and TextMate come only through these two.
        ["TKCode.Arc.Markdown"] = ["TKCode.Arc", "TKCode.StreamMark.Avalonia"],
        ["TKCode.Arc.Highlighting"] = ["TKCode.Arc", "TKCode.StreamMark.Highlighting.TextMate"],
        ["TKCode.Arc.Icons.Lucide"] = ["TKCode.Arc.Core"],
        ["TKCode.Arc.Icons.Lucide.Generators"] = ["Microsoft.CodeAnalysis.Analyzers", "Microsoft.CodeAnalysis.CSharp"],
        ["TKCode.Arc.Fonts"] = ["Avalonia", "TKCode.Arc.Foundation"],
        ["TKCode.Arc.Themes.Corners"] = ["TKCode.Arc.Core"],
        ["TKCode.Arc.Themes.Pixel"] = ["TKCode.Arc.Core", "TKCode.Arc.Themes.Corners"],
        ["TKCode.Arc.AdvancedCharts"] = ["TKCode.Arc"],
        // The AI and support blocks render Markdown; code is colored only when the app enables Highlighting (#147).
        ["TKCode.Arc.Blocks"] = ["TKCode.Arc", "TKCode.Arc.Audio", "TKCode.Arc.Markdown"],
        ["TKCode.Arc.Special"] = ["TKCode.Arc"],
        ["TKCode.Arc.Dock"] = ["TKCode.Arc"],
        ["TKCode.Arc.Workflow"] = ["TKCode.Arc"],
        ["TKCode.Arc.A2UI"] = ["TKCode.Arc", "TKCode.Arc.Highlighting", "TKCode.Arc.Icons.Lucide", "TKCode.Arc.Markdown"],
        ["TKCode.Arc.Terminal"] = ["Avalonia"],
        ["TKCode.Arc.Mobile"] = ["TKCode.Arc.Core"],
        ["TKCode.Arc.Mobile.iOS"] = ["Avalonia.iOS", "TKCode.Arc.Mobile"],
        ["TKCode.Arc.Audio"] = [],
        ["TKCode.Arc.Audio.Native"] = ["TKCode.Arc.Audio", "TKCode.Arc.Foundation"],
        ["TKCode.Arc.Audio.Browser"] = ["TKCode.Arc.Audio", "TKCode.Arc.Foundation"],
        ["TKCode.Arc.Video"] = ["TKCode.Arc"],
        ["TKCode.Arc.Video.Native"] = ["TKCode.Arc.Video"],
        ["TKCode.Arc.Video.Browser"] = ["Avalonia.Browser", "TKCode.Arc.Video"],
        ["TKCode.Arc.Pdf"] = ["TKCode.Arc"],
        ["TKCode.Arc.Pdf.Native"] = ["TKCode.Arc.Pdf"],
        ["TKCode.Arc.Pdf.Browser"] = ["TKCode.Arc.Pdf"],
        // ADR 0051: the engine has no dependency at all; only its controls bring Avalonia and Arc.
        ["TKCode.Arc.Graphics3D"] = [],
        // The WebGPU backend binds wgpu-native itself (function pointers): no package for it, and no native library inside.
        ["TKCode.Arc.Graphics3D.WebGpu"] = ["TKCode.Arc.Graphics3D"],
        ["TKCode.Arc.Graphics3D.Controls"] = ["TKCode.Arc", "TKCode.Arc.Graphics3D"],
        // The browser backend needs no Avalonia.Browser API: JavaScript interop and the runtime's OpenGL ES layer.
        ["TKCode.Arc.Graphics3D.Browser"] = ["TKCode.Arc.Graphics3D.Controls"],
    };

    [Fact]
    public void Every_Package_References_Only_What_Is_Listed()
    {
        var problems = new List<string>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var project in Directory.GetFiles(Path.Combine(RepositoryRoot(), "src"), "*.csproj", SearchOption.AllDirectories))
        {
            var name = Path.GetFileNameWithoutExtension(project);
            seen.Add(name);
            var actual = References(project);
            if (!s_allowed.TryGetValue(name, out var allowed))
            {
                problems.Add($"{name}: not listed (references {string.Join(", ", actual)})");
                continue;
            }
            var extra = actual.Except(allowed, StringComparer.Ordinal).ToList();
            var missing = allowed.Except(actual, StringComparer.Ordinal).ToList();
            if (extra.Count > 0)
            {
                problems.Add($"{name}: new reference {string.Join(", ", extra)}");
            }
            if (missing.Count > 0)
            {
                problems.Add($"{name}: no longer references {string.Join(", ", missing)} (remove it from this list)");
            }
        }
        foreach (var name in s_allowed.Keys.Where(n => !seen.Contains(n)))
        {
            problems.Add($"{name}: listed but has no project");
        }
        problems.ShouldBeEmpty();
    }

    private static List<string> References(string project)
    {
        var document = XDocument.Load(project);
        var references = new List<string>();
        foreach (var element in document.Descendants())
        {
            var include = element.Attribute("Include")?.Value;
            if (include is null)
            {
                continue;
            }
            if (element.Name.LocalName == "PackageReference")
            {
                references.Add(include);
            }
            else if (element.Name.LocalName == "ProjectReference" && element.Attribute("OutputItemType")?.Value != "Analyzer"
                     && element.Attribute("ReferenceOutputAssembly")?.Value != "false")
            {
                references.Add(Path.GetFileNameWithoutExtension(include.Replace('\\', '/')));
            }
        }
        return references.Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToList();
    }

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
