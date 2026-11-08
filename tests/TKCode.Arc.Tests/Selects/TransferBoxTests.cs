using System.Collections.ObjectModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Controls.Templates;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.VisualTree;

namespace TKCode.Arc.Tests.Selects;

public class TransferBoxTests
{
    private sealed record Member(string Name, string Role);

    private static (TransferBox Box, Window Window) Show(TransferBox box)
    {
        var window = TestHelpers.Show(box);
        window.Width = 720;
        window.Height = 420;
        Dispatcher.UIThread.RunJobs();
        return (box, window);
    }

    private static TransferBoxList List(TransferBox box, string part) => TestHelpers.Descendant<TransferBoxList>(box, part);

    private static List<TransferBoxListItem> Rows(TransferBoxList list) =>
        list.GetVisualDescendants().OfType<TransferBoxListItem>().Where(r => r.IsVisible).OrderBy(r => r.Bounds.Y).ToList();

    private static void Click(Window window, Control target, KeyModifiers modifiers = KeyModifiers.None)
    {
        var point = target.TranslatePoint(new Point(target.Bounds.Width / 2, target.Bounds.Height / 2), window)!.Value;
        var raw = modifiers switch
        {
            KeyModifiers.Control => RawInputModifiers.Control,
            KeyModifiers.Shift => RawInputModifiers.Shift,
            _ => RawInputModifiers.None
        };
        window.MouseDown(point, MouseButton.Left, raw);
        window.MouseUp(point, MouseButton.Left, raw);
        Dispatcher.UIThread.RunJobs();
    }

    private static ObservableCollection<string> Languages() => ["C#", "Java", "JavaScript", "TypeScript", "C++", "Swift"];

    [AvaloniaFact]
    public void Shows_Both_Lists_With_Headers_Counts_And_Arc_Row_Metrics()
    {
        var (box, window) = Show(new TransferBox
        {
            SourceHeader = "Languages",
            TargetHeader = "Chosen",
            SourceItems = Languages(),
            TargetItems = new ObservableCollection<string> { "Python" }
        });
        try
        {
            box.Bounds.Height.ShouldBe(320);
            var rows = Rows(List(box, TransferBox.SourceListPart));
            rows.Count.ShouldBe(6);
            rows[0].Bounds.Height.ShouldBeGreaterThanOrEqualTo(36);
            rows[0].Content.ShouldBe("C#");
            Rows(List(box, TransferBox.TargetListPart)).Single().Content.ShouldBe("Python");
            TestHelpers.Descendant<TextBlock>(box, TransferBox.SourceCountPart).Text.ShouldBe("6");
            TestHelpers.Descendant<ContentPresenter>(box, "PART_SourceHeader").Content.ShouldBe("Languages");
            box.CanMoveToTarget.ShouldBeFalse();
            box.CanMoveToSource.ShouldBeFalse();
            box.MoveToTargetCommand.CanExecute(null).ShouldBeFalse();
            TestHelpers.Descendant<Button>(box, TransferBox.MoveToTargetPart).IsEffectivelyEnabled.ShouldBeFalse();
            TestHelpers.Descendant<Button>(box, TransferBox.MoveAllToTargetPart).IsVisible.ShouldBeFalse();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Click_Ctrl_And_Shift_Select_And_The_Move_Edits_The_Bound_Collections_In_Order()
    {
        var source = Languages();
        var target = new ObservableCollection<string> { "Python", "Rust" };
        var (box, window) = Show(new TransferBox { SourceItems = source, TargetItems = target });
        TransferBoxTransferredEventArgs? args = null;
        var events = 0;
        box.ItemsTransferred += (_, e) =>
        {
            args = e;
            events++;
        };
        try
        {
            var list = List(box, TransferBox.SourceListPart);
            Click(window, Rows(list)[3]);
            Click(window, Rows(list)[0], KeyModifiers.Control);
            box.SourceSelectionCount.ShouldBe(2);
            TestHelpers.Descendant<TextBlock>(box, TransferBox.SourceCountPart).Text.ShouldBe("2/6");
            box.CanMoveToTarget.ShouldBeTrue();
            TestHelpers.Descendant<Button>(box, TransferBox.MoveToTargetPart).IsEffectivelyEnabled.ShouldBeTrue();

            Click(window, TestHelpers.Descendant<Button>(box, TransferBox.MoveToTargetPart));

            // Moved in source order (C# before TypeScript), appended to the target, the same references.
            source.ShouldBe(["Java", "JavaScript", "C++", "Swift"]);
            target.ShouldBe(["Python", "Rust", "C#", "TypeScript"]);
            events.ShouldBe(1);
            args!.Direction.ShouldBe(TransferDirection.ToTarget);
            args.Items.ShouldBe(["C#", "TypeScript"]);
            box.SourceSelectionCount.ShouldBe(0);
            box.CanMoveToTarget.ShouldBeFalse();
            Rows(list).Select(r => r.Content).ShouldBe(["Java", "JavaScript", "C++", "Swift"]);

            // Shift extends a range.
            Click(window, Rows(list)[0]);
            Click(window, Rows(list)[2], KeyModifiers.Shift);
            box.GetSelectedItems(TransferBoxSide.Source).ShouldBe(["Java", "JavaScript", "C++"]);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Moving_Back_Restores_Items_To_The_Source_And_Follows_View_Model_Edits()
    {
        var source = Languages();
        var target = new ObservableCollection<string>();
        var (box, window) = Show(new TransferBox { SourceItems = source, TargetItems = target });
        try
        {
            box.MoveAllToTarget().ShouldBe(6);
            source.ShouldBeEmpty();
            target.Count.ShouldBe(6);
            var empty = TestHelpers.Descendant<ContentPresenter>(box, TransferBox.SourceEmptyPart);
            empty.IsVisible.ShouldBeTrue();
            empty.Content.ShouldBe("No items");

            var targetList = List(box, TransferBox.TargetListPart);
            targetList.Selection.Select(1);
            targetList.Selection.Select(4);
            Dispatcher.UIThread.RunJobs();
            box.MoveToSourceCommand.CanExecute(null).ShouldBeTrue();
            box.MoveToSourceCommand.Execute(null);
            source.ShouldBe(["Java", "C++"]);
            empty.IsVisible.ShouldBeFalse();

            // The view model edits the collection: the list follows.
            source.Add("Kotlin");
            source.RemoveAt(0);
            Dispatcher.UIThread.RunJobs();
            Rows(List(box, TransferBox.SourceListPart)).Select(r => r.Content).ShouldBe(["C++", "Kotlin"]);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void A_Kept_Selection_Survives_Other_Rows_Moving()
    {
        var source = Languages();
        var (box, window) = Show(new TransferBox { SourceItems = source, TargetItems = new ObservableCollection<string>() });
        try
        {
            var list = List(box, TransferBox.SourceListPart);
            list.Selection.Select(5);
            Dispatcher.UIThread.RunJobs();
            source.RemoveAt(0);
            Dispatcher.UIThread.RunJobs();
            box.GetSelectedItems(TransferBoxSide.Source).ShouldBe(["Swift"]);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Select_All_Box_Selects_Clears_And_Shows_A_Partial_Selection()
    {
        var (box, window) = Show(new TransferBox { SourceItems = Languages(), TargetItems = new ObservableCollection<string>() });
        try
        {
            var selectAll = TestHelpers.Descendant<Checkbox>(box, TransferBox.SourceSelectAllPart);
            selectAll.IsVisible.ShouldBeTrue();
            selectAll.IsChecked.ShouldBe(false);
            TestHelpers.Descendant<Checkbox>(box, TransferBox.TargetSelectAllPart).IsEnabled.ShouldBeFalse();

            Click(window, selectAll);
            box.SourceSelectionCount.ShouldBe(6);
            selectAll.IsChecked.ShouldBe(true);

            List(box, TransferBox.SourceListPart).Selection.Deselect(2);
            Dispatcher.UIThread.RunJobs();
            selectAll.IsChecked.ShouldBeNull();

            Click(window, selectAll);
            box.SourceSelectionCount.ShouldBe(6);
            Click(window, selectAll);
            box.SourceSelectionCount.ShouldBe(0);
            selectAll.IsChecked.ShouldBe(false);

            box.SelectionMode = SelectionMode.Single;
            selectAll.IsVisible.ShouldBeFalse();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Ctrl_A_Selects_The_List_And_Enter_Moves_It()
    {
        var source = Languages();
        var target = new ObservableCollection<string>();
        var (box, window) = Show(new TransferBox { SourceItems = source, TargetItems = target });
        try
        {
            var list = List(box, TransferBox.SourceListPart);
            Click(window, Rows(list)[0]);
            window.KeyPressQwerty(PhysicalKey.A, RawInputModifiers.Control);
            Dispatcher.UIThread.RunJobs();
            box.SourceSelectionCount.ShouldBe(6);
            window.KeyPressQwerty(PhysicalKey.Enter, RawInputModifiers.None);
            Dispatcher.UIThread.RunJobs();
            source.ShouldBeEmpty();
            target.Count.ShouldBe(6);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Double_Click_Moves_The_Row()
    {
        var source = Languages();
        var target = new ObservableCollection<string>();
        var (box, window) = Show(new TransferBox { SourceItems = source, TargetItems = target });
        try
        {
            var row = Rows(List(box, TransferBox.SourceListPart))[2];
            var point = row.TranslatePoint(new Point(10, row.Bounds.Height / 2), window)!.Value;
            window.MouseDown(point, MouseButton.Left);
            window.MouseUp(point, MouseButton.Left);
            window.MouseDown(point, MouseButton.Left);
            window.MouseUp(point, MouseButton.Left);
            Dispatcher.UIThread.RunJobs();
            target.ShouldBe(["JavaScript"]);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Search_Filters_Rows_And_Move_All_Takes_Only_The_Shown_Ones()
    {
        var source = Languages();
        var target = new ObservableCollection<string>();
        var (box, window) = Show(new TransferBox
        {
            SourceItems = source,
            TargetItems = target,
            IsSearchable = true,
            IsMoveAllVisible = true
        });
        try
        {
            TestHelpers.Descendant<SearchField>(box, "PART_SourceSearch").IsVisible.ShouldBeTrue();
            TestHelpers.Descendant<Button>(box, TransferBox.MoveAllToTargetPart).IsVisible.ShouldBeTrue();
            box.SourceSearchText = "script";
            Dispatcher.UIThread.RunJobs();
            var list = List(box, TransferBox.SourceListPart);
            Rows(list).Select(r => r.Content).ShouldBe(["JavaScript", "TypeScript"]);
            TestHelpers.Descendant<TextBlock>(box, TransferBox.SourceCountPart).Text.ShouldBe("2");

            Click(window, TestHelpers.Descendant<Button>(box, TransferBox.MoveAllToTargetPart));
            target.ShouldBe(["JavaScript", "TypeScript"]);
            source.ShouldBe(["C#", "Java", "C++", "Swift"]);

            var empty = TestHelpers.Descendant<ContentPresenter>(box, TransferBox.SourceEmptyPart);
            empty.IsVisible.ShouldBeTrue();
            empty.Content.ShouldBe("No matches");

            // Typing in the field drives the same filter.
            TestHelpers.Descendant<SearchField>(box, "PART_SourceSearch").Text = "";
            Dispatcher.UIThread.RunJobs();
            box.SourceSearchText.ShouldBe("");
            Rows(list).Count.ShouldBe(4);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Objects_Keep_Their_References_And_Use_The_Text_Selector_And_Item_Template()
    {
        var ada = new Member("Ada", "Engineer");
        var grace = new Member("Grace", "Admiral");
        var source = new ObservableCollection<Member> { ada, grace };
        var target = new ObservableCollection<Member>();
        var (box, window) = Show(new TransferBox
        {
            SourceItems = source,
            TargetItems = target,
            ItemTextSelector = item => ((Member)item!).Name,
            IsSearchable = true
        });
        try
        {
            Rows(List(box, TransferBox.SourceListPart)).Select(r => r.Content).ShouldBe(["Ada", "Grace"]);
            box.SourceSearchText = "gra";
            Dispatcher.UIThread.RunJobs();
            box.MoveAllToTarget();
            target.Single().ShouldBeSameAs(grace);
            source.Single().ShouldBeSameAs(ada);

            box.ItemTemplate = new FuncDataTemplate<Member>((m, _) => new TextBlock { Text = m?.Role });
            Dispatcher.UIThread.RunJobs();
            var row = Rows(List(box, TransferBox.TargetListPart)).Single();
            row.GetVisualDescendants().OfType<TextBlock>().Any(t => t.Text == "Admiral").ShouldBeTrue();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Duplicates_Move_By_Position_And_Not_By_Value()
    {
        var source = new ObservableCollection<string> { "A", "B", "A", "C" };
        var target = new ObservableCollection<string>();
        var (box, window) = Show(new TransferBox { SourceItems = source, TargetItems = target });
        try
        {
            List(box, TransferBox.SourceListPart).Selection.Select(2);
            Dispatcher.UIThread.RunJobs();
            box.MoveToTarget().ShouldBe(1);
            source.ShouldBe(["A", "B", "C"]);
            target.ShouldBe(["A"]);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Read_Only_Missing_Or_Shared_Collections_Disable_Moving_And_Throw_On_A_Direct_Call()
    {
        var shared = Languages();
        var (box, window) = Show(new TransferBox { SourceItems = new ReadOnlyObservableCollection<string>(Languages()), TargetItems = new ObservableCollection<string>() });
        try
        {
            box.SelectAll(TransferBoxSide.Source);
            Dispatcher.UIThread.RunJobs();
            box.SourceSelectionCount.ShouldBe(6);
            box.CanMoveToTarget.ShouldBeFalse();
            box.MoveAllToTargetCommand.CanExecute(null).ShouldBeFalse();
            Should.Throw<InvalidOperationException>(() => box.MoveToTarget());

            box.SourceItems = shared;
            box.TargetItems = shared;
            Should.Throw<InvalidOperationException>(() => box.MoveAllToTarget());

            box.TargetItems = null;
            box.MoveAllToTargetCommand.CanExecute(null).ShouldBeFalse();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void A_Failed_Add_Leaves_Both_Collections_As_They_Were()
    {
        var source = new ObservableCollection<object> { "one", 2, "three" };
        var target = new ObservableCollection<string> { "zero" };
        var (box, window) = Show(new TransferBox { SourceItems = source, TargetItems = target });
        try
        {
            // The int cannot join a collection of strings: nothing is lost or half moved.
            Should.Throw<ArgumentException>(() => box.MoveAllToTarget());
            source.ShouldBe(["one", 2, "three"]);
            target.ShouldBe(["zero"]);
            Dispatcher.UIThread.RunJobs();
            Rows(List(box, TransferBox.SourceListPart)).Count.ShouldBe(3);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Lists_Without_Notifications_Are_Followed_After_A_Move_And_Disabled_Box_Cannot_Move()
    {
        var source = new List<string> { "a", "b" };
        var target = new List<string>();
        var (box, window) = Show(new TransferBox { SourceItems = source, TargetItems = target });
        try
        {
            box.MoveAllToTarget().ShouldBe(2);
            Dispatcher.UIThread.RunJobs();
            Rows(List(box, TransferBox.TargetListPart)).Count.ShouldBe(2);
            Rows(List(box, TransferBox.SourceListPart)).ShouldBeEmpty();

            box.IsEnabled = false;
            box.SelectAll(TransferBoxSide.Target);
            Dispatcher.UIThread.RunJobs();
            box.MoveAllToSourceCommand.CanExecute(null).ShouldBeFalse();
            box.CanMoveToSource.ShouldBeFalse();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Custom_Empty_Content_And_Accessible_Names()
    {
        var (box, window) = Show(new TransferBox
        {
            SourceHeader = "Permissions",
            TargetEmptyContent = "Nothing granted yet",
            SourceItems = new ObservableCollection<string>(),
            TargetItems = new ObservableCollection<string>()
        });
        try
        {
            TestHelpers.Descendant<ContentPresenter>(box, TransferBox.TargetEmptyPart).Content.ShouldBe("Nothing granted yet");
            Avalonia.Automation.AutomationProperties.GetName(List(box, TransferBox.SourceListPart)).ShouldBe("Permissions");
            Avalonia.Automation.AutomationProperties.GetName(TestHelpers.Descendant<Checkbox>(box, TransferBox.SourceSelectAllPart))
                .ShouldBe("Select all: Permissions");
            Avalonia.Automation.AutomationProperties.GetName(TestHelpers.Descendant<Button>(box, TransferBox.MoveToTargetPart))
                .ShouldBe("Move to selected");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Does_Not_Keep_A_Subscription_To_A_Long_Lived_Collection()
    {
        var source = Languages();
        var reference = TestHelpers.ShowAndClose(() => new TransferBox { SourceItems = source, TargetItems = new ObservableCollection<string>() });
        TestHelpers.Collect(reference);
        reference.IsAlive.ShouldBeFalse();
        GC.KeepAlive(source);
    }
}
