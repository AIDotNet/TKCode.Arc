using Avalonia;
using TKCode.Arc.Motion;

namespace TKCode.Arc.Tests.Special.Surfaces;

/// <summary>
/// Link unfurl's pure rules against the observation notes: what an address is, its short form and plain preview, the
/// unfurl geometry fitted frame by frame (notes 6.2), the springs, the drag-to-fold model and paste / space conversion.
/// </summary>
public class LinkUnfurlLogicTests
{
    [Theory]
    [InlineData("https://www.ridgeway.trails/routes/48213907", true, "https://www.ridgeway.trails/routes/48213907")]
    [InlineData("http://example.org/plan", true, "http://example.org/plan")]
    [InlineData("  https://example.org  ", true, "https://example.org")]
    [InlineData("www.sample.io/path", true, "https://www.sample.io/path")]
    [InlineData("HTTPS://Docs.Example.com/a?b=1#c", true, "HTTPS://Docs.Example.com/a?b=1#c")]
    [InlineData("https://example.org:8080/x", true, "https://example.org:8080/x")]
    [InlineData("https://x", false, "")]
    [InlineData("localhost:3000", false, "")]
    [InlineData("http://localhost:3000", false, "")]
    [InlineData("example.org/plan", false, "")]
    [InlineData("https://example.c", false, "")]
    [InlineData("https://example.12", false, "")]
    [InlineData("https://exa mple.org", false, "")]
    [InlineData("see https://example.org", false, "")]
    [InlineData("", false, "")]
    public void Addresses_Need_A_Scheme_Or_Www_And_A_Dotted_Host(string text, bool expected, string url)
    {
        LinkUnfurlAddress.TryParse(text, out var parsed).ShouldBe(expected);
        parsed.ShouldBe(url);
    }

    [Theory]
    [InlineData("https://www.ridgeway.trails/routes/48213907", "ridgeway.trails/routes/48213907", "ridgeway.trails")]
    [InlineData("https://panfold.kitchen/r/m82q/miso-butter-noodles", "panfold.kitchen/r/m82q/miso-butter-noodles", "panfold.kitchen")]
    [InlineData("http://WWW.Sample.io:81/path", "Sample.io:81/path", "sample.io")]
    public void A_Loading_Link_Shows_The_Address_Without_Scheme_And_Www(string url, string shortForm, string host)
    {
        LinkUnfurlAddress.ShortForm(url).ShouldBe(shortForm);
        LinkUnfurlAddress.Host(url).ShouldBe(host);
    }

    [Theory]
    [InlineData("https://example.org/plan", "Plan", "example.org", "example.org/plan")]
    [InlineData("https://example.net/guides/my-trip_plan.html", "My trip plan", "example.net", "example.net/guides/my-trip_plan.html")]
    [InlineData("https://example.org/x?y=1", "X", "example.org", "example.org/x?y=1")]
    [InlineData("https://www.sample.io/path", "Path", "sample.io", "sample.io/path")]
    [InlineData("https://docs.example.com/", "docs.example.com", "docs.example.com", "docs.example.com/")]
    [InlineData("https://example.org/packing-list", "Packing list", "example.org", "example.org/packing-list")]
    public void A_Plain_Preview_Is_Built_From_The_Address(string url, string title, string site, string description)
    {
        var preview = LinkUnfurlAddress.PlainPreview(url);
        preview.Title.ShouldBe(title);
        preview.Site.ShouldBe(site);
        preview.Description.ShouldBe(description);
        preview.Image.ShouldBeNull();
        preview.Favicon.ShouldBeNull();
        preview.Monogram.ShouldBe(char.ToUpperInvariant(site[0]).ToString());
        LinkUnfurl.CreatePlainPreview(url).Title.ShouldBe(title);
    }

    // Notes 6.2: the stay card (440 × 344) unfurling from a 224px link; p, plate w × h, radius, lift, lane, body, image.
    [Theory]
    [InlineData(0.026, 232.6, 28.2, 13.13, 0.08, 2.4, 0.0, 1.079)]
    [InlineData(0.176, 281.6, 56.6, 13.88, 0.53, 34, 0.0, 1.072)]
    [InlineData(0.354, 332.9, 104.5, 14.77, 0.90, 87, 0.18, 1.060)]
    [InlineData(0.464, 360.5, 139.0, 15.32, 0.99, 126, 0.37, 1.052)]
    [InlineData(0.642, 398.3, 200.9, 16.21, 0.90, 195, 0.73, 1.036)]
    [InlineData(0.766, 418.9, 248.1, 16.83, 0.67, 247, 1.0, 1.024)]
    [InlineData(0.904, 434.9, 303.2, 17.52, 0.30, 309, 1.0, 1.010)]
    [InlineData(0.974, 439.4, 333.2, 17.87, 0.08, 342, 1.0, 1.003)]
    public void The_Unfurl_Follows_The_Measured_Frames(double p, double width, double height, double radius, double lift, double lane,
                                                        double body, double image)
    {
        var link = new Rect(330, -60, 224, 26);
        var slot = new Rect(0, 0, 440, 344);
        var frame = LinkUnfurlMorph.Compute(link, slot, p);
        frame.Visible.Width.ShouldBe(width, 1.0);
        frame.Visible.Height.ShouldBe(height, 1.0);
        frame.Radius.ShouldBe(radius, 0.02);
        frame.Lift.ShouldBe(lift, 0.02);
        ((slot.Height + LinkUnfurlLane.Gap) * frame.SlotFactor).ShouldBe(lane, 1.5);
        frame.BodyOpacity.ShouldBe(body, 0.03);
        frame.ImageScale.ShouldBe(image, 0.005);
        frame.Opacity.ShouldBe(Math.Min(1, 10 * p), 1e-9);
    }

    [Fact]
    public void The_Cards_Favicon_Stays_On_The_Links_Favicon()
    {
        var link = new Rect(330, -60, 224, 26);
        var slot = new Rect(0, 0, 440, 344);
        foreach (var p in new[] { 0.0, 0.1, 0.3, 0.6, 0.9, 1.0 })
        {
            var frame = LinkUnfurlMorph.Compute(link, slot, p);
            // The card's favicon sits at (15, 15) in the card: through the clip it is 15 − inset from the visible box's corner.
            var favicon = slot.Position + frame.Offset + new Vector(15, 15);
            (favicon.X - frame.Visible.X).ShouldBe(15 - frame.ClipLeft, 1e-6);
            (favicon.Y - frame.Visible.Y).ShouldBe(15 - frame.ClipTop, 1e-6);
        }
        // Folded, the card's favicon is exactly where the link draws its own (5px inside the link).
        var folded = LinkUnfurlMorph.Compute(link, slot, 0);
        folded.Visible.ShouldBe(link);
        (slot.Position + folded.Offset + new Vector(15, 15)).ShouldBe(new Point(link.X + 5, link.Y + 5));
        folded.Radius.ShouldBe(13);
        // Open, the card rests in its slot with no clip inset and no offset.
        var open = LinkUnfurlMorph.Compute(link, slot, 1);
        open.Visible.ShouldBe(slot);
        open.Offset.ShouldBe(default);
        open.Radius.ShouldBe(18);
        open.Lift.ShouldBe(0, 1e-9);
    }

    [Fact]
    public void The_Springs_Are_The_Fitted_Ones()
    {
        LinkUnfurlMorph.Open.NaturalFrequency.ShouldBe(10.49, 0.05);
        LinkUnfurlMorph.Open.DampingRatio.ShouldBe(1.10, 0.01);
        LinkUnfurlMorph.Fold.NaturalFrequency.ShouldBe(14.97, 0.05);
        LinkUnfurlMorph.Fold.DampingRatio.ShouldBe(1.14, 0.01);
        // The open spring is 99% there after about 760ms and the fold after about 680ms (notes 6.1).
        var open = SpringSolver.Evaluate(LinkUnfurlMorph.Open, -1, 0, 0.76).Displacement;
        Math.Abs(open).ShouldBeLessThan(0.016);
        Math.Abs(SpringSolver.Evaluate(LinkUnfurlMorph.Open, -1, 0, 0.6).Displacement).ShouldBeGreaterThan(0.01);
        var fold = SpringSolver.Evaluate(LinkUnfurlMorph.Fold, -1, 0, 0.68).Displacement;
        Math.Abs(fold).ShouldBeLessThan(0.012);
        // Speed scales time: twice as fast reaches at 0.38s what the measured spring reaches at 0.76s.
        var fast = LinkUnfurlMorph.Scaled(LinkUnfurlMorph.Open, 2);
        SpringSolver.Evaluate(fast, -1, 0, 0.38).Displacement.ShouldBe(open, 1e-6);
    }

    [Fact]
    public void Dragging_Up_Folds_With_The_Pointer()
    {
        // Measured: 80px up on a 344px card folds it by 0.29.
        LinkUnfurlDragFold.Progress(1, -80, 344).ShouldBe(0.71, 0.01);
        LinkUnfurlDragFold.Progress(1, 40, 344).ShouldBe(1);
        LinkUnfurlDragFold.Progress(1, -400, 344).ShouldBe(0);
        // Release: past 40% folds, a flick up folds, a flick down keeps it open.
        LinkUnfurlDragFold.ShouldFold(0.71, 0).ShouldBeFalse();
        LinkUnfurlDragFold.ShouldFold(0.6, 0).ShouldBeTrue();
        LinkUnfurlDragFold.ShouldFold(0.9, -600).ShouldBeTrue();
        LinkUnfurlDragFold.ShouldFold(0.3, 600).ShouldBeFalse();
        LinkUnfurlDragFold.ProgressVelocity(-275.2, 344).ShouldBe(-1, 1e-9);
    }

    [Fact]
    public void A_Pasted_Address_Or_One_Followed_By_A_Typed_Space_Becomes_A_Link()
    {
        var none = Array.Empty<TokenSpan>();
        LinkUnfurl.FindConversion("look ", "look https://example.org/plan", none).ShouldBe((5, "https://example.org/plan", false));
        LinkUnfurl.FindConversion("look", "look https://example.org/plan ", none).ShouldBe((5, "https://example.org/plan", false));
        LinkUnfurl.FindConversion("see www.sample.io/a", "see www.sample.io/a ", none).ShouldBe((4, "www.sample.io/a", true));
        LinkUnfurl.FindConversion("see https://x", "see https://x ", none).ShouldBeNull();
        LinkUnfurl.FindConversion("see https://example.org", "see https://example.orgx", none).ShouldBeNull();
        LinkUnfurl.FindConversion("", "pasted words, not an address", none).ShouldBeNull();
        // An address already inside a link is left alone.
        var link = new[] { new TokenSpan(LinkUnfurlChipRenderer.Kind, "link-1", "https://example.org", 0, 19) };
        LinkUnfurl.FindConversion("https://example.org", "https://example.org ", link).ShouldBeNull();
        LinkUnfurl.FindConversion("", "https://example.org", link).ShouldBeNull();
    }
}
