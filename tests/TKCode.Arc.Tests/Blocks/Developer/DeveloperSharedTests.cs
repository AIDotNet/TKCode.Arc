using Avalonia;
using Avalonia.Automation;
using Avalonia.Collections;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;
using TKCode.Arc.Motion;
using TKCode.Arc.Primitives;

namespace TKCode.Arc.Tests.Blocks.Developer;

public class DeveloperSharedTests
{
    private const double RowHeight = 40;

    private static Window Show(Control control, ArcMotionLevel level = ArcMotionLevel.Off, double width = 600, double height = 500)
    {
        var window = TestHelpers.Show(control, level);
        window.Width = width;
        window.Height = height;
        window.UpdateLayout();
        Dispatcher.UIThread.RunJobs();
        return window;
    }

    private static void Pump(Window window)
    {
        window.UpdateLayout();
        Dispatcher.UIThread.RunJobs();
        window.UpdateLayout();
    }

    private static void Step(ManualFrameClock clock, Window window, double milliseconds)
    {
        // Frames and layout interleave like on screen: one layout pass after every frame.
        var frames = (int)Math.Ceiling(milliseconds / (1000.0 / 60.0));
        for (var i = 0; i < frames; i++)
        {
            clock.Advance(TimeSpan.FromMilliseconds(1000.0 / 60.0));
            window.UpdateLayout();
        }
        Dispatcher.UIThread.RunJobs();
    }

    private static DeveloperListPanel Panel(AvaloniaList<string> items, DeveloperListMotion motion) => new()
    {
        Items = items,
        Motion = motion,
        ItemTemplate = new FuncDataTemplate<string>((text, _) => new Border
        {
            Height = RowHeight,
            Background = Brushes.Transparent,
            Child = new TextBlock { Text = text },
        }),
        VerticalAlignment = Avalonia.Layout.VerticalAlignment.Top,
    };

    private static double LayoutTop(DeveloperListPanel panel, string item) => panel.ContainerFor(item)!.Bounds.Y;

    private static double DrawnTop(DeveloperListPanel panel, string item) => LayoutTop(panel, item) + panel.StateOf(item)!.Value.Offset;

    // ---- Text and springs ----

    [Fact]
    public void Text_Helpers_Count_Format_Join_And_Mask_Without_Throwing()
    {
        DeveloperText.Count(1, "1 event", "{0} events").ShouldBe("1 event");
        DeveloperText.Count(3, "1 event", "{0} events").ShouldBe("3 events");
        DeveloperText.Count(0, "{0} key", "{0} keys").ShouldBe("0 keys");
        DeveloperText.Format("{0} of {1", 1).ShouldBe("{0} of {1");
        DeveloperText.Format(null).ShouldBe(string.Empty);
        DeveloperText.JoinNames(["Development"]).ShouldBe("Development");
        DeveloperText.JoinNames(["Development", " ", "Staging"]).ShouldBe("Development and Staging");
        DeveloperText.JoinNames(["Development", "Staging", "Production"]).ShouldBe("Development, Staging and Production");
        DeveloperText.Mask("rk_live_", "7c3e").ShouldBe("rk_live_••••7c3e");
        DeveloperText.Mask(null, "4c1e", 2).ShouldBe("••4c1e");
        DeveloperText.Bullets(16).Length.ShouldBe(16);
        DeveloperText.Bullets(-1).ShouldBe(string.Empty);
        DeveloperText.Thousands(942317).ShouldBe(942317.ToString("N0", System.Globalization.CultureInfo.CurrentCulture));
    }

    [Fact]
    public void Springs_Reuse_The_Core_Presets_And_Build_Critical_Springs()
    {
        DeveloperSprings.Smooth.ShouldBe(ArcMotionPresets.Smooth);
        DeveloperSprings.Quick.ShouldBe(ArcMotionPresets.Quick);
        DeveloperSprings.Settle.ShouldBe(ArcMotionPresets.Settle);
        DeveloperSprings.Critical(13).DampingRatio.ShouldBe(1, 1e-9);
        Should.Throw<ArgumentOutOfRangeException>(() => DeveloperSprings.Critical(0));
        DeveloperFlash.Glow.Hold.ShouldBe(TimeSpan.FromMilliseconds(480));
        (DeveloperFlash.Glow.Hold!.Value + DeveloperFlash.Glow.FadeDuration).ShouldBe(TimeSpan.FromMilliseconds(1600));
        DeveloperFlash.Held.Hold.ShouldBeNull();
        DeveloperListMotion.Filter.ExitBlur.ShouldBe(4);
        DeveloperListMotion.Disclosure.Exit.ShouldBe(DeveloperListExit.Collapse);
    }

    // ---- Announcer ----

    [AvaloniaFact]
    public void The_Announcer_Says_Repeated_Text_Again_And_Can_Be_Cleared()
    {
        var announcer = new DeveloperAnnouncer();
        var heard = new List<string>();
        announcer.Announced += heard.Add;
        announcer.Announce("Key copied.");
        AutomationProperties.GetName(announcer).ShouldBe("Key copied.");
        announcer.Announce("Key copied.");
        AutomationProperties.GetName(announcer).ShouldNotBe("Key copied.");
        AutomationProperties.GetName(announcer)!.TrimEnd('​').ShouldBe("Key copied.");
        announcer.Announce("Key copied.");
        AutomationProperties.GetName(announcer).ShouldBe("Key copied.");
        announcer.Announce(null);
        announcer.Announce(string.Empty);
        announcer.History.Count.ShouldBe(3);
        heard.Count.ShouldBe(3);
        announcer.Announcement.ShouldBe("Key copied.");
        announcer.Clear();
        announcer.Announcement.ShouldBeNull();
        AutomationProperties.GetName(announcer).ShouldBe(string.Empty);
        AutomationProperties.GetLiveSetting(announcer).ShouldBe(AutomationLiveSetting.Polite);
        announcer.IsAssertive = true;
        AutomationProperties.GetLiveSetting(announcer).ShouldBe(AutomationLiveSetting.Assertive);
        announcer.Focusable.ShouldBeFalse();
        announcer.IsHitTestVisible.ShouldBeFalse();
    }

    // ---- List panel ----

    [AvaloniaFact]
    public void The_List_Stacks_Its_Items_In_Order_Without_Motion_At_First()
    {
        var items = new AvaloniaList<string> { "a", "b", "c" };
        var panel = Panel(items, DeveloperListMotion.Filter);
        var window = Show(panel, ArcMotionLevel.Full);
        try
        {
            panel.LiveItems.ShouldBe(["a", "b", "c"]);
            panel.Bounds.Height.ShouldBe(3 * RowHeight);
            LayoutTop(panel, "c").ShouldBe(2 * RowHeight);
            panel.StateOf("b")!.Value.Opacity.ShouldBe(1);
            panel.IsMoving.ShouldBeFalse();
            panel.ContainerFor("b")!.DataContext.ShouldBe("b");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Filtering_Flips_Staying_Rows_From_Where_They_Were_Drawn_And_Springs_The_Height()
    {
        var clock = new ManualFrameClock();
        using var installed = clock.Install();
        var items = new AvaloniaList<string> { "a", "b", "c", "d" };
        var panel = Panel(items, DeveloperListMotion.Filter);
        var window = Show(panel, ArcMotionLevel.Full);
        try
        {
            items.Remove("a");
            items.Remove("c");
            Pump(window);
            // Each staying row is drawn where it was and glides home.
            LayoutTop(panel, "b").ShouldBe(0);
            panel.StateOf("b")!.Value.Offset.ShouldBe(RowHeight, 0.5);
            panel.StateOf("d")!.Value.Offset.ShouldBe(2 * RowHeight, 0.5);
            DrawnTop(panel, "d").ShouldBe(3 * RowHeight, 0.5);
            // The panel height springs down from four rows to two.
            panel.ContentHeight.ShouldBe(2 * RowHeight);
            panel.ContainerHeight.ShouldBe(4 * RowHeight, 1);

            Step(clock, window, 120);
            panel.StateOf("d")!.Value.Offset.ShouldBeInRange(1, 2 * RowHeight - 1);
            panel.StateOf("d")!.Value.OffsetVelocity.ShouldBeLessThan(0);
            panel.ContainerHeight.ShouldBeInRange(2 * RowHeight + 1, 4 * RowHeight - 1);

            Step(clock, window, 1500);
            panel.StateOf("d")!.Value.Offset.ShouldBe(0, 0.01);
            panel.ContainerHeight.ShouldBe(2 * RowHeight, 0.01);
            panel.Bounds.Height.ShouldBe(2 * RowHeight, 0.5);
            panel.IsMoving.ShouldBeFalse();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void A_Row_That_Fades_Out_Leaves_The_Layout_But_Keeps_Drawing_Until_Done()
    {
        var clock = new ManualFrameClock();
        using var installed = clock.Install();
        var items = new AvaloniaList<string> { "a", "b", "c" };
        var panel = Panel(items, DeveloperListMotion.Filter with { Container = null });
        var exited = new List<object>();
        panel.ItemExited += (_, e) => exited.Add(e.Item);
        var window = Show(panel, ArcMotionLevel.Full);
        try
        {
            var a = panel.ContainerFor("a")!;
            items.Remove("a");
            Pump(window);
            panel.LiveItems.ShouldBe(["b", "c"]);
            panel.ExitingCount.ShouldBe(1);
            panel.ContentHeight.ShouldBe(2 * RowHeight);
            panel.Bounds.Height.ShouldBe(2 * RowHeight);
            // Popped out: still a child, drawn where it was, below the others, not hit-testable.
            a.GetVisualParent().ShouldBe(panel);
            a.Bounds.Y.ShouldBe(0);
            a.ZIndex.ShouldBe(-1);
            a.IsHitTestVisible.ShouldBeFalse();
            // b is laid out where a was, yet drawn where it was.
            LayoutTop(panel, "b").ShouldBe(0);
            DrawnTop(panel, "b").ShouldBe(RowHeight, 0.5);

            Step(clock, window, 80);
            a.Opacity.ShouldBeInRange(0.01, 0.99);
            a.Effect.ShouldNotBeNull();
            panel.StateOf("a")!.Value.IsLeaving.ShouldBeTrue();

            Step(clock, window, 300);
            a.GetVisualParent().ShouldBeNull();
            panel.ExitingCount.ShouldBe(0);
            panel.ContainerFor("a").ShouldBeNull();
            exited.ShouldBe(["a"]);
            // Handed back clean.
            a.Opacity.ShouldBe(1);
            a.Effect.ShouldBeNull();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void A_Returning_Row_Fades_In_At_Its_Place_Out_Of_A_Blur()
    {
        var clock = new ManualFrameClock();
        using var installed = clock.Install();
        var items = new AvaloniaList<string> { "a", "c" };
        var panel = Panel(items, DeveloperListMotion.Filter);
        var window = Show(panel, ArcMotionLevel.Full);
        try
        {
            items.Insert(1, "b");
            Pump(window);
            var b = panel.ContainerFor("b")!;
            LayoutTop(panel, "b").ShouldBe(RowHeight);
            panel.StateOf("b")!.Value.Offset.ShouldBe(0);
            b.Opacity.ShouldBe(0, 0.01);
            panel.StateOf("c")!.Value.Offset.ShouldBe(-RowHeight, 0.5);
            Step(clock, window, 100);
            b.Opacity.ShouldBeInRange(0.05, 0.99);
            b.Effect.ShouldNotBeNull();
            Step(clock, window, 1500);
            b.Opacity.ShouldBe(1);
            b.Effect.ShouldBeNull();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void A_Glide_That_Is_Retargeted_Keeps_Its_Drawn_Place_And_Its_Velocity()
    {
        var clock = new ManualFrameClock();
        using var installed = clock.Install();
        var items = new AvaloniaList<string> { "a", "b", "c" };
        var panel = Panel(items, DeveloperListMotion.Filter);
        var window = Show(panel, ArcMotionLevel.Full);
        try
        {
            items.Remove("a");
            Pump(window);
            Step(clock, window, 100);
            var before = panel.StateOf("b")!.Value;
            before.OffsetVelocity.ShouldBeLessThan(0);
            var drawn = DrawnTop(panel, "b");

            // A new change mid-glide (a back in front, b's slot moves again): b continues from where it is drawn, with its speed.
            items.Insert(0, "x");
            Pump(window);
            DrawnTop(panel, "b").ShouldBe(drawn, 0.5);
            var after = panel.StateOf("b")!.Value;
            after.OffsetVelocity.ShouldBe(before.OffsetVelocity, Math.Abs(before.OffsetVelocity) * 0.05);

            Step(clock, window, 1500);
            panel.StateOf("b")!.Value.Offset.ShouldBe(0, 0.01);
            LayoutTop(panel, "b").ShouldBe(RowHeight);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void A_Row_Removed_And_Put_Back_Mid_Exit_Is_Revived_From_Where_It_Fades()
    {
        var clock = new ManualFrameClock();
        using var installed = clock.Install();
        var items = new AvaloniaList<string> { "a", "b" };
        var panel = Panel(items, DeveloperListMotion.Filter);
        var window = Show(panel, ArcMotionLevel.Full);
        try
        {
            var a = panel.ContainerFor("a")!;
            items.Remove("a");
            Pump(window);
            Step(clock, window, 60);
            items.Insert(0, "a");
            Pump(window);
            panel.ContainerFor("a").ShouldBeSameAs(a);
            panel.ExitingCount.ShouldBe(0);
            a.IsHitTestVisible.ShouldBeTrue();
            Step(clock, window, 1500);
            a.Opacity.ShouldBe(1);
            panel.LiveItems.ShouldBe(["a", "b"]);
            a.GetVisualParent().ShouldBe(panel);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void An_Inserted_Row_Grows_From_Zero_Fades_In_Late_And_Flashes()
    {
        var clock = new ManualFrameClock();
        using var installed = clock.Install();
        var items = new AvaloniaList<string> { "b", "c" };
        var panel = Panel(items, DeveloperListMotion.Insert with { Flash = DeveloperFlash.Timed });
        var window = Show(panel, ArcMotionLevel.Full);
        try
        {
            items.Insert(0, "a");
            Pump(window);
            var state = panel.StateOf("a")!.Value;
            state.HeightFactor.ShouldBe(0, 0.01);
            state.Flash.ShouldBe(1);
            panel.Bounds.Height.ShouldBe(2 * RowHeight, 0.5);
            // The rows below are pushed by the height, not flipped.
            panel.StateOf("b")!.Value.Offset.ShouldBe(0);
            LayoutTop(panel, "b").ShouldBe(0, 0.5);

            Step(clock, window, 50);
            panel.StateOf("a")!.Value.Opacity.ShouldBe(0, 0.01);     // the fade waits 80ms
            Step(clock, window, 150);
            var mid = panel.StateOf("a")!.Value;
            mid.HeightFactor.ShouldBeInRange(0.05, 0.99);
            mid.Opacity.ShouldBeInRange(0.05, 0.99);
            panel.Bounds.Height.ShouldBeInRange(2 * RowHeight + 1, 3 * RowHeight - 1);
            LayoutTop(panel, "b").ShouldBe(mid.HeightFactor * RowHeight, 1);
            panel.ContainerFor("a")!.Clip.ShouldNotBeNull();

            Step(clock, window, 1300);
            panel.StateOf("a")!.Value.HeightFactor.ShouldBe(1);
            panel.ContainerFor("a")!.Clip.ShouldBeNull();
            panel.StateOf("a")!.Value.Flash.ShouldBe(1);               // holds 1.4s, then 300ms delay
            Step(clock, window, 600);
            panel.StateOf("a")!.Value.Flash.ShouldBeInRange(0.01, 0.99);
            Step(clock, window, 1400);
            panel.StateOf("a")!.Value.Flash.ShouldBe(0);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void A_Held_Flash_Stays_Until_It_Is_Released()
    {
        var clock = new ManualFrameClock();
        using var installed = clock.Install();
        var items = new AvaloniaList<string> { "b" };
        var panel = Panel(items, DeveloperListMotion.Insert with { Flash = DeveloperFlash.Held });
        var window = Show(panel, ArcMotionLevel.Full);
        try
        {
            items.Insert(0, "a");
            Pump(window);
            Step(clock, window, 5000);
            panel.StateOf("a")!.Value.Flash.ShouldBe(1);
            panel.EndFlash("a");
            Step(clock, window, 250);
            panel.StateOf("a")!.Value.Flash.ShouldBe(1);                // 300ms delay
            Step(clock, window, 1400);
            panel.StateOf("a")!.Value.Flash.ShouldBe(0);

            // Flash() on a live row (a saved Env variable) glows once.
            panel.Flash("b", DeveloperFlash.Glow).ShouldBeTrue();
            panel.StateOf("b")!.Value.Flash.ShouldBe(1);
            Step(clock, window, 1700);
            panel.StateOf("b")!.Value.Flash.ShouldBe(0);
            panel.Flash("missing").ShouldBeFalse();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void A_Collapsing_Row_Stays_In_Place_And_Pulls_The_Rows_Below_Up()
    {
        var clock = new ManualFrameClock();
        using var installed = clock.Install();
        var items = new AvaloniaList<string> { "a", "b", "c" };
        var panel = Panel(items, DeveloperListMotion.Disclosure);
        var window = Show(panel, ArcMotionLevel.Full);
        try
        {
            var b = panel.ContainerFor("b")!;
            items.Remove("b");
            Pump(window);
            panel.LiveItems.ShouldBe(["a", "c"]);
            panel.ExitingCount.ShouldBe(1);
            b.IsHitTestVisible.ShouldBeFalse();
            LayoutTop(panel, "b").ShouldBe(RowHeight);
            LayoutTop(panel, "c").ShouldBe(2 * RowHeight, 0.5);

            Step(clock, window, 90);                                     // 180ms EaseExit: half way in time
            var factor = panel.StateOf("b")!.Value.HeightFactor;
            factor.ShouldBeInRange(0.05, 0.99);
            LayoutTop(panel, "c").ShouldBe(RowHeight + factor * RowHeight, 1);
            panel.StateOf("c")!.Value.Offset.ShouldBe(0);               // pulled by layout, no FLIP

            Step(clock, window, 200);
            b.GetVisualParent().ShouldBeNull();
            LayoutTop(panel, "c").ShouldBe(RowHeight);
            panel.Bounds.Height.ShouldBe(2 * RowHeight);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void A_Collapsing_Row_Keeps_Its_Slot_When_Other_Rows_Change_Meanwhile()
    {
        var clock = new ManualFrameClock();
        using var installed = clock.Install();
        var items = new AvaloniaList<string> { "a", "b", "c" };
        var panel = Panel(items, DeveloperListMotion.Insert);
        var window = Show(panel, ArcMotionLevel.Full);
        try
        {
            items.Remove("b");
            Pump(window);
            items.Insert(0, "z");
            Pump(window);
            panel.LiveItems.ShouldBe(["z", "a", "c"]);
            // Laid out order: z, a, (b collapsing), c.
            LayoutTop(panel, "b").ShouldBeGreaterThan(LayoutTop(panel, "a"));
            LayoutTop(panel, "c").ShouldBeGreaterThanOrEqualTo(LayoutTop(panel, "b"));
            Step(clock, window, 1500);
            panel.ExitingCount.ShouldBe(0);
            panel.Bounds.Height.ShouldBe(3 * RowHeight, 0.5);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Reduced_Motion_Removes_Leaving_Rows_At_Once_And_Fades_New_Rows_Briefly()
    {
        var clock = new ManualFrameClock();
        using var installed = clock.Install();
        var items = new AvaloniaList<string> { "a", "b" };
        var panel = Panel(items, DeveloperListMotion.Insert with { Flash = DeveloperFlash.Timed });
        var window = Show(panel, ArcMotionLevel.Reduced);
        try
        {
            var a = panel.ContainerFor("a")!;
            items.Remove("a");
            Pump(window);
            a.GetVisualParent().ShouldBeNull();
            panel.ExitingCount.ShouldBe(0);
            panel.StateOf("b")!.Value.Offset.ShouldBe(0);
            panel.Bounds.Height.ShouldBe(RowHeight);

            items.Add("c");
            Pump(window);
            var c = panel.StateOf("c")!.Value;
            c.HeightFactor.ShouldBe(1);
            c.Opacity.ShouldBe(0, 0.01);
            c.Flash.ShouldBe(1);                                         // shown, without its fade
            Step(clock, window, 60);
            panel.StateOf("c")!.Value.Opacity.ShouldBeInRange(0.05, 0.99);
            panel.ContainerFor("c")!.Effect.ShouldBeNull();
            Step(clock, window, 100);
            panel.StateOf("c")!.Value.Opacity.ShouldBe(1);
            Step(clock, window, 1800);
            panel.StateOf("c")!.Value.Flash.ShouldBe(0);

            // Env variables' glow is not shown at all under reduced motion.
            panel.Flash("c", DeveloperFlash.Glow);
            panel.StateOf("c")!.Value.Flash.ShouldBe(0);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Motion_Off_Jumps_But_A_Held_Flash_Still_Marks_The_Row()
    {
        var items = new AvaloniaList<string> { "a", "b" };
        var panel = Panel(items, DeveloperListMotion.Filter with { Flash = DeveloperFlash.Held });
        var window = Show(panel, ArcMotionLevel.Off);
        try
        {
            items.Remove("a");
            items.Insert(0, "z");
            Pump(window);
            panel.ExitingCount.ShouldBe(0);
            panel.StateOf("z")!.Value.Opacity.ShouldBe(1);
            panel.StateOf("z")!.Value.Flash.ShouldBe(1);
            panel.StateOf("b")!.Value.Offset.ShouldBe(0);
            panel.Bounds.Height.ShouldBe(2 * RowHeight);
            panel.EndFlash("z");
            panel.StateOf("z")!.Value.Flash.ShouldBe(0);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void A_One_Off_Motion_Overrides_The_Panel_Motion_And_Replacing_Items_Diffs_By_Identity()
    {
        var clock = new ManualFrameClock();
        using var installed = clock.Install();
        var items = new AvaloniaList<string> { "a", "b", "c" };
        var panel = Panel(items, DeveloperListMotion.Filter);
        var window = Show(panel, ArcMotionLevel.Full);
        try
        {
            var c = panel.ContainerFor("c")!;
            panel.Change(DeveloperListMotion.Instant, () => items.RemoveAt(0));
            Pump(window);
            panel.ExitingCount.ShouldBe(0);
            panel.StateOf("b")!.Value.Offset.ShouldBe(RowHeight, 0.5);     // Instant has no exit, staying rows still glide

            panel.Items = new List<string> { "c", "d" };
            Pump(window);
            panel.ContainerFor("c").ShouldBeSameAs(c);
            panel.LiveItems.ShouldBe(["c", "d"]);
        }
        finally
        {
            window.Close();
        }
    }

    // ---- Morph panel ----

    private static (DeveloperMorphPanel Morph, TextBox Field, TextBlock Count) Morph()
    {
        var field = new TextBox { Width = 200, HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Left };
        var count = new TextBlock { Text = "3 active keys" };
        var morph = new DeveloperMorphPanel
        {
            Title = "Create key",
            Leading = count,
            Content = new StackPanel { Spacing = 8, Children = { field, new Button { Content = "Generate" } } },
            Width = 560,
            VerticalAlignment = Avalonia.Layout.VerticalAlignment.Top,
        };
        return (morph, field, count);
    }

    [AvaloniaFact]
    public void The_Morph_Panel_Opens_To_Full_Width_Focuses_The_First_Field_And_Escape_Returns_The_Focus()
    {
        var (morph, field, count) = Morph();
        var window = Show(morph);
        try
        {
            var shell = morph.Shell!;
            var toggle = morph.Toggle!;
            shell.Bounds.Height.ShouldBe(36);
            shell.Bounds.Width.ShouldBeLessThan(300);
            shell.Bounds.Right.ShouldBe(560, 0.5);                   // right-anchored
            AutomationProperties.GetName(toggle).ShouldBe("Create key");
            count.IsEffectivelyVisible.ShouldBeTrue();
            morph.IsBodyShown.ShouldBeFalse();
            var leading = Leading(morph);
            leading.IsVisible.ShouldBeTrue();

            var opened = 0;
            var closed = new List<DeveloperMorphCloseReason>();
            morph.Opened += (_, _) => opened++;
            morph.Closed += (_, e) => closed.Add(e.Reason);

            toggle.Focus(NavigationMethod.Tab);
            window.KeyPressQwerty(PhysicalKey.Enter, RawInputModifiers.None);
            Pump(window);
            morph.IsOpen.ShouldBeTrue();
            morph.Classes.Contains(":open").ShouldBeTrue();
            opened.ShouldBe(1);
            shell.Bounds.Width.ShouldBe(560, 0.5);
            shell.Bounds.Height.ShouldBeGreaterThan(36 + 30);
            morph.PanelWidth.ShouldBe(560, 0.5);
            AutomationProperties.GetName(toggle).ShouldBe("Close");
            leading.IsVisible.ShouldBeFalse();                       // hidden and inert like Arc's visibility: hidden
            leading.IsHitTestVisible.ShouldBeFalse();
            field.IsFocused.ShouldBeTrue();

            window.KeyPressQwerty(PhysicalKey.Escape, RawInputModifiers.None);
            Pump(window);
            morph.IsOpen.ShouldBeFalse();
            closed.ShouldBe([DeveloperMorphCloseReason.Escape]);
            toggle.IsFocused.ShouldBeTrue();
            shell.Bounds.Height.ShouldBe(36);
            morph.IsBodyShown.ShouldBeFalse();
            leading.IsVisible.ShouldBeTrue();
            leading.Opacity.ShouldBe(1);
            leading.IsHitTestVisible.ShouldBeTrue();

            // The toggle closes too.
            morph.Open();
            Pump(window);
            toggle.Focus(NavigationMethod.Tab);
            window.KeyPressQwerty(PhysicalKey.Enter, RawInputModifiers.None);
            Pump(window);
            morph.IsOpen.ShouldBeFalse();
            morph.Open();
            Pump(window);
            morph.Close();
            Pump(window);
            closed.ShouldBe([DeveloperMorphCloseReason.Escape, DeveloperMorphCloseReason.Toggle, DeveloperMorphCloseReason.Programmatic]);
            opened.ShouldBe(3);
        }
        finally
        {
            window.Close();
        }
    }

    private static Control Leading(DeveloperMorphPanel morph) => TestHelpers.Descendant<Control>(morph, DeveloperMorphPanel.LeadingPart);

    [AvaloniaFact]
    public void Clicking_Anywhere_On_The_Closed_Pill_Opens_It()
    {
        var (morph, _, _) = Morph();
        var window = Show(morph);
        try
        {
            var shell = morph.Shell!;
            var point = shell.TranslatePoint(new Point(shell.Bounds.Width - 10, 18), window)!.Value;
            AvaloniaHeadlessPlatform.ForceRenderTimerTick();
            Dispatcher.UIThread.RunJobs();
            window.MouseDown(point, MouseButton.Left);
            window.MouseUp(point, MouseButton.Left);
            Pump(window);
            morph.IsOpen.ShouldBeTrue();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void The_Morph_Springs_Both_Axes_Fades_The_Body_Late_And_Folds_After_The_Body_Fades()
    {
        var clock = new ManualFrameClock();
        using var installed = clock.Install();
        var (morph, _, count) = Morph();
        var window = Show(morph, ArcMotionLevel.Full);
        try
        {
            var shell = morph.Shell!;
            var closedSize = shell.CurrentSize;
            morph.IsOpen = true;
            Pump(window);
            var target = shell.TargetSize;
            target.Width.ShouldBe(560, 0.5);

            Step(clock, window, 100);
            shell.CurrentSize.Width.ShouldBeInRange(closedSize.Width + 1, target.Width - 1);
            shell.CurrentSize.Height.ShouldBeInRange(closedSize.Height + 1, target.Height - 1);
            var bodyHost = morph.Shell!.Body!;
            bodyHost.Opacity.ShouldBe(0, 0.01);                      // waits 140ms
            Leading(morph).Opacity.ShouldBeLessThan(1);

            Step(clock, window, 100);
            bodyHost.Opacity.ShouldBeInRange(0.05, 0.99);
            Step(clock, window, 1500);
            shell.CurrentSize.Width.ShouldBe(target.Width, 0.5);
            bodyHost.Opacity.ShouldBe(1);

            // The plus turned into a cross on a spring.
            var icon = morph.GetVisualDescendants().OfType<ArcGlyph>().First(g => g.Name == "PART_Icon");
            icon.RenderTransform.ShouldNotBeNull();

            morph.IsOpen = false;
            Pump(window);
            Step(clock, window, 50);
            morph.IsBodyShown.ShouldBeTrue();                        // still fading (100ms)
            Step(clock, window, 100);
            morph.IsBodyShown.ShouldBeFalse();
            Step(clock, window, 1500);
            shell.CurrentSize.Width.ShouldBe(closedSize.Width, 0.5);
            shell.CurrentSize.Height.ShouldBe(36, 0.5);
            Leading(morph).Opacity.ShouldBe(1);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void New_Content_While_Open_Springs_The_Panel_Height()
    {
        var (morph, _, _) = Morph();
        var window = Show(morph);
        try
        {
            morph.IsOpen = true;
            Pump(window);
            var before = morph.Shell!.Bounds.Height;
            var copy = new Button { Content = "Copy" };
            morph.Content = new StackPanel { Children = { new Border { Height = 300 }, copy } };
            Pump(window);
            var expected = 36 + 4 + 300 + copy.Bounds.Height + 14;
            morph.Shell!.Bounds.Height.ShouldBe(expected, 0.5);
            morph.Shell!.Bounds.Height.ShouldBeGreaterThan(before);
            // The focused field left with the old content: the focus moves on to the new content's first stop.
            copy.IsFocused.ShouldBeTrue();
        }
        finally
        {
            window.Close();
        }
    }

    // ---- Secret field ----

    private static IEnumerable<string> TreeText(Visual root)
    {
        foreach (var visual in root.GetVisualDescendants().Prepend(root))
        {
            switch (visual)
            {
                case TextBlock { Text: { } text }:
                    yield return text;
                    break;
                case TextBox { Text: { } boxText }:
                    yield return boxText;
                    break;
                case CopyButton { Value: { } value }:
                    yield return value;
                    break;
            }
            if (visual is Control control && AutomationProperties.GetName(control) is { Length: > 0 } name)
            {
                yield return name;
            }
        }
    }

    private static bool TreeContains(Visual root, string secret) => TreeText(root).Any(t => t.Contains(secret, StringComparison.Ordinal));

    [AvaloniaFact]
    public void A_One_Time_Secret_Shows_Once_Copies_And_Leaves_No_Trace_When_Cleared()
    {
        var field = new DeveloperSecretField { Label = "Secret key", Notice = "Copy it now.", Subject = "Checkout service" };
        var window = Show(field);
        try
        {
            field.Classes.Contains(":empty").ShouldBeTrue();
            field.SetSecret("rk_live_8Qz1Ab");
            Pump(window);
            field.HasSecret.ShouldBeTrue();
            TestHelpers.Descendant<TextBlock>(field, DeveloperSecretField.SecretPart).Text.ShouldBe("rk_live_8Qz1Ab");
            TestHelpers.Descendant<CopyButton>(field, DeveloperSecretField.CopyPart).Value.ShouldBe("rk_live_8Qz1Ab");
            AutomationProperties.GetName(TestHelpers.Descendant<Border>(field, DeveloperSecretField.BoxPart)).ShouldBe("Secret key");
            TreeText(field).ShouldContain("Copy it now.");

            field.ClearSecret();
            Pump(window);
            field.HasSecret.ShouldBeFalse();
            TreeContains(field, "rk_live_8Qz1Ab").ShouldBeFalse();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void A_One_Time_Secret_Develops_Out_Of_A_Blur()
    {
        var clock = new ManualFrameClock();
        using var installed = clock.Install();
        var field = new DeveloperSecretField { Label = "Signing secret", LabelSpacing = 16 };
        var window = Show(field, ArcMotionLevel.Full);
        try
        {
            field.SetSecret("sig_123");
            var text = TestHelpers.Descendant<TextBlock>(field, DeveloperSecretField.SecretPart);
            text.Opacity.ShouldBe(0);
            Step(clock, window, 100);
            text.Opacity.ShouldBe(0);                                 // 140ms delay
            Step(clock, window, 250);
            text.Opacity.ShouldBeInRange(0.05, 0.99);
            text.Effect.ShouldNotBeNull();
            Step(clock, window, 600);
            text.Opacity.ShouldBe(1);
            text.Effect.ShouldBeNull();
            TestHelpers.Descendant<TextBlock>(field, DeveloperSecretField.LabelPart).Margin.Bottom.ShouldBe(16);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void A_Masked_Value_Is_In_The_Tree_Only_While_Revealed()
    {
        var field = new DeveloperSecretField
        {
            Mode = DeveloperSecretMode.Masked,
            Subject = "CACHE_TTL_SECONDS",
            Width = 320,
            VerticalAlignment = Avalonia.Layout.VerticalAlignment.Top,
        };
        var window = Show(field);
        try
        {
            field.SetSecret("hunter2-value");
            Pump(window);
            TreeContains(field, "hunter2-value").ShouldBeFalse();
            var mask = TestHelpers.Descendant<TextBlock>(field, DeveloperSecretField.MaskPart);
            mask.Text.ShouldBe(DeveloperText.Bullets(16));
            AutomationProperties.GetName(mask).ShouldBe("Hidden value");
            field.Bounds.Height.ShouldBe(36);
            var reveal = TestHelpers.Descendant<DeveloperIconToggle>(field, DeveloperSecretField.RevealPart);
            AutomationProperties.GetName(reveal).ShouldBe("Reveal CACHE_TTL_SECONDS");
            TestHelpers.Descendant<Control>(field, DeveloperSecretField.CopySlotPart).IsVisible.ShouldBeFalse();

            var toggled = new List<bool>();
            field.RevealToggled += (_, on) => toggled.Add(on);
            reveal.Focus(NavigationMethod.Tab);
            window.KeyPressQwerty(PhysicalKey.Space, RawInputModifiers.None);
            window.KeyReleaseQwerty(PhysicalKey.Space, RawInputModifiers.None);
            Pump(window);
            field.IsRevealed.ShouldBeTrue();
            toggled.ShouldBe([true]);
            reveal.IsChecked.ShouldBe(true);
            TestHelpers.Descendant<TextBlock>(field, DeveloperSecretField.SecretPart).Text.ShouldBe("hunter2-value");
            TestHelpers.Descendant<Control>(field, DeveloperSecretField.CopySlotPart).IsVisible.ShouldBeTrue();
            TestHelpers.Descendant<CopyButton>(field, DeveloperSecretField.CopyPart).Value.ShouldBe("hunter2-value");
            field.Bounds.Height.ShouldBe(36);                          // fixed slot: the row does not jump

            field.IsRevealed = false;
            Pump(window);
            TreeContains(field, "hunter2-value").ShouldBeFalse();
            reveal.IsChecked.ShouldBe(false);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void A_Sensitive_Value_Never_Enters_The_Tree()
    {
        var field = new DeveloperSecretField { Mode = DeveloperSecretMode.Masked, Subject = "PAYMENTS_SIGNING_KEY", IsSensitive = true };
        var open = new DeveloperSecretField { Mode = DeveloperSecretMode.Masked, Subject = "TOKEN" };
        var window = Show(new StackPanel { Children = { field, open } });
        try
        {
            var asked = 0;
            field.SecretProvider = () =>
            {
                asked++;
                return "psk_live_secret";
            };
            field.SetSecret("psk_live_secret");
            field.HasSecret.ShouldBeFalse();
            field.IsRevealed = true;
            Pump(window);
            field.IsRevealed.ShouldBeFalse();
            asked.ShouldBe(0);
            TreeContains(field, "psk_live_secret").ShouldBeFalse();
            TestHelpers.Descendant<DeveloperIconToggle>(field, DeveloperSecretField.RevealPart).IsVisible.ShouldBeFalse();
            AutomationProperties.GetName(TestHelpers.Descendant<TextBlock>(field, DeveloperSecretField.MaskPart))
                .ShouldBe("Sensitive value, write-only");
            TreeText(field).ShouldContain("Sensitive");

            // A revealed value made sensitive is dropped at once.
            open.SetSecret("tok_visible");
            open.IsRevealed = true;
            Pump(window);
            TreeContains(open, "tok_visible").ShouldBeTrue();
            open.IsSensitive = true;
            Pump(window);
            open.IsRevealed.ShouldBeFalse();
            open.HasSecret.ShouldBeFalse();
            TreeContains(open, "tok_visible").ShouldBeFalse();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void A_Provided_Value_Is_Fetched_On_Reveal_And_Forgotten_When_Hidden()
    {
        var field = new DeveloperSecretField { Mode = DeveloperSecretMode.Masked, Subject = "MAILER_TOKEN" };
        var asked = 0;
        field.SecretProvider = () =>
        {
            asked++;
            return "mt_live_00example";
        };
        var window = Show(field);
        try
        {
            asked.ShouldBe(0);
            field.IsRevealed = true;
            Pump(window);
            asked.ShouldBe(1);
            field.HasSecret.ShouldBeTrue();
            TreeContains(field, "mt_live_00example").ShouldBeTrue();
            field.IsRevealed = false;
            Pump(window);
            field.HasSecret.ShouldBeFalse();
            TreeContains(field, "mt_live_00example").ShouldBeFalse();
            field.IsRevealed = true;
            Pump(window);
            asked.ShouldBe(2);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Revealing_Crossfades_In_The_Fixed_Slot_And_Pops_The_Copy_Button()
    {
        var clock = new ManualFrameClock();
        using var installed = clock.Install();
        var field = new DeveloperSecretField { Mode = DeveloperSecretMode.Masked, Subject = "KEY", Width = 320 };
        var window = Show(field, ArcMotionLevel.Full);
        try
        {
            field.SetSecret("value-1");
            field.IsRevealed = true;
            var mask = TestHelpers.Descendant<TextBlock>(field, DeveloperSecretField.MaskPart);
            var value = TestHelpers.Descendant<TextBlock>(field, DeveloperSecretField.SecretPart);
            var slot = TestHelpers.Descendant<Control>(field, DeveloperSecretField.CopySlotPart);
            Step(clock, window, 80);
            mask.Opacity.ShouldBeInRange(0.01, 0.99);
            value.Opacity.ShouldBeInRange(0.01, 0.99);
            ((TranslateTransform)value.RenderTransform!).Y.ShouldBeGreaterThan(0);
            ((TranslateTransform)mask.RenderTransform!).Y.ShouldBeLessThan(0);
            ((ScaleTransform)slot.RenderTransform!).ScaleX.ShouldBeInRange(0.8, 0.999);
            Step(clock, window, 800);
            mask.Opacity.ShouldBe(0);
            value.Opacity.ShouldBe(1);
            slot.Opacity.ShouldBe(1);

            field.IsRevealed = false;
            Step(clock, window, 100);
            value.Text.ShouldBe("value-1");                             // still fading out
            Step(clock, window, 400);
            value.Text.ShouldBeNull();
            slot.IsVisible.ShouldBeFalse();
            mask.Opacity.ShouldBe(1);
        }
        finally
        {
            window.Close();
        }
    }
}
