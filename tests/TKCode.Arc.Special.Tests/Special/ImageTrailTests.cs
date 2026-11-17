using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Threading;
using TKCode.Arc.Motion;

namespace TKCode.Arc.Tests.Special;

/// <summary>Image trail: spacing along the path, the pool, the pictures' poses, and the control's input and motion levels.</summary>
public class ImageTrailTests
{
    private static readonly TimeSpan Frame = TimeSpan.FromMilliseconds(1000.0 / 60);

    private static ImageTrailModel Model(int capacity = 6) => new(capacity) { Spacing = 50, Lifetime = 0.5, Pictures = 3 };

    // --- model -----------------------------------------------------------------------------------------------------

    [Fact]
    public void The_first_point_drops_a_picture_and_then_one_every_spacing()
    {
        var model = Model();
        model.MoveTo(0, 0).ShouldBe(1);
        model.MoveTo(30, 0).ShouldBe(0);
        model.MoveTo(60, 0).ShouldBe(1);
        model.X(1).ShouldBe(50, 1e-9);
        model.MoveTo(100, 0).ShouldBe(1);
        model.X(2).ShouldBe(100, 1e-9);
    }

    [Fact]
    public void A_fast_flick_leaves_an_even_trail()
    {
        var model = Model(capacity: 10);
        model.MoveTo(0, 0);
        model.MoveTo(0, 210).ShouldBe(4);
        for (var i = 1; i <= 4; i++)
        {
            model.Y(i).ShouldBe(i * 50, 1e-9);
        }
    }

    [Fact]
    public void Pictures_cycle_in_order_and_the_oldest_gives_way()
    {
        var model = Model(capacity: 4);
        for (var x = 0; x <= 250; x += 50)
        {
            model.MoveTo(x, 0);
        }
        model.Dropped.ShouldBe(6);
        model.LiveCount.ShouldBe(4);
        // Slots 0 and 1 were reused by the 5th and 6th pictures.
        model.Picture(0).ShouldBe(4 % 3);
        model.Picture(1).ShouldBe(5 % 3);
        model.Oldest.ShouldBe(2);
    }

    [Fact]
    public void A_move_longer_than_the_pool_drops_only_its_last_pictures()
    {
        var model = Model(capacity: 3);
        model.MoveTo(0, 0);
        model.MoveTo(1000, 0).ShouldBe(3);
        model.X(1).ShouldBe(900, 1e-9);
        model.X(2).ShouldBe(950, 1e-9);
        model.X(0).ShouldBe(1000, 1e-9);
    }

    [Fact]
    public void Lifting_the_pointer_starts_a_new_path()
    {
        var model = Model();
        model.MoveTo(0, 0);
        model.Lift();
        model.MoveTo(500, 500).ShouldBe(1);
        model.X(1).ShouldBe(500);
    }

    [Fact]
    public void A_picture_pops_in_rests_and_sinks_away()
    {
        var model = Model();
        model.MoveTo(0, 0);
        model.Pose(0, true, out var opacity, out var scale, out var sink);
        opacity.ShouldBe(0);
        scale.ShouldBe(0.6, 1e-9);
        model.Step(ImageTrailModel.EnterSeconds * 0.7);
        model.Pose(0, true, out _, out scale, out _);
        scale.ShouldBeGreaterThan(1);
        model.Step(0.2);
        model.Pose(0, true, out opacity, out scale, out sink);
        opacity.ShouldBe(1);
        scale.ShouldBe(1);
        sink.ShouldBe(0);
        model.Step(0.5);
        model.Pose(0, true, out opacity, out scale, out sink);
        opacity.ShouldBeLessThan(1);
        sink.ShouldBeGreaterThan(0);
        model.Step(1).ShouldBeFalse();
        model.LiveCount.ShouldBe(0);
    }

    [Fact]
    public void Without_movement_a_picture_only_fades()
    {
        var model = Model();
        model.MoveTo(0, 0);
        model.Step(0.05);
        model.Pose(0, false, out var opacity, out var scale, out var sink);
        opacity.ShouldBeInRange(0.01, 0.99);
        scale.ShouldBe(1);
        sink.ShouldBe(0);
    }

    // --- control ---------------------------------------------------------------------------------------------------

    private static ImageSource Picture() =>
        ImageSource.FromBitmap(new WriteableBitmap(new PixelSize(8, 6), new Vector(96, 96), PixelFormat.Bgra8888, AlphaFormat.Premul));

    private static ImageTrail Trail() => new()
    {
        Width = 400,
        Height = 300,
        Spacing = 40,
        Sources = [Picture(), Picture(), Picture()],
        Content = new TextBlock { Text = "Move here" }
    };

    [AvaloniaFact]
    public void Moving_the_pointer_leaves_pictures_that_fade_away()
    {
        var frames = new ManualFrameClock();
        using var install = frames.Install();
        var trail = Trail();
        var window = TestHelpers.Show(trail, ArcMotionLevel.Full);
        for (var x = 20; x <= 220; x += 10)
        {
            window.MouseMove(trail.TranslatePoint(new Point(x, 100), window)!.Value, RawInputModifiers.None);
        }
        trail.VisibleImages.ShouldBe(6);
        trail.ImageAt(0).ShouldNotBeNull();
        frames.Advance(TimeSpan.FromSeconds(2), Frame);
        trail.VisibleImages.ShouldBe(0);
        window.Close();
    }

    [AvaloniaFact]
    public void The_arrow_keys_walk_a_cursor_that_leaves_the_trail()
    {
        var frames = new ManualFrameClock();
        using var install = frames.Install();
        var trail = Trail();
        var window = TestHelpers.Show(trail, ArcMotionLevel.Full);
        trail.Focus();
        window.KeyPressQwerty(PhysicalKey.ArrowRight, RawInputModifiers.None);
        // The cursor appears in the middle (one picture), then every 40px of steps leaves another.
        trail.Model.Dropped.ShouldBe(1);
        trail.Model.X(0).ShouldBe(200, 1);
        window.KeyPressQwerty(PhysicalKey.ArrowRight, RawInputModifiers.None);
        trail.Model.Dropped.ShouldBe(2);
        window.KeyPressQwerty(PhysicalKey.Enter, RawInputModifiers.None);
        trail.Model.Dropped.ShouldBe(3);
        window.KeyPressQwerty(PhysicalKey.Escape, RawInputModifiers.None);
        trail.VisibleImages.ShouldBe(0);
        window.Close();
    }

    [AvaloniaFact]
    public void With_motion_off_there_is_no_trail()
    {
        var trail = Trail();
        var window = TestHelpers.Show(trail);
        window.MouseMove(trail.TranslatePoint(new Point(20, 20), window)!.Value, RawInputModifiers.None);
        window.MouseMove(trail.TranslatePoint(new Point(200, 200), window)!.Value, RawInputModifiers.None);
        trail.VisibleImages.ShouldBe(0);
        window.Close();
    }

    [AvaloniaFact]
    public void Without_pictures_nothing_is_dropped()
    {
        var trail = Trail();
        trail.Sources = null;
        var window = TestHelpers.Show(trail, ArcMotionLevel.Full);
        trail.DropAt(new Point(10, 10));
        trail.VisibleImages.ShouldBe(0);
        window.Close();
    }

    [AvaloniaFact]
    public void Is_a_named_group_that_keeps_its_content()
    {
        var trail = Trail();
        var window = TestHelpers.Show(trail);
        AutomationProperties.GetName(trail).ShouldBe("Image trail");
        ControlAutomationPeer.CreatePeerForElement(trail).GetAutomationControlType().ShouldBe(AutomationControlType.Group);
        TestHelpers.Descendant<TextBlock>(trail).Text.ShouldBe("Move here");
        TestHelpers.Descendant<ImageTrailLayer>(trail).IsHitTestVisible.ShouldBeFalse();
        window.Close();
    }

    [AvaloniaFact]
    public void MaxImages_sizes_the_pool()
    {
        var frames = new ManualFrameClock();
        using var install = frames.Install();
        var trail = Trail();
        trail.MaxImages = 3;
        var window = TestHelpers.Show(trail, ArcMotionLevel.Full);
        for (var x = 0; x <= 390; x += 10)
        {
            window.MouseMove(trail.TranslatePoint(new Point(x, 100), window)!.Value, RawInputModifiers.None);
        }
        Dispatcher.UIThread.RunJobs();
        trail.VisibleImages.ShouldBe(3);
        window.Close();
    }
}
