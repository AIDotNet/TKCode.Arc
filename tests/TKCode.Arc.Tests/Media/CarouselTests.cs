using Avalonia;
using Avalonia.Automation.Peers;
using Avalonia.Automation.Provider;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Threading;
using TKCode.Arc.Motion;

namespace TKCode.Arc.Tests.Media;

public class CarouselTests
{
    private static Carousel Make(int count, double slideHeight = 160)
    {
        var carousel = new Carousel { Label = "Suites" };
        for (var i = 0; i < count; i++)
        {
            carousel.Items.Add(new Border { Height = slideHeight, Child = new TextBlock { Text = $"Slide {i + 1}" } });
        }
        return carousel;
    }

    private static Window Show(Carousel carousel, ArcMotionLevel level = ArcMotionLevel.Off)
        => TestHelpers.Show(carousel, level);

    private static void Press(Window window, PhysicalKey key)
    {
        window.KeyPressQwerty(key, RawInputModifiers.None);
        window.KeyReleaseQwerty(key, RawInputModifiers.None);
        Dispatcher.UIThread.RunJobs();
    }

    private static T Part<T>(Carousel carousel, string name) where T : Control => TestHelpers.Descendant<T>(carousel, name);

    [AvaloniaFact]
    public void Slides_Are_Centered_At_Arc_Width_With_Neighbours_Dimmed()
    {
        var carousel = Make(5);
        var window = Show(carousel);
        try
        {
            carousel.SelectedIndex.ShouldBe(0);
            // min(80% of 400, 340) = 320, gap 16: the first slide sits 40px in.
            carousel.SlideWidthPx.ShouldBe(320);
            carousel.Step.ShouldBe(336);
            var track = carousel.Track!;
            var first = (CarouselSlide)track.SlideAt(0)!;
            first.Bounds.X.ShouldBe(40);
            first.Bounds.Width.ShouldBe(320);
            first.Opacity.ShouldBe(1);
            first.IsActive.ShouldBeTrue();
            var second = (CarouselSlide)track.SlideAt(1)!;
            second.Opacity.ShouldBe(0.5);
            ((Avalonia.Media.ScaleTransform)second.RenderTransform!).ScaleX.ShouldBe(0.9, 1e-9);
            second.IsActive.ShouldBeFalse();
            second.IsHitTestVisible.ShouldBeFalse();
            var pager = Part<CarouselPager>(carousel, Carousel.PagerPart);
            pager.Tabs.Count.ShouldBe(5);
            pager.Tabs[0].IsTabStop.ShouldBeTrue();
            pager.Tabs[1].IsTabStop.ShouldBeFalse();
            Part<MediaButton>(carousel, Carousel.PreviousButtonPart).IsSoftDisabled.ShouldBeTrue();
            Part<MediaButton>(carousel, Carousel.NextButtonPart).IsSoftDisabled.ShouldBeFalse();
            Part<MediaButton>(carousel, Carousel.PlayButtonPart).IsVisible.ShouldBeFalse();
            // The control row lines up with the centered slide.
            Part<DockPanel>(carousel, Carousel.ControlsPart).Margin.Left.ShouldBe(40);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Keys_Move_Between_Slides_And_Announce()
    {
        var carousel = Make(5);
        var window = Show(carousel);
        try
        {
            Part<CarouselViewport>(carousel, Carousel.ViewportPart).Focus(NavigationMethod.Tab);
            Dispatcher.UIThread.RunJobs();
            carousel.Classes.Contains(Carousel.ViewportFocusPseudoClass).ShouldBeTrue();
            Press(window, PhysicalKey.ArrowRight);
            carousel.SelectedIndex.ShouldBe(1);
            carousel.TrackOffset.ShouldBe(-336);
            carousel.LastAnnouncement.ShouldBe("2 of 5");
            Press(window, PhysicalKey.End);
            carousel.SelectedIndex.ShouldBe(4);
            Part<MediaButton>(carousel, Carousel.NextButtonPart).IsSoftDisabled.ShouldBeTrue();
            Press(window, PhysicalKey.ArrowRight);
            carousel.SelectedIndex.ShouldBe(4);
            Press(window, PhysicalKey.Home);
            carousel.SelectedIndex.ShouldBe(0);
            carousel.TrackOffset.ShouldBe(0);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Arrow_Keys_On_A_Tab_Move_Focus_To_The_New_Tab()
    {
        var carousel = Make(4);
        var window = Show(carousel);
        try
        {
            var pager = Part<CarouselPager>(carousel, Carousel.PagerPart);
            pager.Tabs[0].Focus(NavigationMethod.Tab);
            Press(window, PhysicalKey.ArrowRight);
            carousel.SelectedIndex.ShouldBe(1);
            pager.Tabs[1].IsFocused.ShouldBeTrue();
            pager.Tabs[1].IsTabStop.ShouldBeTrue();
            pager.Tabs[0].IsTabStop.ShouldBeFalse();
            var peer = ControlAutomationPeer.CreatePeerForElement(pager.Tabs[1]);
            peer.GetAutomationControlType().ShouldBe(AutomationControlType.TabItem);
            peer.GetName().ShouldBe("2 of 4");
            peer.GetProvider<ISelectionItemProvider>()!.IsSelected.ShouldBeTrue();
            peer.GetProvider<ISelectionItemProvider>()!.IsSelected.ShouldBeTrue();
            ControlAutomationPeer.CreatePeerForElement(pager.Tabs[3]).GetProvider<ISelectionItemProvider>()!.Select();
            carousel.SelectedIndex.ShouldBe(3);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Buttons_Step_And_Stay_Focusable_At_The_Ends()
    {
        var carousel = Make(3);
        var window = Show(carousel);
        try
        {
            var next = Part<MediaButton>(carousel, Carousel.NextButtonPart);
            next.Focus(NavigationMethod.Tab);
            Press(window, PhysicalKey.Enter);
            carousel.SelectedIndex.ShouldBe(1);
            Press(window, PhysicalKey.Enter);
            carousel.SelectedIndex.ShouldBe(2);
            next.IsSoftDisabled.ShouldBeTrue();
            Press(window, PhysicalKey.Enter);
            carousel.SelectedIndex.ShouldBe(2);
            next.IsFocused.ShouldBeTrue();
            carousel.Previous();
            carousel.SelectedIndex.ShouldBe(1);
            carousel.GoTo(99);
            carousel.SelectedIndex.ShouldBe(2);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Dragging_Settles_On_The_Nearest_Slide_And_Taps_Select_A_Neighbour()
    {
        var carousel = Make(5);
        var window = Show(carousel);
        try
        {
            var y = 80;
            window.MouseDown(new Point(200, y), MouseButton.Left);
            window.MouseMove(new Point(190, y), RawInputModifiers.LeftMouseButton);
            window.MouseMove(new Point(20, y), RawInputModifiers.LeftMouseButton);
            Dispatcher.UIThread.RunJobs();
            carousel.Classes.Contains(Carousel.DraggingPseudoClass).ShouldBeTrue();
            // The slop is consumed: the track follows from where the drag started.
            carousel.TrackOffset.ShouldBe(-170, 0.01);
            Thread.Sleep(80);
            window.MouseUp(new Point(20, y), MouseButton.Left);
            Dispatcher.UIThread.RunJobs();
            carousel.Classes.Contains(Carousel.DraggingPseudoClass).ShouldBeFalse();
            carousel.SelectedIndex.ShouldBe(1);
            carousel.TrackOffset.ShouldBe(-336);

            // Dragging past the first slide stretches against a rubber band and springs back.
            carousel.GoTo(0);
            Dispatcher.UIThread.RunJobs();
            window.MouseDown(new Point(100, y), MouseButton.Left);
            window.MouseMove(new Point(110, y), RawInputModifiers.LeftMouseButton);
            window.MouseMove(new Point(390, y), RawInputModifiers.LeftMouseButton);
            window.MouseMove(new Point(399, y), RawInputModifiers.LeftMouseButton);
            Dispatcher.UIThread.RunJobs();
            carousel.TrackOffset.ShouldBeGreaterThan(0);
            carousel.TrackOffset.ShouldBeLessThan(289);
            Thread.Sleep(80);
            window.MouseUp(new Point(399, y), MouseButton.Left);
            Dispatcher.UIThread.RunJobs();
            carousel.SelectedIndex.ShouldBe(0);

            // A tap on the visible edge of the next slide brings it to the center.
            window.MouseDown(new Point(396, y), MouseButton.Left);
            window.MouseUp(new Point(396, y), MouseButton.Left);
            Dispatcher.UIThread.RunJobs();
            carousel.SelectedIndex.ShouldBe(1);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Long_Carousels_Realize_Only_Nearby_Slides()
    {
        var carousel = new Carousel { ItemsSource = Enumerable.Range(0, 500).Select(i => $"Slide {i}").ToList() };
        var window = Show(carousel);
        try
        {
            var track = carousel.Track!;
            track.RealizedCount.ShouldBeLessThanOrEqualTo(6);
            carousel.SelectedIndex = 499;
            Dispatcher.UIThread.RunJobs();
            track.RealizedCount.ShouldBeLessThanOrEqualTo(6);
            track.SlideAt(499).ShouldNotBeNull();
            track.SlideAt(0).ShouldBeNull();
            ((CarouselSlide)track.SlideAt(499)!).Content.ShouldBe("Slide 499");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Autoplay_Rotates_And_Pauses_On_Hover()
    {
        var carousel = Make(3);
        carousel.Interval = TimeSpan.FromMilliseconds(250);
        carousel.IsAutoPlay = true;
        var window = Show(carousel, ArcMotionLevel.Full);
        try
        {
            MediaTestSupport.WaitFor(() => carousel.CanRotate).ShouldBeTrue();
            var play = Part<MediaButton>(carousel, Carousel.PlayButtonPart);
            play.IsVisible.ShouldBeTrue();
            Avalonia.Automation.AutomationProperties.GetName(play).ShouldBe("Pause slide rotation");
            carousel.IsRotating.ShouldBeTrue();
            MediaTestSupport.WaitFor(() => carousel.SelectedIndex == 1).ShouldBeTrue();
            // Rotation never announces.
            carousel.LastAnnouncement.ShouldBeNull();

            window.MouseMove(new Point(200, 80));
            Dispatcher.UIThread.RunJobs();
            carousel.IsRotating.ShouldBeFalse();
            MediaTestSupport.Pump(400);
            carousel.SelectedIndex.ShouldBe(1);
            window.MouseMove(new Point(200, 290));
            Dispatcher.UIThread.RunJobs();
            carousel.IsRotating.ShouldBeTrue();

            // The play control stops rotation.
            play.Focus(NavigationMethod.Pointer);
            carousel.IsAutoPlay = false;
            Dispatcher.UIThread.RunJobs();
            carousel.IsRotating.ShouldBeFalse();
            Avalonia.Automation.AutomationProperties.GetName(play).ShouldBe("Start slide rotation");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Reduced_Motion_Has_No_Rotation()
    {
        var carousel = Make(3);
        carousel.Interval = TimeSpan.FromMilliseconds(250);
        carousel.IsAutoPlay = true;
        var window = Show(carousel, ArcMotionLevel.Reduced);
        try
        {
            MediaTestSupport.Pump(100);
            carousel.CanRotate.ShouldBeFalse();
            carousel.IsRotating.ShouldBeFalse();
            Part<MediaButton>(carousel, Carousel.PlayButtonPart).IsVisible.ShouldBeFalse();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Full_Motion_Glides_With_A_Spring()
    {
        var clock = new ManualFrameClock();
        using var _ = clock.Install();
        var carousel = Make(4);
        var window = Show(carousel, ArcMotionLevel.Full);
        try
        {
            carousel.Next();
            Dispatcher.UIThread.RunJobs();
            carousel.SelectedIndex.ShouldBe(1);
            carousel.TrackOffset.ShouldBe(0);
            clock.Advance(TimeSpan.FromMilliseconds(120));
            Dispatcher.UIThread.RunJobs();
            carousel.TrackOffset.ShouldBeLessThan(-30);
            carousel.TrackOffset.ShouldBeGreaterThan(-336);
            var pager = Part<CarouselPager>(carousel, Carousel.PagerPart);
            clock.Advance(TimeSpan.FromSeconds(1));
            Dispatcher.UIThread.RunJobs();
            carousel.TrackOffset.ShouldBe(-336, 0.01);
            carousel.Progress.ShouldBe(1, 0.001);
            pager.Tabs[1].IsTabStop.ShouldBeTrue();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Carousel_Reports_Its_Role()
    {
        var carousel = Make(2);
        var window = Show(carousel);
        try
        {
            var peer = ControlAutomationPeer.CreatePeerForElement(carousel);
            peer.GetLocalizedControlType().ShouldBe("carousel");
            peer.GetName().ShouldBe("Suites");
            var slide = carousel.Track!.SlideAt(1)!;
            var slidePeer = ControlAutomationPeer.CreatePeerForElement(slide);
            slidePeer.GetName().ShouldBe("2 of 2");
            slidePeer.IsControlElement().ShouldBeFalse();
            ControlAutomationPeer.CreatePeerForElement(carousel.Track!.SlideAt(0)!).IsControlElement().ShouldBeTrue();
        }
        finally
        {
            window.Close();
        }
    }

    [Fact]
    public void Release_Physics_Match_Arc()
    {
        // A slow release lands on the nearest slide; a flick always moves at least one.
        CarouselMath.SelectTarget(-100, 0, 336, 0, 5).ShouldBe(0);
        CarouselMath.SelectTarget(-100, -400, 336, 0, 5).ShouldBe(1);
        CarouselMath.SelectTarget(-200, 0, 336, 0, 5).ShouldBe(1);
        // Momentum carries further than the finger.
        CarouselMath.SelectTarget(-336, -2000, 336, 1, 5).ShouldBe(2);
        CarouselMath.SelectTarget(-336, -6000, 336, 1, 5).ShouldBe(4);
        CarouselMath.SelectTarget(0, 5000, 336, 0, 5).ShouldBe(0);
        CarouselMath.Emphasis(0.5, 0, 4).ShouldBe(0.5);
        CarouselMath.Emphasis(-1, 0, 4).ShouldBe(1);
        CarouselMath.Band(50, -100, 0, 400).ShouldBeLessThan(50);
        CarouselMath.Unband(CarouselMath.Band(50, -100, 0, 400), -100, 0, 400).ShouldBe(50, 1e-6);
    }
}
