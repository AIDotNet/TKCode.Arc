using Avalonia.Media;
using TKCode.Arc.Primitives;

namespace TKCode.Arc.Core.Tests.Theme;

/// <summary>The shared blending and size formatting controls use instead of their own copies (#148).</summary>
public class ArcColorTests
{
    [Fact]
    public void Lerp_Mixes_Every_Channel_And_Clamps()
    {
        var from = Color.FromArgb(0, 0, 100, 200);
        var to = Color.FromArgb(255, 255, 0, 100);
        ArcColor.Lerp(from, to, 0.5).ShouldBe(Color.FromArgb(128, 128, 50, 150));
        ArcColor.Lerp(from, to, -1).ShouldBe(from);
        ArcColor.Lerp(from, to, 2).ShouldBe(to);
        ArcColor.Lerp(from, to, double.NaN).ShouldBe(from);
    }

    [Fact]
    public void LerpOklab_Is_Oklchs_Mix_Clamped()
    {
        var a = Colors.Red;
        var b = Colors.Blue;
        ArcColor.LerpOklab(a, b, 0.3).ShouldBe(Oklch.MixOklab(a, b, 0.3));
        ArcColor.LerpOklab(a, b, 5).ShouldBe(Oklch.MixOklab(a, b, 1));
    }

    [Fact]
    public void WithOpacity_Scales_Alpha()
    {
        ArcColor.WithOpacity(Color.FromArgb(200, 1, 2, 3), 0.5).ShouldBe(Color.FromArgb(100, 1, 2, 3));
        ArcColor.WithOpacity(Colors.Black, 3).A.ShouldBe((byte)255);
    }

    [Theory]
    [InlineData(-5, "0 B")]
    [InlineData(512, "512 B")]
    [InlineData(1023, "1023 B")]
    [InlineData(1024, "1 KB")]
    [InlineData(18 * 1024 + 600, "19 KB")]
    [InlineData(2516582, "2.4 MB")]
    [InlineData(1610612736, "1.5 GB")]
    public void Bytes_Uses_Binary_Units(long bytes, string expected) => ArcFormatting.Bytes(bytes).ShouldBe(expected);
}
