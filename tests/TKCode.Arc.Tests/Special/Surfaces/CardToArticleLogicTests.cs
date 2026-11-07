using Avalonia;
using Shouldly;
using Xunit;

namespace TKCode.Arc.Tests.Special.Surfaces;

/// <summary>Card to article: the layout rules and the pull gesture against the measured numbers (no window).</summary>
public class CardToArticleLogicTests
{
    private static TimeSpan Ms(double ms) => TimeSpan.FromMilliseconds(ms);

    [Theory]
    [InlineData(680, 2, 332)]
    [InlineData(652, 2, 318)]
    [InlineData(592, 2, 288)]
    [InlineData(522, 2, 253)]
    public void Wide_grids_have_two_columns_16_apart(double width, int columns, double column)
    {
        CardToArticleLayout.Columns(width).ShouldBe(columns);
        var slot = CardToArticleLayout.Slot(1, width, 4.0 / 3.0);
        slot.Width.ShouldBe(column, 0.5);
        slot.X.ShouldBe(column + 16, 0.5);
        slot.Height.ShouldBe(slot.Width * 0.75, 0.001);
    }

    [Fact]
    public void Grids_at_most_520_wide_have_one_column_12_apart()
    {
        CardToArticleLayout.Columns(520).ShouldBe(1);
        var second = CardToArticleLayout.Slot(1, 472, 4.0 / 3.0);
        second.X.ShouldBe(0);
        second.Width.ShouldBe(472);
        second.Y.ShouldBe(472 * 0.75 + 12, 0.001);
        CardToArticleLayout.GridHeight(4, 680, 4.0 / 3.0).ShouldBe(2 * 249 + 16, 0.01);
    }

    [Theory]
    [InlineData(1440, 1000, 380, 24, 680, 952)]
    [InlineData(700, 844, 16, 24, 668, 796)]
    [InlineData(521, 844, 16, 24, 489, 796)]
    [InlineData(520, 844, 8, 12, 504, 820)]
    [InlineData(390, 844, 8, 12, 374, 820)]
    [InlineData(1440, 1400, 380, 24, 680, 1352)]
    public void The_sheet_is_at_most_680_wide_and_inset_from_the_window(double vw, double vh, double x, double y, double w, double h)
    {
        var sheet = CardToArticleLayout.Sheet(new Size(vw, vh));
        sheet.X.ShouldBe(x, 0.01);
        sheet.Y.ShouldBe(y, 0.01);
        sheet.Width.ShouldBe(w, 0.01);
        sheet.Height.ShouldBe(h, 0.01);
    }

    [Theory]
    [InlineData(320, 20)]
    [InlineData(390, 23.4)]
    [InlineData(640, 38.4)]
    [InlineData(760, 45.6)]
    [InlineData(1440, 48)]
    public void The_article_side_padding_is_6vw_between_20_and_48(double vw, double padding)
        => CardToArticleLayout.BodyPaddingX(vw).ShouldBe(padding, 0.01);

    [Theory]
    [InlineData(332, 11.288)]
    [InlineData(374, 12.716)]
    [InlineData(680, 20)]
    public void The_meta_line_is_3_4_percent_of_the_picture_capped_at_20(double width, double size)
        => CardToArticleLayout.MetaFontSize(width).ShouldBe(size, 0.001);

    [Fact]
    public void Balanced_wrapping_picks_the_narrowest_width_with_the_same_line_count()
    {
        // A text 300 units long in words of 10: lines = ceil(300 / floor-to-10(width)).
        static int Lines(double w) => (int)Math.Ceiling(300 / Math.Max(10, Math.Floor(w / 10) * 10));
        var width = CardToArticleLayout.Balance(200, Lines);
        Lines(200).ShouldBe(2);
        Lines(width).ShouldBe(2);
        width.ShouldBe(150, 0.6);
        CardToArticleLayout.Balance(400, Lines).ShouldBe(400);
    }

    [Theory]
    [InlineData(60, 18, 0.96)]
    [InlineData(160, 48, 0.8933)]
    [InlineData(260, 78, 0.8267)]
    [InlineData(360, 108, 0.76)]
    [InlineData(480, 144, 0.68)]
    [InlineData(760, 144, 0.68)]
    [InlineData(-40, 0, 1)]
    public void The_lift_follows_the_pull_linearly(double d, double translate, double scale)
    {
        var (dy, s) = CardToArticlePullGesture.Lift(d);
        dy.ShouldBe(translate, 0.01);
        s.ShouldBe(scale, 0.0001);
    }

    [Fact]
    public void The_lift_scales_about_50_30_of_the_window()
    {
        var m = CardToArticlePullGesture.LiftMatrix(160, new Point(720, 300));
        m.Transform(new Point(720, 300)).ShouldBe(new Point(720, 348));
        var corner = m.Transform(new Point(0, 0));
        corner.X.ShouldBe(720 - 720 * 0.893333, 0.01);
    }

    [Fact]
    public void A_mouse_pull_past_120_dismisses_and_one_short_of_it_springs_back()
    {
        var pull = new CardToArticlePullGesture();
        pull.PressMouse(100, Ms(0));
        pull.Move(101, true, Ms(16)).ShouldBe(CardToArticlePullResult.Follow);
        pull.Distance.ShouldBe(1);
        pull.Move(220, true, Ms(1000)).ShouldBe(CardToArticlePullResult.Follow);
        pull.Distance.ShouldBe(120);
        pull.Release(Ms(1200)).ShouldBe(CardToArticlePullResult.SnapBack);

        pull.Reset();
        pull.PressMouse(100, Ms(2000));
        pull.Move(221, true, Ms(3000));
        pull.Release(Ms(3200)).ShouldBe(CardToArticlePullResult.Dismiss);
    }

    [Fact]
    public void A_flick_faster_than_600_px_per_second_dismisses_a_short_pull()
    {
        var pull = new CardToArticlePullGesture();
        pull.PressMouse(0, Ms(0));
        for (var i = 1; i <= 6; i++)
        {
            pull.Move(i * 11, true, Ms(i * 16));
        }
        pull.Distance.ShouldBeLessThan(120);
        pull.Release(Ms(96)).ShouldBe(CardToArticlePullResult.Dismiss);
        pull.ReleaseVelocity.ShouldBeGreaterThan(600);

        var slow = new CardToArticlePullGesture();
        slow.PressMouse(0, Ms(0));
        for (var i = 1; i <= 6; i++)
        {
            slow.Move(i * 9, true, Ms(i * 16));
        }
        slow.Release(Ms(96)).ShouldBe(CardToArticlePullResult.SnapBack);
    }

    [Fact]
    public void Upward_pulls_do_not_rubber_band_and_the_distance_caps_at_480()
    {
        var pull = new CardToArticlePullGesture();
        pull.PressMouse(500, Ms(0));
        pull.Move(400, true, Ms(16));
        pull.Distance.ShouldBe(0);
        pull.Move(1500, true, Ms(32));
        pull.Distance.ShouldBe(480);
    }

    [Fact]
    public void Touch_at_the_top_waits_for_the_slop_and_measures_from_there()
    {
        var pull = new CardToArticlePullGesture();
        pull.PressTouch(100, atTop: true, Ms(0));
        pull.Move(111, true, Ms(16)).ShouldBe(CardToArticlePullResult.None);
        pull.Move(116, true, Ms(32)).ShouldBe(CardToArticlePullResult.Follow);
        pull.Distance.ShouldBe(0);
        pull.Move(122, true, Ms(48));
        pull.Distance.ShouldBe(6);
    }

    [Fact]
    public void Touch_that_starts_scrolled_hands_over_to_the_pull_at_the_top()
    {
        var pull = new CardToArticlePullGesture();
        pull.PressTouch(100, atTop: false, Ms(0));
        pull.Move(200, false, Ms(100)).ShouldBe(CardToArticlePullResult.None);
        pull.Move(300, false, Ms(200)).ShouldBe(CardToArticlePullResult.None);
        // The content reached its top: the rest of the same movement pulls.
        pull.Move(350, true, Ms(300)).ShouldBe(CardToArticlePullResult.Follow);
        pull.Distance.ShouldBe(50);
        CardToArticlePullGesture.Lift(pull.Distance).TranslateY.ShouldBe(15);
    }

    [Fact]
    public void Touch_that_moves_up_first_never_pulls()
    {
        var pull = new CardToArticlePullGesture();
        pull.PressTouch(300, atTop: true, Ms(0));
        pull.Move(250, false, Ms(16)).ShouldBe(CardToArticlePullResult.None);
        pull.Release(Ms(32)).ShouldBe(CardToArticlePullResult.None);
    }

    [Fact]
    public void The_wheel_at_the_top_pulls_by_0_6_and_closes_once_past_120()
    {
        var pull = new CardToArticlePullGesture();
        pull.Wheel(6, atTop: false, Ms(0)).ShouldBe(CardToArticlePullResult.None);
        pull.Wheel(-6, atTop: true, Ms(0)).ShouldBe(CardToArticlePullResult.None);
        pull.Wheel(6, atTop: true, Ms(10)).ShouldBe(CardToArticlePullResult.Follow);
        pull.Distance.ShouldBe(3.6, 1e-9);
        CardToArticlePullGesture.Lift(pull.Distance).TranslateY.ShouldBe(1.08, 1e-9);
        var result = CardToArticlePullResult.Follow;
        var t = 10.0;
        while (result == CardToArticlePullResult.Follow)
        {
            t += 10;
            result = pull.Wheel(6, true, Ms(t));
        }
        result.ShouldBe(CardToArticlePullResult.Dismiss);
        pull.Distance.ShouldBeGreaterThan(120);
        pull.Distance.ShouldBeLessThan(124);
        // Later events are swallowed and change nothing.
        var d = pull.Distance;
        pull.Wheel(60, true, Ms(t + 10)).ShouldBe(CardToArticlePullResult.Follow);
        pull.Distance.ShouldBe(d);
    }

    [Fact]
    public void A_wheel_pull_springs_back_after_150ms_without_events()
    {
        var pull = new CardToArticlePullGesture();
        pull.Wheel(50, true, Ms(0));
        pull.Distance.ShouldBe(30);
        pull.WheelIdleElapsed(Ms(100)).ShouldBeFalse();
        pull.Wheel(50, true, Ms(120));
        pull.WheelIdleElapsed(Ms(250)).ShouldBeFalse();
        pull.WheelIdleElapsed(Ms(271)).ShouldBeTrue();
        pull.IsPulling.ShouldBeFalse();
    }

    [Fact]
    public void Scrolling_down_while_wheel_pulled_takes_the_pull_back_first()
    {
        var pull = new CardToArticlePullGesture();
        pull.Wheel(100, true, Ms(0));
        pull.Distance.ShouldBe(60);
        pull.Wheel(-50, true, Ms(10)).ShouldBe(CardToArticlePullResult.Follow);
        pull.Distance.ShouldBe(30);
        pull.Wheel(-100, true, Ms(20));
        pull.Distance.ShouldBe(0);
        pull.Wheel(-10, true, Ms(30)).ShouldBe(CardToArticlePullResult.None);
    }

    [Fact]
    public void Prose_splits_into_paragraphs_and_pull_quotes()
    {
        var blocks = CardToArticleArticle.SplitProse("One line\nwrapped.\n\n> A quote.\n\nLast.");
        blocks.Count.ShouldBe(3);
        blocks[0].ShouldBe(new CardToArticleArticle.ProseBlock("One line wrapped.", false));
        blocks[1].ShouldBe(new CardToArticleArticle.ProseBlock("A quote.", true));
        blocks[2].IsQuote.ShouldBeFalse();
        CardToArticleArticle.Initials("Sigrid Vale").ShouldBe("SV");
        CardToArticleArticle.Initials("ada").ShouldBe("A");
    }

    [Fact]
    public void A_cover_crop_keeps_the_middle_of_the_picture()
    {
        CardToArticleHero.CoverSource(new Size(1600, 900), new Size(400, 300)).ShouldBe(new Rect(200, 0, 1200, 900));
        CardToArticleHero.CoverSource(new Size(1600, 1600), new Size(400, 300)).ShouldBe(new Rect(0, 200, 1600, 1200));
    }

    [Fact]
    public void The_springs_are_the_fitted_critically_damped_ones()
    {
        CardToArticle.OpenSpring.ShouldBe(TKCode.Arc.Motion.SpringSpec.FromVisual(0.52, 0));
        CardToArticle.CloseSpring.ShouldBe(TKCode.Arc.Motion.SpringSpec.FromVisual(0.44, 0));
        CardToArticle.PopSpring.ShouldBe(TKCode.Arc.Motion.SpringSpec.FromVisual(0.30, 0));
        CardToArticleArticle.SnapBackSpring.ShouldBe(TKCode.Arc.Motion.SpringSpec.FromVisual(0.38, 0));
    }

    [Fact]
    public void Samples_have_the_shapes_of_the_reference()
    {
        var stories = CardToArticleSamples.Stories();
        stories.Count.ShouldBe(4);
        stories.Select(s => s.Tag!.Length).ShouldBe([11, 9, 6, 7]);
        stories.Select(s => s.Title.Length).ShouldBe([37, 33, 37, 37]);
        stories.ShouldAllBe(s => s.Image != null && s.Author != null && s.Body is string);
        CardToArticleSamples.AllStories().Select(s => s.Id).Distinct().Count().ShouldBe(6);
    }
}
