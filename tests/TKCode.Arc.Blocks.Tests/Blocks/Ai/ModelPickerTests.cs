using TKCode.Arc.Motion;
using TKCode.Arc.Tests.CorePrimitives;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Threading;
using Avalonia.VisualTree;

namespace TKCode.Arc.Tests.Blocks.Ai;

/// <summary>The model picker: the pill, the menu and the summary wired together.</summary>
public class ModelPickerTests
{
    private sealed class Run : IDisposable
    {
        public Run(ModelPicker picker, Window window)
        {
            Picker = picker;
            Window = window;
        }

        public ModelPicker Picker { get; }
        public Window Window { get; }
        public ModelPill Pill => Picker.Pill!;
        public ModelSummary Summary => Picker.Summary!;
        public ModelMenu Menu => Picker.Menu!;

        public void Pump(int rounds = 3)
        {
            for (var i = 0; i < rounds; i++)
            {
                Dispatcher.UIThread.RunJobs();
                AvaloniaHeadlessPlatform.ForceRenderTimerTick();
            }
            Dispatcher.UIThread.RunJobs();
        }

        public void Key(PhysicalKey key)
        {
            Window.KeyPressQwerty(key, RawInputModifiers.None);
            Window.KeyReleaseQwerty(key, RawInputModifiers.None);
            Pump();
        }

        public void PressPill()
        {
            var point = Pill.TranslatePoint(new Point(Pill.Bounds.Width / 2, Pill.Bounds.Height / 2), Window)!.Value;
            Window.MouseMove(point);
            Pump();
            Window.MouseDown(point, MouseButton.Left);
            Window.MouseUp(point, MouseButton.Left);
            Pump();
        }

        public void Dispose() => Window.Close();
    }

    private static Run Show(Action<ModelPicker>? configure = null, ArcMotionLevel level = ArcMotionLevel.Off, double width = 900)
    {
        var picker = new ModelPicker { HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Top, Margin = new Thickness(0, 40, 0, 0) };
        foreach (var provider in ModelMenuSamples.Providers())
        {
            picker.Providers.Add(provider);
        }
        foreach (var model in ModelMenuSamples.Models())
        {
            picker.Models.Add(model);
        }
        configure?.Invoke(picker);
        var window = TestHelpers.Show(picker, level);
        window.Width = width;
        window.Height = 720;
        var run = new Run(picker, window);
        run.Pump();
        return run;
    }

    [AvaloniaFact]
    public void The_Template_Has_A_Pill_A_Summary_And_A_Menu_And_Selects_The_First_Featured_Model()
    {
        using var run = Show();
        run.Pill.ShouldNotBeNull();
        run.Summary.ShouldNotBeNull();
        run.Menu.ShouldNotBeNull();
        run.Picker.SelectedModel!.Id.ShouldBe("aurora-prime-3");
        run.Picker.SelectedModelId.ShouldBe("aurora-prime-3");
        run.Picker.Effort.ShouldBe(ModelEffort.Medium);
        run.Pill.Model.ShouldBe(run.Picker.SelectedModel);
        run.Pill.Effort.ShouldBe(ModelEffort.Medium);
        run.Summary.Model.ShouldBe(run.Picker.SelectedModel);
        run.Summary.Effort.ShouldBe(ModelEffort.Medium);
        run.Picker.IsOpen.ShouldBeFalse();
    }

    [AvaloniaFact]
    public void The_Stage_Is_At_Most_440_Wide_And_The_Pill_Sits_Above_The_Summary()
    {
        using var run = Show();
        run.Picker.Bounds.Width.ShouldBe(440, 0.5);
        run.Pill.Bounds.Bottom.ShouldBeLessThan(run.Summary.Bounds.Top);
        // 24 px between the pill and the summary.
        (run.Summary.Bounds.Top - run.Pill.Bounds.Bottom).ShouldBe(24, 0.5);
        run.Pill.TranslatePoint(new Point(run.Pill.Bounds.Width / 2, 0), run.Picker)!.Value.X.ShouldBe(220, 1.5);
    }

    [AvaloniaFact]
    public void A_Selected_Model_Id_Is_Resolved_From_The_Catalog_Even_When_It_Arrives_Late()
    {
        using var run = Show(p =>
        {
            p.Models.Clear();
            p.SelectedModelId = "cobalt-atlas-2";
        });
        run.Picker.SelectedModel.ShouldBeNull();
        run.Picker.SelectedModelId.ShouldBe("cobalt-atlas-2");
        run.Picker.Models.AddRange(ModelMenuSamples.Models());
        run.Pump();
        run.Picker.SelectedModel!.Id.ShouldBe("cobalt-atlas-2");
        run.Pill.Model!.Id.ShouldBe("cobalt-atlas-2");
        run.Picker.Effort.ShouldBe(ModelEffort.Medium);
    }

    [AvaloniaFact]
    public void Assigning_A_Model_In_Code_Updates_Every_Part_Snaps_The_Effort_And_Says_Nothing()
    {
        using var run = Show();
        run.Picker.Effort = ModelEffort.Max;
        run.Pump();
        run.Picker.Effort.ShouldBe(ModelEffort.Max);
        var models = new List<ModelPickerModelChangedEventArgs>();
        var efforts = new List<ModelPickerEffortChangedEventArgs>();
        run.Picker.ModelChanged += (_, e) => models.Add(e);
        run.Picker.EffortChanged += (_, e) => efforts.Add(e);

        var cobalt = run.Picker.Models.Single(m => m.Id == "cobalt-atlas-2");
        run.Picker.SelectedModel = cobalt;
        run.Pump();

        run.Pill.Model.ShouldBe(cobalt);
        run.Summary.Model.ShouldBe(cobalt);
        run.Menu.SelectedModel.ShouldBe(cobalt);
        run.Picker.SelectedModelId.ShouldBe("cobalt-atlas-2");
        run.Picker.Effort.ShouldBe(ModelEffort.High); // Cobalt offers Low, Medium, High
        run.Pill.Effort.ShouldBe(ModelEffort.High);
        models.Count.ShouldBe(1);
        models[0].NewModel.ShouldBe(cobalt);
        models[0].OldModel!.Id.ShouldBe("aurora-prime-3");
        efforts.Count.ShouldBe(1);
        efforts[0].IsSnap.ShouldBeTrue();
        efforts[0].OldEffort.ShouldBe(ModelEffort.Max);
        efforts[0].NewEffort.ShouldBe(ModelEffort.High);
        // A change in code is not a person's choice: no confirmation, nothing announced.
        run.Summary.Notice.ShouldBeNull();
        run.Picker.LastAnnouncement.ShouldBeNull();
    }

    [AvaloniaFact]
    public void A_Model_Without_Effort_Has_No_Effort_And_An_Unoffered_Level_Is_Corrected_In_Code()
    {
        using var run = Show();
        var efforts = new List<ModelPickerEffortChangedEventArgs>();
        run.Picker.EffortChanged += (_, e) => efforts.Add(e);

        run.Picker.SelectedModel = run.Picker.Models.Single(m => m.Id == "cobalt-atlas-lite"); // no levels
        run.Pump();
        run.Picker.Effort.ShouldBeNull();
        run.Pill.Effort.ShouldBeNull();
        efforts.Last().NewEffort.ShouldBeNull();

        run.Picker.SelectedModel = run.Picker.Models.Single(m => m.Id == "cobalt-atlas-2"); // Low, Medium, High
        run.Pump();
        efforts.Clear();
        run.Picker.Effort = ModelEffort.Max; // the model does not offer it
        run.Pump();
        run.Picker.Effort.ShouldBe(ModelEffort.High);
        efforts.Count.ShouldBe(1);
        efforts[0].NewEffort.ShouldBe(ModelEffort.High);
        efforts[0].IsSnap.ShouldBeFalse();
    }

    [AvaloniaFact]
    public void Pressing_The_Pill_Opens_The_Menu_And_Pressing_It_Again_Closes_It()
    {
        using var run = Show();
        run.PressPill();
        run.Picker.IsOpen.ShouldBeTrue();
        run.Menu.IsOpen.ShouldBeTrue();
        run.Pill.IsOpen.ShouldBeTrue();
        run.Picker.Classes.ShouldContain(ModelPicker.OpenPseudoClass);

        run.PressPill();
        run.Picker.IsOpen.ShouldBeFalse();
        run.Pill.IsOpen.ShouldBeFalse();
        run.Picker.Classes.ShouldNotContain(ModelPicker.OpenPseudoClass);
    }

    [AvaloniaFact]
    public void IsOpen_Is_Two_Way_With_The_Menu()
    {
        using var run = Show();
        run.Picker.IsOpen = true;
        run.Pump();
        run.Menu.IsOpen.ShouldBeTrue();
        run.Pill.IsOpen.ShouldBeTrue();

        run.Picker.IsOpen = false;
        run.Pump();
        run.Menu.IsOpen.ShouldBeFalse();

        run.Picker.Open();
        run.Pump();
        run.Picker.IsOpen.ShouldBeTrue();
        run.Menu.Close(returnFocus: false);
        run.Pump();
        run.Picker.IsOpen.ShouldBeFalse();
        run.Picker.Open(ModelMenuOpenFocus.Selected);
        run.Pump();
        run.Picker.IsOpen.ShouldBeTrue();
        run.Picker.Close();
        run.Pump();
        run.Picker.IsOpen.ShouldBeFalse();
    }

    [AvaloniaFact]
    public void A_Picker_Declared_Open_Opens_Once_It_Is_Shown()
    {
        using var run = Show(p => p.IsOpen = true);
        run.Pump(6);
        run.Menu.IsOpen.ShouldBeTrue();
        run.Picker.IsOpen.ShouldBeTrue();
    }

    [AvaloniaFact]
    public void Choosing_A_Model_With_The_Keyboard_Raises_The_Event_Confirms_Announces_And_Returns_Focus_To_The_Pill()
    {
        using var run = Show();
        var changes = new List<ModelPickerModelChangedEventArgs>();
        run.Picker.ModelChanged += (_, e) => changes.Add(e);
        run.Pill.Focus(NavigationMethod.Tab);
        run.Pump();

        run.Key(PhysicalKey.Enter);          // opens with the selected model highlighted
        run.Picker.IsOpen.ShouldBeTrue();
        run.Key(PhysicalKey.ArrowDown);      // Aurora Swift 3
        run.Key(PhysicalKey.Enter);

        run.Picker.SelectedModel!.Id.ShouldBe("aurora-swift-3");
        changes.Count.ShouldBe(1);
        changes[0].OldModel!.Id.ShouldBe("aurora-prime-3");
        changes[0].NewModel!.Id.ShouldBe("aurora-swift-3");
        run.Picker.IsOpen.ShouldBeFalse();
        // Swift 3 offers five levels, so Medium stays: the confirmation names only the two models.
        run.Summary.Notice.ShouldBe("Aurora Prime 3 → Aurora Swift 3");
        run.Picker.LastAnnouncement.ShouldBe("Now using Aurora Swift 3, medium effort");
        run.Pill.IsKeyboardFocusWithin.ShouldBeTrue();
        run.Pill.Classes.ShouldContain(":focus-visible");
    }

    [AvaloniaFact]
    public void A_Model_Choice_That_Moves_The_Effort_Adds_The_Adjustment_To_The_Same_Confirmation()
    {
        using var run = Show(p => p.Effort = ModelEffort.Max);
        run.Pump();
        var efforts = new List<ModelPickerEffortChangedEventArgs>();
        run.Picker.EffortChanged += (_, e) => efforts.Add(e);
        run.Pill.Focus(NavigationMethod.Tab);
        run.Key(PhysicalKey.Enter);
        run.Key(PhysicalKey.ArrowDown);
        run.Key(PhysicalKey.ArrowDown);      // Cobalt Atlas 2: Low, Medium, High
        run.Key(PhysicalKey.Enter);

        run.Picker.SelectedModel!.Id.ShouldBe("cobalt-atlas-2");
        run.Picker.Effort.ShouldBe(ModelEffort.High);
        efforts.Count.ShouldBe(1);
        efforts[0].IsSnap.ShouldBeTrue();
        run.Summary.Notice.ShouldBe("Aurora Prime 3 → Cobalt Atlas 2 · Effort adjusted to High");
        run.Picker.LastAnnouncement.ShouldBe("Now using Cobalt Atlas 2, high effort");
    }

    [AvaloniaFact]
    public void Choosing_An_Effort_Level_Raises_The_Event_And_Confirms_It()
    {
        using var run = Show();
        var efforts = new List<ModelPickerEffortChangedEventArgs>();
        run.Picker.EffortChanged += (_, e) => efforts.Add(e);
        run.Pill.Focus(NavigationMethod.Tab);
        run.Key(PhysicalKey.Enter);
        for (var i = 0; i < 4; i++)
        {
            run.Key(PhysicalKey.ArrowDown);  // past the four featured models to the Effort row
        }
        run.Key(PhysicalKey.ArrowRight);     // the submenu, on the selected level (Medium)
        run.Key(PhysicalKey.ArrowDown);      // High
        run.Key(PhysicalKey.Enter);

        run.Picker.Effort.ShouldBe(ModelEffort.High);
        efforts.Count.ShouldBe(1);
        efforts[0].OldEffort.ShouldBe(ModelEffort.Medium);
        efforts[0].NewEffort.ShouldBe(ModelEffort.High);
        efforts[0].IsSnap.ShouldBeFalse();
        run.Picker.SelectedModel!.Id.ShouldBe("aurora-prime-3");
        run.Summary.Notice.ShouldBe("Effort: Medium → High");
        run.Picker.LastAnnouncement.ShouldBe("Aurora Prime 3, high effort");
        run.Picker.IsOpen.ShouldBeFalse();
    }

    [AvaloniaFact]
    public void Choosing_The_Model_Already_Selected_Closes_The_Menu_And_Changes_Nothing()
    {
        using var run = Show();
        var raised = 0;
        run.Picker.ModelChanged += (_, _) => raised++;
        run.Pill.Focus(NavigationMethod.Tab);
        run.Key(PhysicalKey.Enter);
        run.Key(PhysicalKey.Enter);          // the highlighted row is the selected model
        run.Picker.IsOpen.ShouldBeFalse();
        raised.ShouldBe(0);
        run.Summary.Notice.ShouldBeNull();
        run.Picker.LastAnnouncement.ShouldBeNull();
    }

    [AvaloniaFact]
    public void The_Select_And_Effort_Commands_Run_With_The_New_Values()
    {
        var models = new List<object?>();
        var efforts = new List<object?>();
        using var run = Show(p =>
        {
            p.SelectCommand = new RelayCommand(models.Add);
            p.EffortCommand = new RelayCommand(efforts.Add);
        });
        run.Picker.SelectedModel = run.Picker.Models[1];
        run.Pump();
        models.Count.ShouldBe(1);
        ((AiModel)models[0]!).Id.ShouldBe("aurora-swift-3");
        run.Picker.Effort = ModelEffort.Low;
        run.Pump();
        efforts.Last().ShouldBe(ModelEffort.Low);
    }

    private sealed class RelayCommand(Action<object?> execute) : System.Windows.Input.ICommand
    {
        public event EventHandler? CanExecuteChanged { add { } remove { } }

        public bool CanExecute(object? parameter) => true;

        public void Execute(object? parameter) => execute(parameter);
    }

    [AvaloniaFact]
    public void Without_A_Summary_The_Picker_Is_Only_The_Pill_And_Still_Announces()
    {
        using var run = Show(p => p.IsSummaryVisible = false);
        run.Summary.IsVisible.ShouldBeFalse();
        run.Picker.Classes.ShouldContain(ModelPicker.SummaryHiddenPseudoClass);
        run.Picker.Bounds.Height.ShouldBeLessThan(60);

        run.Pill.Focus(NavigationMethod.Tab);
        run.Key(PhysicalKey.Enter);
        run.Key(PhysicalKey.ArrowDown);
        run.Key(PhysicalKey.Enter);
        run.Summary.Notice.ShouldBeNull();
        run.Picker.LastAnnouncement.ShouldBe("Now using Aurora Swift 3, medium effort");

        run.Picker.IsSummaryVisible = true;
        run.Pump();
        run.Summary.IsVisible.ShouldBeTrue();
        run.Picker.Classes.ShouldNotContain(ModelPicker.SummaryHiddenPseudoClass);
    }

    [AvaloniaFact]
    public void Settings_Reach_The_Parts()
    {
        using var run = Show(p =>
        {
            p.SummaryNote = "Prices in USD per 1M tokens · demo values";
            p.CurrencySymbol = "€";
            p.NoticeDuration = TimeSpan.FromSeconds(5);
            p.PagedMenuWidth = 480;
        });
        run.Summary.Note.ShouldBe("Prices in USD per 1M tokens · demo values");
        run.Summary.CurrencySymbol.ShouldBe("€");
        run.Summary.NoticeDuration.ShouldBe(TimeSpan.FromSeconds(5));
        run.Menu.PagedBelowWidth.ShouldBe(480);

        var labels = new ModelPickerLabels
        {
            ModelNotice = (a, b) => $"{a} wird {b}",
            Pill = new ModelPillLabels { AutomationNameEmpty = _ => "Modell wählen" }
        };
        run.Picker.Labels = labels;
        run.Pump();
        run.Pill.Labels.ShouldBe(labels.Pill);
        run.Summary.Labels.ShouldBe(labels.Summary);
        run.Menu.Labels.ShouldBe(labels.Menu);
    }

    [AvaloniaFact]
    public void The_Catalog_Providers_And_Featured_Ids_Follow_Changes()
    {
        using var run = Show();
        run.Menu.FeaturedModels.Select(m => m.Id).ShouldBe(["aurora-prime-3", "aurora-swift-3", "cobalt-atlas-2", "halcyon-wide-15"]);
        run.Picker.FeaturedIds.Add("granite-70b");
        run.Picker.FeaturedIds.Add("ember-large-3");
        run.Pump();
        run.Menu.FeaturedModels.Select(m => m.Id).ShouldBe(["granite-70b", "ember-large-3"]);
        run.Menu.OtherModels.Count.ShouldBe(ModelMenuSamples.Models().Length - 2);

        run.Picker.Providers.Clear();
        run.Pump();
        run.Menu.Providers.ShouldBeEmpty();

        var source = new Avalonia.Collections.AvaloniaList<AiModel>(ModelMenuSamples.Models().Take(3));
        run.Picker.ModelsSource = source;
        run.Picker.FeaturedIds.Clear();
        run.Pump();
        run.Menu.FeaturedModels.Count.ShouldBe(3);
        source.Add(ModelMenuSamples.Models()[5]);
        run.Pump();
        run.Menu.FeaturedModels.Count.ShouldBe(4);
    }

    [AvaloniaFact]
    public void Leaving_The_Window_Closes_The_Menu()
    {
        using var run = Show();
        run.Picker.Open();
        run.Pump();
        run.Menu.IsOpen.ShouldBeTrue();
        var host = (Control)run.Window.Content!;
        run.Window.Content = null;
        run.Pump();
        run.Menu.IsOpen.ShouldBeFalse();
        run.Window.Content = host;
    }

    [AvaloniaFact]
    public void Full_Motion_Shows_The_Confirmation_And_Settles_Without_Errors()
    {
        var clock = new ManualFrameClock();
        using var _ = clock.Install();
        using var run = Show(level: ArcMotionLevel.Full);
        run.Pill.Focus(NavigationMethod.Tab);
        run.Key(PhysicalKey.Enter);
        run.Key(PhysicalKey.ArrowDown);
        run.Key(PhysicalKey.Enter);
        CoreFrames.Run(clock, 60);
        run.Pump();
        run.Picker.SelectedModel!.Id.ShouldBe("aurora-swift-3");
        run.Summary.Notice.ShouldBe("Aurora Prime 3 → Aurora Swift 3");
    }
}
