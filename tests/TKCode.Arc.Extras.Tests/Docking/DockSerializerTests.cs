using System.Text.Json;
using Avalonia;
using Avalonia.Headless.XUnit;
using Avalonia.Layout;

namespace TKCode.Arc.Tests.Docking;

/// <summary>Saving and restoring layouts (spec 4.6): the format, round trips and the read rules.</summary>
public class DockSerializerTests
{
    private static DockLayout LayoutOf(DockSample sample) => new() { Workspace = sample.Workspace };

    /// <summary>The spec's example, with the floats bounds and an auto-hidden pane.</summary>
    private const string SpecExample = """
        {
          "format": "tkcode-arc-dock",
          "version": 1,
          "active": "doc:readme",
          "root": { "split": "h", "size": 1, "children": [
              { "group": ["explorer", "search"], "active": "explorer", "size": 22 },
              { "split": "v", "size": 78, "children": [
                  { "documents": { "group": ["doc:readme"], "active": "doc:readme" }, "size": 70 },
                  { "group": ["terminal"], "size": 30 } ] } ] },
          "hidden": { "left": [], "right": ["outline"], "top": [], "bottom": [] },
          "floats": [ { "bounds": [1320, 180, 420, 560], "maximized": false, "root": { "group": ["problems"] } } ]
        }
        """;

    [AvaloniaFact]
    public void Save_Writes_The_Documented_Format()
    {
        var sample = new DockSample();
        var layout = LayoutOf(sample);
        layout.ActivePane = sample.Readme;
        using var json = JsonDocument.Parse(layout.SaveLayout());
        var root = json.RootElement;
        root.GetProperty("format").GetString().ShouldBe("tkcode-arc-dock");
        root.GetProperty("version").GetInt32().ShouldBe(1);
        root.GetProperty("active").GetString().ShouldBe("doc:readme");
        var tree = root.GetProperty("root");
        tree.GetProperty("split").GetString().ShouldBe("h");
        tree.GetProperty("size").GetDouble().ShouldBe(1);
        var children = tree.GetProperty("children");
        children[0].GetProperty("group").EnumerateArray().Select(e => e.GetString()).ShouldBe(["explorer", "search"]);
        children[0].GetProperty("active").GetString().ShouldBe("explorer");
        children[0].GetProperty("size").GetDouble().ShouldBe(22);
        children[1].GetProperty("split").GetString().ShouldBe("v");
        var documents = children[1].GetProperty("children")[0];
        documents.GetProperty("size").GetDouble().ShouldBe(70);
        documents.GetProperty("documents").GetProperty("group")[0].GetString().ShouldBe("doc:readme");
        documents.GetProperty("documents").TryGetProperty("size", out _).ShouldBeFalse();
        root.GetProperty("hidden").GetProperty("right").GetArrayLength().ShouldBe(0);
        root.GetProperty("floats").GetArrayLength().ShouldBe(0);
        root.TryGetProperty("closed", out _).ShouldBeFalse();
    }

    [AvaloniaFact]
    public void A_Saved_Layout_Round_Trips()
    {
        var sample = new DockSample();
        var outline = DockSample.Tool("outline", "Outline", DockSide.Right);
        var problems = DockSample.Tool("problems", "Problems", DockSide.Bottom);
        sample.Workspace.RightHidden.Add(outline);
        sample.Workspace.Floats.Add(new DockFloat(new DockGroup(problems), new Rect(1320, 180, 420, 560)) { IsMaximized = true });
        sample.ToolGroup.ActivePane = sample.Search;
        var layout = LayoutOf(sample);
        layout.ActivePane = sample.Terminal;
        var saved = layout.SaveLayout();

        // Scramble the layout, then load the saved one back.
        layout.Move(sample.Terminal, DockTarget.Edge(DockPosition.Top)).ShouldBeTrue();
        layout.Move(sample.Explorer, DockTarget.Edge(DockPosition.Right)).ShouldBeTrue();
        layout.LoadLayoutCore(saved, workArea: null).ShouldBeTrue();

        layout.SaveLayout().ShouldBe(saved);
        layout.ActivePane.ShouldBe(sample.Terminal);
        sample.Search.Group!.ActivePane.ShouldBe(sample.Search);
        outline.State.ShouldBe(DockPaneState.AutoHidden);
        problems.State.ShouldBe(DockPaneState.Floating);
        layout.Workspace.Floats[0].IsMaximized.ShouldBeTrue();
        layout.Workspace.Floats[0].Bounds.ShouldBe(new Rect(1320, 180, 420, 560));
        sample.Readme.Group!.IsDocumentGroup.ShouldBeTrue();
    }

    [AvaloniaFact]
    public void The_Spec_Example_Loads_Into_The_Matching_Panes()
    {
        var sample = new DockSample();
        var layout = LayoutOf(sample);
        var outline = DockSample.Tool("outline", "Outline", DockSide.Right);
        var problems = DockSample.Tool("problems", "Problems", DockSide.Bottom);
        var resolved = new List<string>();
        layout.PaneResolver = id =>
        {
            resolved.Add(id);
            return id switch { "outline" => outline, "problems" => problems, _ => null };
        };
        layout.LoadLayoutCore(SpecExample, workArea: null).ShouldBeTrue();
        resolved.ShouldBe(["outline", "problems"]);
        var root = layout.Workspace.Root.ShouldBeOfType<DockSplit>();
        root.Orientation.ShouldBe(Orientation.Horizontal);
        root.Children[0].ShouldBeOfType<DockGroup>().Panes.ShouldBe([sample.Explorer, sample.Search]);
        root.Children[0].Size.ShouldBe(22);
        var right = root.Children[1].ShouldBeOfType<DockSplit>();
        right.Orientation.ShouldBe(Orientation.Vertical);
        right.Children[0].ShouldBeOfType<DockDocumentArea>().Root.ShouldBeOfType<DockGroup>().Panes.ShouldBe([sample.Readme]);
        right.Children[1].Size.ShouldBe(30);
        layout.Workspace.RightHidden.ShouldBe([outline]);
        layout.Workspace.Floats.Count.ShouldBe(1);
        layout.ActivePane.ShouldBe(sample.Readme);
    }

    [AvaloniaFact]
    public void Unknown_Fields_Are_Ignored()
    {
        var sample = new DockSample();
        var layout = LayoutOf(sample);
        const string json = """
            { "format": "tkcode-arc-dock", "version": 1, "theme": { "dark": true }, "extra": [1, 2, { "a": null }],
              "root": { "group": ["terminal", "explorer"], "size": 1, "pinned": true, "meta": { "x": [1] } } }
            """;
        layout.LoadLayoutCore(json, workArea: null).ShouldBeTrue();
        var group = layout.Workspace.Root.ShouldBeOfType<DockSplit>().Children
            .OfType<DockGroup>().First(g => g.Panes.Contains(sample.Terminal));
        group.Panes.Take(2).ShouldBe([sample.Terminal, sample.Explorer]);
    }

    [AvaloniaFact]
    public void A_Newer_Version_Or_Bad_Text_Is_Refused_And_The_Layout_Stays()
    {
        var sample = new DockSample();
        var layout = LayoutOf(sample);
        var before = layout.SaveLayout();
        string[] bad =
        [
            SpecExample.Replace("\"version\": 1", "\"version\": 2"),
            SpecExample.Replace("tkcode-arc-dock", "other-dock"),
            SpecExample.Replace("\"version\": 1,", ""),
            "{ \"format\": \"tkcode-arc-dock\", \"version\": 1, \"root\": { \"group\": [\"a\"], \"split\": \"h\" } }",
            "{ \"format\": \"tkcode-arc-dock\", \"version\": 1, \"root\": { \"split\": \"diagonal\" } }",
            "{ \"format\": \"tkcode-arc-dock\", \"version\": 1, \"root\": { \"split\": \"h\", \"children\": [ { \"documents\": null }, { \"documents\": null } ] } }",
            "{ \"format\": \"tkcode-arc-dock\", \"version\": 1, \"floats\": [ { \"bounds\": [1, 2], \"root\": null } ] }",
            "{ \"format\": \"tkcode-arc-dock\", ",
            "not json",
            ""
        ];
        foreach (var text in bad)
        {
            layout.LoadLayoutCore(text, workArea: null).ShouldBeFalse(text);
        }
        layout.SaveLayout().ShouldBe(before);
        sample.Workspace.Root.ShouldBe(sample.Root);
    }

    [AvaloniaFact]
    public void Ids_Nobody_Knows_Are_Skipped_And_Empty_Groups_Are_Tidied()
    {
        var sample = new DockSample();
        var layout = LayoutOf(sample);
        const string json = """
            { "format": "tkcode-arc-dock", "version": 1,
              "root": { "split": "h", "children": [
                  { "group": ["ghost", "phantom"], "size": 40 },
                  { "split": "h", "size": 60, "children": [ { "group": ["explorer", "explorer"] }, { "group": ["search"] } ] } ] } }
            """;
        layout.LoadLayoutCore(json, workArea: null).ShouldBeTrue();
        layout.Workspace.FindPane("ghost").ShouldBeNull();
        // The ghost group is gone, the inner split merged into the outer one, and the repeated id was placed once.
        layout.Workspace.Groups().ShouldAllBe(g => g.Panes.Count > 0);
        sample.Explorer.Group!.Panes.ShouldBe([sample.Explorer]);
        sample.Search.Group!.Panes.ShouldBe([sample.Search]);
        var row = sample.Explorer.Group.Parent.ShouldBeOfType<DockSplit>();
        row.Orientation.ShouldBe(Orientation.Horizontal);
        row.Children.Take(2).ShouldBe([sample.Explorer.Group, sample.Search.Group]);
    }

    [AvaloniaFact]
    public void Known_Panes_Missing_From_The_Layout_Go_Back_By_Their_Preference_Unless_Saved_As_Closed()
    {
        var sample = new DockSample();
        var layout = LayoutOf(sample);
        const string json = """
            { "format": "tkcode-arc-dock", "version": 1, "root": { "group": ["explorer"] } }
            """;
        layout.LoadLayoutCore(json, workArea: null).ShouldBeTrue();
        // Search joins the explorer (same side), the terminal gets a bottom group, the document gets a document area.
        sample.Search.Group.ShouldBe(sample.Explorer.Group);
        sample.Terminal.Group.ShouldNotBeNull();
        sample.Terminal.Group!.Parent.ShouldBeOfType<DockSplit>().Orientation.ShouldBe(Orientation.Vertical);
        sample.Readme.Group!.IsDocumentGroup.ShouldBeTrue();

        // A pane closed when the layout was saved stays closed.
        layout.Close(sample.Terminal).ShouldBeTrue();
        var saved = layout.SaveLayout();
        saved.ShouldContain("\"closed\"");
        layout.LoadLayoutCore(saved, workArea: null).ShouldBeTrue();
        sample.Terminal.Workspace.ShouldBeNull();
        // ... but a layout that does not mention it brings it back.
        layout.LoadLayoutCore(json, workArea: null).ShouldBeTrue();
        sample.Terminal.Workspace.ShouldBe(layout.Workspace);
    }

    [AvaloniaFact]
    public void A_Floating_Window_Off_Screen_Is_Pulled_Back()
    {
        var work = new Rect(0, 0, 1440, 900);
        DockLayoutSerializer.ClampToWorkArea(new Rect(100, 100, 400, 300), work).ShouldBe(new Rect(100, 100, 400, 300));
        DockLayoutSerializer.ClampToWorkArea(new Rect(3000, 200, 400, 300), work).ShouldBe(new Rect(1040, 200, 400, 300));
        DockLayoutSerializer.ClampToWorkArea(new Rect(-2000, -500, 400, 300), work).ShouldBe(new Rect(0, 0, 400, 300));
        // A window that still shows enough of itself stays where it is.
        DockLayoutSerializer.ClampToWorkArea(new Rect(-100, 50, 3000, 2000), work).ShouldBe(new Rect(-100, 50, 3000, 2000));

        var sample = new DockSample();
        var layout = LayoutOf(sample);
        var problems = DockSample.Tool("problems", "Problems", DockSide.Bottom);
        layout.PaneResolver = id => id == "problems" ? problems : null;
        layout.LoadLayoutCore(SpecExample.Replace("[1320, 180, 420, 560]", "[2600, 180, 420, 560]"), work).ShouldBeTrue();
        layout.Workspace.Floats[0].Bounds.ShouldBe(new Rect(1020, 180, 420, 560));
    }

    [AvaloniaFact]
    public void Saved_Shares_Are_Rounded_And_Hidden_Panes_Are_Listed_By_Edge()
    {
        var sample = new DockSample();
        sample.ToolGroup.Size = 100.0 / 3;
        var outline = DockSample.Tool("outline", "Outline", DockSide.Right);
        sample.Workspace.TopHidden.Add(outline);
        var saved = LayoutOf(sample).SaveLayout();
        saved.ShouldContain("33.3333");
        saved.ShouldNotContain("33.33333333");
        using var json = JsonDocument.Parse(saved);
        json.RootElement.GetProperty("hidden").GetProperty("top")[0].GetString().ShouldBe("outline");
    }
}
