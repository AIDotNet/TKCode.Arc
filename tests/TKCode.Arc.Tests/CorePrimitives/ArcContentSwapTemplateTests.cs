using TKCode.Arc.Motion;
using TKCode.Arc.Primitives;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;

namespace TKCode.Arc.Tests.CorePrimitives;

/// <summary>Core <see cref="ArcContentSwap"/> never builds a user's content template for null content.</summary>
public class ArcContentSwapTemplateTests
{
    [AvaloniaFact]
    public void A_Template_Is_Never_Built_With_Null_Content()
    {
        foreach (var level in new[] { ArcMotionLevel.Off, ArcMotionLevel.Full })
        {
            var clock = new ManualFrameClock();
            using var _ = clock.Install();
            var probe = new NullTemplateProbe<string>();
            var swap = new ArcContentSwap { ContentTemplate = probe.Template, Mode = ContentSwapMode.Fade };
            var window = TestHelpers.Show(new StackPanel { Children = { swap } }, level);
            try
            {
                foreach (var content in new[] { "a", "b", null, "c", null, null, "d" })
                {
                    swap.Content = content;
                    Dispatcher.UIThread.RunJobs();
                    clock.Advance(TimeSpan.FromMilliseconds(400));
                    Dispatcher.UIThread.RunJobs();
                }
                probe.Built.Count.ShouldBeGreaterThan(0, level.ToString());
                probe.NullBuilds.ShouldBe(0, level.ToString());
            }
            finally
            {
                window.Close();
            }
        }
    }

    [AvaloniaFact]
    public void Setting_The_Template_On_A_Swap_Without_Content_Builds_Nothing()
    {
        var probe = new NullTemplateProbe<string>();
        var swap = new ArcContentSwap { Content = "a" };
        var window = TestHelpers.Show(new StackPanel { Children = { swap } });
        try
        {
            swap.Content = null;
            Dispatcher.UIThread.RunJobs();
            swap.ContentTemplate = probe.Template;
            Dispatcher.UIThread.RunJobs();
            probe.NullBuilds.ShouldBe(0);
            swap.Content = "b";
            Dispatcher.UIThread.RunJobs();
            probe.Built.ShouldBe(["b"]);
        }
        finally
        {
            window.Close();
        }
    }
}
