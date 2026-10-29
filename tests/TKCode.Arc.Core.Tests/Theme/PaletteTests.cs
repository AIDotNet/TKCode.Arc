using Avalonia.Media;
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
        var derived = ArcPaletteGenerator.Generate(Color.Parse("#0562EF"), isDark: true)[5];
        ArcPalette.Resolve(derived, isDark: true).Preset.ShouldBe("blue");
    }

    // @ant-design/colors 7.2.1: presetPalettes / presetDarkPalettes (seed, light, dark), plus generate() for Arc's seeds.
    [Theory]
    [InlineData("#f5222d", "#fff1f0 #ffccc7 #ffa39e #ff7875 #ff4d4f #f5222d #cf1322 #a8071a #820014 #5c0011", "#2a1215 #431418 #58181c #791a1f #a61d24 #d32029 #e84749 #f37370 #f89f9a #fac8c3")]
    [InlineData("#fa541c", "#fff2e8 #ffd8bf #ffbb96 #ff9c6e #ff7a45 #fa541c #d4380d #ad2102 #871400 #610b00", "#2b1611 #441d12 #592716 #7c3118 #aa3e19 #d84a1b #e87040 #f3956a #f8b692 #fad4bc")]
    [InlineData("#fa8c16", "#fff7e6 #ffe7ba #ffd591 #ffc069 #ffa940 #fa8c16 #d46b08 #ad4e00 #873800 #612500", "#2b1d11 #442a11 #593815 #7c4a15 #aa6215 #d87a16 #e89a3c #f3b765 #f8cf8d #fae3b7")]
    [InlineData("#faad14", "#fffbe6 #fff1b8 #ffe58f #ffd666 #ffc53d #faad14 #d48806 #ad6800 #874d00 #613400", "#2b2111 #443111 #594214 #7c5914 #aa7714 #d89614 #e8b339 #f3cc62 #f8df8b #faedb5")]
    [InlineData("#fadb14", "#feffe6 #ffffb8 #fffb8f #fff566 #ffec3d #fadb14 #d4b106 #ad8b00 #876800 #614700", "#2b2611 #443b11 #595014 #7c6e14 #aa9514 #d8bd14 #e8d639 #f3ea62 #f8f48b #fafab5")]
    [InlineData("#a0d911", "#fcffe6 #f4ffb8 #eaff8f #d3f261 #bae637 #a0d911 #7cb305 #5b8c00 #3f6600 #254000", "#1f2611 #2e3c10 #3e4f13 #536d13 #6f9412 #8bbb11 #a9d134 #c9e75d #e4f88b #f0fab5")]
    [InlineData("#52c41a", "#f6ffed #d9f7be #b7eb8f #95de64 #73d13d #52c41a #389e0d #237804 #135200 #092b00", "#162312 #1d3712 #274916 #306317 #3c8618 #49aa19 #6abe39 #8fd460 #b2e58b #d5f2bb")]
    [InlineData("#13c2c2", "#e6fffb #b5f5ec #87e8de #5cdbd3 #36cfc9 #13c2c2 #08979c #006d75 #00474f #002329", "#112123 #113536 #144848 #146262 #138585 #13a8a8 #33bcb7 #58d1c9 #84e2d8 #b2f1e8")]
    [InlineData("#1677ff", "#e6f4ff #bae0ff #91caff #69b1ff #4096ff #1677ff #0958d9 #003eb3 #002c8c #001d66", "#111a2c #112545 #15325b #15417e #1554ad #1668dc #3c89e8 #65a9f3 #8dc5f8 #b7dcfa")]
    [InlineData("#2f54eb", "#f0f5ff #d6e4ff #adc6ff #85a5ff #597ef7 #2f54eb #1d39c4 #10239e #061178 #030852", "#131629 #161d40 #1c2755 #203175 #263ea0 #2b4acb #5273e0 #7f9ef3 #a8c1f8 #d2e0fa")]
    [InlineData("#722ed1", "#f9f0ff #efdbff #d3adf7 #b37feb #9254de #722ed1 #531dab #391085 #22075e #120338", "#1a1325 #24163a #301c4d #3e2069 #51258f #642ab5 #854eca #ab7ae0 #cda8f0 #ebd7fa")]
    [InlineData("#eb2f96", "#fff0f6 #ffd6e7 #ffadd2 #ff85c0 #f759ab #eb2f96 #c41d7f #9e1068 #780650 #520339", "#291321 #40162f #551c3b #75204f #a02669 #cb2b83 #e0529c #f37fb7 #f8a8cc #fad2e3")]
    [InlineData("#666666", "#a6a6a6 #999999 #8c8c8c #808080 #737373 #666666 #404040 #1a1a1a #000000 #000000", "#151515 #1f1f1f #2d2d2d #393939 #494949 #5a5a5a #6a6a6a #7b7b7b #888888 #969696")]
    [InlineData("#353535", "#757575 #696969 #5c5c5c #4f4f4f #424242 #353535 #0f0f0f #000000 #000000 #000000", "#111111 #131313 #1e1e1e #232323 #292929 #303030 #3d3d3d #4c4c4c #5a5a5a #676767")]
    [InlineData("#7747FF", "#f6f0ff #f3ebff #d8c2ff #bb99ff #9b70ff #7747ff #5932d9 #3d20b3 #27128c #180c66", "#1a162c #251c45 #32235b #412b7e #5435ad #683fdc #8e67e8 #b392f3 #d2bdf8 #efe7fa")]
    [InlineData("#0562EF", "#e6f4ff #a8d7ff #80bfff #57a5ff #2d87fc #0562ef #004ac9 #0036a3 #00257d #001757", "#111929 #0f2241 #102b56 #0d3777 #0a47a2 #0756ce #2b7ce5 #549ef3 #7dbaf8 #a5d3fa")]
    [InlineData("#0DB879", "#dff7ea #a7ebc9 #7adeaf #52d19a #2dc488 #0db879 #039162 #006b4b #004532 #001f17", "#11211c #103328 #124532 #115e41 #0f7f56 #0e9f6a #2bb27c #4fc893 #77d8aa #a4e7c5")]
    [InlineData("#F3AD20", "#fffced #fff3c4 #ffe89c #ffda73 #ffc94a #f3ad20 #cc8710 #a66505 #804800 #593000", "#2a2012 #423113 #574218 #785919 #a5771c #d2961e #e8b745 #f3d06e #f8e298 #faefc0")]
    [InlineData("#F48120", "#fff8ed #ffe7c4 #ffd29c #ffbb73 #ffa14a #f48120 #cf6011 #a84405 #822e00 #5c1d00", "#2a1b12 #432713 #573518 #794519 #a65b1c #d2711e #e89345 #f3b36e #f8cc98 #fae3c0")]
    [InlineData("#F15F55", "#fff3f0 #fff3f0 #ffdbd4 #ffb6ab #ff8f82 #f15f55 #cc423d #a62929 #80191d #591116", "#2a1717 #42201e #562b28 #773631 #a4453e #d0544b #e88377 #f3aea3 #f8d5ce #faefec")]
    [InlineData("#ED4E9D", "#fff0f5 #fff0f5 #ffcfe2 #ffa6cc #fa7ab6 #ed4e9d #c73884 #a1256b #7a1652 #540e3b", "#291721 #411d30 #55253d #762e52 #a13a6d #cc4588 #e370a6 #f39fc3 #f8c9dc #faecf1")]
    [InlineData("#FFFFFF", "#ffffff #ffffff #ffffff #ffffff #ffffff #ffffff #d9d9d9 #b3b3b3 #8c8c8c #666666", "#2c2c2c #454545 #5b5b5b #7e7e7e #adadad #dcdcdc #e8e8e8 #f3f3f3 #f8f8f8 #fafafa")]
    [InlineData("#000000", "#404040 #333333 #262626 #1a1a1a #0d0d0d #000000 #000000 #000000 #000000 #000000", "#111111 #0f0f0f #0e0e0e #0b0b0b #070707 #030303 #0e0e0e #1a1a1a #252525 #323232")]
    [InlineData("#00FF00", "#eaffe6 #afffa3 #88ff7a #5dff52 #30ff29 #00ff00 #00d907 #00b30c #008c0e #00660e", "#112c13 #0f4511 #0e5b0e #0b7e0b #07ad07 #03dc03 #2de827 #59f34f #85f877 #acfaa0")]
    public void Palette_Generator_Matches_Ant_Design_Colors(string seed, string light, string dark)
    {
        Hex(ArcPaletteGenerator.Generate(Color.Parse(seed), isDark: false)).ShouldBe(light);
        Hex(ArcPaletteGenerator.Generate(Color.Parse(seed), isDark: true)).ShouldBe(dark);
    }

    private static string Hex(Color[] colors) => string.Join(" ", colors.Select(c => $"#{c.R:x2}{c.G:x2}{c.B:x2}"));

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
