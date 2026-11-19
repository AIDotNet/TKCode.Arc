using StreamMark.Core.Diff;
using Xunit;

namespace StreamMark.Core.Tests.Diff;

public sealed class UnifiedDiffTests
{
    private const string Git = """
        diff --git a/src/App.cs b/src/App.cs
        index 83db48f..bf269f4 100644
        --- a/src/App.cs
        +++ b/src/App.cs
        @@ -10,7 +10,8 @@ public sealed class App
             public void Run()
             {
        -        var name = "old";
        -        Console.WriteLine(name);
        +        var name = "new";
        +        var greeting = $"Hello {name}";
        +        Console.WriteLine(greeting);
             }

             -- not a header, a deleted line starting with dashes
        diff --git a/README.md b/docs/README.md
        similarity index 90%
        rename from README.md
        rename to docs/README.md
        --- a/README.md
        +++ b/docs/README.md
        @@ -1,2 +1,2 @@
        -# Title
        +# New title
         body
        \ No newline at end of file
        """;

    [Fact]
    public void GitDiff_FilesHunksAndLineNumbers()
    {
        var doc = UnifiedDiff.Parse(Git);
        Assert.Equal(2, doc.Files.Length);
        var app = doc.Files[0];
        Assert.Equal("src/App.cs", app.DisplayPath);
        var hunk = Assert.Single(app.Hunks);
        Assert.Equal(10, hunk.OldStart);
        Assert.Equal("public sealed class App", hunk.Section);
        Assert.Equal(2, app.Deletions);
        Assert.Equal(3, app.Additions);
        var first = hunk.Lines[0];
        Assert.Equal((10, 10), (first.OldNumber!.Value, first.NewNumber!.Value));
        var deleted = hunk.Lines.First(l => l.Kind == DiffLineKind.Deleted);
        Assert.Equal(12, deleted.OldNumber);
        Assert.Null(deleted.NewNumber);
        var added = hunk.Lines.Last(l => l.Kind == DiffLineKind.Added);
        Assert.Equal(14, added.NewNumber);
        Assert.Equal("        Console.WriteLine(greeting);", added.Text);
        Assert.Equal(1, added.TextStart);

        var readme = doc.Files[1];
        Assert.Equal("README.md → docs/README.md", readme.DisplayPath);
        Assert.Contains(readme.Hunks[0].Lines, l => l.Kind == DiffLineKind.NoNewline);
    }

    [Fact]
    public void HeaderlessSnippet_AndNewFile()
    {
        var snippet = UnifiedDiff.Parse("-old\n+new\n same");
        var hunk = snippet.Files.Single().Hunks.Single();
        Assert.Equal(3, hunk.Lines.Length);
        Assert.Equal(1, hunk.Lines[0].OldNumber);

        var created = UnifiedDiff.Parse("--- /dev/null\n+++ b/new.txt\n@@ -0,0 +1,2 @@\n+a\n+b\n");
        var file = created.Files.Single();
        Assert.Null(file.OldPath);
        Assert.Equal("new.txt", file.DisplayPath);
        Assert.Equal(2, file.Additions);
    }

    [Fact]
    public void DashLinesInsideHunk_AreChanges_NotFileHeaders()
    {
        // 区段未满时 "--- x" 是删除了一行 "-- x"，"+++ y" 是新增了一行 "++ y"
        var doc = UnifiedDiff.Parse("--- a/f.md\n+++ b/f.md\n@@ -1,2 +1,2 @@\n--- separator\n+++ plus\n keep\n");
        var hunk = doc.Files.Single().Hunks.Single();
        Assert.Equal([DiffLineKind.Deleted, DiffLineKind.Added, DiffLineKind.Context], hunk.Lines.Select(l => l.Kind));
        Assert.Equal("-- separator", hunk.Lines[0].Text);
    }

    [Fact]
    public void Streaming_PrefixesNeverThrow()
    {
        for (var i = 0; i <= Git.Length; i++)
        {
            var doc = UnifiedDiff.Parse(Git[..i]);
            Assert.NotNull(doc);
        }
    }

    [Fact]
    public void Split_PairsDeletesWithAdds()
    {
        var hunk = UnifiedDiff.Parse(Git).Files[0].Hunks[0];
        var rows = DiffLayout.Split(hunk);
        // 2 行上下文 + max(2 删, 3 增) + 3 行上下文 / 删除
        var changed = rows.Where(r => r.Left?.Kind == DiffLineKind.Deleted || r.Right?.Kind == DiffLineKind.Added).ToList();
        Assert.Equal(3, changed.Count);
        Assert.NotNull(changed[0].Left);
        Assert.NotNull(changed[0].Right);
        Assert.Null(changed[2].Left);
        Assert.Equal(DiffLineKind.Added, changed[2].Right!.Kind);
    }

    [Fact]
    public void ChangedRanges_PrefixSuffix()
    {
        var ranges = DiffLayout.ChangedRanges("var name = \"old\";", "var name = \"new\";");
        Assert.NotNull(ranges);
        Assert.Equal((12, 3), ranges!.Value.Old);
        Assert.Equal((12, 3), ranges.Value.New);
        Assert.Null(DiffLayout.ChangedRanges("abc", "xyz"));
    }
}
