using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Avalonia.VisualTree;
using TKCode.Arc;
using Xunit;

namespace ArcLibrary.Tests;

public class StatTileTests
{
    private static StatTile Show(StatTile tile)
    {
        var window = new Window { Content = tile, Width = 400, Height = 300 };
        window.Show();
        Dispatcher.UIThread.RunJobs();
        return tile;
    }

    [AvaloniaFact]
    public void The_theme_is_found_and_applied()
    {
        var tile = Show(new StatTile { Title = "Revenue", Value = "$12,400", Caption = "+8% this month" });

        // The template comes from Themes/StatTileTheme.axaml, the sizes from StatTileToken.
        Assert.NotEmpty(tile.GetVisualChildren());
        Assert.Equal(new Thickness(16, 14), tile.Padding);
        Assert.Equal(160, tile.MinWidth);
        var texts = tile.GetVisualDescendants().OfType<TextBlock>().Select(t => t.Text).ToList();
        Assert.Contains("$12,400", texts);
    }

    [AvaloniaFact]
    public void A_token_override_changes_the_tile()
    {
        var tile = Show(new StatTile { Title = "Revenue", Value = "$12,400" });

        ArcTheme.Apply(t => t.Token("ArcLibraryIdentifierStatTile", "Padding", "24"));
        Dispatcher.UIThread.RunJobs();

        Assert.Equal(new Thickness(24), tile.Padding);
    }

    [AvaloniaFact]
    public void The_caption_hides_when_empty()
    {
        var tile = Show(new StatTile { Title = "Revenue", Value = "$12,400" });

        var caption = tile.GetVisualDescendants().OfType<TextBlock>().Last();
        Assert.False(caption.IsVisible);
    }
}
