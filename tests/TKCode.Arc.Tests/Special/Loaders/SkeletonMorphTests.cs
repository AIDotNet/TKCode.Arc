using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Layout;
using Avalonia.Threading;
using Avalonia.VisualTree;
using TKCode.Arc.Motion;
using TKCode.Arc.Primitives;

namespace TKCode.Arc.Tests.Special.Loaders;

/// <summary>
/// Skeleton morph: the measured pulse, skeleton geometry and timings as pure rules; then the region and its blocks in a
/// window: skeletons while loading, content not mounted, resolve in reading order 40 ms apart, the stretch spring, the
/// fades and blur, back to loading at once while the shell eases, reduced and no motion, and accessibility.
/// </summary>
public class SkeletonMorphTests
{
    private static readonly TimeSpan Frame = TimeSpan.FromMilliseconds(1000.0 / 60);

    private static bool Attached(Visual visual) => TopLevel.GetTopLevel(visual) is not null;

    // ---- Pure rules (notes sections 3 and 6) ----

    [Fact]
    public void Pulse_breathes_from_one_to_0_55_in_1_4_s_and_back()
    {
        SkeletonMorphMath.Pulse(0, 0.55).ShouldBe(1);
        SkeletonMorphMath.Pulse(1.4, 0.55).ShouldBe(0.55, 1e-9);
        SkeletonMorphMath.Pulse(2.8, 0.55).ShouldBe(1, 1e-9);
        // The in-out curve is symmetric: half way down and half way back up are the same value.
        SkeletonMorphMath.Pulse(0.7, 0.55).ShouldBe(0.775, 1e-6);
        SkeletonMorphMath.Pulse(2.1, 0.55).ShouldBe(0.775, 1e-6);
        // Slow at both ends (cubic-bezier(.65, 0, .35, 1)).
        (1 - SkeletonMorphMath.Pulse(0.14, 0.55)).ShouldBeLessThan(0.45 * 0.1);
        SkeletonMorphMath.Pulse(1.0, 0.55).ShouldBeGreaterThan(SkeletonMorphMath.Pulse(1.2, 0.55));
        SkeletonMorphMath.Pulse(1.6, 0.55).ShouldBeGreaterThan(SkeletonMorphMath.Pulse(1.5, 0.55));
    }

    [Fact]
    public void Skeleton_box_follows_width_lines_and_line_height()
    {
        // Name: width 124, one 20 px line.
        SkeletonMorphMath.SkeletonSize(124, 1, double.NaN, 1, 20, false, 270).ShouldBe(new Size(124, 20));
        // Role: one 18 px line.
        SkeletonMorphMath.SkeletonSize(164, 1, double.NaN, 1, 18, false, 270).ShouldBe(new Size(164, 18));
        // Bio: full width, three 20 px lines.
        SkeletonMorphMath.SkeletonSize(double.NaN, 1, double.NaN, 3, 20, false, 270).ShouldBe(new Size(270, 60));
        SkeletonMorphMath.SkeletonSize(double.NaN, 0.5, double.NaN, 3, 20, false, 270).ShouldBe(new Size(135, 60));
        // Avatar: a circle with a width is square; the button has an explicit height.
        SkeletonMorphMath.SkeletonSize(52, 1, double.NaN, 0, 20, true, 270).ShouldBe(new Size(52, 52));
        SkeletonMorphMath.SkeletonSize(90, 1, 36, 0, 20, false, 270).ShouldBe(new Size(90, 36));
        // Documented default: 12 px.
        SkeletonMorphMath.SkeletonSize(80, 1, double.NaN, 0, 20, false, 270).ShouldBe(new Size(80, 12));
        // A filling skeleton in an unbounded width (a horizontal stack) has nothing to fill.
        SkeletonMorphMath.SkeletonSize(double.NaN, 1, double.NaN, 1, 20, false, double.PositiveInfinity).Width.ShouldBe(0);
    }

    [Fact]
    public void Text_bars_are_centred_in_their_line_boxes_and_the_last_of_several_is_shorter()
    {
        SkeletonMorphMath.BarHeight(20, 12, 6).ShouldBe(12);
        SkeletonMorphMath.BarHeight(18, 12, 6).ShouldBe(12);
        SkeletonMorphMath.BarHeight(16, 12, 6).ShouldBe(10);
        // Bio, measured: bars 270, 270, 167.4 wide; the first spans y 4–16 of its 20 px line.
        SkeletonMorphMath.Bar(0, 3, 270, 20, 12, 0.62).ShouldBe(new Rect(0, 4, 270, 12));
        SkeletonMorphMath.Bar(1, 3, 270, 20, 12, 0.62).ShouldBe(new Rect(0, 24, 270, 12));
        var last = SkeletonMorphMath.Bar(2, 3, 270, 20, 12, 0.62);
        last.Width.ShouldBe(167.4, 1e-9);
        last.Top.ShouldBe(44);
        // A single line keeps its full width.
        SkeletonMorphMath.Bar(0, 1, 124, 20, 12, 0.62).Width.ShouldBe(124);
        // 16 px note line: 10 px bar from y 3.
        SkeletonMorphMath.Bar(0, 1, 76, 16, 10, 0.62).ShouldBe(new Rect(0, 3, 76, 10));
    }

    [Fact]
    public void Shape_radius_is_round_for_circles_and_clamped_otherwise()
    {
        SkeletonMorphMath.ShapeRadius(new Size(52, 52), 6, true).ShouldBe(26);
        SkeletonMorphMath.ShapeRadius(new Size(90, 36), 18, false).ShouldBe(18);
        SkeletonMorphMath.ShapeRadius(new Size(80, 12), 6, false).ShouldBe(6);
        SkeletonMorphMath.ShapeRadius(new Size(80, 8), 6, false).ShouldBe(4);
    }

    [Fact]
    public void Blocks_mount_40_ms_apart_at_full_motion_and_together_otherwise()
    {
        var stagger = TimeSpan.FromMilliseconds(40);
        SkeletonMorphMath.StaggerDelay(0, stagger, ArcMotionLevel.Full).ShouldBe(TimeSpan.Zero);
        // Profile card: six blocks, the last mounts 200 ms after the first.
        SkeletonMorphMath.StaggerDelay(5, stagger, ArcMotionLevel.Full).ShouldBe(TimeSpan.FromMilliseconds(200));
        SkeletonMorphMath.StaggerDelay(5, stagger, ArcMotionLevel.Reduced).ShouldBe(TimeSpan.Zero);
        SkeletonMorphMath.StaggerDelay(5, stagger, ArcMotionLevel.Off).ShouldBe(TimeSpan.Zero);
        SkeletonMorphMath.StaggerDelay(5, TimeSpan.Zero, ArcMotionLevel.Full).ShouldBe(TimeSpan.Zero);
    }

    [Fact]
    public void Stretch_and_shell_use_the_measured_critically_damped_spring()
    {
        // Fitted: ω ≈ 13.1 rad/s, ζ = 1 (k ≈ 172, c ≈ 26) — the Smooth preset.
        var smooth = ArcMotionPresets.Smooth;
        smooth.NaturalFrequency.ShouldBe(13.1, 0.05);
        smooth.DampingRatio.ShouldBe(1, 1e-6);
        SkeletonMorphMath.RevealBlur(0, 4).ShouldBe(4);
        SkeletonMorphMath.RevealBlur(0.5, 4).ShouldBe(2);
        SkeletonMorphMath.RevealBlur(1, 4).ShouldBe(0);
    }

    [Fact]
    public void Blur_changes_in_half_pixel_steps_and_is_dropped_when_invisible()
    {
        SkeletonMorphMath.BlurStep(4).ShouldBe(4);
        SkeletonMorphMath.BlurStep(3.8).ShouldBe(4);
        SkeletonMorphMath.BlurStep(3.7).ShouldBe(3.5);
        SkeletonMorphMath.BlurStep(1.26).ShouldBe(1.5);
        SkeletonMorphMath.BlurStep(0.6).ShouldBe(0.5);
        SkeletonMorphMath.BlurStep(0.3).ShouldBe(0.5);
        SkeletonMorphMath.BlurStep(0.29).ShouldBe(0);
        SkeletonMorphMath.BlurStep(0).ShouldBe(0);
        SkeletonMorphMath.BlurStep(double.NaN).ShouldBe(0);
    }

    [AvaloniaFact]
    public void Pulse_stops_asking_for_frames_when_nothing_breathes_and_restarts_when_a_block_arrives()
    {
        var clock = new ManualFrameClock();
        using var installed = clock.Install();
        var host = new StackPanel();
        var region = new SkeletonMorph { IsLoading = true, Content = host };
        var window = TestHelpers.Show(region, ArcMotionLevel.Full);
        try
        {
            Frames(clock, window, 3);
            region.IsPulsing.ShouldBeFalse();
            var requests = clock.RequestCount;
            Frames(clock, window, 30);
            clock.RequestCount.ShouldBe(requests);
            var block = new MorphBlock { SkeletonWidth = 40, Content = new Border { Width = 40, Height = 12 } };
            host.Children.Add(block);
            Frames(clock, window, 42);
            region.IsPulsing.ShouldBeTrue();
            block.Placeholder!.Opacity.ShouldBeLessThan(1);
            region.IsLoading = false;
            Frames(clock, window, 60);
            region.IsPulsing.ShouldBeFalse();
        }
        finally
        {
            window.Close();
        }
    }

    // ---- The region in a window ----

    private sealed class Card
    {
        public required SkeletonMorph Region { get; init; }
        public required MorphBlock Avatar { get; init; }
        public required MorphBlock Name { get; init; }
        public required MorphBlock Bio { get; init; }
        public required MorphBlock Follow { get; init; }
        public required Button FollowButton { get; init; }
        public required SpringSizeDecorator Shell { get; set; }
        public MorphBlock[] Blocks => new[] { Avatar, Name, Bio, Follow };
    }

    /// <summary>A profile card whose content has fixed sizes: name 70 × 20 (skeleton 124 × 20), bio 2 lines (skeleton 3).</summary>
    private static Card BuildCard(bool loading)
    {
        var avatar = new MorphBlock { IsCircle = true, SkeletonWidth = 52, Content = new Border { Width = 52, Height = 52 } };
        var name = new MorphBlock { SkeletonWidth = 124, Lines = 1, LineHeight = 20, Content = new Border { Width = 70, Height = 20 } };
        var bio = new MorphBlock { Lines = 3, LineHeight = 20, Content = new Border { Height = 40 } };
        var button = new Button { Content = "Follow", Width = 90, Size = ControlSize.Small };
        var follow = new MorphBlock { SkeletonWidth = 90, SkeletonHeight = 36, SkeletonRadius = 18, Content = button };
        var region = new SkeletonMorph
        {
            IsLoading = loading,
            Width = 312,
            LoadingLabel = "Loading profile",
            Content = new StackPanel
            {
                Spacing = 16,
                Children =
                {
                    new StackPanel { Orientation = Orientation.Horizontal, Spacing = 12, Children = { avatar, name } },
                    bio,
                    follow
                }
            }
        };
        region.Classes.Add("card");
        return new Card { Region = region, Avatar = avatar, Name = name, Bio = bio, Follow = follow, FollowButton = button, Shell = null! };
    }

    private static Window ShowCard(Card card, ArcMotionLevel level)
    {
        var window = TestHelpers.Show(new Border { Padding = new Thickness(20), Child = card.Region }, level);
        window.Width = 400;
        window.Height = 600;
        window.UpdateLayout();
        card.Shell = TestHelpers.Descendant<SpringSizeDecorator>(card.Region);
        return window;
    }

    /// <summary>Runs <paramref name="frames"/> frames, laying out and draining posted work after each (like a real frame).</summary>
    private static void Frames(ManualFrameClock clock, Window window, int frames)
    {
        for (var i = 0; i < frames; i++)
        {
            clock.Advance(Frame, Frame);
            window.UpdateLayout();
            Dispatcher.UIThread.RunJobs();
        }
    }

    [AvaloniaFact]
    public void While_loading_blocks_are_their_skeletons_and_the_content_is_not_mounted()
    {
        var card = BuildCard(loading: true);
        var window = ShowCard(card, ArcMotionLevel.Off);
        try
        {
            card.Name.Bounds.Size.ShouldBe(new Size(124, 20));
            card.Avatar.Bounds.Size.ShouldBe(new Size(52, 52));
            card.Follow.Bounds.Size.ShouldBe(new Size(90, 36));
            // The card is 312 wide with 20 px padding and a 1 px border: the bio fills 270.
            card.Bio.Bounds.Size.ShouldBe(new Size(270, 60));
            foreach (var block in card.Blocks)
            {
                block.IsResolved.ShouldBeFalse();
                block.Classes.Contains(":skeleton").ShouldBeTrue();
                block.Placeholder!.IsVisible.ShouldBeTrue();
                block.Placeholder.Opacity.ShouldBe(1);
            }
            card.Bio.Placeholder!.Box.ShouldBe(new Size(270, 60));
            card.Bio.Placeholder.BarRect(2).Width.ShouldBe(167.4, 1e-6);
            card.Name.Placeholder!.BarRect(0).ShouldBe(new Rect(0, 4, 124, 12));
            // Nothing half loaded can be reached: the button is not in the visual tree.
            Attached(card.FollowButton).ShouldBeFalse();
            card.Region.Classes.Contains(":loading").ShouldBeTrue();
            AutomationProperties.GetItemStatus(card.Region).ShouldBe("Loading profile");
            card.Region.Status!.Announcement.ShouldBe("Loading profile");
            // The card: 20 padding + 1 border around the 60 + 16 + 60 + 16 + 36 = 188 tall content (52 head).
            card.Region.Bounds.Height.ShouldBe(52 + 16 + 60 + 16 + 36 + 42);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Without_motion_loading_ends_with_every_block_showing_its_content_at_once()
    {
        var card = BuildCard(loading: true);
        var window = ShowCard(card, ArcMotionLevel.Off);
        try
        {
            var resolved = 0;
            card.Region.Resolved += (_, _) => resolved++;
            card.Region.IsLoading = false;
            window.UpdateLayout();
            Dispatcher.UIThread.RunJobs();
            resolved.ShouldBe(1);
            card.Region.IsResolving.ShouldBeFalse();
            foreach (var block in card.Blocks)
            {
                block.IsResolved.ShouldBeTrue();
                block.Placeholder!.IsVisible.ShouldBeFalse();
                block.ContentHost!.Opacity.ShouldBe(1);
                block.ContentHost.Effect.ShouldBeNull();
            }
            Attached(card.FollowButton).ShouldBeTrue();
            card.Name.Bounds.Size.ShouldBe(new Size(70, 20));
            card.Bio.Bounds.Size.ShouldBe(new Size(270, 40));
            card.Region.Status!.Announcement.ShouldBeNull();
            AutomationProperties.GetItemStatus(card.Region).ShouldBe(string.Empty);
            card.Region.Classes.Contains(":loading").ShouldBeFalse();
            card.Region.Bounds.Height.ShouldBe(52 + 16 + 40 + 16 + 36 + 42);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void A_block_outside_a_region_shows_its_content()
    {
        var inner = new Border { Width = 30, Height = 10 };
        var block = new MorphBlock { SkeletonWidth = 100, Content = inner };
        var window = TestHelpers.Show(block);
        try
        {
            block.IsResolved.ShouldBeTrue();
            Attached(inner).ShouldBeTrue();
            block.Bounds.Size.ShouldBe(new Size(30, 10));
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Loaded_region_shows_content_from_the_start()
    {
        var card = BuildCard(loading: false);
        var window = ShowCard(card, ArcMotionLevel.Full);
        try
        {
            foreach (var block in card.Blocks)
            {
                block.IsResolved.ShouldBeTrue();
                block.Placeholder!.IsVisible.ShouldBeFalse();
            }
            card.Shell.IsMoving.ShouldBeFalse();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Blocks_resolve_in_reading_order_40_ms_apart_and_the_skeleton_stretches_and_fades()
    {
        var clock = new ManualFrameClock();
        using var installed = clock.Install();
        var card = BuildCard(loading: true);
        var window = ShowCard(card, ArcMotionLevel.Full);
        try
        {
            Frames(clock, window, 3);
            var mountedAt = new Dictionary<MorphBlock, int>();
            var resolved = 0;
            card.Region.Resolved += (_, _) => resolved++;
            card.Region.IsLoading = false;
            card.Region.IsResolving.ShouldBeTrue();
            card.Region.Classes.Contains(":resolving").ShouldBeTrue();
            for (var frame = 1; frame <= 12; frame++)
            {
                Frames(clock, window, 1);
                foreach (var block in card.Blocks)
                {
                    if (block.IsResolved && !mountedAt.ContainsKey(block))
                    {
                        mountedAt[block] = frame;
                    }
                }
            }
            // Delays 0, 40, 80, 120 ms → the first frame at or after each (16.7 ms frames).
            mountedAt[card.Avatar].ShouldBe(1);
            mountedAt[card.Name].ShouldBe(3);
            mountedAt[card.Bio].ShouldBe(5);
            mountedAt[card.Follow].ShouldBe(8);

            // The name mounted 10 frames ago: its skeleton is part way from 124 to the content's 70, and fading.
            var box = card.Name.Placeholder!.Box;
            box.Width.ShouldBeLessThan(124);
            box.Width.ShouldBeGreaterThan(70);
            box.Height.ShouldBe(20, 1e-6);
            // The block itself is already the content's size (layout is final at mount, the skeleton overflows).
            card.Name.Bounds.Size.ShouldBe(new Size(70, 20));
            card.Name.Placeholder.Opacity.ShouldBeLessThan(1);
            card.Name.Placeholder.Opacity.ShouldBeGreaterThan(0);
            card.Name.ContentHost!.Opacity.ShouldBeGreaterThan(0.5);
            card.Name.ContentHost.Opacity.ShouldBeLessThan(1);
            // The bio skeleton shrinks from 60 to the content's 40 px.
            card.Bio.Placeholder!.Box.Height.ShouldBeLessThan(60);
            card.Bio.Placeholder.Box.Height.ShouldBeGreaterThan(40);

            Frames(clock, window, 60);
            resolved.ShouldBe(1);
            card.Region.IsResolving.ShouldBeFalse();
            card.Name.Placeholder.Box.ShouldBe(new Size(70, 20));
            card.Name.Placeholder.IsVisible.ShouldBeFalse();
            card.Name.ContentHost.Opacity.ShouldBe(1);
            card.Name.ContentHost.Effect.ShouldBeNull();
            card.Shell.IsMoving.ShouldBeFalse();
            card.Shell.Bounds.Height.ShouldBe(52 + 16 + 40 + 16 + 36, 0.5);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Content_sharpens_out_of_a_4_px_blur_while_it_fades_in()
    {
        var clock = new ManualFrameClock();
        using var installed = clock.Install();
        var card = BuildCard(loading: true);
        var window = ShowCard(card, ArcMotionLevel.Full);
        try
        {
            card.Region.Stagger = TimeSpan.Zero;
            card.Region.IsLoading = false;
            Frames(clock, window, 1);
            var host = card.Avatar.ContentHost!;
            host.IsVisible.ShouldBeTrue();
            host.Effect.ShouldNotBeNull();
            // 330 ms Standard tween: about two thirds there after 100 ms, so about a third of the blur is left.
            Frames(clock, window, 5);
            host.Opacity.ShouldBeGreaterThan(0.5);
            host.Opacity.ShouldBeLessThan(0.95);
            host.Effect.ShouldBe(ArcBlur.Get(SkeletonMorphMath.BlurStep(SkeletonMorphMath.RevealBlur(host.Opacity, 4))));
            // The placeholder waits 50 ms before it starts fading.
            card.Avatar.Placeholder!.Opacity.ShouldBeLessThan(1);
            Frames(clock, window, 30);
            host.Effect.ShouldBeNull();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Placeholder_holds_full_opacity_for_50_ms_after_mount_then_fades()
    {
        var clock = new ManualFrameClock();
        using var installed = clock.Install();
        var card = BuildCard(loading: true);
        var window = ShowCard(card, ArcMotionLevel.Full);
        try
        {
            card.Region.Stagger = TimeSpan.Zero;
            card.Region.IsLoading = false;
            Frames(clock, window, 3);
            // 50 ms: the fade has not started; the pulse is gone (the fade starts from 1).
            card.Avatar.Placeholder!.Opacity.ShouldBe(1);
            Frames(clock, window, 2);
            card.Avatar.Placeholder.Opacity.ShouldBeLessThan(1);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Skeletons_breathe_together_while_loading()
    {
        var clock = new ManualFrameClock();
        using var installed = clock.Install();
        var card = BuildCard(loading: true);
        var window = ShowCard(card, ArcMotionLevel.Full);
        try
        {
            Frames(clock, window, 42); // 0.7 s: half way down
            var opacity = card.Avatar.Placeholder!.Opacity;
            opacity.ShouldBe(0.775, 0.02);
            foreach (var block in card.Blocks)
            {
                block.Placeholder!.Opacity.ShouldBe(opacity);
            }
            Frames(clock, window, 42); // 1.4 s: the low point
            card.Bio.Placeholder!.Opacity.ShouldBe(0.55, 0.01);
            Frames(clock, window, 84); // 2.8 s: back up
            card.Bio.Placeholder.Opacity.ShouldBe(1, 0.01);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Back_to_loading_is_immediate_for_blocks_while_the_shell_eases_its_height()
    {
        var clock = new ManualFrameClock();
        using var installed = clock.Install();
        var card = BuildCard(loading: false);
        var window = ShowCard(card, ArcMotionLevel.Full);
        try
        {
            Frames(clock, window, 2);
            var loadedHeight = card.Shell.Bounds.Height;
            loadedHeight.ShouldBe(52 + 16 + 40 + 16 + 36, 0.5);
            card.Region.IsLoading = true;
            foreach (var block in card.Blocks)
            {
                block.IsResolved.ShouldBeFalse();
                block.Placeholder!.Opacity.ShouldBe(1);
                block.Placeholder.IsVisible.ShouldBeTrue();
            }
            Attached(card.FollowButton).ShouldBeFalse();
            window.UpdateLayout();
            card.Name.Placeholder!.Box.ShouldBe(new Size(124, 20));
            card.Name.Bounds.Size.ShouldBe(new Size(124, 20));
            // The bio is 60 tall again at once, but the shell eases from 160 to 180 and clips meanwhile.
            card.Shell.IsMoving.ShouldBeTrue();
            Frames(clock, window, 6);
            card.Shell.Bounds.Height.ShouldBeGreaterThan(loadedHeight);
            card.Shell.Bounds.Height.ShouldBeLessThan(loadedHeight + 20);
            card.Shell.Clip.ShouldNotBeNull();
            Frames(clock, window, 60);
            card.Shell.Bounds.Height.ShouldBe(loadedHeight + 20, 0.5);
            card.Shell.Clip.ShouldBeNull();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Reloading_mid_resolve_returns_every_block_to_its_skeleton_in_the_same_frame()
    {
        var clock = new ManualFrameClock();
        using var installed = clock.Install();
        var card = BuildCard(loading: true);
        var window = ShowCard(card, ArcMotionLevel.Full);
        try
        {
            var resolved = 0;
            card.Region.Resolved += (_, _) => resolved++;
            card.Region.IsLoading = false;
            Frames(clock, window, 4);
            card.Avatar.IsResolved.ShouldBeTrue();
            card.Follow.IsResolved.ShouldBeFalse();
            card.Region.IsLoading = true;
            card.Region.IsResolving.ShouldBeFalse();
            foreach (var block in card.Blocks)
            {
                block.IsResolved.ShouldBeFalse();
                block.Placeholder!.Opacity.ShouldBe(1);
            }
            // Blocks that were waiting their turn never mount.
            Frames(clock, window, 30);
            foreach (var block in card.Blocks)
            {
                block.IsResolved.ShouldBeFalse();
            }
            resolved.ShouldBe(0);
            card.Region.IsLoading = false;
            Frames(clock, window, 80);
            resolved.ShouldBe(1);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Reduced_motion_crossfades_without_pulse_stagger_stretch_or_blur()
    {
        var clock = new ManualFrameClock();
        using var installed = clock.Install();
        var card = BuildCard(loading: true);
        var window = ShowCard(card, ArcMotionLevel.Reduced);
        try
        {
            Frames(clock, window, 42);
            card.Bio.Placeholder!.Opacity.ShouldBe(1);
            card.Region.IsLoading = false;
            Frames(clock, window, 1);
            foreach (var block in card.Blocks)
            {
                block.IsResolved.ShouldBeTrue();
            }
            card.Name.ContentHost!.Effect.ShouldBeNull();
            // No stretch: the skeleton takes the content's box at once, and both layers crossfade in 120 ms.
            card.Name.Placeholder!.Box.ShouldBe(new Size(70, 20));
            card.Name.Placeholder.Opacity.ShouldBeLessThan(1);
            card.Name.Placeholder.Opacity.ShouldBeGreaterThan(0);
            card.Shell.IsMoving.ShouldBeFalse();
            Frames(clock, window, 8);
            card.Name.Placeholder.IsVisible.ShouldBeFalse();
            card.Name.ContentHost.Opacity.ShouldBe(1);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Nested_regions_keep_their_own_blocks()
    {
        var innerBlock = new MorphBlock { SkeletonWidth = 40, Content = new Border { Width = 40, Height = 12 } };
        var inner = new SkeletonMorph { IsLoading = true, Content = innerBlock };
        var outerBlock = new MorphBlock { SkeletonWidth = 40, Content = new Border { Width = 40, Height = 12 } };
        var outer = new SkeletonMorph { IsLoading = true, Content = new StackPanel { Children = { outerBlock, inner } } };
        var window = TestHelpers.Show(outer);
        try
        {
            innerBlock.Owner.ShouldBe(inner);
            outer.Blocks.ShouldBe(new[] { outerBlock });
            outer.IsLoading = false;
            outerBlock.IsResolved.ShouldBeTrue();
            innerBlock.IsResolved.ShouldBeFalse();
            inner.IsLoading = false;
            innerBlock.IsResolved.ShouldBeTrue();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Content_changes_while_loaded_show_at_once_and_text_content_uses_the_template()
    {
        var block = new MorphBlock { SkeletonWidth = 80, Lines = 1, Content = "Lena Ortiz" };
        var region = new SkeletonMorph { IsLoading = true, Content = block };
        var window = TestHelpers.Show(region);
        try
        {
            block.ContentHost!.Content.ShouldBeNull();
            region.IsLoading = false;
            block.ContentHost.Content.ShouldBe("Lena Ortiz");
            block.Content = "Priya Raman";
            block.ContentHost.Content.ShouldBe("Priya Raman");
            region.IsLoading = true;
            block.ContentHost.Content.ShouldBeNull();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Card_class_draws_the_measured_card()
    {
        var card = BuildCard(loading: true);
        var window = ShowCard(card, ArcMotionLevel.Off);
        try
        {
            card.Region.Padding.ShouldBe(new Thickness(20));
            card.Region.CornerRadius.ShouldBe(new CornerRadius(26));
            card.Region.BorderThickness.ShouldBe(new Thickness(1));
            card.Region.Background.ShouldNotBeNull();
            card.Region.Focusable.ShouldBeFalse();
            card.Bio.Focusable.ShouldBeFalse();
        }
        finally
        {
            window.Close();
        }
    }
}
