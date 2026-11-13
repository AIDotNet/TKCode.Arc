using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.VisualTree;

namespace TKCode.Arc.Tests.Special.Photos;

/// <summary>The product gallery: thumbnails, selection, buttons, keyboard, the hover zoom and the slide between pictures.</summary>
public class ProductGalleryTests
{
    private static ProductGallery Gallery(int count = 4)
    {
        var gallery = new ProductGallery { Width = 200 };
        for (var i = 0; i < count; i++)
        {
            gallery.Items.Add(new ProductGalleryImage { Source = PhotosTestPictures.Picture(), Description = $"View {i}" });
        }
        return gallery;
    }

    [AvaloniaFact]
    public void The_first_picture_is_on_the_stage_with_a_thumbnail_for_each()
    {
        var gallery = Gallery();
        var window = TestHelpers.Show(gallery);
        try
        {
            gallery.SelectedIndex.ShouldBe(0);
            gallery.SelectedImage!.Description.ShouldBe("View 0");
            gallery.Thumbs.Count.ShouldBe(4);
            gallery.Thumbs[0].IsSelected.ShouldBeTrue();
            AutomationProperties.GetName(gallery.Thumbs[2]).ShouldBe("Image 3 of 4: View 2");
            AutomationProperties.GetHelpText(gallery).ShouldBe("Image 1 of 4: View 0");
            gallery.Stage!.Current.Source.ShouldBeSameAs(gallery.Items[0].Source);
            // The stage is square by default.
            gallery.Stage.Bounds.Width.ShouldBe(gallery.Stage.Bounds.Height, 0.5);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Thumbnails_and_buttons_change_the_picture_and_the_ends_are_disabled()
    {
        var gallery = Gallery(3);
        var window = TestHelpers.Show(gallery);
        try
        {
            var previous = TestHelpers.Descendant<Button>(gallery, ProductGallery.PreviousPart);
            var next = TestHelpers.Descendant<Button>(gallery, ProductGallery.NextPart);
            previous.IsEnabled.ShouldBeFalse();
            next.IsEnabled.ShouldBeTrue();
            gallery.Thumbs[2].Focus(NavigationMethod.Tab);
            window.KeyPressQwerty(PhysicalKey.Enter, RawInputModifiers.None);
            gallery.SelectedIndex.ShouldBe(2);
            next.IsEnabled.ShouldBeFalse();
            gallery.SelectNext().ShouldBeFalse();
            gallery.SelectPrevious().ShouldBeTrue();
            gallery.SelectedIndex.ShouldBe(1);
            gallery.Stage!.Current.Source.ShouldBeSameAs(gallery.Items[1].Source);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Arrow_keys_move_on_the_stage_and_along_the_strip()
    {
        var gallery = Gallery(5);
        var window = TestHelpers.Show(gallery);
        try
        {
            gallery.Focus(NavigationMethod.Tab);
            window.KeyPressQwerty(PhysicalKey.ArrowRight, RawInputModifiers.None);
            gallery.SelectedIndex.ShouldBe(1);
            window.KeyPressQwerty(PhysicalKey.End, RawInputModifiers.None);
            gallery.SelectedIndex.ShouldBe(4);
            window.KeyPressQwerty(PhysicalKey.Home, RawInputModifiers.None);
            gallery.SelectedIndex.ShouldBe(0);
            // In the strip the arrow keys move focus with the selection; only the selected thumbnail is a tab stop.
            gallery.Thumbs[0].Focus(NavigationMethod.Tab);
            window.KeyPressQwerty(PhysicalKey.ArrowRight, RawInputModifiers.None);
            gallery.SelectedIndex.ShouldBe(1);
            gallery.Thumbs[1].IsFocused.ShouldBeTrue();
            gallery.Thumbs.Count(t => t.IsTabStop).ShouldBe(1);
            gallery.Thumbs[1].IsTabStop.ShouldBeTrue();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void A_strip_on_the_left_uses_up_and_down()
    {
        var gallery = Gallery(3);
        gallery.ThumbnailPlacement = ProductGalleryThumbnailPlacement.Left;
        var window = TestHelpers.Show(gallery);
        try
        {
            gallery.Classes.Contains(ProductGallery.ThumbsLeftPseudoClass).ShouldBeTrue();
            gallery.Thumbs[0].Focus(NavigationMethod.Tab);
            window.KeyPressQwerty(PhysicalKey.ArrowDown, RawInputModifiers.None);
            gallery.SelectedIndex.ShouldBe(1);
            // The thumbnails stand above one another.
            gallery.Thumbs[1].Bounds.Top.ShouldBeGreaterThan(gallery.Thumbs[0].Bounds.Top);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void A_mouse_over_the_picture_zooms_it_toward_the_pointer_and_leaving_lets_it_go()
    {
        var gallery = Gallery();
        gallery.ZoomScale = 2.5;
        var window = TestHelpers.Show(gallery);
        try
        {
            var stage = gallery.Stage!;
            var point = stage.TranslatePoint(new Point(stage.Bounds.Width * 0.25, stage.Bounds.Height * 0.75), window)!.Value;
            window.MouseMove(point, RawInputModifiers.None);
            gallery.IsZoomed.ShouldBeTrue();
            stage.Zoom.ShouldBe(2.5, 1e-9);
            gallery.Classes.Contains(ProductGallery.ZoomedPseudoClass).ShouldBeTrue();
            // The point of the picture under the pointer's place stays put.
            var image = stage.Current;
            var focus = new Point(image.Bounds.Width * 0.25, image.Bounds.Height * 0.75);
            var drawn = focus.Transform(image.RenderTransform!.Value);
            drawn.X.ShouldBe(focus.X, 1e-6);
            drawn.Y.ShouldBe(focus.Y, 1e-6);
            window.MouseMove(new Point(-5, -5), RawInputModifiers.None);
            window.MouseMove(new Point(395, 295), RawInputModifiers.None);
            gallery.IsZoomed.ShouldBeFalse();
            stage.Zoom.ShouldBe(1, 1e-9);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Turning_the_hover_zoom_off_keeps_the_picture_at_rest()
    {
        var gallery = Gallery();
        gallery.IsHoverZoomEnabled = false;
        var window = TestHelpers.Show(gallery);
        try
        {
            var stage = gallery.Stage!;
            window.MouseMove(stage.TranslatePoint(new Point(50, 50), window)!.Value, RawInputModifiers.None);
            gallery.IsZoomed.ShouldBeFalse();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Under_full_motion_the_new_picture_slides_in_from_its_side()
    {
        var gallery = Gallery();
        var window = TestHelpers.Show(gallery, Motion.ArcMotionLevel.Full);
        try
        {
            var stage = gallery.Stage!;
            gallery.SelectedIndex = 2;
            // Next: the new picture starts one stage to the right, with the old one beside it on the left.
            stage.Offset.ShouldBe(stage.Bounds.Width + stage.Gap, 1);
            stage.Current.Source.ShouldBeSameAs(gallery.Items[2].Source);
            var left = stage.GetVisualChildren().OfType<Primitives.ArcImage>().First();
            left.Source.ShouldBeSameAs(gallery.Items[0].Source);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void New_items_keep_the_index_in_range_and_one_picture_hides_the_strip()
    {
        var gallery = Gallery(4);
        var window = TestHelpers.Show(gallery);
        try
        {
            gallery.SelectedIndex = 3;
            gallery.ItemsSource = new[] { PhotosTestPictures.Picture(), PhotosTestPictures.Picture() };
            Dispatcher.UIThread.RunJobs();
            gallery.Count.ShouldBe(2);
            gallery.SelectedIndex.ShouldBe(1);
            gallery.Thumbs.Count.ShouldBe(2);
            gallery.ItemsSource = new[] { PhotosTestPictures.Picture() };
            gallery.Classes.Contains(ProductGallery.SinglePseudoClass).ShouldBeTrue();
            TestHelpers.Descendant<ScrollViewer>(gallery, ProductGallery.ThumbScrollerPart).IsVisible.ShouldBeFalse();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void SelectionChanged_Reports_The_Old_And_New_Index()
    {
        var gallery = Gallery();
        var window = TestHelpers.Show(gallery);
        try
        {
            var changes = new List<PhotosSelectionChangedEventArgs>();
            gallery.SelectionChanged += (_, e) => changes.Add(e);
            gallery.SelectedIndex = 2;
            changes.Count.ShouldBe(1);
            changes[0].OldIndex.ShouldBe(0);
            changes[0].NewIndex.ShouldBe(2);
            gallery.SelectNext();
            changes.Count.ShouldBe(2);
            changes[1].NewIndex.ShouldBe(3);
            gallery.SelectedIndex = 3;
            changes.Count.ShouldBe(2);
        }
        finally
        {
            window.Close();
        }
    }
}
