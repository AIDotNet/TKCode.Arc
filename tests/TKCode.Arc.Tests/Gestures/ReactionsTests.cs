using TKCode.Arc.Motion;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.VisualTree;

namespace TKCode.Arc.Tests.Gestures;

public class ReactionsTests
{
    private static Reactions Create(params Reaction[] value)
    {
        var reactions = new Reactions { Value = value };
        reactions.Options.Add(new ReactionOption("like", "Like", ArcIcons.ThumbsUp));
        reactions.Options.Add(new ReactionOption("love", "Love", ArcIcons.Heart));
        reactions.Options.Add(new ReactionOption("star", "Star", ArcIcons.Star));
        return reactions;
    }

    private static Window Show(Reactions reactions, ArcMotionLevel level = ArcMotionLevel.Off)
    {
        var window = TestHelpers.Show(new StackPanel { Margin = new Thickness(40, 160, 40, 0), Children = { reactions } }, level);
        window.Width = 600;
        window.Height = 400;
        Dispatcher.UIThread.RunJobs();
        return window;
    }

    [AvaloniaFact]
    public void Arc_Demo_Shows_One_Pill_And_The_Add_Button()
    {
        var reactions = Create(new Reaction("like", 3));
        var window = Show(reactions);
        try
        {
            reactions.Pills.Count.ShouldBe(1);
            var pill = reactions.PillFor("like")!;
            pill.Bounds.Height.ShouldBe(32);
            pill.IsChecked.ShouldBe(false);
            pill.Padding.ShouldBe(new Thickness(9, 0, 11, 0));
            AutomationProperties.GetName(pill).ShouldBe("Like, 3 reactions");
            Tooltip.GetTip(pill).ShouldBe("Like");
            Tooltip.GetShowDelay(pill).ShouldBe(TimeSpan.FromMilliseconds(450));
            TestHelpers.Descendant<Primitives.RollingDigits>(pill).DisplayText.ShouldBe("3");
            var add = reactions.AddButton!;
            add.Bounds.Width.ShouldBe(38);
            add.Bounds.Height.ShouldBe(32);
            AutomationProperties.GetName(add).ShouldBe("Add reaction");
            Tooltip.GetTip(add).ShouldBe("Add reaction");
            // Pills and the add button sit in one row, 6px apart.
            add.Bounds.X.ShouldBe(pill.Bounds.Right + 6, 0.5);
            AutomationProperties.GetName(reactions).ShouldBe("Reactions");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Tapping_A_Pill_Toggles_The_Viewers_Reaction()
    {
        var reactions = Create(new Reaction("like", 3));
        var changes = new List<ReactionsValueChangedEventArgs>();
        reactions.ValueChanged += (_, e) => changes.Add(e);
        var window = Show(reactions);
        try
        {
            var pill = reactions.PillFor("like")!;
            pill.Focus(NavigationMethod.Tab);
            window.KeyPressQwerty(PhysicalKey.Enter, RawInputModifiers.None);
            Dispatcher.UIThread.RunJobs();
            reactions.Value!.ShouldHaveSingleItem().ShouldBe(new Reaction("like", 4, true));
            pill.IsChecked.ShouldBe(true);
            AutomationProperties.GetName(pill).ShouldBe("Like, 4 reactions, including yours");
            reactions.LastAnnouncement.ShouldBe("Added Like. 4 reactions.");
            changes.ShouldHaveSingleItem().Change.ShouldBe(new ReactionChange("like", true));

            var point = pill.TranslatePoint(new Point(10, 10), window)!.Value;
            window.MouseDown(point, MouseButton.Left);
            window.MouseUp(point, MouseButton.Left);
            Dispatcher.UIThread.RunJobs();
            reactions.Value!.ShouldHaveSingleItem().ShouldBe(new Reaction("like", 3, false));
            reactions.LastAnnouncement.ShouldBe("Removed Like. 3 reactions.");
            changes[1].Change.ShouldBe(new ReactionChange("like", false));
            TestHelpers.Descendant<Primitives.RollingDigits>(pill).DisplayText.ShouldBe("3");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void A_Reaction_That_Reaches_Zero_Leaves_And_Hands_Focus_On()
    {
        var reactions = Create(new Reaction("like", 2), new Reaction("love", 1, true), new Reaction("star", 5));
        var window = Show(reactions);
        try
        {
            var love = reactions.PillFor("love")!;
            love.Focus(NavigationMethod.Tab);
            window.KeyPressQwerty(PhysicalKey.Space, RawInputModifiers.None);
            window.KeyReleaseQwerty(PhysicalKey.Space, RawInputModifiers.None);
            Dispatcher.UIThread.RunJobs();
            reactions.Value!.Select(r => r.Id).ShouldBe(new[] { "like", "star" });
            reactions.PillFor("love").ShouldBeNull();
            reactions.Bar!.Children.ShouldNotContain(love);
            reactions.LastAnnouncement.ShouldBe("Removed Love.");
            reactions.PillFor("star")!.IsFocused.ShouldBeTrue();

            // The last pill hands focus to the previous one, then to the add button.
            reactions.Toggle("star");
            reactions.Toggle("star");
            reactions.Value = new[] { new Reaction("like", 1, true) };
            Dispatcher.UIThread.RunJobs();
            var like = reactions.PillFor("like")!;
            like.Focus(NavigationMethod.Tab);
            window.KeyPressQwerty(PhysicalKey.Enter, RawInputModifiers.None);
            Dispatcher.UIThread.RunJobs();
            reactions.Pills.ShouldBeEmpty();
            reactions.AddButton!.IsFocused.ShouldBeTrue();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Picker_Opens_From_The_Add_Button_With_Roving_Focus()
    {
        var reactions = Create(new Reaction("like", 3));
        var window = Show(reactions);
        try
        {
            var add = reactions.AddButton!;
            add.Focus(NavigationMethod.Tab);
            window.KeyPressQwerty(PhysicalKey.Enter, RawInputModifiers.None);
            Dispatcher.UIThread.RunJobs();
            reactions.IsPickerOpen.ShouldBeTrue();
            add.IsOpen.ShouldBeTrue();
            add.Classes.Contains(ReactionsAddButton.OpenPseudoClass).ShouldBeTrue();
            add.Glyph.ShouldBe(ArcIcons.X);
            AutomationProperties.GetItemStatus(add).ShouldBe("Expanded");
            Tooltip.GetTip(add).ShouldBeNull();
            var popover = reactions.PickerPopover!;
            popover.Placement.ShouldBe(PopoverPlacement.Top);
            popover.Alignment.ShouldBe(PopoverAlignment.Center);
            popover.Popup!.MarginToAnchor.ShouldBe(8);
            popover.Surface!.Padding.ShouldBe(new Thickness(5));
            // The tray sits 8px above the add button, centered on it.
            var surface = popover.Surface;
            var surfaceBottom = surface.TranslatePoint(new Point(0, surface.Bounds.Height), window)!.Value.Y;
            (add.TranslatePoint(default, window)!.Value.Y - surfaceBottom).ShouldBe(8, 1);
            surface.TranslatePoint(new Point(surface.Bounds.Width / 2, 0), window)!.Value.X
                .ShouldBe(add.TranslatePoint(new Point(add.Bounds.Width / 2, 0), window)!.Value.X, 1);
            var picker = reactions.Picker!;
            var options = picker.Options;
            options.Count.ShouldBe(3);
            options[0].IsFocused.ShouldBeTrue();
            options[0].Bounds.Width.ShouldBe(40);
            options.Count(o => o.IsTabStop).ShouldBe(1);

            window.KeyPressQwerty(PhysicalKey.ArrowLeft, RawInputModifiers.None);
            Dispatcher.UIThread.RunJobs();
            options[2].IsFocused.ShouldBeTrue();
            picker.ActiveIndex.ShouldBe(2);
            options[2].Classes.Contains(ReactionsPickerOption.LitPseudoClass).ShouldBeTrue();
            window.KeyPressQwerty(PhysicalKey.ArrowDown, RawInputModifiers.None);
            Dispatcher.UIThread.RunJobs();
            options[0].IsFocused.ShouldBeTrue();
            window.KeyPressQwerty(PhysicalKey.End, RawInputModifiers.None);
            window.KeyPressQwerty(PhysicalKey.Home, RawInputModifiers.None);
            window.KeyPressQwerty(PhysicalKey.ArrowRight, RawInputModifiers.None);
            Dispatcher.UIThread.RunJobs();
            options[1].IsFocused.ShouldBeTrue();
            options[1].IsTabStop.ShouldBeTrue();
            options[0].IsTabStop.ShouldBeFalse();

            window.KeyPressQwerty(PhysicalKey.Enter, RawInputModifiers.None);
            Dispatcher.UIThread.RunJobs();
            reactions.IsPickerOpen.ShouldBeFalse();
            add.IsFocused.ShouldBeTrue();
            reactions.Value!.Select(r => (r.Id, r.Count, r.Reacted)).ShouldBe(new[] { ("like", 3, false), ("love", 1, true) });
            reactions.PillFor("love")!.IsChecked.ShouldBe(true);
            reactions.LastAnnouncement.ShouldBe("Added Love. 1 reaction.");

            // Reopened, the chosen option is pressed and the tray starts at the first option again.
            add.Focus(NavigationMethod.Tab);
            window.KeyPressQwerty(PhysicalKey.Space, RawInputModifiers.None);
            window.KeyReleaseQwerty(PhysicalKey.Space, RawInputModifiers.None);
            Dispatcher.UIThread.RunJobs();
            reactions.IsPickerOpen.ShouldBeTrue();
            options[1].IsChecked.ShouldBe(true);
            options[0].IsFocused.ShouldBeTrue();
            window.KeyPressQwerty(PhysicalKey.Escape, RawInputModifiers.None);
            Dispatcher.UIThread.RunJobs();
            reactions.IsPickerOpen.ShouldBeFalse();
            add.IsFocused.ShouldBeTrue();
            add.Glyph.ShouldBe(ArcIcons.SmilePlus);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Tooltip_Names_Who_Reacted()
    {
        var love = new ReactionOption("love", "Love", ArcIcons.Heart);
        Reactions.DefaultTip(love, new Reaction("love", 2)).ShouldBe("Love");
        Reactions.DefaultTip(love, new Reaction("love", 4, true) { ReactedBy = ["Ada", "Lin"] }).ShouldBe("Love · You, Ada, Lin and 1 other");
        Reactions.DefaultTip(love, new Reaction("love", 6) { ReactedBy = ["Ada", "Lin", "Sam", "Tom"] }).ShouldBe("Love · Ada, Lin, Sam and 3 others");
        Reactions.DefaultTip(love, new Reaction("love", 2) { ReactedBy = ["Ada", "Lin"] }).ShouldBe("Love · Ada and Lin");
        Reactions.DefaultTip(love, new Reaction("love", 1, true) { ReactedBy = [] }).ShouldBe("Love");

        var reactions = Create(new Reaction("love", 2) { ReactedBy = ["Ada", "Lin"] });
        reactions.TipSelector = (option, reaction) => $"{reaction.Count} × {option.Label}";
        var window = Show(reactions);
        try
        {
            Tooltip.GetTip(reactions.PillFor("love")!).ShouldBe("2 × Love");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Tap_Impulse_Stacks_On_Quick_Taps()
    {
        var reactions = Create(new Reaction("like", 3));
        var window = Show(reactions, ArcMotionLevel.Full);
        var clock = new ManualFrameClock();
        using var _ = clock.Install();
        try
        {
            var glyph = reactions.PillFor("like")!.Glyph!;
            reactions.Toggle("like");
            glyph.KickScaleVelocity.ShouldBe(11, 0.001);
            clock.Advance(TimeSpan.FromMilliseconds(50));
            Dispatcher.UIThread.RunJobs();
            glyph.KickScale.ShouldBeGreaterThan(1.05);
            glyph.KickRotation.ShouldBeLessThan(-1);
            var velocity = glyph.KickScaleVelocity;
            reactions.Toggle("like");
            reactions.Toggle("like");
            // Removing never kicks; adding again adds to the running motion instead of restarting it.
            glyph.KickScaleVelocity.ShouldBe(velocity + 11, 0.001);
            clock.Advance(TimeSpan.FromSeconds(1.5));
            Dispatcher.UIThread.RunJobs();
            glyph.KickScale.ShouldBe(1, 0.001);
            glyph.KickRotation.ShouldBe(0, 0.01);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Pills_Enter_And_Leave_With_Presence_While_The_Rest_Glide()
    {
        var reactions = Create(new Reaction("like", 3), new Reaction("love", 1, true));
        var window = Show(reactions, ArcMotionLevel.Full);
        var clock = new ManualFrameClock();
        using var _ = clock.Install();
        try
        {
            var add = reactions.AddButton!;
            var restX = add.Bounds.X;
            reactions.Toggle("star");
            Dispatcher.UIThread.RunJobs();
            var star = reactions.PillFor("star")!;
            star.PresenceScale.ShouldBe(0.5, 0.01);
            star.Opacity.ShouldBeLessThan(0.05);
            // The add button moved right; it glides there from where it was.
            add.Bounds.X.ShouldBeGreaterThan(restX);
            (add.Bounds.X + reactions.Bar!.OffsetOf(add).X).ShouldBe(restX, 0.5);
            clock.Advance(TimeSpan.FromSeconds(1));
            Dispatcher.UIThread.RunJobs();
            star.PresenceScale.ShouldBe(1, 0.001);
            star.Opacity.ShouldBe(1, 0.001);
            reactions.Bar.OffsetOf(add).X.ShouldBe(0, 0.01);

            var love = reactions.PillFor("love")!;
            reactions.Toggle("love");
            Dispatcher.UIThread.RunJobs();
            love.IsLeaving.ShouldBeTrue();
            love.Focusable.ShouldBeFalse();
            reactions.Bar.Children.ShouldContain(love);
            // Popped out of the flow: the star pill takes its place at once and glides there.
            star.Bounds.X.ShouldBe(love.Bounds.X, 0.5);
            clock.Advance(TimeSpan.FromMilliseconds(80));
            Dispatcher.UIThread.RunJobs();
            love.PresenceScale.ShouldBeLessThan(1);
            love.Opacity.ShouldBeLessThan(1);
            clock.Advance(TimeSpan.FromSeconds(1));
            Dispatcher.UIThread.RunJobs();
            reactions.Bar.Children.ShouldNotContain(love);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Wrapping_Grows_The_Bar_On_A_Spring_After_A_Change()
    {
        var reactions = Create(new Reaction("like", 3), new Reaction("love", 1));
        reactions.HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Left;
        var window = Show(reactions, ArcMotionLevel.Full);
        var clock = new ManualFrameClock();
        using var _ = clock.Install();
        try
        {
            var bar = reactions.Bar!;
            // Just wide enough for two pills and the add button: a third pill wraps.
            reactions.Width = Math.Ceiling(reactions.AddButton!.Bounds.Right) + 1;
            Dispatcher.UIThread.RunJobs();
            bar.Bounds.Height.ShouldBe(32, 0.5);
            reactions.Toggle("star");
            Dispatcher.UIThread.RunJobs();
            clock.Advance(TimeSpan.FromMilliseconds(50));
            Dispatcher.UIThread.RunJobs();
            bar.Bounds.Height.ShouldBeGreaterThan(32);
            bar.Bounds.Height.ShouldBeLessThan(72);
            clock.Advance(TimeSpan.FromSeconds(1.5));
            Dispatcher.UIThread.RunJobs();
            bar.Bounds.Height.ShouldBe(72, 0.5);
            // Rows are space-2 apart.
            reactions.AddButton!.Bounds.Y.ShouldBe(40, 0.5);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void External_Value_Updates_Roll_Counts_Without_A_Kick()
    {
        var reactions = Create(new Reaction("like", 9));
        var window = Show(reactions, ArcMotionLevel.Full);
        var clock = new ManualFrameClock();
        using var _ = clock.Install();
        try
        {
            var pill = reactions.PillFor("like")!;
            reactions.Value = new[] { new Reaction("like", 10), new Reaction("unknown", 4), new Reaction("star", 0) };
            Dispatcher.UIThread.RunJobs();
            reactions.Pills.Count.ShouldBe(1);
            TestHelpers.Descendant<Primitives.RollingDigits>(pill).DisplayText.ShouldBe("10");
            pill.Glyph!.KickScaleVelocity.ShouldBe(0);
            AutomationProperties.GetName(pill).ShouldBe("Like, 10 reactions");
        }
        finally
        {
            window.Close();
        }
    }
}
