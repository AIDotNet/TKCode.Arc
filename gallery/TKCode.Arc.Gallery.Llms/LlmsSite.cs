using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using TKCode.Arc.Gallery.Catalog;
using TKCode.Arc.Gallery.Docs;
using TKCode.Arc.Gallery.Shell;

namespace TKCode.Arc.Gallery.Llms;

/// <summary>A file of the site: its path below the web root, such as <c>components/button.md</c>, and its text.</summary>
public sealed record LlmsFile(string Path, string Text);

/// <summary>
/// The files the web host serves to AI assistants, built from the same pages the gallery shows:
/// <list type="bullet">
/// <item><c>llms.txt</c>: the index (https://llmstxt.org), with install steps, rules and a link to every page;</item>
/// <item><c>llms-full.txt</c>: every English page in one file;</item>
/// <item>every page as Markdown at its gallery address plus <c>.md</c> (<c>components/button.md</c>, <c>blocks/sidebar.md</c>,
/// <c>docs/installation.md</c>), and its translations below <c>zh-CN/</c> and <c>zh-TW/</c>. A component page is what the
/// gallery's "Copy page" copies (examples as their AXAML and code-behind, the API reference filled in), under a header
/// with its package; screenshots point at the host's <c>shots/</c> files;</item>
/// <item>the agent skill in <c>skills/tkcode-arc/</c>: <c>SKILL.md</c>, <c>checklist.md</c> and the generated <c>components.md</c>.</item>
/// </list>
/// The hand-written text is in <c>Content/</c>; its <c>{{name}}</c> placeholders are filled in here.
/// </summary>
public static partial class LlmsSite
{
    public const string DefaultBaseUrl = "https://docs.tkcode.si";

    /// <summary>The skill's name (its folder, and <c>name</c> in its front matter).</summary>
    public const string SkillName = "tkcode-arc";

    private const string Repository = "https://github.com/AIDotNet/TKCode.Arc";

    /// <summary>The packages on nuget.org; the others are built from the repository (see the Push step of .github/workflows/ci.yml).</summary>
    private static readonly HashSet<string> s_onNuGet = ["TKCode.Arc", "TKCode.Arc.Core", "TKCode.Arc.Fonts", "TKCode.Arc.Icons.Lucide"];

    /// <summary>The builder call that registers each package's controls.</summary>
    private static readonly Dictionary<string, string> s_registration = new()
    {
        ["TKCode.Arc"] = "UseArc()",
        ["TKCode.Arc.Blocks"] = "UseArcBlocks()",
        ["TKCode.Arc.Mobile"] = "UseArcMobile()",
        ["TKCode.Arc.AdvancedCharts"] = "UseArcAdvancedCharts()",
        ["TKCode.Arc.Workflow"] = "UseArcWorkflow()",
        ["TKCode.Arc.Special"] = "UseArcSpecial()",
    };

    /// <summary>A page of the gallery: a guide of the Docs section, or a component or block with a docs page.</summary>
    /// <param name="Address">The gallery address, such as <c>/components/button</c>; the Markdown is at the same address plus <c>.md</c>.</param>
    /// <param name="Guide">The guide's name for a Docs page (<c>Installation</c>); null for a component or block.</param>
    private sealed record Page(GalleryEntry Entry, string Address, string? Guide);

    /// <summary>All the files, with links that start at <paramref name="baseUrl"/>.</summary>
    public static IReadOnlyList<LlmsFile> Build(string baseUrl = DefaultBaseUrl)
    {
        baseUrl = baseUrl.TrimEnd('/');
        var docs = GalleryCatalog.Docs.Entries.Select(e => new Page(e, GalleryRoute.To(e).Path, e.ClassName["Docs".Length..])).ToArray();
        var components = GalleryCatalog.AllCategories.Concat(GalleryCatalog.Special).Append(GalleryCatalog.Mobile).Select(c => (c.Title, Pages: Documented(c.AllEntries))).Where(g => g.Pages.Length > 0).ToArray();
        var blocks = (GalleryCatalog.Blocks.Subcategories ?? []).Select(c => (c.Title, Pages: Documented(c.AllEntries))).Where(g => g.Pages.Length > 0).ToArray();
        var all = docs.Concat(components.SelectMany(g => g.Pages)).Concat(blocks.SelectMany(g => g.Pages)).ToArray();

        var values = Values(baseUrl);
        values["arc-count"] = GalleryCatalog.Count.ToString(System.Globalization.CultureInfo.InvariantCulture);
        values["block-count"] = blocks.Sum(g => g.Pages.Length).ToString(System.Globalization.CultureInfo.InvariantCulture);
        values["docs"] = string.Join("\n", docs.Select(p => Link(baseUrl, p, p.Entry.Description)));
        values["components"] = Groups(components, p => Link(baseUrl, p, Summary(p), NotOnNuGet(p)));
        values["blocks"] = Groups(blocks, p => Link(baseUrl, p, Summary(p)));
        values["components-catalog"] = Groups(components, p => Link(baseUrl, p, $"`{p.Entry.ClassName}`. {Summary(p)}", NotOnNuGet(p)));
        values["blocks-catalog"] = Groups(blocks, p => Link(baseUrl, p, $"`{p.Entry.ClassName}`. {Summary(p)}"));

        var full = Fill(Template("llms-full.txt"), values) + string.Join("\n\n---\n\n", all.Select(p => Body(baseUrl, values["version"], p, GalleryDocs.SourceLanguage)!.TrimEnd())) + "\n";
        values["full-size"] = $"{Math.Max(1, (int)Math.Round(Encoding.UTF8.GetByteCount(full) / 1048576.0))} MB";

        var files = new List<LlmsFile>
        {
            new("llms.txt", Fill(Template("llms.txt"), values)),
            new("llms-full.txt", full),
            new($"skills/{SkillName}/SKILL.md", Fill(Template("SKILL.md"), values)),
            new($"skills/{SkillName}/checklist.md", Fill(Template("checklist.md"), values)),
            new($"skills/{SkillName}/components.md", Fill(Template("components.md"), values)),
        };
        foreach (var page in all)
        {
            var languages = Languages(page);
            foreach (var language in languages)
            {
                files.Add(new LlmsFile(MarkdownPath(page, language), Body(baseUrl, values["version"], page, language)!.TrimEnd() + "\n\n" + Footer(baseUrl, page, language, languages)));
            }
        }
        return files;
    }

    /// <summary>
    /// Links in <paramref name="files"/> to Markdown and text below <paramref name="baseUrl"/> that are not among the files,
    /// as <c>path: url</c>.
    /// </summary>
    public static IReadOnlyList<string> BrokenLinks(IReadOnlyList<LlmsFile> files, string baseUrl = DefaultBaseUrl)
    {
        baseUrl = baseUrl.TrimEnd('/');
        var paths = files.Select(f => f.Path).ToHashSet(StringComparer.Ordinal);
        return files.SelectMany(f => SiteLink().Matches(f.Text)
                                               .Where(m => m.Value.StartsWith(baseUrl + "/", StringComparison.Ordinal) && !paths.Contains(m.Value[(baseUrl.Length + 1)..]))
                                               .Select(m => $"{f.Path}: {m.Value}"))
                    .Distinct()
                    .ToArray();
    }

    private static Page[] Documented(IEnumerable<GalleryEntry> entries)
        => entries.Where(e => GalleryDocs.Has(e.ClassName)).Select(e => new Page(e, GalleryRoute.To(e).Path, null)).ToArray();

    private static IReadOnlyList<string> Languages(Page page)
        => page.Guide is { } guide
            ? GalleryGuides.Languages.Where(l => GalleryGuides.Read(guide, l) is not null).ToArray()
            : GalleryDocs.Languages(page.Entry.ClassName);

    /// <summary>The Markdown's path below the web root: English at the page's address, translations below their language.</summary>
    private static string MarkdownPath(Page page, string language)
        => (language == GalleryDocs.SourceLanguage ? string.Empty : language + "/") + page.Address.TrimStart('/') + ".md";

    private static string Summary(Page page) => GalleryDocs.Summary(page.Entry.ClassName, GalleryDocs.SourceLanguage) ?? page.Entry.Description;

    private static string Package(Page page) => GalleryDocs.PackageOf(page.Entry.ClassName) ?? "TKCode.Arc";

    private static string NotOnNuGet(Page page)
        => s_onNuGet.Contains(Package(page)) ? string.Empty : $" (`{Package(page)}`, not on NuGet yet)";

    /// <summary>A line of a list of pages: <c>- [Title](url) note: description</c>, the form llms.txt lists files in.</summary>
    private static string Link(string baseUrl, Page page, string description, string note = "")
        => $"- [{page.Entry.Title}]({baseUrl}/{MarkdownPath(page, GalleryDocs.SourceLanguage)}){note}: {description}";

    private static string Groups(IEnumerable<(string Title, Page[] Pages)> groups, Func<Page, string> line)
        => string.Join("\n\n", groups.Select(g => $"### {g.Title}\n\n{string.Join("\n", g.Pages.Select(line))}"));

    /// <summary>The page in <paramref name="language"/> without its footer, or null when it is not written in that language.</summary>
    private static string? Body(string baseUrl, string version, Page page, string language)
    {
        if (page.Guide is { } guide)
        {
            return GalleryGuides.Read(guide, language) is { } text
                ? Shots(baseUrl, GalleryGuides.Split(text).Body, $"Guides/{guide}")
                : null;
        }
        var className = page.Entry.ClassName;
        if (!GalleryDocs.Languages(className).Contains(language, StringComparer.OrdinalIgnoreCase) || GalleryDocs.Get(className, language) is not { } doc)
        {
            return null;
        }
        var words = Words.For(language);
        var package = Package(page);
        var markdown = new StringBuilder();
        markdown.Append("# ").Append(doc.Title).Append("\n\n");
        markdown.Append("> ").Append(doc.Summary).Append("\n\n");
        markdown.Append("- ").Append(words.Package).Append(s_onNuGet.Contains(package)
            ? string.Format(words.OnNuGet, $"`{package}`", $"`dotnet add package {package} --version {version}`")
            : string.Format(words.NotOnNuGet, $"`{package}`", Repository, $"`{s_registration.GetValueOrDefault(package, "UseArc()")}`")).Append('\n');
        markdown.Append("- ").Append(words.Namespace).Append("`xmlns:arc=\"https://github.com/AIDotNet/TKCode.Arc\"`\n");
        markdown.Append("- ").Append(words.LivePage).Append(baseUrl).Append(page.Address).Append("\n\n");
        markdown.Append(Shots(baseUrl, doc.CopyMarkdown, $"Docs/{GalleryDocs.FolderOf(className)}"));
        return markdown.ToString();
    }

    private static string Footer(string baseUrl, Page page, string language, IReadOnlyList<string> languages)
    {
        var words = Words.For(language);
        var links = languages.Select(l => $"[{Words.For(l).Name}]({baseUrl}/{MarkdownPath(page, l)})");
        return $"---\n\n- {words.Languages}{string.Join(" · ", links)}\n- {words.Index}{baseUrl}/llms.txt\n";
    }

    /// <summary>The screenshots of a page in <paramref name="folder"/> (<c>Docs/&lt;Category&gt;/&lt;Component&gt;</c> or
    /// <c>Guides/&lt;Page&gt;</c>), at the address the Browser host publishes them to (its project file, <c>shots/</c>).</summary>
    private static string Shots(string baseUrl, string markdown, string folder)
        => GalleryDocs.WithScreenshotAddresses(markdown, name => $"{baseUrl}/shots/{folder}/images/{name}.webp");

    private static Dictionary<string, string> Values(string baseUrl)
    {
        var metadata = typeof(LlmsSite).Assembly.GetCustomAttributes<AssemblyMetadataAttribute>()
                                       .ToDictionary(a => a.Key, a => a.Value ?? string.Empty, StringComparer.Ordinal);
        return new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["base"] = baseUrl,
            ["skill"] = $"{baseUrl}/skills/{SkillName}",
            ["skill-name"] = SkillName,
            ["repository"] = Repository,
            ["version"] = metadata["ArcVersion"],
            ["atomui"] = metadata["AtomUIVersion"],
            ["avalonia"] = metadata["AvaloniaVersion"],
        };
    }

    private static string Template(string name)
    {
        using var stream = typeof(LlmsSite).Assembly.GetManifestResourceStream($"Content/{name}")
                           ?? throw new InvalidOperationException($"Content/{name} is not embedded");
        using var reader = new StreamReader(stream, Encoding.UTF8);
        return reader.ReadToEnd().Replace("\r\n", "\n");
    }

    /// <summary><paramref name="template"/> with each <c>{{name}}</c> replaced; an unknown name is an error, not an empty string.</summary>
    private static string Fill(string template, IReadOnlyDictionary<string, string> values)
        => Placeholder().Replace(template, m => values.TryGetValue(m.Groups[1].Value, out var value)
            ? value
            : throw new InvalidOperationException($"No value for {m.Value}"));

    [GeneratedRegex(@"\{\{([a-z-]+)\}\}")]
    private static partial Regex Placeholder();

    // A link to a file of the site: an address that ends in .md or .txt.
    [GeneratedRegex(@"https?://[^\s)<>`""']+?\.(?:md|txt)\b")]
    private static partial Regex SiteLink();

    /// <summary>The words of a page's header and footer, per language (en-US for the rest).</summary>
    private sealed record Words(string Name, string Package, string OnNuGet, string NotOnNuGet, string Namespace, string LivePage,
                                string Languages, string Index)
    {
        private static readonly Words s_english = new(
            "English", "Package: ", "{0}, {1}", "{0}, not on NuGet yet: reference the project from {1} and call {2}",
            "XAML namespace: ", "Live page: ", "Languages: ", "Every page: ");

        private static readonly Words s_simplified = new(
            "简体中文", "包：", "{0}，{1}", "{0}，尚未发布到 NuGet：从 {1} 引用项目，并调用 {2}",
            "XAML 命名空间：", "在线页面：", "语言：", "全部页面：");

        private static readonly Words s_traditional = new(
            "繁體中文", "套件：", "{0}，{1}", "{0}，尚未發佈到 NuGet：從 {1} 參考專案，並呼叫 {2}",
            "XAML 命名空間：", "線上頁面：", "語言：", "全部頁面：");

        public static Words For(string language) => language.ToLowerInvariant() switch
        {
            "zh-cn" => s_simplified,
            "zh-tw" => s_traditional,
            _ => s_english
        };
    }
}
