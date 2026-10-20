using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Threading;

namespace TKCode.Arc.Tests.Tables;

public class CodeBlockTests
{
    /// <summary>Highlights <paramref name="code"/> synchronously through a TextMate session, as CodeBlock does.</summary>
    private static Highlighted Highlight(string code, string language)
    {
        var spans = new List<CodeBlockSyntaxSpan>();
        using var session = CodeBlockHighlighter.CreateSession(language);
        session.ShouldNotBeNull(language);
        CodeBlockHighlighter.AppendSpans(code, session.Update(code), CodeBlockHighlighter.FamilyOf(language), spans);
        return new Highlighted(code, spans);
    }

    private sealed record Highlighted(string Code, List<CodeBlockSyntaxSpan> Spans)
    {
        /// <summary>The role of the <paramref name="occurrence"/>-th <paramref name="text"/>; every character must share it.</summary>
        public CodeBlockSyntaxKind this[string text, int occurrence = 0]
        {
            get
            {
                var index = -1;
                for (var i = 0; i <= occurrence; i++)
                {
                    index = Code.IndexOf(text, index + 1, StringComparison.Ordinal);
                    index.ShouldBeGreaterThanOrEqualTo(0, text);
                }
                var kinds = Enumerable.Range(index, text.Length).Select(At).Distinct().ToList();
                kinds.Count.ShouldBe(1, $"'{text}' spans {string.Join(", ", kinds)}");
                return kinds[0];
            }
        }

        private CodeBlockSyntaxKind At(int offset)
        {
            foreach (var span in Spans)
            {
                if (offset >= span.Start && offset < span.Start + span.Length)
                {
                    return span.Kind;
                }
            }
            return CodeBlockSyntaxKind.None;
        }
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
    public void CSharp_Roles()
    {
        var cs = Highlight("""
            public sealed class Cart : Control
            {
                // Totals update when an item changes.
                private int _count = 42;
                public string Name { get; set; } = "Arc";
                public Task<int> RunAsync(string name) => Task.FromResult(name.Length + 1);
            }
            """, "cs");
        cs["public"].ShouldBe(CodeBlockSyntaxKind.Keyword);
        cs["sealed"].ShouldBe(CodeBlockSyntaxKind.Keyword);
        cs["Cart"].ShouldBe(CodeBlockSyntaxKind.Type);
        cs["Control"].ShouldBe(CodeBlockSyntaxKind.Type);
        cs["// Totals update when an item changes."].ShouldBe(CodeBlockSyntaxKind.Comment);
        cs["42"].ShouldBe(CodeBlockSyntaxKind.Number);
        cs["\"Arc\""].ShouldBe(CodeBlockSyntaxKind.String);
        cs["RunAsync"].ShouldBe(CodeBlockSyntaxKind.Function);
        cs["FromResult"].ShouldBe(CodeBlockSyntaxKind.Function);
        cs["{"].ShouldBe(CodeBlockSyntaxKind.Punctuation);
        cs["=>"].ShouldBe(CodeBlockSyntaxKind.Punctuation);
        cs["_count"].ShouldBe(CodeBlockSyntaxKind.None);
    }

    [AvaloniaFact]
    public void TypeScript_Roles()
    {
        var ts = Highlight("export function greet(name: string): string { return `Hi ${name}` + 42; } // done", "ts");
        ts["export"].ShouldBe(CodeBlockSyntaxKind.Keyword);
        ts["function"].ShouldBe(CodeBlockSyntaxKind.Keyword);
        ts["return"].ShouldBe(CodeBlockSyntaxKind.Keyword);
        ts["greet"].ShouldBe(CodeBlockSyntaxKind.Function);
        ts["string"].ShouldBe(CodeBlockSyntaxKind.Type);
        ts["`Hi "].ShouldBe(CodeBlockSyntaxKind.String);
        ts["42"].ShouldBe(CodeBlockSyntaxKind.Number);
        ts["// done"].ShouldBe(CodeBlockSyntaxKind.Comment);
        ts["("].ShouldBe(CodeBlockSyntaxKind.Punctuation);
        ts["+"].ShouldBe(CodeBlockSyntaxKind.Punctuation);

        var tsx = Highlight("<Button onClick={go} variant=\"primary\">Save</Button>", "tsx");
        tsx["Button"].ShouldBe(CodeBlockSyntaxKind.Type); // a component, not an intrinsic element
        tsx["onClick"].ShouldBe(CodeBlockSyntaxKind.Property);
        tsx["\"primary\""].ShouldBe(CodeBlockSyntaxKind.String);
        tsx["<"].ShouldBe(CodeBlockSyntaxKind.Punctuation);
        Highlight("<div className=\"a\" />", "tsx")["div"].ShouldBe(CodeBlockSyntaxKind.Tag);
    }

    [AvaloniaFact]
    public void Json_Roles()
    {
        var json = Highlight("{ \"name\": \"Arc\", \"stars\": 12.5, \"private\": true, \"license\": null }", "json");
        json["\"name\""].ShouldBe(CodeBlockSyntaxKind.Property);
        json["\"Arc\""].ShouldBe(CodeBlockSyntaxKind.String);
        json["12.5"].ShouldBe(CodeBlockSyntaxKind.Number);
        json["true"].ShouldBe(CodeBlockSyntaxKind.Keyword);
        json["null"].ShouldBe(CodeBlockSyntaxKind.Keyword);
        json["{"].ShouldBe(CodeBlockSyntaxKind.Punctuation);
        json[":"].ShouldBe(CodeBlockSyntaxKind.Punctuation);
    }

    [AvaloniaFact]
    public void Xml_Roles()
    {
        var xaml = Highlight("<!-- note -->\n<arc:Button Variant=\"Ghost\" Content=\"Save\" />\n<!--\n  two lines\n-->", "axaml");
        // TextMateSharp's XML grammar scopes only the delimiters; the whole comment is a comment.
        xaml["<!-- note -->"].ShouldBe(CodeBlockSyntaxKind.Comment);
        xaml["two lines"].ShouldBe(CodeBlockSyntaxKind.Comment);
        xaml["Button"].ShouldBe(CodeBlockSyntaxKind.Tag);
        xaml["Variant"].ShouldBe(CodeBlockSyntaxKind.Property);
        xaml["\"Ghost\""].ShouldBe(CodeBlockSyntaxKind.String);
        xaml["/>"].ShouldBe(CodeBlockSyntaxKind.Punctuation);

        var html = Highlight("<div class=\"card\">Hi</div>", "html");
        html["div"].ShouldBe(CodeBlockSyntaxKind.Tag);
        html["class"].ShouldBe(CodeBlockSyntaxKind.Property);
        html["\"card\""].ShouldBe(CodeBlockSyntaxKind.String);
        html["Hi"].ShouldBe(CodeBlockSyntaxKind.None);
    }

    [AvaloniaFact]
    public void Languages_Normalize_And_Unknown_Ones_Stay_Plain()
    {
        CodeBlockHighlighter.NormalizeLanguage(".TSX").ShouldBe("tsx");
        CodeBlockHighlighter.SupportsLanguage("C#").ShouldBeTrue();
        CodeBlockHighlighter.SupportsLanguage("language-python").ShouldBeTrue();
        CodeBlockHighlighter.SupportsLanguage("no-such-language").ShouldBeFalse();
        CodeBlockHighlighter.SupportsLanguage("text").ShouldBeFalse();
        CodeBlockHighlighter.CreateSession("no-such-language").ShouldBeNull();
        CodeBlockHighlighter.FamilyOf("{.AXAML}").ShouldBe(CodeBlockLanguageFamily.Markup);
        CodeBlockHighlighter.FamilyOf("jsonc").ShouldBe(CodeBlockLanguageFamily.Json);
        CodeBlockHighlighter.FamilyOf("ts").ShouldBe(CodeBlockLanguageFamily.Code);
    }

    [AvaloniaFact]
    public async Task Unknown_Language_Shows_Plain_Text()
    {
        var block = new CodeBlock { Language = "no-such-language", Code = "return 1;" };
        var window = Show(block);
        try
        {
            await block.TextPresenter!.WhenHighlighted;
            block.TextPresenter.SpanCount.ShouldBe(0);
            block.TextPresenter.Text.ShouldBe("return 1;");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public async Task Session_Follows_Code_And_Language_Changes()
    {
        var block = new CodeBlock { Language = "cs", Code = "var a = 1;" };
        var window = Show(block);
        try
        {
            var text = block.TextPresenter!;
            await text.WhenHighlighted;
            Dispatcher.UIThread.RunJobs();
            text.Spans.ShouldContain(s => s.Kind == CodeBlockSyntaxKind.Keyword);

            block.Code = "// changed";
            await text.WhenHighlighted;
            Dispatcher.UIThread.RunJobs();
            text.Spans.ShouldBe([new CodeBlockSyntaxSpan(0, 10, CodeBlockSyntaxKind.Comment)]);

            block.Language = "json";
            block.Code = "{ \"a\": 1 }";
            await text.WhenHighlighted;
            Dispatcher.UIThread.RunJobs();
            text.Spans.ShouldContain(new CodeBlockSyntaxSpan(2, 3, CodeBlockSyntaxKind.Property));
            text.Spans.ShouldContain(new CodeBlockSyntaxSpan(7, 1, CodeBlockSyntaxKind.Number));
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public async Task Defaults_Match_Arc()
    {
        var block = new CodeBlock { Code = "const a = 1;\nconst b = 2;" };
        var window = Show(block);
        try
        {
            await block.TextPresenter!.WhenHighlighted;
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
