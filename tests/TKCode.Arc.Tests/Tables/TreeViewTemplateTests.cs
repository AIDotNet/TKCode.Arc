using TKCode.Arc.Motion;
using Avalonia;
using Avalonia.Automation.Peers;
using Avalonia.Automation.Provider;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Controls.Templates;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;

namespace TKCode.Arc.Tests.Tables;

/// <summary>Row content templates, disabled nodes and the row height of <see cref="TreeView"/> (issue #28).</summary>
public class TreeViewTemplateTests
{
    private sealed class Node(string name, string type, List<Node>? children = null)
    {
        public string Name { get; } = name;
        public string Type { get; } = type;
        public List<Node>? Children { get; } = children;
        public bool ReadOnly { get; init; }

        public override string ToString() => $"{Name} : {Type}";
    }

    private sealed class FileNode(string name)
    {
        public string Name { get; } = name;

        public override string ToString() => Name;
    }

    private sealed class FolderNode(string name, params object[] children)
    {
        public string Name { get; } = name;
        public object[] Children { get; } = children;

        public override string ToString() => Name;
    }

    private sealed class Marker : Border
    {
        public object? Data { get; init; }
    }

    /// <summary>Shows its data like a bound TextBlock would: a recycled control follows the new node.</summary>
    private sealed class DataText : TextBlock
    {
        protected override void OnDataContextChanged(EventArgs e)
        {
            base.OnDataContextChanged(e);
            Text = DataContext?.ToString();
        }
    }

    private static Window Show(Control control, ArcMotionLevel level = ArcMotionLevel.Off)
    {
        var window = TestHelpers.Show(control, level);
        window.Width = 600;
        window.Height = 700;
        Dispatcher.UIThread.RunJobs();
        return window;
    }

    private static TreeViewRow Row(TreeView tree, object item) => (TreeViewRow)tree.RowsPanel!.RowFor(item)!;

    private static IEnumerable<TreeViewRow> Rows(TreeView tree) => tree.RowsPanel!.RealizedRows.Cast<TreeViewRow>();

    /// <summary>A template whose built controls count themselves and remember the node they were built for.</summary>
    private static IDataTemplate CountingTemplate(Func<int> next, bool recycle = true) =>
        new FuncDataTemplate<object>((item, _) =>
        {
            next();
            return new DataText();
        }, recycle);

    private static List<Node> Schema() =>
    [
        new("orders", "table",
        [
            new("id", "uuid") { ReadOnly = true },
            new("customer", "text"),
            new("total", "numeric")
        ]),
        new("customers", "table",
        [
            new("id", "uuid") { ReadOnly = true },
            new("email", "text")
        ]),
        new("audit_log", "view") { ReadOnly = true }
    ];

    private static TreeView SchemaTree(List<Node> data, IDataTemplate? template = null) => new()
    {
        ItemsSource = data,
        ChildrenSelector = n => ((Node)n).Children,
        HeaderSelector = n => ((Node)n).Name,
        DisabledSelector = n => ((Node)n).ReadOnly,
        ItemTemplate = template
    };

    [AvaloniaFact]
    public void Item_Template_Builds_One_Control_Per_Pooled_Row_And_Rebinds_On_Scroll()
    {
        var builds = 0;
        var data = Enumerable.Range(1, 10).Select(f => new Node($"folder-{f}", "dir",
            Enumerable.Range(1, 100).Select(i => new Node($"file-{f}-{i}", "ts")).ToList())).ToList();
        var tree = new TreeView
        {
            ItemsSource = data,
            ChildrenSelector = n => ((Node)n).Children,
            ItemTemplate = CountingTemplate(() => ++builds),
            MaxHeight = 400
        };
        var window = Show(tree);
        try
        {
            tree.ExpandAll();
            Dispatcher.UIThread.RunJobs();
            tree.VisibleRows.Count.ShouldBe(10 + 10 * 100);
            var realized = Rows(tree).Count();
            realized.ShouldBeLessThan(40);
            builds.ShouldBe(realized);

            var scroll = TestHelpers.Descendant<ScrollViewer>(tree);
            for (var step = 1; step <= 20; step++)
            {
                scroll.Offset = new Vector(0, step * 1500);
                Dispatcher.UIThread.RunJobs();
            }
            // Scrolling only swaps the data of pooled rows: one build per row the pool ever created.
            var pooled = tree.RowsPanel!.GetVisualChildren().OfType<TreeViewRow>().Count();
            pooled.ShouldBeLessThan(40);
            builds.ShouldBe(pooled);
            foreach (var row in Rows(tree))
            {
                var presenter = row.ContentPresenter.ShouldNotBeNull();
                presenter.Content.ShouldBe(row.Node!.Item);
                presenter.Child.ShouldBeAssignableTo<TextBlock>()!.Text.ShouldBe(row.Node.Item.ToString());
                presenter.Child!.DataContext.ShouldBe(row.Node.Item);
            }
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Content_Sits_In_The_Label_Area_And_Replaces_The_Drawn_Label()
    {
        var data = Schema();
        var tree = SchemaTree(data, CountingTemplate(() => 0));
        var window = Show(tree);
        try
        {
            var row = Row(tree, data[0]);
            var presenter = row.ContentPresenter.ShouldNotBeNull();
            presenter.Bounds.Left.ShouldBe(row.LabelLeft, 0.5);
            presenter.Bounds.Top.ShouldBe(2, 0.5);
            presenter.Bounds.Height.ShouldBe(36, 0.5);
            presenter.Bounds.Right.ShouldBe(row.Bounds.Width - 10, 0.5);
            row.DrawsLabel.ShouldBeFalse();

            tree.ItemTemplate = null;
            Dispatcher.UIThread.RunJobs();
            row = Row(tree, data[0]);
            row.ContentPresenter.ShouldBeNull();
            row.DrawsLabel.ShouldBeTrue();
            row.GetVisualChildren().ShouldBeEmpty();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Content_Keeps_The_Full_Row_Height_While_A_Folder_Opens()
    {
        var frames = new ManualFrameClock();
        using var install = frames.Install();
        var data = Schema();
        var tree = SchemaTree(data, CountingTemplate(() => 0));
        var window = Show(tree, ArcMotionLevel.Full);
        try
        {
            tree.Expand(data[0]);
            Dispatcher.UIThread.RunJobs();
            frames.Advance(TimeSpan.FromMilliseconds(60));
            Dispatcher.UIThread.RunJobs();
            var row = Row(tree, data[0].Children![1]);
            row.Bounds.Height.ShouldBeLessThan(37);
            // The row is clipped while it grows; its content is laid out at the full slot, not squashed.
            row.ContentPresenter!.Bounds.Height.ShouldBe(36, 0.5);
            row.ContentPresenter.Child!.Bounds.Height.ShouldBeGreaterThan(10);
            frames.Advance(TimeSpan.FromSeconds(1.5));
            Dispatcher.UIThread.RunJobs();
            Row(tree, data[0].Children![1]).Bounds.Height.ShouldBe(38, 0.5);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Changing_The_Template_Rebuilds_The_Rows()
    {
        var data = Schema();
        var tree = SchemaTree(data, CountingTemplate(() => 0));
        var window = Show(tree);
        try
        {
            tree.ExpandAll();
            Dispatcher.UIThread.RunJobs();
            Rows(tree).ShouldAllBe(r => r.ContentPresenter!.Child is TextBlock);
            tree.ItemTemplate = new FuncDataTemplate<object>((item, _) => new Marker { Data = item }, true);
            Dispatcher.UIThread.RunJobs();
            foreach (var row in Rows(tree))
            {
                row.ContentPresenter!.Child.ShouldBeOfType<Marker>().Data.ShouldBe(row.Node!.Item);
            }
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void A_Type_Specific_Template_Leaves_Other_Nodes_To_The_Trees_Data_Templates()
    {
        var file = new FileNode("README.md");
        var folder = new FolderNode("docs", new FileNode("intro.md"));
        var tree = new TreeView
        {
            ItemsSource = new object[] { folder, file },
            ChildrenSelector = n => (n as FolderNode)?.Children,
            ItemTemplate = new FuncDataTemplate<FolderNode>((f, _) => new Marker { Data = f }, true)
        };
        tree.DataTemplates.Add(new FuncDataTemplate<FileNode>((f, _) => new TextBlock { Text = "file " + f.Name }, true));
        var window = Show(tree);
        try
        {
            Row(tree, folder).ContentPresenter!.Child.ShouldBeOfType<Marker>();
            Row(tree, file).ContentPresenter!.Child.ShouldBeOfType<TextBlock>().Text.ShouldBe("file README.md");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Type_Ahead_And_The_Accessible_Name_Still_Use_The_Header()
    {
        var data = Schema();
        var tree = SchemaTree(data, CountingTemplate(() => 0));
        var window = Show(tree);
        try
        {
            var row = Row(tree, data[0]);
            Avalonia.Automation.AutomationProperties.GetName(row).ShouldBe("orders");
            ControlAutomationPeer.CreatePeerForElement(row).GetName().ShouldBe("orders");
            row.Focus(NavigationMethod.Tab);
            window.KeyTextInput("c");
            Dispatcher.UIThread.RunJobs();
            tree.FocusKey.ShouldBe(data[1]);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Template_Text_Follows_The_Row_Colors()
    {
        var data = Schema();
        var tree = SchemaTree(data, CountingTemplate(() => 0));
        var window = Show(tree);
        try
        {
            tree.Expand(data[0]);
            Dispatcher.UIThread.RunJobs();
            var rest = Row(tree, data[1]).ContentPresenter!.Child.ShouldBeAssignableTo<TextBlock>()!;
            TestHelpers.ColorOf(rest.Foreground).ShouldBe(TestHelpers.ColorOf(tree.RowForeground));
            tree.SelectedItem = data[1];
            Dispatcher.UIThread.RunJobs();
            TestHelpers.ColorOf(rest.Foreground).ShouldBe(TestHelpers.ColorOf(tree.RowActiveForeground));
            var disabled = Row(tree, data[0].Children![0]).ContentPresenter!.Child.ShouldBeAssignableTo<TextBlock>()!;
            TestHelpers.ColorOf(disabled.Foreground).ShouldBe(TestHelpers.ColorOf(tree.MutedForeground));
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Disabled_Nodes_Cannot_Be_Selected_By_Click_Key_Code_Or_Automation()
    {
        var data = Schema();
        var tree = SchemaTree(data);
        var invoked = new List<object>();
        var changes = 0;
        tree.ItemInvoked += (_, e) => invoked.Add(e.Item);
        tree.SelectionChanged += (_, _) => changes++;
        var window = Show(tree);
        try
        {
            tree.Expand(data[0]);
            Dispatcher.UIThread.RunJobs();
            var readOnly = data[0].Children![0];
            var row = Row(tree, readOnly);
            row.Node!.IsDisabled.ShouldBeTrue();

            FocusRingInput.Click(window, row);
            Dispatcher.UIThread.RunJobs();
            tree.SelectedItem.ShouldBeNull();
            invoked.ShouldBeEmpty();

            row.Focus(NavigationMethod.Tab);
            window.KeyPressQwerty(PhysicalKey.Enter, RawInputModifiers.None);
            window.KeyPressQwerty(PhysicalKey.Space, RawInputModifiers.None);
            Dispatcher.UIThread.RunJobs();
            tree.SelectedItem.ShouldBeNull();
            invoked.ShouldBeEmpty();

            tree.SelectedItem = data[1];
            Dispatcher.UIThread.RunJobs();
            changes.ShouldBe(1);
            tree.SelectedItem = readOnly;
            Dispatcher.UIThread.RunJobs();
            tree.SelectedItem.ShouldBe(data[1]);
            changes.ShouldBe(1);

            var peer = ControlAutomationPeer.CreatePeerForElement(row);
            peer.IsEnabled().ShouldBeFalse();
            ((ISelectionItemProvider)peer).Select();
            tree.SelectedItem.ShouldBe(data[1]);

            // An enabled leaf next to it still selects.
            FocusRingInput.Click(window, Row(tree, data[0].Children![1]));
            Dispatcher.UIThread.RunJobs();
            tree.SelectedItem.ShouldBe(data[0].Children![1]);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Focus_Reaches_A_Disabled_Folder_Which_Still_Opens()
    {
        var locked = new TreeViewItem { Header = "locked", IsDisabled = true };
        locked.Items.Add(new TreeViewItem { Header = "inside.txt" });
        var first = new TreeViewItem { Header = "first.txt" };
        var tree = new TreeView();
        tree.Items.Add(first);
        tree.Items.Add(locked);
        var invoked = new List<object>();
        tree.ItemInvoked += (_, e) => invoked.Add(e.Item);
        var window = Show(tree);
        try
        {
            Row(tree, first).Focus(NavigationMethod.Tab);
            window.KeyPressQwerty(PhysicalKey.ArrowDown, RawInputModifiers.None);
            Dispatcher.UIThread.RunJobs();
            tree.FocusKey.ShouldBe(locked);
            Row(tree, locked).IsFocused.ShouldBeTrue();

            window.KeyPressQwerty(PhysicalKey.ArrowRight, RawInputModifiers.None);
            Dispatcher.UIThread.RunJobs();
            locked.IsExpanded.ShouldBeTrue();
            window.KeyPressQwerty(PhysicalKey.ArrowRight, RawInputModifiers.None);
            Dispatcher.UIThread.RunJobs();
            tree.FocusKey.ShouldBe(locked.Items[0]);

            // Enter on the disabled folder opens and closes it without selecting it.
            window.KeyPressQwerty(PhysicalKey.ArrowLeft, RawInputModifiers.None);
            Dispatcher.UIThread.RunJobs();
            window.KeyPressQwerty(PhysicalKey.Enter, RawInputModifiers.None);
            Dispatcher.UIThread.RunJobs();
            locked.IsExpanded.ShouldBeFalse();
            tree.SelectedItem.ShouldBeNull();
            invoked.ShouldBeEmpty();

            // A click toggles it too.
            FocusRingInput.Click(window, Row(tree, locked));
            Dispatcher.UIThread.RunJobs();
            locked.IsExpanded.ShouldBeTrue();
            tree.SelectedItem.ShouldBeNull();

            // Turning IsDisabled off makes it selectable again.
            locked.IsDisabled = false;
            Dispatcher.UIThread.RunJobs();
            Row(tree, locked).Node!.IsDisabled.ShouldBeFalse();
            FocusRingInput.Click(window, Row(tree, locked));
            Dispatcher.UIThread.RunJobs();
            tree.SelectedItem.ShouldBe(locked);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Keys_Still_Navigate_When_Focus_Is_Inside_The_Row_Content()
    {
        var data = Schema();
        var clicks = 0;
        var template = new FuncDataTemplate<Node>((node, _) =>
        {
            var panel = new DockPanel();
            var button = new Button { Content = "Open", Name = "RowButton" };
            button.Click += (_, _) => clicks++;
            DockPanel.SetDock(button, Dock.Right);
            panel.Children.Add(button);
            panel.Children.Add(new TextBox { Name = "RowText", Text = node.Name });
            return panel;
        }, true);
        var tree = SchemaTree(data, template);
        var invoked = new List<object>();
        tree.ItemInvoked += (_, e) => invoked.Add(e.Item);
        var window = Show(tree);
        try
        {
            var button = Row(tree, data[1]).GetVisualDescendants().OfType<Button>().First();
            button.Focus(NavigationMethod.Tab);
            Dispatcher.UIThread.RunJobs();
            tree.FocusKey.ShouldBe(data[1]);

            // Space belongs to the button, not to the row.
            window.KeyPressQwerty(PhysicalKey.Space, RawInputModifiers.None);
            window.KeyReleaseQwerty(PhysicalKey.Space, RawInputModifiers.None);
            Dispatcher.UIThread.RunJobs();
            clicks.ShouldBe(1);
            invoked.ShouldBeEmpty();
            tree.SelectedItem.ShouldBeNull();

            // Arrows leave the content for the next row.
            window.KeyPressQwerty(PhysicalKey.ArrowDown, RawInputModifiers.None);
            Dispatcher.UIThread.RunJobs();
            tree.FocusKey.ShouldBe(data[2]);
            Row(tree, data[2]).IsFocused.ShouldBeTrue();

            // Keys typed into a text box stay in the text box.
            var text = Row(tree, data[1]).GetVisualDescendants().OfType<TextBox>().First();
            text.Focus(NavigationMethod.Pointer);
            Dispatcher.UIThread.RunJobs();
            window.KeyPressQwerty(PhysicalKey.Space, RawInputModifiers.None);
            window.KeyTextInput("a");
            window.KeyPressQwerty(PhysicalKey.ArrowDown, RawInputModifiers.None);
            Dispatcher.UIThread.RunJobs();
            text.IsFocused.ShouldBeTrue();
            tree.SelectedItem.ShouldBeNull();
            tree.FocusKey.ShouldBe(data[1]);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Row_Height_Sets_The_Slot_And_The_Virtualized_Range()
    {
        var data = Enumerable.Range(1, 200).Select(i => new Node($"item-{i}", "text")).ToList();
        var tree = new TreeView { ItemsSource = data, RowHeight = 56, MaxHeight = 400 };
        var window = Show(tree);
        try
        {
            var row = Row(tree, data[0]);
            row.Bounds.Height.ShouldBe(56, 0.5);
            Row(tree, data[1]).Bounds.Top.ShouldBe(56, 0.5);
            tree.RowsPanel!.Bounds.Height.ShouldBe(200 * 56, 1);
            Rows(tree).Count().ShouldBeLessThan(20);

            tree.ClearValue(TreeView.RowHeightProperty);
            Dispatcher.UIThread.RunJobs();
            Row(tree, data[0]).Bounds.Height.ShouldBe(38, 0.5);
            tree.RowsPanel.Bounds.Height.ShouldBe(200 * 38, 1);
        }
        finally
        {
            window.Close();
        }
    }
}
