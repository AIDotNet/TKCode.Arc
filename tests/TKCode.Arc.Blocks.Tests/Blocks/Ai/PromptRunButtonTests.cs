using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Threading;
using TKCode.Arc.DesignTokens;
using TKCode.Arc.Motion;
using TKCode.Arc.Primitives;
using TKCode.Arc.Tests.CorePrimitives;

namespace TKCode.Arc.Tests.Blocks.Ai;

/// <summary>
/// Prompt studio's Run button: Run and Stop (glyph and label swap, one width for both), the requests it raises, the
/// platform's shortcut cap, the accent look and the press, the label-mode swap and the width spring, and its names.
/// </summary>
public class PromptRunButtonTests
{
    private static PromptRunButton Create() => new()
    {
        RunText = "Run",
        StopText = "Stop",
        RunAccessibleName = "Run prompt",
        StopAccessibleName = "Stop run"
    };

    private static Window Show(Control content, ArcMotionLevel level = ArcMotionLevel.Off)
    {
        var window = TestHelpers.Show(new StackPanel { Margin = new Thickness(20), Children = { content } }, level);
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

    private static string? LabelOf(PromptRunButton button) => button.LabelSwap!.Content as string;

    private static Geometry? GlyphOf(PromptRunButton button) => button.IconSwap!.Content as Geometry;

    [AvaloniaFact]
    public void Idle_Shows_Play_And_Run_And_Running_Shows_Square_And_Stop()
    {
        var button = Create();
        var window = Show(button);
        try
        {
            LabelOf(button).ShouldBe("Run");
            GlyphOf(button).ShouldBeSameAs(ArcIcons.Play);
            button.Classes.Contains(PromptRunButton.RunningPseudoClass).ShouldBeFalse();
            // The glyph is drawn filled (a PromptRunGlyph from the swap's template).
            TestHelpers.Descendant<PromptRunGlyph>(button).Data.ShouldBeSameAs(ArcIcons.Play);

            button.IsRunning = true;
            Settle(window);
            LabelOf(button).ShouldBe("Stop");
            GlyphOf(button).ShouldBeSameAs(ArcIcons.Square);
            button.Classes.Contains(PromptRunButton.RunningPseudoClass).ShouldBeTrue();

            button.IsRunning = false;
            LabelOf(button).ShouldBe("Run");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Run_And_Stop_Share_One_Width()
    {
        var button = Create();
        var window = Show(button);
        try
        {
            var idle = button.Bounds.Width;
            idle.ShouldBeGreaterThan(0);
            button.IsRunning = true;
            Settle(window);
            button.Bounds.Width.ShouldBe(idle, 0.01);

            // The slot is as wide as the wider label.
            var run = TestHelpers.Descendant<TextBlock>(button, "PART_RunReserve");
            var stop = TestHelpers.Descendant<TextBlock>(button, "PART_StopReserve");
            var slot = TestHelpers.Descendant<Grid>(button, "PART_LabelSlot");
            slot.Bounds.Width.ShouldBe(Math.Max(run.DesiredSize.Width, stop.DesiredSize.Width), 0.01);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void A_Click_Asks_To_Run_While_Idle_And_To_Stop_While_Running()
    {
        var button = Create();
        var window = Show(button);
        try
        {
            var log = new List<string>();
            button.Click += (_, _) => log.Add("click");
            button.RunRequested += (_, e) => log.Add("run:" + ReferenceEquals(e.Source, button));
            button.StopRequested += (_, _) => log.Add("stop");

            Click(window, button);
            log.ShouldBe(["click", "run:True"]);
            // The owner flips the state; the button does not.
            button.IsRunning.ShouldBeFalse();

            button.IsRunning = true;
            Click(window, button);
            log.ShouldBe(["click", "run:True", "click", "stop"]);

            // Keys work like any button.
            button.Focus(NavigationMethod.Tab);
            window.KeyPressQwerty(PhysicalKey.Enter, RawInputModifiers.None);
            window.KeyReleaseQwerty(PhysicalKey.Enter, RawInputModifiers.None);
            Settle(window);
            log[^1].ShouldBe("stop");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void The_Requests_Bubble_To_The_Owner()
    {
        var button = Create();
        var host = new Border { Child = button };
        var window = Show(host);
        try
        {
            var runs = 0;
            host.AddHandler(PromptRunButton.RunRequestedEvent, (_, _) => runs++);
            Click(window, button);
            runs.ShouldBe(1);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void The_Shortcut_Cap_Shows_The_Platforms_Run_Shortcut_Muted()
    {
        var saved = PromptKbd.PlatformOverride;
        try
        {
            PromptKbd.PlatformOverride = false;
            var button = Create();
            var window = Show(button);
            try
            {
                var cap = TestHelpers.Descendant<PromptKbd>(button);
                button.EffectiveShortcutText.ShouldBe("Ctrl ↵");
                cap.Text.ShouldBe("Ctrl ↵");
                cap.IsMuted.ShouldBeTrue();
                cap.IsVisible.ShouldBeTrue();
                // The cap takes the button's text color (at 62%).
                TestHelpers.ColorOf(cap.Foreground).ShouldBe(ArcTokens.GetColor(button, ArcDesignTokenKind.AccentForeground));
                cap.Opacity.ShouldBe(0.62);

                button.ShortcutText = "F5";
                cap.Text.ShouldBe("F5");
                button.ShortcutText = "";
                Settle(window);
                cap.IsVisible.ShouldBeFalse();
                button.ShortcutText = null;
                cap.Text.ShouldBe("Ctrl ↵");
            }
            finally
            {
                window.Close();
            }

            PromptKbd.PlatformOverride = true;
            var mac = Create();
            mac.EffectiveShortcutText.ShouldBe("⌘↵");
            AutomationProperties.GetAcceleratorKey(mac).ShouldBe("Cmd+Enter");
        }
        finally
        {
            PromptKbd.PlatformOverride = saved;
        }
    }

    [AvaloniaFact]
    public void The_Button_Is_A_36px_Accent_Pill_That_Darkens_Under_The_Pointer()
    {
        var button = Create();
        var window = Show(button);
        try
        {
            button.Bounds.Height.ShouldBe(36);
            button.Padding.ShouldBe(new Thickness(13, 0, 14, 0));
            var frame = TestHelpers.Descendant<Border>(button, "PART_Frame");
            frame.CornerRadius.ShouldBe(ArcTokens.Get<CornerRadius>(button, ArcDesignTokenKind.RadiusPill));
            TestHelpers.ColorOf(frame.Background).ShouldBe(ArcTokens.GetColor(button, ArcDesignTokenKind.Accent));
            TestHelpers.ColorOf(button.Foreground).ShouldBe(ArcTokens.GetColor(button, ArcDesignTokenKind.AccentForeground));
            button.FontSize.ShouldBe(14);
            button.FontWeight.ShouldBe(FontWeight.Medium);
            TestHelpers.Descendant<ArcContentSwap>(button, "PART_Icon").Bounds.Size.ShouldBe(new Size(13, 13));

            var point = button.TranslatePoint(new Point(10, 10), window)!.Value;
            window.MouseMove(point);
            Settle(window);
            TestHelpers.ColorOf(frame.Background).ShouldBe(ArcTokens.GetColor(button, ArcDesignTokenKind.AccentStrong));
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void A_Keyboard_Focus_Draws_The_Ring_Two_Pixels_Outside()
    {
        var button = Create();
        var window = Show(button);
        try
        {
            var ring = TestHelpers.Descendant<Border>(button, "PART_FocusRing");
            ring.IsVisible.ShouldBeFalse();
            button.Focus(NavigationMethod.Tab);
            Settle(window);
            ring.IsVisible.ShouldBeTrue();
            ring.Margin.ShouldBe(new Thickness(-4));
            ring.BorderThickness.ShouldBe(new Thickness(2));
            button.FocusAdorner.ShouldBeNull();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void The_Swaps_Use_Arcs_Label_Mode_Without_Moving()
    {
        var button = Create();
        var window = Show(button);
        try
        {
            foreach (var swap in new[] { button.IconSwap!, button.LabelSwap! })
            {
                swap.Mode.ShouldBe(ContentSwapMode.Label);
                swap.EnterOffset.ShouldBe(0);
            }
            var size = TestHelpers.Descendant<SpringSizeDecorator>(button, "PART_LabelSize");
            size.Axis.ShouldBe(SpringSizeAxis.Width);
            size.Spring.ShouldBe(ArcSpring.Quick);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void At_Full_Motion_Longer_Words_Spring_The_Width_On_The_Quick_Spring()
    {
        var clock = new ManualFrameClock();
        using var installed = clock.Install();
        var button = Create();
        var window = Show(button, ArcMotionLevel.Full);
        try
        {
            var before = button.Bounds.Width;
            button.RunText = "Run the prompt";
            // The spring starts at the next layout pass.
            Settle(window);
            CoreFrames.Run(clock, 3);
            Settle(window);
            var during = button.Bounds.Width;
            CoreFrames.Run(clock, 60);
            Settle(window);
            var after = button.Bounds.Width;
            after.ShouldBeGreaterThan(before + 20);
            during.ShouldBeGreaterThan(before);
            during.ShouldBeLessThan(after);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Screen_Readers_Hear_Run_Prompt_Or_Stop_Run_With_The_Shortcut()
    {
        var button = Create();
        var window = Show(button);
        try
        {
            var peer = ControlAutomationPeer.CreatePeerForElement(button);
            peer.GetName().ShouldBe("Run prompt");
            peer.GetAutomationControlType().ShouldBe(AutomationControlType.Button);
            // The label and the key cap are not read separately (Arc's aria-hidden).
            peer.GetChildren().ShouldBeEmpty();
            AutomationProperties.GetAcceleratorKey(button).ShouldBe(PromptKbd.RunAcceleratorKey);

            button.IsRunning = true;
            peer.GetName().ShouldBe("Stop run");

            // Without accessible names the visible labels are used.
            button.StopAccessibleName = null;
            peer.GetName().ShouldBe("Stop");
            button.IsRunning = false;
            button.RunAccessibleName = null;
            peer.GetName().ShouldBe("Run");
        }
        finally
        {
            window.Close();
        }
    }
}
