using StreamMark.Mermaid;
using Xunit;

namespace StreamMark.Avalonia.Tests.Mermaid;

public sealed class CssColorResolverTests
{
    private static readonly Dictionary<string, string> Vars = new()
    {
        ["bg"] = "#000000",
        ["fg"] = "#ffffff",
        ["accent"] = "#ff0000",
        ["_line"] = "var(--line, color-mix(in srgb, var(--fg) 50%, var(--bg)))",
        ["fs-xs"] = "0.75rem",
    };

    [Theory]
    [InlineData("color-mix(in srgb, #ffffff 50%, #000000)", "#808080")]
    [InlineData("color-mix(in srgb, #ffffff 10%, #000000)", "#1a1a1a")]
    [InlineData("color-mix(in srgb, #fff, #000)", "#808080")]
    [InlineData("color-mix(in srgb, #ff0000 25%, #0000ff 75%)", "#4000bf")]
    [InlineData("color-mix(in srgb, #ff0000 20%, #0000ff 20%)", "rgba(128,0,128,0.4)")]
    [InlineData("color-mix(in srgb, var(--fg) 30%, var(--bg))", "#4d4d4d")]
    [InlineData("color-mix(in srgb, var(--accent, var(--fg)) 100%, var(--bg))", "#ff0000")]
    [InlineData("color-mix(in srgb, var(--missing, var(--fg)) 100%, var(--bg))", "#ffffff")]
    [InlineData("color-mix(in srgb, #000000 50%, transparent)", "rgba(0,0,0,0.5)")]
    [InlineData("color-mix(in srgb, color-mix(in srgb, var(--bg) 35%, black) 55%, transparent)", "rgba(0,0,0,0.55)")]
    [InlineData("var(--_line)", "#808080")]
    [InlineData("var(--fs-xs)", "12")]
    [InlineData("drop-shadow(0 1px 2px color-mix(in srgb, #ffffff 50%, transparent))", "drop-shadow(0 1px 2px rgba(255,255,255,0.5))")]
    public void Substitute_ResolvesToLiterals(string input, string expected)
    {
        Assert.Equal(expected, CssColorResolver.Substitute(input, Vars, 0));
    }

    [Fact]
    public void Resolve_ReadsRootStyleAndSvgRule_IncludingLastDeclarationWithoutSemicolon()
    {
        const string svg = """
            <svg xmlns="http://www.w3.org/2000/svg" width="10" height="10" style="--bg:#000000;--fg:#ffffff;--accent:#00ff00">
            <style>
              svg {
                --_text: var(--fg);
                --_fill: color-mix(in srgb, var(--accent, var(--fg)) 50%, var(--bg));
              }
            </style>
            <rect fill="var(--_fill)" stroke="var(--_text)" />
            </svg>
            """;

        var resolved = CssColorResolver.Resolve(svg);

        Assert.Contains("fill=\"#008000\"", resolved);
        Assert.Contains("stroke=\"#ffffff\"", resolved);
        Assert.DoesNotContain("var(", resolved);
        Assert.DoesNotContain("color-mix(", resolved);
    }

    [Fact]
    public void IdentifierEndingInVar_IsNotTreatedAsFunction()
    {
        Assert.Equal("myvar(1)", CssColorResolver.Substitute("myvar(1)", Vars, 0));
    }

    [Fact]
    public void UnknownVariableWithoutFallback_BecomesNone()
    {
        Assert.Equal("none", CssColorResolver.Substitute("var(--nope)", Vars, 0));
    }

    [Fact]
    public void CyclicVariables_Terminate()
    {
        var vars = new Dictionary<string, string> { ["a"] = "var(--b)", ["b"] = "var(--a)" };
        var result = CssColorResolver.Substitute("var(--a)", vars, 0);
        Assert.NotNull(result);
    }
}
