using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Threading;
using TKCode.Arc.Motion;

namespace TKCode.Arc.Tests.Blocks.Developer;

/// <summary>
/// The Review step of <see cref="ImportWizard"/> on the sample file (22 rows, nine with one problem each, five of them with a
/// safe fix, four clean rows that update existing records): the counts and rows, clean-up (paced and at once), editing with
/// the keyboard and the pointer, leaving rows out, the filter, the compact layout and the names for screen readers.
/// </summary>
public class ImportWizardReviewTests
{
    private static readonly int[] IssueLines = [3, 6, 9, 11, 13, 16, 17, 20, 23];

    private sealed class Rig : IDisposable
    {
        public Rig(double width = 760, ArcMotionLevel level = ArcMotionLevel.Off, double timing = 0)
        {
            Wizard = new ImportWizard { TimingScale = timing };
            Host = new Border { Width = width, Child = Wizard, VerticalAlignment = VerticalAlignment.Top };
            Window = TestHelpers.Show(Host, level);
            Window.Width = 1000;
            Window.Height = 1600;
            Pump();
            Wizard.LoadCsv(Wizard.SampleCsv!, Wizard.SampleFileName).ShouldBeTrue();
            TestHelpers.PumpUntil(() => Wizard.Session.HasFile, 5000).ShouldBeTrue();
            Wizard.GoTo(ImportWizardStep.Map);
            Pump();
            Wizard.GoTo(ImportWizardStep.Review);
            Pump();
        }

        public ImportWizard Wizard { get; }

        public Border Host { get; }

        public Window Window { get; }

        public ImportWizardSession Session => Wizard.Session;

        public ImportWizardReviewView View => Wizard.CurrentView.ShouldBeOfType<ImportWizardReviewView>();

        public ImportWizardLabels Labels => Wizard.Labels;

        public void Pump()
        {
            for (var i = 0; i < 3; i++)
            {
                Window.UpdateLayout();
                Dispatcher.UIThread.RunJobs();
            }
        }

        public ImportReviewRow Row(int line) => Session.Rows.Single(r => r.LineNumber == line);

        public int FieldOf(string key) => Session.Fields.Select((t, i) => (t, i)).Single(p => Session.Targets[p.t].Key == key).i;

        public ImportWizardReviewCell Cell(int line, string key) =>
            View.Table.RowFor(Row(line)).ShouldNotBeNull().Cells[FieldOf(key)];

        public int[] ShownLines() => View.Table.ShownRows.Select(r => r.LineNumber).ToArray();

        public void Press(PhysicalKey key)
        {
            Window.KeyPressQwerty(key, RawInputModifiers.None);
            Window.KeyReleaseQwerty(key, RawInputModifiers.None);
            Pump();
        }

        public void Await(Task task)
        {
            TestHelpers.PumpUntil(() => task.IsCompleted, 5000).ShouldBeTrue();
            task.GetAwaiter().GetResult();
            Pump();
        }

        public void Dispose() => Window.Close();
    }

    private static void Click(Control control) => control.RaiseEvent(new RoutedEventArgs(Avalonia.Controls.Button.ClickEvent));

    [AvaloniaFact]
    public void Sample_shows_nine_rows_to_fix_thirteen_ready_and_five_values_to_clean_up()
    {
        using var rig = new Rig();
        var view = rig.View;
        rig.Session.Fields.Count.ShouldBe(7);
        rig.Session.IssueRows.ShouldBe(9);
        rig.Session.ReadyRows.ShouldBe(13);
        rig.Session.FixableCells.ShouldBe(5);

        view.Subtitle.PlainText.ShouldBe("9 rows to fix · 13 ready");
        AutomationProperties.GetLiveSetting(view.Subtitle).ShouldBe(AutomationLiveSetting.Polite);
        view.Filter.ShowAll.ShouldBeFalse();
        view.Filter.Issues.IsSelected.ShouldBeTrue();
        view.Filter.Issues.Count.ShouldBe(9);
        view.Filter.All.Count.ShouldBe(22);
        view.CleanUpButton.IsVisible.ShouldBeTrue();
        view.CleanUpButton.Words.PlainText.ShouldBe("Clean up 5 values");
        view.ImportText.ShouldBe("Import 13 contacts · 9 left out");
        rig.ShownLines().ShouldBe(IssueLines);

        // Problems show on their cells; clean cells of the same row have none.
        var email = rig.Cell(3, "email");
        email.Problem.ShouldNotBeNull();
        email.Icon.ShouldBe(ImportReviewIcon.Alert);
        email.Tint.ShouldBe(ImportReviewTint.Danger);
        rig.Cell(3, "name").Problem.ShouldBeNull();
        rig.Cell(3, "name").Tint.ShouldBe(ImportReviewTint.None);
        rig.Cell(9, "name").Value.ShouldBe(string.Empty);

        // Rows with a message line are taller than plain rows (49.4 against 41).
        var tall = view.Table.RowFor(rig.Row(3)).ShouldNotBeNull();
        tall.LayoutHeight.ShouldBeGreaterThan(48);
    }

    [AvaloniaFact]
    public void Names_for_screen_readers_follow_the_labels()
    {
        using var rig = new Rig();
        var view = rig.View;
        var labels = rig.Labels;
        var email = rig.Cell(3, "email");
        AutomationProperties.GetName(email).ShouldBe(labels.CellName("Email", 3, email.Problem));
        AutomationProperties.GetName(rig.Cell(3, "name")).ShouldBe(labels.CellName("Name", 3, null));
        var leaveOut = view.Table.RowFor(rig.Row(3)).ShouldNotBeNull().LeaveOut;
        AutomationProperties.GetName(leaveOut).ShouldBe("Leave out line 3");
        AutomationProperties.GetName(view.TableBox.Viewer).ShouldBe(labels.TableName);
        view.TableBox.Viewer.Focusable.ShouldBeTrue();
        AutomationProperties.GetName(view.Filter).ShouldBe(labels.FilterName);
        AutomationProperties.GetName(view.Filter.Issues).ShouldBe("To fix 9");
        AutomationProperties.GetName(view.Filter.All).ShouldBe("All 22");
        AutomationProperties.GetName(view.CleanUpButton).ShouldBe("Clean up 5 values");
        AutomationProperties.GetName(view.ImportButton).ShouldBe("Import 13 contacts · 9 left out");
        AutomationProperties.GetName(view.Subtitle).ShouldBe("9 rows to fix · 13 ready");
    }

    [AvaloniaFact]
    public void Clean_up_fixes_the_five_values_and_the_fixed_rows_leave_to_fix()
    {
        using var rig = new Rig();
        var view = rig.View;
        rig.Await(view.CleanUpAsync());

        rig.Session.ReadyRows.ShouldBe(18);
        rig.Session.IssueRows.ShouldBe(4);
        rig.Session.SkippedRows.ShouldBe(4);
        rig.Session.FixableCells.ShouldBe(0);
        rig.Row(6).Values[rig.FieldOf("email")].ShouldBe("rafael.ortiz@kiln.example");
        rig.Row(11).Values[rig.FieldOf("country")].ShouldBe("United States");
        rig.Row(16).Values[rig.FieldOf("email")].ShouldBe("mei.tanaka@bramble.example");
        rig.Row(20).Values[rig.FieldOf("country")].ShouldBe("Netherlands");
        rig.Row(23).Values[rig.FieldOf("plan")].ShouldBe("Team");

        view.Subtitle.PlainText.ShouldBe("4 rows to fix · 18 ready");
        view.Filter.Issues.Count.ShouldBe(4);
        view.Filter.All.Count.ShouldBe(22);
        view.ImportText.ShouldBe("Import 18 contacts · 4 left out");
        view.CleanUpButton.IsVisible.ShouldBeFalse();
        // It left with its last words, like Arc's button.
        view.CleanUpButton.Words.PlainText.ShouldBe("Clean up 1 value");
        view.GlowCount.ShouldBe(0);
        rig.ShownLines().ShouldBe([3, 9, 13, 17]);

        var records = rig.Session.BuildRecords();
        records.Count.ShouldBe(18);
        records.Count(r => r.IsUpdate).ShouldBe(4);
        records.Count(r => !r.IsUpdate).ShouldBe(14);
        records.Single(r => r.RowNumber == 6).Values["email"].ShouldBe("rafael.ortiz@kiln.example");
    }

    [AvaloniaFact]
    public void Clean_up_in_all_keeps_every_row_and_the_glow_fades()
    {
        using var rig = new Rig();
        var view = rig.View;
        Click(view.Filter.All);
        rig.Pump();
        view.Filter.ShowAll.ShouldBeTrue();
        rig.ShownLines().Length.ShouldBe(22);

        rig.Await(view.CleanUpAsync());
        rig.ShownLines().Length.ShouldBe(22);
        var fixedCell = rig.Cell(6, "email");
        fixedCell.Problem.ShouldBeNull();
        fixedCell.Icon.ShouldBe(ImportReviewIcon.None);
        fixedCell.Tint.ShouldBe(ImportReviewTint.None);
        rig.Row(6).Fixed[rig.FieldOf("email")].ShouldBeFalse();
    }

    [AvaloniaFact]
    public void Clean_up_with_full_motion_fixes_the_first_value_at_once_then_one_at_a_time()
    {
        using var rig = new Rig(level: ArcMotionLevel.Full, timing: 1);
        var view = rig.View;
        var task = view.CleanUpAsync();
        // The first fix lands with the click; the rest follow every 90ms.
        rig.Session.FixableCells.ShouldBe(4);
        view.IsCleaningUp.ShouldBeTrue();
        var fixedCell = rig.Cell(6, "email");
        fixedCell.Icon.ShouldBe(ImportReviewIcon.Check);
        fixedCell.Tint.ShouldBe(ImportReviewTint.Success);
        view.GlowCount.ShouldBe(1);
        rig.Await(task);
        rig.Session.FixableCells.ShouldBe(0);
        rig.Session.ReadyRows.ShouldBe(18);
        // The glowing rows stay in "To fix" until their glow ends, then leave.
        TestHelpers.PumpUntil(() => rig.ShownLines().Length == 4 && view.Table.LeavingCount == 0, 5000).ShouldBeTrue();
        rig.ShownLines().ShouldBe([3, 9, 13, 17]);
    }

    [AvaloniaFact]
    public void Clean_up_with_reduced_motion_fixes_every_value_at_once()
    {
        using var rig = new Rig(level: ArcMotionLevel.Reduced, timing: 1);
        var task = rig.View.CleanUpAsync();
        task.IsCompleted.ShouldBeTrue();
        rig.Session.FixableCells.ShouldBe(0);
        rig.Session.ReadyRows.ShouldBe(18);
    }

    [AvaloniaFact]
    public void Enter_opens_the_editor_with_the_caret_at_the_end_and_enter_saves_a_fix()
    {
        using var rig = new Rig();
        var view = rig.View;
        var cell = rig.Cell(9, "name");
        cell.Focus(NavigationMethod.Tab).ShouldBeTrue();
        rig.Press(PhysicalKey.Enter);

        var editor = view.Editor.ShouldNotBeNull();
        editor.IsFocused.ShouldBeTrue();
        cell.IsEditing.ShouldBeTrue();
        AutomationProperties.GetName(editor).ShouldBe(rig.Labels.EditorName("Name", 9));
        editor.Text = "Chen Wei";
        editor.CaretIndex = editor.Text.Length;
        rig.Press(PhysicalKey.Enter);

        view.Editor.ShouldBeNull();
        cell.IsEditing.ShouldBeFalse();
        rig.Row(9).Values[rig.FieldOf("name")].ShouldBe("Chen Wei");
        rig.Row(9).HasProblems.ShouldBeFalse();
        rig.Session.IssueRows.ShouldBe(8);
        rig.Session.ReadyRows.ShouldBe(14);
        view.Subtitle.PlainText.ShouldBe("8 rows to fix · 14 ready");
        // The fixed row glowed and left "To fix" (no waits in tests); the focus went to the same cell of the next row.
        rig.ShownLines().ShouldNotContain(9);
        rig.Cell(11, "name").IsFocused.ShouldBeTrue();
    }

    [AvaloniaFact]
    public void Editor_puts_the_caret_at_the_end_without_selecting()
    {
        using var rig = new Rig();
        var cell = rig.Cell(3, "email");
        Click(cell);
        rig.Pump();
        var editor = rig.View.Editor.ShouldNotBeNull();
        editor.Text.ShouldBe(cell.Value);
        editor.CaretIndex.ShouldBe(cell.Value.Length);
        editor.SelectionStart.ShouldBe(editor.SelectionEnd);
        editor.Bounds.Height.ShouldBe(40);
        editor.Bounds.Width.ShouldBeGreaterThanOrEqualTo(140);
    }

    [AvaloniaFact]
    public void Escape_cancels_the_edit_and_gives_the_focus_back()
    {
        using var rig = new Rig();
        var view = rig.View;
        var cell = rig.Cell(3, "email");
        var before = cell.Value;
        cell.Focus(NavigationMethod.Tab);
        rig.Press(PhysicalKey.Enter);
        var editor = view.Editor.ShouldNotBeNull();
        editor.Text = "someone@example.com";
        rig.Press(PhysicalKey.Escape);

        view.Editor.ShouldBeNull();
        cell.IsFocused.ShouldBeTrue();
        rig.Row(3).Values[rig.FieldOf("email")].ShouldBe(before);
        rig.Row(3).HasProblems.ShouldBeTrue();
        rig.Session.IssueRows.ShouldBe(9);
    }

    [AvaloniaFact]
    public void Enter_on_an_edit_that_keeps_the_row_gives_the_focus_back_to_the_cell()
    {
        using var rig = new Rig();
        var view = rig.View;
        var cell = rig.Cell(3, "company");
        cell.Focus(NavigationMethod.Tab);
        rig.Press(PhysicalKey.Enter);
        view.Editor.ShouldNotBeNull().Text = "Larkspur Labs";
        rig.Press(PhysicalKey.Enter);
        view.Editor.ShouldBeNull();
        cell.IsFocused.ShouldBeTrue();
        cell.Value.ShouldBe("Larkspur Labs");
        rig.ShownLines().ShouldContain(3);
        rig.Session.IssueRows.ShouldBe(9);
    }

    [AvaloniaFact]
    public void Space_opens_the_editor_and_leaving_the_field_saves()
    {
        using var rig = new Rig();
        var view = rig.View;
        var cell = rig.Cell(13, "phone");
        cell.Focus(NavigationMethod.Tab);
        rig.Press(PhysicalKey.Space);
        var editor = view.Editor.ShouldNotBeNull();
        editor.Text = "+44 20 7946 0999";
        view.BackButton.Focus(NavigationMethod.Tab).ShouldBeTrue();
        rig.Pump();

        view.Editor.ShouldBeNull();
        view.BackButton.IsFocused.ShouldBeTrue();
        rig.Row(13).HasProblems.ShouldBeFalse();
        rig.Session.ReadyRows.ShouldBe(14);
    }

    [AvaloniaFact]
    public void An_edit_that_breaks_a_value_shows_the_problem_and_a_fixable_one_brings_clean_up_back()
    {
        using var rig = new Rig();
        var view = rig.View;
        rig.Await(view.CleanUpAsync());
        view.CleanUpButton.IsVisible.ShouldBeFalse();
        Click(view.Filter.All);
        rig.Pump();

        var country = rig.Cell(2, "country");
        Click(country);
        rig.Pump();
        view.Editor.ShouldNotBeNull().Text = "UK";
        rig.Press(PhysicalKey.Enter);

        country.Problem.ShouldNotBeNull();
        country.Tint.ShouldBe(ImportReviewTint.Danger);
        rig.Session.FixableCells.ShouldBe(1);
        view.CleanUpButton.IsVisible.ShouldBeTrue();
        view.CleanUpButton.Words.PlainText.ShouldBe("Clean up 1 value");
        view.Subtitle.PlainText.ShouldBe("5 rows to fix · 17 ready");
    }

    [AvaloniaFact]
    public void Leave_out_removes_the_row_from_the_import()
    {
        using var rig = new Rig();
        var view = rig.View;
        var row = view.Table.RowFor(rig.Row(3)).ShouldNotBeNull();
        Click(row.LeaveOut);
        rig.Pump();

        rig.Row(3).IsRemoved.ShouldBeTrue();
        rig.ShownLines().ShouldNotContain(3);
        view.Filter.Issues.Count.ShouldBe(8);
        view.Filter.All.Count.ShouldBe(21);
        // The removed row still counts as left out, as it will in the summary.
        view.ImportText.ShouldBe("Import 13 contacts · 9 left out");
        rig.Session.SkippedRows.ShouldBe(9);

        // A clean row left out from "All" is not imported either.
        Click(view.Filter.All);
        rig.Pump();
        Click(view.Table.RowFor(rig.Row(2)).ShouldNotBeNull().LeaveOut);
        rig.Pump();
        rig.ShownLines().Length.ShouldBe(20);
        rig.Session.ReadyRows.ShouldBe(12);
        rig.Session.BuildRecords().Select(r => r.RowNumber).ShouldNotContain(2);
    }

    [AvaloniaFact]
    public void Leaving_out_the_focused_row_moves_the_focus_to_the_next_row()
    {
        using var rig = new Rig();
        var view = rig.View;
        var leaveOut = view.Table.RowFor(rig.Row(3)).ShouldNotBeNull().LeaveOut;
        leaveOut.Focus(NavigationMethod.Tab).ShouldBeTrue();
        rig.Press(PhysicalKey.Enter);
        view.Table.RowFor(rig.Row(6)).ShouldNotBeNull().LeaveOut.IsFocused.ShouldBeTrue();
    }

    [AvaloniaFact]
    public void Filter_switches_between_the_rows_to_fix_and_all_rows()
    {
        using var rig = new Rig();
        var view = rig.View;
        Click(view.Filter.All);
        rig.Pump();
        view.Filter.All.IsSelected.ShouldBeTrue();
        view.Filter.Issues.IsSelected.ShouldBeFalse();
        rig.ShownLines().ShouldBe(Enumerable.Range(2, 22).ToArray());

        Click(view.Filter.Issues);
        rig.Pump();
        view.Filter.ShowAll.ShouldBeFalse();
        rig.ShownLines().ShouldBe(IssueLines);
        view.Table.LeavingCount.ShouldBe(0);
    }

    [AvaloniaFact]
    public void Columns_fit_their_content_up_to_220_and_the_table_scrolls_sideways()
    {
        using var rig = new Rig();
        var view = rig.View;
        var widths = view.Table.Widths;
        widths.Length.ShouldBe(9);
        widths[0].ShouldBe(48);
        widths[^1].ShouldBe(40);
        widths.Skip(1).Take(7).ShouldAllBe(w => w <= 220.01);
        view.TableBox.Viewer.Extent.Width.ShouldBeGreaterThan(view.TableBox.Viewer.Viewport.Width);
    }

    [AvaloniaFact]
    public void The_table_region_scrolls_sideways_with_the_arrow_keys()
    {
        using var rig = new Rig();
        var viewer = rig.View.TableBox.Viewer;
        viewer.Focus(NavigationMethod.Tab).ShouldBeTrue();
        viewer.Offset.X.ShouldBe(0);
        rig.Press(PhysicalKey.ArrowRight);
        viewer.Offset.X.ShouldBeGreaterThan(0);
        rig.Press(PhysicalKey.End);
        viewer.Offset.X.ShouldBe(viewer.Extent.Width - viewer.Viewport.Width, 0.5);
        rig.Press(PhysicalKey.Home);
        viewer.Offset.X.ShouldBe(0);
    }

    [AvaloniaFact]
    public async Task The_table_follows_the_theme()
    {
        using var rig = new Rig();
        var table = rig.View.Table;
        var light = table.Palette.Foreground;
        await TestHelpers.ApplyAsync(TestHelpers.Dark);
        try
        {
            rig.Pump();
            table.Palette.Foreground.ShouldNotBe(light);
            table.Palette.Danger.ShouldBe(ArcTokens.GetColor(table, TKCode.Arc.DesignTokens.ArcDesignTokenKind.Danger));
        }
        finally
        {
            await TestHelpers.ApplyAsync(null);
        }
    }

    [AvaloniaFact]
    public void Few_fields_stretch_to_the_box_width_and_no_clean_up_shows_without_safe_fixes()
    {
        var wizard = new ImportWizard
        {
            TimingScale = 0,
            Targets =
            [
                new ImportTarget { Key = "name", Label = "Name", IsRequired = true, Validate = v => v.Length == 0 ? "Needs a name" : null },
                new ImportTarget { Key = "city", Label = "City" },
            ],
        };
        var host = new Border { Width = 760, Child = wizard, VerticalAlignment = VerticalAlignment.Top };
        var window = TestHelpers.Show(host);
        window.Width = 1000;
        window.Height = 1200;
        try
        {
            wizard.LoadCsv("Name,City\nAda,Lyon\n,Porto\n", "people.csv").ShouldBeTrue();
            wizard.GoTo(ImportWizardStep.Map);
            wizard.GoTo(ImportWizardStep.Review);
            for (var i = 0; i < 3; i++)
            {
                window.UpdateLayout();
                Dispatcher.UIThread.RunJobs();
            }
            var view = wizard.CurrentView.ShouldBeOfType<ImportWizardReviewView>();
            view.Subtitle.PlainText.ShouldBe("1 row to fix · 1 ready");
            view.CleanUpButton.IsVisible.ShouldBeFalse();
            view.Table.ShownRows.Single().LineNumber.ShouldBe(3);
            // CSS min-width: 100%: the two field columns share the rest of the box.
            view.Table.TotalWidth.ShouldBe(view.TableBox.Viewer.Viewport.Width, 1);
            view.Table.Widths[0].ShouldBe(48);
            view.Table.Widths[^1].ShouldBe(40);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Compact_layout_wraps_the_bar_when_needed_and_stretches_the_import_button()
    {
        using var rig = new Rig(width: 300);
        rig.Pump();
        var view = rig.View;
        rig.Wizard.IsCompact.ShouldBeTrue();
        view.ImportButton.HorizontalAlignment.ShouldBe(HorizontalAlignment.Stretch);
        view.ImportButton.Bounds.Right.ShouldBe(view.BackButton.Parent.ShouldBeOfType<Grid>().Bounds.Width, 1);
        view.TableBox.Viewer.Extent.Width.ShouldBeGreaterThan(view.TableBox.Viewer.Viewport.Width);
        // The button wraps under the filter exactly when the two do not fit on one line (12 apart).
        var filter = view.Filter.Bounds;
        var button = view.CleanUpButton.Bounds;
        var fits = filter.Width + 12 + button.Width <= view.Bar.Bounds.Width + 0.5;
        view.Bar.IsWrapped.ShouldBe(!fits);
        if (view.Bar.IsWrapped)
        {
            button.Top.ShouldBe(filter.Bottom + 12, 0.5);
            button.Left.ShouldBe(0);
        }
        else
        {
            button.Right.ShouldBe(view.Bar.Bounds.Width, 0.5);
        }

        using var wide = new Rig();
        wide.View.Bar.IsWrapped.ShouldBeFalse();
        wide.View.ImportButton.HorizontalAlignment.ShouldBe(HorizontalAlignment.Right);
        wide.View.CleanUpButton.Bounds.Right.ShouldBe(wide.View.Bar.Bounds.Width, 0.5);
    }

    [AvaloniaFact]
    public void New_labels_and_nouns_apply_to_the_shown_step()
    {
        using var rig = new Rig();
        rig.Wizard.Labels = new ImportWizardLabels
        {
            ReviewTitle = "Check rows",
            ReviewCount = _ => "{0} broken, {1} fine",
            FilterIssues = "Problems",
            FilterAll = "Everything",
            LineHeader = "#",
            LeaveOut = line => $"Skip {line}",
            CleanUp = n => "Tidy {0}",
        };
        rig.Wizard.Noun = "customer";
        rig.Wizard.PluralNoun = "customers";
        rig.Pump();
        var view = rig.View;
        view.Subtitle.PlainText.ShouldBe("9 broken, 13 fine");
        AutomationProperties.GetName(view.Filter.Issues).ShouldBe("Problems 9");
        AutomationProperties.GetName(view.Filter.All).ShouldBe("Everything 22");
        view.CleanUpButton.Words.PlainText.ShouldBe("Tidy 5");
        AutomationProperties.GetName(view.Table.RowFor(rig.Row(3)).ShouldNotBeNull().LeaveOut).ShouldBe("Skip 3");
        view.ImportText.ShouldBe("Import 13 customers · 9 left out");
    }

    [AvaloniaFact]
    public void Back_returns_to_mapping_and_the_review_keeps_its_fixes()
    {
        using var rig = new Rig();
        rig.Await(rig.View.CleanUpAsync());
        Click(rig.View.BackButton);
        rig.Pump();
        rig.Wizard.Step.ShouldBe(ImportWizardStep.Map);
        rig.Wizard.GoTo(ImportWizardStep.Review);
        rig.Pump();
        rig.View.Subtitle.PlainText.ShouldBe("4 rows to fix · 18 ready");
        rig.View.CleanUpButton.IsVisible.ShouldBeFalse();
        rig.ShownLines().ShouldBe([3, 9, 13, 17]);
    }

    [AvaloniaFact]
    public void Import_starts_with_the_ready_rows()
    {
        using var rig = new Rig();
        rig.Await(rig.View.CleanUpAsync());
        Click(rig.View.ImportButton);
        rig.Pump();
        rig.Wizard.Step.ShouldBe(ImportWizardStep.Import);
        rig.Wizard.RunRecords.Count.ShouldBe(18);
    }
}
