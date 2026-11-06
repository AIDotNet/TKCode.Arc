using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;
using Avalonia.VisualTree;

namespace TKCode.Arc.Tests.Docking;

/// <summary>The dock views (spec 5.2, 5.3, 6, 7): structure from XAML, incremental updates, tabs, sizes, closing.</summary>
public class DockLayoutViewTests
{
    internal const string SpecXaml = """
        <arc:DockLayout xmlns="https://github.com/avaloniaui" xmlns:arc="https://github.com/AIDotNet/TKCode.Arc"
                        xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">
            <arc:DockSplit Orientation="Horizontal">
                <arc:DockGroup Size="22">
                    <arc:DockPane Id="explorer" Title="Explorer" Icon="{x:Static arc:ArcIcons.Folder}" PreferredSide="Left">
                        <TextBox Name="ExplorerBox" />
                    </arc:DockPane>
                    <arc:DockPane Id="search" Title="Search" PreferredSide="Left">
                        <TextBlock Text="Search" />
                    </arc:DockPane>
                </arc:DockGroup>
                <arc:DockSplit Orientation="Vertical" Size="78">
                    <arc:DockDocumentArea Size="70">
                        <arc:DockGroup>
                            <arc:DockPane Id="doc:readme" Title="README.md"><TextBlock Text="Readme" /></arc:DockPane>
                            <arc:DockPane Id="doc:app" Title="App.axaml"><TextBlock Text="App" /></arc:DockPane>
                        </arc:DockGroup>
                    </arc:DockDocumentArea>
                    <arc:DockGroup Size="30">
                        <arc:DockPane Id="terminal" Title="Terminal" PreferredSide="Bottom"><TextBlock Text="Terminal" /></arc:DockPane>
                    </arc:DockGroup>
                </arc:DockSplit>
            </arc:DockSplit>
        </arc:DockLayout>
        """;

    internal static DockLayout LoadSpec() => (DockLayout)AvaloniaRuntimeXamlLoader.Load(SpecXaml);

    internal static Window Show(DockLayout layout, double width = 900, double height = 600)
    {
        var window = TestHelpers.Show(layout);
        window.Width = width;
        window.Height = height;
        Dispatcher.UIThread.RunJobs();
        AvaloniaHeadlessPlatform.ForceRenderTimerTick();
        Dispatcher.UIThread.RunJobs();
        return window;
    }

    internal static DockPane Pane(DockLayout layout, string id) => layout.Workspace.FindPane(id).ShouldNotBeNull();

    internal static DockGroupView ViewOf(DockGroup group) => group.View.ShouldBeOfType<DockGroupView>();

    internal static DockTab TabOf(DockLayout layout, string id)
    {
        var pane = Pane(layout, id);
        return ViewOf(pane.Group!).TabOf(pane).ShouldNotBeNull();
    }

    [AvaloniaFact]
    public void The_Xaml_Form_Renders_The_Nested_Splits_And_Groups()
    {
        var layout = LoadSpec();
        var window = Show(layout);
        try
        {
            var root = layout.Host!.Children.Single().ShouldBeOfType<DockSplitView>();
            root.Orientation.ShouldBe(Orientation.Horizontal);
            root.Panels.Panels.Count.ShouldBe(2);
            root.Panels.Panels[0].Content.ShouldBeOfType<DockGroupView>();
            var right = root.Panels.Panels[1].Content.ShouldBeOfType<DockSplitView>();
            right.Orientation.ShouldBe(Orientation.Vertical);
            right.Panels.Panels[0].Content.ShouldBeOfType<DockDocumentAreaView>().Child.ShouldBeOfType<DockGroupView>();
            right.Panels.Panels[1].Content.ShouldBeOfType<DockGroupView>();

            // Each group is a card with a 36px strip of 28px tabs; only active panes are in the visual tree.
            var tools = ViewOf(Pane(layout, "explorer").Group!);
            tools.Strip!.Bounds.Height.ShouldBe(36);
            TabOf(layout, "explorer").Bounds.Height.ShouldBe(28);
            Pane(layout, "explorer").GetVisualRoot().ShouldBe(window);
            Pane(layout, "search").GetVisualRoot().ShouldBeNull();
            Pane(layout, "doc:readme").GetVisualRoot().ShouldBe(window);
            Pane(layout, "doc:app").GetVisualRoot().ShouldBeNull();
            ViewOf(Pane(layout, "doc:readme").Group!).Classes.ShouldContain(":document");
            TabOf(layout, "doc:readme").Classes.ShouldContain(":document");
            TabOf(layout, "doc:readme").ShowsClose.ShouldBeTrue();
            TabOf(layout, "explorer").ShowsClose.ShouldBeFalse();

            // Shares come from the model.
            root.Panels.Sizes.ShouldBe([22, 78]);
            right.Panels.Sizes.ShouldBe([70, 30]);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Choosing_A_Tab_Shows_Its_Pane_And_Takes_The_Old_One_Out_Of_The_Tree()
    {
        var layout = LoadSpec();
        var window = Show(layout);
        try
        {
            // The tool group is narrow: its tabs overflow and the overflow button covers the end of the last one.
            var tab = TabOf(layout, "search");
            var point = tab.TranslatePoint(new Point(8, tab.Bounds.Height / 2), window)!.Value;
            window.MouseDown(point, MouseButton.Left);
            window.MouseUp(point, MouseButton.Left);
            Dispatcher.UIThread.RunJobs();
            Pane(layout, "search").GetVisualRoot().ShouldBe(window);
            Pane(layout, "explorer").GetVisualRoot().ShouldBeNull();
            layout.ActivePane.ShouldBe(Pane(layout, "search"));
            tab.IsSelected.ShouldBeTrue();
            tab.Classes.ShouldContain(":selected");
            var group = ViewOf(Pane(layout, "search").Group!);
            group.Classes.ShouldContain(":active");
            tab.Classes.ShouldContain(":group-active");
            group.Strip!.Pill.HasTarget.ShouldBeTrue();
            ViewOf(Pane(layout, "terminal").Group!).Classes.ShouldNotContain(":active");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void A_Moved_Pane_Keeps_Its_Content_Instance()
    {
        var layout = LoadSpec();
        var window = Show(layout);
        try
        {
            var explorer = Pane(layout, "explorer");
            var box = explorer.Content.ShouldBeOfType<TextBox>();
            box.Text = "typed";
            layout.Move(explorer, DockTarget.Split(Pane(layout, "terminal").Group!, DockPosition.Right)).ShouldBeTrue();
            Dispatcher.UIThread.RunJobs();
            explorer.Content.ShouldBeSameAs(box);
            box.Text.ShouldBe("typed");
            box.GetVisualRoot().ShouldBe(window);
            var view = ViewOf(explorer.Group!);
            view.GetVisualRoot().ShouldBe(window);
            explorer.GetVisualAncestors().ShouldContain(view);
            // The old group kept its other pane and now shows it.
            Pane(layout, "search").GetVisualRoot().ShouldBe(window);
            layout.LastAnnouncement.ShouldBe("Explorer docked to the right of Terminal");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Views_Are_Reused_When_The_Structure_Changes()
    {
        var layout = LoadSpec();
        var window = Show(layout);
        try
        {
            var documents = layout.Workspace.DocumentArea!;
            var documentsView = documents.View;
            var toolsView = Pane(layout, "explorer").Group!.View;
            // Closing the terminal collapses the vertical split: the document area moves up into the root split.
            layout.Close(Pane(layout, "terminal")).ShouldBeTrue();
            Dispatcher.UIThread.RunJobs();
            documents.View.ShouldBeSameAs(documentsView);
            Pane(layout, "explorer").Group!.View.ShouldBeSameAs(toolsView);
            var root = layout.Host!.Children.Single().ShouldBeOfType<DockSplitView>();
            root.Panels.Panels[1].Content.ShouldBeSameAs(documentsView);
            documentsView!.GetVisualRoot().ShouldBe(window);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Node_Sizes_And_Divider_Positions_Stay_In_Step()
    {
        var layout = LoadSpec();
        var window = Show(layout);
        try
        {
            var root = layout.Host!.Children.Single().ShouldBeOfType<DockSplitView>();
            var tools = Pane(layout, "explorer").Group!;
            // Model to view.
            tools.Size = 30;
            root.Panels.Sizes.ShouldBe([30, 78]);
            Dispatcher.UIThread.RunJobs();

            // View to model: move the divider with the keyboard.
            var changes = new List<DockLayoutChangeKind>();
            layout.LayoutChanged += (_, e) => changes.Add(e.Kind);
            var handle = root.Panels.Handles[0];
            handle.Focus();
            window.KeyPressQwerty(PhysicalKey.ArrowRight, RawInputModifiers.None);
            Dispatcher.UIThread.RunJobs();
            TestHelpers.PumpUntil(() => changes.Contains(DockLayoutChangeKind.Resize)).ShouldBeTrue();
            var right = (DockSplit)layout.Workspace.Root!.ShouldBeOfType<DockSplit>().Children[1];
            var shares = root.Panels.Sizes!;
            tools.Size.ShouldBe(shares[0], 1e-6);
            right.Size.ShouldBe(shares[1], 1e-6);
            tools.Size.ShouldBeGreaterThan(30 / 108.0 * 100);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void The_Close_Button_Closes_A_Document_And_Closing_Can_Be_Cancelled()
    {
        var layout = LoadSpec();
        var window = Show(layout);
        try
        {
            var readme = Pane(layout, "doc:readme");
            var app = Pane(layout, "doc:app");
            var cancel = true;
            var closed = new List<DockPane>();
            layout.PaneClosing += (_, e) => e.Cancel = cancel && e.Pane == readme;
            layout.PaneClosed += (_, e) => closed.Add(e.Pane!);

            var close = TabOf(layout, "doc:readme").CloseButton.ShouldNotBeNull();
            AutomationProperties.GetName(close).ShouldBe("Close README.md");
            close.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Avalonia.Controls.Button.ClickEvent));
            Dispatcher.UIThread.RunJobs();
            readme.Workspace.ShouldBe(layout.Workspace);
            closed.ShouldBeEmpty();

            cancel = false;
            layout.Select(readme);
            layout.Close(readme).ShouldBeTrue();
            Dispatcher.UIThread.RunJobs();
            closed.ShouldBe([readme]);
            readme.Workspace.ShouldBeNull();
            readme.GetVisualRoot().ShouldBeNull();
            app.GetVisualRoot().ShouldBe(window);
            layout.ActivePane.ShouldBe(app);
            layout.LastAnnouncement.ShouldBe("README.md closed");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void An_Empty_Document_Area_Shows_The_Empty_State()
    {
        var layout = LoadSpec();
        var window = Show(layout);
        try
        {
            layout.Close(Pane(layout, "doc:readme"));
            layout.Close(Pane(layout, "doc:app"));
            Dispatcher.UIThread.RunJobs();
            var view = layout.Workspace.DocumentArea!.View.ShouldBeOfType<DockDocumentAreaView>();
            var empty = view.EmptyCard.ShouldNotBeNull().Child.ShouldBeOfType<EmptyState>();
            empty.Title.ShouldBe("No open documents");
            empty.GetVisualRoot().ShouldBe(window);

            layout.EmptyDocumentContent = "Drop a file here";
            Dispatcher.UIThread.RunJobs();
            view.EmptyCard!.Child.ShouldBeOfType<ContentControl>().Content.ShouldBe("Drop a file here");

            // Opening a document fills the area again.
            var doc = DockSample.Document("doc:new", "New.md");
            layout.Open(doc);
            Dispatcher.UIThread.RunJobs();
            doc.GetVisualRoot().ShouldBe(window);
            doc.Group!.IsDocumentGroup.ShouldBeTrue();
            layout.ActivePane.ShouldBe(doc);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Focus_Inside_A_Pane_Makes_It_The_Active_Pane()
    {
        var layout = LoadSpec();
        var window = Show(layout);
        try
        {
            layout.ActivePane = Pane(layout, "terminal");
            var box = Pane(layout, "explorer").Content.ShouldBeOfType<TextBox>();
            box.Focus();
            Dispatcher.UIThread.RunJobs();
            layout.ActivePane.ShouldBe(Pane(layout, "explorer"));
            Pane(layout, "explorer").Classes.ShouldContain(":active");
            Pane(layout, "terminal").Classes.ShouldNotContain(":active");

            layout.Activate(Pane(layout, "search"));
            Dispatcher.UIThread.RunJobs();
            layout.ActivePane.ShouldBe(Pane(layout, "search"));
            TabOf(layout, "search").IsKeyboardFocusWithin.ShouldBeTrue();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Tabs_That_Do_Not_Fit_Scroll_And_Show_The_Overflow_List()
    {
        var layout = new DockLayout();
        var group = new DockGroup();
        for (var i = 0; i < 12; i++)
        {
            group.Panes.Add(DockSample.Document($"doc:{i}", $"Document number {i}.md"));
        }
        layout.Root = new DockDocumentArea { Root = group };
        var window = Show(layout, 420, 300);
        try
        {
            var view = ViewOf(group);
            view.Strip!.IsOverflowing.ShouldBeTrue();
            view.OverflowButton!.IsVisible.ShouldBeTrue();
            // Selecting the last tab scrolls it into view.
            layout.Select(group.Panes[11]);
            Dispatcher.UIThread.RunJobs();
            view.Strip.Offset.ShouldBeGreaterThan(0);
            var tab = view.TabOf(group.Panes[11])!;
            (tab.Bounds.Right).ShouldBeLessThanOrEqualTo(view.Strip.Bounds.Width + 0.5);

            layout.ShowTabList(view, view.OverflowButton);
            Dispatcher.UIThread.RunJobs();
            var rows = layout.Menu!.Items.Cast<MenuEntry>().ToList();
            rows.Count.ShouldBe(12);
            rows[11].IsChecked.ShouldBeTrue();
            rows[0].RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(MenuEntry.ClickEvent));
            Dispatcher.UIThread.RunJobs();
            group.ActivePane.ShouldBe(group.Panes[0]);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void A_Locked_Layout_Keeps_Its_Panes()
    {
        var layout = LoadSpec();
        var window = Show(layout);
        try
        {
            layout.IsLocked = true;
            Dispatcher.UIThread.RunJobs();
            layout.Classes.ShouldContain(":locked");
            TabOf(layout, "doc:readme").ShowsClose.ShouldBeFalse();
            layout.Close(Pane(layout, "doc:readme")).ShouldBeFalse();
            layout.CloseActiveCommand.CanExecute(null).ShouldBeFalse();
            // Maximize changes nothing in the layout, so it stays on.
            layout.BuildPaneMenu(Pane(layout, "terminal")).Where(row => (string?)row.Header != "Maximize").ShouldAllBe(row => !row.IsEnabled);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void The_Modified_Dot_Follows_The_Pane()
    {
        var layout = LoadSpec();
        var window = Show(layout);
        try
        {
            var readme = Pane(layout, "doc:readme");
            var tab = TabOf(layout, "doc:readme");
            readme.IsModified = true;
            tab.Classes.ShouldContain(":modified");
            AutomationProperties.GetName(tab).ShouldBe("README.md, modified");
            readme.IsModified = false;
            tab.Classes.ShouldNotContain(":modified");
            AutomationProperties.GetName(tab).ShouldBe("README.md");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Opening_A_Tool_Without_A_Target_Uses_Its_Preferred_Side()
    {
        var layout = LoadSpec();
        var window = Show(layout);
        try
        {
            var outline = DockSample.Tool("outline", "Outline", DockSide.Right);
            outline.PreferredSize = 225;
            var opened = new List<DockLayoutChangeKind>();
            layout.LayoutChanged += (_, e) => opened.Add(e.Kind);
            layout.Open(outline);
            Dispatcher.UIThread.RunJobs();
            opened.ShouldContain(DockLayoutChangeKind.Open);
            var root = layout.Workspace.Root.ShouldBeOfType<DockSplit>();
            root.Children[^1].ShouldBe(outline.Group);
            // 225px of the 888px inside the layout's padding.
            (outline.Group!.Size / root.Children.Sum(c => c.Size)).ShouldBe(225.0 / 888, 0.01);
            outline.GetVisualRoot().ShouldBe(window);
            Should.Throw<InvalidOperationException>(() => layout.Open(DockSample.Tool("outline", "Again", DockSide.Left)));
            Should.Throw<InvalidOperationException>(() => layout.Open(DockSample.Document("doc:x", "X"), DockTarget.Edge(DockPosition.Left)));
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Replacing_The_Workspace_Shows_The_New_One()
    {
        var layout = LoadSpec();
        var window = Show(layout);
        try
        {
            var sample = new DockSample();
            layout.Workspace = sample.Workspace;
            Dispatcher.UIThread.RunJobs();
            sample.Explorer.GetVisualRoot().ShouldBe(window);
            layout.Root.ShouldBe(sample.Root);
            layout.ActivePane.ShouldBeNull();
        }
        finally
        {
            window.Close();
        }
    }
}
