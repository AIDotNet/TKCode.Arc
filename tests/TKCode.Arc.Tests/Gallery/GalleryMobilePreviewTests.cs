using TKCode.Arc.Gallery.Docs;
using TKCode.Arc.Gallery.Localization;
using TKCode.Arc.Gallery.Shell;
using TKCode.Arc.Motion;
using TKCode.Arc.Primitives;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.LogicalTree;
using Avalonia.VisualTree;

namespace TKCode.Arc.Tests.Gallery;

/// <summary>
/// The Fit / Mobile switch of a docs page's Preview (design/engineering/gallery-mobile-preview.md, section 3): the same
/// demo view moves onto a phone whose 390 × 844 viewport is the demo's window.
/// </summary>
public class GalleryMobilePreviewTests
{
    private static Window Show(GalleryDemoView view, ArcMotionLevel level = ArcMotionLevel.Off)
    {
        var window = TestHelpers.Show(new Border { Width = 880, Child = view }, level);
        window.Width = 1200;
        window.Height = 1100;
        Dispatcher.UIThread.RunJobs();
        return window;
    }

    private static Rect InWindow(Visual visual, Visual window)
        => new Rect(visual.Bounds.Size).TransformToAABB(visual.TransformToVisual(window)!.Value);

    [AvaloniaFact]
    public void Only_The_Page_Preview_Of_A_Desktop_Component_Has_The_Size_Switch()
    {
        var hero = new GalleryDemoView("Button.Variants", hero: true);
        var usage = new GalleryDemoView("Button.Sizes");
        var mobileId = GalleryDemos.All.First(d => GalleryDemoView.IsMobileDemo(d)).Id;
        var mobile = new GalleryDemoView(mobileId, hero: true);

        usage.SizeSwitch.ShouldBeNull();
        mobile.SizeSwitch.ShouldBeNull();
        var window = Show(hero);
        try
        {
            var size = hero.SizeSwitch.ShouldNotBeNull();
            size.Segments.Count.ShouldBe(2);
            AutomationProperties.GetName(size).ShouldBe("Preview size");
            size.Segments.Select(AutomationProperties.GetName).ShouldBe(["Fit", "Mobile"]);
            size.SelectedIndex.ShouldBe(0);
            hero.IsMobile.ShouldBeFalse();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Mobile_Moves_The_Same_View_Into_A_390_Viewport_And_Fit_Moves_It_Back()
    {
        var view = new GalleryDemoView("Input.Basic", hero: true);
        var window = Show(view);
        try
        {
            var demo = view.View.ShouldNotBeNull();
            var input = demo.GetVisualDescendants().OfType<Input>().First();
            input.Text = "typed before switching";
            var fitWidth = demo.Bounds.Width;

            view.SizeSwitch!.Segments[1].RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));
            Dispatcher.UIThread.RunJobs();

            view.IsMobile.ShouldBeTrue();
            var phone = view.Phone.ShouldNotBeNull();
            phone.Screen.ShouldBeSameAs(demo);
            ArcViewport.FindHost(demo).ShouldBeSameAs(phone.Viewport);
            ArcViewport.GetSize(demo).ShouldBe(new Size(390, 844));
            ArcViewport.IsCompact(demo).ShouldBeTrue();
            phone.CurrentSize.ShouldBe(GalleryPreviewPhone.OutlineSize);
            // The demo lays out in the viewport less the screen's padding, not in the stage's width.
            demo.Bounds.Width.ShouldBeLessThanOrEqualTo(390);
            demo.Bounds.Width.ShouldBeLessThan(fitWidth);
            input.Text.ShouldBe("typed before switching");
            // On the page the phone is drawn at 680 / 844 of its viewport.
            var screen = InWindow(phone.Viewport, window);
            screen.Width.ShouldBe(390 * phone.Scale, 0.5);
            screen.Height.ShouldBe(680, 0.5);

            view.ShowMobile(false);
            Dispatcher.UIThread.RunJobs();
            view.IsMobile.ShouldBeFalse();
            view.SizeSwitch.SelectedIndex.ShouldBe(0);
            phone.Screen.ShouldBeNull();
            ArcViewport.FindHost(demo).ShouldBeNull();
            demo.Bounds.Width.ShouldBe(fitWidth, 0.5);
            input.Text.ShouldBe("typed before switching");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Switching_Keeps_Keyboard_Focus_In_The_Demo()
    {
        var view = new GalleryDemoView("Input.Basic", hero: true);
        var window = Show(view);
        try
        {
            var input = view.View!.GetVisualDescendants().OfType<TextBox>().First();
            input.Focus();
            Dispatcher.UIThread.RunJobs();
            input.IsKeyboardFocusWithin.ShouldBeTrue();

            view.ShowMobile(true);
            Dispatcher.UIThread.RunJobs();
            input.IsKeyboardFocusWithin.ShouldBeTrue();

            view.ShowMobile(false);
            Dispatcher.UIThread.RunJobs();
            input.IsKeyboardFocusWithin.ShouldBeTrue();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void A_Select_Menu_Opens_On_The_Phone_At_The_Phone_Scale()
    {
        var view = new GalleryDemoView("Select.Field", hero: true);
        var window = Show(view);
        try
        {
            view.ShowMobile(true);
            Dispatcher.UIThread.RunJobs();
            var select = view.View!.GetVisualDescendants().OfType<Select>().First();
            select.Open();
            Dispatcher.UIThread.RunJobs();

            var surface = TestHelpers.Descendant<ArcPopup>(select).Surface.ShouldNotBeNull();
            var menu = InWindow(surface, window);
            var field = InWindow(select, window);
            var screen = ArcViewport.GetBounds(select).ShouldNotBeNull();
            screen.Contains(menu).ShouldBeTrue($"menu {menu} is outside the phone's screen {screen}");
            // The field is 280 wide in the viewport; on the page both it and its menu are scaled.
            field.Width.ShouldBe(280 * view.Phone!.Scale, 0.5);
            menu.Width.ShouldBeLessThan(280);

            // Leaving the phone closes the menu: it belongs to the place the demo leaves.
            view.ShowMobile(false);
            Dispatcher.UIThread.RunJobs();
            select.IsOpen.ShouldBeFalse();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void A_Dialog_Opened_On_The_Phone_Covers_Only_Its_Screen()
    {
        var view = new GalleryDemoView("Dialog.ShortForm", hero: true);
        var window = Show(view);
        try
        {
            view.ShowMobile(true);
            Dispatcher.UIThread.RunJobs();
            var dialog = view.View!.GetLogicalDescendants().OfType<ModalOverlay>().First();
            dialog.IsOpen = true;
            Dispatcher.UIThread.RunJobs();

            var host = view.Phone!.Viewport.Layer.GetVisualDescendants().OfType<ModalHost>().ShouldHaveSingleItem();
            InWindow(host, window).ShouldBe(InWindow(view.Phone.Viewport, window));
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void The_Outline_Morphs_From_The_Stage_Content_With_Full_Motion()
    {
        var view = new GalleryDemoView("Button.Variants", hero: true);
        var window = Show(view, ArcMotionLevel.Full);
        try
        {
            view.ShowMobile(true);
            Dispatcher.UIThread.RunJobs();
            var phone = view.Phone.ShouldNotBeNull();
            phone.CurrentSize.ShouldNotBe(GalleryPreviewPhone.OutlineSize);
            TestHelpers.PumpUntil(() => !phone.IsMorphing).ShouldBeTrue();
            phone.CurrentSize.Width.ShouldBe(GalleryPreviewPhone.OutlineSize.Width, 0.5);
            phone.CurrentSize.Height.ShouldBe(GalleryPreviewPhone.OutlineSize.Height, 0.5);

            // Back to Fit (as on uiarc.dev): the demo is back on the stage at once and fades in, while the phone keeps a
            // picture of it, grows and fades out, then leaves the stage.
            view.ShowMobile(false);
            Dispatcher.UIThread.RunJobs();
            ArcViewport.FindHost(view.View!).ShouldBeNull();
            phone.Screen.ShouldBeOfType<Image>();
            phone.GetVisualParent().ShouldNotBeNull();
            view.View!.Opacity.ShouldBeLessThan(1);
            TestHelpers.PumpUntil(() => phone.GetVisualParent() is null).ShouldBeTrue();
            phone.Screen.ShouldBeNull();
            TestHelpers.PumpUntil(() => view.View.Opacity >= 1).ShouldBeTrue();

            // And to Mobile again: a picture of the stage's demo fades where it stood; the demo fades in on the phone.
            view.ShowMobile(true);
            Dispatcher.UIThread.RunJobs();
            phone.Screen.ShouldBeSameAs(view.View);
            view.View.Opacity.ShouldBeLessThan(1);
            TestHelpers.PumpUntil(() => view.View.Opacity >= 1 && !phone.IsMorphing).ShouldBeTrue();
            view.GetVisualDescendants().OfType<Image>().ShouldBeEmpty();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void The_Code_Tab_Hides_The_Size_Switch_And_The_Names_Follow_The_Language()
    {
        var view = new GalleryDemoView("Button.Variants", hero: true);
        var window = Show(view);
        try
        {
            view.ShowCode(true);
            view.SizeSwitch!.IsVisible.ShouldBeFalse();
            view.ShowCode(false);
            view.SizeSwitch.IsVisible.ShouldBeTrue();

            GalleryText.SetLanguage("zh-CN");
            Dispatcher.UIThread.RunJobs();
            AutomationProperties.GetName(view.SizeSwitch).ShouldBe("预览尺寸");
            view.SizeSwitch.Segments.Select(AutomationProperties.GetName).ShouldBe(["适应", "移动端"]);
        }
        finally
        {
            GalleryText.SetLanguage("en-US");
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Only_The_Page_Preview_Has_A_Fullscreen_Button_And_A_Mobile_Demo_Has_No_Size_Switch_In_It()
    {
        new GalleryDemoView("Button.Sizes").FullscreenButton.ShouldBeNull();
        var mobileId = GalleryDemos.All.First(d => GalleryDemoView.IsMobileDemo(d)).Id;
        var view = new GalleryDemoView(mobileId, hero: true);
        var window = Show(view);
        try
        {
            var button = view.FullscreenButton.ShouldNotBeNull();
            AutomationProperties.GetName(button).ShouldBe("Fullscreen");
            view.OpenFullscreen();
            Dispatcher.UIThread.RunJobs();
            var fullscreen = view.Fullscreen.ShouldNotBeNull();
            fullscreen.SizeSwitch.ShouldBeNull();
            // The phone frame the demo runs in moves with it.
            fullscreen.GetVisualDescendants().OfType<PhoneFrame>().ShouldHaveSingleItem();
            fullscreen.Close();
            Dispatcher.UIThread.RunJobs();
            view.Fullscreen.ShouldBeNull();
            view.GetVisualDescendants().OfType<PhoneFrame>().ShouldHaveSingleItem();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Fullscreen_Covers_The_Window_With_The_Same_View_And_Escape_Puts_It_Back()
    {
        var view = new GalleryDemoView("Input.Basic", hero: true);
        var window = Show(view);
        try
        {
            var demo = view.View.ShouldNotBeNull();
            var input = demo.GetVisualDescendants().OfType<Input>().First();
            input.Text = "kept across fullscreen";
            var stageHeight = demo.FindAncestorOfType<Border>()!.FindAncestorOfType<Border>()!.Bounds.Height;

            view.FullscreenButton!.Focus();
            view.FullscreenButton.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));
            Dispatcher.UIThread.RunJobs();

            var fullscreen = view.Fullscreen.ShouldNotBeNull();
            fullscreen.IsOpen.ShouldBeTrue();
            fullscreen.IsVisualAncestorOf(demo).ShouldBeTrue();
            AutomationProperties.GetName(fullscreen).ShouldBe("Input");
            var panel = InWindow(fullscreen, window);
            panel.Width.ShouldBe(window.ClientSize.Width, 1);
            panel.Height.ShouldBe(window.ClientSize.Height, 1);
            input.Text.ShouldBe("kept across fullscreen");
            // The page keeps the stage's height while the demo is away.
            view.Bounds.Height.ShouldBeGreaterThan(stageHeight);

            window.KeyPressQwerty(PhysicalKey.Escape, RawInputModifiers.None);
            Dispatcher.UIThread.RunJobs();
            TestHelpers.PumpUntil(() => view.Fullscreen is null).ShouldBeTrue();
            view.IsVisualAncestorOf(demo).ShouldBeTrue();
            input.Text.ShouldBe("kept across fullscreen");
            TestHelpers.PumpUntil(() => view.FullscreenButton.IsKeyboardFocusWithin).ShouldBeTrue();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Fullscreen_Opens_In_The_Current_Size_Fits_The_Phone_To_The_Window_And_Switches_Inside()
    {
        var view = new GalleryDemoView("Select.Field", hero: true);
        var window = Show(view);
        try
        {
            view.ShowMobile(true);
            Dispatcher.UIThread.RunJobs();
            view.OpenFullscreen();
            Dispatcher.UIThread.RunJobs();

            var fullscreen = view.Fullscreen.ShouldNotBeNull();
            fullscreen.IsMobile.ShouldBeTrue();
            fullscreen.SizeSwitch.ShouldNotBeNull().SelectedIndex.ShouldBe(1);
            var phone = fullscreen.Phone.ShouldNotBeNull();
            phone.Screen.ShouldBeSameAs(view.View);
            // A 1100-tall window has room for the viewport at (nearly) its real size, more than on the page.
            phone.Scale.ShouldBeGreaterThan(view.Phone!.Scale);
            phone.Scale.ShouldBeLessThanOrEqualTo(1);
            ArcViewport.GetSize(view.View!).ShouldBe(new Size(390, 844));

            fullscreen.ShowMobile(false);
            Dispatcher.UIThread.RunJobs();
            phone.Screen.ShouldBeNull();
            ArcViewport.FindHost(view.View!).ShouldBeNull();

            fullscreen.Close();
            Dispatcher.UIThread.RunJobs();
            // The page's own size did not change: the demo goes back onto its phone.
            view.IsMobile.ShouldBeTrue();
            view.Phone.Screen.ShouldBeSameAs(view.View);
        }
        finally
        {
            window.Close();
        }
    }
}
