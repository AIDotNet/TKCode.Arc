using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using TKCode.Arc.DesignTokens;
using TKCode.Arc.Themes.Corners;
using TKCode.Arc.Themes.Pixel;

namespace TKCode.Arc.Tests.Theme;

public class CompactDensityIntegrationTests
{
    [AvaloniaFact]
    public async Task Compact_preserves_size_ordering_and_combines_order_independently_with_corners_and_pixel()
    {
        var host = new Border();
        var window = TestHelpers.Show(host);
        try
        {
            await TestHelpers.ApplyAsync(t => t.Compact());
            var sm = ArcTokens.Get<double>(host, ArcDesignTokenKind.ControlSizeSM);
            var md = ArcTokens.Get<double>(host, ArcDesignTokenKind.ControlSizeMD);
            var lg = ArcTokens.Get<double>(host, ArcDesignTokenKind.ControlSizeLG);
            sm.ShouldBe(28);
            md.ShouldBe(34);
            lg.ShouldBe(40);
            sm.ShouldBeLessThan(md);
            md.ShouldBeLessThan(lg);
            ArcTokens.Get<CornerRadius>(host, ArcDesignTokenKind.RadiusControl).ShouldBe(new CornerRadius(10));

            // Compact + Square Corners == Square Corners + Compact
            await TestHelpers.ApplyAsync(t => t.Compact().Corners(ArcCornerStyle.Square));
            var r1 = ArcTokens.Get<CornerRadius>(host, ArcDesignTokenKind.RadiusControl);
            var h1 = ArcTokens.Get<double>(host, ArcDesignTokenKind.ControlSizeMD);

            await TestHelpers.ApplyAsync(null);
            await TestHelpers.ApplyAsync(t => t.Corners(ArcCornerStyle.Square).Compact());
            var r2 = ArcTokens.Get<CornerRadius>(host, ArcDesignTokenKind.RadiusControl);
            var h2 = ArcTokens.Get<double>(host, ArcDesignTokenKind.ControlSizeMD);

            r1.ShouldBe(new CornerRadius(0));
            r2.ShouldBe(new CornerRadius(0));
            h1.ShouldBe(34);
            h2.ShouldBe(34);

            // Compact + Pixel keeps compact heights and Pixel square corners
            await TestHelpers.ApplyAsync(t => t.Compact().Pixel());
            ArcTokens.Get<double>(host, ArcDesignTokenKind.ControlSizeMD).ShouldBe(34);
            ArcTokens.Get<CornerRadius>(host, ArcDesignTokenKind.RadiusControl).ShouldBe(new CornerRadius(0));
        }
        finally
        {
            window.Close();
            await TestHelpers.ApplyAsync(null);
        }
    }
}
