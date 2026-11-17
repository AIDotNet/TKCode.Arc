using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Threading;
using Shouldly;
using TKCode.Arc.Motion;
using Xunit;

namespace TKCode.Arc.Tests.Blocks.Developer;

/// <summary>
/// The "Match fields" step of <see cref="ImportWizard"/>: the matcher's scores on the sample file, what each confidence cell
/// shows (score, chosen, ignored, duplicate), the alert and the primary button, the reveal (once per file; reduced motion
/// at once), the compact rows and the names and Tab order for keyboard and screen reader users.
/// </summary>
public class ImportWizardMapTests
{
    private sealed class Rig
    {
        public Rig(double width = 760, ArcMotionLevel level = ArcMotionLevel.Off, double timing = 0)
        {
            Wizard = new ImportWizard { TimingScale = timing };
            Host = new Border { Width = width, Child = Wizard, VerticalAlignment = Avalonia.Layout.VerticalAlignment.Top };
            Window = TestHelpers.Show(Host, level);
            Window.Width = 1000;
            Window.Height = 1200;
            Pump();
        }

        public ImportWizard Wizard { get; }

        public Border Host { get; }

        public Window Window { get; }

        public void Pump()
        {
            for (var i = 0; i < 3; i++)
            {
                Window.UpdateLayout();
                Dispatcher.UIThread.RunJobs();
            }
        }

        public ImportWizardMapView OpenMap(string? csv = null)
        {
            Wizard.LoadCsv(csv ?? ImportWizardDefaults.SampleCsv, "contacts.csv").ShouldBeTrue();
            TestHelpers.PumpUntil(() => Wizard.FileName is not null, 5000).ShouldBeTrue();
            Wizard.GoTo(ImportWizardStep.Map);
            Pump();
            return Map;
        }

        public ImportWizardMapView Map => Wizard.CurrentView.ShouldBeOfType<ImportWizardMapView>();

        public DeveloperAnnouncer Alerts => TestHelpers.Descendant<DeveloperAnnouncer>(Wizard, ImportWizard.AlertAnnouncerPart);

        public void Choose(int row, int target)
        {
            Map.Rows[row].Select.SelectedIndex = target + 1;
            Pump();
        }
    }

    // ---- Matcher ----

    [Fact]
    public void Sample_columns_get_their_scores_and_comments_are_left_out()
    {
        var table = ImportWizardCsv.Parse(ImportWizardDefaults.SampleCsv);
        var matches = ImportWizardMatcher.Match(table.Headers, table.Rows, ImportWizardDefaults.CreateContactTargets().ToList());
        matches.Select(m => m.Confidence).ShouldBe([99, 99, 88, 64, 95, 99, 88, 0]);
        matches.Select(m => m.Target).ShouldBe([0, 1, 2, 3, 4, 5, 6, -1]);
    }

    // ---- Cells ----

    [AvaloniaFact]
    public void Map_shows_every_column_with_its_score_and_counts_the_mapped_ones()
    {
        var rig = new Rig();
        var map = rig.OpenMap();
        map.IsMatching.ShouldBeFalse();
        map.Rows.Count.ShouldBe(8);
        map.Rows.Select(r => r.Confidence.Result).ShouldBe(
        [
            ImportMapResult.Score, ImportMapResult.Score, ImportMapResult.Score, ImportMapResult.Score,
            ImportMapResult.Score, ImportMapResult.Score, ImportMapResult.Score, ImportMapResult.Ignored,
        ]);
        map.Rows.Take(7).Select(r => r.Confidence.Percent).ShouldBe([99, 99, 88, 64, 95, 99, 88]);
        map.Rows[0].NameText.Text.ShouldBe("Contact name");
        map.Rows[7].NameText.Text.ShouldBe("Comments");
        map.Rows[7].IsIgnoredShown.ShouldBeTrue();
        map.Rows[0].Select.SelectedIndex.ShouldBe(1);
        map.Rows[7].Select.SelectedIndex.ShouldBe(0);
        map.Count.PlainText.ShouldBe("7 of 8 columns mapped");
        map.NextButton.IsEnabled.ShouldBeTrue();
        map.AlertText.IsVisible.ShouldBeFalse();
        rig.Wizard.Announcer!.History.ShouldContain("7 of 8 columns mapped.");

        // Levels: 99 and 95 exact, 88 likely, 64 check; the meters fill to the percent.
        ImportWizardMatcher.LevelOf(map.Rows[2].Confidence.Percent).ShouldBe(ImportConfidenceLevel.Likely);
        ImportWizardMatcher.LevelOf(map.Rows[3].Confidence.Percent).ShouldBe(ImportConfidenceLevel.Check);
        map.Rows[0].Confidence.Shown!.Meter!.Fill.ShouldBe(0.99, 0.001);
        map.Rows[3].Confidence.Shown!.Meter!.Fill.ShouldBe(0.64, 0.001);
        map.Rows[7].Confidence.Shown!.Meter.ShouldBeNull();
    }

    [AvaloniaFact]
    public void Options_list_dont_import_then_every_field_with_required_marked()
    {
        var rig = new Rig();
        var map = rig.OpenMap();
        var options = map.Rows[0].Select.ItemsSource!.Cast<SelectOption>().Select(o => o.Label).ToArray();
        options.ShouldBe(["Don't import", "Name (required)", "Email (required)", "Company", "Role", "Phone", "Country", "Plan"]);
    }

    [AvaloniaFact]
    public void Choosing_fields_shows_chosen_ignored_and_duplicate()
    {
        var rig = new Rig();
        var map = rig.OpenMap();

        // Comments → Role, which Team role already has: both rows say Duplicate and the alert names the field.
        rig.Choose(7, 3);
        rig.Wizard.Session.Columns[7].Target.ShouldBe(3);
        map.Rows[7].Confidence.Result.ShouldBe(ImportMapResult.Duplicate);
        map.Rows[3].Confidence.Result.ShouldBe(ImportMapResult.Duplicate);
        map.Rows[7].IsIgnoredShown.ShouldBeFalse();
        map.Count.PlainText.ShouldBe("8 of 8 columns mapped");
        map.AlertText.IsVisible.ShouldBeTrue();
        map.AlertText.Text.ShouldBe("Role is chosen for two columns. Keep one.");
        map.NextButton.IsEnabled.ShouldBeFalse();
        rig.Alerts.History.ShouldContain("Role is chosen for two columns. Keep one.");

        // Team role → Don't import: it is ignored (muted) and Comments shows Chosen with a full meter.
        rig.Choose(3, -1);
        map.Rows[3].Confidence.Result.ShouldBe(ImportMapResult.Ignored);
        map.Rows[3].IsIgnoredShown.ShouldBeTrue();
        map.Rows[7].Confidence.Result.ShouldBe(ImportMapResult.Chosen);
        map.Rows[7].Confidence.Shown!.Meter!.Fill.ShouldBe(1, 0.001);
        AutomationProperties.GetName(map.Rows[7].Confidence).ShouldBe("Chosen");
        AutomationProperties.GetName(map.Rows[3].Confidence).ShouldBe("Ignored");
        map.Count.PlainText.ShouldBe("7 of 8 columns mapped");
        map.AlertText.IsVisible.ShouldBeFalse();
        map.NextButton.IsEnabled.ShouldBeTrue();

        // The proposed field again restores the matcher's percent.
        rig.Choose(7, -1);
        rig.Choose(3, 3);
        map.Rows[3].Confidence.Result.ShouldBe(ImportMapResult.Score);
        map.Rows[3].Confidence.Percent.ShouldBe(64);
        AutomationProperties.GetName(map.Rows[3].Confidence).ShouldBe("64% confidence");
        map.Rows[7].Confidence.Result.ShouldBe(ImportMapResult.Ignored);
    }

    [AvaloniaFact]
    public void Missing_required_field_blocks_and_alerts()
    {
        var rig = new Rig();
        var map = rig.OpenMap();
        rig.Choose(0, -1);
        rig.Wizard.Session.IsMappingValid.ShouldBeFalse();
        map.AlertText.IsVisible.ShouldBeTrue();
        map.AlertText.Text.ShouldBe("Choose a column for name.");
        map.NextButton.IsEnabled.ShouldBeFalse();
        rig.Alerts.History.ShouldContain("Choose a column for name.");

        // A required field missing wins over a duplicate (Contact name → Email).
        rig.Choose(0, 1);
        map.Rows[0].Confidence.Result.ShouldBe(ImportMapResult.Duplicate);
        map.AlertText.Text.ShouldBe("Choose a column for name.");

        rig.Choose(0, 0);
        map.AlertText.IsVisible.ShouldBeFalse();
        map.NextButton.IsEnabled.ShouldBeTrue();
        map.NextButton.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Avalonia.Controls.Button.ClickEvent));
        rig.Pump();
        rig.Wizard.Step.ShouldBe(ImportWizardStep.Review);
    }

    [AvaloniaFact]
    public void Back_returns_to_the_upload_step()
    {
        var rig = new Rig();
        var map = rig.OpenMap();
        map.BackButton.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Avalonia.Controls.Button.ClickEvent));
        rig.Pump();
        rig.Wizard.Step.ShouldBe(ImportWizardStep.Upload);
        rig.Wizard.FileName.ShouldNotBeNull();
    }

    // ---- Reveal ----

    [AvaloniaFact]
    public void First_visit_reveals_one_column_at_a_time_and_a_revisit_shows_the_results()
    {
        var rig = new Rig(level: ArcMotionLevel.Full, timing: 1);
        var map = rig.OpenMap();
        map.IsMatching.ShouldBeTrue();
        map.Rows[0].Confidence.Result.ShouldBe(ImportMapResult.Matching);
        map.Rows[7].Confidence.Result.ShouldBe(ImportMapResult.Matching);
        AutomationProperties.GetName(map.Rows[0].Confidence).ShouldBe("Matching");
        map.NextButton.IsEnabled.ShouldBeFalse();
        map.Count.PlainText.ShouldBe("0 of 8 columns mapped");

        // The first column resolves 270ms in; the rest follow (faster here).
        TestHelpers.PumpUntil(() => map.Rows[0].Confidence.Result != ImportMapResult.Matching, 5000).ShouldBeTrue();
        map.Rows[7].Confidence.Result.ShouldBe(ImportMapResult.Matching);
        map.NextButton.IsEnabled.ShouldBeFalse();
        rig.Wizard.TimingScale = 0.05;
        TestHelpers.PumpUntil(() => !map.IsMatching, 5000).ShouldBeTrue();
        rig.Pump();
        map.Rows.All(r => r.Confidence.Result != ImportMapResult.Matching).ShouldBeTrue();
        rig.Wizard.Session.Columns.All(c => c.IsRevealed).ShouldBeTrue();
        map.Count.PlainText.ShouldBe("7 of 8 columns mapped");
        map.NextButton.IsEnabled.ShouldBeTrue();
        rig.Wizard.Announcer!.History.ShouldContain("7 of 8 columns mapped.");

        rig.Wizard.GoTo(ImportWizardStep.Review);
        rig.Pump();
        rig.Wizard.GoTo(ImportWizardStep.Map);
        rig.Pump();
        var again = rig.Map;
        again.ShouldNotBeSameAs(map);
        again.IsMatching.ShouldBeFalse();
        again.Rows.All(r => r.Confidence.Result != ImportMapResult.Matching).ShouldBeTrue();
        again.Rows.All(r => !r.Confidence.IsMatchingVisible).ShouldBeTrue();
        again.Count.PlainText.ShouldBe("7 of 8 columns mapped");
        again.NextButton.IsEnabled.ShouldBeTrue();
    }

    [AvaloniaFact]
    public void Leaving_during_the_reveal_finishes_it_and_a_new_file_reveals_again()
    {
        var rig = new Rig(level: ArcMotionLevel.Full, timing: 1);
        var map = rig.OpenMap();
        map.IsMatching.ShouldBeTrue();
        rig.Wizard.GoTo(ImportWizardStep.Upload);
        rig.Pump();
        rig.Wizard.Session.Columns.All(c => c.IsRevealed).ShouldBeTrue();
        rig.Wizard.GoTo(ImportWizardStep.Map);
        rig.Pump();
        rig.Map.IsMatching.ShouldBeFalse();

        rig.Wizard.TimingScale = 0;
        var fresh = rig.OpenMap("Full name,Email\nAda Quill,ada@quill.example\n");
        rig.Wizard.Session.Columns.All(c => c.IsRevealed).ShouldBeTrue();
        fresh.Rows.Count.ShouldBe(2);
        fresh.Rows.Select(r => r.Confidence.Percent).ShouldBe([99, 99]);
    }

    [AvaloniaFact]
    public void Reduced_motion_resolves_every_column_at_once_and_the_meters_still_fill()
    {
        var rig = new Rig(level: ArcMotionLevel.Reduced, timing: 1);
        var map = rig.OpenMap();
        map.IsMatching.ShouldBeFalse();
        map.Rows.All(r => r.Confidence.Result != ImportMapResult.Matching).ShouldBeTrue();
        map.NextButton.IsEnabled.ShouldBeTrue();
        // The fill heads to the percent (a spring-shaped tween with reduced motion).
        map.Rows[0].Confidence.Shown!.Meter!.Target.ShouldBe(0.99, 0.001);
    }

    // ---- Layout ----

    [AvaloniaFact]
    public void Compact_rows_stack_the_select_under_the_source_and_hide_the_arrow()
    {
        var rig = new Rig(width: 468);
        var map = rig.OpenMap();
        rig.Pump();
        rig.Wizard.IsCompact.ShouldBeTrue();
        var row = map.Rows[0];
        row.Arrow.IsVisible.ShouldBeFalse();
        row.Confidence.Bounds.Width.ShouldBe(84, 0.5);
        var name = row.NameText.TranslatePoint(default, row)!.Value;
        var select = row.Select.TranslatePoint(default, row)!.Value;
        select.Y.ShouldBeGreaterThan(name.Y + 30);

        rig.Host.Width = 760;
        TestHelpers.PumpUntil(() => !rig.Wizard.IsCompact, 2000).ShouldBeTrue();
        rig.Pump();
        row.Arrow.IsVisible.ShouldBeTrue();
        row.Confidence.Bounds.Width.ShouldBe(92, 0.5);
        var wideName = row.NameText.TranslatePoint(default, row)!.Value;
        var wideSelect = row.Select.TranslatePoint(default, row)!.Value;
        wideSelect.X.ShouldBeGreaterThan(wideName.X + 200);
    }

    [AvaloniaFact]
    public void Wide_rows_match_arc_geometry()
    {
        var rig = new Rig();
        var map = rig.OpenMap();
        rig.Pump();
        var row = map.Rows[1];
        row.Select.Bounds.Height.ShouldBe(34, 0.5);
        row.Confidence.Bounds.Width.ShouldBe(92, 0.5);
        row.Confidence.Shown!.Meter!.Bounds.Size.ShouldBe(new Avalonia.Size(28, 4));
        row.Arrow.IsVisible.ShouldBeTrue();
    }

    // ---- Keyboard and names ----

    [AvaloniaFact]
    public void Selects_are_named_after_their_columns_and_tab_runs_down_the_list_to_the_footer()
    {
        var rig = new Rig();
        var map = rig.OpenMap();
        AutomationProperties.GetName(map.Rows[0].Select).ShouldBe("Field for Contact name");
        AutomationProperties.GetName(map.Rows[4].Select).ShouldBe("Field for Cell");
        AutomationProperties.GetName(map.Rows[0].Confidence).ShouldBe("99% confidence");

        map.Rows[0].Select.Focus(NavigationMethod.Tab).ShouldBeTrue();
        for (var i = 1; i < map.Rows.Count; i++)
        {
            rig.Window.KeyPressQwerty(PhysicalKey.Tab, RawInputModifiers.None);
            rig.Pump();
            rig.Window.FocusManager!.GetFocusedElement().ShouldBeSameAs(map.Rows[i].Select);
        }
        rig.Window.KeyPressQwerty(PhysicalKey.Tab, RawInputModifiers.None);
        rig.Pump();
        rig.Window.FocusManager!.GetFocusedElement().ShouldBeSameAs(map.BackButton);
        rig.Window.KeyPressQwerty(PhysicalKey.Tab, RawInputModifiers.None);
        rig.Pump();
        rig.Window.FocusManager!.GetFocusedElement().ShouldBeSameAs(map.NextButton);
    }

    [AvaloniaFact]
    public void Keyboard_picks_a_field_from_the_select()
    {
        var rig = new Rig();
        var map = rig.OpenMap();
        var select = map.Rows[7].Select;
        select.Focus(NavigationMethod.Tab).ShouldBeTrue();
        // Closed select: Down opens it on the chosen option; Down moves on, Enter picks.
        rig.Window.KeyPressQwerty(PhysicalKey.ArrowDown, RawInputModifiers.None);
        rig.Pump();
        select.IsOpen.ShouldBeTrue();
        rig.Window.KeyPressQwerty(PhysicalKey.ArrowDown, RawInputModifiers.None);
        rig.Window.KeyPressQwerty(PhysicalKey.Enter, RawInputModifiers.None);
        rig.Pump();
        select.IsOpen.ShouldBeFalse();
        rig.Wizard.Session.Columns[7].Target.ShouldBe(0);
        map.Rows[7].Confidence.Result.ShouldBe(ImportMapResult.Duplicate);
    }

    [AvaloniaFact]
    public void New_labels_reword_the_step()
    {
        var rig = new Rig();
        var map = rig.OpenMap();
        rig.Wizard.Labels = new ImportWizardLabels
        {
            MapTitle = "Felder zuordnen",
            DoNotImport = "Nicht importieren",
            FieldFor = column => $"Feld für {column}",
            Ignored = "Ausgelassen",
            MappedCount = _ => "{0} von {1} Spalten zugeordnet",
        };
        rig.Pump();
        map.Count.PlainText.ShouldBe("7 von 8 Spalten zugeordnet");
        AutomationProperties.GetName(map.Rows[7].Select).ShouldBe("Feld für Comments");
        AutomationProperties.GetName(map.Rows[7].Confidence).ShouldBe("Ausgelassen");
        map.Rows[7].Select.SelectedIndex.ShouldBe(0);
        ((SelectOption)map.Rows[7].Select.SelectedItem!).Label.ShouldBe("Nicht importieren");
        rig.Wizard.Session.Columns[7].Target.ShouldBe(-1);
        rig.Wizard.Session.Columns[0].Target.ShouldBe(0);
    }
}
