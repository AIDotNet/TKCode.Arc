using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.VisualTree;
using TKCode.Arc.Motion;

namespace TKCode.Arc.Tests.Blocks.Content;

public class FaqSectionTests
{
    private static Window Show(Control control, double width = 1100)
    {
        var window = TestHelpers.Show(control);
        window.Width = width;
        window.Height = 900;
        Dispatcher.UIThread.RunJobs();
        return window;
    }

    private static FaqSection Section(FaqSectionVariant variant = FaqSectionVariant.Accordion)
    {
        var section = new FaqSection { Variant = variant };
        section.Items.Add(new FaqItem { Id = "a", Question = "Can I cancel?", Answer = "Yes, whenever you like.", Category = "Billing" });
        section.Items.Add(new FaqItem { Id = "b", Question = "Do you refund?", Answer = "Within 30 days of the café order.", Category = "Billing" });
        section.Items.Add(new FaqItem { Id = "c", Question = "Is it offline?", Answer = "The desktop app works offline.", Category = "Technical" });
        return section;
    }

    private static void Press(Window window, FaqTrigger trigger)
    {
        trigger.Focus();
        window.KeyPressQwerty(PhysicalKey.Enter, RawInputModifiers.None);
        window.KeyReleaseQwerty(PhysicalKey.Enter, RawInputModifiers.None);
        Dispatcher.UIThread.RunJobs();
    }

    [AvaloniaFact]
    public void Empty_Items_Show_The_Samples_And_The_First_Question_Is_Open()
    {
        var section = new FaqSection();
        Show(section);
        section.Rows.Count.ShouldBeGreaterThan(0);
        section.Rows[0].IsOpen.ShouldBeTrue();
        section.Rows.Skip(1).ShouldAllBe(r => !r.IsOpen);
        section.Title.ShouldBe("Frequently asked questions");
    }

    [AvaloniaFact]
    public void Opening_Another_Question_Closes_The_First_Unless_Multiple()
    {
        var section = Section();
        var window = Show(section);
        Press(window, section.Rows[1].Trigger!);
        section.OpenItems.ShouldBe(["b"]);
        section.Rows[0].IsOpen.ShouldBeFalse();
        section.Rows[1].IsOpen.ShouldBeTrue();

        section.IsMultiple = true;
        Press(window, section.Rows[0].Trigger!);
        section.OpenItems.ShouldBe(["b", "a"]);
        section.Rows[0].IsOpen.ShouldBeTrue();
        section.Rows[1].IsOpen.ShouldBeTrue();

        Press(window, section.Rows[0].Trigger!);
        section.OpenItems.ShouldBe(["b"]);
    }

    [AvaloniaFact]
    public void Setting_Open_Items_Opens_Those_Rows()
    {
        var section = Section();
        Show(section);
        section.OpenItems = ["c"];
        section.Rows[2].IsOpen.ShouldBeTrue();
        section.Rows[0].IsOpen.ShouldBeFalse();
    }

    [AvaloniaFact]
    public void An_Id_Defaults_To_The_Question()
    {
        var section = new FaqSection();
        section.Items.Add(new FaqItem { Question = "Why?", Answer = "Because." });
        Show(section);
        section.OpenItems.ShouldBe(["Why?"]);
    }

    [AvaloniaFact]
    public void Arrow_Keys_Move_Between_Questions_And_Wrap()
    {
        var section = Section();
        var window = Show(section);
        section.Rows[0].Trigger!.Focus();
        window.KeyPressQwerty(PhysicalKey.ArrowDown, RawInputModifiers.None);
        Dispatcher.UIThread.RunJobs();
        section.Rows[1].Trigger!.IsFocused.ShouldBeTrue();
        window.KeyPressQwerty(PhysicalKey.End, RawInputModifiers.None);
        Dispatcher.UIThread.RunJobs();
        section.Rows[2].Trigger!.IsFocused.ShouldBeTrue();
        window.KeyPressQwerty(PhysicalKey.ArrowDown, RawInputModifiers.None);
        Dispatcher.UIThread.RunJobs();
        section.Rows[0].Trigger!.IsFocused.ShouldBeTrue();
        window.KeyPressQwerty(PhysicalKey.ArrowUp, RawInputModifiers.None);
        Dispatcher.UIThread.RunJobs();
        section.Rows[2].Trigger!.IsFocused.ShouldBeTrue();
        window.KeyPressQwerty(PhysicalKey.Home, RawInputModifiers.None);
        Dispatcher.UIThread.RunJobs();
        section.Rows[0].Trigger!.IsFocused.ShouldBeTrue();
    }

    [AvaloniaFact]
    public void Columns_Lists_Topics_With_Counts_And_Shows_The_Chosen_One()
    {
        var section = Section(FaqSectionVariant.Columns);
        var window = Show(section);
        section.CategoryButtons.Select(b => b.Topic).ShouldBe(["Billing", "Technical"]);
        section.CategoryButtons.Select(b => b.Count).ShouldBe([2, 1]);
        section.ActiveCategory.ShouldBe("Billing");
        section.CategoryButtons[0].IsActive.ShouldBeTrue();
        section.Rows.Select(r => r.Item.Id).ShouldBe(["a", "b"]);

        section.CategoryButtons[1].Focus();
        window.KeyPressQwerty(PhysicalKey.Enter, RawInputModifiers.None);
        window.KeyReleaseQwerty(PhysicalKey.Enter, RawInputModifiers.None);
        Dispatcher.UIThread.RunJobs();
        section.SelectedCategory.ShouldBe("Technical");
        section.CategoryButtons[1].IsActive.ShouldBeTrue();
        section.Stage!.Current.ShouldNotBeNull();
        section.Stage.LeavingCount.ShouldBe(0);
        section.Rows.Select(r => r.Item.Id).ShouldBe(["a", "b", "c"]);
    }

    [AvaloniaFact]
    public void Columns_Stack_At_760()
    {
        var section = Section(FaqSectionVariant.Columns);
        var window = Show(section, 1100);
        section.IsStacked.ShouldBeFalse();
        window.Width = 600;
        Dispatcher.UIThread.RunJobs();
        section.IsStacked.ShouldBeTrue();
        section.Classes.Contains(FaqSection.NarrowPseudoClass).ShouldBeTrue();
    }

    [AvaloniaFact]
    public void Search_Filters_Counts_And_Ignores_Case_And_Accents()
    {
        var section = Section(FaqSectionVariant.Search);
        Show(section);
        section.CountLine!.Text.ShouldBe("3 questions");

        section.Query = "CAFE";
        section.Rows.Select(r => r.IsPresent).ShouldBe([false, true, false]);
        section.CountLine.Text.ShouldBe("1 answer");
        section.IsEmptyShown.ShouldBeFalse();

        section.Query = "the";
        section.CountLine.Text.ShouldBe("2 answers");
    }

    [AvaloniaFact]
    public void Search_Shows_An_Excerpt_For_A_Closed_Row_That_Matches_In_Its_Answer()
    {
        var section = Section(FaqSectionVariant.Search);
        Show(section);
        section.Query = "offline";
        section.Rows[2].HasHint.ShouldBeFalse();
        section.Query = "desktop";
        section.Rows[2].HasHint.ShouldBeTrue();
        section.Rows[2].Hint!.ShouldContain("desktop");
        section.OpenItems = ["c"];
        section.Rows[2].HasHint.ShouldBeFalse();
    }

    [AvaloniaFact]
    public void Search_With_No_Result_Offers_To_Clear()
    {
        var section = Section(FaqSectionVariant.Search);
        Show(section);
        section.Query = "zebra";
        section.IsEmptyShown.ShouldBeTrue();
        section.EmptyLine!.Text.ShouldBe("No answers mention “zebra”.");
        section.ClearSearch();
        section.Query.ShouldBe(string.Empty);
        section.IsEmptyShown.ShouldBeFalse();
        section.Rows.ShouldAllBe(r => r.IsPresent);
    }

    [AvaloniaFact]
    public void The_Field_And_The_Query_Stay_In_Step()
    {
        var section = Section(FaqSectionVariant.Search);
        Show(section);
        section.SearchBox!.Text = "cancel";
        Dispatcher.UIThread.RunJobs();
        section.Query.ShouldBe("cancel");
        section.Query = "refund";
        section.SearchBox.Text.ShouldBe("refund");
    }

    [Fact]
    public void Matches_Map_Back_To_The_Original_Text()
    {
        FaqText.Matches("Café au lait", "cafe").ShouldBe([(0, 4)]);
        FaqText.Matches("one one", "one").ShouldBe([(0, 3), (4, 3)]);
        FaqText.Matches("anything", "  ").ShouldBeEmpty();
    }

    [Fact]
    public void Snippet_Is_A_Window_Around_The_First_Match()
    {
        var answer = string.Concat(Enumerable.Repeat("word ", 30)) + "the needle is right here " + string.Concat(Enumerable.Repeat("word ", 40));
        var snippet = FaqText.Snippet(answer, "needle");
        snippet.ShouldNotBeNull();
        snippet.ShouldStartWith("…");
        snippet.ShouldEndWith("…");
        snippet.ShouldContain("needle");
        FaqText.Snippet(answer, "absent").ShouldBeNull();
    }

    [AvaloniaFact]
    public void Contact_Row_Raises_The_Event_And_Runs_The_Command()
    {
        var section = Section();
        var ran = 0;
        var raised = 0;
        section.Contact = new FaqContact { Label = "Ask us", Description = "Soon", Command = new TestCommand(() => ran++) };
        section.ContactInvoked += (_, e) =>
        {
            raised++;
            e.Contact.Label.ShouldBe("Ask us");
        };
        var window = Show(section);
        var row = section.ContactRow!;
        row.Label.ShouldBe("Ask us");
        AutomationProperties.GetName(row).ShouldBe("Ask us");
        row.Focus();
        window.KeyPressQwerty(PhysicalKey.Enter, RawInputModifiers.None);
        window.KeyReleaseQwerty(PhysicalKey.Enter, RawInputModifiers.None);
        Dispatcher.UIThread.RunJobs();
        raised.ShouldBe(1);
        ran.ShouldBe(1);
    }

    [AvaloniaFact]
    public void Contact_Row_Can_Be_Hidden()
    {
        var section = Section();
        Show(section);
        section.ContactRow!.IsVisible.ShouldBeTrue();
        section.IsContactVisible = false;
        section.ContactRow.IsVisible.ShouldBeFalse();
    }

    [AvaloniaFact]
    public void Switching_Variant_Rebuilds_Under_Reduced_And_Full_Motion()
    {
        foreach (var level in new[] { ArcMotionLevel.Reduced, ArcMotionLevel.Full })
        {
            var section = Section();
            var window = TestHelpers.Show(section, level);
            window.Width = 1100;
            Dispatcher.UIThread.RunJobs();
            section.Variant = FaqSectionVariant.Search;
            Dispatcher.UIThread.RunJobs();
            section.SearchBox.ShouldNotBeNull();
            section.Variant = FaqSectionVariant.Columns;
            Dispatcher.UIThread.RunJobs();
            section.SelectedCategory = "Technical";
            Dispatcher.UIThread.RunJobs();
            section.Stage.ShouldNotBeNull();
        }
    }

    private sealed class TestCommand(Action run) : System.Windows.Input.ICommand
    {
        public event EventHandler? CanExecuteChanged
        {
            add { }
            remove { }
        }

        public bool CanExecute(object? parameter) => true;

        public void Execute(object? parameter) => run();
    }
}
