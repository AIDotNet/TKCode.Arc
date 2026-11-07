using Avalonia;
using Avalonia.Input;

namespace TKCode.Arc.Tests.Special.Surfaces;

/// <summary>The control center's pure logic against the numbers measured in the notes.</summary>
public class ControlCenterMathTests
{
    [Theory]
    [InlineData(5, 1.95)]
    [InlineData(10, 3.84)]
    [InlineData(20, 7.34)]
    [InlineData(30, 10.59)]
    [InlineData(45, 15.0)]
    [InlineData(60, 18.95)]
    public void Dial_overshoot_follows_the_fitted_rubber_band(double fingerDegrees, double knobDegrees)
    {
        var raw = 120 + fingerDegrees / 3;
        var knob = ControlCenterMath.DialKnobMinutes(raw);
        ((knob - 120) * 3).ShouldBe(knobDegrees, 0.06);
        // Below 5 the same curve: a 75° pull lands 22.5° past the limit.
        ((ControlCenterMath.DialKnobMinutes(5 - 75.0 / 3) - 5) * 3).ShouldBe(-22.5, 0.1);
    }

    [Fact]
    public void Dial_is_linear_inside_the_range_and_holds_number_and_arc_at_the_limits()
    {
        ControlCenterMath.DialKnobMinutes(71.4).ShouldBe(71.4);
        ControlCenterMath.DialNumber(71.4).ShouldBe(71);
        ControlCenterMath.DialNumber(126).ShouldBe(120);
        ControlCenterMath.DialNumber(2).ShouldBe(5);
        ControlCenterMath.DialArc(50).ShouldBe(50.0 / 120, 1e-9);
        ControlCenterMath.DialArc(130).ShouldBe(1);
        ControlCenterMath.DialArc(1).ShouldBe(5.0 / 120, 1e-9);
    }

    [Theory]
    [InlineData(53, 55)]
    [InlineData(52, 50)]
    [InlineData(2, 5)]
    [InlineData(131, 120)]
    [InlineData(117.6, 120)]
    public void Dial_snaps_to_five_minutes_within_range(double raw, int snapped) =>
        ControlCenterMath.SnapMinutes(raw).ShouldBe(snapped);

    [Fact]
    public void Angles_are_clockwise_from_twelve_and_unwrap_across_it()
    {
        var c = new Point(100, 100);
        ControlCenterMath.AngleOf(new Point(100, 0), c).ShouldBe(0, 1e-9);
        ControlCenterMath.AngleOf(new Point(200, 100), c).ShouldBe(90, 1e-9);
        ControlCenterMath.AngleOf(new Point(100, 200), c).ShouldBe(180, 1e-9);
        ControlCenterMath.AngleOf(new Point(0, 100), c).ShouldBe(270, 1e-9);
        ControlCenterMath.AngleDelta(350, 10).ShouldBe(20, 1e-9);
        ControlCenterMath.AngleDelta(10, 350).ShouldBe(-20, 1e-9);
    }

    [Theory]
    [InlineData(50, Key.Right, 55)]
    [InlineData(50, Key.Up, 55)]
    [InlineData(50, Key.Left, 45)]
    [InlineData(50, Key.Down, 45)]
    [InlineData(50, Key.PageUp, 65)]
    [InlineData(50, Key.PageDown, 35)]
    [InlineData(95, Key.Home, 5)]
    [InlineData(50, Key.End, 120)]
    [InlineData(120, Key.Right, 120)]
    [InlineData(10, Key.PageDown, 5)]
    public void Dial_keys(int value, Key key, int expected) => ControlCenterMath.DialKey(value, key).ShouldBe(expected);

    [Theory]
    [InlineData(2.4, 1.10)]
    [InlineData(22.4, 4.04)]
    [InlineData(62.4, 5.11)]
    [InlineData(162.4, 5.62)]
    public void Meter_stretch_follows_the_fitted_rubber_band(double overflow, double stretch)
    {
        ControlCenterMath.MeterStretch(overflow).ShouldBe(stretch, 0.03);
        ControlCenterMath.MeterStretch(-overflow).ShouldBe(-stretch, 0.03);
    }

    [Fact]
    public void Meter_drags_relative_and_taps_land_on_the_tapped_value()
    {
        // Grabbed at 44, moved 20px on a 326px track: 50.
        ControlCenterMath.Snap(ControlCenterMath.MeterDragValue(44, 20, 326, 0, 100), 0, 100, 1).ShouldBe(50);
        ControlCenterMath.Snap(ControlCenterMath.MeterValueAt(780 - 557, 326, 0, 100), 0, 100, 1).ShouldBe(68);
        ControlCenterMath.Snap(ControlCenterMath.MeterValueAt(640 - 557, 326, 0, 100), 0, 100, 1).ShouldBe(25);
        ControlCenterMath.MeterFraction(100, 85, 130).ShouldBe(1.0 / 3, 1e-9);
        ControlCenterMath.Snap(112, 85, 130, 5).ShouldBe(110);
        ControlCenterMath.SegmentWidth(326).ShouldBe(8.75, 1e-9);
    }

    [Theory]
    [InlineData(64, Key.Right, 0, 100, 1, 65)]
    [InlineData(64, Key.PageUp, 0, 100, 1, 68)]
    [InlineData(64, Key.PageDown, 0, 100, 1, 60)]
    [InlineData(64, Key.Home, 0, 100, 1, 0)]
    [InlineData(64, Key.End, 0, 100, 1, 100)]
    [InlineData(100, Key.Right, 85, 130, 5, 105)]
    [InlineData(100, Key.PageUp, 85, 130, 5, 120)]
    [InlineData(100, Key.Home, 85, 130, 5, 85)]
    public void Meter_keys(int value, Key key, int min, int max, int step, int expected) =>
        ControlCenterMath.MeterKey(value, key, min, max, step).ShouldBe(expected);

    [Fact]
    public void Closing_crossfade_matches_the_measured_curve()
    {
        ControlCenterMath.CloseCrossfade(0).ShouldBe((0, 1));
        var (tile, detail) = ControlCenterMath.CloseCrossfade(0.54);
        tile.ShouldBe(1);
        detail.ShouldBe(0.911, 0.002);
        ControlCenterMath.CloseCrossfade(0.25).Tile.ShouldBeInRange(0.85, 0.87);
        ControlCenterMath.CloseCrossfade(0.95).Detail.ShouldBe(0, 1e-9);
    }

    [Fact]
    public void Labels_format_times_counts_and_people()
    {
        var labels = new ControlCenterLabels();
        var noon = new DateTimeOffset(2026, 10, 27, 11, 23, 0, TimeSpan.Zero);
        labels.FormatTime(ControlCenterMath.Until(noon, 50)).ShouldBe("12:13 PM");
        labels.FormatTime(ControlCenterMath.Until(noon.AddMinutes(18), 5)).ShouldBe("11:46 AM");
        labels.FormatTime(new DateTimeOffset(2026, 10, 27, 13, 29, 0, TimeSpan.Zero)).ShouldBe("1:29 PM");
        labels.Until("12:13 PM").ShouldBe("Until 12:13 PM");
        labels.ChannelCount(0, 3).ShouldBe("0 of 3 channels");
        labels.Percent(64).ShouldBe("64%");
        labels.DialValueText(50, "12:13 PM").ShouldBe("50 minutes, until 12:13 PM");
        labels.PeopleSentence(["Mara"]).ShouldBe("Always let through: Mara");
        labels.PeopleSentence(["Mara", "Oskar", "Ines"]).ShouldBe("Always let through: Mara, Oskar and Ines");
        labels.PeopleSentence(["A", "B", "C", "D", "E"]).ShouldBe("Always let through: A, B and 3 more");
        ControlCenterMath.Initials("Mara Lind").ShouldBe("ML");
        ControlCenterMath.FirstName("Ines Paz").ShouldBe("Ines");
    }
}
