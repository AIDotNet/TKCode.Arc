using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using TKCode.Arc.Motion;
using TKCode.Arc.Primitives;
using TKCode.Arc.Tests.CorePrimitives;
using Avalonia;
using Avalonia.Animation;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Automation.Provider;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;

namespace TKCode.Arc.Tests.Blocks.Ai;

/// <summary>
/// The filter pills of the memory card: building the pills, choosing by pointer and keys (auto-activation, wrap,
/// Home / End), the two-way key and its fallback, the roving tab stop, concealing, the gliding background, sideways
/// scrolling with edge fades, and what screen readers get. Headless text advances 14 DIP a character.
/// </summary>
public class PillFilterBarTests
{
    private static List<PillFilterOption> Groups() =>
    [
        new(null, "All"),
        new("about", "About you"),
        new("preferences", "Preferences"),
        new("projects", "Projects"),
        new("people", "People")
    ];

    private static PillFilterBar Create(string? selected = null, IList<PillFilterOption>? options = null) =>
        new() { Options = options ?? Groups(), SelectedKey = selected, AccessibleName = "Filter memories" };

    private static Window Show(Control content, ArcMotionLevel level = ArcMotionLevel.Off)
    {
        var window = TestHelpers.Show(new StackPanel { Margin = new Thickness(20), Children = { content } }, level);
        window.Width = 760;
        Settle(window);
        return window;
    }

    private static void Settle(Window window)
    {
        window.UpdateLayout();
        AvaloniaHeadlessPlatform.ForceRenderTimerTick();
        Dispatcher.UIThread.RunJobs();
        window.UpdateLayout();
    }

    private static Point CenterOf(Visual visual, Window window) =>
        visual.TranslatePoint(new Point(visual.Bounds.Width / 2, visual.Bounds.Height / 2), window)!.Value;

    private static void Click(Window window, Visual visual)
    {
        var point = CenterOf(visual, window);
        window.MouseMove(point);
        window.MouseDown(point, MouseButton.Left);
        window.MouseUp(point, MouseButton.Left);
        Settle(window);
    }

    private static void Key(Window window, PhysicalKey key, RawInputModifiers modifiers = RawInputModifiers.None)
    {
        window.KeyPressQwerty(key, modifiers);
        window.KeyReleaseQwerty(key, modifiers);
        Settle(window);
    }

    private static Border FocusRing(PillFilterButton pill) => TestHelpers.Descendant<Border>(pill, "PART_FocusRing");

    private static string?[] Keys(PillFilterBar bar) => bar.Pills.Select(p => p.Key).ToArray();

    /// <summary>Button peers anywhere under <paramref name="peer"/>.</summary>
    private static int Buttons(AutomationPeer peer) =>
        peer.GetChildren().Sum(child => (child.GetAutomationControlType() == AutomationControlType.Button ? 1 : 0) + Buttons(child));

    private sealed class Model : INotifyPropertyChanged
    {
        private string? _key;

        public string? Key
        {
            get => _key;
            set
            {
                _key = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Key)));
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
    }

    #region Pills

    [AvaloniaFact]
    public void Builds_One_Pill_Per_Option_And_Marks_The_Selected_One()
    {
        var bar = Create("preferences");
        var window = Show(bar);
        try
        {
            bar.Pills.Select(p => p.Label).ShouldBe(["All", "About you", "Preferences", "Projects", "People"]);
            Keys(bar).ShouldBe([null, "about", "preferences", "projects", "people"]);
            bar.SelectedPill.ShouldBeSameAs(bar.Pills[2]);
            for (var i = 0; i < 5; i++)
            {
                bar.Pills[i].IsSelected.ShouldBe(i == 2);
                bar.Pills[i].Classes.Contains(PillFilterButton.SelectedPseudoClass).ShouldBe(i == 2);
            }
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void A_Null_Key_Selects_The_All_Pill()
    {
        var bar = Create();
        var window = Show(bar);
        try
        {
            bar.SelectedKey.ShouldBeNull();
            bar.SelectedPill.ShouldBeSameAs(bar.Pills[0]);
            bar.Pills[0].Label.ShouldBe("All");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Geometry_Is_32px_Pills_2px_Apart_In_A_34px_Row_With_12px_Padding()
    {
        var bar = Create();
        var window = Show(bar);
        try
        {
            bar.Bounds.Height.ShouldBe(34);
            var pills = bar.Pills;
            foreach (var pill in pills)
            {
                pill.Bounds.Height.ShouldBe(32);
                pill.Bounds.Top.ShouldBe(0);
                var label = TestHelpers.Descendant<TextBlock>(pill, "PART_Label");
                pill.Bounds.Width.ShouldBe(label.Bounds.Width + 24, 0.01);
                label.Bounds.Left.ShouldBe(12, 0.01);
            }
            for (var i = 1; i < pills.Count; i++)
            {
                pills[i].Bounds.Left.ShouldBe(pills[i - 1].Bounds.Right + 2, 0.01);
            }
            var track = TestHelpers.Descendant<PillFilterTrack>(bar);
            track.TranslatePoint(default, bar)!.Value.Y.ShouldBe(1);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Widths_Do_Not_Change_When_The_Selection_Moves()
    {
        var bar = Create();
        var window = Show(bar);
        try
        {
            var before = bar.Pills.Select(p => p.Bounds.Width).ToArray();
            bar.Pills[0].FontWeight.ShouldBe(bar.Pills[2].FontWeight);
            bar.SelectedKey = "preferences";
            Settle(window);
            bar.Pills.Select(p => p.Bounds.Width).ToArray().ShouldBe(before);
            bar.Pills[0].FontWeight.ShouldBe(bar.Pills[2].FontWeight);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Replacing_The_Options_Reuses_The_Pill_Controls()
    {
        var bar = Create("projects");
        var window = Show(bar);
        try
        {
            var first = bar.Pills.ToArray();
            bar.Options = [new(null, "Everything"), new("about", "You"), new("projects", "Work")];
            Settle(window);
            bar.Pills.Count.ShouldBe(3);
            bar.Pills[0].ShouldBeSameAs(first[0]);
            bar.Pills[2].ShouldBeSameAs(first[2]);
            bar.Pills.Select(p => p.Label).ShouldBe(["Everything", "You", "Work"]);
            TestHelpers.Descendant<PillFilterTrack>(bar).Children.OfType<PillFilterButton>().Count().ShouldBe(3);
            bar.SelectedPill!.Key.ShouldBe("projects");
            bar.SelectedKey.ShouldBe("projects");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void An_Observable_List_Rebuilds_The_Pills_When_Its_Content_Changes()
    {
        var options = new ObservableCollection<PillFilterOption>(Groups());
        var bar = Create("people", options);
        var window = Show(bar);
        try
        {
            options.Insert(4, new("places", "Places"));
            Settle(window);
            Keys(bar).ShouldBe([null, "about", "preferences", "projects", "places", "people"]);
            bar.SelectedPill!.Label.ShouldBe("People");
            options[1] = new("about", "You");
            bar.Pills[1].Label.ShouldBe("You");
        }
        finally
        {
            window.Close();
        }
    }

    #endregion

    #region Choosing

    [AvaloniaFact]
    public void Clicking_A_Pill_Selects_It_And_Raises_SelectionChanged()
    {
        var bar = Create();
        var raised = 0;
        bar.SelectionChanged += (_, e) =>
        {
            raised++;
            e.Source.ShouldBeSameAs(bar);
            bar.SelectedKey.ShouldBe("projects");
        };
        var window = Show(bar);
        try
        {
            Click(window, bar.Pills[3]);
            bar.SelectedKey.ShouldBe("projects");
            bar.SelectedPill.ShouldBeSameAs(bar.Pills[3]);
            raised.ShouldBe(1);
            // A pointer press focuses the pill without a ring.
            bar.Pills[3].IsFocused.ShouldBeTrue();
            FocusRing(bar.Pills[3]).IsVisible.ShouldBeFalse();

            // The selected pill again: nothing changes, nothing is raised.
            Click(window, bar.Pills[3]);
            raised.ShouldBe(1);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Code_And_Bindings_Do_Not_Raise_SelectionChanged()
    {
        var bar = Create();
        var raised = 0;
        bar.SelectionChanged += (_, _) => raised++;
        var window = Show(bar);
        try
        {
            bar.SelectedKey = "people";
            Settle(window);
            bar.SelectedPill.ShouldBeSameAs(bar.Pills[4]);
            raised.ShouldBe(0);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Arrows_Move_And_Select_At_Once_And_Wrap()
    {
        var bar = Create();
        var raised = new List<string?>();
        bar.SelectionChanged += (_, _) => raised.Add(bar.SelectedKey);
        var window = Show(bar);
        try
        {
            bar.FocusSelected().ShouldBeTrue();
            Key(window, PhysicalKey.ArrowRight);
            bar.SelectedKey.ShouldBe("about");
            bar.Pills[1].IsFocused.ShouldBeTrue();
            bar.Pills[1].Classes.ShouldContain(":focus-visible");
            FocusRing(bar.Pills[1]).IsVisible.ShouldBeTrue();

            Key(window, PhysicalKey.ArrowLeft);
            Key(window, PhysicalKey.ArrowLeft);
            bar.SelectedKey.ShouldBe("people"); // wrapped from the first to the last
            bar.Pills[4].IsFocused.ShouldBeTrue();

            Key(window, PhysicalKey.ArrowRight);
            bar.SelectedKey.ShouldBeNull(); // and from the last to the first
            bar.Pills[0].IsFocused.ShouldBeTrue();
            raised.ShouldBe(["about", null, "people", null]);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Home_And_End_Select_The_First_And_The_Last()
    {
        var bar = Create("preferences");
        var window = Show(bar);
        try
        {
            bar.FocusSelected().ShouldBeTrue();
            Key(window, PhysicalKey.End);
            bar.SelectedKey.ShouldBe("people");
            bar.Pills[4].IsFocused.ShouldBeTrue();
            Key(window, PhysicalKey.Home);
            bar.SelectedKey.ShouldBeNull();
            bar.Pills[0].IsFocused.ShouldBeTrue();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Arrows_With_Modifiers_And_Other_Keys_Are_Left_Alone()
    {
        var bar = Create("about");
        var raised = 0;
        bar.SelectionChanged += (_, _) => raised++;
        var window = Show(bar);
        try
        {
            bar.FocusSelected().ShouldBeTrue();
            Key(window, PhysicalKey.ArrowRight, RawInputModifiers.Shift);
            Key(window, PhysicalKey.ArrowDown);
            Key(window, PhysicalKey.A);
            bar.SelectedKey.ShouldBe("about");
            raised.ShouldBe(0);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Enter_And_Space_On_A_Pill_Select_It()
    {
        var bar = Create();
        var raised = 0;
        bar.SelectionChanged += (_, _) => raised++;
        var window = Show(bar);
        try
        {
            bar.Pills[2].Focus(NavigationMethod.Tab);
            Key(window, PhysicalKey.Enter);
            bar.SelectedKey.ShouldBe("preferences");
            bar.Pills[3].Focus(NavigationMethod.Tab);
            Key(window, PhysicalKey.Space);
            bar.SelectedKey.ShouldBe("projects");
            raised.ShouldBe(2);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void SelectedKey_Is_Two_Way()
    {
        PillFilterBar.SelectedKeyProperty.GetMetadata(typeof(PillFilterBar)).DefaultBindingMode.ShouldBe(BindingMode.TwoWay);
        var model = new Model { Key = "about" };
        var bar = new PillFilterBar { Options = Groups() };
        bar.Bind(PillFilterBar.SelectedKeyProperty, new Binding(nameof(Model.Key)) { Source = model });
        var window = Show(bar);
        try
        {
            bar.SelectedPill!.Key.ShouldBe("about");
            Click(window, bar.Pills[4]);
            model.Key.ShouldBe("people");
            bar.FocusSelected().ShouldBeTrue();
            Key(window, PhysicalKey.Home);
            model.Key.ShouldBeNull();
            model.Key = "projects";
            Settle(window);
            bar.SelectedPill.ShouldBeSameAs(bar.Pills[3]);
        }
        finally
        {
            window.Close();
        }
    }

    #endregion

    #region Fallback when the key matches nothing

    [AvaloniaFact]
    public void Removing_The_Selected_Option_Falls_Back_To_All_Without_An_Event()
    {
        var options = new ObservableCollection<PillFilterOption>(Groups());
        var model = new Model { Key = "projects" };
        var bar = new PillFilterBar { Options = options };
        bar.Bind(PillFilterBar.SelectedKeyProperty, new Binding(nameof(Model.Key)) { Source = model });
        var raised = 0;
        bar.SelectionChanged += (_, _) => raised++;
        var window = Show(bar);
        try
        {
            options.RemoveAt(3);
            Settle(window);
            bar.SelectedKey.ShouldBeNull();
            model.Key.ShouldBeNull(); // written back through the two-way binding
            bar.SelectedPill.ShouldBeSameAs(bar.Pills[0]);
            raised.ShouldBe(0);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void A_List_Cleared_And_Refilled_In_One_Go_Keeps_Its_Selection()
    {
        var options = new ObservableCollection<PillFilterOption>(Groups());
        var bar = Create("people", options);
        var window = Show(bar);
        try
        {
            options.Clear();
            bar.Pills.Count.ShouldBe(0);
            foreach (var option in Groups())
            {
                options.Add(option);
            }
            Settle(window);
            bar.SelectedKey.ShouldBe("people");
            bar.SelectedPill.ShouldBeSameAs(bar.Pills[4]);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void An_Unknown_Key_Falls_Back_To_The_First_Option_When_There_Is_No_All()
    {
        var bar = Create("people", [new("about", "About you"), new("people", "People")]);
        var window = Show(bar);
        try
        {
            bar.SelectedKey = "nowhere";
            bar.SelectedPill.ShouldBeNull();
            // Meanwhile the first pill is the tab stop so the bar can still be reached.
            KeyboardNavigation.GetIsTabStop(bar.Pills[0]).ShouldBeTrue();
            Settle(window);
            bar.SelectedKey.ShouldBe("about");
            bar.SelectedPill.ShouldBeSameAs(bar.Pills[0]);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void No_Options_Leave_The_Key_Alone_And_Hide_The_Background()
    {
        var bar = Create("people", []);
        var window = Show(bar);
        try
        {
            bar.SelectedKey.ShouldBe("people");
            bar.Pills.ShouldBeEmpty();
            bar.Indicator!.CurrentOpacity.ShouldBe(0);
            bar.FocusSelected().ShouldBeFalse();
            bar.Options = Groups();
            Settle(window);
            bar.SelectedPill.ShouldBeSameAs(bar.Pills[4]);
            bar.Indicator.CurrentRect.ShouldBe(bar.Pills[4].Bounds);
        }
        finally
        {
            window.Close();
        }
    }

    #endregion

    #region Focus

    [AvaloniaFact]
    public void Only_The_Selected_Pill_Is_A_Tab_Stop()
    {
        var before = new Avalonia.Controls.Button { Content = "Before" };
        var after = new Avalonia.Controls.Button { Content = "After" };
        var bar = Create("preferences");
        var window = Show(new StackPanel { Children = { before, bar, after } });
        try
        {
            bar.Pills.Select(KeyboardNavigation.GetIsTabStop).ShouldBe([false, false, true, false, false]);
            before.Focus(NavigationMethod.Tab);
            Key(window, PhysicalKey.Tab);
            bar.Pills[2].IsFocused.ShouldBeTrue();
            Key(window, PhysicalKey.Tab);
            after.IsFocused.ShouldBeTrue();
            Key(window, PhysicalKey.Tab, RawInputModifiers.Shift);
            bar.Pills[2].IsFocused.ShouldBeTrue();

            Key(window, PhysicalKey.ArrowRight);
            bar.Pills.Select(KeyboardNavigation.GetIsTabStop).ShouldBe([false, false, false, true, false]);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void FocusSelected_Focuses_The_Selected_Pill_With_Its_Ring()
    {
        var bar = Create("projects");
        var window = Show(bar);
        try
        {
            bar.FocusSelected().ShouldBeTrue();
            Settle(window);
            bar.Pills[3].IsFocused.ShouldBeTrue();
            FocusRing(bar.Pills[3]).IsVisible.ShouldBeTrue();
            var ring = FocusRing(bar.Pills[3]);
            ring.BorderThickness.ShouldBe(new Thickness(2));
            ring.Bounds.ShouldBe(new Rect(bar.Pills[3].Bounds.Size)); // inside the pill's edge
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Concealed_Pills_Fade_Out_And_Take_No_Focus_Or_Input()
    {
        var bar = Create("about");
        var window = Show(bar);
        try
        {
            bar.IsConcealed = true;
            Settle(window);
            bar.Classes.ShouldContain(PillFilterBar.ConcealedPseudoClass);
            bar.Opacity.ShouldBe(0);
            bar.IsHitTestVisible.ShouldBeFalse();
            bar.Pills.ShouldAllBe(p => !p.Focusable);
            bar.FocusSelected().ShouldBeFalse();
            bar.Pills[1].IsFocused.ShouldBeFalse();
            AutomationProperties.GetIsOffscreenBehavior(bar).ShouldBe(IsOffscreenBehavior.Offscreen);
            var peer = ControlAutomationPeer.CreatePeerForElement(bar);
            peer.IsOffscreen().ShouldBeTrue();
            peer.GetChildren().ShouldBeEmpty();

            // Pills built while concealed are not focusable either.
            bar.Options = [.. Groups(), new("places", "Places")];
            bar.Pills[5].Focusable.ShouldBeFalse();

            bar.IsConcealed = false;
            Settle(window);
            peer.IsOffscreen().ShouldBeFalse();
            Buttons(peer).ShouldBe(6);
            bar.Opacity.ShouldBe(1);
            bar.IsHitTestVisible.ShouldBeTrue();
            bar.Pills.ShouldAllBe(p => p.Focusable);
            bar.FocusSelected().ShouldBeTrue();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Concealing_Fades_In_160ms_At_Full_And_120ms_At_Reduced()
    {
        foreach (var (level, ms) in new[] { (ArcMotionLevel.Full, 160), (ArcMotionLevel.Reduced, 120) })
        {
            var bar = Create();
            var window = Show(bar, level);
            try
            {
                var fade = bar.Transitions!.OfType<DoubleTransition>().Single(t => t.Property == Visual.OpacityProperty);
                fade.Duration.ShouldBe(TimeSpan.FromMilliseconds(ms));
                var color = bar.Pills[0].Transitions!.OfType<ArcBrushTransition>().Single();
                color.Property.ShouldBe(Avalonia.Controls.Primitives.TemplatedControl.ForegroundProperty);
                color.Duration.ShouldBe(TimeSpan.FromMilliseconds(ms));
            }
            finally
            {
                window.Close();
            }
        }
    }

    #endregion

    #region The selected background

    [AvaloniaFact]
    public void The_Background_Lands_On_The_Selected_Pill()
    {
        var bar = Create("preferences");
        var window = Show(bar);
        try
        {
            var indicator = bar.Indicator!;
            indicator.CurrentOpacity.ShouldBe(1);
            indicator.CurrentRect.ShouldBe(bar.Pills[2].Bounds);
            indicator.Bounds.ShouldBe(new Rect(TestHelpers.Descendant<PillFilterTrack>(bar).Bounds.Size));
            TestHelpers.ColorOf(indicator.Fill).ShouldBe(ArcTokens.GetColor(bar, DesignTokens.ArcDesignTokenKind.SurfaceMuted));
            indicator.CornerRadius.TopLeft.ShouldBeGreaterThanOrEqualTo(16);

            bar.FocusSelected();
            Key(window, PhysicalKey.End);
            indicator.CurrentRect.ShouldBe(bar.Pills[4].Bounds);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void The_Background_Follows_A_Pill_That_Changes_Width()
    {
        var bar = Create("projects");
        var window = Show(bar);
        try
        {
            bar.Options = [new(null, "All"), new("about", "About you and your work"), new("preferences", "Preferences"), new("projects", "Projects")];
            Settle(window);
            bar.Indicator!.CurrentRect.ShouldBe(bar.Pills[3].Bounds);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void At_Full_The_Background_Glides_On_The_Quick_Spring()
    {
        var bar = Create();
        var window = Show(bar, ArcMotionLevel.Full);
        try
        {
            var clock = new ManualFrameClock();
            using var installed = clock.Install();
            var indicator = bar.Indicator!;
            var from = bar.Pills[0].Bounds;
            indicator.CurrentRect.ShouldBe(from);
            var to = bar.Pills[2].Bounds;
            Click(window, bar.Pills[2]);
            var t = CoreFrames.Run(clock, 6);
            var (dx, _) = SpringSolver.Evaluate(ArcMotionPresets.Quick, from.X - to.X, 0, t);
            indicator.CurrentRect.X.ShouldBe(to.X + dx, 0.01);
            indicator.CurrentRect.X.ShouldBeLessThan(to.X - 1);
            var (dw, _) = SpringSolver.Evaluate(ArcMotionPresets.Quick, from.Width - to.Width, 0, t);
            indicator.CurrentRect.Width.ShouldBe(to.Width + dw, 0.01);
            CoreFrames.Run(clock, 60);
            indicator.CurrentRect.X.ShouldBe(to.X, 0.01);
            indicator.CurrentRect.Width.ShouldBe(to.Width, 0.01);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void At_Reduced_And_Off_The_Background_Jumps()
    {
        foreach (var level in new[] { ArcMotionLevel.Reduced, ArcMotionLevel.Off })
        {
            var bar = Create();
            var window = Show(bar, level);
            try
            {
                var clock = new ManualFrameClock();
                using var installed = clock.Install();
                Click(window, bar.Pills[4]);
                bar.Indicator!.CurrentRect.ShouldBe(bar.Pills[4].Bounds);
                bar.Indicator.CurrentOpacity.ShouldBe(1);
            }
            finally
            {
                window.Close();
            }
        }
    }

    #endregion

    #region Scrolling and edge fades

    // At 228 wide (a 288 card less the toolbar's 4 + 56 insets) the five pills scroll.
    private static (Window Window, PillFilterBar Bar) ShowNarrow(string? selected = null, double width = 228, ArcMotionLevel level = ArcMotionLevel.Off)
    {
        var bar = Create(selected);
        bar.Width = width;
        bar.HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Left;
        var window = Show(bar, level);
        return (window, bar);
    }

    [AvaloniaFact]
    public void A_Bar_That_Fits_Has_No_Fades()
    {
        var bar = Create();
        var window = Show(bar);
        try
        {
            bar.Scroller!.OpacityMask.ShouldBeNull();
            bar.Classes.ShouldNotContain(PillFilterBar.ScrollableStartPseudoClass);
            bar.Classes.ShouldNotContain(PillFilterBar.ScrollableEndPseudoClass);
            bar.Scroller.Offset.X.ShouldBe(0);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void A_Narrow_Bar_Scrolls_And_Fades_Only_Where_There_Is_More()
    {
        var (window, bar) = ShowNarrow();
        try
        {
            var viewer = bar.Scroller!;
            viewer.Extent.Width.ShouldBeGreaterThan(viewer.Viewport.Width);
            viewer.Offset.X.ShouldBe(0);
            bar.Classes.ShouldNotContain(PillFilterBar.ScrollableStartPseudoClass);
            bar.Classes.ShouldContain(PillFilterBar.ScrollableEndPseudoClass);
            var mask = viewer.OpacityMask.ShouldBeAssignableTo<Avalonia.Media.IGradientBrush>()!;
            mask.GradientStops[0].Color.A.ShouldBe((byte)255); // no start fade
            mask.GradientStops[2].Offset.ShouldBe(1 - 28 / viewer.Viewport.Width, 0.0001);
            mask.GradientStops[3].Color.A.ShouldBe((byte)0);

            bar.FocusSelected();
            Key(window, PhysicalKey.End);
            viewer.Offset.X.ShouldBe(viewer.Extent.Width - viewer.Viewport.Width, 0.01);
            bar.Classes.ShouldContain(PillFilterBar.ScrollableStartPseudoClass);
            bar.Classes.ShouldNotContain(PillFilterBar.ScrollableEndPseudoClass);
            mask = viewer.OpacityMask.ShouldBeAssignableTo<Avalonia.Media.IGradientBrush>()!;
            mask.GradientStops[0].Color.A.ShouldBe((byte)0);
            mask.GradientStops[1].Offset.ShouldBe(20 / viewer.Viewport.Width, 0.0001);
            mask.GradientStops[3].Color.A.ShouldBe((byte)255); // no end fade at the end

            // Scrolling within the middle keeps the same mask (no new brush while the state holds).
            Key(window, PhysicalKey.ArrowLeft);
            Key(window, PhysicalKey.ArrowLeft);
            bar.Classes.ShouldContain(PillFilterBar.ScrollableStartPseudoClass);
            bar.Classes.ShouldContain(PillFilterBar.ScrollableEndPseudoClass);
            var both = viewer.OpacityMask;
            viewer.Offset = new Vector(viewer.Offset.X - 3, 0);
            Settle(window);
            viewer.OpacityMask.ShouldBeSameAs(both);

            Key(window, PhysicalKey.Home);
            viewer.Offset.X.ShouldBe(0);
            bar.Classes.ShouldNotContain(PillFilterBar.ScrollableStartPseudoClass);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void The_Selected_Pill_Scrolls_Into_View_With_The_Least_Movement_Clear_Of_The_Fades()
    {
        var (window, bar) = ShowNarrow();
        try
        {
            var viewer = bar.Scroller!;
            var viewport = viewer.Viewport.Width;
            bar.FocusSelected();
            // Right to "About you": the row moves just enough for it to end where the end fade begins.
            Key(window, PhysicalKey.ArrowRight);
            var about = bar.Pills[1].Bounds;
            viewer.Offset.X.ShouldBe(Math.Max(0, about.Right + 28 - viewport), 0.01);
            Key(window, PhysicalKey.ArrowRight);
            var preferences = bar.Pills[2].Bounds;
            viewer.Offset.X.ShouldBe(preferences.Right + 28 - viewport, 0.01);
            var offset = viewer.Offset.X;
            // Back to "About you": it is left of the start fade, so it comes in to sit just clear of it.
            Key(window, PhysicalKey.ArrowLeft);
            viewer.Offset.X.ShouldBe(Math.Max(0, about.Left - 20), 0.01);
            viewer.Offset.X.ShouldBeLessThan(offset);
            // A pill already in view (clear of both fades) does not move the row.
            viewer.Offset = new Vector(about.Left - 30, 0);
            Settle(window);
            bar.FocusSelected().ShouldBeTrue();
            Settle(window);
            viewer.Offset.X.ShouldBe(about.Left - 30, 0.01);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void A_Selection_Set_Before_Showing_Is_Already_In_View()
    {
        var (window, bar) = ShowNarrow("people");
        try
        {
            var viewer = bar.Scroller!;
            viewer.Offset.X.ShouldBe(viewer.Extent.Width - viewer.Viewport.Width, 0.01);
            bar.Indicator!.CurrentRect.ShouldBe(bar.Pills[4].Bounds);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void At_Full_The_Row_Scrolls_On_The_Quick_Spring_And_Stops_When_The_Person_Scrolls()
    {
        var (window, bar) = ShowNarrow(level: ArcMotionLevel.Full);
        try
        {
            var clock = new ManualFrameClock();
            using var installed = clock.Install();
            var viewer = bar.Scroller!;
            var max = viewer.Extent.Width - viewer.Viewport.Width;
            bar.FocusSelected();
            Key(window, PhysicalKey.End);
            var t = CoreFrames.Run(clock, 5);
            var (dx, _) = SpringSolver.Evaluate(ArcMotionPresets.Quick, 0 - max, 0, t);
            viewer.Offset.X.ShouldBe(max + dx, 0.01);
            CoreFrames.Run(clock, 60);
            viewer.Offset.X.ShouldBe(max, 0.01);

            Key(window, PhysicalKey.Home);
            CoreFrames.Run(clock, 3);
            var during = viewer.Offset.X;
            during.ShouldBeGreaterThan(0);
            viewer.Offset = new Vector(during + 10, 0); // the person scrolls
            Settle(window);
            CoreFrames.Run(clock, 30);
            viewer.Offset.X.ShouldBe(during + 10, 0.01);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Focus_Does_Not_Scroll_The_Row_By_Itself()
    {
        var (window, bar) = ShowNarrow();
        try
        {
            bar.Scroller!.BringIntoViewOnFocusChange.ShouldBeFalse();
        }
        finally
        {
            window.Close();
        }
    }

    #endregion

    #region Looks and automation

    [AvaloniaFact]
    public void Text_Is_Secondary_And_Turns_Foreground_When_Selected_Or_Hovered()
    {
        var bar = Create();
        var window = Show(bar);
        try
        {
            var secondary = ArcTokens.GetColor(bar, DesignTokens.ArcDesignTokenKind.TextSecondary);
            var foreground = ArcTokens.GetColor(bar, DesignTokens.ArcDesignTokenKind.Foreground);
            TestHelpers.ColorOf(bar.Pills[0].Foreground).ShouldBe(foreground);
            TestHelpers.ColorOf(bar.Pills[1].Foreground).ShouldBe(secondary);
            bar.Pills[1].Background.ShouldNotBeNull(); // transparent, so the whole pill takes the pointer
            TestHelpers.ColorOf(bar.Pills[1].Background).A.ShouldBe((byte)0);
            window.MouseMove(CenterOf(bar.Pills[1], window));
            Settle(window);
            bar.Pills[1].Classes.ShouldContain(PillFilterButton.HoverPseudoClass);
            TestHelpers.ColorOf(bar.Pills[1].Foreground).ShouldBe(foreground);
            TestHelpers.Descendant<TextBlock>(bar.Pills[1], "PART_Label").FontSize.ShouldBe(14);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Screen_Readers_Get_A_Named_Group_Of_Toggle_Buttons()
    {
        var bar = Create("preferences");
        var window = Show(bar);
        try
        {
            var peer = ControlAutomationPeer.CreatePeerForElement(bar);
            peer.GetAutomationControlType().ShouldBe(AutomationControlType.Group);
            peer.GetName().ShouldBe("Filter memories");
            bar.AccessibleName = "Filter by group";
            peer.GetName().ShouldBe("Filter by group");

            var pill = ControlAutomationPeer.CreatePeerForElement(bar.Pills[2]);
            pill.GetAutomationControlType().ShouldBe(AutomationControlType.Button);
            pill.GetName().ShouldBe("Preferences");
            pill.GetProvider<IToggleProvider>()!.ToggleState.ShouldBe(ToggleState.On);
            ControlAutomationPeer.CreatePeerForElement(bar.Pills[0]).GetProvider<IToggleProvider>()!.ToggleState.ShouldBe(ToggleState.Off);
            AutomationProperties.GetPositionInSet(bar.Pills[2]).ShouldBe(3);
            AutomationProperties.GetSizeOfSet(bar.Pills[2]).ShouldBe(5);

            // Toggling through automation chooses the pill like a click.
            ControlAutomationPeer.CreatePeerForElement(bar.Pills[4]).GetProvider<IToggleProvider>()!.Toggle();
            Settle(window);
            bar.SelectedKey.ShouldBe("people");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void A_Bar_Away_From_The_Tree_Stops_Following_Its_List()
    {
        var options = new ObservableCollection<PillFilterOption>(Groups());
        var bar = Create(null, options);
        var host = new StackPanel { Children = { bar } };
        var window = Show(host);
        try
        {
            host.Children.Remove(bar);
            options.Add(new("places", "Places"));
            bar.Pills.Count.ShouldBe(5);
            host.Children.Add(bar);
            Settle(window);
            bar.Pills.Count.ShouldBe(6);
            options.RemoveAt(5);
            bar.Pills.Count.ShouldBe(5);
        }
        finally
        {
            window.Close();
        }
    }

    #endregion
}
