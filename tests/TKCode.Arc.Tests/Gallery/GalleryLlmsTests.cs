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
        button.ShouldStartWith("# Button\n\n> A clear, responsive action with quiet secondary states.\n\n- Package: `TKCode.Arc`, `dotnet add package TKCode.Arc --version ");
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
        var pages = files.Select(f => f.Path).Where(p => p.EndsWith(".md", StringComparison.Ordinal) && !p.StartsWith("zh-", StringComparison.Ordinal) && !p.StartsWith("skills/", StringComparison.Ordinal));
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
