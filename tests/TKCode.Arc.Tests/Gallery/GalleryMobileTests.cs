using TKCode.Arc.Gallery.Catalog;
using TKCode.Arc.Gallery.Docs;
using TKCode.Arc.Gallery.Localization;
using TKCode.Arc.Gallery.Shell;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Avalonia.VisualTree;
using MobilePage = TKCode.Arc.Mobile.Page;

namespace TKCode.Arc.Tests.Gallery;

/// <summary>
/// The Mobile section: the TKCode.Arc.Mobile components, their docs pages and demos, and the phone frame the demos run in.
/// </summary>
public class GalleryMobileTests
{
    private static GalleryCategory Mobile => GalleryCatalog.Mobile;

    [AvaloniaFact]
    public void Mobile_Section_Lists_The_Catalog_Components_In_Order_And_Is_Not_Counted_As_Arc()
    {
        Mobile.Entries.Select(e => e.ClassName).ShouldBe(MobileCatalog.Components.Select(c => c.ClassName));
        Mobile.Entries.Select(e => e.ClassName).ShouldBe(["MobilePage", "MobileNavbar", "MobileTabbar", "MobileNavigationStack", "MobileListGroup", "MobileSheet", "MobileButton", "MobileSwitch", "MobileTextField", "MobileSearchbar"]);
        Mobile.Entries.ShouldAllBe(e => !e.IsArc && e.ArcUrl == null);
        GalleryCatalog.Count.ShouldBe(111);
        GalleryCatalog.AllCategories.ShouldNotContain(Mobile);
        GalleryCatalog.AllEntries.Select(e => e.ClassName).ShouldBeUnique();
        // The desktop Button and Switch keep their own entries: the mobile ones carry a prefix.
        GalleryCatalog.AllEntries.ShouldContain(e => e.ClassName == "Button");
        GalleryCatalog.AllEntries.ShouldContain(e => e.ClassName == "Switch");
    }

    [AvaloniaFact]
    public void Every_Mobile_Component_Has_Demos_And_An_Up_To_Date_Page_In_Every_Language()
    {
        foreach (var entry in Mobile.Entries)
        {
            GalleryDocs.Has(entry.ClassName).ShouldBeTrue($"{entry.ClassName} has no docs page");
            GalleryDocs.HasDemos(entry.ClassName).ShouldBeTrue($"{entry.ClassName} has no demos");
            GalleryDocs.PackageOf(entry.ClassName).ShouldBe("TKCode.Arc.Mobile", entry.ClassName);
            GalleryDocs.FolderOf(entry.ClassName).ShouldBe($"Mobile/{entry.ClassName}");
            foreach (var language in new[] { "en-US", "zh-CN", "zh-TW" })
            {
                var doc = GalleryDocs.Get(entry.ClassName, language).ShouldNotBeNull();
                doc.Language.ShouldBe(language, $"{entry.ClassName} is not translated into {language}");
                doc.IsOutdated.ShouldBeFalse($"{entry.ClassName} ({language}) was translated from an older en-US.md");
                doc.Summary.ShouldNotBeNullOrWhiteSpace();
            }
            GalleryDocs.Summary(entry.ClassName, "en-US").ShouldBe(entry.Description, entry.ClassName);
        }
    }

    [AvaloniaFact]
    public void Api_Tables_Describe_The_Mobile_Types_Not_The_Desktop_Ones_With_The_Same_Name()
    {
        // MobileButton / MobileSwitch are TKCode.Arc.Mobile.Button / Switch; the desktop Button and Switch have their own tables.
        var mobileButton = GalleryDocs.ApiReference("MobileButton", "en-US");
        mobileButton.ShouldContain("### MobileButton");
        mobileButton.ShouldContain("Mobile button: a touch-first button");
        mobileButton.ShouldContain("Derives from `Button`");
        GalleryDocs.ApiReference("Button", "en-US").ShouldNotContain("touch-first");

        var page = GalleryDocs.ApiReference("MobilePage", "en-US");
        page.ShouldContain("| `SafeArea` | `Thickness` |");
        page.ShouldContain("| `UsePlatformSafeArea` | `bool` | `true` |");
        page.ShouldContain("| `Header` | `object?` |");
        GalleryDocs.ApiReference("MobileNavbar", "en-US").ShouldContain("| `Title` | `string?` |");
        GalleryDocs.ApiReference("MobileTabbarItem", "en-US").ShouldContain("| `Icon` | `Geometry?` |");
        // The descriptions are translated through Docs/Api.
        GalleryDocs.ApiReference("MobileNavbar", "zh-CN").ShouldContain("栏的标题。");
    }

    [AvaloniaFact]
    public void Mobile_Pages_Have_Routes_Under_Components()
    {
        foreach (var entry in Mobile.Entries)
        {
            var route = GalleryRoute.To(entry);
            route.Kind.ShouldBe(GalleryRouteKind.Components);
            route.Path.ShouldBe($"/components/{entry.Slug}");
            GalleryRoute.Parse(route.Path)!.Entry.ShouldBe(entry);
        }
    }

    [AvaloniaFact]
    public void Mobile_Demos_Run_Inside_A_Phone_Frame_And_Other_Demos_Do_Not()
    {
        foreach (var entry in Mobile.Entries)
        {
            GalleryDemos.All.Where(d => d.Component == entry.ClassName).ShouldNotBeEmpty(entry.ClassName);
        }
        GalleryDemos.All.Where(d => GalleryDemoView.IsMobileDemo(d)).Select(d => d.Component).Distinct()
            .ShouldBe(Mobile.Entries.Select(e => e.ClassName), ignoreOrder: true);

        var mobile = TestHelpers.Show(new GalleryDemoView("MobilePage.Basic"));
        try
        {
            mobile.GetVisualDescendants().OfType<PhoneFrame>().ShouldHaveSingleItem();
        }
        finally
        {
            mobile.Close();
        }
        var desktop = TestHelpers.Show(new GalleryDemoView("Switch.SettingsList"));
        try
        {
            desktop.GetVisualDescendants().OfType<PhoneFrame>().ShouldBeEmpty();
        }
        finally
        {
            desktop.Close();
        }
    }

    [AvaloniaFact]
    public void Every_Mobile_Demo_Renders_On_Every_Device_In_Both_Orientations()
    {
        foreach (var demo in GalleryDemos.All.Where(GalleryDemoView.IsMobileDemo))
        {
            var view = new GalleryDemoView(demo.Id);
            var window = TestHelpers.Show(view);
            try
            {
                var frame = window.GetVisualDescendants().OfType<PhoneFrame>().ShouldHaveSingleItem(demo.Id);
                foreach (var device in PhoneDevice.Presets)
                {
                    foreach (var landscape in new[] { false, true })
                    {
                        frame.Device = device;
                        frame.IsLandscape = landscape;
                        frame.IsKeyboardVisible = landscape;
                        Dispatcher.UIThread.RunJobs();
                        window.UpdateLayout();
                        frame.GetVisualDescendants().OfType<MobilePage>().ToList().ForEach(p => p.IsLoaded.ShouldBeTrue(demo.Id));
                    }
                }
            }
            finally
            {
                window.Close();
            }
        }
    }

    [AvaloniaFact]
    public void Frame_Starts_As_An_iPhone_15_In_Portrait_With_No_Keyboard()
    {
        var frame = new PhoneFrame();
        frame.Device.ShouldBe(PhoneDevice.IPhone15);
        frame.Device.Name.ShouldBe("iPhone 15");
        frame.ScreenSize.ShouldBe(new Size(390, 844));
        frame.IsLandscape.ShouldBeFalse();
        frame.IsKeyboardVisible.ShouldBeFalse();
        frame.SafeArea.ShouldBe(new Thickness(0, 59, 0, 34));
    }

    [AvaloniaFact]
    public void Frame_Offers_The_Presets_And_Swaps_Width_And_Height_In_Landscape()
    {
        PhoneDevice.Presets.Select(d => (d.Width, d.Height)).ShouldBe([(390d, 844d), (412d, 915d), (360d, 640d), (768d, 1024d)]);
        var frame = new PhoneFrame { Device = PhoneDevice.Pixel };
        frame.ScreenSize.ShouldBe(new Size(412, 915));
        frame.IsLandscape = true;
        frame.ScreenSize.ShouldBe(new Size(915, 412));
        frame.SafeArea.ShouldBe(PhoneDevice.Pixel.LandscapeSafeArea);
        frame.Device = PhoneDevice.Tablet;
        frame.ScreenSize.ShouldBe(new Size(1024, 768));
    }

    [AvaloniaFact]
    public void Frame_Passes_Its_Safe_Area_To_The_Page_And_Follows_The_Device_Orientation_And_Keyboard()
    {
        var page = new MobilePage { Content = new TextBlock { Text = "Body" } };
        var frame = new PhoneFrame { Screen = new UserControl { Content = page } };
        var window = TestHelpers.Show(frame);
        try
        {
            frame.ScreenPage.ShouldBeSameAs(page);
            // The page keeps what the frame gives it and does not ask the platform.
            page.UsePlatformSafeArea.ShouldBeFalse();
            page.SafeArea.ShouldBe(new Thickness(0, 59, 0, 34));

            frame.Device = PhoneDevice.SmallPhone;
            page.SafeArea.ShouldBe(new Thickness(0, 24, 0, 0));

            frame.Device = PhoneDevice.IPhone15;
            frame.IsLandscape = true;
            page.SafeArea.ShouldBe(new Thickness(59, 0, 59, 21));

            // The keyboard covers the home indicator's room and takes its height from the content area.
            frame.IsLandscape = false;
            frame.IsKeyboardVisible = true;
            page.SafeArea.ShouldBe(new Thickness(0, 59, 0, 0));
            frame.ContentSize.ShouldBe(new Size(390, 844 - 291));
            frame.IsKeyboardVisible = false;
            page.SafeArea.ShouldBe(new Thickness(0, 59, 0, 34));
            frame.ContentSize.ShouldBe(new Size(390, 844));
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Frame_Pads_A_Screen_That_Is_Not_A_Page()
    {
        var content = new Border { Child = new TextBlock { Text = "No page" } };
        var frame = new PhoneFrame { Screen = content };
        var window = TestHelpers.Show(frame);
        try
        {
            frame.ScreenPage.ShouldBeNull();
            var host = content.GetVisualParent<Border>().ShouldNotBeNull();
            host.Padding.ShouldBe(new Thickness(0, 59, 0, 34));
            frame.Device = PhoneDevice.Tablet;
            host.Padding.ShouldBe(PhoneDevice.Tablet.PortraitSafeArea);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Frame_Toolbar_Switches_Device_Orientation_And_Keyboard_Both_Ways()
    {
        var frame = new PhoneFrame();
        var window = TestHelpers.Show(frame);
        try
        {
            var segmented = frame.GetVisualDescendants().OfType<GallerySegmented>().ToList();
            segmented.Count.ShouldBe(2);
            segmented[0].SelectedIndex.ShouldBe(0);
            frame.Device = PhoneDevice.Tablet;
            segmented[0].SelectedIndex.ShouldBe(3);
            frame.IsLandscape = true;
            segmented[1].SelectedIndex.ShouldBe(1);
            var keyboard = frame.GetVisualDescendants().OfType<Switch>().ShouldHaveSingleItem();
            frame.IsKeyboardVisible = true;
            keyboard.IsChecked.ShouldBe(true);
            keyboard.IsChecked = false;
            frame.IsKeyboardVisible.ShouldBeFalse();

            frame.ShowToolbar = false;
            segmented[0].GetVisualParent<Visual>()!.IsVisible.ShouldBeFalse();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Shell_Text_Of_The_Mobile_Section_Is_Translated()
    {
        foreach (var kind in new[]
                 {
                     GalleryShellLangResourceKind.Category_Mobile, GalleryShellLangResourceKind.PhoneDevice,
                     GalleryShellLangResourceKind.PhonePortrait, GalleryShellLangResourceKind.PhoneLandscape,
                     GalleryShellLangResourceKind.PhoneKeyboard, GalleryShellLangResourceKind.PhoneSimulationNote
                 })
        {
            GalleryText.Get(kind).ShouldNotBe(kind.ToString());
        }
        GalleryText.TryGetCategory("Mobile", out var section).ShouldBeTrue();
        section.ShouldBe(GalleryShellLangResourceKind.Category_Mobile);
        GalleryText.Get(section).ShouldBe(Mobile.Title);
    }
}
