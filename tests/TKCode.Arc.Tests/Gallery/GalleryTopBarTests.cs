using TKCode.Arc.Gallery.Localization;
using TKCode.Arc.Gallery.Shell;
using TKCode.Arc.Motion;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Avalonia.VisualTree;

namespace TKCode.Arc.Tests.Gallery;

/// <summary>
/// The top bar fits its brand, menu and buttons into any width without overlap (GalleryTopBar.FitLevel). The headless test
/// font is not the real one, so these tests check the rules rather than the level at a given width; the levels for real
/// fonts are in design/engineering/gallery-app.md.
/// </summary>
public class GalleryTopBarTests
{
    private static readonly string[] Languages = ["en-US", "zh-CN", "zh-TW"];

    /// <summary>The bar's pieces that are shown: the brand, the section menu and each button on the right, in bar coordinates.</summary>
    private static List<(string Name, Rect Bounds)> Pieces(GalleryTopBar bar)
    {
        var pieces = new List<(string, Rect)>();
        void Add(string name, Control piece)
        {
            if (piece.IsVisible && piece.TranslatePoint(default, bar) is { } origin)
            {
                pieces.Add((name, new Rect(origin, piece.Bounds.Size)));
            }
        }
        foreach (var part in bar.Header.GetVisualDescendants().OfType<Control>())
        {
            if (part.Name == "PART_BrandSlot")
            {
                Add("brand", part);
            }
            else if (part.Name == "PART_NavList")
            {
                Add("menu", part);
            }
        }
        for (var i = 0; i < bar.Buttons.Children.Count; i++)
        {
            Add($"button {i}", bar.Buttons.Children[i]);
        }
        return pieces;
    }

    private static void SettleLayout()
    {
        for (var i = 0; i < 4; i++)
        {
            Dispatcher.UIThread.RunJobs();
        }
    }

    private static void ShouldFit(GalleryTopBar bar, string because)
    {
        var pieces = Pieces(bar).OrderBy(p => p.Bounds.X).ToList();
        pieces.ShouldContain(p => p.Name == "brand", because);
        pieces.ShouldContain(p => p.Name == "button 4", "the light / dark toggle always stays; " + because);
        foreach (var (name, bounds) in pieces)
        {
            bounds.X.ShouldBeGreaterThanOrEqualTo(0, $"{name} {because}");
            bounds.Right.ShouldBeLessThanOrEqualTo(bar.Bounds.Width + 0.5, $"{name} {because}");
        }
        for (var i = 1; i < pieces.Count; i++)
        {
            pieces[i].Bounds.X.ShouldBeGreaterThanOrEqualTo(pieces[i - 1].Bounds.Right - 0.5,
                $"{pieces[i].Name} overlaps {pieces[i - 1].Name} {because}");
        }
    }

    [AvaloniaFact]
    public void The_Bar_Fits_Every_Window_Width_In_Every_Language()
    {
        foreach (var language in Languages)
        {
            GalleryText.SetLanguage(language);
            var levels = new List<int>();
            // From wide to the window's minimum width: the bar gives up room in order, never the other way round.
            foreach (var width in new double[] { 1920, 1728, 1480, 1280, 1100, 1024, 900 })
            {
                var window = new GalleryWindow { Width = width, Height = 600 };
                ArcMotion.SetLevel(window, ArcMotionLevel.Off);
                window.Show();
                try
                {
                    SettleLayout();
                    var bar = window.TopBar;
                    ShouldFit(bar, $"at {width} px in {language} (level {bar.FitLevel})");
                    levels.Add(bar.FitLevel);
                }
                finally
                {
                    window.Close();
                }
            }
            GalleryText.SetLanguage("en-US");
            levels.ShouldBe(levels.Order().ToList(), $"{language}: {string.Join(", ", levels)}");
            levels[^1].ShouldBeLessThan(6, $"{language}: the narrowest window still has the GitHub button");
        }
    }

    [AvaloniaFact]
    public void A_Phone_Wide_Page_Keeps_The_Brand_And_The_Toggles()
    {
        // The web host has no minimum width.
        var shell = new GalleryShell(null);
        var window = TestHelpers.Show(shell);
        try
        {
            window.Width = 390;
            window.Height = 700;
            SettleLayout();
            shell.TopBar.FitLevel.ShouldBeGreaterThanOrEqualTo(6);
            ShouldFit(shell.TopBar, "at 390 px");
            shell.TopBar.LanguageButton.IsEffectivelyVisible.ShouldBeTrue();

            // Widening the page gives the room back.
            window.Width = 1920;
            SettleLayout();
            shell.TopBar.FitLevel.ShouldBeLessThan(4);
            ShouldFit(shell.TopBar, "at 1920 px");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Icon_Only_Buttons_Keep_Their_Names_As_Tooltips()
    {
        var shell = new GalleryShell(null);
        var window = TestHelpers.Show(shell);
        try
        {
            window.Width = 390;
            window.Height = 700;
            SettleLayout();
            var right = shell.TopBar.Buttons;
            var named = right.Children.OfType<Button>().Where(b => b.IsVisible)
                             .Select(b => Tooltip.GetTip(b) as string).ToList();
            named.ShouldContain("Search");
            named.ShouldContain("Language");
            named.ShouldContain("Appearance");

            window.Width = 1920;
            SettleLayout();
            // A labelled button has no tooltip that repeats its label; the language button keeps "Language".
            var wide = right.Children.OfType<Button>().Select(b => Tooltip.GetTip(b) as string).ToList();
            wide.Contains("Appearance").ShouldBe(shell.TopBar.FitLevel >= 1);
            wide.Contains("Search").ShouldBe(shell.TopBar.FitLevel >= 5);
            wide.ShouldContain("Language");
        }
        finally
        {
            window.Close();
        }
    }
}
