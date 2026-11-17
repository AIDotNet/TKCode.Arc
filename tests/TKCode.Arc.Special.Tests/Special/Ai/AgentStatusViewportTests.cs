using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Layout;
using Avalonia.Threading;
using Avalonia.VisualTree;
using TKCode.Arc.Primitives;

namespace TKCode.Arc.Tests.Special.Ai;

/// <summary>
/// <see cref="AgentStatus"/> treats an <see cref="ArcViewportHost"/> as its window (M4 of
/// design/engineering/gallery-mobile-preview.md); the other viewport-aware controls are in ArcViewportControlsTests.
/// </summary>
public class AgentStatusViewportTests
{
    /// <summary>A wide window with a 390 × 700 host on its left (not at the window's origin) and the control inside.</summary>
    private static (Window Window, ArcViewportHost Host) InPhone(Control control, double width = 390)
    {
        var host = new ArcViewportHost
        {
            Width = width,
            Height = 700,
            Margin = new Thickness(200, 60, 0, 0),
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Top,
            Children = { new StackPanel { Margin = new Thickness(16), Children = { control } } }
        };
        var window = TestHelpers.Show(host);
        window.Width = 1200;
        window.Height = 900;
        Dispatcher.UIThread.RunJobs();
        return (window, host);
    }

    [AvaloniaFact]
    public void An_Agent_Status_Opened_In_A_Host_Floats_On_The_Host()
    {
        var status = new AgentStatus
        {
            Status = AgentStatusRunState.Working,
            Agent = new AgentModel("Atlas Scout 2"),
            Activity = new AgentStatusActivity("Reading the logs", ArcIcons.Search)
        };
        var (window, host) = InPhone(status);
        try
        {
            status.IsExpanded = true;
            Dispatcher.UIThread.RunJobs();
            status.IsFloating.ShouldBeTrue();
            // Its viewport's layer, so the open panel stays on the phone's screen.
            status.Surface.GetVisualAncestors().ShouldContain(host.Layer);
            status.IsExpanded = false;
            Dispatcher.UIThread.RunJobs();
        }
        finally
        {
            window.Close();
        }
    }
}
