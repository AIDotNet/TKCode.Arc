namespace TKCode.Arc.Tests.Blocks.Ai;

/// <summary>The pure filtering of the "/" and "@" lists.</summary>
public class AiSuggestionFilterTests
{
    private static string[] Titles(IEnumerable<AiSuggestionRow> rows) => rows.Select(r => r.Title).ToArray();

    [Fact]
    public void An_Empty_Query_Lists_Every_Command_In_Source_Order()
    {
        var rows = AiSuggestionFilter.Commands(AiSuggestionSamples.Commands(), "");
        Titles(rows).ShouldBe(["recap", "rewrite", "outline", "debug"]);
        rows[0].ShouldBe(new AiSuggestionRow("recap", "recap", "Boil a doc or file down to its main points", ArcIcons.FileText, null));
        rows.ShouldAllBe(r => r.Group == null);
    }

    [Fact]
    public void Commands_Match_By_Name_Prefix_Ignoring_Case()
    {
        Titles(AiSuggestionFilter.Commands(AiSuggestionSamples.Commands(), "re"))
            .ShouldBe(["recap", "rewrite"]);
        Titles(AiSuggestionFilter.Commands(AiSuggestionSamples.Commands(), "REW"))
            .ShouldBe(["rewrite"]);
        // A match inside the name is not a prefix; descriptions are not searched.
        AiSuggestionFilter.Commands(AiSuggestionSamples.Commands(), "line").ShouldBeEmpty();
        AiSuggestionFilter.Commands(AiSuggestionSamples.Commands(), "tone").ShouldBeEmpty();
    }

    [Fact]
    public void Commands_Without_A_Name_And_Null_Inputs_Are_Skipped()
    {
        var commands = new[] { new AiCommand { Id = "blank" }, new AiCommand { Id = "x", Name = "explain" } };
        Titles(AiSuggestionFilter.Commands(commands, null)).ShouldBe(["explain"]);
        AiSuggestionFilter.Commands(null, "a").ShouldBeEmpty();
        AiSuggestionFilter.Mentions(null, AiSuggestionSamples.Groups, "a").ShouldBeEmpty();
    }

    [Fact]
    public void An_Empty_Query_Lists_Every_Mention_Grouped_In_Group_Order()
    {
        var rows = AiSuggestionFilter.Mentions(AiSuggestionSamples.Mentions(), ["Docs", "Files"], "");
        Titles(rows).ShouldBe(["Sprint 15 review", "Export timeouts", "Onboarding flow v2", "export-worker.ts", "usage-oct.csv", "style-guide.pdf"]);
        rows.Select(r => r.Group).ShouldBe(["Docs", "Docs", "Docs", "Files", "Files", "Files"]);
        rows[0].Hint.ShouldBe("Wiki, edited Mar 3");
        rows[0].Id.ShouldBe("d-sprint");
    }

    [Fact]
    public void Mentions_Match_The_Label_Or_The_Hint_Anywhere_Ignoring_Case()
    {
        // "export" is in two labels (one a prefix match) and in a hint.
        var rows = AiSuggestionFilter.Mentions(AiSuggestionSamples.Mentions(), AiSuggestionSamples.Groups, "EXPORT");
        Titles(rows).ShouldBe(["export-worker.ts", "Export timeouts"]);
        // Hints are searched too ("monthly" is only in a hint; "ops-318" in another, case ignored).
        Titles(AiSuggestionFilter.Mentions(AiSuggestionSamples.Mentions(), AiSuggestionSamples.Groups, "monthly")).ShouldBe(["usage-oct.csv"]);
        Titles(AiSuggestionFilter.Mentions(AiSuggestionSamples.Mentions(), AiSuggestionSamples.Groups, "ops-318")).ShouldBe(["Export timeouts"]);
        AiSuggestionFilter.Mentions(AiSuggestionSamples.Mentions(), AiSuggestionSamples.Groups, "zzz").ShouldBeEmpty();
    }

    [Fact]
    public void Groups_Without_Matches_Are_Dropped()
    {
        var rows = AiSuggestionFilter.Mentions(AiSuggestionSamples.Mentions(), AiSuggestionSamples.Groups, "edited");
        Titles(rows).ShouldBe(["Sprint 15 review", "Onboarding flow v2"]);
        rows.ShouldAllBe(r => r.Group == "Docs");
    }

    [Fact]
    public void Label_Prefix_Matches_Come_First_Within_A_Group_And_Order_Is_Otherwise_Kept()
    {
        var items = new[]
        {
            new AiContextItem { Id = "1", Label = "plan-b.md", Hint = "drafts", Group = "Files" },
            new AiContextItem { Id = "2", Label = "draft-notes.md", Hint = "inbox", Group = "Files" },
            new AiContextItem { Id = "3", Label = "old-draft.md", Hint = "archive", Group = "Files" },
            new AiContextItem { Id = "4", Label = "Drafting guide", Hint = "Wiki", Group = "Docs" },
            new AiContextItem { Id = "5", Label = "Draft review", Hint = "Wiki", Group = "Files" }
        };
        var rows = AiSuggestionFilter.Mentions(items, AiSuggestionSamples.Groups, "draft");
        rows.Select(r => r.Id).ShouldBe(["2", "5", "1", "3", "4"]);
    }

    [Fact]
    public void Items_With_An_Unknown_Or_Null_Group_Come_Last_Without_A_Heading()
    {
        var items = new[]
        {
            new AiContextItem { Id = "u1", Label = "loose.txt", Group = "Elsewhere" },
            new AiContextItem { Id = "d1", Label = "Roadmap", Group = "Docs" },
            new AiContextItem { Id = "n1", Label = "scratch.txt" },
            new AiContextItem { Id = "f1", Label = "main.ts", Group = "Files" },
            // Group names compare exactly: "files" is not "Files".
            new AiContextItem { Id = "u2", Label = "lower.ts", Group = "files" }
        };
        var rows = AiSuggestionFilter.Mentions(items, AiSuggestionSamples.Groups, "");
        rows.Select(r => r.Id).ShouldBe(["f1", "d1", "u1", "n1", "u2"]);
        rows.Select(r => r.Group).ShouldBe(["Files", "Docs", null, null, null]);
    }

    [Fact]
    public void Without_A_Group_Order_Everything_Is_One_Untitled_Group()
    {
        var rows = AiSuggestionFilter.Mentions(AiSuggestionSamples.Mentions(), null, "s");
        rows.ShouldAllBe(r => r.Group == null);
        // Prefix matches first ("style-guide.pdf", "Sprint 15 review"), then the rest in source order.
        Titles(rows).Take(2).ShouldBe(["style-guide.pdf", "Sprint 15 review"]);
        rows.Count.ShouldBe(6);
    }

    [Fact]
    public void A_Repeated_Group_Name_In_The_Order_Counts_Once()
    {
        var rows = AiSuggestionFilter.Mentions(AiSuggestionSamples.Mentions(), ["Docs", "Docs", "Files"], "");
        rows.Select(r => r.Group).Distinct().ShouldBe(["Docs", "Files"]);
        rows.Count.ShouldBe(6);
    }
}
