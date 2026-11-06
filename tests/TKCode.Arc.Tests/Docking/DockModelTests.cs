using Avalonia;
using Avalonia.Headless.XUnit;
using Avalonia.Layout;

namespace TKCode.Arc.Tests.Docking;

/// <summary>The dock model (spec 4.3): structure rules, targets and moves, ids, placement by preference.</summary>
public class DockModelTests
{
    [AvaloniaFact]
    public void Nodes_Know_Their_Parent_And_Workspace_And_Defaults()
    {
        var s = new DockSample();
        s.ToolGroup.Parent.ShouldBe(s.Root);
        s.DocumentGroup.Parent.ShouldBe(s.Documents);
        s.Documents.Workspace.ShouldBe(s.Workspace);
        s.Root.Parent.ShouldBeNull();
        s.Explorer.Group.ShouldBe(s.ToolGroup);
        s.Explorer.State.ShouldBe(DockPaneState.Docked);
        s.Explorer.IsPinned.ShouldBeTrue();
        s.DocumentGroup.IsDocumentGroup.ShouldBeTrue();
        s.ToolGroup.IsDocumentGroup.ShouldBeFalse();
        s.ToolGroup.ActivePane.ShouldBe(s.Explorer);
        new DockGroup().Size.ShouldBe(1);
        new DockGroup().MinSize.ShouldBe(120);
        new DockPane().PreferredSize.ShouldBe(280);
    }

    [AvaloniaFact]
    public void All_Panes_Lists_The_Tree_Then_The_Strips_Then_The_Floats()
    {
        var s = new DockSample();
        var outline = DockSample.Tool("outline", "Outline", DockSide.Right);
        var problems = DockSample.Tool("problems", "Problems", DockSide.Bottom);
        s.Workspace.RightHidden.Add(outline);
        s.Workspace.Floats.Add(new DockFloat(new DockGroup(problems), new Rect(10, 10, 400, 300)));
        s.Workspace.AllPanes().Select(p => p.Id).ShouldBe(["explorer", "search", "doc:readme", "terminal", "outline", "problems"]);
        s.Workspace.FindPane("problems").ShouldBe(problems);
        s.Workspace.FindPane("nope").ShouldBeNull();
        outline.State.ShouldBe(DockPaneState.AutoHidden);
        outline.IsPinned.ShouldBeFalse();
        outline.Group.ShouldBeNull();
        problems.State.ShouldBe(DockPaneState.Floating);
        s.Workspace.AutoHidden(DockSide.Right).ShouldBe(s.Workspace.RightHidden);
    }

    [AvaloniaFact]
    public void Pane_Ids_Must_Be_Unique_And_Present()
    {
        var s = new DockSample();
        Should.Throw<InvalidOperationException>(() => s.ToolGroup.Panes.Add(DockSample.Tool("terminal", "Second terminal", DockSide.Bottom)));
        Should.Throw<InvalidOperationException>(() => s.ToolGroup.Panes.Add(new DockPane()));
        Should.Throw<InvalidOperationException>(() => s.Workspace.LeftHidden.Add(DockSample.Tool("search", "Again", DockSide.Left)));
        // A whole subtree is checked when it joins.
        var duplicate = new DockGroup(DockSample.Document("doc:readme", "Copy"));
        Should.Throw<InvalidOperationException>(() => s.Root.Children.Add(duplicate));
        // A pane is in one place at a time.
        Should.Throw<InvalidOperationException>(() => s.TerminalGroup.Panes.Add(s.Explorer));
        // Ids repeat freely outside a workspace until the tree joins one.
        var loose = new DockSplit(Orientation.Horizontal, new DockGroup(DockSample.Document("a", "A")), new DockGroup(DockSample.Document("a", "A2")));
        Should.Throw<InvalidOperationException>(() => new DockWorkspace { Root = loose });
    }

    [AvaloniaFact]
    public void A_Workspace_Has_At_Most_One_Document_Area()
    {
        var s = new DockSample();
        Should.Throw<InvalidOperationException>(() => s.Root.Children.Add(new DockDocumentArea()));
        Should.Throw<InvalidOperationException>(() => s.Documents.Root = new DockDocumentArea());
    }

    [AvaloniaFact]
    public void An_Emptied_Group_Is_Removed_And_A_Single_Child_Split_Is_Replaced_By_Its_Child()
    {
        var s = new DockSample();
        s.Workspace.Remove(s.Terminal).ShouldBeTrue();
        // The terminal group went, then the vertical split held only the document area, which took its place and share.
        s.Root.Children.Count.ShouldBe(2);
        s.Root.Children[1].ShouldBe(s.Documents);
        s.Documents.Size.ShouldBe(78);
        s.Documents.Parent.ShouldBe(s.Root);
        s.Right.Children.ShouldBeEmpty();
        s.Right.Parent.ShouldBeNull();
        s.TerminalGroup.Workspace.ShouldBeNull();
        s.Terminal.Group.ShouldBeNull();
        s.Terminal.Workspace.ShouldBeNull();
    }

    [AvaloniaFact]
    public void The_Document_Area_Stays_When_Its_Last_Document_Closes()
    {
        var s = new DockSample();
        s.Workspace.Remove(s.Readme);
        s.Documents.Root.ShouldBeNull();
        s.Documents.Workspace.ShouldBe(s.Workspace);
        s.Workspace.DocumentArea.ShouldBe(s.Documents);
        s.Right.Children.ShouldBe([s.Documents, s.TerminalGroup]);
    }

    [AvaloniaFact]
    public void A_Root_Group_That_Empties_Leaves_An_Empty_Workspace()
    {
        var pane = DockSample.Tool("a", "A", DockSide.Left);
        var workspace = new DockWorkspace { Root = new DockGroup(pane) };
        workspace.Remove(pane);
        workspace.Root.ShouldBeNull();
        workspace.AllPanes().ShouldBeEmpty();
    }

    [AvaloniaFact]
    public void Nested_Splits_Of_The_Same_Orientation_Merge_Sharing_Their_Size()
    {
        var a = new DockGroup(DockSample.Tool("a", "A", DockSide.Left)) { Size = 20 };
        var b = new DockGroup(DockSample.Tool("b", "B", DockSide.Left)) { Size = 1 };
        var c = new DockGroup(DockSample.Tool("c", "C", DockSide.Left)) { Size = 3 };
        var d = new DockGroup(DockSample.Tool("d", "D", DockSide.Left)) { Size = 1 };
        var inner = new DockSplit(Orientation.Horizontal, b, c) { Size = 60 };
        var outer = new DockSplit(Orientation.Horizontal, a, inner, d);
        var workspace = new DockWorkspace { Root = outer };
        // Any removal runs the tidy-up over the whole tree.
        var e = DockSample.Tool("e", "E", DockSide.Left);
        d.Panes.Add(e);
        d.Panes.Remove(e);
        outer.Children.ShouldBe([a, b, c, d]);
        b.Size.ShouldBe(15, 1e-9);
        c.Size.ShouldBe(45, 1e-9);
        a.Size.ShouldBe(20);
        workspace.Root.ShouldBe(outer);
    }

    [AvaloniaFact]
    public void Splitting_A_Group_Wraps_It_In_A_Split_Of_Halves()
    {
        var s = new DockSample();
        s.Workspace.Move(s.Search, DockTarget.Split(s.ToolGroup, DockPosition.Bottom)).ShouldBeTrue();
        var column = s.Root.Children[0].ShouldBeOfType<DockSplit>();
        column.Orientation.ShouldBe(Orientation.Vertical);
        column.Size.ShouldBe(22);
        column.Children.Count.ShouldBe(2);
        column.Children[0].ShouldBe(s.ToolGroup);
        var added = column.Children[1].ShouldBeOfType<DockGroup>();
        added.Panes.ShouldBe([s.Search]);
        s.ToolGroup.Panes.ShouldBe([s.Explorer]);
        s.ToolGroup.Size.ShouldBe(50);
        added.Size.ShouldBe(50);
    }

    [AvaloniaFact]
    public void Splitting_Along_The_Parent_Split_Inserts_A_Sibling_Taking_Half()
    {
        var s = new DockSample();
        s.Workspace.Move(s.Search, DockTarget.Split(s.ToolGroup, DockPosition.Right)).ShouldBeTrue();
        s.Root.Children.Count.ShouldBe(3);
        s.Root.Children[0].ShouldBe(s.ToolGroup);
        s.Root.Children[1].ShouldBeOfType<DockGroup>().Panes.ShouldBe([s.Search]);
        s.Root.Children[1].Size.ShouldBe(11);
        s.ToolGroup.Size.ShouldBe(11);
        s.Root.Children[2].ShouldBe(s.Right);
    }

    [AvaloniaFact]
    public void Moving_The_Only_Pane_Next_To_Another_Group_Removes_Its_Old_Group()
    {
        var s = new DockSample();
        s.Workspace.Move(s.Terminal, DockTarget.Split(s.ToolGroup, DockPosition.Bottom)).ShouldBeTrue();
        // The bottom group left the right column, so the document area took the column's place.
        s.Root.Children[1].ShouldBe(s.Documents);
        s.Terminal.Group.ShouldNotBe(s.TerminalGroup);
        s.TerminalGroup.Workspace.ShouldBeNull();
        s.Terminal.Group!.Parent.ShouldBeOfType<DockSplit>().Orientation.ShouldBe(Orientation.Vertical);
    }

    [AvaloniaFact]
    public void Splitting_A_Group_Off_Itself_With_Its_Only_Pane_Is_Refused()
    {
        var s = new DockSample();
        s.Workspace.CanPlace(s.Terminal, DockTarget.Split(s.TerminalGroup, DockPosition.Left)).ShouldBeFalse();
        s.Workspace.Move(s.Terminal, DockTarget.Split(s.TerminalGroup, DockPosition.Left)).ShouldBeFalse();
        s.Terminal.Group.ShouldBe(s.TerminalGroup);
    }

    [AvaloniaFact]
    public void Into_Inserts_At_The_Index_And_Activates_Or_Reorders_In_Place()
    {
        var s = new DockSample();
        s.Workspace.Move(s.Terminal, DockTarget.Into(s.ToolGroup, 1)).ShouldBeTrue();
        s.ToolGroup.Panes.ShouldBe([s.Explorer, s.Terminal, s.Search]);
        s.ToolGroup.ActivePane.ShouldBe(s.Terminal);
        s.Workspace.Move(s.Explorer, DockTarget.Into(s.ToolGroup)).ShouldBeTrue();
        s.ToolGroup.Panes.ShouldBe([s.Terminal, s.Search, s.Explorer]);
        s.ToolGroup.ActivePane.ShouldBe(s.Explorer);
    }

    [AvaloniaFact]
    public void An_Edge_Target_Docks_Along_The_Whole_Layout_Taking_A_Quarter()
    {
        var s = new DockSample();
        // The root is horizontal: a new group joins it on the right with a quarter of the total.
        s.Workspace.Move(s.Search, DockTarget.Edge(DockPosition.Right)).ShouldBeTrue();
        s.Root.Children.Count.ShouldBe(3);
        var right = s.Root.Children[2].ShouldBeOfType<DockGroup>();
        right.Panes.ShouldBe([s.Search]);
        (right.Size / s.Root.Children.Sum(c => c.Size)).ShouldBe(0.25, 1e-9);

        // Docking to the bottom wraps the whole root in a vertical split of 75 / 25.
        s.Workspace.Move(s.Explorer, DockTarget.Edge(DockPosition.Bottom)).ShouldBeTrue();
        var root = s.Workspace.Root.ShouldBeOfType<DockSplit>();
        root.Orientation.ShouldBe(Orientation.Vertical);
        root.Children[0].ShouldBe(s.Root);
        root.Children[0].Size.ShouldBe(75);
        root.Children[1].ShouldBeOfType<DockGroup>().Panes.ShouldBe([s.Explorer]);
        root.Children[1].Size.ShouldBe(25);
        s.ToolGroup.Workspace.ShouldBeNull();
    }

    [AvaloniaFact]
    public void Documents_Stay_In_The_Document_Area_And_Tools_Need_Permission_To_Enter()
    {
        var s = new DockSample();
        s.Workspace.CanPlace(s.Readme, DockTarget.Edge(DockPosition.Left)).ShouldBeFalse();
        s.Workspace.CanPlace(s.Readme, DockTarget.Into(s.ToolGroup)).ShouldBeFalse();
        s.Workspace.CanPlace(s.Readme, DockTarget.Split(s.ToolGroup, DockPosition.Left)).ShouldBeFalse();
        s.Workspace.CanPlace(s.Readme, DockTarget.DocumentArea(DockPosition.Right)).ShouldBeFalse();
        s.Workspace.Move(s.Readme, DockTarget.Edge(DockPosition.Left)).ShouldBeFalse();
        s.Readme.Group.ShouldBe(s.DocumentGroup);

        s.Workspace.CanPlace(s.Terminal, DockTarget.Into(s.DocumentGroup)).ShouldBeFalse();
        s.Workspace.CanPlace(s.Terminal, DockTarget.Split(s.DocumentGroup, DockPosition.Right)).ShouldBeFalse();
        s.Terminal.CanDockAsDocument = true;
        s.Workspace.CanPlace(s.Terminal, DockTarget.Into(s.DocumentGroup)).ShouldBeTrue();

        // Splitting a document group keeps documents inside the area.
        var second = DockSample.Document("doc:two", "Two.md");
        s.DocumentGroup.Panes.Add(second);
        s.Workspace.Move(second, DockTarget.Split(s.DocumentGroup, DockPosition.Right)).ShouldBeTrue();
        var editors = s.Documents.Root.ShouldBeOfType<DockSplit>();
        editors.Children.Count.ShouldBe(2);
        second.Group!.IsDocumentGroup.ShouldBeTrue();
    }

    [AvaloniaFact]
    public void A_Document_Area_Target_Docks_A_Tool_Beside_The_Whole_Area()
    {
        var s = new DockSample();
        s.Workspace.Move(s.Search, DockTarget.DocumentArea(DockPosition.Left)).ShouldBeTrue();
        var row = s.Right.Children[0].ShouldBeOfType<DockSplit>();
        row.Orientation.ShouldBe(Orientation.Horizontal);
        row.Children[0].ShouldBeOfType<DockGroup>().Panes.ShouldBe([s.Search]);
        row.Children[1].ShouldBe(s.Documents);
        row.Size.ShouldBe(70);
        s.Search.Group!.IsDocumentGroup.ShouldBeFalse();
    }

    [AvaloniaFact]
    public void Removing_The_Active_Pane_Selects_Its_Right_Neighbour_Then_Its_Left()
    {
        var a = DockSample.Document("a", "A");
        var b = DockSample.Document("b", "B");
        var c = DockSample.Document("c", "C");
        var group = new DockGroup(a, b, c);
        _ = new DockWorkspace { Root = new DockDocumentArea { Root = group } };
        group.ActivePane = b;
        group.Panes.Remove(b);
        group.ActivePane.ShouldBe(c);
        group.Panes.Remove(c);
        group.ActivePane.ShouldBe(a);
        // The active pane is always one of the panes.
        group.ActivePane = DockSample.Document("x", "X");
        group.ActivePane.ShouldBe(a);
    }

    [AvaloniaFact]
    public void Placing_By_Preference_Joins_Tools_Of_The_Same_Side_Or_Opens_A_Group_On_That_Edge()
    {
        var s = new DockSample();
        var outline = DockSample.Tool("outline", "Outline", DockSide.Left);
        s.Workspace.PlaceByPreference(outline).ShouldBe(s.ToolGroup);
        s.ToolGroup.ActivePane.ShouldBe(outline);

        var properties = DockSample.Tool("properties", "Properties", DockSide.Right);
        var group = s.Workspace.PlaceByPreference(properties, 0.2);
        s.Root.Children[^1].ShouldBe(group);
        (group.Size / s.Root.Children.Sum(c => c.Size)).ShouldBe(0.2, 1e-9);

        var doc = DockSample.Document("doc:new", "New.md");
        s.Workspace.PlaceByPreference(doc).ShouldBe(s.DocumentGroup);
    }

    [AvaloniaFact]
    public void A_Document_Without_A_Document_Area_Creates_One()
    {
        var tool = DockSample.Tool("tool", "Tool", DockSide.Left);
        var workspace = new DockWorkspace { Root = new DockGroup(tool) };
        var doc = DockSample.Document("doc", "Doc");
        var group = workspace.PlaceByPreference(doc);
        workspace.DocumentArea.ShouldNotBeNull();
        group.IsDocumentGroup.ShouldBeTrue();
        var root = workspace.Root.ShouldBeOfType<DockSplit>();
        root.Children[1].ShouldBe(workspace.DocumentArea);

        var empty = new DockWorkspace();
        var first = empty.PlaceByPreference(DockSample.Document("d1", "D1"));
        empty.Root.ShouldBeOfType<DockDocumentArea>().Root.ShouldBe(first);
    }

    [AvaloniaFact]
    public void The_Model_Raises_Changed_Once_Per_Operation_After_Tidying()
    {
        var s = new DockSample();
        var count = 0;
        s.Workspace.Changed += (_, _) =>
        {
            count++;
            // The tree is already tidy when the event arrives.
            s.Workspace.Root.ShouldBe(s.Root);
        };
        s.Workspace.Move(s.Terminal, DockTarget.Into(s.ToolGroup));
        count.ShouldBe(1);
    }

    [AvaloniaFact]
    public void Floats_Are_Tidied_And_Removed_When_Empty()
    {
        var s = new DockSample();
        var problems = DockSample.Tool("problems", "Problems", DockSide.Bottom);
        var dock = new DockFloat(new DockGroup(problems), new Rect(0, 0, 300, 200));
        s.Workspace.Floats.Add(dock);
        dock.Workspace.ShouldBe(s.Workspace);
        s.Workspace.CanPlace(s.Readme, DockTarget.Into(problems.Group!)).ShouldBeTrue();
        s.Workspace.Remove(problems);
        s.Workspace.Floats.ShouldBeEmpty();
        dock.Workspace.ShouldBeNull();
    }
}
