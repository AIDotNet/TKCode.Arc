using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Threading;

namespace TKCode.Arc.Tests.Tables;

public class CodeBlockTests
{
    private static List<(string Text, CodeBlockSyntaxKind Kind)> Tokens(string code, string language)
    {
        var spans = new List<CodeBlockSyntaxSpan>();
        CodeBlockTokenizer.Tokenize(code, language, spans);
        return spans.Select(s => (code.Substring(s.Start, s.Length), s.Kind)).ToList();
    }

    private static Avalonia.Controls.Window Show(Control control, double width = 800)
    {
        var window = TestHelpers.Show(control);
        window.Width = width;
        window.Height = 800;
        Dispatcher.UIThread.RunJobs();
        return window;
    }

    [AvaloniaFact]
    public void Tokenizer_Follows_Arcs_Rules()
    {
        var tokens = Tokens("export function greet(name: string) { return `Hi ${name}` + 42; } // done", "tsx");
        tokens.ShouldContain(("export", CodeBlockSyntaxKind.Keyword));
        tokens.ShouldContain(("function", CodeBlockSyntaxKind.Keyword));
        tokens.ShouldContain(("greet", CodeBlockSyntaxKind.Function));
        tokens.ShouldContain(("name", CodeBlockSyntaxKind.Property)); // tsx: identifier followed by ':'
        tokens.ShouldContain(("string", CodeBlockSyntaxKind.Type));
        tokens.ShouldContain(("`Hi ${name}`", CodeBlockSyntaxKind.String));
        tokens.ShouldContain(("42", CodeBlockSyntaxKind.Number));
        tokens.ShouldContain(("// done", CodeBlockSyntaxKind.Comment));
        tokens.ShouldContain(("{", CodeBlockSyntaxKind.Punctuation));
    }

    [AvaloniaFact]
    public void Tokenizer_Handles_Tags_Json_Keys_And_Unterminated_Quotes()
    {
        Tokens("<Button onClick={go}>", "tsx").ShouldContain(("Button", CodeBlockSyntaxKind.Tag));
        Tokens("</div>", "html").ShouldContain(("div", CodeBlockSyntaxKind.Tag));
        Tokens("<div>", "ts").ShouldNotContain(("div", CodeBlockSyntaxKind.Tag));
        var json = Tokens("{ \"name\": \"Arc\", \"stars\": 12.5 }", "json");
        json.ShouldContain(("\"name\"", CodeBlockSyntaxKind.Property));
        json.ShouldContain(("\"Arc\"", CodeBlockSyntaxKind.String));
        json.ShouldContain(("12.5", CodeBlockSyntaxKind.Number));
        // An unterminated quote is plain text and scanning continues after it.
        Tokens("say \"hello", "tsx").ShouldBe([]);
        // Digits glued to letters are not numbers (\b), "a-b" is one identifier.
        Tokens("123abc", "tsx").ShouldBe([]);
        Tokens("a-b(", "tsx").ShouldContain(("a-b", CodeBlockSyntaxKind.Function));
        Tokens("/* open", "tsx").ShouldBe([("/", CodeBlockSyntaxKind.Punctuation), ("*", CodeBlockSyntaxKind.Punctuation)]);
    }

    [AvaloniaFact]
    public void Tokenizer_Extends_To_CSharp_And_Xaml()
    {
        Tokens("public sealed class Foo : Control", "cs").ShouldContain(("sealed", CodeBlockSyntaxKind.Keyword));
        Tokens("var t = Task.Run(x);", "csharp").ShouldContain(("Task", CodeBlockSyntaxKind.Type));
        var xaml = Tokens("<arc:Button Variant=\"Ghost\" />", "axaml");
        xaml.ShouldContain(("Button", CodeBlockSyntaxKind.Tag));
        xaml.ShouldContain(("Variant", CodeBlockSyntaxKind.Property));
        CodeBlockTokenizer.NormalizeLanguage(".TSX").ShouldBe("tsx");
    }

    [AvaloniaFact]
    public void Defaults_Match_Arc()
    {
        var block = new CodeBlock { Code = "const a = 1;\nconst b = 2;" };
        var window = Show(block);
        try
        {
            block.Language.ShouldBe("tsx");
            block.LineCount.ShouldBe(2);
            var label = TestHelpers.Descendant<CodeBlockHeaderLabel>(block);
            label.FileName.ShouldBe("Source code");
            label.Language.ShouldBe("tsx");
            TestHelpers.Descendant<CopyButton>(block).Label.ShouldBe("Copy code");
            TestHelpers.Descendant<CodeBlockExpandButton>(block).IsVisible.ShouldBeFalse();
            var text = block.TextPresenter!;
            text.SpanCount.ShouldBeGreaterThan(4);
            text.FontSize.ShouldBe(14);
            text.LineHeight.ShouldBe(23.8, 0.01);
            block.CornerRadius.TopLeft.ShouldBe(26);
            Avalonia.Automation.AutomationProperties.GetName(block).ShouldBe("tsx source code");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Syntax_Colors_Come_From_The_Token()
    {
        var block = new CodeBlock { Code = "return 1;" };
        var window = TestHelpers.Show(block);
        try
        {
            TestHelpers.ColorOf(block.KeywordBrush).ShouldBe(Oklch.ToColor(0.48, 0.15, 285));
            TestHelpers.ColorOf(block.StringBrush).ShouldBe(Oklch.ToColor(0.46, 0.13, 150));
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Long_Source_Collapses_And_Expands()
    {
        var code = string.Join("\n", Enumerable.Range(1, 10).Select(i => $"const line{i} = {i};"));
        var block = new CodeBlock { Code = code, IsCollapsible = true, MaxLines = 3, FileName = "lines.ts" };
        var window = Show(block);
        try
        {
            var viewport = TestHelpers.Descendant<CodeBlockViewport>(block);
            viewport.Bounds.Height.ShouldBe(Math.Round(3 * 23.8 + 40), 1);
            var toggle = TestHelpers.Descendant<CodeBlockExpandButton>(block);
            toggle.IsVisible.ShouldBeTrue();
            toggle.CurrentLabel.ShouldBe("Show all 10 lines");
            toggle.Focus();
            window.KeyPressQwerty(PhysicalKey.Enter, RawInputModifiers.None);
            Dispatcher.UIThread.RunJobs();
            block.IsExpanded.ShouldBeTrue();
            toggle.CurrentLabel.ShouldBe("Show fewer lines");
            viewport.Bounds.Height.ShouldBe(10 * 23.8 + 40, 1);
            Avalonia.Automation.AutomationProperties.GetName(block).ShouldBe("lines.ts source code");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Line_Numbers_And_Tabs()
    {
        var block = new CodeBlock { Code = "a\r\n\tb\r\nc", ShowLineNumbers = true };
        var window = TestHelpers.Show(block);
        try
        {
            block.LineCount.ShouldBe(3);
            block.DisplayCode.ShouldBe("a\n  b\nc");
            var numbers = block.GetVisualDescendantsOfType<TextBlock>().First(t => t.Name == "PART_LineNumbers");
            numbers.IsVisible.ShouldBeTrue();
            numbers.Text.ShouldBe("1\n2\n3");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Narrow_Block_Uses_The_Compact_Layout()
    {
        var block = new CodeBlock { Code = "x", Width = 360 };
        var window = TestHelpers.Show(block);
        try
        {
            block.Classes.ShouldContain(":compact");
            TestHelpers.Descendant<CopyButton>(block).IsIconOnly.ShouldBeTrue();
            block.TextPresenter!.FontSize.ShouldBe(12);
            block.TextPresenter!.Padding.Left.ShouldBe(16);
        }
        finally
        {
            window.Close();
        }
    }
}

internal static class CodeBlockTestExtensions
{
    public static IEnumerable<T> GetVisualDescendantsOfType<T>(this Avalonia.Visual root)
        => Avalonia.VisualTree.VisualExtensions.GetVisualDescendants(root).OfType<T>();
}
