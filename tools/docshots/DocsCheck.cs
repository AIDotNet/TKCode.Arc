using TKCode.Arc.Tests.Gallery;

namespace TKCode.Arc.DocShots;

/// <summary>
/// <c>check &lt;page.md&gt;...</c>: the rules GalleryDocsTests and GalleryDemosTests enforce, per file and without a build,
/// plus the screenshot rules and a report of names the API does not have. FAIL lines must be fixed; WARN lines must be
/// looked at (a name the page invented, a screenshot not captured yet).
/// </summary>
internal static class DocsCheck
{
    /// <summary>The widest scene that fits the docs column without shrinking.</summary>
    private const double MaxSceneWidth = 840;

    public static int Run(string[] files)
    {
        var failures = 0;
        var warnings = 0;
        foreach (var file in files)
        {
            var (problems, notes, lines) = Check(file);
            failures += problems.Count;
            warnings += notes.Count;
            Console.WriteLine(problems.Count == 0 ? $"OK   {file} ({lines} lines)" : $"FAIL {file}");
            foreach (var problem in problems)
            {
                Console.WriteLine("       FAIL " + problem);
            }
            foreach (var note in notes)
            {
                Console.WriteLine("       WARN " + note);
            }
        }
        Console.WriteLine(failures == 0 ? $"ALL OK ({warnings} warning(s))" : $"{failures} problem(s), {warnings} warning(s)");
        return failures == 0 ? 0 : 1;
    }

    private static (List<string> Problems, List<string> Notes, int Lines) Check(string file)
    {
        var problems = new List<string>();
        var notes = new List<string>();
        if (!File.Exists(file))
        {
            return (["file not found"], notes, 0);
        }
        var page = DocFile.Load(file);
        CheckFrontMatter(page, problems);

        if (page.IsSource && page.IsGuide)
        {
            // A Docs-section page (Guides/<Page>/): free headings, no API line or demos, one # title.
            if (page.Lines.Count(l => l.StartsWith("# ", StringComparison.Ordinal)) != 1)
            {
                problems.Add("a guide page has exactly one '# ' title line");
            }
        }
        else if (page.IsSource)
        {
            if (!page.Headings.SequenceEqual(DocFile.Sections))
            {
                problems.Add($"sections must be exactly, in order: {string.Join(" | ", DocFile.Sections)}   (found: {string.Join(" | ", page.Headings)})");
            }
            if (!page.ApiTypes.Contains(page.ClassName))
            {
                problems.Add($"missing the line <!-- api: {page.ClassName} --> (the folder name is the control's class name)");
            }
            CheckDemos(page, problems);
        }
        else
        {
            CheckTranslation(page, problems);
        }

        foreach (var block in page.Blocks)
        {
            if (block.IsXaml)
            {
                if (DocSnippets.ValidateXaml(block.Code) is { } reason)
                {
                    problems.Add($"XAML block at line {block.Line} does not load: {reason}");
                }
            }
            else if (block.Language is not ("csharp" or "bash" or "json" or "text"))
            {
                notes.Add($"code block at line {block.Line} is '{block.Language}': use xml or csharp");
            }
        }

        CheckScreenshots(page, problems, notes);
        foreach (var (line, name) in ApiNames.Unknown(page))
        {
            notes.Add($"line {line}: `{name}` is not a TKCode.Arc / Avalonia / .NET name and the page's code does not declare it");
        }
        return (problems, notes, page.Lines.Length);
    }

    private static void CheckFrontMatter(DocFile page, List<string> problems)
    {
        if (!page.Text.StartsWith("---\n", StringComparison.Ordinal) || page.Text.IndexOf("\n---\n", 3, StringComparison.Ordinal) < 0)
        {
            problems.Add("front matter: the file must start with a --- block");
            return;
        }
        var front = page.Text.Substring(4, page.Text.IndexOf("\n---\n", 3, StringComparison.Ordinal) - 4).Split('\n');
        // Guides carry only a title; the sidebar entry has the summary.
        string[] keys = (page.IsSource, page.IsGuide) switch
        {
            (true, true) => ["title:"],
            (false, true) => ["title:", "source-hash:"],
            (true, false) => ["title:", "summary:"],
            (false, false) => ["title:", "summary:", "source-hash:"]
        };
        foreach (var key in keys)
        {
            if (!front.Any(l => l.StartsWith(key, StringComparison.Ordinal) && l.Length > key.Length + 1))
            {
                problems.Add($"front matter: missing {key}");
            }
        }
    }

    /// <summary>Every demo file of the component is shown (GDOC012) and every demo shown exists (GDOC010).</summary>
    private static void CheckDemos(DocFile page, List<string> problems)
    {
        var docs = Path.GetDirectoryName(Path.GetDirectoryName(page.Folder))!;
        var category = Path.GetFileName(Path.GetDirectoryName(page.Folder))!;
        var demos = Path.Combine(Path.GetDirectoryName(docs)!, "Demos", category, page.ClassName);
        var files = Directory.Exists(demos)
            ? Directory.GetFiles(demos, "*.axaml").Select(Path.GetFileNameWithoutExtension).ToList()
            : [];
        var shown = page.Demos;
        foreach (var name in files.Where(f => !shown.Contains(f!)))
        {
            problems.Add($"the demo Demos/{category}/{page.ClassName}/{name}.axaml is not shown: keep its <Demo name=\"{name}\" /> line");
        }
        foreach (var name in shown.Where(d => !d.Contains('.') && !files.Contains(d)))
        {
            problems.Add($"<Demo name=\"{name}\" /> has no file Demos/{category}/{page.ClassName}/{name}.axaml");
        }
        var firstSection = Array.FindIndex(page.Lines, l => l.StartsWith("## ", StringComparison.Ordinal));
        if (firstSection < 0 || !page.Lines.Take(firstSection).Any(l => l.TrimStart().StartsWith("<Demo ", StringComparison.Ordinal)))
        {
            problems.Add("the first <Demo> line (the page's preview) must stay above the first ## section");
        }
    }

    private static void CheckTranslation(DocFile page, List<string> problems)
    {
        var sourcePath = Path.Combine(page.Folder, "en-US.md");
        if (!File.Exists(sourcePath))
        {
            problems.Add("no en-US.md next to this translation");
            return;
        }
        var english = DocFile.Load(sourcePath);
        if (page.Headings.Count != english.Headings.Count)
        {
            problems.Add($"sections: {page.Headings.Count} '## ' headings but en-US.md has {english.Headings.Count}");
        }
        var mine = page.Blocks.Select(b => b.Language).ToList();
        var theirs = english.Blocks.Select(b => b.Language).ToList();
        if (!mine.SequenceEqual(theirs))
        {
            problems.Add($"code blocks: [{string.Join(",", mine)}] but en-US.md has [{string.Join(",", theirs)}]");
        }
        if (!page.ApiTypes.SequenceEqual(english.ApiTypes))
        {
            problems.Add("the <!-- api: ... --> lines differ from en-US.md");
        }
        if (!page.Demos.SequenceEqual(english.Demos))
        {
            problems.Add($"<Demo> lines: [{string.Join(",", page.Demos)}] but en-US.md has [{string.Join(",", english.Demos)}]");
        }
        var shots = page.Screenshots;
        var sourceShots = english.Screenshots;
        if (!shots.Select(s => s.Name).SequenceEqual(sourceShots.Select(s => s.Name)))
        {
            problems.Add($"screenshots: [{string.Join(",", shots.Select(s => s.Name))}] but en-US.md has [{string.Join(",", sourceShots.Select(s => s.Name))}]");
        }
        else
        {
            for (var i = 0; i < shots.Count; i++)
            {
                if (shots[i].Scene is { } scene && sourceShots[i].Scene is { } sourceScene && scene.Code != sourceScene.Code)
                {
                    problems.Add($"the XAML block under ![...](images/{shots[i].Name}.png) (line {scene.Line}) must be byte-identical to en-US.md's: the screenshot is captured from it");
                }
            }
        }
    }

    private static void CheckScreenshots(DocFile page, List<string> problems, List<string> notes)
    {
        foreach (var (line, text) in page.StrayImages)
        {
            problems.Add($"line {line}: an image must be a line of its own of the form ![Caption](images/Name.png): {text}");
        }
        var names = new HashSet<string>(StringComparer.Ordinal);
        foreach (var shot in page.Screenshots)
        {
            if (!names.Add(shot.Name))
            {
                problems.Add($"line {shot.Line}: the screenshot name '{shot.Name}' is used twice");
            }
            if (shot.Caption.Trim().Length == 0)
            {
                problems.Add($"line {shot.Line}: the screenshot needs a caption: ![What the picture shows](images/{shot.Name}.png)");
            }
            if (shot.Scene is null)
            {
                problems.Add($"line {shot.Line}: the next non-blank line after ![...](images/{shot.Name}.png) must open the ```xml block it is captured from");
                continue;
            }
            if (page.IsSource && Scene.WidthOf(shot.Scene.Code) is var width and > MaxSceneWidth)
            {
                problems.Add($"line {shot.Scene.Line}: the scene is {width} wide; at most {MaxSceneWidth} fits the docs column");
            }
            foreach (var variant in new[] { $"{shot.Name}.webp", $"{shot.Name}.dark.webp" })
            {
                if (!File.Exists(Path.Combine(page.ImagesFolder, variant)))
                {
                    notes.Add($"line {shot.Line}: images/{variant} does not exist yet (run: DocShots shoot {Path.Combine(page.Folder, "en-US.md")})");
                }
            }
        }
    }
}
