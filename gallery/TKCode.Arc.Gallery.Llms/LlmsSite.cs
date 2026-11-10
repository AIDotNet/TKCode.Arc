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
/// <item>every component and block page also as a short reference at its address plus <c>.api.md</c>
/// (<c>components/button.api.md</c>): its header, first example, the components to choose instead and the API reference,
/// a quarter of the full page on average;</item>
/// <item><c>skills.md</c>: the agent's entry point in one file, the skill with absolute links followed by the index of
/// every component and block (as https://icons.lobehub.com/skills.md does);</item>
/// <item>the agent skill in <c>skills/tkcode-arc/</c>: <c>SKILL.md</c>, <c>checklist.md</c>, the generated <c>components.md</c>
/// (every component and block with its class, package, purpose and other names) and <c>choosing.md</c> (per component, the
/// nearby components to choose instead and when, from each page's Related section).</item>
/// </list>
/// The hand-written text is in <c>Content/</c>; its <c>{{name}}</c> placeholders are filled in here.
/// </summary>
public static partial class LlmsSite
{
    public const string DefaultBaseUrl = "https://docs.tkcode.si";

    /// <summary>The skill's name (its folder, and <c>name</c> in its front matter).</summary>
    public const string SkillName = "tkcode-arc";

    private const string Repository = "https://github.com/AIDotNet/TKCode.Arc";

    /// <summary>The packages on nuget.org; the others are built from the repository (the Push step of .github/workflows/ci.yml lists them).</summary>
    private static readonly HashSet<string> s_onNuGet = [
        "TKCode.Arc", "TKCode.Arc.Core", "TKCode.Arc.Fonts", "TKCode.Arc.Icons.Lucide", "TKCode.Arc.Blocks", "TKCode.Arc.AdvancedCharts",
        "TKCode.Arc.Special", "TKCode.Arc.Workflow", "TKCode.Arc.Themes.Pixel", "TKCode.Arc.Themes.Corners", "TKCode.Arc.Mobile",
        "TKCode.Arc.Dock", "TKCode.Arc.Terminal", "TKCode.Arc.Audio", "TKCode.Arc.Pdf", "TKCode.Arc.Video",
    ];

    /// <summary>The builder call that registers each package's controls.</summary>
    private static readonly Dictionary<string, string> s_registration = new()
    {
        ["TKCode.Arc"] = "UseArcControls()",
        ["TKCode.Arc.Blocks"] = "UseArcBlocks()",
        ["TKCode.Arc.Mobile"] = "UseArcMobile()",
        ["TKCode.Arc.AdvancedCharts"] = "UseArcAdvancedCharts()",
        ["TKCode.Arc.Workflow"] = "UseArcWorkflow()",
        ["TKCode.Arc.Dock"] = "UseArcDock()",
        ["TKCode.Arc.Pdf"] = "UseArcPdf()",
        ["TKCode.Arc.Video"] = "UseArcVideo()",
        ["TKCode.Arc.Special"] = "UseArcSpecial()",
    };

    /// <summary>Groups whose components are effects and showpieces: listed with a note, so agents do not reach for them in ordinary screens.</summary>
    private static readonly HashSet<string> s_decorative = ["Text effects", "Playful", "Cursor", "Illustrations", "Showpieces"];

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

        var aliases = Aliases(all);
        var values = Values(baseUrl);
        values["arc-count"] = GalleryCatalog.Count.ToString(System.Globalization.CultureInfo.InvariantCulture);
        values["block-count"] = blocks.Sum(g => g.Pages.Length).ToString(System.Globalization.CultureInfo.InvariantCulture);
        values["docs"] = string.Join("\n", docs.Select(p => Link(baseUrl, p, p.Entry.Description)));
        values["components"] = Groups(components, ComponentsPackage, (p, note) => Link(baseUrl, p, Summary(p) + AlsoCalled(aliases, p) + Covers(p), note));
        values["blocks"] = Groups(blocks, BlocksPackage, (p, note) => Link(baseUrl, p, Summary(p) + AlsoCalled(aliases, p) + Covers(p), note));
        values["components-catalog"] = Groups(components, ComponentsPackage, (p, note) => Link(baseUrl, p, $"`{XamlName(p)}`. {Summary(p)}{AlsoCalled(aliases, p)}", note));
        values["blocks-catalog"] = Groups(blocks, BlocksPackage, (p, note) => Link(baseUrl, p, $"`{p.Entry.ClassName}`. {Summary(p)}{AlsoCalled(aliases, p)}", note));
        values["components-choosing"] = Groups(components, ComponentsPackage, (p, note) => Choosing(baseUrl, p, note), "\n\n");
        values["blocks-choosing"] = Groups(blocks, BlocksPackage, (p, note) => Choosing(baseUrl, p, note), "\n\n");

        var full = Fill(Template("llms-full.txt"), values) + string.Join("\n\n---\n\n", all.Select(p => Body(baseUrl, values["version"], p, GalleryDocs.SourceLanguage)!.TrimEnd())) + "\n";
        values["full-size"] = $"{Math.Max(1, (int)Math.Round(Encoding.UTF8.GetByteCount(full) / 1048576.0))} MB";

        var skill = Fill(Template("SKILL.md"), values);
        var files = new List<LlmsFile>
        {
            new("llms.txt", Fill(Template("llms.txt"), values)),
            new("skills.md", EntryPoint(skill, values)),
            new("llms-full.txt", full),
            new($"skills/{SkillName}/SKILL.md", skill),
            new($"skills/{SkillName}/checklist.md", Fill(Template("checklist.md"), values)),
            new($"skills/{SkillName}/components.md", Fill(Template("components.md"), values)),
            new($"skills/{SkillName}/choosing.md", Fill(Template("choosing.md"), values)),
        };
        foreach (var page in all)
        {
            var languages = Languages(page);
            foreach (var language in languages)
            {
                files.Add(new LlmsFile(MarkdownPath(page, language), Body(baseUrl, values["version"], page, language)!.TrimEnd() + "\n\n" + Footer(baseUrl, page, language, languages)));
            }
            if (Reference(baseUrl, values["version"], page) is { } reference)
            {
                files.Add(new LlmsFile(ReferencePath(page), reference));
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

    /// <summary>The short reference's path: the English page's address plus <c>.api.md</c>.</summary>
    private static string ReferencePath(Page page) => page.Address.TrimStart('/') + ".api.md";

    /// <summary>
    /// A component or block page cut to what an agent needs to write code against it: the header, the first example,
    /// the components to choose instead and the API reference; null for a guide. The full page stays the place for
    /// usage, every example, accessibility and motion.
    /// </summary>
    private static string? Reference(string baseUrl, string version, Page page)
    {
        if (page.Guide is not null || GalleryDocs.Get(page.Entry.ClassName, GalleryDocs.SourceLanguage) is not { } doc)
        {
            return null;
        }
        var markdown = doc.CopyMarkdown;
        var text = new StringBuilder();
        text.Append("# ").Append(doc.Title).Append(" reference\n\n> ").Append(doc.Summary).Append("\n\n");
        Header(text, baseUrl, version, page, GalleryDocs.SourceLanguage);
        text.Append("- Full page, with usage, every example, accessibility and motion: ").Append(baseUrl).Append('/').Append(MarkdownPath(page, GalleryDocs.SourceLanguage)).Append("\n\n");
        if (FirstExample().Match(markdown) is { Success: true } example)
        {
            text.Append("## Example\n\n").Append(example.Value.Trim()).Append("\n\n");
        }
        if (Alternatives(page.Entry.ClassName) is { Count: > 0 } alternatives)
        {
            text.Append("## Choose instead\n\n").Append(string.Join("\n", alternatives)).Append("\n\n");
        }
        var api = markdown.IndexOf("\n## API reference\n", StringComparison.Ordinal);
        if (api >= 0)
        {
            var end = markdown.IndexOf("\n## ", api + 1, StringComparison.Ordinal);
            text.Append(markdown[(api + 1)..(end < 0 ? markdown.Length : end)].Trim()).Append("\n\n");
        }
        text.Append("---\n\n- ").Append(Words.For(GalleryDocs.SourceLanguage).Index).Append(baseUrl).Append("/llms.txt\n");
        return text.ToString();
    }

    // The first fenced code block of a page: the example under its title.
    [GeneratedRegex(@"^```[a-z]*\n.*?\n```$", RegexOptions.Multiline | RegexOptions.Singleline)]
    private static partial Regex FirstExample();

    private static string Summary(Page page) => GalleryDocs.Summary(page.Entry.ClassName, GalleryDocs.SourceLanguage) ?? page.Entry.Description;

    private static string Package(Page page) => GalleryDocs.PackageOf(page.Entry.ClassName) ?? "TKCode.Arc";

    private const string ComponentsPackage = "TKCode.Arc";

    private const string MobilePackage = "TKCode.Arc.Mobile";

    private const string ArcXmlns = "xmlns:arc=\"https://github.com/AIDotNet/TKCode.Arc\"";

    private const string MobileXmlns = "xmlns:arcm=\"https://github.com/AIDotNet/TKCode.Arc.Mobile\"";

    /// <summary>
    /// How a class is written in XAML: <c>Button</c> for an Arc control (with the <c>arc:</c> prefix), and for a phone
    /// control of <c>TKCode.Arc.Mobile</c>, whose docs page is keyed <c>MobileButton</c>, <c>arcm:Button</c>: the
    /// control is <c>TKCode.Arc.Mobile.Button</c> in its own XAML namespace.
    /// </summary>
    public static string XamlName(string className)
        => (GalleryDocs.PackageOf(className) ?? ComponentsPackage) == MobilePackage && className.StartsWith("Mobile", StringComparison.Ordinal) && className.Length > 6
            ? "arcm:" + className[6..]
            : className;

    private static string XamlName(Page page) => XamlName(page.Entry.ClassName);

    private const string BlocksPackage = "TKCode.Arc.Blocks";

    /// <summary>A package and how it is registered, as a list line or group heading names it: <c>`TKCode.Arc.Special`, UseArcSpecial()</c>.</summary>
    private static string PackageNote(string package)
        => $"`{package}`, `{s_registration.GetValueOrDefault(package, "UseArcControls()")}`" + (s_onNuGet.Contains(package) ? string.Empty : ", not on NuGet yet");

    /// <summary>The other names of each documented class, from <c>Content/aliases.txt</c>; a class that has no page is an error.</summary>
    public static IReadOnlyDictionary<string, string> Aliases() => Aliases(null);

    private static Dictionary<string, string> Aliases(IReadOnlyCollection<Page>? pages)
    {
        var known = pages?.Select(p => p.Entry.ClassName).ToHashSet(StringComparer.Ordinal);
        var aliases = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var raw in Template("aliases.txt").Split('\n'))
        {
            var line = raw.Trim();
            if (line.Length == 0 || line.StartsWith('#'))
            {
                continue;
            }
            var colon = line.IndexOf(':');
            var name = colon > 0 ? line[..colon].Trim() : throw new InvalidOperationException($"aliases.txt: '{line}' is not 'Class: name, name'");
            if (known is not null && !known.Contains(name))
            {
                throw new InvalidOperationException($"aliases.txt: {name} is not a documented component or block");
            }
            if (!aliases.TryAdd(name, line[(colon + 1)..].Trim()))
            {
                throw new InvalidOperationException($"aliases.txt: {name} is listed twice");
            }
        }
        return aliases;
    }

    /// <summary>
    /// The site's <c>skills.md</c>: the skill, its links to the files beside it made absolute, then the index of every
    /// component and block, so one fetch gives an agent the workflow and the candidates.
    /// </summary>
    private static string EntryPoint(string skill, IReadOnlyDictionary<string, string> values)
    {
        foreach (var file in s_skillFiles)
        {
            skill = skill.Replace($"]({file})", $"]({values["skill"]}/{file})", StringComparison.Ordinal);
        }
        return skill.TrimEnd() + "\n\n" + Fill(Template("skills-index.md"), values);
    }

    private static readonly string[] s_skillFiles = ["components.md", "choosing.md", "checklist.md"];

    /// <summary>Usage subsections every page has, which say nothing about what a component can do.</summary>
    private static readonly HashSet<string> s_genericSections = new(StringComparer.OrdinalIgnoreCase)
    {
        "Anatomy", "Binding to a view model", "Content guidelines", "Best practices", "Common questions", "Data shape and performance",
    };

    /// <summary>
    /// What a page shows, as llms.txt's outline: the titles of its Usage subsections without the ones every page has
    /// and without the worked example (<c> Covers: Variants, Sizes, Icon-only button.</c>), or empty.
    /// </summary>
    public static string Covers(string className)
    {
        if (GalleryDocs.Get(className, GalleryDocs.SourceLanguage)?.CopyMarkdown is not { } markdown)
        {
            return string.Empty;
        }
        var start = markdown.IndexOf("\n## Usage\n", StringComparison.Ordinal);
        if (start < 0)
        {
            return string.Empty;
        }
        var end = markdown.IndexOf("\n## ", start + 1, StringComparison.Ordinal);
        var titles = markdown[start..(end < 0 ? markdown.Length : end)].Split('\n')
                             .Where(l => l.StartsWith("### ", StringComparison.Ordinal))
                             .Select(l => l[4..].Trim())
                             .Where(t => !s_genericSections.Contains(t) && !t.StartsWith("Example", StringComparison.Ordinal))
                             .ToArray();
        return titles.Length == 0 ? string.Empty : $" Covers: {string.Join(", ", titles)}.";
    }

    private static string Covers(Page page) => Covers(page.Entry.ClassName);

    private static string AlsoCalled(IReadOnlyDictionary<string, string> aliases, Page page)
        => aliases.TryGetValue(page.Entry.ClassName, out var names) ? $" Also called: {names}." : string.Empty;

    /// <summary>
    /// The nearby components to choose instead of <paramref name="page"/>'s and when, from its English page's Related
    /// section: its "Choose it when" table as one line per row (without the row of the component itself), or its bullets
    /// when it has no table.
    /// </summary>
    public static IReadOnlyList<string> Alternatives(string className)
    {
        if (GalleryDocs.Get(className, GalleryDocs.SourceLanguage)?.CopyMarkdown is not { } markdown)
        {
            return [];
        }
        var start = markdown.IndexOf("\n## Related\n", StringComparison.Ordinal);
        if (start < 0)
        {
            return [];
        }
        var end = markdown.IndexOf("\n## ", start + 1, StringComparison.Ordinal);
        var lines = markdown[start..(end < 0 ? markdown.Length : end)].Split('\n');
        var rows = lines.Where(l => l.StartsWith("| `", StringComparison.Ordinal))
                        .Select(l => l.Split('|', StringSplitOptions.TrimEntries))
                        .Where(c => c.Length >= 4 && c[1] != $"`{className}`")   // some tables list the component itself first
                        .Select(c => $"- {c[1]}: {c[2]}")
                        .ToList();
        if (rows.Count > 0)
        {
            return rows;
        }
        var bullets = new List<string>();
        foreach (var line in lines)
        {
            if (line.StartsWith("- ", StringComparison.Ordinal))
            {
                bullets.Add(line.TrimEnd());
            }
            else if (bullets.Count > 0 && line.StartsWith("  ", StringComparison.Ordinal) && line.Trim().Length > 0)
            {
                bullets[^1] += " " + line.Trim();
            }
        }
        return bullets;
    }

    /// <summary>A component's entry in choosing.md: its class and page, purpose, other names and the alternatives.</summary>
    private static string Choosing(string baseUrl, Page page, string note)
    {
        var text = new StringBuilder();
        text.Append("#### `").Append(XamlName(page)).Append("`\n\n")
            .Append('[').Append(page.Entry.Title).Append("](").Append(baseUrl).Append('/').Append(MarkdownPath(page, GalleryDocs.SourceLanguage)).Append(')')
            .Append(note).Append(": ").Append(Summary(page)).Append('\n');
        if (Alternatives(page.Entry.ClassName) is { Count: > 0 } alternatives)
        {
            text.Append("\nChoose instead:\n\n").Append(string.Join("\n", alternatives)).Append('\n');
        }
        return text.ToString().TrimEnd();
    }

    /// <summary>A line of a list of pages: <c>- [Title](url) note: description</c>, the form llms.txt lists files in.</summary>
    private static string Link(string baseUrl, Page page, string description, string note = "")
        => $"- [{page.Entry.Title}]({baseUrl}/{MarkdownPath(page, GalleryDocs.SourceLanguage)}){note}: {description}";

    /// <summary>
    /// The groups as <c>### Title</c> sections of lines. A group whose pages share a package other than
    /// <paramref name="defaultPackage"/> names it under its heading; in a mixed group each such line gets the note
    /// (the second argument of <paramref name="line"/>, empty otherwise). Decorative groups say so.
    /// </summary>
    private static string Groups(IEnumerable<(string Title, Page[] Pages)> groups, string defaultPackage, Func<Page, string, string> line, string separator = "\n")
        => string.Join("\n\n", groups.Select(g =>
        {
            var packages = g.Pages.Select(Package).Distinct(StringComparer.Ordinal).ToArray();
            var shared = packages.Length == 1 ? packages[0] : null;
            var head = new StringBuilder("### ").Append(g.Title).Append("\n\n");
            if (shared is not null && shared != defaultPackage)
            {
                head.Append("Package: ").Append(PackageNote(shared)).Append(".\n\n");
            }
            if (shared == MobilePackage)
            {
                head.Append("XAML namespace: `").Append(MobileXmlns).Append("`. The controls are `TKCode.Arc.Mobile.Button`, `.Page` and so on, written `arcm:Button`; the API tables call them `MobileButton` so they do not clash with the desktop names.\n\n");
            }
            if (s_decorative.Contains(g.Title))
            {
                head.Append("Decorative: effects and showpieces. Use one only when the brief asks for that effect, not for ordinary controls.\n\n");
            }
            return head + string.Join(separator, g.Pages.Select(p => line(p, shared is null && Package(p) != defaultPackage ? $" ({PackageNote(Package(p))})" : string.Empty)));
        }));

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
        var markdown = new StringBuilder();
        markdown.Append("# ").Append(doc.Title).Append("\n\n");
        markdown.Append("> ").Append(doc.Summary).Append("\n\n");
        Header(markdown, baseUrl, version, page, language);
        markdown.Append("- ").Append(words.Reference).Append(baseUrl).Append('/').Append(ReferencePath(page)).Append("\n\n");
        markdown.Append(Shots(baseUrl, doc.CopyMarkdown, $"Docs/{GalleryDocs.FolderOf(className)}"));
        return markdown.ToString();
    }

    /// <summary>The lines under a component page's summary: package and install command, XAML namespace, live page.</summary>
    private static void Header(StringBuilder markdown, string baseUrl, string version, Page page, string language)
    {
        var words = Words.For(language);
        var package = Package(page);
        markdown.Append("- ").Append(words.Package).Append(s_onNuGet.Contains(package)
            ? string.Format(words.OnNuGet, $"`{package}`", $"`dotnet add package {package} --version {version}`")
            : string.Format(words.NotOnNuGet, $"`{package}`", Repository, $"`{s_registration.GetValueOrDefault(package, "UseArcControls()")}`"));
        if (s_onNuGet.Contains(package) && package != ComponentsPackage && s_registration.TryGetValue(package, out var call))
        {
            markdown.Append(string.Format(words.Register, $"`{call}`"));
        }
        markdown.Append('\n');
        markdown.Append("- ").Append(words.Namespace).Append('`').Append(package == MobilePackage ? MobileXmlns : ArcXmlns).Append("`\n");
        markdown.Append("- ").Append(words.LivePage).Append(baseUrl).Append(page.Address).Append('\n');
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
    private sealed record Words(string Name, string Package, string OnNuGet, string NotOnNuGet, string Register, string Namespace,
                                string LivePage, string Reference, string Languages, string Index)
    {
        private static readonly Words s_english = new(
            "English", "Package: ", "{0}, {1}", "{0}, not on NuGet yet: reference the project from {1} and call {2}", ", registered with {0} in `UseArc(...)`",
            "XAML namespace: ", "Live page: ", "API reference only (English): ", "Languages: ", "Every page: ");

        private static readonly Words s_simplified = new(
            "简体中文", "包：", "{0}，{1}", "{0}，尚未发布到 NuGet：从 {1} 引用项目，并调用 {2}", "，在 `UseArc(...)` 中以 {0} 注册",
            "XAML 命名空间：", "在线页面：", "只看 API 参考（英文）：", "语言：", "全部页面：");

        private static readonly Words s_traditional = new(
            "繁體中文", "套件：", "{0}，{1}", "{0}，尚未發佈到 NuGet：從 {1} 參考專案，並呼叫 {2}", "，在 `UseArc(...)` 中以 {0} 註冊",
            "XAML 命名空間：", "線上頁面：", "只看 API 參考（英文）：", "語言：", "全部頁面：");

        public static Words For(string language) => language.ToLowerInvariant() switch
        {
            "zh-cn" => s_simplified,
            "zh-tw" => s_traditional,
            _ => s_english
        };
    }
}
