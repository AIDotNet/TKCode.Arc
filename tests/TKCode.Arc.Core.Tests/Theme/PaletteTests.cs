using AtomUI.Theme.Algorithms;
using Avalonia.Media;
using Avalonia.Styling;
using Shouldly;

namespace TKCode.Arc.Core.Tests.Theme;

public class PaletteTests
{
    [Fact]
    public void Preset_Seeds_Resolve_To_Hand_Tuned_Tables()
    {
        var violetLight = ArcPalette.Resolve(Color.Parse("#7747FF"), isDark: false);
        violetLight.Accent.ShouldBe(Color.Parse("#7747FF"));
        violetLight.AccentStrong.ShouldBe(Color.Parse("#5528CE"));
        violetLight.Preset.ShouldBe("violet");

        var violetDark = ArcPalette.Resolve(Color.Parse("#7747FF"), isDark: true);
        violetDark.Accent.ShouldBe(Color.Parse("#A78BFF"));
        violetDark.ControlOn.ShouldBe(Color.Parse("#8A66FF"));
    }

    [Fact]
    public void Dark_Derived_Root_Primary_Maps_Back_To_The_Preset()
    {
        var derived = PaletteGenerator.GeneratePalette(Color.Parse("#0562EF"), new PaletteGenerateOption { ThemeVariant = ThemeVariant.Dark })[5];
        ArcPalette.Resolve(derived, isDark: true).Preset.ShouldBe("blue");
    }

    [Fact]
    public void AtomUI_Default_Primary_Maps_To_Neutral()
    {
        ArcPalette.Resolve(Color.Parse("#1677FF"), isDark: false).Preset.ShouldBe("neutral");
        ArcPalette.Resolve(Color.Parse("#1677FF"), isDark: false).Accent.ShouldBe(Color.Parse("#353535"));
    }

    [Theory]
    [InlineData("#E11D48")]
    [InlineData("#14B8A6")]
    [InlineData("#FACC15")]
    [InlineData("#4F46E5")]
    public void Custom_Seeds_Keep_Foreground_And_Glyph_Contrast(string seed)
    {
        foreach (var dark in new[] { false, true })
        {
            var colors = ArcPalette.Resolve(Color.Parse(seed), dark);
            colors.Preset.ShouldBe("custom");
            Oklch.Contrast(colors.AccentForeground, colors.Accent).ShouldBeGreaterThanOrEqualTo(3.0);
            Oklch.Contrast(colors.ControlGlyph, colors.ControlOn).ShouldBeGreaterThanOrEqualTo(2.0);
        }
    }

    [Fact]
    public void Oklch_Round_Trips_In_Gamut_Colors()
    {
        foreach (var hex in new[] { "#7747FF", "#0DB879", "#F3AD20", "#585858" })
        {
            var c = Color.Parse(hex);
            var (l, ch, h) = Oklch.FromColor(c);
            Oklch.ToColor(l, ch, h).ShouldBe(c);
        }
    }

    [Fact]
    public void Neutral_Foundation_Values_Match_The_Catalog()
    {
        Oklch.ToColor(0.46, 0, 0).ShouldBe(Color.Parse("#585858"));
        Oklch.ToColor(0.19, 0, 0).ShouldBe(Color.Parse("#141414"));
    }
}
