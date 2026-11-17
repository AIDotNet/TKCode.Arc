using System.Collections.ObjectModel;
using System.ComponentModel;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Data;
using Avalonia.Headless.XUnit;
using Avalonia.Layout;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;
using Avalonia.VisualTree;

namespace TKCode.Arc.Tests.Docking;

/// <summary>Item sources (spec 4.5, 15): one pane per item, collection changes, user close, templates, IDockItem, restore.</summary>
public class DockSourcesTests
{
    internal sealed class FileItem : IDockItem, INotifyPropertyChanged
    {
        private string _name;

        public FileItem(string path, string name)
        {
            Path = path;
            _name = name;
        }

        public string Path { get; }

        public string Name
        {
            get => _name;
            set
            {
                _name = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Name)));
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IDockItem.DockTitle)));
            }
        }

        public string DockId => "file:" + Path;

        public string DockTitle => Name;

        public event PropertyChangedEventHandler? PropertyChanged;
    }

    private static DockLayout Layout() => new()
    {
        Root = new DockSplit(Orientation.Horizontal,
            new DockGroup(DockSample.Tool("explorer", "Explorer", DockSide.Left)) { Size = 25 },
            new DockDocumentArea { Size = 75 })
    };

    private static void Settle() => Dispatcher.UIThread.RunJobs();

    [AvaloniaFact]
    public void Each_Item_Becomes_A_Document_Pane_With_The_Item_As_Its_Data_Context()
    {
        var layout = Layout();
        var files = new ObservableCollection<string> { "README.md", "App.axaml" };
        layout.DocumentsSource = files;
        var window = DockLayoutViewTests.Show(layout);
        try
        {
            var group = layout.Workspace.DocumentArea!.Root.ShouldBeOfType<DockGroup>();
            group.Panes.Select(p => p.Id).ShouldBe(["README.md", "App.axaml"]);
            var readme = group.Panes[0];
            readme.Title.ShouldBe("README.md");
            readme.DataContext.ShouldBe("README.md");
            readme.Content.ShouldBe("README.md");
            readme.PreferredSide.ShouldBeNull();
            layout.PaneForItem("App.axaml").ShouldBe(group.Panes[1]);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Adding_Opens_And_Activates_Removing_Closes_Without_Asking()
    {
        var layout = Layout();
        var files = new ObservableCollection<string> { "a.md" };
        layout.DocumentsSource = files;
        var window = DockLayoutViewTests.Show(layout);
        try
        {
            var closing = 0;
            var closed = new List<string>();
            var kinds = new List<DockLayoutChangeKind>();
            layout.PaneClosing += (_, _) => closing++;
            layout.PaneClosed += (_, e) => closed.Add(e.Pane!.Id);
            layout.LayoutChanged += (_, e) => kinds.Add(e.Kind);
            files.Add("b.md");
            Settle();
            var b = layout.Workspace.FindPane("b.md").ShouldNotBeNull();
            layout.ActivePane.ShouldBe(b);
            kinds.ShouldContain(DockLayoutChangeKind.Open);
            b.GetVisualRoot().ShouldBe(window);

            files.Remove("a.md");
            Settle();
            layout.Workspace.FindPane("a.md").ShouldBeNull();
            closing.ShouldBe(0);
            closed.ShouldBe(["a.md"]);
            kinds.ShouldContain(DockLayoutChangeKind.Close);

            files[0] = "c.md";
            Settle();
            layout.Workspace.FindPane("b.md").ShouldBeNull();
            layout.Workspace.FindPane("c.md").ShouldNotBeNull();

            files.Add("d.md");
            files.Clear();
            Settle();
            layout.Workspace.DocumentArea!.Root.ShouldBeNull();
            layout.Workspace.FindPane("explorer").ShouldNotBeNull();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Closing_A_Tab_Removes_Its_Item_Unless_A_Handler_Cancels()
    {
        var layout = Layout();
        var files = new ObservableCollection<string> { "a.md", "b.md" };
        layout.DocumentsSource = files;
        var window = DockLayoutViewTests.Show(layout);
        try
        {
            var cancel = true;
            var closed = new List<string>();
            layout.PaneClosing += (_, e) => e.Cancel = cancel;
            layout.PaneClosed += (_, e) => closed.Add(e.Pane!.Id);
            var a = layout.Workspace.FindPane("a.md")!;
            layout.Close(a).ShouldBeFalse();
            files.ShouldBe(["a.md", "b.md"]);
            a.Group.ShouldNotBeNull();

            cancel = false;
            // The close button of the tab.
            DockLayoutViewTests.TabOf(layout, "a.md").CloseButton.ShouldNotBeNull();
            layout.Close(a).ShouldBeTrue();
            Settle();
            files.ShouldBe(["b.md"]);
            a.Group.ShouldBeNull();
            closed.ShouldBe(["a.md"]);
            // The pane let go of its item.
            a.DataContext.ShouldBeNull();
            layout.PaneForItem("a.md").ShouldBeNull();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void A_Read_Only_Source_Keeps_Its_Item_When_The_Tab_Closes()
    {
        var layout = Layout();
        var files = new[] { "a.md", "b.md" };
        layout.DocumentsSource = files;
        var window = DockLayoutViewTests.Show(layout);
        try
        {
            var closed = new List<string>();
            layout.PaneClosed += (_, e) => closed.Add(e.Pane!.Id);
            layout.Close(layout.Workspace.FindPane("a.md")!).ShouldBeTrue();
            files.ShouldBe(["a.md", "b.md"]);
            layout.Workspace.FindPane("a.md").ShouldBeNull();
            closed.ShouldBe(["a.md"]);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void A_List_Without_Notifications_Loses_The_Item_And_The_Pane_Closes()
    {
        var layout = Layout();
        var files = new List<string> { "a.md", "b.md" };
        layout.DocumentsSource = files;
        var window = DockLayoutViewTests.Show(layout);
        try
        {
            layout.Close(layout.Workspace.FindPane("a.md")!).ShouldBeTrue();
            files.ShouldBe(["b.md"]);
            layout.Workspace.FindPane("a.md").ShouldBeNull();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void A_Template_That_Makes_A_Pane_Is_Used_And_Other_Output_Is_Wrapped()
    {
        var layout = Layout();
        var files = new ObservableCollection<FileItem> { new("src/App.cs", "App.cs") };
        layout.DocumentTemplate = new FuncDataTemplate<FileItem>((item, _) => new DockPane
        {
            [!DockPane.IdProperty] = new Binding(nameof(FileItem.Path)),
            [!DockPane.TitleProperty] = new Binding(nameof(FileItem.Name)),
            Content = new TextBlock { [!TextBlock.TextProperty] = new Binding(nameof(FileItem.Path)) },
            IsModified = true
        });
        layout.DocumentsSource = files;
        var window = DockLayoutViewTests.Show(layout);
        try
        {
            var pane = layout.Workspace.FindPane("src/App.cs").ShouldNotBeNull();
            pane.Title.ShouldBe("App.cs");
            pane.IsModified.ShouldBeTrue();
            pane.Content.ShouldBeOfType<TextBlock>().Text.ShouldBe("src/App.cs");
            // The binding stays: renaming the item renames the tab.
            files[0].Name = "Program.cs";
            pane.Title.ShouldBe("Program.cs");

            // A template that makes a view: the view is wrapped; id and title come from IDockItem.
            layout.DocumentTemplate = new FuncDataTemplate<FileItem>((item, _) => new TextBlock { Text = item.Path });
            Settle();
            var wrapped = layout.Workspace.FindPane("file:src/App.cs").ShouldNotBeNull();
            wrapped.Title.ShouldBe("Program.cs");
            wrapped.Content.ShouldBeOfType<TextBlock>().Text.ShouldBe("src/App.cs");
            wrapped.DataContext.ShouldBe(files[0]);
            layout.Workspace.FindPane("src/App.cs").ShouldBeNull();
            // The item's DockTitle drives the wrapped pane's title.
            files[0].Name = "Main.cs";
            wrapped.Title.ShouldBe("Main.cs");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void A_Xaml_Data_Template_Builds_Panes_With_Bindings()
    {
        var layout = (DockLayout)AvaloniaRuntimeXamlLoader.Load("""
            <arc:DockLayout xmlns="https://github.com/avaloniaui" xmlns:arc="https://github.com/AIDotNet/TKCode.Arc">
                <arc:DockLayout.DocumentTemplate>
                    <DataTemplate>
                        <arc:DockPane Id="{Binding Path}" Title="{Binding Name}">
                            <TextBlock Text="{Binding Path}" />
                        </arc:DockPane>
                    </DataTemplate>
                </arc:DockLayout.DocumentTemplate>
                <arc:DockDocumentArea />
            </arc:DockLayout>
            """);
        var files = new ObservableCollection<FileItem> { new("a.cs", "A"), new("b.cs", "B") };
        layout.DocumentsSource = files;
        var window = DockLayoutViewTests.Show(layout);
        try
        {
            layout.Workspace.AllPanes().Select(p => (p.Id, p.Title)).ShouldBe([("a.cs", "A"), ("b.cs", "B")]);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Tools_Go_To_Their_Preferred_Side_Or_The_Left()
    {
        var layout = Layout();
        var tools = new ObservableCollection<string> { "Outline", "Problems" };
        layout.ToolTemplate = new FuncDataTemplate<string>((item, _) => item == "Problems"
            ? new DockPane { PreferredSide = DockSide.Bottom, Content = new TextBlock { Text = item } }
            : new TextBlock { Text = item });
        layout.ToolsSource = tools;
        var window = DockLayoutViewTests.Show(layout);
        try
        {
            var outline = layout.Workspace.FindPane("Outline").ShouldNotBeNull();
            outline.PreferredSide.ShouldBe(DockSide.Left);
            // The left tools share the explorer's group.
            outline.Group.ShouldBe(layout.Workspace.FindPane("explorer")!.Group);
            var problems = layout.Workspace.FindPane("Problems").ShouldNotBeNull();
            var root = layout.Workspace.Root.ShouldBeOfType<DockSplit>();
            root.Orientation.ShouldBe(Orientation.Vertical);
            root.Children[^1].ShouldBe(problems.Group);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void A_Saved_Layout_Restores_Source_Panes_By_Id()
    {
        var files = new ObservableCollection<string> { "a.md", "b.md", "c.md" };
        var layout = Layout();
        layout.DocumentsSource = files;
        var window = DockLayoutViewTests.Show(layout);
        string json;
        try
        {
            var c = layout.Workspace.FindPane("c.md")!;
            layout.Move(c, DockTarget.Split(c.Group!, DockPosition.Right)).ShouldBeTrue();
            layout.AutoHide(layout.Workspace.FindPane("explorer")!).ShouldBeTrue();
            json = layout.SaveLayout();
        }
        finally
        {
            window.Close();
        }

        var other = Layout();
        other.DocumentsSource = files;
        var otherWindow = DockLayoutViewTests.Show(other);
        try
        {
            other.LoadLayout(json).ShouldBeTrue();
            Settle();
            var groups = other.Workspace.DocumentArea!.Root!.SelfAndDescendants().OfType<DockGroup>().ToList();
            groups.Select(g => string.Join(",", g.Panes.Select(p => p.Id))).ShouldBe(["a.md,b.md", "c.md"]);
            other.Workspace.LeftHidden.Select(p => p.Id).ShouldBe(["explorer"]);
            // The restored panes are still the source's: removing an item closes its pane.
            files.Remove("c.md");
            Settle();
            other.Workspace.FindPane("c.md").ShouldBeNull();
            other.Workspace.DocumentArea!.Root.ShouldBeOfType<DockGroup>();
        }
        finally
        {
            otherWindow.Close();
        }
    }

    [AvaloniaFact]
    public void A_Closed_Source_Pane_Does_Not_Come_Back_With_A_Restored_Layout()
    {
        var files = new ObservableCollection<string> { "a.md", "b.md" };
        var layout = Layout();
        layout.DocumentsSource = files;
        var window = DockLayoutViewTests.Show(layout);
        try
        {
            var json = layout.SaveLayout();
            files.Remove("b.md");
            Settle();
            layout.LoadLayout(json).ShouldBeTrue();
            layout.Workspace.FindPane("b.md").ShouldBeNull();
            layout.Workspace.FindPane("a.md").ShouldNotBeNull();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Sources_Set_While_Xaml_Loads_Wait_For_The_Tree()
    {
        var layout = new DockLayout();
        layout.BeginInit();
        layout.DocumentsSource = new[] { "a.md" };
        layout.Root = new DockSplit(Orientation.Horizontal,
            new DockGroup(DockSample.Tool("explorer", "Explorer", DockSide.Left)),
            new DockDocumentArea());
        layout.EndInit();
        var pane = layout.Workspace.FindPane("a.md").ShouldNotBeNull();
        pane.Group!.IsDocumentGroup.ShouldBeTrue();
        layout.Workspace.Root.ShouldBeOfType<DockSplit>().Children.Count.ShouldBe(2);
    }

    [AvaloniaFact]
    public void Replacing_The_Source_Or_The_Workspace_Keeps_The_Panes_In_Step()
    {
        var layout = Layout();
        layout.DocumentsSource = new ObservableCollection<string> { "a.md" };
        var window = DockLayoutViewTests.Show(layout);
        try
        {
            var a = layout.Workspace.FindPane("a.md")!;
            layout.DocumentsSource = new ObservableCollection<string> { "x.md" };
            layout.Workspace.FindPane("a.md").ShouldBeNull();
            a.Group.ShouldBeNull();
            var x = layout.Workspace.FindPane("x.md").ShouldNotBeNull();

            var old = layout.Workspace;
            layout.Workspace = new DockWorkspace { Root = new DockDocumentArea() };
            old.FindPane("x.md").ShouldBeNull();
            layout.Workspace.FindPane("x.md").ShouldBe(x);
            x.Group!.IsDocumentGroup.ShouldBeTrue();

            layout.DocumentsSource = null;
            layout.Workspace.AllPanes().ShouldBeEmpty();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Duplicate_Ids_Are_Refused()
    {
        var layout = Layout();
        Should.Throw<InvalidOperationException>(() => layout.DocumentsSource = new[] { "same", "same" });
    }
}
