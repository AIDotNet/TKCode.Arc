using System.Text.Json;
using System.Text.Json.Nodes;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia;

namespace TKCode.Arc.Tests.Tables;

public class JsonViewerTests
{
    private const string Sample = """
        {
          "id": "evt_1",
          "count": 3,
          "ok": true,
          "none": null,
          "order-id": "A-1",
          "lines": [ { "amount": 4800 }, { "amount": 1200 } ],
          "customer": { "name": "Ada", "email": "ada@example.com" }
        }
        """;

    private static Window Show(Control control)
    {
        var window = TestHelpers.Show(control);
        window.Width = 700;
        window.Height = 800;
        Dispatcher.UIThread.RunJobs();
        return window;
    }

    private static string[] Ids(JsonViewer viewer) => viewer.VisibleRows.Cast<JsonViewerRowModel>().Select(r => r.Id).ToArray();

    private static JsonViewerRow Row(JsonViewer viewer, string id) => (JsonViewerRow)viewer.RowsPanel!.RowFor(id)!;

    [AvaloniaFact]
    public void Opens_The_Root_And_Builds_Paths_Like_Arc()
    {
        var viewer = new JsonViewer { Json = Sample };
        var window = Show(viewer);
        try
        {
            Ids(viewer).ShouldBe(["root", "root.id", "root.count", "root.ok", "root.none", "root[\"order-id\"]", "root.lines", "root.customer"]);
            viewer.CurrentPath.ShouldBe("root");
            viewer.ExpandedPaths.ShouldBe(["root"]);
            var rows = viewer.VisibleRows.Cast<JsonViewerRowModel>().ToArray();
            rows[1].Node!.Type.ShouldBe(JsonViewerValueType.String);
            rows[2].Node!.Type.ShouldBe(JsonViewerValueType.Number);
            rows[3].Node!.Type.ShouldBe(JsonViewerValueType.Boolean);
            rows[4].Node!.Type.ShouldBe(JsonViewerValueType.Null);
            rows[6].Node!.Count.ShouldBe(2);
            JsonViewerPaths.CountLabel(JsonViewerValueType.Array, 2).ShouldBe("2 items");
            JsonViewerPaths.CountLabel(JsonViewerValueType.Object, 1).ShouldBe("1 key");
            JsonViewerPaths.Preview(rows[7].Node!).ShouldBe("{ name, email }");
            Row(viewer, "root.lines").Bounds.Height.ShouldBe(30);
            Avalonia.Automation.AutomationProperties.GetName(Row(viewer, "root.lines")).ShouldBe("lines, array, 2 items");
            Avalonia.Automation.AutomationProperties.GetName(Row(viewer, "root.id")).ShouldBe("id: evt_1");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Keyboard_Moves_Opens_And_Closes()
    {
        var viewer = new JsonViewer { Json = Sample };
        var current = new List<string>();
        viewer.CurrentRowChanged += (_, e) => current.Add(e.Path);
        var window = Show(viewer);
        try
        {
            var root = Row(viewer, "root");
            root.IsTabStop.ShouldBeTrue();
            root.Focus(NavigationMethod.Tab);
            window.KeyPressQwerty(PhysicalKey.End, RawInputModifiers.None);
            Dispatcher.UIThread.RunJobs();
            viewer.CurrentPath.ShouldBe("root.customer");
            window.KeyPressQwerty(PhysicalKey.ArrowRight, RawInputModifiers.None);
            Dispatcher.UIThread.RunJobs();
            Ids(viewer).ShouldContain("root.customer.name");
            window.KeyPressQwerty(PhysicalKey.ArrowRight, RawInputModifiers.None);
            Dispatcher.UIThread.RunJobs();
            viewer.CurrentPath.ShouldBe("root.customer.name");
            window.KeyPressQwerty(PhysicalKey.ArrowLeft, RawInputModifiers.None);
            Dispatcher.UIThread.RunJobs();
            viewer.CurrentPath.ShouldBe("root.customer");
            window.KeyPressQwerty(PhysicalKey.Enter, RawInputModifiers.None);
            Dispatcher.UIThread.RunJobs();
            Ids(viewer).ShouldNotContain("root.customer.name");
            window.KeyPressQwerty(PhysicalKey.ArrowUp, RawInputModifiers.None);
            Dispatcher.UIThread.RunJobs();
            viewer.CurrentPath.ShouldBe("root.lines");
            window.KeyPressQwerty(PhysicalKey.Home, RawInputModifiers.None);
            Dispatcher.UIThread.RunJobs();
            viewer.CurrentPath.ShouldBe("root");
            Row(viewer, "root").IsFocused.ShouldBeTrue();
            current.ShouldBe(["root.customer", "root.customer.name", "root.customer", "root.lines", "root"]);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Search_Opens_Matching_Branches_And_Steps_Through_Matches()
    {
        var viewer = new JsonViewer { Json = Sample };
        var window = Show(viewer);
        try
        {
            viewer.Query = "AMOUNT";
            Dispatcher.UIThread.RunJobs();
            viewer.MatchCount.ShouldBe(2);
            viewer.CurrentMatch.ShouldBe("root.lines[0].amount");
            viewer.CurrentPath.ShouldBe("root.lines[0].amount");
            Ids(viewer).ShouldContain("root.lines[1].amount");
            viewer.NextMatch();
            viewer.CurrentPath.ShouldBe("root.lines[1].amount");
            viewer.NextMatch();
            viewer.CurrentPath.ShouldBe("root.lines[0].amount");

            // Closing a branch while searching is remembered until the query changes.
            viewer.Collapse("root.lines");
            Ids(viewer).ShouldNotContain("root.lines[0]");
            viewer.CurrentPath.ShouldBe("root.lines");

            // Values match too; Escape in the search box clears.
            var box = TestHelpers.Descendant<TextBox>(viewer);
            viewer.Query = "ada@";
            Dispatcher.UIThread.RunJobs();
            viewer.CurrentPath.ShouldBe("root.customer.email");
            box.Focus();
            window.KeyPressQwerty(PhysicalKey.Escape, RawInputModifiers.None);
            Dispatcher.UIThread.RunJobs();
            viewer.Query.ShouldBe(string.Empty);
            viewer.MatchCount.ShouldBe(0);
            Ids(viewer).ShouldNotContain("root.customer.email");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Long_Branches_Load_In_Pages()
    {
        var items = new JsonArray();
        for (var i = 0; i < 120; i++)
        {
            items.Add(i);
        }
        var viewer = new JsonViewer { Value = items };
        var window = Show(viewer);
        try
        {
            var ids = Ids(viewer);
            ids.Length.ShouldBe(1 + 50 + 1);
            ids[^1].ShouldBe("root::more");
            var more = viewer.VisibleRows.Cast<JsonViewerRowModel>().Last();
            more.Hidden.ShouldBe(70);
            viewer.FocusPath("root::more");
            Dispatcher.UIThread.RunJobs();
            Avalonia.Automation.AutomationProperties.GetName(Row(viewer, "root::more")).ShouldBe("Show 50 more, 70 hidden");
            viewer.CurrentPath.ShouldBe("root");
            window.KeyPressQwerty(PhysicalKey.Enter, RawInputModifiers.None);
            Dispatcher.UIThread.RunJobs();
            Ids(viewer).Length.ShouldBe(1 + 100 + 1);
            viewer.FocusPath("root::more");
            window.KeyPressQwerty(PhysicalKey.Space, RawInputModifiers.None);
            Dispatcher.UIThread.RunJobs();
            Ids(viewer).Length.ShouldBe(1 + 120);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Expand_All_Collapse_All_And_Two_Way_Paths()
    {
        var viewer = new JsonViewer { Json = Sample };
        var changes = 0;
        viewer.ExpandedPathsChanged += (_, _) => changes++;
        var window = Show(viewer);
        try
        {
            viewer.ExpandAll();
            viewer.ExpandedPaths.ShouldBe(["root", "root.lines", "root.lines[0]", "root.lines[1]", "root.customer"], ignoreOrder: true);
            Ids(viewer).ShouldContain("root.lines[1].amount");
            viewer.CollapseAll();
            viewer.ExpandedPaths.ShouldBe(["root"]);
            changes.ShouldBe(2);
            viewer.ExpandedPaths = ["root", "root.customer"];
            Ids(viewer).ShouldContain("root.customer.name");
            Ids(viewer).ShouldNotContain("root.lines[0]");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Copy_Text_And_Invalid_Json()
    {
        using var document = JsonDocument.Parse("""{"a":[1,"x"],"s":"plain"}""");
        JsonViewerPaths.CopyText(document.RootElement.GetProperty("s")).ShouldBe("plain");
        JsonViewerPaths.CopyText(document.RootElement.GetProperty("a")).ShouldBe("[\n  1,\n  \"x\"\n]");
        JsonViewerPaths.Child("root", "order-id").ShouldBe("root[\"order-id\"]");
        JsonViewerPaths.Child("root", "$ok_1").ShouldBe("root.$ok_1");
        JsonViewerPaths.Within("root.a", "root.a[0]").ShouldBeTrue();
        JsonViewerPaths.Within("root.a", "root.ab").ShouldBeFalse();

        var viewer = new JsonViewer { Json = "{ broken" };
        var window = Show(viewer);
        try
        {
            viewer.ParseError.ShouldNotBeNull();
            viewer.VisibleRows.Count.ShouldBe(0);
            viewer.Json = "[1, 2]";
            viewer.ParseError.ShouldBeNull();
            Ids(viewer).ShouldBe(["root", "root[0]", "root[1]"]);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Ctrl_C_Copies_The_Current_Value()
    {
        var viewer = new JsonViewer { Json = Sample };
        JsonViewerCopiedEventArgs? copied = null;
        viewer.Copied += (_, e) => copied = e;
        var window = Show(viewer);
        try
        {
            viewer.FocusPath("root.customer");
            window.KeyPressQwerty(PhysicalKey.C, RawInputModifiers.Control);
            Dispatcher.UIThread.RunJobs();
            copied.ShouldNotBeNull();
            copied.Kind.ShouldBe(JsonViewerCopyKind.Value);
            copied.Text.ShouldContain("\"name\": \"Ada\"");
            window.KeyPressQwerty(PhysicalKey.C, RawInputModifiers.Control | RawInputModifiers.Shift);
            Dispatcher.UIThread.RunJobs();
            copied.Kind.ShouldBe(JsonViewerCopyKind.Path);
            copied.Text.ShouldBe("root.customer");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Large_Documents_Realize_Only_What_Is_Visible()
    {
        var items = new JsonArray();
        for (var i = 0; i < 3000; i++)
        {
            items.Add(new JsonObject { ["id"] = i, ["name"] = $"item {i}" });
        }
        var viewer = new JsonViewer { Value = new JsonObject { ["items"] = items }, PageSize = 5000 };
        var window = Show(viewer);
        try
        {
            viewer.ExpandAll();
            Dispatcher.UIThread.RunJobs();
            viewer.VisibleRows.Count.ShouldBe(2 + 3000 * 3);
            viewer.RowsPanel!.RealizedRows.Count().ShouldBeLessThan(40);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Row_Actions_Copy_On_Hover_And_Rows_Toggle_On_Click()
    {
        var viewer = new JsonViewer { Json = Sample };
        JsonViewerCopiedEventArgs? copied = null;
        viewer.Copied += (_, e) => copied = e;
        var window = Show(viewer);
        try
        {
            var row = Row(viewer, "root.customer");
            var width = row.Bounds.Width;
            // Hover shows the actions; the second one copies the path.
            var pathAction = row.TranslatePoint(new Avalonia.Point(width - 1 - 13, 15), window)!.Value;
            window.MouseMove(pathAction);
            Dispatcher.UIThread.RunJobs();
            window.MouseDown(pathAction, MouseButton.Left);
            window.MouseUp(pathAction, MouseButton.Left);
            Dispatcher.UIThread.RunJobs();
            copied.ShouldNotBeNull();
            copied.Kind.ShouldBe(JsonViewerCopyKind.Path);
            copied.Path.ShouldBe("root.customer");
            row.ActionState(JsonViewerCopyKind.Path).ShouldBe(CopyState.Copied);
            Ids(viewer).ShouldNotContain("root.customer.name");

            // A click elsewhere on the row opens the branch and makes it current.
            var label = row.TranslatePoint(new Avalonia.Point(60, 15), window)!.Value;
            window.MouseMove(label);
            window.MouseDown(label, MouseButton.Left);
            window.MouseUp(label, MouseButton.Left);
            Dispatcher.UIThread.RunJobs();
            Ids(viewer).ShouldContain("root.customer.name");
            viewer.CurrentPath.ShouldBe("root.customer");
        }
        finally
        {
            window.Close();
        }
    }
}
