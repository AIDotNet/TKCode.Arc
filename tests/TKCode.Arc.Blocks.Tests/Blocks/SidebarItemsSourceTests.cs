using System.Collections.ObjectModel;
using System.ComponentModel;
using TKCode.Arc.Motion;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Data;
using Avalonia.Data.Core;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Markup.Xaml.MarkupExtensions.CompiledBindings;
using Avalonia.Threading;

namespace TKCode.Arc.Tests.Blocks;

public class SidebarItemsSourceTests
{
    private sealed class MenuNode : INotifyPropertyChanged
    {
        private string _title = "";
        private bool _visible = true;
        private bool _expanded;

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

        public bool Expanded
        {
            get => _expanded;
            set
            {
                if (_expanded != value)
                {
                    _expanded = value;
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Expanded)));
                }
            }
        }

        public bool ReadOnlyExpanded => true;

        public event PropertyChangedEventHandler? PropertyChanged;
    }

    private sealed class SectionNode : INotifyPropertyChanged
    {
        private string? _title;
        private string? _altTitle;
        private bool _collapsible;
        private bool _expanded = true;
        private bool _visible = true;
        private ObservableCollection<MenuNode>? _items = new();

        public string? Title
        {
            get => _title;
            set
            {
                _title = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Title)));
            }
        }

        public string? AltTitle
        {
            get => _altTitle;
            set
            {
                _altTitle = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(AltTitle)));
            }
        }

        public bool Collapsible
        {
            get => _collapsible;
            set
            {
                _collapsible = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Collapsible)));
            }
        }

        public bool Expanded
        {
            get => _expanded;
            set
            {
                if (_expanded != value)
                {
                    _expanded = value;
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Expanded)));
                }
            }
        }

        public bool ReadOnlyExpanded => false;

        public bool Visible
        {
            get => _visible;
            set
            {
                _visible = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Visible)));
            }
        }

        public ObservableCollection<MenuNode>? Items
        {
            get => _items;
            set
            {
                _items = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Items)));
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
    }

    private sealed class CustomItem : SidebarItem
    {
        public bool CanAdd { get; set; }
    }

    private sealed class CustomSection : SidebarSection
    {
        public string? CategoryId { get; set; }
    }

    private static BindingBase Compiled<TSource>(
        string name,
        Func<TSource, object?> get,
        Type type,
        Action<TSource, object?>? set = null,
        BindingMode mode = BindingMode.Default)
    {
        var info = new ClrPropertyInfo(name, o => get((TSource)o), set is null ? null : (o, v) => set((TSource)o, v), type);
        var path = new CompiledBindingPathBuilder()
            .Property(info, PropertyInfoAccessorFactory.CreateInpcPropertyAccessor)
            .Build();
        return new CompiledBinding(path) { Mode = mode };
    }

    private static BindingBase Compiled(string name, Func<MenuNode, object?> get, Type type) =>
        Compiled<MenuNode>(name, get, type);

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

    private static Sidebar MakeSections(object sectionsSource)
    {
        return new Sidebar
        {
            SectionsSource = (System.Collections.IEnumerable)sectionsSource,
            SectionHeaderMember = nameof(SectionNode.Title),
            SectionItemsMember = nameof(SectionNode.Items),
            SectionIsCollapsibleMember = nameof(SectionNode.Collapsible),
            SectionIsExpandedMember = nameof(SectionNode.Expanded),
            SectionIsVisibleMember = nameof(SectionNode.Visible),
            KeyMember = nameof(MenuNode.Key),
            LabelMember = nameof(MenuNode.Title),
            ChildrenMember = nameof(MenuNode.Children),
            IsExpandedMember = nameof(MenuNode.Expanded)
        };
    }

    private static void ClickControl(Window window, Control control)
    {
        var center = control.TranslatePoint(new Point(control.Bounds.Width / 2, control.Bounds.Height / 2), window)!.Value;
        window.MouseDown(center, MouseButton.Left);
        window.MouseUp(center, MouseButton.Left);
        Dispatcher.UIThread.RunJobs();
    }

    [AvaloniaFact]
    public void Section_header_trailing_template_gets_the_section_data_as_data_context()
    {
        var projects = new SectionNode { Title = "Projects", Items = new ObservableCollection<MenuNode> { new MenuNode { Key = "a", Title = "Atlas" } } };
        var teams = new SectionNode { Title = "Teams", Items = new ObservableCollection<MenuNode> { new MenuNode { Key = "t", Title = "Core" } } };
        var data = new ObservableCollection<SectionNode> { projects, teams };
        var sidebar = MakeSections(data);
        var declared = new SidebarSection { Header = "Declared", Items = { new SidebarItem { Key = "d", Label = "D" } } };
        sidebar.Sections.Add(declared);
        sidebar.SectionHeaderTrailingTemplate = new FuncDataTemplate<object>((_, _) => new Button());
        var window = TestHelpers.Show(sidebar);
        try
        {
            Dispatcher.UIThread.RunJobs();
            var bound = sidebar.Sections[0];
            sidebar.HeaderOf(bound).ShouldNotBeNull().TrailingContent.ShouldBeOfType<Button>().DataContext.ShouldBeSameAs(projects);
            sidebar.HeaderOf(sidebar.Sections[1])!.TrailingContent.ShouldBeOfType<Button>().DataContext.ShouldBeSameAs(teams);
            // A section declared in Sections is its own data.
            sidebar.HeaderOf(declared)!.TrailingContent.ShouldBeOfType<Button>().DataContext.ShouldBeSameAs(declared);

            // A section added to the source gets its content too.
            var later = new SectionNode { Title = "Later", Items = new ObservableCollection<MenuNode> { new MenuNode { Key = "l", Title = "L" } } };
            data.Add(later);
            Dispatcher.UIThread.RunJobs();
            var laterSection = sidebar.Sections.First(x => x.Header == "Later");
            sidebar.HeaderOf(laterSection)!.TrailingContent.ShouldBeOfType<Button>().DataContext.ShouldBeSameAs(later);
        }
        finally
        {
            window.Close();
        }
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

    [AvaloniaFact]
    public void IsExpandedMember_sets_initial_state_follows_vm_changes_and_allows_user_toggle_on_oneway_and_readonly()
    {
        var openNode = new MenuNode
        {
            Key = "open",
            Title = "Open parent",
            Expanded = true,
            Children = { new MenuNode { Key = "open/1", Title = "Child 1" } }
        };
        var closedNode = new MenuNode
        {
            Key = "closed",
            Title = "Closed parent",
            Expanded = false,
            Children = { new MenuNode { Key = "closed/1", Title = "Child 2" } }
        };
        var sidebar = Make(new ObservableCollection<MenuNode> { openNode, closedNode });
        sidebar.IsExpandedMember = nameof(MenuNode.Expanded);

        var openItem = sidebar.Sections[0].Items[0];
        var closedItem = sidebar.Sections[0].Items[1];
        openItem.IsExpanded.ShouldBeTrue();
        closedItem.IsExpanded.ShouldBeFalse();

        // VM PropertyChanged updates SidebarItem.IsExpanded.
        closedNode.Expanded = true;
        closedItem.IsExpanded.ShouldBeTrue();
        openNode.Expanded = false;
        openItem.IsExpanded.ShouldBeFalse();

        // User toggling works with OneWay string path without throwing or writing back.
        openItem.SetCurrentValue(SidebarItem.IsExpandedProperty, true);
        openItem.IsExpanded.ShouldBeTrue();
        openNode.Expanded.ShouldBeFalse();

        // User toggling also works on a read-only VM property (no setter) via path and via OneWay compiled binding.
        sidebar.IsExpandedMember = nameof(MenuNode.ReadOnlyExpanded);
        openItem = sidebar.Sections[0].Items[0];
        openItem.IsExpanded.ShouldBeTrue();
        openItem.SetCurrentValue(SidebarItem.IsExpandedProperty, false);
        openItem.IsExpanded.ShouldBeFalse();

        sidebar.IsExpandedMember = "Missing";
        sidebar.IsExpandedMemberBinding = Compiled<MenuNode>(
            nameof(MenuNode.ReadOnlyExpanded),
            n => n.ReadOnlyExpanded,
            typeof(bool),
            mode: BindingMode.OneWay);
        openItem = sidebar.Sections[0].Items[0];
        openItem.IsExpanded.ShouldBeTrue();
        openItem.SetCurrentValue(SidebarItem.IsExpandedProperty, false);
        openItem.IsExpanded.ShouldBeFalse();
    }

    [AvaloniaFact]
    public void IsExpandedMemberBinding_wins_over_path_and_twoway_writes_back_to_vm_and_unsubscribes_on_remove()
    {
        var node = new MenuNode
        {
            Key = "p",
            Title = "Projects",
            Expanded = true,
            Children = { new MenuNode { Key = "p/a", Title = "Atlas" } }
        };
        var data = new ObservableCollection<MenuNode> { node };
        var sidebar = Make(data);
        sidebar.IsExpandedMember = "Missing";
        sidebar.IsExpandedMemberBinding = Compiled<MenuNode>(
            nameof(MenuNode.Expanded),
            n => n.Expanded,
            typeof(bool),
            (n, v) => n.Expanded = (bool)v!,
            BindingMode.TwoWay);

        var item = sidebar.Sections[0].Items[0];
        item.IsExpanded.ShouldBeTrue();

        // User toggle writes back to VM.
        item.SetCurrentValue(SidebarItem.IsExpandedProperty, false);
        item.IsExpanded.ShouldBeFalse();
        node.Expanded.ShouldBeFalse();

        // VM update pushes to SidebarItem.
        node.Expanded = true;
        item.IsExpanded.ShouldBeTrue();

        // Removing item unsubscribes PropertyChanged on the removed SidebarItem (no stale write-back / leak).
        data.Remove(node);
        item.SetCurrentValue(SidebarItem.IsExpandedProperty, false);
        node.Expanded.ShouldBeTrue();
    }

    [AvaloniaFact]
    public void SectionsSource_maps_plain_section_models_nested_items_and_trailing_template()
    {
        var projects = new MenuNode
        {
            Key = "projects",
            Title = "Projects",
            Expanded = true,
            CanAdd = true,
            Children = { new MenuNode { Key = "projects/atlas", Title = "Atlas" } }
        };
        var workspace = new SectionNode
        {
            Title = "Workspace",
            Collapsible = true,
            Expanded = true,
            Visible = true,
            Items = new ObservableCollection<MenuNode> { projects }
        };
        var archive = new SectionNode
        {
            Title = "Archive",
            Collapsible = true,
            Expanded = false,
            Visible = false,
            Items = new ObservableCollection<MenuNode> { new MenuNode { Key = "arch/old", Title = "Old" } }
        };

        var sidebar = MakeSections(new ObservableCollection<SectionNode> { workspace, archive });
        sidebar.TrailingTemplate = new FuncDataTemplate<MenuNode>((_, _) => new Button());

        sidebar.Sections.Count.ShouldBe(2);

        var first = sidebar.Sections[0];
        first.Header.ShouldBe("Workspace");
        first.IsCollapsible.ShouldBeTrue();
        first.IsExpanded.ShouldBeTrue();
        first.IsVisible.ShouldBeTrue();
        first.Items.Count.ShouldBe(1);
        first.Items[0].Label.ShouldBe("Projects");
        first.Items[0].Key.ShouldBe("projects");
        first.Items[0].IsExpanded.ShouldBeTrue();
        first.Items[0].Tag.ShouldBeSameAs(projects);
        first.Items[0].Items.Count.ShouldBe(1);
        first.Items[0].Items[0].Label.ShouldBe("Atlas");
        first.Items[0].TrailingIcon.ShouldBeOfType<Button>().DataContext.ShouldBeSameAs(projects);

        var second = sidebar.Sections[1];
        second.Header.ShouldBe("Archive");
        second.IsCollapsible.ShouldBeTrue();
        second.IsExpanded.ShouldBeFalse();
        second.IsVisible.ShouldBeFalse();
        second.Items.Count.ShouldBe(1);
        second.Items[0].Label.ShouldBe("Old");
    }

    [AvaloniaFact]
    public void Section_member_bindings_win_over_string_paths_and_twoway_is_expanded_writes_back()
    {
        var sectionVm = new SectionNode
        {
            Title = "Primary",
            AltTitle = "Secondary",
            Collapsible = true,
            Expanded = false,
            Visible = true,
            Items = new ObservableCollection<MenuNode>
            {
                new MenuNode { Key = "home", Title = "Home" }
            }
        };
        var sections = new ObservableCollection<SectionNode> { sectionVm };
        var sidebar = new Sidebar
        {
            SectionsSource = sections,
            SectionHeaderMember = nameof(SectionNode.AltTitle),
            SectionItemsMember = "MissingItems",
            SectionIsCollapsibleMember = "MissingCollapsible",
            SectionIsExpandedMember = "MissingExpanded",
            SectionIsVisibleMember = "MissingVisible",
            SectionHeaderMemberBinding = Compiled<SectionNode>(nameof(SectionNode.Title), s => s.Title, typeof(string)),
            SectionItemsMemberBinding = Compiled<SectionNode>(nameof(SectionNode.Items), s => s.Items, typeof(ObservableCollection<MenuNode>)),
            SectionIsCollapsibleMemberBinding = Compiled<SectionNode>(nameof(SectionNode.Collapsible), s => s.Collapsible, typeof(bool)),
            SectionIsExpandedMemberBinding = Compiled<SectionNode>(
                nameof(SectionNode.Expanded),
                s => s.Expanded,
                typeof(bool),
                (s, v) => s.Expanded = (bool)v!,
                BindingMode.TwoWay),
            SectionIsVisibleMemberBinding = Compiled<SectionNode>(nameof(SectionNode.Visible), s => s.Visible, typeof(bool)),
            KeyMemberBinding = Compiled(nameof(MenuNode.Key), n => n.Key, typeof(string)),
            LabelMemberBinding = Compiled(nameof(MenuNode.Title), n => n.Title, typeof(string))
        };

        var section = sidebar.Sections[0];
        section.Header.ShouldBe("Primary");
        section.IsCollapsible.ShouldBeTrue();
        section.IsExpanded.ShouldBeFalse();
        section.IsVisible.ShouldBeTrue();
        section.Items.Count.ShouldBe(1);
        section.Items[0].Label.ShouldBe("Home");

        // User toggle writes back through TwoWay binding.
        section.SetCurrentValue(SidebarSection.IsExpandedProperty, true);
        section.IsExpanded.ShouldBeTrue();
        sectionVm.Expanded.ShouldBeTrue();

        // Clearing binding falls back to string path.
        sidebar.SectionHeaderMemberBinding = null;
        sidebar.Sections[0].Header.ShouldBe("Secondary");

        // Removing section unsubscribes PropertyChanged so mutating the removed section does not write back.
        var removed = sidebar.Sections[0];
        sections.Clear();
        removed.SetCurrentValue(SidebarSection.IsExpandedProperty, false);
        sectionVm.Expanded.ShouldBeTrue();
    }

    [AvaloniaFact]
    public void Section_default_is_expanded_is_true_and_oneway_readonly_still_toggles()
    {
        var sectionVm = new SectionNode
        {
            Title = "DefaultOpen",
            Collapsible = true,
            Items = new ObservableCollection<MenuNode> { new MenuNode { Key = "a", Title = "A" } }
        };
        var sidebar = new Sidebar
        {
            SectionsSource = new[] { sectionVm },
            SectionHeaderMember = nameof(SectionNode.Title),
            SectionItemsMember = nameof(SectionNode.Items),
            SectionIsCollapsibleMember = nameof(SectionNode.Collapsible),
            KeyMember = nameof(MenuNode.Key),
            LabelMember = nameof(MenuNode.Title)
        };
        sidebar.Sections[0].IsExpanded.ShouldBeTrue();

        // Read-only IsExpanded property starts false and can still be toggled locally.
        sidebar.SectionIsExpandedMember = nameof(SectionNode.ReadOnlyExpanded);
        var section = sidebar.Sections[0];
        section.IsExpanded.ShouldBeFalse();
        section.SetCurrentValue(SidebarSection.IsExpandedProperty, true);
        section.IsExpanded.ShouldBeTrue();
    }

    [AvaloniaFact]
    public void Section_property_changes_and_replacing_items_collection_update_live()
    {
        var oldItems = new ObservableCollection<MenuNode> { new MenuNode { Key = "a", Title = "A" } };
        var sectionVm = new SectionNode
        {
            Title = "Group 1",
            Collapsible = false,
            Expanded = true,
            Visible = true,
            Items = oldItems
        };
        var sidebar = MakeSections(new ObservableCollection<SectionNode> { sectionVm });
        var window = TestHelpers.Show(new DockPanel { Children = { sidebar } }, ArcMotionLevel.Off);
        try
        {
            Dispatcher.UIThread.RunJobs();
            var section = sidebar.Sections[0];
            var header = sidebar.HeaderOf(section).ShouldNotBeNull();
            header.Title.ShouldBe("Group 1");
            header.IsInteractive.ShouldBeFalse();

            sectionVm.Title = "Group Renamed";
            sectionVm.Collapsible = true;
            sectionVm.Expanded = false;
            Dispatcher.UIThread.RunJobs();

            section.Header.ShouldBe("Group Renamed");
            section.IsCollapsible.ShouldBeTrue();
            section.IsExpanded.ShouldBeFalse();
            header.Title.ShouldBe("Group Renamed");
            header.IsInteractive.ShouldBeTrue();
            header.IsExpanded.ShouldBeFalse();

            sectionVm.Visible = false;
            Dispatcher.UIThread.RunJobs();
            section.IsVisible.ShouldBeFalse();
            sidebar.NavigableNodes.ShouldBeEmpty();

            sectionVm.Visible = true;
            sectionVm.Expanded = true;
            var newItems = new ObservableCollection<MenuNode> { new MenuNode { Key = "b", Title = "B" } };
            sectionVm.Items = newItems;
            Dispatcher.UIThread.RunJobs();

            section.Items.Count.ShouldBe(1);
            section.Items[0].Label.ShouldBe("B");

            // Old collection is no longer watched; new collection is watched.
            oldItems.Add(new MenuNode { Key = "stale", Title = "Stale" });
            section.Items.Count.ShouldBe(1);

            newItems.Add(new MenuNode { Key = "c", Title = "C" });
            section.Items.Count.ShouldBe(2);
            section.Items[1].Label.ShouldBe("C");

            // Replacing Items with null clears the section's items.
            sectionVm.Items = null;
            section.Items.Count.ShouldBe(0);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void SectionsSource_and_section_items_collection_changes_preserve_instances_across_add_remove_reorder()
    {
        var itemA = new MenuNode { Key = "a", Title = "A" };
        var itemB = new MenuNode { Key = "b", Title = "B" };
        var sec1 = new SectionNode { Title = "One", Collapsible = true, Items = new ObservableCollection<MenuNode> { itemA } };
        var sec2 = new SectionNode { Title = "Two", Collapsible = true, Items = new ObservableCollection<MenuNode> { itemB } };
        var sections = new ObservableCollection<SectionNode> { sec1, sec2 };

        var sidebar = MakeSections(sections);
        var boundSec1 = sidebar.Sections[0];
        var boundSec2 = sidebar.Sections[1];
        var boundItemA = boundSec1.Items[0];
        var boundItemB = boundSec2.Items[0];

        // Add a third section and a second item to sec1.
        var sec3 = new SectionNode { Title = "Three", Items = new ObservableCollection<MenuNode>() };
        sections.Add(sec3);
        sec1.Items!.Add(new MenuNode { Key = "a2", Title = "A2" });

        sidebar.Sections.Count.ShouldBe(3);
        sidebar.Sections[0].ShouldBeSameAs(boundSec1);
        sidebar.Sections[1].ShouldBeSameAs(boundSec2);
        boundSec1.Items[0].ShouldBeSameAs(boundItemA);
        boundSec1.Items.Count.ShouldBe(2);

        // Reorder sections and items.
        sections.Move(0, 1);
        sec1.Items.Move(0, 1);

        sidebar.Sections[0].ShouldBeSameAs(boundSec2);
        sidebar.Sections[1].ShouldBeSameAs(boundSec1);
        boundSec1.Items[1].ShouldBeSameAs(boundItemA);
        boundSec2.Items[0].ShouldBeSameAs(boundItemB);

        // Remove sec2.
        sections.Remove(sec2);
        sidebar.Sections.Count.ShouldBe(2);
        sidebar.Sections[0].ShouldBeSameAs(boundSec1);
    }

    [AvaloniaFact]
    public void SidebarSection_instances_in_SectionsSource_are_used_as_is()
    {
        var direct = new CustomSection
        {
            Header = "Direct",
            CategoryId = "cat-1",
            IsCollapsible = true,
            Items = { new SidebarItem { Key = "d1", Label = "Direct Item" } }
        };
        var vmSection = new SectionNode
        {
            Title = "Bound",
            Items = new ObservableCollection<MenuNode> { new MenuNode { Key = "b1", Title = "Bound Item" } }
        };
        var sections = new ObservableCollection<object> { direct, vmSection };
        var sidebar = MakeSections(sections);

        sidebar.Sections.Count.ShouldBe(2);
        sidebar.Sections[0].ShouldBeSameAs(direct);
        direct.Items.Count.ShouldBe(1);
        sidebar.Sections[1].Header.ShouldBe("Bound");
        sidebar.Sections[1].Items[0].Label.ShouldBe("Bound Item");

        // Removing the direct section removes it from Sidebar.Sections without clearing its own Items.
        sections.Remove(direct);
        sidebar.Sections.Count.ShouldBe(1);
        direct.Items.Count.ShouldBe(1);
    }

    [AvaloniaFact]
    public void ItemsSource_SectionsSource_and_explicit_Sections_coexist_in_order_and_clear_independently()
    {
        var explicitSection = new SidebarSection
        {
            Header = "Explicit",
            Items = { new SidebarItem { Key = "exp", Label = "Explicit Page" } }
        };
        var flatNode = new MenuNode { Key = "flat", Title = "Flat" };
        var flatItems = new ObservableCollection<MenuNode> { flatNode };
        var groupedNode = new MenuNode { Key = "grp", Title = "Grouped" };
        var sectionVm = new SectionNode
        {
            Title = "Bound Section",
            Items = new ObservableCollection<MenuNode> { groupedNode }
        };
        var boundSections = new ObservableCollection<SectionNode> { sectionVm };

        var sidebar = new Sidebar
        {
            SectionHeaderMember = nameof(SectionNode.Title),
            SectionItemsMember = nameof(SectionNode.Items),
            KeyMember = nameof(MenuNode.Key),
            LabelMember = nameof(MenuNode.Title)
        };
        sidebar.Sections.Add(explicitSection);

        sidebar.SectionsSource = boundSections;
        sidebar.Sections.Count.ShouldBe(2);
        sidebar.Sections[0].Header.ShouldBe("Bound Section");
        sidebar.Sections[1].ShouldBeSameAs(explicitSection);

        sidebar.ItemsSource = flatItems;
        sidebar.Sections.Count.ShouldBe(3);
        sidebar.Sections[0].Header.ShouldBeNull();
        sidebar.Sections[0].Items[0].Label.ShouldBe("Flat");
        sidebar.Sections[1].Header.ShouldBe("Bound Section");
        sidebar.Sections[1].Items[0].Label.ShouldBe("Grouped");
        sidebar.Sections[2].ShouldBeSameAs(explicitSection);

        // Mutating one source preserves the other's live item instances and bindings!
        var flatItem = sidebar.Sections[0].Items[0];
        var groupedItem = sidebar.Sections[1].Items[0];
        flatItems.Add(new MenuNode { Key = "flat2", Title = "Flat 2" });
        groupedNode.Title = "Grouped Renamed";
        sidebar.Sections[1].Items[0].ShouldBeSameAs(groupedItem);
        groupedItem.Label.ShouldBe("Grouped Renamed");

        boundSections.Add(new SectionNode { Title = "Bound Section 2", Items = new ObservableCollection<MenuNode>() });
        flatNode.Title = "Flat Renamed";
        sidebar.Sections[0].Items[0].ShouldBeSameAs(flatItem);
        flatItem.Label.ShouldBe("Flat Renamed");
        sidebar.Sections.Count.ShouldBe(4);
        sidebar.Sections[3].ShouldBeSameAs(explicitSection);

        // Clearing ItemsSource leaves SectionsSource and explicit Sections intact.
        sidebar.ItemsSource = null;
        sidebar.Sections.Count.ShouldBe(3);
        sidebar.Sections[0].Header.ShouldBe("Bound Section");
        sidebar.Sections[0].Items[0].ShouldBeSameAs(groupedItem);
        sidebar.Sections[1].Header.ShouldBe("Bound Section 2");
        sidebar.Sections[2].ShouldBeSameAs(explicitSection);

        // Re-adding ItemsSource puts it back at index 0, then clearing SectionsSource leaves ItemsSource and explicit Sections intact.
        sidebar.ItemsSource = flatItems;
        sidebar.Sections.Count.ShouldBe(4);
        sidebar.Sections[0].Header.ShouldBeNull();
        sidebar.SectionsSource = null;
        sidebar.Sections.Count.ShouldBe(2);
        sidebar.Sections[0].Header.ShouldBeNull();
        sidebar.Sections[0].Items.Count.ShouldBe(2);
        sidebar.Sections[1].ShouldBeSameAs(explicitSection);
    }

    [AvaloniaFact]
    public void Rendered_bound_sections_support_header_collapse_expanded_parents_and_SelectedKey()
    {
        var childAtlas = new MenuNode { Key = "projects/atlas", Title = "Atlas" };
        var parentProjects = new MenuNode
        {
            Key = "projects",
            Title = "Projects",
            Expanded = true,
            Children = { childAtlas }
        };
        var parentHidden = new MenuNode
        {
            Key = "teams",
            Title = "Teams",
            Expanded = false,
            Children = { new MenuNode { Key = "teams/core", Title = "Core" } }
        };
        var workspace = new SectionNode
        {
            Title = "Workspace",
            Collapsible = true,
            Expanded = true,
            Items = new ObservableCollection<MenuNode> { parentProjects, parentHidden }
        };
        var sidebar = new Sidebar
        {
            SelectedKey = "projects/atlas",
            SectionsSource = new ObservableCollection<SectionNode> { workspace },
            SectionHeaderMemberBinding = Compiled<SectionNode>(nameof(SectionNode.Title), s => s.Title, typeof(string)),
            SectionItemsMemberBinding = Compiled<SectionNode>(nameof(SectionNode.Items), s => s.Items, typeof(ObservableCollection<MenuNode>)),
            SectionIsCollapsibleMemberBinding = Compiled<SectionNode>(nameof(SectionNode.Collapsible), s => s.Collapsible, typeof(bool)),
            SectionIsExpandedMemberBinding = Compiled<SectionNode>(
                nameof(SectionNode.Expanded),
                s => s.Expanded,
                typeof(bool),
                (s, v) => s.Expanded = (bool)v!,
                BindingMode.TwoWay),
            KeyMemberBinding = Compiled(nameof(MenuNode.Key), n => n.Key, typeof(string)),
            LabelMemberBinding = Compiled(nameof(MenuNode.Title), n => n.Title, typeof(string)),
            ChildrenMemberBinding = Compiled(nameof(MenuNode.Children), n => n.Children, typeof(ObservableCollection<MenuNode>)),
            IsExpandedMemberBinding = Compiled<MenuNode>(
                nameof(MenuNode.Expanded),
                n => n.Expanded,
                typeof(bool),
                (n, v) => n.Expanded = (bool)v!,
                BindingMode.TwoWay)
        };

        var window = TestHelpers.Show(new DockPanel { Children = { sidebar } }, ArcMotionLevel.Off);
        try
        {
            window.Width = 600;
            window.Height = 800;
            Dispatcher.UIThread.RunJobs();

            var section = sidebar.Sections[0];
            var header = sidebar.HeaderOf(section).ShouldNotBeNull();
            header.Title.ShouldBe("Workspace");
            header.IsInteractive.ShouldBeTrue();
            header.IsExpanded.ShouldBeTrue();

            var projectsItem = section.Items[0];
            var atlasItem = projectsItem.Items[0];
            var teamsItem = section.Items[1];

            // Projects starts expanded so Atlas is navigable; Teams starts collapsed so Core is not.
            sidebar.RowOf(projectsItem).ShouldNotBeNull().IsExpanded.ShouldBeTrue();
            sidebar.RowOf(teamsItem).ShouldNotBeNull().IsExpanded.ShouldBeFalse();
            sidebar.NavigableNodes.Select(n => n.Label).ShouldBe(["Workspace", "Projects", "Atlas", "Teams"]);

            // SelectedKey selects Atlas inside the bound section.
            sidebar.SelectedItem.ShouldBeSameAs(atlasItem);
            sidebar.MarkerRow.ShouldBeSameAs(sidebar.RowOf(atlasItem));

            // Clicking the parent row toggles IsExpanded and writes back to VM.
            ClickControl(window, sidebar.RowOf(projectsItem)!);
            projectsItem.IsExpanded.ShouldBeFalse();
            parentProjects.Expanded.ShouldBeFalse();
            sidebar.NavigableNodes.Select(n => n.Label).ShouldBe(["Workspace", "Projects", "Teams"]);

            // Clicking the collapsible section header folds the section and writes back to VM.
            ClickControl(window, header);
            section.IsExpanded.ShouldBeFalse();
            workspace.Expanded.ShouldBeFalse();
            sidebar.NavigableNodes.Select(n => n.Label).ShouldBe(["Workspace"]);

            // Clicking again expands it.
            ClickControl(window, header);
            section.IsExpanded.ShouldBeTrue();
            workspace.Expanded.ShouldBeTrue();
            sidebar.NavigableNodes.Select(n => n.Label).ShouldBe(["Workspace", "Projects", "Teams"]);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Detaching_sidebar_suspends_bound_listeners_and_reattaching_resyncs_preserving_selection()
    {
        var node = new MenuNode { Key = "home", Title = "Home" };
        var sectionVm = new SectionNode
        {
            Title = "Main",
            Collapsible = true,
            Items = new ObservableCollection<MenuNode> { node }
        };
        var sections = new ObservableCollection<SectionNode> { sectionVm };
        var sidebar = MakeSections(sections);
        sidebar.SelectedKey = "home";

        var dock = new DockPanel { Children = { sidebar } };
        var window = TestHelpers.Show(dock, ArcMotionLevel.Off);
        try
        {
            Dispatcher.UIThread.RunJobs();
            var boundSection = sidebar.Sections[0];
            var boundItem = boundSection.Items[0];
            sidebar.SelectedItem.ShouldBeSameAs(boundItem);

            // Detach: listeners are suspended so off-screen mutations do not run handlers or keep sidebar alive.
            dock.Children.Remove(sidebar);
            Dispatcher.UIThread.RunJobs();

            sectionVm.Title = "Updated Main";
            node.Title = "Updated Home";
            sectionVm.Items!.Add(new MenuNode { Key = "late", Title = "Late" });
            boundSection.Header.ShouldBe("Main");
            boundItem.Label.ShouldBe("Home");
            sidebar.SelectedKey.ShouldBe("home");

            // Re-attach: bindings and watchers resume, latest VM data is synced, and existing instances & selection stay intact.
            dock.Children.Add(sidebar);
            Dispatcher.UIThread.RunJobs();

            sidebar.Sections[0].ShouldBeSameAs(boundSection);
            sidebar.Sections[0].Items[0].ShouldBeSameAs(boundItem);
            boundSection.Header.ShouldBe("Updated Main");
            boundItem.Label.ShouldBe("Updated Home");
            boundSection.Items.Count.ShouldBe(2);
            boundSection.Items[1].Label.ShouldBe("Late");
            sidebar.SelectedItem.ShouldBeSameAs(boundItem);
            sidebar.SelectedKey.ShouldBe("home");
            sidebar.RowOf(boundItem).ShouldNotBeNull().IsSelected.ShouldBeTrue();
        }
        finally
        {
            window.Close();
        }
    }
}
