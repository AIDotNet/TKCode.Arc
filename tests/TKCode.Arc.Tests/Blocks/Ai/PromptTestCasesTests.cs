using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Automation.Provider;
using Avalonia.Collections;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Threading;
using TKCode.Arc.DesignTokens;
using TKCode.Arc.Motion;
using TKCode.Arc.Tests.CorePrimitives;

namespace TKCode.Arc.Tests.Blocks.Ai;

/// <summary>
/// The test cases of Prompt studio as a segmented control: options per case and their labels, the selection (pointer,
/// keys with wrap, Home / End, two-way), the roving tab stop, the gliding background on the quick spring, the stretched
/// layout with equal shares, list changes and the radio group screen readers get.
/// </summary>
public class PromptTestCasesTests
{
    private static PromptTestCases Create(IList<PromptTestCase>? cases = null, int selected = 0)
    {
        var list = cases ?? PromptRunSamples.Cases();
        return new PromptTestCases { Cases = list, SelectedCase = selected >= 0 && selected < list.Count ? list[selected] : null, AccessibleName = "Test case" };
    }

    private static Window Show(Control content, ArcMotionLevel level = ArcMotionLevel.Off, double width = 760)
    {
        var window = TestHelpers.Show(new StackPanel { Margin = new Thickness(20), Children = { content } }, level);
        window.Width = width;
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

    private static void Click(Window window, Visual visual)
    {
        var point = visual.TranslatePoint(new Point(visual.Bounds.Width / 2, visual.Bounds.Height / 2), window)!.Value;
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

    [AvaloniaFact]
    public void Builds_One_Option_Per_Case_Labelled_By_It()
    {
        var cases = PromptRunSamples.Cases();
        var control = Create(cases);
        var window = Show(control);
        try
        {
            control.Options.Select(o => o.Label).ShouldBe(["Broken spar", "Late parcel", "Out of policy"]);
            control.Options.Select(o => o.Case).ShouldBe(cases);
            control.SelectedOption.ShouldBeSameAs(control.Options[0]);
            control.Options.Select(o => o.IsSelected).ShouldBe([true, false, false]);
            control.Options[0].Classes.Contains(PromptTestCaseOption.SelectedPseudoClass).ShouldBeTrue();

            // A case's label is followed.
            cases[1].Label = "Late box";
            control.Options[1].Label.ShouldBe("Late box");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void The_Frame_Is_A_36px_Pill_With_30px_Options_Two_Pixels_Apart()
    {
        var control = Create();
        var window = Show(control);
        try
        {
            control.Bounds.Height.ShouldBe(36);
            var frame = TestHelpers.Descendant<Border>(control, "PART_Frame");
            frame.BorderThickness.ShouldBe(new Thickness(1));
            frame.Padding.ShouldBe(new Thickness(2));
            TestHelpers.ColorOf(frame.BorderBrush).ShouldBe(ArcTokens.GetColor(control, ArcDesignTokenKind.Border));
            var options = control.Options;
            options.ShouldAllBe(o => o.Bounds.Height == 30);
            options[1].Bounds.Left.ShouldBe(options[0].Bounds.Right + 2, 0.01);
            options[2].Bounds.Left.ShouldBe(options[1].Bounds.Right + 2, 0.01);
            options[0].Padding.ShouldBe(new Thickness(12, 0));
            // Selected text is Foreground, the others TextMuted.
            TestHelpers.ColorOf(options[0].Foreground).ShouldBe(ArcTokens.GetColor(control, ArcDesignTokenKind.Foreground));
            TestHelpers.ColorOf(options[1].Foreground).ShouldBe(ArcTokens.GetColor(control, ArcDesignTokenKind.TextMuted));
            TestHelpers.ColorOf(control.Indicator!.Fill).ShouldBe(ArcTokens.GetColor(control, ArcDesignTokenKind.SurfaceMuted));
            control.Indicator.CurrentRect.ShouldBe(options[0].Bounds);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void A_Click_Selects_Its_Case_And_Says_So_Code_Does_Not()
    {
        var control = Create();
        var window = Show(control);
        try
        {
            var changes = 0;
            control.SelectionChanged += (_, _) => changes++;
            Click(window, control.Options[2]);
            control.SelectedCase.ShouldBeSameAs(control.Cases![2]);
            control.SelectedOption.ShouldBeSameAs(control.Options[2]);
            control.Indicator!.CurrentRect.ShouldBe(control.Options[2].Bounds);
            changes.ShouldBe(1);

            // The selected option again: nothing changes.
            Click(window, control.Options[2]);
            changes.ShouldBe(1);

            control.SelectedCase = control.Cases[1];
            Settle(window);
            control.SelectedOption.ShouldBeSameAs(control.Options[1]);
            changes.ShouldBe(1);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Arrow_Keys_Move_And_Select_At_Once_Wrapping_And_Home_End_Go_To_The_Ends()
    {
        var control = Create();
        var window = Show(control);
        try
        {
            var changes = 0;
            control.SelectionChanged += (_, _) => changes++;
            control.FocusSelected().ShouldBeTrue();
            Key(window, PhysicalKey.ArrowRight);
            control.SelectedCase.ShouldBeSameAs(control.Cases![1]);
            control.Options[1].IsFocused.ShouldBeTrue();
            Key(window, PhysicalKey.ArrowDown);
            control.SelectedCase.ShouldBeSameAs(control.Cases[2]);
            Key(window, PhysicalKey.ArrowRight);
            control.SelectedCase.ShouldBeSameAs(control.Cases[0]);
            Key(window, PhysicalKey.ArrowLeft);
            control.SelectedCase.ShouldBeSameAs(control.Cases[2]);
            Key(window, PhysicalKey.ArrowUp);
            control.SelectedCase.ShouldBeSameAs(control.Cases[1]);
            Key(window, PhysicalKey.Home);
            control.SelectedCase.ShouldBeSameAs(control.Cases[0]);
            Key(window, PhysicalKey.End);
            control.SelectedCase.ShouldBeSameAs(control.Cases[2]);
            control.Options[2].IsFocused.ShouldBeTrue();
            changes.ShouldBe(7);

            // With a modifier the keys are left alone.
            Key(window, PhysicalKey.ArrowLeft, RawInputModifiers.Shift);
            control.SelectedCase.ShouldBeSameAs(control.Cases[2]);
            // Space does nothing more.
            Key(window, PhysicalKey.Space);
            control.SelectedCase.ShouldBeSameAs(control.Cases[2]);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Only_The_Selected_Option_Is_A_Tab_Stop_And_The_Keyboard_Ring_Is_Inside_It()
    {
        var before = new Button { Content = "before" };
        var control = Create(selected: 1);
        var window = Show(new StackPanel { Children = { before, control } });
        try
        {
            control.Options.Select(KeyboardNavigation.GetIsTabStop).ShouldBe([false, true, false]);
            before.Focus(NavigationMethod.Tab);
            Key(window, PhysicalKey.Tab);
            control.Options[1].IsFocused.ShouldBeTrue();
            var ring = TestHelpers.Descendant<Border>(control.Options[1], "PART_FocusRing");
            ring.IsVisible.ShouldBeTrue();
            ring.Margin.ShouldBe(default);
            ring.BorderThickness.ShouldBe(new Thickness(2));

            // A pointer focus draws no ring.
            Click(window, control.Options[2]);
            TestHelpers.Descendant<Border>(control.Options[2], "PART_FocusRing").IsVisible.ShouldBeFalse();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void A_Case_That_Is_Not_Listed_Selects_Nothing()
    {
        var control = Create(selected: -1);
        var window = Show(control);
        try
        {
            control.SelectedOption.ShouldBeNull();
            control.Options.ShouldAllBe(o => !o.IsSelected);
            control.Options.Select(KeyboardNavigation.GetIsTabStop).ShouldBe([true, false, false]);
            control.Indicator!.HasTarget.ShouldBeFalse();
            control.FocusSelected().ShouldBeTrue();
            control.Options[0].IsFocused.ShouldBeTrue();

            control.SelectedCase = new PromptTestCase { Label = "Elsewhere" };
            Settle(window);
            control.SelectedOption.ShouldBeNull();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void At_Full_The_Background_Glides_On_The_Quick_Spring()
    {
        var control = Create();
        var window = Show(control, ArcMotionLevel.Full);
        try
        {
            var clock = new ManualFrameClock();
            using var installed = clock.Install();
            var indicator = control.Indicator!;
            var from = control.Options[0].Bounds;
            indicator.CurrentRect.ShouldBe(from);
            var to = control.Options[2].Bounds;
            Click(window, control.Options[2]);
            var t = CoreFrames.Run(clock, 6);
            var (dx, _) = SpringSolver.Evaluate(ArcMotionPresets.Quick, from.X - to.X, 0, t);
            indicator.CurrentRect.X.ShouldBe(to.X + dx, 0.01);
            indicator.CurrentRect.X.ShouldBeLessThan(to.X - 1);
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
            var control = Create();
            var window = Show(control, level);
            try
            {
                var clock = new ManualFrameClock();
                using var installed = clock.Install();
                Click(window, control.Options[2]);
                control.Indicator!.CurrentRect.ShouldBe(control.Options[2].Bounds);
            }
            finally
            {
                window.Close();
            }
        }
    }

    [AvaloniaFact]
    public void Stretched_The_Options_Share_The_Width_Equally()
    {
        var control = Create();
        control.HorizontalAlignment = HorizontalAlignment.Stretch;
        var window = Show(control, width: 900);
        try
        {
            control.Classes.Contains(PromptTestCases.StretchedPseudoClass).ShouldBeTrue();
            control.Track!.IsUniform.ShouldBeTrue();
            var widths = control.Options.Select(o => o.Bounds.Width).ToArray();
            // Equal shares, each laid out to whole pixels.
            widths[0].ShouldBe(widths[1], 1);
            widths[1].ShouldBe(widths[2], 1);
            var inner = control.Track.Bounds.Width;
            (widths.Sum() + 4).ShouldBe(inner, 2);
            control.Indicator!.CurrentRect.ShouldBe(control.Options[0].Bounds);

            control.HorizontalAlignment = HorizontalAlignment.Left;
            Settle(window);
            control.Track.IsUniform.ShouldBeFalse();
            control.Classes.Contains(PromptTestCases.StretchedPseudoClass).ShouldBeFalse();
        }
        finally
        {
            window.Close();
        }
    }

    [Theory]
    [InlineData(new[] { 80.0, 90.0, 100.0 }, 400, 2, new[] { 132.0, 132.0, 132.0 })]
    [InlineData(new[] { 80.0, 90.0, 150.0 }, 340, 2, new[] { 93.0, 93.0, 150.0 })]
    [InlineData(new[] { 120.0, 120.0, 150.0 }, 300, 2, new[] { 120.0, 120.0, 150.0 })]
    public void Equal_Shares_Never_Go_Below_An_Options_Content(double[] desired, double width, double spacing, double[] expected)
    {
        for (var i = 0; i < desired.Length; i++)
        {
            PromptTestCaseTrack.UniformShare(desired, width, spacing, i).ShouldBe(expected[i], 0.01);
        }
    }

    [AvaloniaFact]
    public void The_Options_Follow_The_List_And_Reuse_Their_Controls()
    {
        var cases = new AvaloniaList<PromptTestCase>(PromptRunSamples.Cases());
        var control = Create(cases);
        var window = Show(control);
        try
        {
            var first = control.Options[0];
            cases.Add(PromptRunSamples.Case("x", "Extra"));
            Settle(window);
            control.Options.Count.ShouldBe(4);
            control.Options[0].ShouldBeSameAs(first);
            control.Options[3].Label.ShouldBe("Extra");
            AutomationProperties.GetSizeOfSet(control.Options[3]).ShouldBe(4);

            cases.RemoveAt(0);
            Settle(window);
            control.Options.Count.ShouldBe(3);
            control.Options[0].Label.ShouldBe("Late parcel");
            // The selected case is gone: nothing is selected (the owner picks another).
            control.SelectedOption.ShouldBeNull();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void The_Selected_Case_Binds_Two_Ways()
    {
        var cases = PromptRunSamples.Cases();
        var source = new PromptTestCases { Cases = cases };
        var control = new PromptTestCases { Cases = cases };
        control.Bind(PromptTestCases.SelectedCaseProperty, new Binding(nameof(PromptTestCases.SelectedCase)) { Source = source, Mode = BindingMode.TwoWay });
        var window = Show(control);
        try
        {
            source.SelectedCase = cases[1];
            Settle(window);
            control.SelectedOption.ShouldBeSameAs(control.Options[1]);
            Click(window, control.Options[2]);
            source.SelectedCase.ShouldBeSameAs(cases[2]);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Screen_Readers_Get_A_Radio_Group()
    {
        var control = Create();
        var window = Show(control);
        try
        {
            var peer = ControlAutomationPeer.CreatePeerForElement(control);
            peer.GetName().ShouldBe("Test case");
            peer.GetAutomationControlType().ShouldBe(AutomationControlType.Group);
            var selection = peer.GetProvider<ISelectionProvider>()!;
            selection.CanSelectMultiple.ShouldBeFalse();
            selection.GetSelection().Single().GetName().ShouldBe("Broken spar");

            var option = ControlAutomationPeer.CreatePeerForElement(control.Options[1]);
            option.GetAutomationControlType().ShouldBe(AutomationControlType.RadioButton);
            option.GetName().ShouldBe("Late parcel");
            var item = option.GetProvider<ISelectionItemProvider>()!;
            item.IsSelected.ShouldBeFalse();
            item.Select();
            Settle(window);
            control.SelectedCase.ShouldBeSameAs(control.Cases![1]);
            item.IsSelected.ShouldBeTrue();
            AutomationProperties.GetPositionInSet(control.Options[1]).ShouldBe(2);
            AutomationProperties.GetSizeOfSet(control.Options[1]).ShouldBe(3);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void The_Control_Is_Collected_While_Its_Cases_Live_On()
    {
        var cases = PromptRunSamples.Cases();
        var weak = TestHelpers.ShowAndClose(() => Create(cases));
        TestHelpers.Collect(weak);
        weak.IsAlive.ShouldBeFalse();
        cases[1].Label = "Late box";
    }

    [AvaloniaFact]
    public void Options_Taken_Out_And_Put_Back_Show_The_Labels_Changed_Meanwhile()
    {
        var cases = PromptRunSamples.Cases();
        var control = Create(cases);
        var host = new Border { Child = control };
        var window = Show(host);
        try
        {
            host.Child = null;
            cases[1].Label = "Late box";
            host.Child = control;
            Settle(window);
            control.Options.Select(o => o.Label).ShouldBe(["Broken spar", "Late box", "Out of policy"]);

            // And they follow the cases again.
            cases[2].Label = "Refund request";
            control.Options[2].Label.ShouldBe("Refund request");
        }
        finally
        {
            window.Close();
        }
    }
}
