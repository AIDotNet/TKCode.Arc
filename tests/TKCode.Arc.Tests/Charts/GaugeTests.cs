using Avalonia.Automation.Peers;
using Avalonia.Automation.Provider;
using Avalonia.Headless.XUnit;

namespace TKCode.Arc.Tests.Charts;

public class GaugeTests
{
    private static Gauge Disk(double value) => new()
    {
        Value = value,
        Label = "Disk",
        Thresholds =
        {
            new GaugeThreshold(0, GaugeTone.Success, "Healthy"),
            new GaugeThreshold(60, GaugeTone.Warning, "Filling up"),
            new GaugeThreshold(85, GaugeTone.Danger, "Critical")
        }
    };

    [AvaloniaFact]
    public void Defaults_Match_Arc()
    {
        var gauge = new Gauge();
        gauge.Minimum.ShouldBe(0);
        gauge.Maximum.ShouldBe(100);
        gauge.Tone.ShouldBe(GaugeTone.Accent);
        gauge.Thresholds.Count.ShouldBe(0);
    }

    [AvaloniaFact]
    public void Fills_To_The_Value_And_Names_The_Band()
    {
        var gauge = Disk(72);
        var window = TestHelpers.Show(gauge);
        try
        {
            gauge.Percent.ShouldBe(72);
            gauge.DrawnSweep.ShouldBe(0.72, 1e-9);
            gauge.DigitsText.ShouldBe("72");
            gauge.StatusText.ShouldBe("Filling up");
            gauge.CurrentThreshold!.Tone.ShouldBe(GaugeTone.Warning);
            gauge.Value = 90;
            gauge.StatusText.ShouldBe("Critical");
            gauge.DigitsText.ShouldBe("90");
            gauge.Bounds.Width.ShouldBe(176);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Maps_Any_Range_And_Clamps()
    {
        var gauge = new Gauge { Minimum = 300, Maximum = 850, Value = 575 };
        var window = TestHelpers.Show(gauge);
        try
        {
            gauge.Percent.ShouldBe(50);
            gauge.Value = 1000;
            gauge.Percent.ShouldBe(100);
            gauge.DrawnSweep.ShouldBe(1, 1e-9);
            gauge.Value = -5;
            gauge.Percent.ShouldBe(0);
            gauge.Maximum = 300;
            gauge.Percent.ShouldBe(0);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Reads_As_A_Read_Only_Meter()
    {
        var gauge = Disk(40);
        var window = TestHelpers.Show(gauge);
        try
        {
            var peer = ControlAutomationPeer.CreatePeerForElement(gauge);
            peer.GetAutomationControlType().ShouldBe(AutomationControlType.ProgressBar);
            peer.GetName().ShouldBe("Disk");
            peer.GetHelpText().ShouldBe("40%, Healthy");
            var range = peer.GetProvider<IRangeValueProvider>()!;
            range.IsReadOnly.ShouldBeTrue();
            range.Value.ShouldBe(40);
            range.Maximum.ShouldBe(100);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Without_Thresholds_There_Is_No_State_Line()
    {
        var gauge = new Gauge { Value = 30, Tone = GaugeTone.Danger };
        var window = TestHelpers.Show(gauge);
        try
        {
            gauge.CurrentThreshold.ShouldBeNull();
            gauge.StatusText.ShouldBe(string.Empty);
            gauge.ValueText.ShouldBe("30%");
        }
        finally
        {
            window.Close();
        }
    }
}
