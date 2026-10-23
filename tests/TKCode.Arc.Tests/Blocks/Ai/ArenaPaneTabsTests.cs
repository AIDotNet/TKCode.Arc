using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Automation.Provider;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Shouldly;
using TKCode.Arc.DesignTokens;
using TKCode.Arc.Motion;
using TKCode.Arc.Primitives;
using TKCode.Arc.Tests.CorePrimitives;

namespace TKCode.Arc.Tests.Blocks.Ai;

/// <summary>
/// <see cref="ArenaPaneTabs"/>: the strip's geometry (60 high, 3 inside, two 52px tabs, the highlight on the selected one),
/// selection by pointer and keys (roving tab stop, wrapping Left / Right, Home / End), two-way binding and the event, the
/// highlight's 220ms tween on a manual clock (Reduced and Off jump), the names and statuses, short names after the reveal
/// and the privacy of the blind test.
/// </summary>
public class ArenaPaneTabsTests
{
    private sealed class Rig : IDisposable
    {
        private readonly IDisposable _clockInstall;

        public Rig(ArenaPaneTabs tabs, ArcMotionLevel level = ArcMotionLevel.Off, double width = 538)
        {
            _clockInstall = Clock.Install();
            Tabs = tabs;
            Host = new Border { Width = width, Child = tabs, HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Left, VerticalAlignment = Avalonia.Layout.VerticalAlignment.Top };
            Window = TestHelpers.Show(Host, level);
            Window.Width = width + 100;
            Window.Height = 300;
            Layout();
            Frames(30);
        }

        public ManualFrameClock Clock { get; } = new();
        public ArenaPaneTabs Tabs { get; }
        public Border Host { get; }
        public Window Window { get; }
        public ArenaPaneTab A => Tabs.TabA!;
        public ArenaPaneTab B => Tabs.TabB!;
        public GlidingIndicator Highlight => Tabs.Highlight!;

        public void Layout()
        {
            Dispatcher.UIThread.RunJobs();
            Window.UpdateLayout();
            Dispatcher.UIThread.RunJobs();
        }

        public void Frames(int count)
        {
            for (var i = 0; i < count; i++)
            {
                CoreFrames.Run(Clock, 1);
                Layout();
            }
        }

        public void Key(PhysicalKey key)
        {
            Window.KeyPressQwerty(key, RawInputModifiers.None);
            Window.KeyReleaseQwerty(key, RawInputModifiers.None);
            Layout();
        }

        public void Click(Visual visual)
        {
            var point = visual.TranslatePoint(new Point(visual.Bounds.Width / 2, visual.Bounds.Height / 2), Window) ?? default;
            Window.MouseMove(point, RawInputModifiers.None);
            Window.MouseDown(point, MouseButton.Left, RawInputModifiers.None);
            Window.MouseUp(point, MouseButton.Left, RawInputModifiers.None);
            Layout();
        }

        /// <summary>The highlight's target for <paramref name="tab"/>: the tab's box, half the outline in.</summary>
        public Rect RectOf(ArenaPaneTab tab) => new Rect(tab.TranslatePoint(default, Highlight)!.Value, tab.Bounds.Size).Deflate(0.5);

        public void Dispose()
        {
            Window.Close();
            _clockInstall.Dispose();
        }
    }

    private static Rig Show(ArenaPaneStatus a = ArenaPaneStatus.Done, ArenaPaneStatus b = ArenaPaneStatus.FinishedFirst, bool revealed = false,
        ArcMotionLevel level = ArcMotionLevel.Off, double width = 538)
        => new(ArenaPaneSamples.Tabs(ArenaPaneSamples.State(ArenaSlot.A, status: a), ArenaPaneSamples.State(ArenaSlot.B, status: b), revealed),
            level, width);

    /// <summary>A two-way binding target for <see cref="ArenaPaneTabs.SelectedSlot"/>.</summary>
    private sealed class SlotHolder : AvaloniaObject
    {
        public static readonly StyledProperty<ArenaSlot> SlotProperty = AvaloniaProperty.Register<SlotHolder, ArenaSlot>(nameof(Slot));

        public ArenaSlot Slot { get => GetValue(SlotProperty); set => SetValue(SlotProperty, value); }
    }

    #region Geometry and look

    [AvaloniaFact]
    public void The_Strip_Is_60_High_With_Two_52px_Tabs_Sharing_The_Width()
    {
        using var rig = Show();
        rig.Tabs.Bounds.Height.ShouldBe(60);
        rig.Tabs.Margin.ShouldBe(new Thickness(20, 12, 20, 0));
        rig.Tabs.Bounds.Width.ShouldBe(538 - 40);
        rig.Tabs.Padding.ShouldBe(new Thickness(3));
        rig.Tabs.CornerRadius.ShouldBe(new CornerRadius(18));
        TestHelpers.ColorOf(rig.Tabs.Background).ShouldBe(ArcTokens.GetColor(rig.Tabs, ArcDesignTokenKind.SurfaceMuted));
        TestHelpers.ColorOf(rig.Tabs.BorderBrush).ShouldBe(ArcTokens.GetColor(rig.Tabs, ArcDesignTokenKind.Border));
        rig.A.Bounds.Height.ShouldBe(52);
        rig.B.Bounds.Height.ShouldBe(52);
        // 1 border + 3 inside on each side; the tabs split the rest.
        var inner = 538 - 40 - 2 * (1 + 3);
        rig.A.Bounds.Width.ShouldBe(inner / 2.0, 0.01);
        rig.B.Bounds.Width.ShouldBe(inner / 2.0, 0.01);
        rig.A.TranslatePoint(default, rig.Tabs)!.Value.ShouldBe(new Point(4, 4));
        rig.B.TranslatePoint(default, rig.Tabs)!.Value.X.ShouldBe(4 + inner / 2.0, 0.01);

        // The highlight: Surface, a 1px Border outline, radius 15 at its outer edge, under the selected tab.
        TestHelpers.ColorOf(rig.Highlight.Fill).ShouldBe(ArcTokens.GetColor(rig.Tabs, ArcDesignTokenKind.Surface));
        TestHelpers.ColorOf(rig.Highlight.Stroke).ShouldBe(ArcTokens.GetColor(rig.Tabs, ArcDesignTokenKind.Border));
        rig.Highlight.StrokeThickness.ShouldBe(1);
        (rig.Highlight.CornerRadius.TopLeft + rig.Highlight.StrokeThickness / 2).ShouldBe(15);
        rig.Highlight.CurrentRect.ShouldBe(rig.RectOf(rig.A));
        rig.Highlight.CurrentOpacity.ShouldBe(1);
    }

    [AvaloniaFact]
    public void Each_Tab_Has_The_Identity_Over_The_Compact_Status_Lined_Up_On_The_Name()
    {
        using var rig = Show();
        var identity = rig.A.Identity!;
        var pill = rig.A.StatusPill!;
        pill.IsCompact.ShouldBeTrue();
        pill.Bounds.Height.ShouldBe(20);
        pill.Margin.ShouldBe(new Thickness(-7, 0, 0, 0));
        identity.ShowsShortName.ShouldBeTrue();
        identity.TranslatePoint(default, rig.A)!.Value.X.ShouldBe(12);
        pill.TranslatePoint(default, rig.A)!.Value.X.ShouldBe(12 - 7);
        // The two lines sit together, centered in the 52px tab.
        var top = identity.TranslatePoint(default, rig.A)!.Value.Y;
        var bottom = pill.TranslatePoint(new Point(0, pill.Bounds.Height), rig.A)!.Value.Y;
        pill.TranslatePoint(default, rig.A)!.Value.Y.ShouldBe(top + identity.Bounds.Height, 0.01);
        ((top + bottom) / 2).ShouldBe(26, 0.5);

        rig.A.Identity!.Face!.Text.ShouldBe("Model A");
        rig.B.Identity!.Face!.Text.ShouldBe("Model B");
        rig.A.StatusPill!.Label.ShouldBe("Done");
        // The compact pill says the short word for the answer that finished first.
        rig.B.StatusPill!.Label.ShouldBe("First");
    }

    [AvaloniaFact]
    public void The_Selected_Name_Is_Foreground_And_The_Other_TextSecondary()
    {
        using var rig = Show();
        TestHelpers.ColorOf(rig.A.Identity!.Foreground).ShouldBe(ArcTokens.GetColor(rig.Tabs, ArcDesignTokenKind.Foreground));
        TestHelpers.ColorOf(rig.B.Identity!.Foreground).ShouldBe(ArcTokens.GetColor(rig.Tabs, ArcDesignTokenKind.TextSecondary));
        rig.Tabs.SelectedSlot = ArenaSlot.B;
        rig.Layout();
        TestHelpers.ColorOf(rig.A.Identity.Foreground).ShouldBe(ArcTokens.GetColor(rig.Tabs, ArcDesignTokenKind.TextSecondary));
        TestHelpers.ColorOf(rig.B.Identity.Foreground).ShouldBe(ArcTokens.GetColor(rig.Tabs, ArcDesignTokenKind.Foreground));
    }

    [AvaloniaFact]
    public void The_Name_Color_Changes_Over_160ms_At_Full_Motion()
    {
        using var rig = Show(level: ArcMotionLevel.Full);
        var transitions = rig.B.Identity!.Transitions.ShouldNotBeNull();
        transitions.OfType<ArcBrushTransition>().ShouldContain(t => t.Property == TemplatedControl.ForegroundProperty);
        transitions.OfType<ArcBrushTransition>().First().Duration.ShouldBe(ArcMotionPresets.Fast);
    }

    #endregion

    #region Selection

    [AvaloniaFact]
    public void A_Click_Selects_The_Tab_Raises_The_Event_And_Moves_The_Tab_Stop()
    {
        using var rig = Show();
        var raised = new List<ArenaSlot>();
        rig.Tabs.SelectedSlotChanged += (_, e) =>
        {
            e.Source.ShouldBe(rig.Tabs);
            raised.Add(rig.Tabs.SelectedSlot);
        };
        rig.Tabs.SelectedSlot.ShouldBe(ArenaSlot.A);
        rig.A.IsSelected.ShouldBeTrue();
        KeyboardNavigation.GetIsTabStop(rig.A).ShouldBeTrue();
        KeyboardNavigation.GetIsTabStop(rig.B).ShouldBeFalse();

        rig.Click(rig.B);
        rig.Tabs.SelectedSlot.ShouldBe(ArenaSlot.B);
        raised.ShouldBe([ArenaSlot.B]);
        rig.B.IsSelected.ShouldBeTrue();
        rig.B.Classes.ShouldContain(ArenaPaneTab.SelectedPseudoClass);
        rig.A.IsSelected.ShouldBeFalse();
        KeyboardNavigation.GetIsTabStop(rig.A).ShouldBeFalse();
        KeyboardNavigation.GetIsTabStop(rig.B).ShouldBeTrue();
        rig.Highlight.CurrentRect.ShouldBe(rig.RectOf(rig.B));

        // Clicking the selected tab changes nothing.
        rig.Click(rig.B);
        raised.Count.ShouldBe(1);
    }

    [AvaloniaFact]
    public void Left_And_Right_Wrap_And_Select_Home_And_End_Go_To_The_Ends()
    {
        using var rig = Show();
        rig.Tabs.FocusSelected().ShouldBeTrue();
        rig.A.IsFocused.ShouldBeTrue();

        rig.Key(PhysicalKey.ArrowRight);
        rig.Tabs.SelectedSlot.ShouldBe(ArenaSlot.B);
        rig.B.IsFocused.ShouldBeTrue();
        rig.B.Classes.ShouldContain(":focus-visible");

        rig.Key(PhysicalKey.ArrowRight);
        rig.Tabs.SelectedSlot.ShouldBe(ArenaSlot.A);
        rig.A.IsFocused.ShouldBeTrue();

        rig.Key(PhysicalKey.ArrowLeft);
        rig.Tabs.SelectedSlot.ShouldBe(ArenaSlot.B);
        rig.B.IsFocused.ShouldBeTrue();

        rig.Key(PhysicalKey.Home);
        rig.Tabs.SelectedSlot.ShouldBe(ArenaSlot.A);
        rig.A.IsFocused.ShouldBeTrue();

        rig.Key(PhysicalKey.End);
        rig.Tabs.SelectedSlot.ShouldBe(ArenaSlot.B);
        rig.B.IsFocused.ShouldBeTrue();
        rig.Key(PhysicalKey.End);
        rig.Tabs.SelectedSlot.ShouldBe(ArenaSlot.B);
    }

    [AvaloniaFact]
    public void The_Strip_Is_One_Tab_Stop()
    {
        var before = new Button { Content = "Before" };
        var after = new Button { Content = "After" };
        var tabs = ArenaPaneSamples.Tabs(ArenaPaneSamples.State(ArenaSlot.A), ArenaPaneSamples.State(ArenaSlot.B));
        tabs.SelectedSlot = ArenaSlot.B;
        var window = TestHelpers.Show(new StackPanel { Width = 538, Children = { before, tabs, after } });
        try
        {
            window.Width = 640;
            window.UpdateLayout();
            before.Focus(NavigationMethod.Tab);
            window.KeyPressQwerty(PhysicalKey.Tab, RawInputModifiers.None);
            Dispatcher.UIThread.RunJobs();
            tabs.TabB!.IsFocused.ShouldBeTrue();
            window.KeyPressQwerty(PhysicalKey.Tab, RawInputModifiers.None);
            Dispatcher.UIThread.RunJobs();
            after.IsFocused.ShouldBeTrue();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void SelectedSlot_Binds_Both_Ways()
    {
        var holder = new SlotHolder { Slot = ArenaSlot.B };
        var tabs = ArenaPaneSamples.Tabs(ArenaPaneSamples.State(ArenaSlot.A), ArenaPaneSamples.State(ArenaSlot.B));
        tabs[!!ArenaPaneTabs.SelectedSlotProperty] = holder[!!SlotHolder.SlotProperty];
        using var rig = new Rig(tabs);
        rig.Tabs.SelectedSlot.ShouldBe(ArenaSlot.B);
        rig.Highlight.CurrentRect.ShouldBe(rig.RectOf(rig.B));

        rig.Click(rig.A);
        holder.Slot.ShouldBe(ArenaSlot.A);
        holder.Slot = ArenaSlot.B;
        rig.Layout();
        rig.Tabs.SelectedSlot.ShouldBe(ArenaSlot.B);
        rig.B.IsSelected.ShouldBeTrue();
    }

    #endregion

    #region Highlight motion

    [AvaloniaFact]
    public void At_Full_The_Highlight_Slides_In_220ms_On_The_Enter_Curve()
    {
        using var rig = Show(level: ArcMotionLevel.Full);
        var from = rig.RectOf(rig.A);
        var to = rig.RectOf(rig.B);
        rig.Highlight.CurrentRect.ShouldBe(from);

        rig.Tabs.SelectedSlot = ArenaSlot.B;
        rig.Layout();
        var distance = to.X - from.X;
        distance.ShouldBeGreaterThan(200);
        // Halfway through, the strong ease-out has covered most of the way; no overshoot at any frame.
        var samples = new List<double>();
        for (var i = 0; i < 15; i++)
        {
            rig.Frames(1);
            samples.Add((rig.Highlight.CurrentRect.X - from.X) / distance);
        }
        samples[6].ShouldBeInRange(0.85, 0.99);
        samples.ShouldAllBe(p => p <= 1.0001);
        samples.Zip(samples.Skip(1)).ShouldAllBe(pair => pair.Second >= pair.First - 1e-9);
        rig.Highlight.CurrentRect.ShouldBe(to);
        rig.Highlight.CurrentOpacity.ShouldBe(1);
    }

    [AvaloniaFact]
    public void At_Reduced_And_Off_The_Highlight_Jumps()
    {
        foreach (var level in new[] { ArcMotionLevel.Reduced, ArcMotionLevel.Off })
        {
            using var rig = Show(level: level);
            rig.Tabs.SelectedSlot = ArenaSlot.B;
            rig.Layout();
            rig.Highlight.CurrentRect.ShouldBe(rig.RectOf(rig.B));
            rig.Highlight.CurrentOpacity.ShouldBe(1);
        }
    }

    [AvaloniaFact]
    public void A_Resize_Moves_The_Highlight_At_Once()
    {
        using var rig = Show(level: ArcMotionLevel.Full);
        rig.Tabs.SelectedSlot = ArenaSlot.B;
        rig.Frames(30);
        rig.Host.Width = 400;
        rig.Layout();
        rig.Highlight.CurrentRect.ShouldBe(rig.RectOf(rig.B));
        rig.B.Bounds.Width.ShouldBe((400 - 40 - 8) / 2.0, 0.01);
    }

    #endregion

    #region Names, statuses and privacy

    [AvaloniaFact]
    public void The_Strip_Is_A_Named_Tab_List_Of_Two_Selectable_Tab_Items()
    {
        using var rig = Show();
        var list = ControlAutomationPeer.CreatePeerForElement(rig.Tabs);
        list.GetAutomationControlType().ShouldBe(AutomationControlType.Tab);
        list.GetName().ShouldBe("Answers");
        var selection = list.GetProvider<ISelectionProvider>().ShouldNotBeNull();
        selection.CanSelectMultiple.ShouldBeFalse();
        selection.GetSelection().ShouldHaveSingleItem().ShouldBe(ControlAutomationPeer.CreatePeerForElement(rig.A));

        var a = ControlAutomationPeer.CreatePeerForElement(rig.A);
        var b = ControlAutomationPeer.CreatePeerForElement(rig.B);
        list.GetChildren().ShouldBe([a, b]);
        a.GetAutomationControlType().ShouldBe(AutomationControlType.TabItem);
        a.GetName().ShouldBe("Model A");
        b.GetName().ShouldBe("Model B");
        a.GetChildren().ShouldBeEmpty();
        AutomationProperties.GetItemStatus(rig.A).ShouldBe("Done");
        // The whole word for screen readers, not the compact pill's short one.
        AutomationProperties.GetItemStatus(rig.B).ShouldBe("Finished first");
        a.GetProvider<ISelectionItemProvider>()!.IsSelected.ShouldBeTrue();
        b.GetProvider<ISelectionItemProvider>()!.IsSelected.ShouldBeFalse();

        b.GetProvider<ISelectionItemProvider>()!.Select();
        rig.Tabs.SelectedSlot.ShouldBe(ArenaSlot.B);
        selection.GetSelection().ShouldHaveSingleItem().ShouldBe(b);
    }

    [AvaloniaFact]
    public void The_Tabs_Follow_The_States()
    {
        using var rig = Show(ArenaPaneStatus.Thinking, ArenaPaneStatus.Thinking);
        rig.A.StatusPill!.Label.ShouldBe("Thinking");
        rig.Tabs.PaneA!.Status = ArenaPaneStatus.Writing;
        rig.Tabs.PaneB!.Status = ArenaPaneStatus.FinishedFirst;
        rig.A.StatusPill.Label.ShouldBe("Writing");
        rig.B.StatusPill!.Label.ShouldBe("First");
        AutomationProperties.GetItemStatus(rig.A).ShouldBe("Writing");

        // A new state object replaces the old one, which is let go.
        var old = rig.Tabs.PaneA;
        rig.Tabs.PaneA = ArenaPaneSamples.State(ArenaSlot.A, status: ArenaPaneStatus.Stopped);
        rig.A.StatusPill.Label.ShouldBe("Stopped");
        old.Status = ArenaPaneStatus.Failed;
        rig.A.StatusPill.Label.ShouldBe("Stopped");
    }

    [AvaloniaFact]
    public void Blind_Tabs_Never_Name_Their_Models()
    {
        using var rig = Show();
        var a = rig.Tabs.PaneA!.Model!;
        var b = rig.Tabs.PaneB!.Model!;
        foreach (var status in Enum.GetValues<ArenaPaneStatus>())
        {
            rig.Tabs.PaneA!.Status = status;
            rig.Tabs.PaneB!.Status = status;
            rig.Layout();
            ArenaPaneTests.ShouldNotLeak(rig.Tabs, a);
            ArenaPaneTests.ShouldNotLeak(rig.Tabs, b);
        }
    }

    [AvaloniaFact]
    public void Revealed_Tabs_Show_The_Short_Names_After_The_Turn()
    {
        using var rig = Show();
        rig.Tabs.IsRevealed = true;
        ArenaPaneTests.ShouldNotLeak(rig.Tabs, rig.Tabs.PaneA!.Model!);
        rig.Layout();
        // "Lumen 3 Pro" has the short name "Lumen Pro"; "Kite 5" has none and shows its name.
        rig.A.Identity!.Face!.Text.ShouldBe("Lumen Pro");
        rig.B.Identity!.Face!.Text.ShouldBe("Kite 5");
        rig.A.Identity.Face.Mark.ShouldNotBeNull();
        AutomationProperties.GetName(rig.A).ShouldBe("Model A, Lumen 3 Pro");
        AutomationProperties.GetName(rig.B).ShouldBe("Model B, Kite 5");
        rig.A.Classes.ShouldContain(ArenaPaneTab.RevealedPseudoClass);

        rig.Tabs.IsRevealed = false;
        AutomationProperties.GetName(rig.A).ShouldBe("Model A");
        rig.Layout();
        rig.A.Identity.Face!.Text.ShouldBe("Model A");
        ArenaPaneTests.ShouldNotLeak(rig.Tabs, rig.Tabs.PaneA!.Model!);
    }

    [AvaloniaFact]
    public void A_New_Pair_And_A_Hide_In_One_Turn_Never_Name_The_New_Models()
    {
        using var rig = Show(revealed: true, level: ArcMotionLevel.Full);
        rig.A.Identity!.ShowsModel.ShouldBeTrue();
        var next = new AiModel { Id = "tern-flash", Name = "Tern 1.2 Flash", ShortName = "Tern Flash", ProviderId = "tern" };
        rig.Tabs.PaneA!.Model = next;
        ArenaPaneTests.ShouldNotLeak(rig.Tabs, next);
        rig.Tabs.IsRevealed = false;
        ArenaPaneTests.ShouldNotLeak(rig.Tabs, next);
        rig.Frames(30);
        ArenaPaneTests.ShouldNotLeak(rig.Tabs, next);
        AutomationProperties.GetName(rig.A).ShouldBe("Model A");
    }

    #endregion
}
