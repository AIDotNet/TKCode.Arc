using System.Collections.Immutable;
using TKCode.Arc.CodeTokens;

namespace TKCode.Arc.Tests.Tables;

/// <summary>
/// The highlighter the Special components color code with (#147): CodeBlock's roles and colors from the installed provider
/// (UseArcHighlighting()), plain lines without one, and no Markdown package in between.
/// </summary>
public class CodeTokenHighlighterTests
{
    [Fact]
    public void Colors_Code_With_The_Installed_Provider_In_CodeBlock_Colors()
    {
        CodeTokenHighlighter.Shared.SupportsLanguage("csharp").ShouldBeTrue();
        using var session = CodeTokenHighlighter.Shared.CreateSession("csharp", HighlightThemeKind.Dark);
        var lines = session.Update("// note\nvar x = 1;");
        lines.Count.ShouldBe(2);
        var comment = lines[0].ShouldHaveSingleItem();
        comment.Style.ShouldBe(HighlightFontStyle.Italic);
        comment.Foreground.ShouldBe(CodeBlockSyntaxColors.Get(CodeBlockSyntaxKind.Comment, isDarkMode: true).ToUInt32());
        lines[1].ShouldNotBeEmpty();
    }

    [Fact]
    public void Unchanged_Lines_Keep_Their_Tokens()
    {
        using var session = CodeTokenHighlighter.Shared.CreateSession("csharp", HighlightThemeKind.Light);
        var first = session.Update("var a = 1;\nvar b = 2;");
        var second = session.Update("var a = 1;\nvar b = 3;");
        second[0].ShouldBe(first[0]);
    }

    [Fact]
    public void Without_A_Provider_Code_Is_Plain()
    {
        var provider = CodeBlockHighlighter.Provider;
        CodeBlockHighlighter.Provider = null;
        try
        {
            CodeTokenHighlighter.Shared.SupportsLanguage("csharp").ShouldBeFalse();
            using var session = CodeTokenHighlighter.Shared.CreateSession("csharp", HighlightThemeKind.Light);
            var lines = session.Update("var a = 1;\nvar b = 2;\n");
            lines.Count.ShouldBe(3);
            lines.ShouldAllBe(line => line.IsEmpty);
        }
        finally
        {
            CodeBlockHighlighter.Provider = provider;
        }
    }

    [Fact]
    public void ArcHighlighting_SharedHighlighter_Exposes_Shared_LightPlus_Engine()
    {
        var engine = ArcHighlighting.SharedHighlighter;
        engine.ShouldBeSameAs(TextMateCodeHighlighter.Engine);
        engine.SupportsLanguage("csharp").ShouldBeTrue();
        using var session = engine.CreateSession("csharp", StreamMark.Core.Highlighting.HighlightThemeKind.Light);
        var lines = session.Update("public sealed class Demo { }");
        lines.Count.ShouldBe(1);
        lines[0].ShouldNotBeEmpty();
    }
}
