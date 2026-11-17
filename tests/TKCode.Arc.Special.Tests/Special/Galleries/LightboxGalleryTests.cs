using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.VisualTree;
using TKCode.Arc.Gestures;
using TKCode.Arc.Primitives;

namespace TKCode.Arc.Tests.Special.Photos;

/// <summary>The lightbox gallery: the tile grid, opening and closing, moving between photos, zooming and the flight.</summary>
public class LightboxGalleryTests
{
    private static LightboxGallery Gallery(int count = 6)
    {
        var gallery = new LightboxGallery { MinTileWidth = 80 };
        for (var i = 0; i < count; i++)
        {
            gallery.Items.Add(new LightboxGalleryImage { Source = PhotosTestPictures.Picture(30, 20), Caption = $"Caption {i}" });
        }
        return gallery;
    }

    private static void Settle()
    {
        Dispatcher.UIThread.RunJobs();
        AvaloniaHeadlessPlatform.ForceRenderTimerTick();
        Dispatcher.UIThread.RunJobs();
    }

    [AvaloniaFact]
    public void The_grid_fits_columns_of_equal_tiles_named_after_their_photos()
    {
        var gallery = Gallery(7);
        var window = TestHelpers.Show(gallery);
        try
        {
            gallery.Tiles.Count.ShouldBe(7);
            var grid = TestHelpers.Descendant<LightboxGalleryGrid>(gallery);
            // 400 wide with 80 px tiles and 8 px gaps: four columns.
            grid.ColumnCount.ShouldBe(4);
            gallery.Tiles[0].Bounds.Width.ShouldBe(gallery.Tiles[0].Bounds.Height, 0.5);
            gallery.Tiles[4].Bounds.Top.ShouldBeGreaterThan(gallery.Tiles[0].Bounds.Bottom);
            AutomationProperties.GetName(gallery.Tiles[2]).ShouldBe("Photo 3 of 7: Caption 2");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Arrow_keys_move_between_tiles_by_row_and_column()
    {
        var gallery = Gallery(8);
        var window = TestHelpers.Show(gallery);
        try
        {
            gallery.Tiles[1].Focus(NavigationMethod.Tab);
            window.KeyPressQwerty(PhysicalKey.ArrowDown, RawInputModifiers.None);
            gallery.Tiles[5].IsFocused.ShouldBeTrue();
            window.KeyPressQwerty(PhysicalKey.ArrowLeft, RawInputModifiers.None);
            gallery.Tiles[4].IsFocused.ShouldBeTrue();
            gallery.Tiles.Count(t => t.IsTabStop).ShouldBe(1);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Opening_a_tile_shows_its_photo_in_a_dialog_with_the_caption()
    {
        var gallery = Gallery();
        var window = TestHelpers.Show(gallery);
        try
        {
            gallery.Tiles[2].Focus(NavigationMethod.Tab);
            window.KeyPressQwerty(PhysicalKey.Enter, RawInputModifiers.None);
            Settle();
            gallery.OpenIndex.ShouldBe(2);
            gallery.IsOpen.ShouldBeTrue();
            var view = gallery.View.ShouldNotBeNull();
            TopLevel.GetTopLevel(view).ShouldBeSameAs(window);
            gallery.Presence!.IsOpen.ShouldBeTrue();
            view.Caption!.Text.ShouldBe("Caption 2");
            view.Counter!.Text.ShouldBe("3 / 6");
            AutomationProperties.GetName(view.Viewport!).ShouldBe("Photo 3 of 6: Caption 2");
            view.Viewport!.Current.Picture.ShouldNotBeNull();
            view.Viewport.IsFocused.ShouldBeTrue();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Keys_move_between_photos_and_Escape_returns_to_the_tile_of_the_photo_on_show()
    {
        var gallery = Gallery();
        var window = TestHelpers.Show(gallery);
        try
        {
            gallery.Open(1);
            Settle();
            window.KeyPressQwerty(PhysicalKey.ArrowRight, RawInputModifiers.None);
            gallery.OpenIndex.ShouldBe(2);
            window.KeyPressQwerty(PhysicalKey.End, RawInputModifiers.None);
            gallery.OpenIndex.ShouldBe(5);
            gallery.View!.NextButton!.IsEnabled.ShouldBeFalse();
            window.KeyPressQwerty(PhysicalKey.Escape, RawInputModifiers.None);
            Settle();
            gallery.OpenIndex.ShouldBe(-1);
            gallery.IsOpen.ShouldBeFalse();
            gallery.Tiles[5].IsFocused.ShouldBeTrue();
            TestHelpers.PumpUntil(() => TopLevel.GetTopLevel(gallery.View!) is null, 2000).ShouldBeTrue();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Plus_zooms_in_about_the_middle_and_zero_shows_the_whole_photo()
    {
        var gallery = Gallery();
        var window = TestHelpers.Show(gallery);
        try
        {
            gallery.Open(0);
            Settle();
            var viewport = gallery.View!.Viewport!;
            window.KeyPressQwerty(PhysicalKey.Equal, RawInputModifiers.None);
            gallery.IsZoomed.ShouldBeTrue();
            viewport.View.Zoom.ShouldBe(1.6, 1e-6);
            // The middle of the viewport stays put.
            var middle = new Point(viewport.Bounds.Width / 2, viewport.Bounds.Height / 2);
            viewport.View.WorldToScreen(middle).X.ShouldBe(middle.X, 1e-6);
            gallery.View.ZoomOutButton!.IsEnabled.ShouldBeTrue();
            // Arrow keys pan a zoomed photo instead of moving to the next one.
            window.KeyPressQwerty(PhysicalKey.ArrowRight, RawInputModifiers.None);
            gallery.OpenIndex.ShouldBe(0);
            window.KeyPressQwerty(PhysicalKey.Digit0, RawInputModifiers.None);
            gallery.IsZoomed.ShouldBeFalse();
            viewport.View.ShouldBe(PanZoomTransform.Identity);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void The_zoom_stops_at_the_maximum_and_a_zoomed_photo_stays_on_screen()
    {
        var gallery = Gallery();
        gallery.MaxZoom = 3;
        var window = TestHelpers.Show(gallery);
        try
        {
            gallery.Open(0);
            Settle();
            var viewport = gallery.View!.Viewport!;
            viewport.ZoomBy(10, new Point(0, 0), animate: false);
            viewport.View.Zoom.ShouldBe(3, 1e-9);
            var photo = viewport.Current.Bounds;
            var screen = viewport.View.WorldToScreen(photo);
            // A photo larger than the viewport covers it; one smaller is centred.
            if (screen.Width >= viewport.Bounds.Width)
            {
                screen.Left.ShouldBeLessThanOrEqualTo(0.001);
                screen.Right.ShouldBeGreaterThanOrEqualTo(viewport.Bounds.Width - 0.001);
            }
            else
            {
                screen.Center.X.ShouldBe(viewport.Bounds.Width / 2, 0.001);
            }
            gallery.View.ZoomInButton!.IsEnabled.ShouldBeFalse();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void The_close_button_closes_and_OpenIndex_can_open_and_close_too()
    {
        var gallery = Gallery();
        var window = TestHelpers.Show(gallery);
        try
        {
            gallery.OpenIndex = 3;
            Settle();
            gallery.IsOpen.ShouldBeTrue();
            gallery.View!.CloseButton!.Focus(NavigationMethod.Tab);
            window.KeyPressQwerty(PhysicalKey.Enter, RawInputModifiers.None);
            Settle();
            gallery.OpenIndex.ShouldBe(-1);
            gallery.OpenIndex = 0;
            Settle();
            gallery.IsOpen.ShouldBeTrue();
            gallery.OpenIndex = -1;
            Settle();
            gallery.IsOpen.ShouldBeFalse();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Under_full_motion_the_photo_flies_out_of_its_tile_and_the_tile_is_hidden()
    {
        var gallery = Gallery();
        var window = TestHelpers.Show(gallery, Motion.ArcMotionLevel.Full);
        try
        {
            gallery.Open(1);
            Dispatcher.UIThread.RunJobs();
            var flight = gallery.Flight.ShouldNotBeNull();
            flight.Source.ShouldBeSameAs(gallery.Tiles[1]);
            flight.IsSourceHidden.ShouldBeTrue();
            flight.PairedKeys.ShouldBe([LightboxGallery.FlightKey]);
            // Moving to another photo hides that photo's tile instead.
            gallery.ShowNext();
            Dispatcher.UIThread.RunJobs();
            gallery.Flight!.Source.ShouldBeSameAs(gallery.Tiles[2]);
            gallery.Tiles[1].Face!.Opacity.ShouldBe(1);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Removing_the_open_photo_moves_to_the_last_one()
    {
        var gallery = Gallery(3);
        var window = TestHelpers.Show(gallery);
        try
        {
            gallery.Open(2);
            Settle();
            gallery.Items.RemoveAt(2);
            Settle();
            gallery.OpenIndex.ShouldBe(1);
            gallery.View!.Counter!.Text.ShouldBe("2 / 2");
        }
        finally
        {
            window.Close();
        }
    }
}
