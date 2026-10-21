using TKCode.Arc.Motion;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia;

namespace TKCode.Arc.Tests.Tables;

public class TreeViewTests
{
    private sealed record Node(string Name, List<Node>? Children = null)
    {
        public override string ToString() => Name;
    }

    private static TreeViewItem Folder(string name, bool open, params TreeViewItem[] children)
    {
        var folder = new TreeViewItem { Header = name, IsExpanded = open };
        folder.Items.AddRange(children);
        return folder;
    }

    private static TreeView Sample(out TreeViewItem src, out TreeViewItem components, out TreeViewItem readme)
    {
        var tree = new TreeView();
        components = Folder("components", false, new TreeViewItem { Header = "button.tsx" }, new TreeViewItem { Header = "card.tsx" });
        src = Folder("src", true, components, new TreeViewItem { Header = "index.ts" });
        readme = new TreeViewItem { Header = "README.md" };
        tree.Items.Add(src);
        tree.Items.Add(readme);
        return tree;
    }

    private static Window Show(Control control, ArcMotionLevel level = ArcMotionLevel.Off)
    {
        var window = TestHelpers.Show(control, level);
        window.Width = 600;
        window.Height = 700;
        Dispatcher.UIThread.RunJobs();
        return window;
    }

    private static string[] Labels(TreeView tree) => tree.VisibleRows.Cast<TreeViewNode>().Select(n => n.Label).ToArray();

    private static TreeViewRow Row(TreeView tree, object item) => (TreeViewRow)tree.RowsPanel!.RowFor(item)!;

    [AvaloniaFact]
    public void Inline_Items_Flatten_To_Visible_Rows()
    {
        var tree = Sample(out _, out var components, out _);
        var window = Show(tree);
        try
        {
            Labels(tree).ShouldBe(["src", "components", "index.ts", "README.md"]);
            var node = (TreeViewNode)tree.VisibleRows[1];
            node.Depth.ShouldBe(2);
            node.HasChildren.ShouldBeTrue();
            node.Position.ShouldBe(1);
            node.SetSize.ShouldBe(2);
            var row = Row(tree, components);
            row.Bounds.Height.ShouldBe(38);
            row.Bounds.Top.ShouldBe(38);
            Avalonia.Automation.AutomationProperties.GetName(tree).ShouldBe("File tree");
            tree.CornerRadius.TopLeft.ShouldBe(16);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Keyboard_Follows_The_Tree_Pattern()
    {
        var tree = Sample(out var src, out var components, out var readme);
        var expanded = new List<object>();
        var collapsed = new List<object>();
        tree.ItemExpanded += (_, e) => expanded.Add(e.Item);
        tree.ItemCollapsed += (_, e) => collapsed.Add(e.Item);
        var window = Show(tree);
        try
        {
            Row(tree, src).IsTabStop.ShouldBeTrue();
            Row(tree, components).IsTabStop.ShouldBeFalse();
            Row(tree, src).Focus(NavigationMethod.Tab);
            window.KeyPressQwerty(PhysicalKey.ArrowDown, RawInputModifiers.None);
            Dispatcher.UIThread.RunJobs();
            tree.FocusKey.ShouldBe(components);
            Row(tree, components).IsFocused.ShouldBeTrue();

            // → opens a closed folder, then enters it.
            window.KeyPressQwerty(PhysicalKey.ArrowRight, RawInputModifiers.None);
            Dispatcher.UIThread.RunJobs();
            components.IsExpanded.ShouldBeTrue();
            expanded.ShouldBe([components]);
            Labels(tree).ShouldBe(["src", "components", "button.tsx", "card.tsx", "index.ts", "README.md"]);
            window.KeyPressQwerty(PhysicalKey.ArrowRight, RawInputModifiers.None);
            Dispatcher.UIThread.RunJobs();
            ((TreeViewNode)tree.VisibleRows[2]).Label.ShouldBe("button.tsx");
            tree.FocusKey.ShouldBe(((TreeViewNode)tree.VisibleRows[2]).Item);

            // ← goes to the parent, then closes it.
            window.KeyPressQwerty(PhysicalKey.ArrowLeft, RawInputModifiers.None);
            Dispatcher.UIThread.RunJobs();
            tree.FocusKey.ShouldBe(components);
            window.KeyPressQwerty(PhysicalKey.ArrowLeft, RawInputModifiers.None);
            Dispatcher.UIThread.RunJobs();
            components.IsExpanded.ShouldBeFalse();
            collapsed.ShouldBe([components]);

            window.KeyPressQwerty(PhysicalKey.End, RawInputModifiers.None);
            Dispatcher.UIThread.RunJobs();
            tree.FocusKey.ShouldBe(readme);
            window.KeyPressQwerty(PhysicalKey.Home, RawInputModifiers.None);
            Dispatcher.UIThread.RunJobs();
            tree.FocusKey.ShouldBe(src);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Enter_Selects_And_Toggles_And_Typeahead_Jumps()
    {
        var tree = Sample(out var src, out var components, out var readme);
        var invoked = new List<object>();
        TreeViewSelectionChangedEventArgs? change = null;
        tree.ItemInvoked += (_, e) => invoked.Add(e.Item);
        tree.SelectionChanged += (_, e) => change = e;
        var window = Show(tree);
        try
        {
            Row(tree, src).Focus(NavigationMethod.Tab);
            window.KeyPressQwerty(PhysicalKey.ArrowDown, RawInputModifiers.None);
            window.KeyPressQwerty(PhysicalKey.Enter, RawInputModifiers.None);
            Dispatcher.UIThread.RunJobs();
            tree.SelectedItem.ShouldBe(components);
            components.IsExpanded.ShouldBeTrue();
            invoked.ShouldBe([components]);
            change!.NewItem.ShouldBe(components);
            tree.RowsPanel!.HighlightKey.ShouldBe(components);

            // Space on the same row: invoked again, folder closes.
            window.KeyPressQwerty(PhysicalKey.Space, RawInputModifiers.None);
            Dispatcher.UIThread.RunJobs();
            components.IsExpanded.ShouldBeFalse();
            invoked.Count.ShouldBe(2);

            window.KeyTextInput("r");
            Dispatcher.UIThread.RunJobs();
            tree.FocusKey.ShouldBe(readme);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Data_Source_With_Children_Selector()
    {
        var page = new Node("page.tsx");
        var app = new Node("app", [new Node("layout.tsx"), page]);
        var data = new List<Node> { app, new("package.json") };
        var tree = new TreeView { ItemsSource = data, ChildrenSelector = n => ((Node)n).Children };
        var window = Show(tree);
        try
        {
            Labels(tree).ShouldBe(["app", "package.json"]);
            tree.Expand(app);
            Labels(tree).ShouldBe(["app", "layout.tsx", "page.tsx", "package.json"]);
            tree.IsItemExpanded(app).ShouldBeTrue();
            tree.CollapseAll();
            Labels(tree).ShouldBe(["app", "package.json"]);

            // Selecting a hidden node opens its ancestors and highlights it.
            tree.SelectedItem = page;
            Dispatcher.UIThread.RunJobs();
            tree.IsItemExpanded(app).ShouldBeTrue();
            tree.RowsPanel!.HighlightKey.ShouldBe(page);
            tree.ExpandAll();
            Labels(tree).Length.ShouldBe(4);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Thousands_Of_Nodes_Realize_Only_A_Screenful()
    {
        var data = Enumerable.Range(1, 20).Select(f => new Node($"folder-{f}",
            Enumerable.Range(1, 500).Select(i => new Node($"file-{f}-{i}.ts")).ToList())).ToList();
        var tree = new TreeView { ItemsSource = data, ChildrenSelector = n => ((Node)n).Children, MaxHeight = 400 };
        var window = Show(tree);
        try
        {
            tree.ExpandAll();
            Dispatcher.UIThread.RunJobs();
            tree.VisibleRows.Count.ShouldBe(20 + 20 * 500);
            tree.RowsPanel!.RealizedRows.Count().ShouldBeLessThan(40);
            tree.RowsPanel.Bounds.Height.ShouldBe(10020 * 38, 1);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Opening_A_Folder_Grows_Its_Space_On_A_Spring()
    {
        var frames = new ManualFrameClock();
        using var install = frames.Install();
        var tree = Sample(out _, out var components, out var readme);
        var window = Show(tree, ArcMotionLevel.Full);
        try
        {
            var panel = tree.RowsPanel!;
            var before = Row(tree, readme).Bounds.Top;
            tree.Expand(components);
            Dispatcher.UIThread.RunJobs();
            panel.HasRowMotion.ShouldBeTrue();
            frames.Advance(TimeSpan.FromMilliseconds(80));
            Dispatcher.UIThread.RunJobs();
            var moving = Row(tree, readme).Bounds.Top;
            moving.ShouldBeGreaterThan(before);
            moving.ShouldBeLessThan(before + 2 * 38);
            frames.Advance(TimeSpan.FromSeconds(1.5));
            Dispatcher.UIThread.RunJobs();
            Row(tree, readme).Bounds.Top.ShouldBe(before + 2 * 38, 0.5);
            panel.HasRowMotion.ShouldBeFalse();

            // Closing shrinks the space back while the rows fade.
            tree.Collapse(components);
            Dispatcher.UIThread.RunJobs();
            frames.Advance(TimeSpan.FromMilliseconds(80));
            Dispatcher.UIThread.RunJobs();
            Row(tree, readme).Bounds.Top.ShouldBeGreaterThan(before);
            frames.Advance(TimeSpan.FromSeconds(1.5));
            Dispatcher.UIThread.RunJobs();
            Row(tree, readme).Bounds.Top.ShouldBe(before, 0.5);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Changes_Before_The_Next_Layout_Pass_Diff_Against_Rows_Still_Closing()
    {
        var frames = new ManualFrameClock();
        using var install = frames.Install();
        var tree = Sample(out var src, out var components, out var readme);
        var window = Show(tree, ArcMotionLevel.Full);
        try
        {
            tree.Expand(components);
            frames.Advance(TimeSpan.FromSeconds(1.5));
            Dispatcher.UIThread.RunJobs();

            // Both closes land in one dispatcher job: the second diffs while the first one's space is still open.
            tree.Collapse(components);
            tree.Collapse(src);
            Labels(tree).ShouldBe(["src", "README.md"]);
            // components and index.ts still hold their space (button.tsx and card.tsx left with index.ts, their anchor).
            tree.RowsPanel!.RowTop(1).ShouldBe(38 + 2 * 38, 0.5);
            frames.Advance(TimeSpan.FromSeconds(1.5));
            Dispatcher.UIThread.RunJobs();
            Row(tree, readme).Bounds.Top.ShouldBe(38, 0.5);
            tree.RowsPanel.HasRowMotion.ShouldBeFalse();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Clicking_A_Folder_Selects_And_Toggles_It()
    {
        var tree = Sample(out _, out var components, out _);
        var invoked = new List<object>();
        tree.ItemInvoked += (_, e) => invoked.Add(e.Item);
        var window = Show(tree);
        try
        {
            var row = Row(tree, components);
            var point = row.TranslatePoint(new Avalonia.Point(60, 20), window)!.Value;
            window.MouseMove(point);
            window.MouseDown(point, MouseButton.Left);
            window.MouseUp(point, MouseButton.Left);
            Dispatcher.UIThread.RunJobs();
            tree.SelectedItem.ShouldBe(components);
            components.IsExpanded.ShouldBeTrue();
            invoked.ShouldBe([components]);
            tree.FocusKey.ShouldBe(components);
            row.IsFocused.ShouldBeTrue();
            row.Classes.ShouldNotContain(":focus-visible");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Reduced_Motion_Opens_Folders_At_Once()
    {
        var frames = new ManualFrameClock();
        using var install = frames.Install();
        var tree = Sample(out _, out var components, out var readme);
        var window = Show(tree, ArcMotionLevel.Reduced);
        try
        {
            var before = Row(tree, readme).Bounds.Top;
            tree.Expand(components);
            Dispatcher.UIThread.RunJobs();
            tree.RowsPanel!.HasRowMotion.ShouldBeFalse();
            Row(tree, readme).Bounds.Top.ShouldBe(before + 2 * 38, 0.5);
            Row(tree, components).ChevronAngle.ShouldBe(90);
        }
        finally
        {
            window.Close();
        }
    }
}
