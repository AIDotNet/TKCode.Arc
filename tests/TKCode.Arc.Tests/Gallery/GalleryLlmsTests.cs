using System.Text.RegularExpressions;
using TKCode.Arc.Gallery.Catalog;
using TKCode.Arc.Gallery.Docs;
using TKCode.Arc.Gallery.Llms;
using TKCode.Arc.Gallery.Shell;
using Avalonia.Headless.XUnit;
using Avalonia.Platform;

namespace TKCode.Arc.Tests.Gallery;

/// <summary>
/// The files the web host serves to AI assistants (<see cref="LlmsSite"/>): a Markdown file for every page in every
/// language it is written in, an llms.txt that links each page once, links and screenshots that resolve, and a skill
/// whose front matter agents accept.
/// </summary>
public class GalleryLlmsTests
{
    private const string BaseUrl = "https://docs.example.test";

    [AvaloniaFact]
    public void Every_Page_Has_A_Markdown_File_In_Each_Of_Its_Languages()
    {
        var files = LlmsSite.Build(BaseUrl).ToDictionary(f => f.Path, f => f.Text, StringComparer.Ordinal);
        var entries = GalleryCatalog.AllEntries.Where(e => GalleryCatalog.Docs.Entries.Contains(e) || GalleryDocs.Has(e.ClassName)).ToList();
        entries.Count.ShouldBeGreaterThan(GalleryCatalog.Count);
        foreach (var entry in entries)
        {
            var path = GalleryRoute.To(entry).Path.TrimStart('/') + ".md";
            var languages = GalleryCatalog.Docs.Entries.Contains(entry) ? GalleryGuides.Languages : GalleryDocs.Languages(entry.ClassName);
            foreach (var language in languages)
            {
                var file = language == GalleryDocs.SourceLanguage ? path : $"{language}/{path}";
                files.ShouldContainKey(file);
                files[file].ShouldStartWith("# ", customMessage: file);
            }
        }
    }

    [AvaloniaFact]
    public void A_Component_Page_Is_Its_Copied_Page_Under_A_Header()
    {
        var files = LlmsSite.Build(BaseUrl).ToDictionary(f => f.Path, f => f.Text, StringComparer.Ordinal);
        var button = files["components/button.md"];
        button.ShouldStartWith("# Button\n\n> The default button for one action, in primary, secondary, ghost and danger weights, three sizes and a loading state that keeps its width.\n\n- Package: `TKCode.Arc`, `dotnet add package TKCode.Arc --version ");
        button.ShouldContain(GalleryDocs.Get("Button", GalleryDocs.SourceLanguage)!.CopyMarkdown.Split("\n## ")[1]);
        button.ShouldNotContain("<Demo ");
        button.ShouldNotContain("<!-- api:");
        button.ShouldContain($"[简体中文]({BaseUrl}/zh-CN/components/button.md)");

        files["zh-CN/components/button.md"].ShouldContain("- 包：`TKCode.Arc`");
        // Every package is on NuGet since 0.3.0-preview.5: block and data grid pages give the install command too.
        files["zh-TW/blocks/sidebar.md"].ShouldContain("- 套件：`TKCode.Arc.Blocks`，`dotnet add package TKCode.Arc.Blocks --version ");
        files["components/data-grid.md"].ShouldContain("- Package: `TKCode.Arc.AdvancedCharts`, `dotnet add package TKCode.Arc.AdvancedCharts --version ");
    }

    [AvaloniaFact]
    public void Llms_Txt_Links_Every_English_Page_Once()
    {
        var files = LlmsSite.Build(BaseUrl);
        var index = files.Single(f => f.Path == "llms.txt").Text;
        index.ShouldStartWith("# TKCode.Arc\n\n> ");
        var linked = Regex.Matches(index, @"^- \[[^\]]+\]\(([^)]+)\)", RegexOptions.Multiline).Select(m => m.Groups[1].Value).ToList();
        var pages = files.Select(f => f.Path).Where(p => p.EndsWith(".md", StringComparison.Ordinal) && !p.StartsWith("zh-", StringComparison.Ordinal) && !p.StartsWith("skills/", StringComparison.Ordinal)
                                                            && !p.EndsWith(".api.md", StringComparison.Ordinal));
        foreach (var page in pages)
        {
            linked.Count(l => l == $"{BaseUrl}/{page}").ShouldBe(1, page);
        }
        index.ShouldContain($"[SKILL.md]({BaseUrl}/skills/tkcode-arc/SKILL.md)");
    }

    [AvaloniaFact]
    public void Links_And_Screenshots_Resolve()
    {
        var files = LlmsSite.Build(BaseUrl);
        LlmsSite.BrokenLinks(files, BaseUrl).ShouldBeEmpty();
        // The generator fills the {{name}} placeholders of its own hand-written text (llms.txt and the agent skill); the pages
        // quote double braces on purpose (the prompt studio's variables), so they are not checked.
        files.Where(f => (f.Path == "llms.txt" || f.Path.StartsWith("skills/", StringComparison.Ordinal)) && f.Text.Contains("{{", StringComparison.Ordinal))
            .Select(f => f.Path).ShouldBeEmpty("unfilled placeholders");

        // The Browser host publishes the screenshots resources of TKCode.Arc.Gallery.Shots as files under shots/.
        var shots = files.SelectMany(f => Regex.Matches(f.Text, Regex.Escape(BaseUrl) + @"/shots/([^)\s]+\.webp)").Select(m => m.Groups[1].Value))
                         .Distinct()
                         .ToList();
        shots.ShouldNotBeEmpty();
        shots.Where(s => !AssetLoader.Exists(new Uri($"avares://TKCode.Arc.Gallery.Shots/{s}"))).ShouldBeEmpty();
    }

    [AvaloniaFact]
    public void The_Catalog_Names_Each_Package_Outside_TKCode_Arc()
    {
        var catalog = LlmsSite.Build(BaseUrl).Single(f => f.Path == $"skills/{LlmsSite.SkillName}/components.md").Text;
        var components = catalog[..catalog.IndexOf("\n## Blocks", StringComparison.Ordinal)];
        // Every line of a component outside TKCode.Arc names its package, on the line or under its group's heading.
        foreach (var section in components.Split("\n### ").Skip(1))
        {
            foreach (var line in section.Split('\n').Where(l => l.StartsWith("- [", StringComparison.Ordinal)))
            {
                var slug = Regex.Match(line, @"/components/([a-z0-9-]+)\.md").Groups[1].Value;
                var entry = GalleryCatalog.AllEntries.First(e => GalleryRoute.To(e).Path == "/components/" + slug);
                var package = GalleryDocs.PackageOf(entry.ClassName) ?? "TKCode.Arc";
                if (package != "TKCode.Arc")
                {
                    (line.Contains($"`{package}`", StringComparison.Ordinal) || section.Contains($"Package: `{package}`", StringComparison.Ordinal)).ShouldBeTrue(line);
                }
            }
        }
        components.ShouldContain("Package: `TKCode.Arc.Special`, `UseArcSpecial()`.");
        components.ShouldContain("Package: `TKCode.Arc.Mobile`, `UseArcMobile()`.");
        // Phone controls are written in their own XAML namespace, without the Mobile prefix of their page name.
        components.ShouldContain("`arcm:Button`. ");
        components.ShouldNotContain("`MobileButton`. ");
        LlmsSite.XamlName("Button").ShouldBe("Button");
        var mobile = LlmsSite.Build(BaseUrl).Single(f => f.Path == "components/mobile-button.md").Text;
        mobile.ShouldContain("- XAML namespace: `xmlns:arcm=\"https://github.com/AIDotNet/TKCode.Arc.Mobile\"`");
    }

    [AvaloniaFact]
    public void The_Catalog_Keeps_Whole_Summaries_And_Other_Names()
    {
        var catalog = LlmsSite.Build(BaseUrl).Single(f => f.Path == $"skills/{LlmsSite.SkillName}/components.md").Text;
        // A summary that mentions "#channel" is not cut at the "#".
        catalog.ShouldContain("`MentionInput`. A textarea with @people and #channel mentions that act as single tokens, with suggestions at the caret.");
        catalog.ShouldContain("`Dialog`. ");
        Regex.IsMatch(catalog, @"`Dialog`\. [^\n]*Also called: modal,").ShouldBeTrue();
        LlmsSite.Aliases().Count.ShouldBeGreaterThan(100);
    }

    [AvaloniaFact]
    public void Choosing_Lists_The_Alternatives_Of_Each_Component()
    {
        var choosing = LlmsSite.Build(BaseUrl).Single(f => f.Path == $"skills/{LlmsSite.SkillName}/choosing.md").Text;
        LlmsSite.Alternatives("Select").ShouldContain("- `Combobox`: The list is long, or people type to find the value");
        LlmsSite.Alternatives("AgentStatus").ShouldNotContain(a => a.StartsWith("- `AgentStatus`", StringComparison.Ordinal));
        choosing.ShouldContain("#### `Select`\n\n[Select](" + BaseUrl + "/components/select.md): ");
        // Every documented component and block has a section, and every one of them has alternatives to name.
        var documented = GalleryCatalog.AllEntries.Where(e => GalleryDocs.Has(e.ClassName)).Select(e => e.ClassName).Distinct().ToList();
        foreach (var name in documented)
        {
            choosing.ShouldContain($"#### `{LlmsSite.XamlName(name)}`\n", customMessage: name);
            LlmsSite.Alternatives(name).ShouldNotBeEmpty(name);
        }
    }

    [AvaloniaFact]
    public void Skills_Md_Is_The_Skill_With_Its_Index_In_One_File()
    {
        var files = LlmsSite.Build(BaseUrl);
        var entry = files.Single(f => f.Path == "skills.md").Text;
        var skill = files.Single(f => f.Path == $"skills/{LlmsSite.SkillName}/SKILL.md").Text;
        entry.ShouldStartWith(skill[..skill.IndexOf("\n# ", StringComparison.Ordinal)]);
        // At the web root the skill's relative links to the files beside it would not resolve.
        entry.ShouldNotContain("](components.md)");
        entry.ShouldNotContain("](choosing.md)");
        entry.ShouldContain($"]({BaseUrl}/skills/{LlmsSite.SkillName}/choosing.md)");
        entry.ShouldContain($"- [Select]({BaseUrl}/components/select.md): `Select`. ");
        files.Single(f => f.Path == "llms.txt").Text.ShouldContain($"[skills.md]({BaseUrl}/skills.md)");
    }

    [AvaloniaFact]
    public void Each_Component_Has_A_Short_Reference_Beside_Its_Page()
    {
        var files = LlmsSite.Build(BaseUrl).ToDictionary(f => f.Path, f => f.Text, StringComparer.Ordinal);
        var pages = files.Keys.Where(p => (p.StartsWith("components/", StringComparison.Ordinal) || p.StartsWith("blocks/", StringComparison.Ordinal))
                                          && !p.EndsWith(".api.md", StringComparison.Ordinal)).ToList();
        pages.Count.ShouldBeGreaterThan(200);
        foreach (var page in pages)
        {
            var reference = page[..^3] + ".api.md";
            files.ShouldContainKey(reference);
            files[reference].ShouldContain("\n## API reference\n", customMessage: reference);
            files[reference].Length.ShouldBeLessThan(files[page].Length, reference);
            files[page].ShouldContain($"{BaseUrl}/{reference}");
        }
        var select = files["components/select.api.md"];
        select.ShouldStartWith("# Select reference\n\n> ");
        select.ShouldContain("## Example\n\n```xml\n");
        select.ShouldContain("## Choose instead\n\n- `Combobox`: ");
        files["components/agent-status.api.md"].ShouldContain("- Package: `TKCode.Arc.Special`, `dotnet add package TKCode.Arc.Special --version ");
        files["components/agent-status.api.md"].ShouldContain(", registered with `UseArcSpecial()` in `UseArc(...)`");
    }

    [AvaloniaFact]
    public void Llms_Txt_Outlines_What_Each_Page_Covers()
    {
        LlmsSite.Covers("Button").ShouldBe(" Covers: Variants, Sizes, Loading and label morph, Icon-only button, Disabled.");
        var index = LlmsSite.Build(BaseUrl).Single(f => f.Path == "llms.txt").Text;
        index.ShouldContain("Covers: Variants, Sizes, Loading and label morph, Icon-only button, Disabled.");
    }

    [AvaloniaFact]
    public void The_Skill_Has_A_Front_Matter_Agents_Accept()
    {
        var skill = LlmsSite.Build(BaseUrl).Single(f => f.Path == $"skills/{LlmsSite.SkillName}/SKILL.md").Text;
        var front = Regex.Match(skill, @"\A---\nname: ([^\n]+)\ndescription: ([^\n]+)\n---\n");
        front.Success.ShouldBeTrue();
        front.Groups[1].Value.ShouldBe(LlmsSite.SkillName);
        Regex.IsMatch(front.Groups[1].Value, "^[a-z0-9-]{1,64}$").ShouldBeTrue();
        // Agent skill descriptions are at most 1024 characters and carry no XML tags.
        front.Groups[2].Value.Length.ShouldBeLessThanOrEqualTo(1024);
        front.Groups[2].Value.ShouldNotContain("<");
    }
}
