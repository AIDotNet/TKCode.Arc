using TKCode.Arc.Gallery.Demos;
using TKCode.Arc.Motion;
using Avalonia;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using Shouldly;
using Xunit;

namespace TKCode.Arc.Tests.Blocks.Showpieces;

/// <summary>
/// <see cref="LayoutMorph"/>'s rendering cost guards: pictures are drawn from copies at the size they are shown
/// (<see cref="LayoutMorphPictures"/>), and on a GPU compositor moving cards whose drawing does not change are drawn from a
/// bitmap cache (forced on here: the headless renderer is software, where the cards stay vectors).
/// </summary>
public class LayoutMorphRenderingTests
{
    private static LayoutMorph Create()
    {
        var morph = new LayoutMorph { Title = "Studio", DefaultPanelTitle = "Shelf" };
        var items = LayoutMorphDemoData.CreateItems();
        morph.Items.AddRange(items);
        morph.DefaultStats.AddRange(LayoutMorphDemoData.CreateTotals(items));
        return morph;
    }

    private static (Window Window, LayoutMorph Morph) Show(LayoutMorph? morph = null, double scaling = 2,
        ArcMotionLevel level = ArcMotionLevel.Off, double width = 1200)
    {
        morph ??= Create();
        var window = TestHelpers.Show(morph, level);
        window.Width = width;
        window.Height = 1100;
        window.SetRenderScaling(scaling);
        Pump();
        return (window, morph);
    }

    private static void Pump(int n = 4)
    {
        for (var i = 0; i < n; i++)
        {
            Dispatcher.UIThread.RunJobs();
            AvaloniaHeadlessPlatform.ForceRenderTimerTick();
        }
        Dispatcher.UIThread.RunJobs();
    }

    private static void RunFrames(ManualFrameClock clock, int count)
    {
        for (var i = 0; i < count; i++)
        {
            clock.Advance(TimeSpan.FromMilliseconds(16));
            Dispatcher.UIThread.RunJobs();
        }
    }

    private static Point WindowPoint(LayoutMorph morph, Point stagePoint) =>
        morph.Stage.TranslatePoint(stagePoint, TopLevel.GetTopLevel(morph)!)!.Value;

    private static Bitmap? PictureOf(LayoutMorphStage stage, int index) => stage.CardAt(index).Item.Image as Bitmap;

    /// <summary>Device px per picture pixel where card <paramref name="index"/> draws its picture now.</summary>
    private static double Density(LayoutMorphStage stage, int index, double scaling)
    {
        var card = stage.CardAt(index);
        return scaling * LayoutMorphStage.PictureDensity(card.ScreenMatrix, card.Media, PictureOf(stage, index)!.PixelSize);
    }

    private static int LevelOf(LayoutMorphStage stage, int index)
    {
        var picture = PictureOf(stage, index)!;
        var copy = stage.Pictures.CopyOf(picture);
        if (copy is null)
        {
            return 0;
        }
        for (var level = 0; level <= 30; level++)
        {
            if (LayoutMorphPictures.CopySize(picture.PixelSize, level) == copy.PixelSize)
            {
                return level;
            }
        }
        return -1;
    }

    private static List<int> PictureCards(LayoutMorphStage stage) =>
        Enumerable.Range(0, stage.Count).Where(i => PictureOf(stage, i) is not null).ToList();

    // Picture copies ---------------------------------------------------------------------------------------------------------

    [Theory]
    [InlineData(2, 0)]
    [InlineData(1, 0)]
    [InlineData(0.81, 0)]
    [InlineData(0.8, 1)]
    [InlineData(0.64, 2)]
    [InlineData(0.5, 3)]
    [InlineData(0.356, 4)]
    [InlineData(0.0001, 30)]
    public void A_copy_is_the_smallest_step_still_as_dense_as_needed(double density, int level)
    {
        LayoutMorphPictures.LevelFor(density).ShouldBe(level);
        Math.Pow(LayoutMorphPictures.Step, -level).ShouldBeGreaterThanOrEqualTo(Math.Min(1, density) - 1e-12);
    }

    [Fact]
    public void Copy_sizes_round_up_and_never_vanish()
    {
        LayoutMorphPictures.CopySize(new PixelSize(640, 800), 0).ShouldBe(new PixelSize(640, 800));
        LayoutMorphPictures.CopySize(new PixelSize(640, 800), 1).ShouldBe(new PixelSize(512, 640));
        LayoutMorphPictures.CopySize(new PixelSize(640, 800), 4).ShouldBe(new PixelSize(263, 328));
        LayoutMorphPictures.CopySize(new PixelSize(640, 800), 30).ShouldBe(new PixelSize(1, 1));
    }

    [AvaloniaFact]
    public void Grid_pictures_are_drawn_from_copies_at_their_screen_size_and_fit_a_small_budget()
    {
        var (window, morph) = Show();
        try
        {
            var stage = morph.Stage;
            var cards = PictureCards(stage);
            cards.Count.ShouldBe(15);
            foreach (var i in cards)
            {
                var picture = PictureOf(stage, i)!;
                var copy = stage.Pictures.CopyOf(picture).ShouldNotBeNull($"card {i}");
                stage.Pictures.Picture(picture).ShouldBeSameAs(copy);
                stage.CardAt(i).UsesOriginalPicture.ShouldBeFalse();
                // Dense enough for the screen (2x), and the tightest step that is.
                var density = Density(stage, i, 2);
                var level = LevelOf(stage, i);
                level.ShouldBe(LayoutMorphPictures.LevelFor(density), $"card {i}");
                (copy.PixelSize.Width / (double)picture.PixelSize.Width).ShouldBeGreaterThanOrEqualTo(density - 1e-9);
                (copy.PixelSize.Width / (double)picture.PixelSize.Width / LayoutMorphPictures.Step).ShouldBeLessThan(density);
            }
            // Fifteen 640 × 800 pictures are 30.7 MB as textures; their grid copies a sixth of that.
            stage.Pictures.Bytes.ShouldBeLessThan(8L << 20);
            stage.Pictures.Bytes.ShouldBeGreaterThan(2L << 20);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Copies_are_reused_from_frame_to_frame_without_allocating()
    {
        var (window, morph) = Show();
        try
        {
            var stage = morph.Stage;
            var builds = stage.Pictures.Builds;
            var copies = PictureCards(stage).Select(i => stage.Pictures.CopyOf(PictureOf(stage, i)!)).ToList();
            for (var i = 0; i < 20; i++)
            {
                stage.ApplyVisuals();
            }
            const int frames = 200;
            var before = GC.GetAllocatedBytesForCurrentThread();
            for (var i = 0; i < frames; i++)
            {
                stage.ApplyVisuals();
            }
            var perFrame = (GC.GetAllocatedBytesForCurrentThread() - before) / (double)frames;
            perFrame.ShouldBeLessThan(256);
            stage.Pictures.Builds.ShouldBe(builds);
            PictureCards(stage).Select(i => stage.Pictures.CopyOf(PictureOf(stage, i)!)).ShouldBe(copies);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void A_larger_layout_grows_the_copies_and_the_grid_shrinks_them_back_at_rest()
    {
        var (window, morph) = Show();
        try
        {
            var stage = morph.Stage;
            var grid = LevelOf(stage, 2);
            grid.ShouldBeGreaterThan(1);
            morph.Layout = LayoutMorphMode.Carousel;
            Pump();
            // The front card is drawn about twice as large: a bigger copy, as dense as needed.
            var carousel = LevelOf(stage, 2);
            carousel.ShouldBeLessThan(grid);
            carousel.ShouldBe(LayoutMorphPictures.LevelFor(Density(stage, 2, 2)));
            // Without motion the stage is at rest at once: the copies shrink back.
            morph.Layout = LayoutMorphMode.Grid;
            Pump();
            LevelOf(stage, 2).ShouldBe(grid);
            // List thumbnails are tiny.
            morph.Layout = LayoutMorphMode.List;
            Pump();
            LevelOf(stage, 2).ShouldBeGreaterThan(grid);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void With_full_motion_copies_grow_a_few_per_frame_and_shrink_only_after_the_hold()
    {
        var clock = new ManualFrameClock();
        using var installed = clock.Install();
        // The Gallery preview's width: the carousel draws its pictures at 0.71 of their pixels.
        var (window, morph) = Show(level: ArcMotionLevel.Full, width: 817);
        try
        {
            var stage = morph.Stage;
            clock.Advance(TimeSpan.FromSeconds(2));
            Pump();
            var grid = LevelOf(stage, 2);
            morph.Layout = LayoutMorphMode.Carousel;
            var builds = stage.Pictures.Builds;
            var frames = 0;
            while (stage.IsMoving && frames++ < 200)
            {
                RunFrames(clock, 1);
                if (stage.IsMoving)
                {
                    (stage.Pictures.Builds - builds).ShouldBeLessThanOrEqualTo(LayoutMorphPictures.BuildsPerFrame);
                }
                builds = stage.Pictures.Builds;
            }
            stage.IsMoving.ShouldBeFalse();
            RunFrames(clock, 2);
            var carousel = LevelOf(stage, 2);
            carousel.ShouldBe(LayoutMorphPictures.LevelFor(Density(stage, 2, 2)));
            (carousel, grid).ShouldBe((1, 4));

            // Back to the grid: the copies keep their size while the cards fly and for the hold after they land.
            morph.Layout = LayoutMorphMode.Grid;
            frames = 0;
            while (stage.IsMoving && frames++ < 200)
            {
                RunFrames(clock, 1);
            }
            RunFrames(clock, 2);
            LevelOf(stage, 2).ShouldBe(carousel);
            RunFrames(clock, (int)Math.Ceiling(ShowpieceRenderer.CacheHold.TotalMilliseconds / 16) + 2);
            LevelOf(stage, 2).ShouldBe(grid);
            // Idle afterwards: no frames.
            var requests = clock.RequestCount;
            RunFrames(clock, 30);
            clock.RequestCount.ShouldBe(requests);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Copies_go_with_the_stage_and_with_their_pictures()
    {
        var (window, morph) = Show();
        var layers = (Avalonia.Controls.Primitives.VisualLayerManager)window.Content!;
        try
        {
            var stage = morph.Stage;
            stage.Pictures.Count.ShouldBe(15);
            layers.Child = null;
            Pump();
            stage.Pictures.Count.ShouldBe(0);
            layers.Child = morph;
            Pump();
            stage.Pictures.Count.ShouldBe(15);
            var first = PictureCards(stage)[0];
            stage.Pictures.CopyOf(PictureOf(stage, first)!).ShouldNotBeNull();

            // A new picture replaces the old one's copy.
            var item = stage.CardAt(first).Item;
            var old = (Bitmap)item.Image!;
            var replacement = new RenderTargetBitmap(new PixelSize(400, 500));
            item.Image = replacement;
            Pump();
            stage.Pictures.CopyOf(old).ShouldBeNull();
            stage.Pictures.CopyOf(replacement).ShouldNotBeNull();
            stage.Pictures.Count.ShouldBe(15);

            // A picture smaller than it is drawn is drawn as it is; a drawing is never copied.
            var small = new RenderTargetBitmap(new PixelSize(32, 32));
            item.Image = small;
            Pump();
            stage.Pictures.CopyOf(small).ShouldBeNull();
            stage.Pictures.Picture(small).ShouldBeSameAs(small);
            var drawing = new DrawingImage(new GeometryDrawing { Brush = Brushes.Red, Geometry = new RectangleGeometry(new Rect(0, 0, 640, 800)) });
            item.Image = drawing;
            Pump();
            stage.Pictures.Picture(drawing).ShouldBeSameAs(drawing);
            stage.Pictures.Count.ShouldBe(14);

            // Removed items take their copies with them.
            morph.Items.RemoveAt(1);
            Pump();
            stage.Pictures.Count.ShouldBe(PictureCards(stage).Count(i => PictureOf(stage, i)!.PixelSize.Width > 32));
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void The_open_detail_draws_the_original_picture()
    {
        var (window, morph) = Show();
        try
        {
            var stage = morph.Stage;
            morph.Open(stage.CardAt(0).Item);
            Pump();
            stage.CardAt(0).UsesOriginalPicture.ShouldBeTrue();
            stage.CardAt(1).UsesOriginalPicture.ShouldBeFalse();
            morph.Close();
            Pump();
            stage.CardAt(0).UsesOriginalPicture.ShouldBeFalse();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void A_new_screen_scaling_makes_the_copies_again()
    {
        var (window, morph) = Show(scaling: 1);
        try
        {
            var stage = morph.Stage;
            var at1 = stage.Pictures.CopyOf(PictureOf(stage, 2)!)!.PixelSize;
            window.SetRenderScaling(2);
            Pump();
            var at2 = stage.Pictures.CopyOf(PictureOf(stage, 2)!)!.PixelSize;
            at2.Width.ShouldBeGreaterThan(at1.Width);
            LevelOf(stage, 2).ShouldBe(LayoutMorphPictures.LevelFor(Density(stage, 2, 2)));
        }
        finally
        {
            window.Close();
        }
    }

    // Card bitmaps -------------------------------------------------------------------------------------------------------------

    [AvaloniaFact]
    public void The_software_renderer_keeps_moving_cards_as_vectors()
    {
        var clock = new ManualFrameClock();
        using var installed = clock.Install();
        var morph = Create();
        morph.Layout = LayoutMorphMode.Carousel;
        var (window, _) = Show(morph, level: ArcMotionLevel.Full);
        try
        {
            var stage = morph.Stage;
            clock.Advance(TimeSpan.FromSeconds(2));
            Pump();
            stage.AllowCardCache.ShouldBeFalse();
            morph.FocusItem(morph.EffectiveItems[6]);
            RunFrames(clock, 10);
            stage.IsMoving.ShouldBeTrue();
            Enumerable.Range(0, stage.Count).ShouldAllBe(i => !stage.IsCardCached(i) && stage.CardAt(i).CacheMode == null);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Turning_the_carousel_draws_the_moving_cards_from_bitmaps_and_rests_as_vectors()
    {
        // Performance guard: under a perspective matrix a card's shadow, rounded clips, text and border take the GPU's
        // slow paths every frame; a card that only moves is one textured quad. Matrix, opacity and draw order stay on the
        // card (outside the bitmap); the drawing is the card's child, so a change of it renders the bitmap again.
        var clock = new ManualFrameClock();
        using var installed = clock.Install();
        var morph = Create();
        morph.Layout = LayoutMorphMode.Carousel;
        var (window, _) = Show(morph, level: ArcMotionLevel.Full);
        try
        {
            var stage = morph.Stage;
            clock.Advance(TimeSpan.FromSeconds(2));
            Pump();
            stage.CardCacheOverride = true;
            stage.AllowCardCache.ShouldBeTrue();
            Enumerable.Range(0, stage.Count).ShouldAllBe(i => !stage.IsCardCached(i));

            morph.FocusItem(morph.EffectiveItems[6]);
            RunFrames(clock, 4);
            stage.IsMoving.ShouldBeTrue();
            var visible = Enumerable.Range(0, stage.Count).Where(i => stage.CardAt(i).Opacity > 0.003).ToList();
            visible.Count.ShouldBeGreaterThan(3);
            foreach (var i in visible.Where(i => i != 6 && i != 2))
            {
                stage.IsCardCached(i).ShouldBeTrue($"card {i}");
                var card = stage.CardAt(i);
                card.CacheMode.ShouldBeSameAs(stage.CardCache);
                card.Face.CacheMode.ShouldBeNull();
                card.RenderTransform.ShouldBeOfType<MatrixTransform>().Matrix.ShouldBe(card.ScreenMatrix);
            }
            // The bitmap is rendered at least as large as the card is shown.
            stage.CardCache.RenderAtScale.ShouldBeGreaterThanOrEqualTo(
                visible.Max(i => ShowpieceRenderer.Magnification(stage.CardAt(i).ScreenMatrix,
                    new Rect(0, 0, stage.CardAt(i).DrawWidth, stage.CardAt(i).DrawHeight))) - 1e-9);

            var frames = 0;
            while (stage.IsMoving && frames++ < 200)
            {
                RunFrames(clock, 1);
            }
            // At rest the bitmaps are kept for the hold, then every card is vectors again.
            stage.IsCacheMoving.ShouldBeTrue();
            visible.Where(i => i != 6 && i != 2).ShouldAllBe(i => stage.IsCardCached(i));
            RunFrames(clock, (int)Math.Ceiling(ShowpieceRenderer.CacheHold.TotalMilliseconds / 16) + 2);
            stage.IsCacheMoving.ShouldBeFalse();
            Enumerable.Range(0, stage.Count).ShouldAllBe(i => !stage.IsCardCached(i) && stage.CardAt(i).CacheMode == null);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Dragging_the_carousel_caches_the_cards_without_waiting_for_frames()
    {
        var clock = new ManualFrameClock();
        using var installed = clock.Install();
        var morph = Create();
        morph.Layout = LayoutMorphMode.Carousel;
        var (window, _) = Show(morph, level: ArcMotionLevel.Full);
        try
        {
            var stage = morph.Stage;
            clock.Advance(TimeSpan.FromSeconds(2));
            Pump();
            stage.CardCacheOverride = true;
            var start = WindowPoint(morph, new Point(stage.Bounds.Width / 2, 120));
            window.MouseDown(start, MouseButton.Left);
            for (var i = 1; i <= 8; i++)
            {
                window.MouseMove(start - new Vector(12 * i, 0), RawInputModifiers.LeftMouseButton);
                Dispatcher.UIThread.RunJobs();
            }
            stage.IsDragging.ShouldBeTrue();
            stage.IsCardCached(3).ShouldBeTrue();
            stage.IsCardCached(4).ShouldBeTrue();
            window.MouseUp(start - new Vector(96, 0), MouseButton.Left);
            RunFrames(clock, 200);
            Enumerable.Range(0, stage.Count).ShouldAllBe(i => !stage.IsCardCached(i));
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Cards_whose_drawing_changes_every_frame_stay_vectors()
    {
        var clock = new ManualFrameClock();
        using var installed = clock.Install();
        var (window, morph) = Show(level: ArcMotionLevel.Full);
        try
        {
            var stage = morph.Stage;
            clock.Advance(TimeSpan.FromSeconds(2));
            Pump();
            stage.CardCacheOverride = true;
            // A flight morphs every card's size: rendering a bitmap per frame would cost more than the vectors.
            morph.Layout = LayoutMorphMode.Carousel;
            var widths = Enumerable.Range(0, stage.Count).Select(i => stage.CardAt(i).DrawHeight).ToArray();
            var morphing = 0;
            for (var f = 0; f < 40; f++)
            {
                RunFrames(clock, 1);
                for (var i = 0; i < stage.Count; i++)
                {
                    var height = stage.CardAt(i).DrawHeight;
                    if (Math.Abs(height - widths[i]) > 1e-6 && f > 0)
                    {
                        morphing++;
                        stage.IsCardCached(i).ShouldBeFalse($"card {i} frame {f}");
                    }
                    widths[i] = height;
                }
            }
            morphing.ShouldBeGreaterThan(100);

            // A lift (shadow) changes the drawing too: the pointed card stays vectors; resting cards are never cached.
            RunFrames(clock, 120);
            window.MouseMove(WindowPoint(morph, stage.CardAt(2).ScreenCenter));
            for (var f = 0; f < 10; f++)
            {
                RunFrames(clock, 1);
                Enumerable.Range(0, stage.Count).ShouldAllBe(i => !stage.IsCardCached(i));
            }
            stage.HoverIndex.ShouldBe(2);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void The_helix_keeps_its_turning_cards_as_vectors()
    {
        // Measured: in the helix the bitmaps gained under 10% and cost more on the slowest frames (cards keep turning their
        // backs and the focused card lifts, so bitmaps are rendered again every frame).
        var clock = new ManualFrameClock();
        using var installed = clock.Install();
        var morph = Create();
        morph.Layout = LayoutMorphMode.Helix;
        var (window, _) = Show(morph, level: ArcMotionLevel.Full);
        try
        {
            var stage = morph.Stage;
            clock.Advance(TimeSpan.FromSeconds(2));
            Pump();
            stage.CardCacheOverride = true;
            LayoutMorphStage.CachesCards(LayoutMorphMode.Helix).ShouldBeFalse();
            morph.FocusItem(morph.EffectiveItems[16]);
            for (var f = 0; f < 20; f++)
            {
                RunFrames(clock, 1);
                stage.IsMoving.ShouldBeTrue();
                Enumerable.Range(0, stage.Count).ShouldAllBe(i => !stage.IsCardCached(i));
            }
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void A_cached_card_renders_its_bitmap_again_when_its_drawing_changes()
    {
        var clock = new ManualFrameClock();
        using var installed = clock.Install();
        var morph = Create();
        morph.Layout = LayoutMorphMode.Carousel;
        var (window, _) = Show(morph, level: ArcMotionLevel.Full);
        try
        {
            var stage = morph.Stage;
            clock.Advance(TimeSpan.FromSeconds(2));
            Pump();
            stage.CardCacheOverride = true;
            morph.FocusItem(morph.EffectiveItems[6]);
            RunFrames(clock, 4);
            stage.IsCardCached(4).ShouldBeTrue();
            void Frame()
            {
                // A frame with the compositor's commit, which records the faces that were invalidated.
                RunFrames(clock, 1);
                AvaloniaHeadlessPlatform.ForceRenderTimerTick();
                Dispatcher.UIThread.RunJobs();
            }

            Frame();
            var face = stage.CardAt(4).Face;
            var renders = face.RenderCount;
            Frame();
            Frame();
            // Moving alone never draws the card again: the bitmap goes through the matrix.
            face.RenderCount.ShouldBe(renders);
            stage.CardAt(4).Item.Title = "Renamed while turning";
            Frame();
            face.RenderCount.ShouldBe(renders + 1);
            stage.IsCardCached(4).ShouldBeTrue();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Leaving_the_window_mid_turn_drops_the_bitmaps_and_the_copies()
    {
        var clock = new ManualFrameClock();
        using var installed = clock.Install();
        var morph = Create();
        morph.Layout = LayoutMorphMode.Carousel;
        var (window, _) = Show(morph, level: ArcMotionLevel.Full);
        var layers = (Avalonia.Controls.Primitives.VisualLayerManager)window.Content!;
        try
        {
            var stage = morph.Stage;
            clock.Advance(TimeSpan.FromSeconds(2));
            Pump();
            stage.CardCacheOverride = true;
            morph.FocusItem(morph.EffectiveItems[6]);
            RunFrames(clock, 4);
            Enumerable.Range(0, stage.Count).Count(i => stage.IsCardCached(i)).ShouldBeGreaterThan(0);
            layers.Child = null;
            Should.NotThrow(() => Pump());
            Enumerable.Range(0, stage.Count).ShouldAllBe(i => !stage.IsCardCached(i) && stage.CardAt(i).CacheMode == null);
            stage.Pictures.Count.ShouldBe(0);
            layers.Child = morph;
            Should.NotThrow(() => Pump());
            RunFrames(clock, 120);
            // Copies again for every picture the carousel shows smaller than its pixels (the front ones are drawn at full size).
            var shown = PictureCards(stage).Where(i => stage.CardAt(i).Opacity > 0.003 && !stage.CardAt(i).HidesContent).ToList();
            shown.Count.ShouldBeGreaterThan(2);
            shown.ShouldAllBe(i => LevelOf(stage, i) == LayoutMorphPictures.LevelFor(Density(stage, i, 2)));
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void A_card_is_still_one_button_with_its_drawing_inside()
    {
        var (window, morph) = Show();
        try
        {
            var card = morph.Stage.CardAt(0);
            card.Face.Focusable.ShouldBeFalse();
            card.Face.IsHitTestVisible.ShouldBeFalse();
            card.Face.Bounds.Size.ShouldBe(card.Bounds.Size);
            var peer = ControlAutomationPeer.CreatePeerForElement(card);
            peer.GetAutomationControlType().ShouldBe(AutomationControlType.Button);
            peer.GetChildren().ShouldBeEmpty();
            peer.GetName().ShouldBe(card.Item.AccessibleName);
        }
        finally
        {
            window.Close();
        }
    }
}
