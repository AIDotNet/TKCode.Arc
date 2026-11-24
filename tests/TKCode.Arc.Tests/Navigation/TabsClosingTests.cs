using System.Collections.ObjectModel;
using System.Windows.Input;
using TKCode.Arc.Primitives;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;

namespace TKCode.Arc.Tests.Navigation;

/// <summary>Card variant, closable tabs and the add button (#187).</summary>
public class TabsClosingTests
{
    private static Tabs Inline(params string[] headers)
    {
        var tabs = new Tabs();
        foreach (var header in headers)
        {
            tabs.Items.Add(new TabItem { Header = header, Content = new TextBlock { Text = header + " panel" } });
        }
        return tabs;
    }

    private static TabItem Tab(Tabs tabs, int index) => (TabItem)tabs.ContainerFromIndex(index)!;

    private static Point Center(Visual visual, Visual relativeTo)
        => visual.TranslatePoint(new Point(visual.Bounds.Width / 2, visual.Bounds.Height / 2), relativeTo)!.Value;

    private static void Click(Window window, Point point, MouseButton button = MouseButton.Left)
    {
        window.MouseDown(point, button);
        window.MouseUp(point, button);
        Dispatcher.UIThread.RunJobs();
    }

    [AvaloniaFact]
    public void Defaults_Keep_The_Pill_Look_And_Build_No_Close_Or_Add_Buttons()
    {
        var tabs = Inline("One", "Two");
        var window = TestHelpers.Show(tabs);
        try
        {
            tabs.Variant.ShouldBe(TabsVariant.Pill);
            tabs.AreTabsClosable.ShouldBeFalse();
            tabs.IsAddButtonVisible.ShouldBeFalse();
            Tab(tabs, 0).IsClosable.ShouldBeFalse();
            tabs.Classes.Contains(":card").ShouldBeFalse();
            Tab(tabs, 0).Classes.Contains(":closable").ShouldBeFalse();
            tabs.GetVisualDescendants().OfType<Avalonia.Controls.Button>().ShouldBeEmpty();
            var shell = TestHelpers.Descendant<Border>(tabs, "PART_ListShell");
            shell.HorizontalAlignment.ShouldBe(Avalonia.Layout.HorizontalAlignment.Left);
            TestHelpers.Descendant<Border>(tabs, "PART_Baseline").IsVisible.ShouldBeFalse();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Card_Variant_Stretches_The_Strip_And_Opens_The_Selected_Card_Onto_The_Baseline()
    {
        var tabs = Inline("One", "Two", "Three");
        tabs.Variant = TabsVariant.Card;
        tabs.Width = 600;
        var window = TestHelpers.Show(tabs);
        try
        {
            Dispatcher.UIThread.RunJobs();
            tabs.Classes.Contains(":card").ShouldBeTrue();
            Tab(tabs, 1).Classes.Contains(":card").ShouldBeTrue();
            var shell = TestHelpers.Descendant<Border>(tabs, "PART_ListShell");
            shell.Bounds.Width.ShouldBe(600, 0.5);
            var baseline = TestHelpers.Descendant<Border>(tabs, "PART_Baseline");
            baseline.IsVisible.ShouldBeTrue();
            baseline.Bounds.Width.ShouldBe(600, 0.5);
            baseline.Bounds.Height.ShouldBe(1, 0.01);

            var pill = TestHelpers.Descendant<GlidingIndicator>(tabs);
            pill.CornerRadius.BottomLeft.ShouldBe(0);
            pill.CornerRadius.TopLeft.ShouldBeGreaterThan(0);
            var viewport = TestHelpers.Descendant<ScrollViewer>(tabs);
            // The selected card reaches past the bottom of the list, over the baseline (clipped there, so it reads as open).
            var bottom = pill.TranslatePoint(new Point(0, pill.CurrentRect.Bottom), viewport)!.Value.Y;
            bottom.ShouldBeGreaterThan(viewport.Bounds.Height);
            // A card that is not selected stops above the baseline, so its hover fill never covers it.
            var tabBottom = Tab(tabs, 1).TranslatePoint(new Point(0, Tab(tabs, 1).Bounds.Height), viewport)!.Value.Y;
            tabBottom.ShouldBe(viewport.Bounds.Height - 1, 0.5);

            tabs.Variant = TabsVariant.Pill;
            Dispatcher.UIThread.RunJobs();
            Tab(tabs, 1).Classes.Contains(":card").ShouldBeFalse();
            shell.HorizontalAlignment.ShouldBe(Avalonia.Layout.HorizontalAlignment.Left);
            pill.CornerRadius.BottomLeft.ShouldBeGreaterThan(0);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Closable_Tab_Builds_Its_Close_Button_And_Shows_It_On_The_Selected_Tab()
    {
        var tabs = Inline("One", "Two", "Three");
        ((TabItem)tabs.Items[0]!).IsClosable = true;
        ((TabItem)tabs.Items[1]!).IsClosable = true;
        var window = TestHelpers.Show(tabs);
        try
        {
            Tab(tabs, 0).Classes.Contains(":closable").ShouldBeTrue();
            Tab(tabs, 0).CloseButton.ShouldNotBeNull();
            Tab(tabs, 0).CloseButton!.IsVisible.ShouldBeTrue();
            Tab(tabs, 1).CloseButton.ShouldNotBeNull();
            // Not selected and not hovered: the slot keeps its room, the button hides.
            Tab(tabs, 1).CloseButton!.IsVisible.ShouldBeFalse();
            ((Control)Tab(tabs, 1).CloseButton!.Parent!).Bounds.Width.ShouldBeGreaterThan(0);
            Tab(tabs, 2).CloseButton.ShouldBeNull();
            Tab(tabs, 0).CloseButton!.Focusable.ShouldBeFalse();
            Avalonia.Automation.AutomationProperties.GetName(Tab(tabs, 0).CloseButton!).ShouldBe("Close tab");

            tabs.SelectedIndex = 1;
            Dispatcher.UIThread.RunJobs();
            Tab(tabs, 1).CloseButton!.IsVisible.ShouldBeTrue();
            Tab(tabs, 0).CloseButton!.IsVisible.ShouldBeFalse();

            ((TabItem)tabs.Items[1]!).IsClosable = false;
            Dispatcher.UIThread.RunJobs();
            Tab(tabs, 1).Classes.Contains(":closable").ShouldBeFalse();
            ((Control)Tab(tabs, 1).CloseButton!.Parent!).IsVisible.ShouldBeFalse();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Clicking_Close_Raises_The_Request_Without_Selecting_And_The_App_Removes_The_Tab()
    {
        var tabs = Inline("One", "Two", "Three");
        tabs.AreTabsClosable = true;
        TabCloseRequestedEventArgs? request = null;
        tabs.TabCloseRequested += (_, e) =>
        {
            request = e;
            tabs.Items.Remove(e.Item);
        };
        var window = TestHelpers.Show(tabs, Motion.ArcMotionLevel.Off);
        try
        {
            var third = Tab(tabs, 2);
            var point = Center(third, window);
            window.MouseMove(point);
            Dispatcher.UIThread.RunJobs();
            third.CloseButton!.IsVisible.ShouldBeTrue();
            Click(window, Center(third.CloseButton!, window));

            request.ShouldNotBeNull();
            request!.Index.ShouldBe(2);
            request.Item.ShouldBe(third);
            request.Container.ShouldBe(third);
            tabs.ItemCount.ShouldBe(2);
            tabs.SelectedIndex.ShouldBe(0);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Closing_The_Selected_Tab_Selects_The_Next_One_Else_The_Previous()
    {
        var source = new ObservableCollection<string> { "A", "B", "C", "D" };
        var tabs = new Tabs { ItemsSource = source, AreTabsClosable = true };
        tabs.TabCloseRequested += (_, e) => source.Remove((string)e.Item!);
        var window = TestHelpers.Show(tabs);
        try
        {
            tabs.SelectedItem = "B";
            Dispatcher.UIThread.RunJobs();
            var added = new List<object?>();
            tabs.SelectionChanged += (_, e) => added.AddRange(e.AddedItems.Cast<object?>());

            Click(window, Center(Tab(tabs, 1).CloseButton!, window));
            tabs.SelectedItem.ShouldBe("C");
            tabs.SelectedIndex.ShouldBe(1);
            // Straight to the neighbour: the stock rule never selects the first tab on the way.
            added.ShouldBe(new object?[] { "C" });
            Tab(tabs, 1).IsSelected.ShouldBeTrue();
            TestHelpers.Descendant<TabsPanelHost>(tabs).CurrentPresenter.Content.ShouldBe("C");

            tabs.SelectedItem = "D";
            Dispatcher.UIThread.RunJobs();
            Click(window, Center(Tab(tabs, 2).CloseButton!, window));
            tabs.SelectedItem.ShouldBe("C");

            // Removing another tab keeps the selection where it is.
            source.Remove("A");
            Dispatcher.UIThread.RunJobs();
            tabs.SelectedItem.ShouldBe("C");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Removing_The_Selected_Tab_Skips_A_Disabled_Neighbour()
    {
        var tabs = Inline("One", "Two", "Three", "Four");
        ((TabItem)tabs.Items[2]!).IsEnabled = false;
        var window = TestHelpers.Show(tabs);
        try
        {
            tabs.SelectedIndex = 1;
            Dispatcher.UIThread.RunJobs();
            tabs.Items.RemoveAt(1);
            Dispatcher.UIThread.RunJobs();
            // "Three" took the place but is disabled; "Four" is the next selectable tab.
            ((TabItem)tabs.SelectedItem!).Header.ShouldBe("Four");
            // The AlwaysSelected rule is back: removing the selected tab again still selects a neighbour.
            tabs.Items.RemoveAt(2);
            Dispatcher.UIThread.RunJobs();
            tabs.SelectedIndex.ShouldBe(0);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Delete_Closes_The_Focused_Tab_And_Focus_Moves_To_The_New_Selection()
    {
        var tabs = Inline("One", "Two", "Three");
        tabs.AreTabsClosable = true;
        var requests = 0;
        tabs.TabCloseRequested += (_, e) =>
        {
            requests++;
            tabs.Items.Remove(e.Item);
        };
        var window = TestHelpers.Show(tabs);
        try
        {
            tabs.SelectedIndex = 1;
            Dispatcher.UIThread.RunJobs();
            Tab(tabs, 1).Focus(NavigationMethod.Tab);
            window.KeyPressQwerty(PhysicalKey.Delete, RawInputModifiers.None);
            Dispatcher.UIThread.RunJobs();
            requests.ShouldBe(1);
            ((TabItem)tabs.SelectedItem!).Header.ShouldBe("Three");
            Tab(tabs, 1).IsFocused.ShouldBeTrue();
            Tab(tabs, 1).IsTabStop.ShouldBeTrue();

            // A tab that is not closable ignores Delete.
            ((TabItem)tabs.Items[0]!).IsClosable = false;
            tabs.AreTabsClosable = false;
            Tab(tabs, 0).Focus(NavigationMethod.Tab);
            window.KeyPressQwerty(PhysicalKey.Delete, RawInputModifiers.None);
            Dispatcher.UIThread.RunJobs();
            requests.ShouldBe(1);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Middle_Click_Closes_A_Closable_Tab_Only()
    {
        var tabs = Inline("One", "Two", "Three");
        ((TabItem)tabs.Items[2]!).IsClosable = true;
        var closed = new List<int>();
        tabs.TabCloseRequested += (_, e) => closed.Add(e.Index);
        var window = TestHelpers.Show(tabs);
        try
        {
            Click(window, Center(Tab(tabs, 1), window), MouseButton.Middle);
            Click(window, Center(Tab(tabs, 2), window), MouseButton.Middle);
            closed.ShouldBe(new[] { 2 });
            tabs.SelectedIndex.ShouldBe(0);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Close_Command_Runs_With_The_Item_Unless_The_Request_Was_Handled()
    {
        var source = new ObservableCollection<string> { "A", "B" };
        var command = new RecordingCommand();
        var tabs = new Tabs { ItemsSource = source, AreTabsClosable = true, CloseTabCommand = command };
        var handle = false;
        tabs.TabCloseRequested += (_, e) => e.Handled = handle;
        var window = TestHelpers.Show(tabs);
        try
        {
            Click(window, Center(Tab(tabs, 0).CloseButton!, window));
            command.Parameters.ShouldBe(new object?[] { "A" });
            handle = true;
            Click(window, Center(Tab(tabs, 0).CloseButton!, window));
            command.Parameters.Count.ShouldBe(1);
            // The tabs never remove an item themselves.
            source.Count.ShouldBe(2);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Add_Button_Follows_The_Last_Tab_And_Raises_The_Request()
    {
        var tabs = Inline("One", "Two");
        var command = new RecordingCommand();
        tabs.AddTabCommand = command;
        var requests = 0;
        tabs.TabAddRequested += (_, _) =>
        {
            requests++;
            tabs.Items.Add(new TabItem { Header = "New" });
        };
        var window = TestHelpers.Show(tabs);
        try
        {
            tabs.IsAddButtonVisible = true;
            Dispatcher.UIThread.RunJobs();
            var add = tabs.GetVisualDescendants().OfType<Avalonia.Controls.Button>().Single(b => b.Name == Tabs.AddButtonPart);
            add.IsVisible.ShouldBeTrue();
            add.Focusable.ShouldBeTrue();
            Avalonia.Automation.AutomationProperties.GetName(add).ShouldBe("Add tab");
            var viewport = TestHelpers.Descendant<ScrollViewer>(tabs);
            var addLeft = add.TranslatePoint(default, tabs)!.Value.X;
            addLeft.ShouldBeGreaterThanOrEqualTo(viewport.TranslatePoint(new Point(viewport.Bounds.Width, 0), tabs)!.Value.X - 0.5);

            Click(window, Center(add, window));
            requests.ShouldBe(1);
            command.Parameters.Count.ShouldBe(1);
            tabs.ItemCount.ShouldBe(3);

            tabs.IsAddButtonVisible = false;
            Dispatcher.UIThread.RunJobs();
            add.IsVisible.ShouldBeFalse();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaTheory]
    [InlineData(TabsVariant.Pill)]
    [InlineData(TabsVariant.Card)]
    public void Add_Button_Stays_In_View_Next_To_The_Scroll_Buttons_When_The_List_Overflows(TabsVariant variant)
    {
        var tabs = Inline("Inbox", "Drafts", "Sent", "Scheduled", "Spam", "Trash", "Archive", "Starred", "Snoozed");
        tabs.Variant = variant;
        tabs.AreTabsClosable = true;
        tabs.IsAddButtonVisible = true;
        tabs.Width = 420;
        var window = TestHelpers.Show(tabs);
        try
        {
            Dispatcher.UIThread.RunJobs();
            tabs.Classes.Contains(":overflow").ShouldBeTrue();
            var buttons = tabs.GetVisualDescendants().OfType<Avalonia.Controls.Button>().ToList();
            var add = buttons.Single(b => b.Name == Tabs.AddButtonPart);
            var right = buttons.Single(b => b.Name == Tabs.ScrollRightPart);
            right.IsVisible.ShouldBeTrue();
            var addBox = new Rect(add.TranslatePoint(default, tabs)!.Value, add.Bounds.Size);
            var rightBox = new Rect(right.TranslatePoint(default, tabs)!.Value, right.Bounds.Size);
            addBox.Right.ShouldBeLessThanOrEqualTo(tabs.Bounds.Width + 0.5);
            addBox.Width.ShouldBeGreaterThan(0);
            rightBox.Right.ShouldBeLessThanOrEqualTo(addBox.Left + 0.5);

            tabs.SelectedIndex = 8;
            Dispatcher.UIThread.RunJobs();
            TestHelpers.Descendant<ScrollViewer>(tabs).Offset.X.ShouldBeGreaterThan(0);
            add.TranslatePoint(default, tabs)!.Value.X.ShouldBe(addBox.Left, 0.5);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void A_Selected_Tab_Is_Revealed_On_Mount_And_An_Added_One_Once_It_Is_Laid_Out()
    {
        var tabs = Inline("Inbox", "Drafts", "Sent", "Scheduled", "Spam", "Trash", "Archive", "Starred", "Snoozed");
        tabs.Width = 420;
        tabs.SelectedIndex = 8;
        tabs.IsAddButtonVisible = true;
        tabs.TabAddRequested += (_, _) =>
        {
            tabs.Items.Add(new TabItem { Header = "Untitled" });
            tabs.SelectedIndex = tabs.ItemCount - 1;
        };
        var window = TestHelpers.Show(tabs);
        try
        {
            Dispatcher.UIThread.RunJobs();
            var viewport = TestHelpers.Descendant<ScrollViewer>(tabs);
            var end = viewport.Offset.X;
            end.ShouldBeGreaterThan(0);

            tabs.SelectedIndex = 0;
            Dispatcher.UIThread.RunJobs();
            viewport.Offset.X.ShouldBe(0, 0.5);

            var add = tabs.GetVisualDescendants().OfType<Avalonia.Controls.Button>().Single(b => b.Name == Tabs.AddButtonPart);
            Click(window, Center(add, window));
            tabs.SelectedIndex.ShouldBe(9);
            viewport.Offset.X.ShouldBeGreaterThan(end);
            var added = Tab(tabs, 9);
            (added.TranslatePoint(new Point(added.Bounds.Width, 0), viewport)!.Value.X).ShouldBeLessThanOrEqualTo(viewport.Bounds.Width + 0.5);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Arrow_Keys_Skip_Hidden_Tabs()
    {
        var tabs = Inline("One", "Two", "Three");
        ((TabItem)tabs.Items[1]!).IsVisible = false;
        var window = TestHelpers.Show(tabs);
        try
        {
            Tab(tabs, 0).Focus(NavigationMethod.Tab);
            window.KeyPressQwerty(PhysicalKey.ArrowRight, RawInputModifiers.None);
            Dispatcher.UIThread.RunJobs();
            tabs.SelectedIndex.ShouldBe(2);
        }
        finally
        {
            window.Close();
        }
    }

    private sealed class RecordingCommand : ICommand
    {
        public List<object?> Parameters { get; } = new();

        public event EventHandler? CanExecuteChanged
        {
            add { }
            remove { }
        }

        public bool CanExecute(object? parameter) => true;

        public void Execute(object? parameter) => Parameters.Add(parameter);
    }
}
