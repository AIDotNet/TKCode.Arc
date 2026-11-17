using Avalonia.Headless.XUnit;
using Avalonia.Media;
using Shouldly;

namespace TKCode.Arc.Tests.AdvancedCharts;

/// <summary>The cell text cache: layouts are reused while nothing changes, rebuilt when it does, and Chinese text falls back to another font.</summary>
public class DataGridTextCacheTests
{
    private static readonly Typeface Face = new(FontFamily.Default);

    private static Avalonia.Media.TextFormatting.TextLayout? Get(DataGridTextCache cache, object owner, int version, string text, double maxWidth = 200,
                                                                bool tabular = false, int style = 0) =>
        cache.Get(owner, 0, version, style, text, static s => s, Face, 13, Brushes.Black, tabular, maxWidth);

    [AvaloniaFact]
    public void A_Layout_Is_Reused_Until_The_Version_The_Width_Or_The_Style_Changes()
    {
        var cache = new DataGridTextCache();
        var owner = new object();
        cache.BeginFrame();
        var first = Get(cache, owner, 1, "Lumen Labs")!;
        first.Height.ShouldBeGreaterThan(0);
        ReferenceEquals(Get(cache, owner, 1, "Lumen Labs"), first).ShouldBeTrue();
        cache.BeginFrame();
        ReferenceEquals(Get(cache, owner, 1, "Lumen Labs"), first).ShouldBeTrue();
        ReferenceEquals(Get(cache, owner, 1, "Lumen Labs", maxWidth: 200.3), first).ShouldBeTrue();
        ReferenceEquals(Get(cache, owner, 1, "Lumen Labs", maxWidth: 120), first).ShouldBeFalse();
        var version = Get(cache, owner, 2, "Basalt Freight")!;
        cache.TextOf(owner, 0).ShouldBe("Basalt Freight");
        ReferenceEquals(Get(cache, owner, 2, "Basalt Freight", style: 1), version).ShouldBeFalse();
    }

    [AvaloniaFact]
    public void Empty_Text_Has_No_Layout_And_Long_Text_Is_Trimmed_To_The_Width()
    {
        var cache = new DataGridTextCache();
        var owner = new object();
        cache.BeginFrame();
        Get(cache, owner, 1, string.Empty).ShouldBeNull();
        var trimmed = Get(cache, new object(), 1, "A very long account name that cannot fit", maxWidth: 80)!;
        trimmed.WidthIncludingTrailingWhitespace.ShouldBeLessThanOrEqualTo(80.5);
        var natural = Get(cache, new object(), 1, "Lumen Labs", maxWidth: 400)!;
        natural.WidthIncludingTrailingWhitespace.ShouldBeGreaterThan(40);
    }

    [AvaloniaFact]
    public void Chinese_Text_Is_Shaped_With_A_Fallback_Font_And_Trimmed()
    {
        var cache = new DataGridTextCache();
        cache.BeginFrame();
        var chinese = Get(cache, new object(), 1, "北京蓝岩科技有限公司", maxWidth: 400)!;
        chinese.WidthIncludingTrailingWhitespace.ShouldBeGreaterThan(40);
        var trimmed = Get(cache, new object(), 1, "北京蓝岩科技有限公司华东区分公司二期项目", maxWidth: 90)!;
        trimmed.WidthIncludingTrailingWhitespace.ShouldBeLessThanOrEqualTo(90.5);
    }

    [AvaloniaFact]
    public void Entries_Unused_For_A_Frame_Are_Dropped_And_Clear_Empties_The_Cache()
    {
        var cache = new DataGridTextCache();
        var kept = new object();
        var dropped = new object();
        cache.BeginFrame();
        Get(cache, kept, 1, "kept");
        Get(cache, dropped, 1, "dropped");
        cache.Count.ShouldBe(2);
        cache.BeginFrame();
        Get(cache, kept, 1, "kept");
        cache.BeginFrame();
        cache.TextOf(dropped, 0).ShouldBeNull();
        cache.Clear();
        cache.Count.ShouldBe(0);
    }
}
