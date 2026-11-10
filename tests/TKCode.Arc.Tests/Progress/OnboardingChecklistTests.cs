using TKCode.Arc.Motion;
using TKCode.Arc.Primitives;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;

namespace TKCode.Arc.Tests.Progress;

public class OnboardingChecklistTests
{
    /// <summary>A little longer than the 720 ms a checked step stays in place.</summary>
    private static void WaitForSettle(OnboardingChecklist checklist, string id)
    {
        TestHelpers.PumpUntil(() => checklist.Model.IsSettled(id)).ShouldBeTrue();
        Dispatcher.UIThread.RunJobs();
    }

    private static OnboardingChecklist Create(params string[] ids)
    {
        var checklist = new OnboardingChecklist { Title = "Set up" };
        foreach (var id in ids)
        {
            checklist.Steps.Add(new OnboardingChecklistStep(id, "Step " + id, "About " + id));
        }
        return checklist;
    }

    private static List<OnboardingChecklistStepRow> OpenRows(OnboardingChecklist checklist) =>
        checklist.GetVisualDescendants().OfType<OnboardingChecklistStepRow>().Where(r => r.FindAncestorOfType<OnboardingChecklistFold>()!.IsOpen).ToList();

    private static OnboardingChecklistStepRow Row(OnboardingChecklist checklist, string id) =>
        OpenRows(checklist).First(r => r.Step.Id == id);

    private static List<OnboardingChecklistCompletedRow> CompletedRows(OnboardingChecklist checklist) =>
        checklist.GetVisualDescendants().OfType<OnboardingChecklistCompletedRow>().ToList();

    private static void Click(Control button)
    {
        button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Dispatcher.UIThread.RunJobs();
    }

    private static void Layout(Control control)
    {
        Dispatcher.UIThread.RunJobs();
        control.UpdateLayout();
    }

    [Fact]
    public void Model_Keeps_Done_In_Step_Order_And_Writes_The_Sources_Messages()
    {
        var model = new OnboardingChecklistModel();
        model.Init([new OnboardingChecklistStep("a", "Alpha"), new OnboardingChecklistStep("b", "Beta"), new OnboardingChecklistStep("c", "Gamma")], null);
        model.Expanded.ShouldBe("a");

        model.Check("c").ShouldBeTrue();
        model.Check("c").ShouldBeFalse();
        model.Message.ShouldBe("Gamma done. 1 of 3 complete.");
        model.Check("a").ShouldBeTrue();
        model.Done.ShouldBe(["a", "c"]);
        model.Check("b").ShouldBeTrue();
        model.Message.ShouldBe("All 3 steps done.");
        model.AllDone.ShouldBeTrue();

        model.Uncheck("b").ShouldBeTrue();
        model.Message.ShouldBe("Beta marked as not done. 2 of 3 complete.");
        model.Check("missing").ShouldBeFalse();
    }

    [Fact]
    public void Model_Settling_Hands_The_Open_Panel_On_And_Wraps()
    {
        var model = new OnboardingChecklistModel();
        model.Init([new OnboardingChecklistStep("a", "A"), new OnboardingChecklistStep("b", "B"), new OnboardingChecklistStep("c", "C")], null);
        model.Expand("c");
        model.Check("c");
        // Still open while it stays in place, struck through.
        model.Expanded.ShouldBe("c");
        model.Settle("c").ShouldBeTrue();
        model.Expanded.ShouldBe("a");
        model.Settle("c").ShouldBeFalse();

        // Settling a step that is not open leaves the open panel alone.
        model.Check("b");
        model.Settle("b");
        model.Expanded.ShouldBe("a");

        model.Check("a");
        model.Settle("a");
        model.Expanded.ShouldBeNull();

        // Unchecking with nothing open opens that step.
        model.Uncheck("b");
        model.Expanded.ShouldBe("b");
        model.IsSettled("b").ShouldBeFalse();
    }

    [Fact]
    public void Model_Expand_Toggles_And_A_Failure_Is_Cleared_By_The_Next_Start()
    {
        var model = new OnboardingChecklistModel();
        model.Init([new OnboardingChecklistStep("a", "A"), new OnboardingChecklistStep("b", "B")], ["b"]);
        model.Done.ShouldBe(["b"]);
        model.IsSettled("b").ShouldBeTrue();
        model.Expand("a");
        model.Expanded.ShouldBeNull();
        model.Expand("a");
        model.Expanded.ShouldBe("a");

        model.Start("a");
        model.IsPending("a").ShouldBeTrue();
        model.Fail("a", "Nope");
        model.IsPending("a").ShouldBeFalse();
        model.ErrorOf("a").ShouldBe("Nope");
        model.Message.ShouldBe("Nope");
        model.Start("a");
        model.ErrorOf("a").ShouldBeNull();
    }

    [AvaloniaFact]
    public void First_Unfinished_Step_Opens_And_Default_Title_Words_Apply()
    {
        var checklist = Create("a", "b", "c");
        checklist.Completed = ["a"];
        var window = TestHelpers.Show(checklist);
        try
        {
            checklist.Model.Expanded.ShouldBe("b");
            OpenRows(checklist).Select(r => r.Step.Id).ShouldBe(["b", "c"]);
            Row(checklist, "b").PanelPart.ShouldNotBeNull();
            // The panel is only built for a step that has been opened.
            Row(checklist, "c").PanelPart.ShouldBeNull();
            TestHelpers.Descendant<OnboardingChecklistSwapText>(checklist, "PART_Title").Text.ShouldBe("Set up");
            TestHelpers.Descendant<OnboardingChecklistNumber>(checklist, "PART_CountNumber").Value.ShouldBe(1);
            TestHelpers.Descendant<TextBlock>(checklist, "PART_CountText").Text.ShouldBe(" of 3 done");
            TestHelpers.Descendant<ArcRing>(checklist, "PART_RingFill").Value.ShouldBe(1.0 / 3, 0.0001);
            checklist.DoneTitle.ShouldBe("You’re all set");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Checking_Reports_At_Once_And_Folds_After_The_Settle_Time()
    {
        var checklist = Create("a", "b");
        var reports = new List<IReadOnlyList<string>>();
        checklist.CompletedChanged += (_, e) => reports.Add(e.Completed);
        var window = TestHelpers.Show(checklist);
        try
        {
            var row = Row(checklist, "a");
            Click(row.Check!);

            reports.Single().ShouldBe(["a"]);
            checklist.Completed.ShouldBe(["a"]);
            row.Check!.IsChecked.ShouldBe(true);
            AutomationProperties.GetItemStatus(checklist).ShouldBe("Step a done. 1 of 2 complete.");
            // Still in the list, still open, struck through.
            OpenRows(checklist).Count.ShouldBe(2);
            checklist.Model.Expanded.ShouldBe("a");
            TestHelpers.Descendant<OnboardingChecklistStrikeText>(row).StrikeProgress.ShouldBe(1);

            WaitForSettle(checklist, "a");
            OpenRows(checklist).Select(r => r.Step.Id).ShouldBe(["b"]);
            checklist.Model.Expanded.ShouldBe("b");
            TestHelpers.Descendant<OnboardingChecklistFold>(checklist, "PART_Group").IsOpen.ShouldBeTrue();
            TestHelpers.Descendant<OnboardingChecklistNumber>(checklist, "PART_GroupNumber").Value.ShouldBe(1);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Unchecking_Before_It_Settles_Keeps_The_Row_And_The_Timer_Does_Not_Fold_It()
    {
        var checklist = Create("a", "b");
        var window = TestHelpers.Show(checklist);
        try
        {
            var row = Row(checklist, "a");
            Click(row.Check!);
            Click(row.Check!);
            row.Check!.IsChecked.ShouldBe(false);
            checklist.Completed.ShouldBeEmpty();
            AutomationProperties.GetItemStatus(checklist).ShouldBe("Step a marked as not done. 0 of 2 complete.");

            // Longer than the settle time: nothing folds.
            var deadline = DateTime.UtcNow.AddMilliseconds(900);
            TestHelpers.PumpUntil(() => DateTime.UtcNow > deadline);
            checklist.Model.IsSettled("a").ShouldBeFalse();
            OpenRows(checklist).Count.ShouldBe(2);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void All_Done_Swaps_The_Title_And_Draws_The_Ring_Tick()
    {
        var checklist = Create("a");
        checklist.DoneTitle = "Finished";
        var window = TestHelpers.Show(checklist);
        try
        {
            checklist.IsComplete.ShouldBeFalse();
            TestHelpers.Descendant<OnboardingChecklistSwap>(checklist, "PART_RingTick").Current.ShouldBeNull();
            checklist.CompleteStep("a");
            Dispatcher.UIThread.RunJobs();

            checklist.IsComplete.ShouldBeTrue();
            TestHelpers.Descendant<OnboardingChecklistSwapText>(checklist, "PART_Title").Text.ShouldBe("Finished");
            TestHelpers.Descendant<OnboardingChecklistSwap>(checklist, "PART_RingTick").Current.ShouldBeOfType<StrokeDrawGlyph>();
            TestHelpers.Descendant<ArcRing>(checklist, "PART_RingFill").Value.ShouldBe(1);
            AutomationProperties.GetItemStatus(checklist).ShouldBe("All 1 steps done.");
            AutomationProperties.GetName(checklist).ShouldBe("Finished");

            checklist.ReopenStep("a");
            Dispatcher.UIThread.RunJobs();
            TestHelpers.Descendant<OnboardingChecklistSwapText>(checklist, "PART_Title").Text.ShouldBe("Set up");
            TestHelpers.Descendant<OnboardingChecklistSwap>(checklist, "PART_RingTick").Current.ShouldBeNull();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Completed_Group_Opens_And_Reopening_A_Step_Moves_Focus_To_Its_Check()
    {
        var checklist = Create("a", "b");
        checklist.Completed = ["a"];
        var window = TestHelpers.Show(checklist);
        try
        {
            CompletedRows(checklist).ShouldBeEmpty();
            var toggle = TestHelpers.Descendant<OnboardingChecklistGroupToggle>(checklist, "PART_GroupToggle");
            Click(toggle);
            Layout(checklist);
            var completed = CompletedRows(checklist).Single();
            completed.Step.Id.ShouldBe("a");
            completed.Check!.IsChecked.ShouldBe(true);

            completed.Check!.Focus(NavigationMethod.Tab);
            Click(completed.Check!);
            Layout(checklist);

            checklist.Completed.ShouldBeEmpty();
            var row = Row(checklist, "a");
            row.Check!.IsFocused.ShouldBeTrue();
            row.Check!.IsChecked.ShouldBe(false);
            TestHelpers.Descendant<OnboardingChecklistFold>(checklist, "PART_Group").IsOpen.ShouldBeFalse();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void When_A_Focused_Step_Settles_Focus_Moves_To_The_Next_Action_Then_The_Title_Then_The_Group()
    {
        var checklist = new OnboardingChecklist { Title = "Set up" };
        checklist.Steps.Add(new OnboardingChecklistStep("a", "A"));
        checklist.Steps.Add(new OnboardingChecklistStep("b", "B") { ActionLabel = "Do B", Action = () => Task.CompletedTask });
        checklist.Steps.Add(new OnboardingChecklistStep("c", "C"));
        var window = TestHelpers.Show(checklist);
        try
        {
            var a = Row(checklist, "a");
            a.Check!.Focus(NavigationMethod.Tab);
            Click(a.Check!);
            WaitForSettle(checklist, "a");
            Layout(checklist);
            var action = TestHelpers.Descendant<OnboardingChecklistActionButton>(Row(checklist, "b"));
            action.IsFocused.ShouldBeTrue();

            // The action's completion checks B; focus is inside it, so it moves on to C, which has no action: its title.
            Click(action);
            Layout(checklist);
            WaitForSettle(checklist, "b");
            Layout(checklist);
            Row(checklist, "c").TitleButton!.IsFocused.ShouldBeTrue();

            var c = Row(checklist, "c");
            c.Check!.Focus(NavigationMethod.Tab);
            Click(c.Check!);
            WaitForSettle(checklist, "c");
            Layout(checklist);
            TestHelpers.Descendant<OnboardingChecklistGroupToggle>(checklist, "PART_GroupToggle").IsFocused.ShouldBeTrue();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public async Task Action_Shows_Pending_Then_Checks_The_Step_Off()
    {
        var gate = new TaskCompletionSource();
        var runs = 0;
        var step = new OnboardingChecklistStep("a", "A")
        {
            ActionLabel = "Do it",
            Action = () =>
            {
                runs++;
                return gate.Task;
            }
        };
        var checklist = new OnboardingChecklist { Title = "Set up" };
        checklist.Steps.Add(step);
        var window = TestHelpers.Show(checklist);
        try
        {
            var action = TestHelpers.Descendant<OnboardingChecklistActionButton>(checklist);
            Click(action);
            action.State.ShouldBe(OnboardingChecklistActionState.Pending);
            action.Classes.Contains(":pending").ShouldBeTrue();

            // Presses while it runs do nothing.
            Click(action);
            runs.ShouldBe(1);

            gate.SetResult();
            await Task.Yield();
            Dispatcher.UIThread.RunJobs();
            action.State.ShouldBe(OnboardingChecklistActionState.Done);
            checklist.Completed.ShouldBe(["a"]);

            // Nor after it is done.
            Click(action);
            runs.ShouldBe(1);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public async Task Failing_Action_Keeps_The_Step_Open_With_The_Message_And_Can_Retry()
    {
        var attempts = 0;
        var step = new OnboardingChecklistStep("a", "A")
        {
            ActionLabel = "Do it",
            Action = async () =>
            {
                await Task.Yield();
                if (++attempts == 1)
                {
                    throw new InvalidOperationException("Provider down");
                }
            }
        };
        var checklist = new OnboardingChecklist { Title = "Set up" };
        checklist.Steps.Add(step);
        var window = TestHelpers.Show(checklist);
        try
        {
            var action = TestHelpers.Descendant<OnboardingChecklistActionButton>(checklist);
            Click(action);
            TestHelpers.PumpUntil(() => checklist.Model.ErrorOf("a") is not null).ShouldBeTrue();
            Dispatcher.UIThread.RunJobs();

            action.State.ShouldBe(OnboardingChecklistActionState.Idle);
            checklist.Completed.ShouldBeNull();
            TestHelpers.Descendant<TextBlock>(checklist, "PART_Error").Text.ShouldBe("Provider down");
            TestHelpers.Descendant<OnboardingChecklistFold>(checklist, "PART_ErrorFold").IsOpen.ShouldBeTrue();
            AutomationProperties.GetItemStatus(checklist).ShouldBe("Provider down");

            Click(action);
            TestHelpers.PumpUntil(() => checklist.Model.IsDone("a")).ShouldBeTrue();
            await Task.Yield();
            Dispatcher.UIThread.RunJobs();
            TestHelpers.Descendant<OnboardingChecklistFold>(checklist, "PART_ErrorFold").IsOpen.ShouldBeFalse();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void An_Exception_Without_A_Message_Uses_The_Fallback_And_A_Command_Runs_The_Step()
    {
        var ran = 0;
        var checklist = new OnboardingChecklist { Title = "Set up" };
        checklist.Steps.Add(new OnboardingChecklistStep("a", "A")
        {
            ActionLabel = "Fail",
            Action = () => Task.FromException(new EmptyMessageException())
        });
        checklist.Steps.Add(new OnboardingChecklistStep("b", "B") { ActionLabel = "Run", Command = new RelayCommand(() => ran++) });
        var window = TestHelpers.Show(checklist);
        try
        {
            Click(TestHelpers.Descendant<OnboardingChecklistActionButton>(Row(checklist, "a")));
            TestHelpers.PumpUntil(() => checklist.Model.ErrorOf("a") is not null).ShouldBeTrue();
            checklist.Model.ErrorOf("a").ShouldBe("That didn’t work. Try again.");

            Click(Row(checklist, "b").TitleButton!);
            Layout(checklist);
            Click(TestHelpers.Descendant<OnboardingChecklistActionButton>(Row(checklist, "b")));
            TestHelpers.PumpUntil(() => checklist.Model.IsDone("b")).ShouldBeTrue();
            ran.ShouldBe(1);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void A_Step_Without_An_Action_Label_Has_No_Button()
    {
        var checklist = Create("a");
        var window = TestHelpers.Show(checklist);
        try
        {
            Layout(checklist);
            TestHelpers.Descendant<OnboardingChecklistActionButton>(checklist).IsVisible.ShouldBeFalse();
            TestHelpers.Descendant<TextBlock>(checklist, "PART_Description").Text.ShouldBe("About a");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Titles_Open_Only_One_Step_And_Arrow_Keys_Walk_Them()
    {
        var checklist = Create("a", "b", "c");
        var window = TestHelpers.Show(checklist);
        try
        {
            Click(Row(checklist, "b").TitleButton!);
            checklist.Model.Expanded.ShouldBe("b");
            Click(Row(checklist, "b").TitleButton!);
            checklist.Model.Expanded.ShouldBeNull();

            var a = Row(checklist, "a").TitleButton!;
            var b = Row(checklist, "b").TitleButton!;
            var c = Row(checklist, "c").TitleButton!;
            a.Focus(NavigationMethod.Tab);
            window.KeyPressQwerty(PhysicalKey.ArrowDown, RawInputModifiers.None);
            b.IsFocused.ShouldBeTrue();
            window.KeyPressQwerty(PhysicalKey.End, RawInputModifiers.None);
            c.IsFocused.ShouldBeTrue();
            window.KeyPressQwerty(PhysicalKey.ArrowDown, RawInputModifiers.None);
            a.IsFocused.ShouldBeTrue();
            window.KeyPressQwerty(PhysicalKey.ArrowUp, RawInputModifiers.None);
            c.IsFocused.ShouldBeTrue();
            window.KeyPressQwerty(PhysicalKey.Home, RawInputModifiers.None);
            a.IsFocused.ShouldBeTrue();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Hide_Folds_The_Body_Reports_It_And_Takes_Focus_From_Inside()
    {
        var checklist = Create("a");
        var hidden = new List<bool>();
        checklist.HiddenChanged += (_, e) => hidden.Add(e.IsHidden);
        var window = TestHelpers.Show(checklist);
        try
        {
            var hide = TestHelpers.Descendant<OnboardingChecklistHideButton>(checklist, "PART_Hide");
            var body = TestHelpers.Descendant<OnboardingChecklistFold>(checklist, "PART_Body");
            var label = TestHelpers.Descendant<OnboardingChecklistSwapText>(checklist, "PART_HideLabel");
            label.Text.ShouldBe("Hide");
            AutomationProperties.GetName(hide).ShouldBe("Hide checklist");
            var full = checklist.Bounds.Height;

            Click(hide);
            Layout(checklist);
            checklist.IsHidden.ShouldBeTrue();
            hidden.ShouldBe([true]);
            body.IsOpen.ShouldBeFalse();
            body.IsHitTestVisible.ShouldBeFalse();
            label.Text.ShouldBe("Show");
            AutomationProperties.GetName(hide).ShouldBe("Show checklist");
            checklist.Bounds.Height.ShouldBeLessThan(full);

            Click(hide);
            Layout(checklist);
            hidden.ShouldBe([true, false]);
            body.IsOpen.ShouldBeTrue();

            // Code hides it while focus is inside: no event, and focus moves to the button.
            var check = Row(checklist, "a").Check!;
            check.Focus(NavigationMethod.Tab);
            checklist.IsHidden = true;
            Dispatcher.UIThread.RunJobs();
            hide.IsFocused.ShouldBeTrue();
            hidden.Count.ShouldBe(2);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Starting_Hidden_Measures_Only_The_Header()
    {
        var checklist = Create("a");
        checklist.IsHidden = true;
        var window = TestHelpers.Show(checklist);
        try
        {
            Layout(checklist);
            var body = TestHelpers.Descendant<OnboardingChecklistFold>(checklist, "PART_Body");
            body.IsOpen.ShouldBeFalse();
            body.Bounds.Height.ShouldBe(0);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Setting_Completed_From_Code_Folds_At_Once_Without_An_Event()
    {
        var checklist = Create("a", "b");
        var reports = 0;
        checklist.CompletedChanged += (_, _) => reports++;
        var window = TestHelpers.Show(checklist);
        try
        {
            checklist.Completed = ["a"];
            Dispatcher.UIThread.RunJobs();
            reports.ShouldBe(0);
            checklist.Model.IsSettled("a").ShouldBeTrue();
            checklist.Model.Expanded.ShouldBe("b");
            OpenRows(checklist).Select(r => r.Step.Id).ShouldBe(["b"]);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Steps_Can_Change_And_ItemsSource_Takes_Precedence()
    {
        var items = new Avalonia.Collections.AvaloniaList<OnboardingChecklistStep> { new("x", "X"), new("z", "Z") };
        var checklist = Create("a");
        checklist.ItemsSource = items;
        var window = TestHelpers.Show(checklist);
        try
        {
            OpenRows(checklist).Select(r => r.Step.Id).ShouldBe(["x", "z"]);
            items.Insert(1, new OnboardingChecklistStep("y", "Y"));
            Dispatcher.UIThread.RunJobs();
            OpenRows(checklist).Select(r => r.Step.Id).ShouldBe(["x", "y", "z"]);
            TestHelpers.Descendant<TextBlock>(checklist, "PART_CountText").Text.ShouldBe(" of 3 done");

            checklist.Model.Expanded.ShouldBe("x");
            items.RemoveAt(0);
            Dispatcher.UIThread.RunJobs();
            OpenRows(checklist).Select(r => r.Step.Id).ShouldBe(["y", "z"]);
            checklist.Model.Expanded.ShouldBe("y");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Step_Text_Follows_The_Step_And_Strings_Translate()
    {
        var checklist = Create("a");
        var window = TestHelpers.Show(checklist);
        try
        {
            var step = checklist.Steps[0];
            step.Title = "Renamed";
            Dispatcher.UIThread.RunJobs();
            TestHelpers.Descendant<OnboardingChecklistStrikeText>(checklist).Text.ShouldBe("Renamed");
            AutomationProperties.GetName(Row(checklist, "a").Check!).ShouldBe("Renamed");

            checklist.Strings = new OnboardingChecklistStrings { Hide = "隐藏", CountFormat = "/ {0} 已完成", Progress = "进度" };
            Dispatcher.UIThread.RunJobs();
            TestHelpers.Descendant<OnboardingChecklistSwapText>(checklist, "PART_HideLabel").Text.ShouldBe("隐藏");
            TestHelpers.Descendant<TextBlock>(checklist, "PART_CountText").Text.ShouldBe(" / 1 已完成");
            AutomationProperties.GetName(TestHelpers.Descendant<Panel>(checklist, "PART_Ring")).ShouldBe("进度");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Narrow_Cards_Show_Only_The_Chevron()
    {
        var checklist = Create("a");
        checklist.Width = 320;
        var window = TestHelpers.Show(checklist);
        try
        {
            Layout(checklist);
            checklist.Classes.Contains(OnboardingChecklist.NarrowPseudoClass).ShouldBeTrue();
            TestHelpers.Descendant<OnboardingChecklistSwapText>(checklist, "PART_HideLabel").IsVisible.ShouldBeFalse();
            TestHelpers.Descendant<OnboardingChecklistHideButton>(checklist, "PART_Hide").Bounds.Width.ShouldBe(36);

            checklist.Width = 500;
            Layout(checklist);
            checklist.Classes.Contains(OnboardingChecklist.NarrowPseudoClass).ShouldBeFalse();
            TestHelpers.Descendant<OnboardingChecklistSwapText>(checklist, "PART_HideLabel").IsVisible.ShouldBeTrue();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Numbers_Fold_A_Column_Per_Place()
    {
        var number = new OnboardingChecklistNumber();
        var window = TestHelpers.Show(number);
        try
        {
            number.Set(9, false);
            Layout(number);
            number.GetVisualChildren().OfType<OnboardingChecklistFold>().Count(f => f.IsOpen).ShouldBe(1);
            number.Set(10, false);
            Layout(number);
            number.GetVisualChildren().OfType<OnboardingChecklistFold>().Count(f => f.IsOpen).ShouldBe(2);
            number.Set(7, false);
            Layout(number);
            number.GetVisualChildren().OfType<OnboardingChecklistFold>().Count(f => f.IsOpen).ShouldBe(1);
            number.Value.ShouldBe(7);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Full_Motion_Folds_A_Row_Out_Through_Its_Spring_Then_Removes_It()
    {
        var checklist = Create("a", "b");
        checklist.Completed = null;
        var window = TestHelpers.Show(checklist, ArcMotionLevel.Full);
        try
        {
            Layout(checklist);
            TestHelpers.PumpUntil(() => checklist.IsLoaded).ShouldBeTrue();
            var row = Row(checklist, "a");
            var fold = row.FindAncestorOfType<OnboardingChecklistFold>()!;
            checklist.CompleteStep("a");
            WaitForSettle(checklist, "a");

            fold.IsOpen.ShouldBeFalse();
            // Inert while it leaves, then gone.
            fold.IsHitTestVisible.ShouldBeFalse();
            TestHelpers.PumpUntil(() => fold.Parent is null).ShouldBeTrue();
            checklist.GetVisualDescendants().OfType<OnboardingChecklistStepRow>().Select(r => r.Step.Id).ShouldBe(["b"]);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Full_Motion_Swap_Keeps_The_Old_Layer_Until_It_Has_Left()
    {
        var swap = new OnboardingChecklistSwap { Kind = OnboardingChecklistSwapKind.Rise };
        var window = TestHelpers.Show(swap, ArcMotionLevel.Full);
        try
        {
            swap.Show(new TextBlock { Text = "One" }, 1, false);
            swap.LeavingCount.ShouldBe(0);
            swap.Show(new TextBlock { Text = "Two" }, 1, true);
            swap.LeavingCount.ShouldBe(1);
            TestHelpers.PumpUntil(() => swap.LeavingCount == 0).ShouldBeTrue();
            swap.GetVisualChildren().Count().ShouldBe(1);
        }
        finally
        {
            window.Close();
        }
    }

    private sealed class EmptyMessageException : Exception
    {
        public override string Message => string.Empty;
    }

    private sealed class RelayCommand(Action execute) : System.Windows.Input.ICommand
    {
        public event EventHandler? CanExecuteChanged
        {
            add { }
            remove { }
        }

        public bool CanExecute(object? parameter) => true;

        public void Execute(object? parameter) => execute();
    }
}
