using System.Globalization;
using Avalonia;
using TKCode.Arc.Gestures;

namespace TKCode.Arc.Tests.Special.Surfaces;

/// <summary>The wallet stack's pure logic against the measured values (notes sections 2b, 4.1–4.3).</summary>
public class WalletStackLogicTests
{
    private static readonly WalletStackGeometry G296 = new(296);
    private static readonly WalletStackGeometry G272 = new(272);

    private static WalletStackPose Stack(double d, double fan = 0, in WalletStackGeometry? g = null, double lift = 0)
        => WalletStackLayout.Stack(d, 4, g ?? G296, fan, lift);

    [Fact]
    public void Geometry_follows_the_card_width()
    {
        G296.CardHeight.ShouldBe(186.66, 0.01);
        G296.StageHeight.ShouldBe(406.66, 0.01);
        G296.RoundedStage.ShouldBe(407);
        G272.RoundedStage.ShouldBe(392);
        WalletStackLayout.DetailTop(G296).ShouldBe(12 + 0.88 * 186.66 + 14, 0.01);
    }

    [Fact]
    public void Resting_stack_matches_the_measurement()
    {
        double[] y = { 99.97, 88.77, 77.57, 66.37 };
        double[] scale = { 1, 0.95, 0.90, 0.85 };
        for (var d = 0; d < 4; d++)
        {
            Stack(d).Y.ShouldBe(y[d], 0.05);
            Stack(d).Scale.ShouldBe(scale[d], 1e-9);
        }
        Stack(0, g: G272).Y.ShouldBe(98.67, 0.1);
        Stack(1, g: G272).Y.ShouldBe(88.38, 0.1);
        Stack(2, g: G272).Y.ShouldBe(78.09, 0.1);
    }

    [Fact]
    public void Fanned_stack_matches_the_measurement()
    {
        double[] y = { 144.77, 103.70, 62.64, 21.57 };
        for (var d = 0; d < 4; d++)
        {
            Stack(d, fan: 1).Y.ShouldBe(y[d], 0.05);
        }
        Stack(0, 1, G272).Y.ShouldBe(139.84, 0.1);
        Stack(1, 1, G272).Y.ShouldBe(102.11, 0.1);
    }

    [Fact]
    public void Pocket_layers_sit_lower_and_larger()
    {
        Stack(-1).Y.ShouldBe(365, 1e-9);
        Stack(-1).Scale.ShouldBe(0.90, 1e-9);
        Stack(-2).Y.ShouldBe(372, 1e-9);
        Stack(-2).Scale.ShouldBe(0.92, 1e-9);
        Stack(-3).Y.ShouldBe(379, 1e-9);
        Stack(-3).Scale.ShouldBe(0.94, 1e-9);
        Stack(-1, g: G272).Y.ShouldBe(350, 1e-9);
        // The push is linear between the front and the pocket.
        Stack(-0.5).Y.ShouldBe((99.97 + 365) / 2, 0.05);
        Stack(-0.5).Scale.ShouldBe(0.95, 1e-9);
    }

    [Fact]
    public void Shadows_are_continuous_across_card_boundaries()
    {
        Stack(0).Shadow.ShouldBe(0.45, 1e-9);
        Stack(1).Shadow.ShouldBe(0.15, 1e-9);
        Stack(-1).Shadow.ShouldBe(0, 1e-9);
        Stack(0.999).Shadow.ShouldBe(0.15, 0.001);
        Stack(-0.001).Shadow.ShouldBe(0.45, 0.001);
        // Hover: +.35, cards behind rise 8px, the front card does not.
        Stack(1, lift: 1).Y.ShouldBe(88.77 - 8, 0.05);
        Stack(1, lift: 1).Shadow.ShouldBe(0.5, 1e-9);
        Stack(0, lift: 1).Y.ShouldBe(99.97, 0.05);
        Stack(0, lift: 1).Shadow.ShouldBe(0.80, 1e-9);
    }

    [Fact]
    public void Deeper_cards_wait_hidden_behind_the_fourth()
    {
        var five = WalletStackLayout.Stack(4, 5, G296, 0, 0);
        five.Opacity.ShouldBe(0);
        five.Y.ShouldBe(WalletStackLayout.Stack(3, 5, G296, 0, 0).Y, 1e-9);
        WalletStackLayout.Stack(-4, 5, G296, 0, 0).Opacity.ShouldBe(0);
    }

    [Fact]
    public void Drag_distance_is_one_card_to_the_pocket()
    {
        WalletStackLayout.CardDistance(G296, 4, 1).ShouldBe(220.23, 0.05);
        WalletStackLayout.CardDistance(G296, 4, 0).ShouldBe(265.03, 0.05);
        WalletStackLayout.OpenDistance(G296, 4).ShouldBe(217.2, 0.2);
    }

    [Fact]
    public void Tucked_row_matches_the_measurement()
    {
        WalletStackLayout.Tuck(0, G296).ShouldBe(new WalletStackPose(377, 0.94, 0, 1));
        WalletStackLayout.Tuck(1, G296).Y.ShouldBe(371);
        WalletStackLayout.Tuck(2, G296).Y.ShouldBe(365);
        WalletStackLayout.Tuck(2, G296).Scale.ShouldBe(0.90, 1e-9);
        WalletStackLayout.Tuck(0, G272).Y.ShouldBe(362);
        WalletStackLayout.Tuck(3, G296).Opacity.ShouldBe(0);
    }

    [Fact]
    public void Opening_raises_one_card_and_tucks_the_others_linearly()
    {
        const double o = 0.54;
        // Card 0 opens: y = y_stack·(1 − o), scale 1 − 0.12·o, shadow .45 + .55·o.
        var raised = WalletStackLayout.Pose(0, 4, G296, 0, 0, 0, o, o, WalletStackLayout.TuckRank(0, 0, -1));
        raised.Y.ShouldBe(99.97 * (1 - o), 0.05);
        raised.Scale.ShouldBe(1 - 0.12 * o, 1e-9);
        raised.Shadow.ShouldBe(0.45 + 0.55 * o, 1e-9);
        // Card 1 tucks into slot 0: 88.77 + 288.23 × .54 = 244.4 (measured 244.33).
        var tucked = WalletStackLayout.Pose(1, 4, G296, 0, 0, 0, o, 0, WalletStackLayout.TuckRank(1, 0, -1));
        tucked.Y.ShouldBe(244.33, 0.1);
        tucked.Shadow.ShouldBe(0.15 * (1 - o), 1e-9);
        // Fully open.
        WalletStackLayout.Pose(0, 4, G296, 0, 0, 0, 1, 1, 0).ShouldBe(new WalletStackPose(0, 0.88, 1, 1));
        WalletStackLayout.Pose(3, 4, G296, 0, 0, 0, 1, 0, WalletStackLayout.TuckRank(3, 0, -1)).Y.ShouldBe(365, 1e-9);
    }

    [Fact]
    public void Switching_the_open_card_hands_the_tucked_slot_over()
    {
        // From card 0 to card 1, halfway: both are in slot 0's lane and mirror each other (measured 178.7 / 198.3 at 392ms).
        var old = WalletStackLayout.Pose(0, 4, G296, 0.5, 0, 0, 1, 0.47, WalletStackLayout.TuckRank(0, 1, 0));
        var next = WalletStackLayout.Pose(1, 4, G296, 0.5, 0, 0, 1, 0.53, WalletStackLayout.TuckRank(1, 1, 0));
        old.Y.ShouldBe(377 * 0.53, 0.05);
        next.Y.ShouldBe(377 * 0.47, 0.05);
        (old.Y + next.Y).ShouldBe(377, 0.05);
        // Card 2 stays in slot 1.
        WalletStackLayout.Pose(2, 4, G296, 0.5, 0, 0, 1, 0, WalletStackLayout.TuckRank(2, 1, 0)).Y.ShouldBe(371, 1e-9);
    }

    [Fact]
    public void Pulling_the_open_card_up_overshoots_its_top()
    {
        var pose = WalletStackLayout.Pose(0, 4, G296, 0, 0, 0, 1.16, 1.16, 0);
        pose.Y.ShouldBe(-16, 0.1);
    }

    [Theory]
    [InlineData(0.577, 0.295)]
    [InlineData(0.731, 0.552)]
    [InlineData(0.833, 0.722)]
    [InlineData(0.9, 0.833)]
    public void Activity_fades_in_over_the_last_part_of_opening(double open, double opacity)
    {
        WalletStackLayout.Detail(open).Opacity.ShouldBe(opacity, 0.002);
        WalletStackLayout.Detail(0).Offset.ShouldBe(16);
        WalletStackLayout.Detail(1).Offset.ShouldBe(0);
    }

    [Fact]
    public void Drag_follows_one_to_one_and_rubber_bands_past_the_ends()
    {
        const double d = 220.23;
        WalletStackGesture.DragPosition(0, 110, d, 4).ShouldBe(110 / d, 1e-9);
        WalletStackGesture.DragPosition(0, 376, d, 4).ShouldBe(376 / d, 1e-9);
        var stretched = WalletStackGesture.DragPosition(0, -100, d, 4) * d;
        stretched.ShouldBe(RubberBand.Apply(-100, d, 0.55), 1e-9);
        stretched.ShouldBe(-44.0, 0.5);
        (WalletStackGesture.DragPosition(3, 100, d, 4) - 3).ShouldBeGreaterThan(0);
        (WalletStackGesture.DragPosition(3, 100, d, 4) - 3).ShouldBeLessThan(100 / d);
    }

    [Fact]
    public void Release_projects_two_tenths_of_a_second_and_moves_at_most_one_card()
    {
        const double d = 220.23;
        // Slow: 100px springs back, 120px moves on.
        WalletStackGesture.ReleaseTarget(100 / d, 0, d, 0, 4).ShouldBe(0);
        WalletStackGesture.ReleaseTarget(120 / d, 0, d, 0, 4).ShouldBe(1);
        // A flick: 30px at 600px/s projects past half a card.
        WalletStackGesture.ReleaseTarget(30 / d, 600, d, 0, 4).ShouldBe(1);
        WalletStackGesture.ReleaseTarget(30 / d, 300, d, 0, 4).ShouldBe(0);
        // Never more than one card from the start, never out of the stack.
        WalletStackGesture.ReleaseTarget(1.7, 3000, d, 0, 4).ShouldBe(1);
        WalletStackGesture.ReleaseTarget(-0.2, -900, d, 0, 4).ShouldBe(0);
        WalletStackGesture.ReleaseTarget(3.1, 900, d, 3, 4).ShouldBe(3);
    }

    [Fact]
    public void Open_card_pull_maps_and_closes_under_one_half()
    {
        const double d = 217.2;
        WalletStackGesture.OpenAmount(1, 100, d, 296).ShouldBe(1 - 100 / d, 1e-9);
        WalletStackGesture.OpenAmount(1, 400, d, 296).ShouldBe(0);
        WalletStackGesture.OpenAmount(1, -140, d, 296).ShouldBe(1.16, 0.01);
        WalletStackGesture.ReleaseCloses(1 - 100 / d, 0, d).ShouldBeFalse();
        WalletStackGesture.ReleaseCloses(1 - 120 / d, 0, d).ShouldBeTrue();
        // A downward flick closes from higher up; an upward one keeps it open.
        WalletStackGesture.ReleaseCloses(0.8, 900, d).ShouldBeTrue();
        WalletStackGesture.ReleaseCloses(0.45, -900, d).ShouldBeFalse();
    }

    [Fact]
    public void Money_uses_the_currency_and_the_culture()
    {
        var us = new WalletStackFormat("USD", CultureInfo.GetCultureInfo("en-US"));
        us.Money(2914.35m).ShouldBe("$2,914.35");
        us.Money(37.8m).ShouldBe("$37.80");
        us.Signed(-63.27m).ShouldBe("−$63.27");
        us.Signed(42m).ShouldBe("+$42.00");
        us.Signed(3120m).ShouldBe("+$3,120.00");
        new WalletStackFormat("EUR", CultureInfo.GetCultureInfo("en-US")).Money(1234.5m).ShouldBe("€1,234.50");
        var de = new WalletStackFormat("EUR", CultureInfo.GetCultureInfo("de-DE"));
        de.Money(2914.35m).Replace('\u00a0', ' ').ShouldBe("2.914,35 €");
        de.Signed(-4.8m).Replace('\u00a0', ' ').ShouldBe("−4,80 €");
        new WalletStackFormat("JPY", CultureInfo.GetCultureInfo("en-US")).Money(1200m).ShouldBe("¥1,200");
        WalletStackFormat.CardNumber("5306").ShouldBe("•••• •••• •••• 5306");
    }

    [Fact]
    public void Default_labels_read_the_card()
    {
        var labels = new WalletStackLabels();
        var card = WalletStackSamples.Cards()[1];
        labels.CardName(card, 2, 4, "$1,472.18").ShouldBe("Obsidian Charge card ending in 1148, 2 of 4, statement balance $1,472.18");
        labels.FrontAnnouncement(card, 2, 4).ShouldBe("Obsidian Charge card in front, 2 of 4");
        labels.OpenAnnouncement(card).ShouldBe("Obsidian Charge card open");
        labels.ActivityName(card).ShouldBe("Obsidian activity");
        labels.TransactionsName(card).ShouldBe("Recent activity on Obsidian");
        (labels.CountBefore(4) + "1" + labels.CountAfter(4)).ShouldBe("1 of 4");
    }

    [Fact]
    public void Tilt_matrix_is_a_perspective_rotation_about_the_centre()
    {
        var size = new Size(296, 186.66);
        WalletStackTiltLight.Perspective(0, 0, size).ShouldBe(Matrix.Identity);
        // Pointer at the top left (rotateX 4.59, rotateY −4.68): the left edge recedes (shorter), the top edge too.
        var m = WalletStackTiltLight.Perspective(4.59, -4.68, size);
        var center = new Point(148, 93.33).Transform(m);
        center.X.ShouldBe(148, 1e-3);
        center.Y.ShouldBe(93.33, 1e-3);
        double Height(double x) => new Point(x, size.Height).Transform(m).Y - new Point(x, 0).Transform(m).Y;
        double Width(double y) => new Point(size.Width, y).Transform(m).X - new Point(0, y).Transform(m).X;
        Height(0).ShouldBeLessThan(Height(size.Width));
        Width(0).ShouldBeLessThan(Width(size.Height));
        // About 5°: a few percent, not more.
        (Height(size.Width) / Height(0)).ShouldBeLessThan(1.06);
    }

    [Fact]
    public void Samples_are_four_cards_of_four_materials_with_three_transactions()
    {
        var cards = WalletStackSamples.Cards();
        cards.Count.ShouldBe(4);
        cards.Select(c => c.Material).ShouldBe(new[]
        {
            WalletStackMaterial.Metal, WalletStackMaterial.Black, WalletStackMaterial.Glass, WalletStackMaterial.Color,
        });
        cards.ShouldAllBe(c => c.Transactions.Count == 3 && c.LastFour.Length == 4 && c.Holder == WalletStackSamples.Holder);
        cards.Select(c => c.Id).Distinct().Count().ShouldBe(4);
        WalletStackBadge.Initials("Ruth Okafor").ShouldBe("RO");
        WalletStackBadge.Initials("ferry").ShouldBe("F");
    }

    [Fact]
    public void Guilloche_is_closed_rings_around_the_upper_right()
    {
        var geometry = WalletStackTextures.Guilloche();
        var bounds = geometry.Bounds;
        bounds.Center.X.ShouldBe(616, 15);
        bounds.Center.Y.ShouldBe(216, 15);
        bounds.Height.ShouldBeLessThan(bounds.Width);
        WalletStackTextures.ValueNoise(3.5, 2.25, 7).ShouldBeInRange(0, 1);
        WalletStackTextures.BoxRadius(6).ShouldBe(5.52, 0.01);
    }
}
