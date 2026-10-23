using TKCode.Arc.DesignTokens;
using TKCode.Arc.Motion;
using TKCode.Arc.Primitives;
using TKCode.Arc.Tests.CorePrimitives;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Automation.Provider;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;

namespace TKCode.Arc.Tests.Blocks.Ai;

/// <summary>The prompt studio's version pill: what it shows, what it asks its owner, its look and how its text changes move.</summary>
public class PromptVersionPillTests
{
    internal static Window Show(Control content, ArcMotionLevel level = ArcMotionLevel.Off)
    {
        var window = TestHelpers.Show(new StackPanel { Margin = new Thickness(20), Children = { content } }, level);
        Settle(window);
        return window;
    }

    internal static void Settle(Window window)
    {
        window.UpdateLayout();
        AvaloniaHeadlessPlatform.ForceRenderTimerTick();
        Dispatcher.UIThread.RunJobs();
        window.UpdateLayout();
    }

    /// <summary>Runs posted jobs until <paramref name="done"/> holds (motion continuations come back from the thread pool).</summary>
    internal static bool WaitFor(Func<bool> done)
    {
        for (var i = 0; i < 200; i++)
        {
            Dispatcher.UIThread.RunJobs();
            if (done())
            {
                return true;
            }
            Thread.Sleep(5);
        }
        return false;
    }

    internal static Point CenterOf(Visual visual, Window window) =>
        visual.TranslatePoint(new Point(visual.Bounds.Width / 2, visual.Bounds.Height / 2), window)!.Value;

    /// <summary>Layers in a content swap: 1 at rest, 2 while the old content fades out.</summary>
    internal static int Layers(ArcContentSwap swap) => swap.GetVisualChildren().Count();

    internal static List<PromptPillOpenRequestedEventArgs> Record(PromptHeaderPill pill)
    {
        var raised = new List<PromptPillOpenRequestedEventArgs>();
        pill.OpenRequested += (_, e) => raised.Add(e);
        return raised;
    }

    private static ArcContentSwap StateSwap(PromptVersionPill pill) => TestHelpers.Descendant<ArcContentSwap>(pill, PromptVersionPill.StatePart);

    private static ArcContentSwap LabelSwap(PromptVersionPill pill) => TestHelpers.Descendant<ArcContentSwap>(pill, PromptVersionPill.LabelPart);

    #region What it shows

    [AvaloniaFact]
    public void A_Production_Version_Shows_Its_Label_A_Green_Dot_And_The_Production_Word()
    {
        var pill = PromptMenuSamples.VersionPill();
        Show(pill);
        pill.Bounds.Height.ShouldBe(32);
        pill.Padding.ShouldBe(new Thickness(11, 0, 9, 0));
        pill.CurrentLabel!.Text.ShouldBe("v4");
        pill.CurrentState!.Text.ShouldBe("Production");
        pill.CurrentState.ShowsDot.ShouldBeTrue();
        var dot = TestHelpers.Descendant<Avalonia.Controls.Shapes.Ellipse>(pill.CurrentState, "PART_Dot");
        dot.IsVisible.ShouldBeTrue();
        dot.Bounds.Width.ShouldBe(6);
        TestHelpers.ColorOf(dot.Fill).ShouldBe(ArcTokens.GetColor(pill, ArcDesignTokenKind.Success));
        pill.Classes.Contains(PromptVersionPill.ProductionPseudoClass).ShouldBeTrue();
        AutomationProperties.GetName(pill).ShouldBe("v4 Production");
    }

    [AvaloniaFact]
    public void Another_Open_Version_Says_Latest_Without_A_Dot()
    {
        var pill = PromptMenuSamples.VersionPill(production: false);
        Show(pill);
        pill.CurrentState!.Text.ShouldBe("Latest");
        pill.CurrentState.ShowsDot.ShouldBeFalse();
        TestHelpers.Descendant<Avalonia.Controls.Shapes.Ellipse>(pill.CurrentState, "PART_Dot").IsVisible.ShouldBeFalse();
        pill.Classes.Contains(PromptVersionPill.ProductionPseudoClass).ShouldBeFalse();
        AutomationProperties.GetName(pill).ShouldBe("v4 Latest");
    }

    [AvaloniaFact]
    public void The_Pill_Holds_No_Words_Of_Its_Own()
    {
        var pill = new PromptVersionPill { Label = "v2", IsProduction = true };
        Show(pill);
        pill.CurrentState!.Text.ShouldBe(string.Empty);
        AutomationProperties.GetName(pill).ShouldBe("v2");
    }

    [AvaloniaFact]
    public void The_Name_Format_Names_The_Pill()
    {
        var pill = PromptMenuSamples.VersionPill();
        pill.NameFormat = (label, production) => production ? $"Version {label}, in production" : $"Version {label}";
        Show(pill);
        AutomationProperties.GetName(pill).ShouldBe("Version v4, in production");
        pill.IsProduction = false;
        AutomationProperties.GetName(pill).ShouldBe("Version v4");
        pill.Label = "v5";
        AutomationProperties.GetName(pill).ShouldBe("Version v5");
    }

    [AvaloniaFact]
    public void Production_Is_Wider_Than_Latest()
    {
        var production = PromptMenuSamples.VersionPill();
        var latest = PromptMenuSamples.VersionPill(production: false);
        var window = Show(new StackPanel { Spacing = 8, Children = { production, latest } });
        Settle(window);
        production.Bounds.Width.ShouldBeGreaterThan(latest.Bounds.Width);
        // Label, 8px, state: the label layer sits at the left edge of the text, the state 8px after it.
        var label = production.CurrentLabel!;
        var state = production.CurrentState!;
        var labelLeft = label.TranslatePoint(default, production)!.Value.X;
        var stateLeft = state.TranslatePoint(default, production)!.Value.X;
        labelLeft.ShouldBe(12, 0.01); // 1px border + 11px padding
        stateLeft.ShouldBe(labelLeft + label.Bounds.Width + 8, 0.01);
    }

    #endregion

    #region Look

    [AvaloniaFact]
    public void Open_Looks_Like_Hover_And_The_Chevron_Does_Not_Turn()
    {
        var pill = PromptMenuSamples.VersionPill();
        Show(pill);
        var chevron = TestHelpers.Descendant<ArcGlyph>(pill, "PART_Chevron");
        TestHelpers.ColorOf(pill.Background).ShouldBe(ArcTokens.GetColor(pill, ArcDesignTokenKind.Surface));
        TestHelpers.ColorOf(pill.BorderBrush).ShouldBe(ArcTokens.GetColor(pill, ArcDesignTokenKind.Border));
        pill.IsOpen = true;
        pill.Classes.Contains(PromptHeaderPill.OpenPseudoClass).ShouldBeTrue();
        TestHelpers.ColorOf(pill.Background).ShouldBe(ArcTokens.GetColor(pill, ArcDesignTokenKind.SurfaceMuted));
        TestHelpers.ColorOf(pill.BorderBrush).ShouldBe(ArcTokens.GetColor(pill, ArcDesignTokenKind.BorderStrong));
        (chevron.RenderTransform?.Value ?? Matrix.Identity).ShouldBe(Matrix.Identity);
        chevron.Size.ShouldBe(14);
        chevron.StrokeThickness.ShouldBe(1.75);
        pill.IsOpen = false;
        TestHelpers.ColorOf(pill.Background).ShouldBe(ArcTokens.GetColor(pill, ArcDesignTokenKind.Surface));
    }

    [AvaloniaFact]
    public void The_Pointer_Tints_It_But_Touch_Does_Not()
    {
        var pill = PromptMenuSamples.VersionPill();
        var window = Show(pill);
        var center = CenterOf(pill, window);
        window.MouseMove(center, RawInputModifiers.None);
        Dispatcher.UIThread.RunJobs();
        pill.Classes.Contains(PromptHeaderPill.HoverPseudoClass).ShouldBeTrue();
        TestHelpers.ColorOf(pill.Background).ShouldBe(ArcTokens.GetColor(pill, ArcDesignTokenKind.SurfaceMuted));
        window.MouseMove(new Point(1, 280), RawInputModifiers.None);
        Dispatcher.UIThread.RunJobs();
        pill.Classes.Contains(PromptHeaderPill.HoverPseudoClass).ShouldBeFalse();
    }

    [AvaloniaFact]
    public void The_Focus_Ring_Shows_For_The_Keyboard_Only()
    {
        var pill = PromptMenuSamples.VersionPill();
        var other = PromptMenuSamples.VersionPill();
        Show(new StackPanel { Children = { pill, other } });
        var ring = TestHelpers.Descendant<Border>(pill, "PART_FocusRing");
        pill.Focus(NavigationMethod.Tab);
        Dispatcher.UIThread.RunJobs();
        ring.IsVisible.ShouldBeTrue();
        other.Focus(NavigationMethod.Pointer);
        Dispatcher.UIThread.RunJobs();
        ring.IsVisible.ShouldBeFalse();
        TestHelpers.Descendant<Border>(other, "PART_FocusRing").IsVisible.ShouldBeFalse();
        ring.Margin.ShouldBe(new Thickness(-4));
        ring.BorderThickness.ShouldBe(new Thickness(2));
    }

    #endregion

    #region What it asks

    [AvaloniaFact]
    public void A_Press_Asks_To_Open_With_The_Surface_And_Never_Clicks()
    {
        var pill = PromptMenuSamples.VersionPill();
        var window = Show(pill);
        var raised = Record(pill);
        var clicks = 0;
        pill.Click += (_, _) => clicks++;
        var center = CenterOf(pill, window);
        window.MouseDown(center, MouseButton.Left, RawInputModifiers.None);
        raised.Count.ShouldBe(1);
        raised[0].Focus.ShouldBe(ModelMenuOpenFocus.Surface);
        raised[0].Source.ShouldBe(pill);
        window.MouseUp(center, MouseButton.Left, RawInputModifiers.None);
        clicks.ShouldBe(0);
        raised.Count.ShouldBe(1);
    }

    [AvaloniaFact]
    public void A_Ctrl_Press_Or_A_Secondary_Press_Asks_Nothing()
    {
        var pill = PromptMenuSamples.VersionPill();
        var window = Show(pill);
        var raised = Record(pill);
        var center = CenterOf(pill, window);
        window.MouseDown(center, MouseButton.Left, RawInputModifiers.Control);
        window.MouseUp(center, MouseButton.Left, RawInputModifiers.Control);
        window.MouseDown(center, MouseButton.Right, RawInputModifiers.None);
        window.MouseUp(center, MouseButton.Right, RawInputModifiers.None);
        raised.ShouldBeEmpty();
    }

    [AvaloniaFact]
    public void Enter_Space_And_Down_Ask_To_Open_With_The_Selected_Row()
    {
        var pill = PromptMenuSamples.VersionPill();
        var window = Show(pill);
        var raised = Record(pill);
        var clicks = 0;
        pill.Click += (_, _) => clicks++;
        pill.Focus(NavigationMethod.Tab);
        window.KeyPressQwerty(PhysicalKey.Enter, RawInputModifiers.None);
        window.KeyReleaseQwerty(PhysicalKey.Enter, RawInputModifiers.None);
        window.KeyPressQwerty(PhysicalKey.Space, RawInputModifiers.None);
        window.KeyReleaseQwerty(PhysicalKey.Space, RawInputModifiers.None);
        window.KeyPressQwerty(PhysicalKey.ArrowDown, RawInputModifiers.None);
        window.KeyPressQwerty(PhysicalKey.ArrowUp, RawInputModifiers.None);
        window.KeyPressQwerty(PhysicalKey.Enter, RawInputModifiers.Meta);
        window.KeyPressQwerty(PhysicalKey.Enter, RawInputModifiers.Control);
        raised.Count.ShouldBe(3);
        raised.ShouldAllBe(e => e.Focus == ModelMenuOpenFocus.Selected);
        clicks.ShouldBe(0);
    }

    [AvaloniaFact]
    public void A_Disabled_Pill_Asks_Nothing()
    {
        var pill = PromptMenuSamples.VersionPill();
        pill.IsEnabled = false;
        var window = Show(pill);
        var raised = Record(pill);
        window.MouseDown(CenterOf(pill, window), MouseButton.Left, RawInputModifiers.None);
        pill.RequestOpen(ModelMenuOpenFocus.Selected);
        raised.ShouldBeEmpty();
    }

    [AvaloniaFact]
    public void Assistive_Technology_Sees_A_Button_That_Expands_A_Dialog()
    {
        var pill = PromptMenuSamples.VersionPill();
        Show(pill);
        var raised = Record(pill);
        var peer = ControlAutomationPeer.CreatePeerForElement(pill);
        peer.GetAutomationControlType().ShouldBe(AutomationControlType.Button);
        var expand = peer.GetProvider<IExpandCollapseProvider>()!;
        expand.ShowsMenu.ShouldBeFalse();
        expand.ExpandCollapseState.ShouldBe(ExpandCollapseState.Collapsed);
        expand.Expand();
        raised.Count.ShouldBe(1);
        pill.IsOpen = true;
        expand.ExpandCollapseState.ShouldBe(ExpandCollapseState.Expanded);
        expand.Expand();
        raised.Count.ShouldBe(1);
        expand.Collapse();
        raised.Count.ShouldBe(2);
        peer.GetProvider<IInvokeProvider>()!.Invoke();
        raised.Count.ShouldBe(3);
        peer.GetChildren().ShouldBeEmpty();
    }

    #endregion

    #region Motion

    [AvaloniaFact]
    public void The_State_Crossfades_And_The_Width_Follows_On_The_Quick_Spring()
    {
        var clock = new ManualFrameClock();
        using var installed = clock.Install();
        var pill = PromptMenuSamples.VersionPill();
        var latest = PromptMenuSamples.VersionPill(production: false);
        var window = Show(new StackPanel { Spacing = 8, Children = { pill, latest } }, ArcMotionLevel.Full);
        CoreFrames.Run(clock, 10);
        Settle(window);
        var wide = pill.TextSize!.Bounds.Width;
        var narrow = latest.TextSize!.Bounds.Width;
        narrow.ShouldBeLessThan(wide);
        var swap = StateSwap(pill);
        var label = pill.CurrentLabel;

        pill.IsProduction = false;
        Settle(window);
        Layers(swap).ShouldBe(2);
        pill.CurrentLabel.ShouldBeSameAs(label);
        Layers(LabelSwap(pill)).ShouldBe(1);
        var previous = wide;
        for (var frame = 0; frame < 6; frame++)
        {
            CoreFrames.Run(clock, 1);
            Settle(window);
            var width = pill.TextSize.Bounds.Width;
            width.ShouldBeLessThanOrEqualTo(previous + 0.001);
            previous = width;
        }
        previous.ShouldBeLessThan(wide);
        previous.ShouldBeGreaterThan(narrow + 0.5);
        CoreFrames.Run(clock, 40);
        Settle(window);
        pill.TextSize.Bounds.Width.ShouldBe(narrow, 0.01);
        WaitFor(() => Layers(swap) == 1).ShouldBeTrue();
    }

    [AvaloniaFact]
    public void Reduced_Motion_Crossfades_But_Jumps_The_Width()
    {
        var clock = new ManualFrameClock();
        using var installed = clock.Install();
        var pill = PromptMenuSamples.VersionPill();
        var latest = PromptMenuSamples.VersionPill(production: false);
        var window = Show(new StackPanel { Spacing = 8, Children = { pill, latest } }, ArcMotionLevel.Reduced);
        CoreFrames.Run(clock, 10);
        Settle(window);
        pill.IsProduction = false;
        Settle(window);
        pill.TextSize!.Bounds.Width.ShouldBe(latest.TextSize!.Bounds.Width, 0.01);
        Layers(StateSwap(pill)).ShouldBe(2);
        CoreFrames.Run(clock, 10);
        Settle(window);
        WaitFor(() => Layers(StateSwap(pill)) == 1).ShouldBeTrue();
    }

    [AvaloniaFact]
    public void Motion_Off_Changes_At_Once()
    {
        var pill = PromptMenuSamples.VersionPill();
        var latest = PromptMenuSamples.VersionPill(production: false);
        var window = Show(new StackPanel { Spacing = 8, Children = { pill, latest } });
        pill.IsProduction = false;
        Settle(window);
        Layers(StateSwap(pill)).ShouldBe(1);
        pill.TextSize!.Bounds.Width.ShouldBe(latest.TextSize!.Bounds.Width, 0.01);
        pill.Label = "v5";
        Settle(window);
        pill.CurrentLabel!.Text.ShouldBe("v5");
        Layers(LabelSwap(pill)).ShouldBe(1);
    }

    [AvaloniaFact]
    public void The_Same_Words_Make_No_New_Layer()
    {
        var pill = PromptMenuSamples.VersionPill();
        Show(pill);
        var state = pill.CurrentState;
        var label = pill.CurrentLabel;
        pill.LatestText = "Newest";
        pill.NameFormat = (l, _) => l;
        pill.CurrentState.ShouldBeSameAs(state);
        pill.CurrentLabel.ShouldBeSameAs(label);
    }

    #endregion
}
