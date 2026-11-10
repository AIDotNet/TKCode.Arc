using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Threading;
using TKCode.Arc.Motion;

namespace TKCode.Arc.Tests.Special;

/// <summary>Magnify dock: the lens and the row's re-flow, the launch bounce, and the control's input, events and motion levels.</summary>
public class MagnifyDockTests
{
    private static readonly TimeSpan Frame = TimeSpan.FromMilliseconds(1000.0 / 60);

    private static MagnifyDockModel Model(int count = 7)
    {
        var model = new MagnifyDockModel { ItemSize = 48, Gap = 8, Magnification = 2, Reach = 150 };
        model.Resize(count);
        return model;
    }

    private static void Run(MagnifyDockModel model, double seconds)
    {
        for (var t = 0.0; t < seconds; t += 1.0 / 60)
        {
            model.Step(1.0 / 60);
        }
    }

    // --- model -----------------------------------------------------------------------------------------------------

    [Fact]
    public void The_lens_grows_the_icon_under_it_most_and_fades_out_with_distance()
    {
        var model = Model();
        model.Lens = model.RestCenter(3);
        model.TargetScale(3).ShouldBe(2, 1e-9);
        model.TargetScale(2).ShouldBeLessThan(2);
        model.TargetScale(2).ShouldBe(model.TargetScale(4), 1e-9);
        model.TargetScale(1).ShouldBeLessThan(model.TargetScale(2));
        model.TargetScale(0).ShouldBe(1, 0.01);
        model.Lens = double.NaN;
        model.TargetScale(3).ShouldBe(1);
    }

    [Fact]
    public void Icons_spring_to_their_size_and_the_row_stays_centred()
    {
        var model = Model();
        model.Lens = model.RestCenter(3);
        Run(model, 1);
        model.Scale(3).ShouldBe(2, 0.01);
        var left = model.Layout();
        (left + model.Width / 2).ShouldBe(model.RestWidth / 2, 1e-9);
        // The grown icons push their neighbours aside without overlapping.
        for (var i = 1; i < model.Count; i++)
        {
            var gap = model.Center(i) - model.ItemSize * model.Scale(i) / 2 - (model.Center(i - 1) + model.ItemSize * model.Scale(i - 1) / 2);
            gap.ShouldBe(8, 1e-9);
        }
        model.Center(3).ShouldBe(model.RestCenter(3), 1e-9);
    }

    [Fact]
    public void The_reserved_width_covers_every_lens_position()
    {
        var model = Model();
        var reserved = model.MaxWidth();
        for (var lens = 0.0; lens <= model.RestWidth; lens += 3)
        {
            model.Lens = lens;
            model.Settle();
            model.Width.ShouldBeLessThanOrEqualTo(reserved + 0.5);
        }
    }

    [Fact]
    public void A_launch_hops_three_times_and_lands()
    {
        var model = Model();
        model.Launch(2);
        model.IsBouncing(2).ShouldBeTrue();
        var peaks = new List<double>();
        var last = 0.0;
        var rising = true;
        for (var t = 0.0; t < MagnifyDockModel.HopSeconds * 3 + 0.1; t += 1.0 / 240)
        {
            model.Step(1.0 / 240);
            var lift = model.Lift(2);
            if (rising && lift < last)
            {
                peaks.Add(last);
                rising = false;
            }
            else if (!rising && lift > last)
            {
                rising = true;
            }
            last = lift;
        }
        peaks.Count.ShouldBe(3);
        peaks[0].ShouldBeGreaterThan(peaks[1]);
        peaks[1].ShouldBeGreaterThan(peaks[2]);
        model.IsBouncing(2).ShouldBeFalse();
        model.Lift(2).ShouldBe(0);
    }

    [Fact]
    public void Without_springs_sizes_jump()
    {
        var model = Model();
        model.Springs = false;
        model.Lens = model.RestCenter(0);
        model.Step(1.0 / 60).ShouldBeFalse();
        model.Scale(0).ShouldBe(2);
    }

    // --- control ---------------------------------------------------------------------------------------------------

    private static MagnifyDock Dock() => new()
    {
        Items =
        {
            new MagnifyDockItem { Label = "Notes", Content = new TextBlock { Text = "N" } },
            new MagnifyDockItem { Label = "Mail", Content = new TextBlock { Text = "M" } },
            new MagnifyDockItem { Label = "Photos", Content = new TextBlock { Text = "P" }, IsRunning = true },
            new MagnifyDockItem { Label = "Music", Content = new TextBlock { Text = "U" } },
            new MagnifyDockItem { Label = "Maps", Content = new TextBlock { Text = "A" } }
        }
    };

    private static MagnifyDockItem Item(MagnifyDock dock, int index) => (MagnifyDockItem)dock.ContainerFromIndex(index)!;

    [AvaloniaFact]
    public void Reserves_room_for_the_widest_row_and_its_labels()
    {
        var dock = Dock();
        var window = TestHelpers.Show(dock, ArcMotionLevel.Full);
        dock.Bounds.Width.ShouldBeGreaterThan(5 * 48 + 4 * 8 + 16);
        dock.Bounds.Height.ShouldBeGreaterThanOrEqualTo(48 * 1.8 + 16 + 40 - 0.5);
        Item(dock, 0).Bounds.Width.ShouldBe(48);
        window.Close();
    }

    [AvaloniaFact]
    public void The_pointer_magnifies_the_icons_near_it_and_shows_a_label()
    {
        var frames = new ManualFrameClock();
        using var install = frames.Install();
        var dock = Dock();
        var window = TestHelpers.Show(dock, ArcMotionLevel.Full);
        var icon = Item(dock, 2);
        var point = icon.TranslatePoint(new Point(24, 24), window)!.Value;
        window.MouseMove(point, RawInputModifiers.None);
        frames.Advance(TimeSpan.FromSeconds(1), Frame);
        icon.CurrentPose.M11.ShouldBe(1.8, 0.02);
        Item(dock, 0).CurrentPose.M11.ShouldBeLessThan(icon.CurrentPose.M11);
        dock.LabelIndex.ShouldBe(2);
        dock.LabelOpacity.ShouldBe(1);
        window.MouseMove(new Point(0, 0), RawInputModifiers.None);
        frames.Advance(TimeSpan.FromSeconds(1), Frame);
        icon.CurrentPose.M11.ShouldBe(1, 0.01);
        window.Close();
    }

    [AvaloniaFact]
    public void A_click_launches_marks_running_and_reports_the_icon()
    {
        var dock = Dock();
        var window = TestHelpers.Show(dock);
        MagnifyDockItemEventArgs? launched = null;
        dock.ItemLaunched += (_, e) => launched = e;
        var icon = Item(dock, 1);
        var point = icon.TranslatePoint(new Point(24, 24), window)!.Value;
        window.MouseDown(point, MouseButton.Left);
        window.MouseUp(point, MouseButton.Left);
        launched.ShouldNotBeNull();
        launched.Index.ShouldBe(1);
        launched.Container.ShouldBe(icon);
        icon.IsRunning.ShouldBeTrue();
        window.Close();
    }

    [AvaloniaFact]
    public void A_launch_bounces_under_full_motion()
    {
        var frames = new ManualFrameClock();
        using var install = frames.Install();
        var dock = Dock();
        var window = TestHelpers.Show(dock, ArcMotionLevel.Full);
        dock.Launch(3);
        frames.Advance(TimeSpan.FromSeconds(0.2), Frame);
        Item(dock, 3).CurrentPose.M32.ShouldBeLessThan(-10);
        frames.Advance(TimeSpan.FromSeconds(1.2), Frame);
        Item(dock, 3).CurrentPose.M32.ShouldBe(0, 0.01);
        window.Close();
    }

    [AvaloniaFact]
    public void Arrow_keys_move_between_icons_and_magnify_the_focused_one()
    {
        var frames = new ManualFrameClock();
        using var install = frames.Install();
        var dock = Dock();
        var window = TestHelpers.Show(dock, ArcMotionLevel.Full);
        Item(dock, 0).Focus(NavigationMethod.Tab);
        window.KeyPressQwerty(PhysicalKey.ArrowRight, RawInputModifiers.None);
        Item(dock, 1).IsFocused.ShouldBeTrue();
        window.KeyPressQwerty(PhysicalKey.End, RawInputModifiers.None);
        Item(dock, 4).IsFocused.ShouldBeTrue();
        window.KeyPressQwerty(PhysicalKey.ArrowRight, RawInputModifiers.None);
        Item(dock, 0).IsFocused.ShouldBeTrue();
        window.KeyPressQwerty(PhysicalKey.ArrowLeft, RawInputModifiers.None);
        Item(dock, 4).IsFocused.ShouldBeTrue();
        frames.Advance(TimeSpan.FromSeconds(1), Frame);
        dock.LabelIndex.ShouldBe(4);
        Item(dock, 4).CurrentPose.M11.ShouldBe(1.8, 0.02);
        window.Close();
    }

    [AvaloniaFact]
    public void Reduced_motion_keeps_the_icons_their_size()
    {
        var frames = new ManualFrameClock();
        using var install = frames.Install();
        var dock = Dock();
        var window = TestHelpers.Show(dock, ArcMotionLevel.Reduced);
        var icon = Item(dock, 2);
        window.MouseMove(icon.TranslatePoint(new Point(24, 24), window)!.Value, RawInputModifiers.None);
        dock.Launch(2);
        frames.Advance(TimeSpan.FromSeconds(0.3), Frame);
        icon.CurrentPose.ShouldBe(Matrix.Identity);
        dock.LabelOpacity.ShouldBe(1);
        window.Close();
    }

    [AvaloniaFact]
    public void Icons_are_named_buttons_in_a_toolbar()
    {
        var dock = Dock();
        var window = TestHelpers.Show(dock);
        AutomationProperties.GetName(dock).ShouldBe("Dock");
        ControlAutomationPeer.CreatePeerForElement(dock).GetAutomationControlType().ShouldBe(AutomationControlType.ToolBar);
        var icon = Item(dock, 2);
        AutomationProperties.GetName(icon).ShouldBe("Photos");
        ControlAutomationPeer.CreatePeerForElement(icon).GetAutomationControlType().ShouldBe(AutomationControlType.Button);
        icon.Classes.Contains(":running").ShouldBeTrue();
        window.Close();
    }
}
