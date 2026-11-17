using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Avalonia.VisualTree;

namespace TKCode.Arc.Tests.Blocks.AppShell;

/// <summary>
/// <c>IsVisible</c> (and <c>IsEnabled</c>) on the item classes of the command palette and the site header (issue #117):
/// a hidden item is not listed, a disabled one is dimmed, and both follow later changes.
/// </summary>
public class ItemStateTests
{
    private static void Settle()
    {
        Dispatcher.UIThread.RunJobs();
        AvaloniaHeadlessPlatform.ForceRenderTimerTick();
        Dispatcher.UIThread.RunJobs();
    }

    [Fact]
    public void The_Command_Palette_Filter_Leaves_Out_Hidden_Commands()
    {
        var alpha = new CommandPaletteItem { Id = "alpha", Label = "Alpha" };
        var beta = new CommandPaletteItem { Id = "beta", Label = "Beta", IsVisible = false };
        var gamma = new CommandPaletteItem { Id = "gamma", Label = "Gamma" };
        CommandPaletteFilter.Group([alpha, beta, gamma], string.Empty, "General").SelectMany(g => g.Items).ShouldBe([alpha, gamma]);
        // A hidden command is not found by the search either.
        CommandPaletteFilter.Group([alpha, beta, gamma], "beta", "General").ShouldBeEmpty();
        beta.IsVisible = true;
        CommandPaletteFilter.Group([alpha, beta, gamma], string.Empty, "General").SelectMany(g => g.Items).ShouldBe([alpha, beta, gamma]);
    }

    [AvaloniaFact]
    public void The_Command_Palette_Follows_A_Command_That_Is_Hidden_Later()
    {
        var alpha = new CommandPaletteItem { Id = "alpha", Label = "Alpha" };
        var beta = new CommandPaletteItem { Id = "beta", Label = "Beta" };
        var palette = new CommandPalette { Width = 500, Height = 400 };
        palette.Items.AddRange([alpha, beta]);
        var window = TestHelpers.Show(palette);
        try
        {
            Settle();
            palette.VisibleItems.ShouldBe([alpha, beta]);
            beta.IsVisible = false;
            Settle();
            palette.VisibleItems.ShouldBe([alpha]);
            beta.IsVisible = true;
            Settle();
            palette.VisibleItems.ShouldBe([alpha, beta]);
        }
        finally
        {
            window.Close();
        }
    }

    private static List<string> NavLabels(SiteHeader header)
        => header.GetVisualDescendants().OfType<SiteNavButton>().Select(b => b.Label).ToList();

    [AvaloniaFact]
    public void The_Site_Header_Leaves_Out_Hidden_Items_And_Dims_Disabled_Ones()
    {
        var product = new SiteHeaderItem { Value = "product", Label = "Product" };
        var pricing = new SiteHeaderItem { Value = "pricing", Label = "Pricing" };
        var docs = new SiteHeaderItem { Value = "docs", Label = "Docs" };
        var header = new SiteHeader { Variant = SiteHeaderVariant.Simple, Width = 1100 };
        header.Items.Add(product);
        header.Items.Add(pricing);
        header.Items.Add(docs);
        var window = TestHelpers.Show(header);
        try
        {
            window.Width = 1200;
            Settle();
            NavLabels(header).ShouldBe(["Product", "Pricing", "Docs"]);

            pricing.IsVisible = false;
            Settle();
            NavLabels(header).ShouldBe(["Product", "Docs"]);
            pricing.IsVisible = true;
            Settle();
            NavLabels(header).ShouldBe(["Product", "Pricing", "Docs"]);

            pricing.IsEnabled = false;
            Settle();
            header.GetVisualDescendants().OfType<SiteNavButton>().Single(b => b.Label == "Pricing").IsEnabled.ShouldBeFalse();
            header.GetVisualDescendants().OfType<SiteNavButton>().Single(b => b.Label == "Docs").IsEnabled.ShouldBeTrue();
        }
        finally
        {
            window.Close();
        }
    }
}
