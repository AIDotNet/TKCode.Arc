using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using TKCode.Arc.Gallery.Demos;

namespace TKCode.Arc.Tests.Blocks.Showpieces;

/// <summary>Every Customer galaxy demo loads, lays out and renders in light and dark.</summary>
public class CustomerGalaxyDemosTests
{
    private static IEnumerable<Func<UserControl>> Demos() =>
    [
        () => new CustomerGalaxyPreviewDemo(),
        () => new CustomerGalaxyClustersDemo(),
        () => new CustomerGalaxyFocusedClusterDemo(),
        () => new CustomerGalaxySidePanelDemo(),
        () => new CustomerGalaxyNarrowDemo(),
        () => new CustomerGalaxyYourDataDemo()
    ];

    [AvaloniaFact]
    public async Task Every_Demo_Renders_In_Light_And_Dark()
    {
        try
        {
            foreach (var dark in new[] { false, true })
            {
                await TestHelpers.ApplyAsync(dark ? TestHelpers.Dark : null);
                foreach (var create in Demos())
                {
                    var demo = create();
                    var window = TestHelpers.Show(demo);
                    try
                    {
                        window.Width = 900;
                        window.Height = 1100;
                        Dispatcher.UIThread.RunJobs();
                        AvaloniaHeadlessPlatform.ForceRenderTimerTick();
                        Dispatcher.UIThread.RunJobs();
                        var galaxy = TestHelpers.Descendant<CustomerGalaxy>(demo);
                        galaxy.Stage.ShouldNotBeNull().Field.Count.ShouldBeGreaterThan(0, demo.GetType().Name);
                        galaxy.Summary.AccountCount.ShouldBeGreaterThan(0);
                        // Draw the scene once (the test host has no rendered frames to capture).
                        var stage = galaxy.Stage!;
                        using var bitmap = new Avalonia.Media.Imaging.RenderTargetBitmap(new Avalonia.PixelSize(64, 64));
                        using (var context = bitmap.CreateDrawingContext())
                        {
                            stage.Render(context);
                        }
                    }
                    finally
                    {
                        window.Close();
                    }
                }
            }
        }
        finally
        {
            await TestHelpers.ApplyAsync(null);
        }
    }

    [AvaloniaFact]
    public void The_Focused_Cluster_Demo_Opens_Focused_With_Its_Largest_Account_Selected()
    {
        var demo = new CustomerGalaxyFocusedClusterDemo();
        var window = TestHelpers.Show(demo);
        try
        {
            window.Width = 700;
            window.Height = 1000;
            Dispatcher.UIThread.RunJobs();
            var galaxy = TestHelpers.Descendant<CustomerGalaxy>(demo);
            galaxy.FocusedGroupKey.ShouldBe("corporate");
            galaxy.SelectedAccount.ShouldNotBeNull().Groups["plan"].ShouldBe("corporate");
            galaxy.Stage!.FocusGroup.ShouldBe(galaxy.LayoutSet!.IndexOf("corporate"));
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void The_Data_Demo_Uses_Its_Own_States_And_Euros()
    {
        var demo = new CustomerGalaxyYourDataDemo();
        var window = TestHelpers.Show(demo);
        try
        {
            window.Width = 700;
            window.Height = 1000;
            Dispatcher.UIThread.RunJobs();
            var galaxy = TestHelpers.Descendant<CustomerGalaxy>(demo);
            galaxy.HealthStates.Count.ShouldBe(3);
            galaxy.Summary.AccountCount.ShouldBe(420);
            galaxy.Rows.Count.ShouldBe(3);
            TestHelpers.Descendant<ShowpieceNumber>(galaxy, CustomerGalaxy.MrrPart).DisplayText.ShouldStartWith("€");
            // The overview's list borrows the health grouping's header.
            TestHelpers.Descendant<Avalonia.Controls.TextBlock>(galaxy, CustomerGalaxy.ListTitlePart).Text.ShouldBe("By status");
            galaxy.SelectedGroupingKey = "cloud";
            Dispatcher.UIThread.RunJobs();
            galaxy.ClusterLabels.Count.ShouldBe(3);
            TestHelpers.Descendant<Avalonia.Controls.TextBlock>(galaxy, CustomerGalaxy.ListTitlePart).Text.ShouldBe("By hosting");
        }
        finally
        {
            window.Close();
        }
    }
}
