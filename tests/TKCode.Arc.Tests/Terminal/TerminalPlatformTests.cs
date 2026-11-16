using System.Runtime.InteropServices;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Shouldly;
using TKCode.Terminal.Avalonia;
using TKCode.Terminal.Core;

namespace TKCode.Arc.Tests.Terminal;

/// <summary>What TerminalView and the libghostty-vt loader do differently per platform (issue #136: phones).</summary>
public class TerminalPlatformTests
{
    [AvaloniaFact]
    public void Metrics_Taken_Before_The_View_Is_Shown_Are_Recomputed_When_It_Is()
    {
        // Phones measure before the screen's scaling is known; metrics cached then (at scaling 1) must not stick.
        var view = new TerminalView();
        var detached = view.Metrics;
        detached.Scaling.ShouldBe(1.0);

        var window = TestHelpers.Show(view);
        try
        {
            view.Metrics.ShouldNotBeSameAs(detached);
            view.Metrics.Scaling.ShouldBe(window.RenderScaling);
        }
        finally
        {
            window.Close();
            Dispatcher.UIThread.RunJobs();
        }
    }

    [Fact]
    public void Off_iOS_The_Library_Resolver_Is_Left_To_The_Host()
    {
        // Desktop and Android find libghostty-vt by the default probing; only iOS (an embedded framework) needs a resolver,
        // so elsewhere a host can still install its own for the assembly.
        GhosttyRuntime.EnsureInitialized();
        Should.NotThrow(() => NativeLibrary.SetDllImportResolver(typeof(TerminalCore).Assembly, static (_, _, _) => 0));
    }
}
