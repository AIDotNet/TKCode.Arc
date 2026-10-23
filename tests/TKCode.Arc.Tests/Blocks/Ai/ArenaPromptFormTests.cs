using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Automation.Provider;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;
using TKCode.Arc.DesignTokens;
using TKCode.Arc.Motion;
using TKCode.Arc.Primitives;
using TKCode.Arc.Tests.CorePrimitives;

namespace TKCode.Arc.Tests.Blocks.Ai;

/// <summary>
/// The model arena's prompt row: the box and its keys (Enter, Ctrl / ⌘ + Enter, Escape), Run and Stop in one width, the
/// disabled Run, the sample prompt chips (pressed state, a chip runs its own prompt, the shortcut runs the box's), the
/// compact row, the Tab order, the look, and every name.
/// </summary>
public class ArenaPromptFormTests
{
    private static ArenaPrompt[] Prompts() =>
    [
        new() { Id = "sql", Label = "Dedupe orders in SQL", Text = "Write a SQL query that removes duplicate rows from an orders table." },
        new() { Id = "debounce", Label = "Debounce vs throttle", Text = "Explain debounce and throttle to a new frontend developer." },
        new() { Id = "move", Label = "Move a 1:1", Text = "Draft a short note asking Jonas to move our Friday 1:1." }
    ];

    private static ArenaPromptForm Create(IEnumerable<ArenaPrompt>? prompts = null, string? text = null) => new()
    {
        Placeholder = "Ask both models one question",
        PromptName = "Prompt sent to both models",
        RunText = "Run",
        StopText = "Stop",
        ShortcutHint = "Ctrl ↵",
        PromptsName = "Sample prompts",
        Prompts = prompts ?? Prompts(),
        Text = text ?? "Write a SQL query that removes duplicate rows from an orders table."
    };

    private static Window Show(Control content, ArcMotionLevel level = ArcMotionLevel.Off, double width = 784)
    {
        var window = TestHelpers.Show(new Border { Width = width, Child = content, VerticalAlignment = Avalonia.Layout.VerticalAlignment.Top }, level);
        window.Width = 900;
        window.Height = 400;
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

    /// <summary>The swap's layers once a finished exit has been removed (the removal is posted from a continuation).</summary>
    private static int LayersAfterRemoval(ArcContentSwap swap, int expected)
    {
        var count = 0;
        for (var i = 0; i < 100; i++)
        {
            Dispatcher.UIThread.RunJobs();
            count = swap.GetVisualChildren().OfType<ContentPresenter>().Count();
            if (count == expected)
            {
                break;
            }
            Thread.Sleep(5);
        }
        return count;
    }

    private sealed class Log
    {
        public List<string> Entries { get; } = new();

        public Log(ArenaPromptForm form)
        {
            form.RunRequested += (_, _) => Entries.Add("run");
            form.StopRequested += (_, _) => Entries.Add("stop");
            form.PromptChosen += (_, e) => Entries.Add("chose:" + e.Prompt.Id);
        }
    }

    [AvaloniaFact]
    public void Enter_In_The_Box_Asks_To_Run_And_Never_Breaks_The_Line()
    {
        var form = Create();
        var window = Show(form);
        try
        {
            var log = new Log(form);
            form.FocusBox().ShouldBeTrue();
            Key(window, PhysicalKey.Enter);
            Key(window, PhysicalKey.Enter, RawInputModifiers.Shift);
            log.Entries.ShouldBe(["run", "run"]);
            form.Text.ShouldNotContain("\n");
            // The owner runs; the form does not change its own state.
            form.IsRunning.ShouldBeFalse();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Ctrl_And_Cmd_Enter_In_The_Box_Ask_To_Run()
    {
        var form = Create();
        var window = Show(form);
        try
        {
            var log = new Log(form);
            form.FocusBox();
            Key(window, PhysicalKey.Enter, RawInputModifiers.Control);
            Key(window, PhysicalKey.Enter, RawInputModifiers.Meta);
            log.Entries.ShouldBe(["run", "run"]);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Nothing_Runs_From_An_Empty_Box_Or_When_Running_Is_Not_Possible()
    {
        var form = Create(text: "");
        var window = Show(form);
        try
        {
            var log = new Log(form);
            form.FocusBox();
            Key(window, PhysicalKey.Enter);
            Key(window, PhysicalKey.Enter, RawInputModifiers.Control);
            form.Text = "   ";
            Key(window, PhysicalKey.Enter);
            form.Text = "Compare two sorting algorithms.";
            form.CanRun = false;
            Key(window, PhysicalKey.Enter);
            log.Entries.ShouldBeEmpty();
            form.CanRun = true;
            Key(window, PhysicalKey.Enter);
            log.Entries.ShouldBe(["run"]);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Run_Is_Disabled_At_52_Percent_Without_Text_And_Stop_Stays_Enabled()
    {
        var form = Create(text: "");
        var window = Show(form);
        try
        {
            var run = form.RunButton!;
            run.IsEnabled.ShouldBeFalse();
            run.Opacity.ShouldBe(0.52, 0.001);
            form.FocusRun().ShouldBeFalse();

            form.Text = "Plan a launch checklist.";
            run.IsEnabled.ShouldBeTrue();
            run.Opacity.ShouldBe(1, 0.001);

            form.CanRun = false;
            run.IsEnabled.ShouldBeFalse();

            // While running the button is Stop, whatever the box holds.
            form.Text = "";
            form.IsRunning = true;
            run.IsEnabled.ShouldBeTrue();
            form.FocusRun().ShouldBeTrue();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Run_Asks_To_Run_And_Stop_Asks_To_Stop()
    {
        var form = Create();
        var window = Show(form);
        try
        {
            var log = new Log(form);
            Click(window, form.RunButton!);
            form.IsRunning = true;
            Settle(window);
            Click(window, form.RunButton!);
            log.Entries.ShouldBe(["run", "stop"]);

            // Enter and Space on the focused button click it like any button.
            form.FocusRun();
            Key(window, PhysicalKey.Enter);
            log.Entries[^1].ShouldBe("stop");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Escape_Stops_From_The_Box_Or_The_Button_And_Leaves_The_Focus_Where_It_Is()
    {
        var form = Create();
        var window = Show(form);
        try
        {
            var log = new Log(form);
            form.FocusBox();
            Key(window, PhysicalKey.Escape);
            log.Entries.ShouldBeEmpty();

            form.IsRunning = true;
            Key(window, PhysicalKey.Escape);
            form.Box!.IsFocused.ShouldBeTrue();
            form.FocusRun();
            Key(window, PhysicalKey.Escape);
            form.RunButton!.IsFocused.ShouldBeTrue();
            // Not from a chip.
            form.Chips[0].Focus(NavigationMethod.Tab);
            Key(window, PhysicalKey.Escape);
            log.Entries.ShouldBe(["stop", "stop"]);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void A_Chip_Asks_For_Its_Prompt_But_The_Shortcut_On_A_Chip_Or_On_Run_Runs_The_Box()
    {
        var prompts = Prompts();
        var form = Create(prompts, "My own question about caching");
        var window = Show(form);
        try
        {
            var log = new Log(form);
            Click(window, form.Chips[1]);
            log.Entries.ShouldBe(["chose:debounce"]);
            // A click never flips the chip: the box decides.
            form.Chips[1].IsChecked.ShouldBe(false);

            form.Chips[2].Focus(NavigationMethod.Tab);
            Key(window, PhysicalKey.Enter);
            log.Entries[^1].ShouldBe("chose:move");

            log.Entries.Clear();
            Key(window, PhysicalKey.Enter, RawInputModifiers.Control);
            form.FocusRun();
            Key(window, PhysicalKey.Enter, RawInputModifiers.Meta);
            log.Entries.ShouldBe(["run", "run"]);
            form.Text.ShouldBe("My own question about caching");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void A_Chip_Is_Pressed_While_The_Box_Holds_Its_Prompt()
    {
        var prompts = Prompts();
        var form = Create(prompts, prompts[0].Text);
        var window = Show(form);
        try
        {
            form.Chips.Select(c => c.IsChecked).ShouldBe([true, false, false]);
            form.Text = prompts[1].Text;
            form.Chips.Select(c => c.IsChecked).ShouldBe([false, true, false]);
            // Editing the text by hand releases every chip.
            form.Text = prompts[1].Text + " Keep it short.";
            form.Chips.Select(c => c.IsChecked).ShouldBe([false, false, false]);
            // A prompt whose text changes is compared again.
            prompts[2].Text = form.Text;
            form.Chips.Select(c => c.IsChecked).ShouldBe([false, false, true]);
            // An empty box presses nothing, even a chip without text.
            form.Prompts = [new ArenaPrompt { Id = "blank", Label = "Blank" }];
            form.Text = "";
            form.Chips.Single().IsChecked.ShouldBe(false);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void The_Chips_Follow_The_Prompts_And_Hide_When_There_Are_None()
    {
        var prompts = new Avalonia.Collections.AvaloniaList<ArenaPrompt>(Prompts());
        var form = Create(prompts);
        var window = Show(form);
        try
        {
            form.Chips.Select(c => c.Label).ShouldBe(["Dedupe orders in SQL", "Debounce vs throttle", "Move a 1:1"]);
            prompts[0].Label = "Remove duplicate orders";
            form.Chips[0].Label.ShouldBe("Remove duplicate orders");

            prompts.RemoveAt(1);
            Settle(window);
            form.Chips.Count.ShouldBe(2);
            form.ChipsPanel!.Children.Count.ShouldBe(2);

            var withChips = form.Bounds.Height;
            prompts.Clear();
            Settle(window);
            form.ChipsScroller!.IsVisible.ShouldBeFalse();
            // Without chips the row is the field and the padding: 6 + 48 + 14.
            form.Bounds.Height.ShouldBe(68, 0.5);
            withChips.ShouldBe(106, 0.5);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Run_And_Stop_Share_The_Width_Of_The_Wider_Face_And_Crossfade()
    {
        var form = Create();
        var window = Show(form);
        try
        {
            var run = form.RunButton!;
            var idle = run.Bounds.Width;
            var runReserve = TestHelpers.Descendant<Decorator>(run, ArenaRunButton.RunReservePart);
            var stopReserve = TestHelpers.Descendant<Decorator>(run, ArenaRunButton.StopReservePart);
            runReserve.Bounds.Width.ShouldBeGreaterThan(stopReserve.Child!.DesiredSize.Width);
            // 12 + the wider face + 12, inside the 1px edge.
            idle.ShouldBe(runReserve.Child!.DesiredSize.Width + 26, 0.5);

            form.IsRunning = true;
            Settle(window);
            run.Bounds.Width.ShouldBe(idle, 0.01);
            run.Classes.Contains(ArenaRunButton.RunningPseudoClass).ShouldBeTrue();
            form.Classes.Contains(ArenaPromptForm.RunningPseudoClass).ShouldBeTrue();

            var face = run.FaceSwap!;
            face.Mode.ShouldBe(ContentSwapMode.Crossfade);
            var stopFace = (StackPanel)face.Content!;
            stopFace.Children.OfType<PromptRunGlyph>().Single().Data.ShouldBeSameAs(ArcIcons.Square);
            stopFace.Children.OfType<TextBlock>().Single().Text.ShouldBe("Stop");
            stopFace.Children.OfType<PromptKbd>().ShouldBeEmpty();

            form.IsRunning = false;
            var runFace = (StackPanel)face.Content!;
            runFace.Children.OfType<ArcGlyph>().Single().Data.ShouldBeSameAs(ArcIcons.Play);
            runFace.Children.OfType<TextBlock>().Single().Text.ShouldBe("Run");
            runFace.Children.OfType<PromptKbd>().Single().Text.ShouldBe("Ctrl ↵");
            // A new face every time: a quick Run → Stop → Run never puts one control in two layers.
            runFace.ShouldNotBeSameAs(runReserve.Child);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void At_Full_Motion_The_Faces_Crossfade_Out_In_100ms_And_In_Over_160ms()
    {
        var clock = new ManualFrameClock();
        using var installed = clock.Install();
        var form = Create();
        var window = Show(form, ArcMotionLevel.Full);
        try
        {
            var face = form.RunButton!.FaceSwap!;
            form.IsRunning = true;
            Settle(window);
            var layers = face.GetVisualChildren().OfType<ContentPresenter>().ToList();
            layers.Count.ShouldBe(2);
            var incoming = layers.Single(p => p.Content == face.Content);
            incoming.Opacity.ShouldBe(0, 0.001);
            CoreFrames.Run(clock, 4);
            incoming.Opacity.ShouldBeGreaterThan(0);
            incoming.Opacity.ShouldBeLessThan(1);
            ((TranslateTransform)incoming.RenderTransform!).Y.ShouldBe(0, 0.001);
            CoreFrames.Run(clock, 8);
            incoming.Opacity.ShouldBe(1, 0.001);
            LayersAfterRemoval(face, 1).ShouldBe(1);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void The_Compact_Row_Hides_The_Key_Cap()
    {
        var form = Create();
        var window = Show(form);
        try
        {
            var run = form.RunButton!;
            var wide = run.Bounds.Width;
            form.IsCompact = true;
            Settle(window);
            form.Classes.Contains(ArenaPromptForm.CompactPseudoClass).ShouldBeTrue();
            run.ShortcutText.ShouldBeNull();
            ((StackPanel)run.FaceSwap!.Content!).Children.OfType<PromptKbd>().ShouldBeEmpty();
            run.Bounds.Width.ShouldBeLessThan(wide - 10);

            form.IsCompact = false;
            form.ShortcutHint = "⌘↵";
            ((StackPanel)run.FaceSwap!.Content!).Children.OfType<PromptKbd>().Single().Text.ShouldBe("⌘↵");
            form.ShortcutHint = null;
            ((StackPanel)run.FaceSwap!.Content!).Children.OfType<PromptKbd>().ShouldBeEmpty();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Tab_Goes_From_The_Box_To_Run_To_The_Chips()
    {
        var form = Create();
        var window = Show(form);
        try
        {
            form.FocusBox();
            var order = new List<object?> { window.FocusManager!.GetFocusedElement() };
            for (var i = 0; i < 4; i++)
            {
                Key(window, PhysicalKey.Tab);
                order.Add(window.FocusManager!.GetFocusedElement());
            }
            order.ShouldBe([form.Box, form.RunButton, form.Chips[0], form.Chips[1], form.Chips[2]]);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Screen_Readers_Hear_The_Box_Run_Or_Stop_And_A_Group_Of_Toggle_Chips()
    {
        var form = Create();
        var window = Show(form);
        try
        {
            AutomationProperties.GetName(form.Box!).ShouldBe("Prompt sent to both models");
            var runPeer = ControlAutomationPeer.CreatePeerForElement(form.RunButton!);
            runPeer.GetName().ShouldBe("Run");
            runPeer.GetChildren().ShouldBeEmpty();
            AutomationProperties.GetAcceleratorKey(form.RunButton!).ShouldBe(PromptKbd.RunAcceleratorKey);
            form.IsRunning = true;
            runPeer.GetName().ShouldBe("Stop");

            var group = ControlAutomationPeer.CreatePeerForElement(form.ChipsPanel!);
            group.GetName().ShouldBe("Sample prompts");
            group.GetAutomationControlType().ShouldBe(AutomationControlType.Group);

            var chip = ControlAutomationPeer.CreatePeerForElement(form.Chips[0]);
            chip.GetName().ShouldBe("Dedupe orders in SQL");
            ((IToggleProvider)chip).ToggleState.ShouldBe(ToggleState.On);
            ((IToggleProvider)ControlAutomationPeer.CreatePeerForElement(form.Chips[1])).ToggleState.ShouldBe(ToggleState.Off);

            form.PromptName = "Question for both";
            form.PromptsName = "Examples";
            AutomationProperties.GetName(form.Box!).ShouldBe("Question for both");
            group.GetName().ShouldBe("Examples");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void The_Field_Is_A_48px_Frame_Inside_The_Padding_And_The_Chips_Run_To_The_Edges()
    {
        var form = Create();
        var window = Show(form);
        try
        {
            form.Bounds.Height.ShouldBe(106, 0.5);
            var field = form.Field!;
            field.Bounds.Height.ShouldBe(48);
            field.Bounds.X.ShouldBe(20, 0.01);
            field.Bounds.Y.ShouldBe(6, 0.01);
            field.Bounds.Width.ShouldBe(784 - 40, 0.01);
            field.CornerRadius.ShouldBe(ArcTokens.Get<CornerRadius>(form, ArcDesignTokenKind.RadiusControl));
            field.Padding.ShouldBe(new Thickness(16, 0, 5, 0));
            TestHelpers.ColorOf(field.Background).ShouldBe(ArcTokens.GetColor(form, ArcDesignTokenKind.Surface));

            var run = form.RunButton!;
            run.Bounds.Height.ShouldBe(36);
            // 5px above and below inside the 1px edge.
            run.TranslatePoint(default, field)!.Value.Y.ShouldBe(6, 0.01);
            (field.Bounds.Width - run.TranslatePoint(new Point(run.Bounds.Width, 0), field)!.Value.X).ShouldBe(6, 0.01);

            var scroller = form.ChipsScroller!;
            scroller.Bounds.X.ShouldBe(0, 0.01);
            scroller.Bounds.Width.ShouldBe(784, 0.01);
            scroller.Bounds.Y.ShouldBe(6 + 48 + 10, 0.01);
            var first = form.Chips[0];
            first.TranslatePoint(default, form)!.Value.X.ShouldBe(20, 0.01);
            first.Bounds.Height.ShouldBe(28);
            (form.Chips[1].TranslatePoint(default, form)!.Value.X - first.TranslatePoint(new Point(first.Bounds.Width, 0), form)!.Value.X)
                .ShouldBe(6, 0.01);
            scroller.HorizontalScrollBarVisibility.ShouldBe(Avalonia.Controls.Primitives.ScrollBarVisibility.Hidden);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Narrow_Chips_Scroll_Sideways_On_One_Line()
    {
        var form = Create();
        var window = Show(form, width: 358);
        try
        {
            var scroller = form.ChipsScroller!;
            scroller.Extent.Width.ShouldBeGreaterThan(scroller.Viewport.Width);
            form.Chips.Select(c => c.TranslatePoint(default, form)!.Value.Y).Distinct().Count().ShouldBe(1);
            form.Chips[2].Focus(NavigationMethod.Tab);
            Settle(window);
            scroller.Offset.X.ShouldBeGreaterThan(0);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void The_Field_Border_Turns_Strong_While_The_Focus_Is_Inside_And_Draws_No_Ring()
    {
        var form = Create();
        var window = Show(form);
        try
        {
            var field = form.Field!;
            TestHelpers.ColorOf(field.BorderBrush).ShouldBe(ArcTokens.GetColor(form, ArcDesignTokenKind.Border));
            form.FocusBox();
            Settle(window);
            TestHelpers.ColorOf(field.BorderBrush).ShouldBe(ArcTokens.GetColor(form, ArcDesignTokenKind.BorderStrong));
            form.Box!.FocusAdorner.ShouldBeNull();
            form.FocusRun();
            Settle(window);
            TestHelpers.ColorOf(field.BorderBrush).ShouldBe(ArcTokens.GetColor(form, ArcDesignTokenKind.BorderStrong));
            TestHelpers.Descendant<Border>(form.RunButton!, "PART_FocusRing").IsVisible.ShouldBeTrue();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Run_Is_Accent_While_Idle_And_Takes_The_Secondary_Look_While_Running()
    {
        var form = Create();
        var window = Show(form);
        try
        {
            var run = form.RunButton!;
            var frame = TestHelpers.Descendant<Border>(run, "PART_Frame");
            TestHelpers.ColorOf(frame.Background).ShouldBe(ArcTokens.GetColor(run, ArcDesignTokenKind.Accent));
            TestHelpers.ColorOf(frame.BorderBrush).ShouldBe(ArcTokens.GetColor(run, ArcDesignTokenKind.Accent));
            TestHelpers.ColorOf(run.Foreground).ShouldBe(ArcTokens.GetColor(run, ArcDesignTokenKind.AccentForeground));
            run.Padding.ShouldBe(new Thickness(12, 0));
            run.FontSize.ShouldBe(14);
            run.FontWeight.ShouldBe(FontWeight.Medium);
            var cap = ((StackPanel)run.FaceSwap!.Content!).Children.OfType<PromptKbd>().Single();
            cap.IsMuted.ShouldBeTrue();
            TestHelpers.ColorOf(cap.Foreground).ShouldBe(ArcTokens.GetColor(run, ArcDesignTokenKind.AccentForeground));

            form.IsRunning = true;
            Settle(window);
            TestHelpers.ColorOf(frame.Background).ShouldBe(ArcTokens.GetColor(run, ArcDesignTokenKind.Surface));
            TestHelpers.ColorOf(frame.BorderBrush).ShouldBe(ArcTokens.GetColor(run, ArcDesignTokenKind.Border));
            TestHelpers.ColorOf(run.Foreground).ShouldBe(ArcTokens.GetColor(run, ArcDesignTokenKind.Foreground));

            var point = run.TranslatePoint(new Point(10, 10), window)!.Value;
            window.MouseMove(point);
            Settle(window);
            TestHelpers.ColorOf(frame.Background).ShouldBe(ArcTokens.GetColor(run, ArcDesignTokenKind.SurfaceMuted));
            run.Opacity.ShouldBe(1, 0.001);
            form.IsRunning = false;
            Settle(window);
            run.Opacity.ShouldBe(0.91, 0.001);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Chips_Are_28px_Pills_That_Read_Pressed_And_Hovered()
    {
        var prompts = Prompts();
        var form = Create(prompts, prompts[0].Text);
        var window = Show(form);
        try
        {
            var pressed = form.Chips[0];
            var plain = form.Chips[1];
            pressed.FontSize.ShouldBe(12);
            TestHelpers.ColorOf(pressed.Background).ShouldBe(ArcTokens.GetColor(form, ArcDesignTokenKind.SurfaceMuted));
            TestHelpers.ColorOf(pressed.BorderBrush).ShouldBe(ArcTokens.GetColor(form, ArcDesignTokenKind.BorderStrong));
            TestHelpers.ColorOf(pressed.Foreground).ShouldBe(ArcTokens.GetColor(form, ArcDesignTokenKind.Foreground));
            TestHelpers.ColorOf(plain.BorderBrush).ShouldBe(ArcTokens.GetColor(form, ArcDesignTokenKind.Border));
            TestHelpers.ColorOf(plain.Foreground).ShouldBe(ArcTokens.GetColor(form, ArcDesignTokenKind.TextSecondary));

            var tint = TestHelpers.Descendant<Border>(plain, "PART_Tint");
            tint.Opacity.ShouldBe(0);
            window.MouseMove(plain.TranslatePoint(new Point(10, 10), window)!.Value);
            Settle(window);
            tint.Opacity.ShouldBe(0.05, 0.001);
            TestHelpers.ColorOf(plain.Foreground).ShouldBe(ArcTokens.GetColor(form, ArcDesignTokenKind.Foreground));
            TestHelpers.ColorOf(plain.BorderBrush).ShouldBe(ArcTokens.GetColor(form, ArcDesignTokenKind.BorderStrong));
            // The label sits 12px in (1px edge and 11px padding).
            TestHelpers.Descendant<TextBlock>(plain, "PART_Label").TranslatePoint(default, plain)!.Value.X.ShouldBe(12, 0.01);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Without_The_Focus_A_Long_Prompt_Ends_In_An_Ellipsis()
    {
        var form = Create();
        var window = Show(form);
        try
        {
            var box = form.Box!;
            var resting = TestHelpers.Descendant<TextBlock>(box, "PART_Resting");
            var presenter = TestHelpers.Descendant<TextPresenter>(box, "PART_TextPresenter");
            resting.IsVisible.ShouldBeTrue();
            resting.TextTrimming.ShouldBe(TextTrimming.CharacterEllipsis);
            presenter.Opacity.ShouldBe(0);

            form.FocusBox();
            Settle(window);
            resting.IsVisible.ShouldBeFalse();
            presenter.Opacity.ShouldBe(1);
            box.CaretIndex.ShouldBe(form.Text.Length);

            // Empty: the placeholder.
            form.FocusRun();
            form.Text = "";
            Settle(window);
            resting.IsVisible.ShouldBeFalse();
            TestHelpers.Descendant<TextBlock>(box, "PART_Placeholder").IsVisible.ShouldBeTrue();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Typing_In_The_Box_Writes_The_Text_Back()
    {
        var form = Create(text: "");
        var window = Show(form);
        try
        {
            form.FocusBox();
            window.KeyTextInput("Hi");
            Settle(window);
            form.Text.ShouldBe("Hi");
            form.RunButton!.IsEnabled.ShouldBeTrue();
        }
        finally
        {
            window.Close();
        }
    }
}
