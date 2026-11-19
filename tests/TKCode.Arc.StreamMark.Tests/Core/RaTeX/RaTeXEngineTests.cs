using StreamMark.RaTeX;
using Xunit;

namespace StreamMark.Core.Tests.RaTeX;

/// <summary>需要本机已构建 RaTeX 原生库（native/build.sh）。</summary>
public sealed class RaTeXEngineTests
{
    [Theory]
    [InlineData(@"\frac{1}{2}")]
    [InlineData(@"\dfrac{a}{b} + \boldsymbol{x}")]
    [InlineData(@"\ce{H2SO4 + 2NaOH -> Na2SO4 + 2H2O}")]
    [InlineData(@"\begin{align} a &= b \\ c &= d \end{align}")]
    [InlineData(@"\xrightarrow{f} \overset{!}{=} \boxed{x}")]
    public void CommandsThatCSharpMathRejected_Render(string latex)
    {
        var result = RaTeXEngine.Layout(latex, displayMode: true);
        Assert.True(result.IsSuccess, result.Error);
        Assert.NotEmpty(result.DisplayList!.Items);
        Assert.True(result.DisplayList.Width > 0);
    }

    [Fact]
    public void InlineMode_ReportsBaselineMetrics()
    {
        var result = RaTeXEngine.Layout("x^2", displayMode: false);
        Assert.True(result.IsSuccess, result.Error);
        Assert.True(result.DisplayList!.Height > 0);
        Assert.Contains(result.DisplayList.Items, i => i.Type == "GlyphPath" && !string.IsNullOrEmpty(i.Font));
    }

    [Theory]
    [InlineData(@"\frac{1}{")]
    [InlineData(@"\left( x")]
    public void IncompleteInput_ReturnsError(string latex)
    {
        var result = RaTeXEngine.Layout(latex, displayMode: false);
        Assert.False(result.IsSuccess);
        Assert.False(string.IsNullOrWhiteSpace(result.Error));
    }

    [Fact]
    public void ConcurrentCalls_AreSafe()
    {
        Parallel.For(0, 200, i =>
        {
            var result = RaTeXEngine.Layout($@"\sum_{{k=1}}^{{{i}}} k^2", displayMode: i % 2 == 0);
            Assert.True(result.IsSuccess, result.Error);
        });
    }
}
