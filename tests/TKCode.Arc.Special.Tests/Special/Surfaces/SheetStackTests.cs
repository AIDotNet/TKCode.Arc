using Avalonia;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Threading;
using Avalonia.VisualTree;
using TKCode.Arc.Motion;
using AvaloniaButton = Avalonia.Controls.Button;

namespace TKCode.Arc.Tests.Special.Surfaces;

/// <summary>The sheet stack control: stack operations, presentation, focus, keyboard, drag and motion.</summary>
public class SheetStackTests
{
    private static readonly TimeSpan Frame = TimeSpan.FromMilliseconds(1000.0 / 60);

    private sealed class Fixture
    {
        public SheetStack Stack = null!;
        public Window Window = null!;
        public AvaloniaButton OpenButton = null!;
        public AvaloniaButton ProfileRow = null!;
        public AvaloniaButton PickButton = null!;
        public AvaloniaButton SaveButton = null!;
        public TextBox NameBox = null!;
        public Border Excluded = null!;
        public readonly List<SheetStackChangedEventArgs> Changes = new();

        public SheetStackSheet Sheet(string id) => Stack.Sheets.First(s => s.Id == id);

        public SheetStackEntry Entry(string id) => Stack.Layer.Entries.First(e => e.Sheet.Id == id);

        public void Pump()
        {
            Dispatcher.UIThread.RunJobs();
            AvaloniaHeadlessPlatform.ForceRenderTimerTick();
            Dispatcher.UIThread.RunJobs();
        }

        public Point Center(Visual visual)
            => visual.TranslatePoint(new Point(visual.Bounds.Width / 2, visual.Bounds.Height / 2), Window) ?? default;

        public void Click(Visual visual)
        {
            Pump();
            var p = Center(visual);
            Window.MouseMove(p, RawInputModifiers.None);
            Window.MouseDown(p, MouseButton.Left);
            Window.MouseUp(p, MouseButton.Left);
            Pump();
        }

        public void Drag(Point from, double dy, int steps = 10, int pauseMs = 0)
        {
            Pump();
            Window.MouseMove(from, RawInputModifiers.None);
            Window.MouseDown(from, MouseButton.Left);
            for (var i = 1; i <= steps; i++)
            {
                Window.MouseMove(from + new Vector(0, dy * i / steps), RawInputModifiers.LeftMouseButton);
                if (pauseMs > 0)
                {
                    Thread.Sleep(pauseMs);
                }
            }
            Window.MouseUp(from + new Vector(0, dy), MouseButton.Left);
            Pump();
        }

        public void Key(PhysicalKey key, RawInputModifiers modifiers = RawInputModifiers.None)
        {
            Window.KeyPressQwerty(key, modifiers);
            Window.KeyReleaseQwerty(key, modifiers);
            Pump();
        }

        public IInputElement? Focused => Window.FocusManager?.GetFocusedElement();
    }

    private static Fixture Open(ArcMotionLevel level = ArcMotionLevel.Off, double width = 742, double height = 578,
                                bool contained = true, SheetStackMode mode = SheetStackMode.Auto)
    {
        var f = new Fixture();
        f.OpenButton = new TKCode.Arc.Button { Content = "Open settings", HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
        SheetStack.SetOpenSheet(f.OpenButton, "account");
        f.ProfileRow = new TKCode.Arc.Button { Content = "Mira Rautio", Height = 66 };
        SheetStack.SetOpenSheet(f.ProfileRow, "profile");
        f.PickButton = new TKCode.Arc.Button { Content = "Pick avatar" };
        SheetStack.SetOpenSheet(f.PickButton, "avatar");
        f.SaveButton = new TKCode.Arc.Button { Content = "Save changes", IsEnabled = false };
        f.NameBox = new TextBox { Text = "Mira Rautio" };
        f.Excluded = new Border { Height = 120, Background = Avalonia.Media.Brushes.Transparent };
        SheetStack.SetIsDragExcluded(f.Excluded, true);
        f.Stack = new SheetStack
        {
            IsContained = contained,
            Mode = mode,
            Width = width,
            Height = height,
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Top,
            Content = f.OpenButton,
            Sheets =
            {
                new SheetStackSheet
                {
                    Id = "account", Title = "Account",
                    Content = new StackPanel { Spacing = 14, Children = { f.ProfileRow, new Border { Height = 140 } } }
                },
                new SheetStackSheet
                {
                    Id = "profile", Title = "Your profile", BackLabel = "Details",
                    Content = new StackPanel { Spacing = 14, Children = { f.PickButton, f.NameBox, new Border { Height = 200 } } },
                    Footer = f.SaveButton
                },
                new SheetStackSheet
                {
                    Id = "avatar", Title = "Pick avatar",
                    Content = new StackPanel { Children = { f.Excluded, new Border { Height = 200 } } },
                    Footer = new StackPanel
                    {
                        Orientation = Orientation.Horizontal, Spacing = 8,
                        Children = { new AvaloniaButton { Content = "Remove photo" }, new AvaloniaButton { Content = "Use photo" } }
                    }
                }
            }
        };
        f.Stack.StackChanged += (_, e) => f.Changes.Add(e);
        f.Window = TestHelpers.Show(f.Stack, level);
        f.Window.Width = width + 40;
        f.Window.Height = height + 40;
        f.Pump();
        return f;
    }

    [AvaloniaFact]
    public void Push_pop_and_close_follow_the_stack()
    {
        var f = Open();
        try
        {
            f.Stack.IsOpen.ShouldBeFalse();
            f.Stack.Push("account").ShouldBeTrue();
            f.Stack.Push("profile").ShouldBeTrue();
            f.Stack.Push("avatar").ShouldBeTrue();
            f.Stack.Stack.ShouldBe(["account", "profile", "avatar"]);
            f.Stack.IsOpen.ShouldBeTrue();
            f.Sheet("avatar").IsTop.ShouldBeTrue();
            f.Sheet("account").Depth.ShouldBe(2);
            f.Stack.Pop().ShouldBeTrue();
            f.Stack.Stack.ShouldBe(["account", "profile"]);
            f.Stack.Push("avatar");
            f.Stack.PopTo("account").ShouldBeTrue();
            f.Stack.Stack.ShouldBe(["account"]);
            f.Stack.Push("nope").ShouldBeFalse();
            f.Stack.Close();
            f.Stack.Stack.ShouldBeEmpty();
            f.Stack.IsOpen.ShouldBeFalse();
            f.Changes.Select(c => c.Reason).ShouldBe([
                SheetStackChangeReason.Push, SheetStackChangeReason.Push, SheetStackChangeReason.Push,
                SheetStackChangeReason.Programmatic, SheetStackChangeReason.Push, SheetStackChangeReason.Programmatic,
                SheetStackChangeReason.Programmatic
            ]);
            f.Changes[3].OldStack.ShouldBe(["account", "profile", "avatar"]);
            f.Changes[3].NewStack.ShouldBe(["account", "profile"]);
        }
        finally
        {
            f.Window.Close();
        }
    }

    [AvaloniaFact]
    public void Pushing_an_open_sheet_pops_back_to_it()
    {
        var f = Open();
        try
        {
            f.Stack.Push("account");
            f.Stack.Push("profile");
            f.Stack.Push("avatar");
            f.Stack.Push("account").ShouldBeTrue();
            f.Stack.Stack.ShouldBe(["account"]);
        }
        finally
        {
            f.Window.Close();
        }
    }

    [AvaloniaFact]
    public void Pushing_can_be_cancelled()
    {
        var f = Open();
        try
        {
            f.Stack.Pushing += (_, e) => e.Cancel = e.Id == "account" && e.Trigger is AvaloniaButton;
            f.Click(f.OpenButton);
            f.Stack.IsOpen.ShouldBeFalse();
            f.Stack.Push("account").ShouldBeTrue();
        }
        finally
        {
            f.Window.Close();
        }
    }

    [AvaloniaFact]
    public void Stack_property_is_two_way()
    {
        var f = Open();
        try
        {
            f.Stack.Stack = ["account", "profile", "ghost", "account"];
            f.Stack.Stack.ShouldBe(["account", "profile"]);
            f.Sheet("profile").IsTop.ShouldBeTrue();
            f.Changes.Last().Reason.ShouldBe(SheetStackChangeReason.Programmatic);
            f.Stack.Stack = [];
            f.Stack.IsOpen.ShouldBeFalse();
        }
        finally
        {
            f.Window.Close();
        }
    }

    [AvaloniaFact]
    public void Triggers_open_sheets_on_click()
    {
        var f = Open();
        try
        {
            f.Click(f.OpenButton);
            f.Stack.Stack.ShouldBe(["account"]);
            f.Click(f.ProfileRow);
            f.Stack.Stack.ShouldBe(["account", "profile"]);
            f.Click(f.PickButton);
            f.Stack.Stack.ShouldBe(["account", "profile", "avatar"]);
        }
        finally
        {
            f.Window.Close();
        }
    }

    [AvaloniaFact]
    public void Header_shows_the_back_label_and_close_all()
    {
        var f = Open();
        try
        {
            f.Stack.Push("account");
            var account = f.Sheet("account");
            account.BackText.ShouldBeNull();
            account.CloseName.ShouldBe("Close");
            f.Stack.Push("profile");
            f.Sheet("profile").BackText.ShouldBe("Account");
            f.Sheet("profile").CloseName.ShouldBe("Close all");
            f.Stack.Push("avatar");
            // The profile level's back label differs from its title.
            f.Sheet("avatar").BackText.ShouldBe("Details");
            f.Stack.Labels = new SheetStackLabels { CloseAll = "Tout fermer", BackName = l => "Retour à " + l };
            f.Sheet("avatar").CloseName.ShouldBe("Tout fermer");
            f.Sheet("avatar").BackName.ShouldBe("Retour à Details");
        }
        finally
        {
            f.Window.Close();
        }
    }

    [AvaloniaFact]
    public void Close_button_closes_one_or_all()
    {
        var f = Open();
        try
        {
            f.Stack.Push("account");
            f.Stack.Push("profile");
            f.Pump();
            f.Click(f.Sheet("profile").CloseButton!);
            f.Stack.Stack.ShouldBeEmpty();
            f.Changes.Last().Reason.ShouldBe(SheetStackChangeReason.CloseAll);
            f.Stack.Push("account");
            f.Pump();
            f.Click(f.Sheet("account").CloseButton!);
            f.Stack.Stack.ShouldBeEmpty();
            f.Changes.Last().Reason.ShouldBe(SheetStackChangeReason.Close);
        }
        finally
        {
            f.Window.Close();
        }
    }

    [AvaloniaFact]
    public void Back_button_pops_the_top_only()
    {
        var f = Open();
        try
        {
            f.Stack.Push("account");
            f.Stack.Push("profile");
            f.Pump();
            f.Click(f.Sheet("profile").BackButton!);
            f.Stack.Stack.ShouldBe(["account"]);
            f.Changes.Last().Reason.ShouldBe(SheetStackChangeReason.Back);
        }
        finally
        {
            f.Window.Close();
        }
    }

    [AvaloniaFact]
    public void Escape_pops_the_top_even_with_focus_outside()
    {
        var f = Open();
        try
        {
            f.Stack.Push("account");
            f.Stack.Push("profile");
            f.OpenButton.Focus();
            f.Key(PhysicalKey.Escape);
            f.Stack.Stack.ShouldBe(["account"]);
            f.Changes.Last().Reason.ShouldBe(SheetStackChangeReason.Escape);
            f.Key(PhysicalKey.Escape);
            f.Stack.Stack.ShouldBeEmpty();
        }
        finally
        {
            f.Window.Close();
        }
    }

    [AvaloniaFact]
    public void Scrim_press_pops_the_top()
    {
        var f = Open();
        try
        {
            f.Stack.Push("account");
            f.Stack.Push("profile");
            f.Pump();
            var corner = f.Stack.TranslatePoint(new Point(10, 10), f.Window)!.Value;
            f.Window.MouseDown(corner, MouseButton.Left);
            f.Window.MouseUp(corner, MouseButton.Left);
            f.Pump();
            f.Stack.Stack.ShouldBe(["account"]);
            f.Changes.Last().Reason.ShouldBe(SheetStackChangeReason.ScrimPress);
        }
        finally
        {
            f.Window.Close();
        }
    }

    [AvaloniaFact]
    public void Levels_below_are_out_of_reach_but_not_disabled()
    {
        var f = Open();
        try
        {
            f.Stack.Push("account");
            f.Stack.Push("profile");
            f.Pump();
            var account = f.Sheet("account");
            var profile = f.Sheet("profile");
            account.IsHitTestVisible.ShouldBeFalse();
            account.IsEnabled.ShouldBeTrue();
            account.Opacity.ShouldBe(1);
            KeyboardNavigation.GetTabNavigation(account).ShouldBe(KeyboardNavigationMode.None);
            KeyboardNavigation.GetTabNavigation(profile).ShouldBe(KeyboardNavigationMode.Cycle);
            account.Classes.Contains(":lower").ShouldBeTrue();
            profile.Classes.Contains(":top").ShouldBeTrue();
            ControlAutomationPeer.CreatePeerForElement(account).GetChildren().ShouldBeEmpty();
            ControlAutomationPeer.CreatePeerForElement(profile).GetChildren().ShouldNotBeEmpty();
            ControlAutomationPeer.CreatePeerForElement(profile).GetName().ShouldBe("Your profile");
            // A press on the strip of the level below lands on the scrim (and pops the top).
            var hit = f.Window.InputHitTest(f.Center(f.ProfileRow));
            hit.ShouldNotBe(f.ProfileRow);
        }
        finally
        {
            f.Window.Close();
        }
    }

    [AvaloniaFact]
    public void Focus_moves_into_a_new_sheet_and_back_to_its_opener()
    {
        var f = Open();
        try
        {
            f.OpenButton.Focus(NavigationMethod.Tab);
            f.Key(PhysicalKey.Enter);
            f.Stack.Stack.ShouldBe(["account"]);
            f.Focused.ShouldBe(f.Sheet("account"));
            f.ProfileRow.Focus(NavigationMethod.Tab);
            f.Key(PhysicalKey.Enter);
            f.Focused.ShouldBe(f.Sheet("profile"));
            f.Key(PhysicalKey.Escape);
            f.Focused.ShouldBe(f.ProfileRow);
            f.ProfileRow.Classes.Contains(":focus-visible").ShouldBeTrue();
            f.Stack.Push("profile");
            f.Stack.Close();
            f.Focused.ShouldBe(f.OpenButton);
        }
        finally
        {
            f.Window.Close();
        }
    }

    [AvaloniaFact]
    public void Tab_cycles_inside_the_top_sheet_and_skips_a_disabled_footer()
    {
        var f = Open();
        try
        {
            f.Stack.Push("account");
            f.Stack.Push("profile");
            f.Pump();
            var profile = f.Sheet("profile");
            var seen = new List<IInputElement?>();
            for (var i = 0; i < 12; i++)
            {
                f.Key(PhysicalKey.Tab);
                seen.Add(f.Focused);
            }
            seen.All(e => e is Visual v && profile.IsVisualAncestorOf(v)).ShouldBeTrue(string.Join(", ", seen.Select(e => e?.GetType().Name + ":" + (e as Control)?.Name)));
            seen.ShouldNotContain(f.SaveButton);
            seen.ShouldContain(f.PickButton);
            seen.ShouldContain(f.NameBox);
            seen.ShouldContain(profile.CloseButton);
            seen.ShouldContain(profile.BackButton);
            seen.ShouldNotContain(f.ProfileRow);
        }
        finally
        {
            f.Window.Close();
        }
    }

    [AvaloniaFact]
    public void Auto_mode_follows_the_container_width()
    {
        var wide = Open(width: 742);
        try
        {
            wide.Stack.ActualMode.ShouldBe(SheetStackMode.Dialog);
            wide.Stack.Push("account");
            wide.Pump();
            var sheet = wide.Sheet("account");
            sheet.Classes.Contains(":dialog-mode").ShouldBeTrue();
            sheet.Bounds.Width.ShouldBe(440);
            sheet.Bounds.Center.Y.ShouldBe(578 / 2.0, 0.6);
            wide.Stack.Mode = SheetStackMode.Sheet;
            wide.Pump();
            wide.Stack.ActualMode.ShouldBe(SheetStackMode.Sheet);
            sheet.Bounds.Width.ShouldBe(560);
            sheet.Bounds.Bottom.ShouldBe(578, 0.6);
        }
        finally
        {
            wide.Window.Close();
        }
        var narrow = Open(width: 342, height: 600);
        try
        {
            narrow.Stack.ActualMode.ShouldBe(SheetStackMode.Sheet);
            narrow.Stack.Push("account");
            narrow.Pump();
            narrow.Sheet("account").Bounds.Width.ShouldBe(342);
            narrow.Sheet("account").SurfaceCornerRadius.BottomLeft.ShouldBe(0);
        }
        finally
        {
            narrow.Window.Close();
        }
    }

    [AvaloniaFact]
    public void Tall_content_scrolls_under_a_pinned_footer()
    {
        var f = Open(width: 592, height: 300);
        try
        {
            f.Stack.Push("account");
            f.Stack.Push("profile");
            f.Pump();
            var profile = f.Sheet("profile");
            // 300 - 20 - 10 (one level below).
            profile.Bounds.Height.ShouldBe(270, 0.6);
            var body = profile.Body!;
            body.Bounds.Height.ShouldBeLessThan(body.Extent.Height, $"body {body.Bounds.Height} extent {body.Extent.Height} viewport {body.Viewport.Height}");
            f.SaveButton.IsEffectivelyVisible.ShouldBeTrue();
        }
        finally
        {
            f.Window.Close();
        }
    }

    [AvaloniaFact]
    public void Dragging_down_far_enough_dismisses_the_top()
    {
        var f = Open(width: 592);
        try
        {
            f.Stack.Push("account");
            f.Stack.Push("profile");
            f.Pump();
            var profile = f.Sheet("profile");
            var header = profile.TranslatePoint(new Point(profile.Bounds.Width / 2, 30), f.Window)!.Value;
            f.Drag(header, 40, pauseMs: 40);
            f.Stack.Stack.ShouldBe(["account", "profile"]);
            f.Drag(header, 400);
            f.Stack.Stack.ShouldBe(["account"]);
            f.Changes.Last().Reason.ShouldBe(SheetStackChangeReason.Drag);
        }
        finally
        {
            f.Window.Close();
        }
    }

    [AvaloniaFact]
    public void Drag_excluded_content_and_non_dismissible_sheets_stay()
    {
        var f = Open(width: 592);
        try
        {
            f.Stack.Push("account");
            f.Stack.Push("profile");
            f.Stack.Push("avatar");
            f.Pump();
            f.Drag(f.Center(f.Excluded), 400);
            f.Stack.Stack.Count.ShouldBe(3);
            var avatar = f.Sheet("avatar");
            avatar.IsDismissible = false;
            var header = avatar.TranslatePoint(new Point(avatar.Bounds.Width / 2, 30), f.Window)!.Value;
            f.Drag(header, 400);
            f.Stack.Stack.Count.ShouldBe(3);
        }
        finally
        {
            f.Window.Close();
        }
    }

    [AvaloniaFact]
    public void Not_contained_covers_the_window_overlay()
    {
        var f = Open(contained: false);
        try
        {
            f.Stack.Push("account");
            f.Pump();
            var overlay = (Panel)f.Stack.Layer.Parent!;
            overlay.ShouldNotBeNull();
            overlay.IsVisualAncestorOf(f.Stack).ShouldBeFalse();
            overlay.Children.ShouldContain(f.Stack.Layer);
            f.Stack.Close();
            f.Pump();
            overlay.Children.ShouldNotContain(f.Stack.Layer);
        }
        finally
        {
            f.Window.Close();
        }
    }

    // Motion (ManualFrameClock installed before the window is shown).

    private static void Advance(ManualFrameClock clock, double seconds)
    {
        clock.Advance(TimeSpan.FromSeconds(seconds), Frame);
        Dispatcher.UIThread.RunJobs();
    }

    private static double VisibleTop(Fixture f, string id)
    {
        var entry = f.Entry(id);
        return f.Sheet(id).Bounds.Top + entry.Transform.Matrix.M32;
    }

    [AvaloniaFact]
    public void Levels_below_settle_one_peek_above_the_top()
    {
        var clock = new ManualFrameClock();
        using var _ = clock.Install();
        var f = Open(ArcMotionLevel.Full, width: 592);
        try
        {
            f.Stack.Push("account");
            Advance(clock, 1.2);
            f.Stack.Push("profile");
            Advance(clock, 0.1);
            // Mid-rise: the new level is still on its way up and the one below has started to shrink.
            f.Entry("profile").Y.Value.ShouldBeGreaterThan(5);
            f.Entry("account").Transform.Matrix.M11.ShouldBeInRange(0.951, 0.999);
            Advance(clock, 1.5);
            f.Entry("account").Transform.Matrix.M11.ShouldBe(0.95, 0.001);
            f.Sheet("account").DimOpacity.ShouldBe(0.5, 0.001);
            VisibleTop(f, "account").ShouldBe(VisibleTop(f, "profile") - 10, 0.5);
        }
        finally
        {
            f.Window.Close();
        }
    }

    [AvaloniaFact]
    public void The_level_below_peeks_again_after_pop_and_push()
    {
        var clock = new ManualFrameClock();
        using var _ = clock.Install();
        var f = Open(ArcMotionLevel.Full, width: 592);
        try
        {
            f.Stack.Push("account");
            Advance(clock, 1.2);
            for (var round = 0; round < 3; round++)
            {
                f.Stack.Push("profile");
                Advance(clock, 1.5);
                VisibleTop(f, "account").ShouldBe(VisibleTop(f, "profile") - 10, 0.5);
                f.Stack.Pop();
                Advance(clock, 0.05 + round * 0.2);
            }
        }
        finally
        {
            f.Window.Close();
        }
    }

    [AvaloniaFact]
    public void An_interrupted_push_turns_around_smoothly()
    {
        var clock = new ManualFrameClock();
        using var _ = clock.Install();
        var f = Open(ArcMotionLevel.Full, width: 592);
        try
        {
            f.Stack.Push("account");
            Advance(clock, 1.2);
            f.Stack.Push("profile");
            Advance(clock, 0.18);
            var risingY = f.Entry("profile").Y.Value;
            f.Key(PhysicalKey.Escape);
            var previous = f.Entry("account").Transform.Matrix.M11;
            var maxStep = 0.0;
            for (var i = 0; i < 60; i++)
            {
                Advance(clock, 1.0 / 60);
                var scale = f.Entry("account").Transform.Matrix.M11;
                maxStep = Math.Max(maxStep, Math.Abs(scale - previous));
                previous = scale;
            }
            maxStep.ShouldBeLessThan(0.006);
            previous.ShouldBe(1, 0.001);
            risingY.ShouldBeGreaterThan(0);
            f.Stack.Layer.Entries.Count.ShouldBe(1);
        }
        finally
        {
            f.Window.Close();
        }
    }

    [AvaloniaFact]
    public void Close_all_drops_the_levels_as_one_group()
    {
        var clock = new ManualFrameClock();
        using var _ = clock.Install();
        var f = Open(ArcMotionLevel.Full, width: 592);
        try
        {
            f.Stack.Push("account");
            f.Stack.Push("profile");
            Advance(clock, 1.5);
            var scaleBefore = f.Entry("account").Transform.Matrix.M11;
            f.Stack.Close();
            Advance(clock, 0.1);
            // Each level keeps its own scale while it falls; the scrim fades with them.
            f.Entry("account").Transform.Matrix.M11.ShouldBe(scaleBefore, 0.001);
            f.Entry("account").Y.Value.ShouldBeGreaterThan(50);
            f.Entry("profile").Y.Value.ShouldBeGreaterThan(50);
            f.Stack.Layer.ScrimOpacity.ShouldBeLessThan(1);
            Advance(clock, 1.5);
            f.Stack.Layer.Entries.ShouldBeEmpty();
            f.Stack.Layer.IsVisible.ShouldBeFalse();
        }
        finally
        {
            f.Window.Close();
        }
    }

    [AvaloniaFact]
    public void Reduced_motion_only_dims_the_levels_below()
    {
        var clock = new ManualFrameClock();
        using var _ = clock.Install();
        var f = Open(ArcMotionLevel.Reduced, width: 592);
        try
        {
            f.Stack.Push("account");
            f.Stack.Push("profile");
            Advance(clock, 0.05);
            f.Sheet("profile").Opacity.ShouldBeInRange(0.01, 0.99);
            f.Entry("profile").Y.Value.ShouldBe(0);
            Advance(clock, 0.5);
            var matrix = f.Entry("account").Transform.Matrix;
            matrix.M11.ShouldBe(1);
            matrix.M32.ShouldBe(0);
            f.Sheet("account").DimOpacity.ShouldBe(0.5);
            f.Sheet("profile").Opacity.ShouldBe(1);
        }
        finally
        {
            f.Window.Close();
        }
    }

    [AvaloniaFact]
    public void Dragging_moves_the_level_below_with_the_offset()
    {
        var clock = new ManualFrameClock();
        using var _ = clock.Install();
        var f = Open(ArcMotionLevel.Full, width: 592);
        try
        {
            f.Stack.Push("account");
            f.Stack.Push("profile");
            Advance(clock, 1.5);
            var profile = f.Sheet("profile");
            var start = profile.TranslatePoint(new Point(profile.Bounds.Width / 2, 30), f.Window)!.Value;
            f.Pump();
            f.Window.MouseMove(start, RawInputModifiers.None);
            f.Window.MouseDown(start, MouseButton.Left);
            f.Window.MouseMove(start + new Vector(0, 30), RawInputModifiers.LeftMouseButton);
            f.Window.MouseMove(start + new Vector(0, 60), RawInputModifiers.LeftMouseButton);
            Dispatcher.UIThread.RunJobs();
            var y = f.Entry("profile").Y.Value;
            y.ShouldBe(54, 0.5);
            var p = y / f.Entry("profile").Height.Value;
            f.Sheet("account").DimOpacity.ShouldBe(0.5 * (1 - p), 0.01);
            // A pointer that rests before release has no fling: a 54 px drag springs back.
            Thread.Sleep(150);
            f.Window.MouseUp(start + new Vector(0, 60), MouseButton.Left);
            Advance(clock, 1.5);
            f.Stack.Stack.Count.ShouldBe(2);
            f.Entry("profile").Y.Value.ShouldBe(0, 0.2);
            f.Sheet("account").DimOpacity.ShouldBe(0.5, 0.01);
        }
        finally
        {
            f.Window.Close();
        }
    }
}
