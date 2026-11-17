using Avalonia.Controls;
using Avalonia.Headless.XUnit;

namespace TKCode.Arc.Tests.Special.AudioVideo;

/// <summary>The playhead repaints only once its cut has moved a quarter of a device pixel, or reached an end.</summary>
public class NowPlayingRepaintTests
{
    [AvaloniaFact]
    public void Repaints_after_a_quarter_pixel_and_at_the_ends()
    {
        var host = new Border { Width = 300, Height = 20 };
        var window = TestHelpers.Show(host);
        try
        {
            // 300 px at scale 1: a quarter pixel is 1/1200 of the width.
            NowPlayingRepaint.Moved(host, 0.5, 0.5 + 0.2 / 300).ShouldBeFalse();
            NowPlayingRepaint.Moved(host, 0.5, 0.5 + 0.3 / 300).ShouldBeTrue();
            NowPlayingRepaint.Moved(host, 0.5, 0.5 - 0.3 / 300).ShouldBeTrue();
            NowPlayingRepaint.Moved(host, 0.9999, 1).ShouldBeTrue();
            NowPlayingRepaint.Moved(host, 0.0001, 0).ShouldBeTrue();
            NowPlayingRepaint.Moved(host, 0, 0.0001).ShouldBeTrue();
            NowPlayingRepaint.Moved(host, 1, 1).ShouldBeFalse();
        }
        finally
        {
            window.Close();
        }
    }
}
