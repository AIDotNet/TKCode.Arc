using System.Collections.ObjectModel;
using System.ComponentModel;
using TKCode.Arc.Motion;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Data;
using Avalonia.Data.Core;
using Avalonia.Markup.Xaml.MarkupExtensions.CompiledBindings;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;

namespace TKCode.Arc.Tests.Blocks;

public class SidebarItemsSourceTests
{
    private sealed class MenuNode : INotifyPropertyChanged
    {
        private string _title = "";

        public string Key { get; set; } = "";

        public string Title
        {
            get => _title;
            set
            {
                _title = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Title)));
            }
        }

        public bool CanAdd { get; set; }
        public ObservableCollection<MenuNode> Children { get; } = new();

        public bool Visible
        {
            get => _visible;
            set
            {
                _visible = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Visible)));
            }
        }

        private bool _visible = true;

        public event PropertyChangedEventHandler? PropertyChanged;
    }

    private sealed class CustomItem : SidebarItem
    {
        public bool CanAdd { get; set; }
    }

    private static Sidebar Make(object source)
    {
        var sidebar = new Sidebar
        {
            ItemsSource = (System.Collections.IEnumerable)source,
            KeyMember = nameof(MenuNode.Key),
            LabelMember = nameof(MenuNode.Title),
            ChildrenMember = nameof(MenuNode.Children)
        };
        return sidebar;
    }

    [AvaloniaFact]
    public void Data_objects_become_items_with_mapped_members_and_children()
    {
        var parent = new MenuNode { Key = "p", Title = "Parent", Children = { new MenuNode { Key = "c", Title = "Child" } } };
        var data = new ObservableCollection<MenuNode> { parent, new MenuNode { Key = "q", Title = "Other" } };
        var sidebar = Make(data);

        var items = sidebar.Sections[0].Items;
        items.Count.ShouldBe(2);
        items[0].Label.ShouldBe("Parent");
        items[0].Key.ShouldBe("p");
        items[0].Tag.ShouldBeSameAs(parent);
        items[0].Items.Count.ShouldBe(1);
        items[0].Items[0].Label.ShouldBe("Child");

        parent.Title = "Renamed";
        items[0].Label.ShouldBe("Renamed");
    }

    [AvaloniaFact]
    public void IsVisibleMember_follows_a_data_members_visibility_and_the_binding_wins_over_the_path()
    {
        var node = new MenuNode { Key = "p", Title = "Parent" };
        var sidebar = Make(new ObservableCollection<MenuNode> { node });
        sidebar.IsVisibleMember = nameof(MenuNode.Visible);

        var item = sidebar.Sections[0].Items[0];
        item.IsVisible.ShouldBeTrue();
        node.Visible = false;
        item.IsVisible.ShouldBeFalse();
        node.Visible = true;
        item.IsVisible.ShouldBeTrue();

        // The compiled binding wins when both are set, and keeps the page trimming and NativeAOT safe.
        sidebar.IsVisibleMember = "Missing";
        sidebar.IsVisibleMemberBinding = Compiled(nameof(MenuNode.Visible), n => n.Visible, typeof(bool));
        item = sidebar.Sections[0].Items[0];
        node.Visible = false;
        item.IsVisible.ShouldBeFalse();
    }

    private static BindingBase Compiled(string name, Func<MenuNode, object?> get, Type type)
    {
        var info = new ClrPropertyInfo(name, o => get((MenuNode)o), null, type);
        var path = new CompiledBindingPathBuilder()
            .Property(info, PropertyInfoAccessorFactory.CreateInpcPropertyAccessor)
            .Build();
        return new CompiledBinding(path);
    }

    [AvaloniaFact]
    public void Member_bindings_map_without_string_paths_and_win_over_them()
    {
        var parent = new MenuNode { Key = "p", Title = "Parent", Children = { new MenuNode { Key = "c", Title = "Child" } } };
        var sidebar = new Sidebar
        {
            LabelMember = nameof(MenuNode.Key),
            KeyMemberBinding = Compiled(nameof(MenuNode.Key), n => n.Key, typeof(string)),
            LabelMemberBinding = Compiled(nameof(MenuNode.Title), n => n.Title, typeof(string)),
            ChildrenMemberBinding = Compiled(nameof(MenuNode.Children), n => n.Children, typeof(ObservableCollection<MenuNode>)),
            ItemsSource = new ObservableCollection<MenuNode> { parent }
        };

        var item = sidebar.Sections[0].Items[0];
        item.Key.ShouldBe("p");
        item.Label.ShouldBe("Parent");
        item.Items.Count.ShouldBe(1);
        item.Items[0].Label.ShouldBe("Child");

        parent.Title = "Renamed";
        item.Label.ShouldBe("Renamed");
        parent.Children.Add(new MenuNode { Key = "d", Title = "Second" });
        item.Items.Count.ShouldBe(2);

        sidebar.LabelMemberBinding = null;
        sidebar.Sections[0].Items[0].Label.ShouldBe("p");
    }

    [AvaloniaFact]
    public void Collection_changes_are_followed_and_items_are_kept()
    {
        var a = new MenuNode { Key = "a", Title = "A" };
        var data = new ObservableCollection<MenuNode> { a };
        var sidebar = Make(data);
        var first = sidebar.Sections[0].Items[0];
        first.IsExpanded = true;

        data.Add(new MenuNode { Key = "b", Title = "B" });
        a.Children.Add(new MenuNode { Key = "a1", Title = "A1" });

        var items = sidebar.Sections[0].Items;
        items.Count.ShouldBe(2);
        items[0].ShouldBeSameAs(first);
        items[0].IsExpanded.ShouldBeTrue();
        items[0].Items.Count.ShouldBe(1);

        data.Clear();
        items.Count.ShouldBe(0);
        sidebar.ItemsSource = null;
        sidebar.Sections.Count.ShouldBe(0);
    }

    [AvaloniaFact]
    public void Trailing_template_gets_the_data_as_data_context()
    {
        var data = new[] { new MenuNode { Key = "a", Title = "A", CanAdd = true } };
        var sidebar = Make(data);
        sidebar.TrailingTemplate = new FuncDataTemplate<MenuNode>((_, _) => new Button());

        var trailing = sidebar.Sections[0].Items[0].TrailingIcon.ShouldBeOfType<Button>();
        trailing.DataContext.ShouldBeSameAs(data[0]);
    }

    [AvaloniaFact]
    public void Subclassed_items_are_used_as_they_are()
    {
        var custom = new CustomItem { Key = "x", Label = "X", CanAdd = true };
        var sidebar = new Sidebar { ItemsSource = new[] { custom } };
        sidebar.TrailingTemplate = new FuncDataTemplate<CustomItem>((_, _) => new Button());

        sidebar.Sections[0].Items[0].ShouldBeSameAs(custom);
        custom.TrailingIcon.ShouldBeOfType<Button>().DataContext.ShouldBeSameAs(custom);
    }

    [AvaloniaFact]
    public void Bound_rows_are_shown_and_selected_by_key()
    {
        var data = new[] { new MenuNode { Key = "a", Title = "A" }, new MenuNode { Key = "b", Title = "B" } };
        var sidebar = Make(data);
        sidebar.SelectedKey = "b";
        var window = TestHelpers.Show(new DockPanel { Children = { sidebar } }, ArcMotionLevel.Off);
        try
        {
            Dispatcher.UIThread.RunJobs();
            sidebar.SelectedItem.ShouldBeSameAs(sidebar.Sections[0].Items[1]);
            sidebar.RowOf(sidebar.Sections[0].Items[0]).ShouldNotBeNull();
        }
        finally
        {
            window.Close();
        }
    }
}
