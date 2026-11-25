// Ported from vscode (src/vs/editor/test/common/modes/supports/tokenization.test.ts, commit b57d5c9e), MIT license.
// The 'Token font CSS' suite is not ported: the font token parts of tokenization.ts are left out (TokenTheme.cs).
// The theme tests at the end are not upstream's: they check the built-in themes and StandaloneTheme.

using Shouldly;
using TKCode.Arc.Editor.Languages;
using TKCode.Arc.Editor.Standalone;
using TKCode.Arc.Editor.Tokens;
using Xunit;

namespace TKCode.Arc.Editor.Tests;

public sealed class TokenThemeTests
{
    private static ExternalThemeTrieElement Node(ThemeTrieElementRule rule, params (string Key, ExternalThemeTrieElement Child)[] children) =>
        new(rule, children.ToDictionary(c => c.Key, c => c.Child, StringComparer.Ordinal));

    [Fact]
    public void Matching_Gives_Higher_Priority_To_Deeper_Matches()
    {
        var theme = TokenTheme.CreateFromRawTokenTheme(
        [
            new("") { Foreground = "100000", Background = "200000" },
            new("punctuation.definition.string.begin.html") { Foreground = "300000" },
            new("punctuation.definition.string") { Foreground = "400000" },
        ], []);

        var colorMap = new ColorMap();
        colorMap.GetId("100000");
        var b = colorMap.GetId("200000");
        colorMap.GetId("400000");
        var d = colorMap.GetId("300000");

        var actual = theme.MatchRule("punctuation.definition.string.begin.html");

        actual.ShouldBe(new ThemeTrieElementRule(FontStyle.None, d, b));
    }

    [Fact]
    public void Matching_Can_Match()
    {
        var theme = TokenTheme.CreateFromRawTokenTheme(
        [
            new("") { Foreground = "F8F8F2", Background = "272822" },
            new("source") { Background = "100000" },
            new("something") { Background = "100000" },
            new("bar") { Background = "200000" },
            new("baz") { Background = "200000" },
            new("bar") { FontStyle = "bold" },
            new("constant") { FontStyle = "italic", Foreground = "300000" },
            new("constant.numeric") { Foreground = "400000" },
            new("constant.numeric.hex") { FontStyle = "bold" },
            new("constant.numeric.oct") { FontStyle = "bold italic underline" },
            new("constant.numeric.bin") { FontStyle = "bold strikethrough" },
            new("constant.numeric.dec") { FontStyle = "", Foreground = "500000" },
            new("storage.object.bar") { FontStyle = "", Foreground = "600000" },
        ], []);

        var colorMap = new ColorMap();
        var a = colorMap.GetId("F8F8F2");
        var b = colorMap.GetId("272822");
        var c = colorMap.GetId("200000");
        var d = colorMap.GetId("300000");
        var e = colorMap.GetId("400000");
        var f = colorMap.GetId("500000");
        var g = colorMap.GetId("100000");
        var h = colorMap.GetId("600000");

        void AssertMatch(string scopeName, ThemeTrieElementRule expected) =>
            theme.MatchRule(scopeName).ShouldBe(expected, "when matching <<" + scopeName + ">>");

        void AssertSimpleMatch(string scopeName, FontStyle fontStyle, ColorId foreground, ColorId background) =>
            AssertMatch(scopeName, new ThemeTrieElementRule(fontStyle, foreground, background));

        void AssertNoMatch(string scopeName) => AssertMatch(scopeName, new ThemeTrieElementRule(FontStyle.None, a, b));

        // matches defaults
        AssertNoMatch("");
        AssertNoMatch("bazz");
        AssertNoMatch("asdfg");

        // matches source
        AssertSimpleMatch("source", FontStyle.None, a, g);
        AssertSimpleMatch("source.ts", FontStyle.None, a, g);
        AssertSimpleMatch("source.tss", FontStyle.None, a, g);

        // matches something
        AssertSimpleMatch("something", FontStyle.None, a, g);
        AssertSimpleMatch("something.ts", FontStyle.None, a, g);
        AssertSimpleMatch("something.tss", FontStyle.None, a, g);

        // matches baz
        AssertSimpleMatch("baz", FontStyle.None, a, c);
        AssertSimpleMatch("baz.ts", FontStyle.None, a, c);
        AssertSimpleMatch("baz.tss", FontStyle.None, a, c);

        // matches constant
        AssertSimpleMatch("constant", FontStyle.Italic, d, b);
        AssertSimpleMatch("constant.string", FontStyle.Italic, d, b);
        AssertSimpleMatch("constant.hex", FontStyle.Italic, d, b);

        // matches constant.numeric
        AssertSimpleMatch("constant.numeric", FontStyle.Italic, e, b);
        AssertSimpleMatch("constant.numeric.baz", FontStyle.Italic, e, b);

        // matches constant.numeric.hex
        AssertSimpleMatch("constant.numeric.hex", FontStyle.Bold, e, b);
        AssertSimpleMatch("constant.numeric.hex.baz", FontStyle.Bold, e, b);

        // matches constant.numeric.oct
        AssertSimpleMatch("constant.numeric.oct", FontStyle.Bold | FontStyle.Italic | FontStyle.Underline, e, b);
        AssertSimpleMatch("constant.numeric.oct.baz", FontStyle.Bold | FontStyle.Italic | FontStyle.Underline, e, b);

        // matches constant.numeric.bin
        AssertSimpleMatch("constant.numeric.bin", FontStyle.Bold | FontStyle.Strikethrough, e, b);
        AssertSimpleMatch("constant.numeric.bin.baz", FontStyle.Bold | FontStyle.Strikethrough, e, b);

        // matches constant.numeric.dec
        AssertSimpleMatch("constant.numeric.dec", FontStyle.None, f, b);
        AssertSimpleMatch("constant.numeric.dec.baz", FontStyle.None, f, b);

        // matches storage.object.bar
        AssertSimpleMatch("storage.object.bar", FontStyle.None, h, b);
        AssertSimpleMatch("storage.object.bar.baz", FontStyle.None, h, b);

        // does not match storage.object.bar
        AssertSimpleMatch("storage.object.bart", FontStyle.None, a, b);
        AssertSimpleMatch("storage.object", FontStyle.None, a, b);
        AssertSimpleMatch("storage", FontStyle.None, a, b);

        AssertSimpleMatch("bar", FontStyle.Bold, a, c);
    }

    [Fact]
    public void Parsing_Can_Parse()
    {
        var actual = TokenThemeParser.ParseTokenTheme(
        [
            new("") { Foreground = "F8F8F2", Background = "272822" },
            new("source") { Background = "100000" },
            new("something") { Background = "100000" },
            new("bar") { Background = "010000" },
            new("baz") { Background = "010000" },
            new("bar") { FontStyle = "bold" },
            new("constant") { FontStyle = "italic", Foreground = "ff0000" },
            new("constant.numeric") { Foreground = "00ff00" },
            new("constant.numeric.hex") { FontStyle = "bold" },
            new("constant.numeric.oct") { FontStyle = "bold italic underline" },
            new("constant.numeric.dec") { FontStyle = "", Foreground = "0000ff" },
        ]);

        ParsedTokenThemeRule[] expected =
        [
            new("", 0, FontStyle.NotSet, "F8F8F2", "272822"),
            new("source", 1, FontStyle.NotSet, null, "100000"),
            new("something", 2, FontStyle.NotSet, null, "100000"),
            new("bar", 3, FontStyle.NotSet, null, "010000"),
            new("baz", 4, FontStyle.NotSet, null, "010000"),
            new("bar", 5, FontStyle.Bold, null, null),
            new("constant", 6, FontStyle.Italic, "ff0000", null),
            new("constant.numeric", 7, FontStyle.NotSet, "00ff00", null),
            new("constant.numeric.hex", 8, FontStyle.Bold, null, null),
            new("constant.numeric.oct", 9, FontStyle.Bold | FontStyle.Italic | FontStyle.Underline, null, null),
            new("constant.numeric.dec", 10, FontStyle.None, "0000ff", null),
        ];

        actual.ShouldBe(expected);
    }

    [Fact]
    public void Resolving_Strcmp_Works()
    {
        var actual = new List<string> { "bar", "z", "zu", "a", "ab", "" };
        actual.Sort(TokenThemeParser.Strcmp);

        actual.ShouldBe(["", "a", "ab", "bar", "z", "zu"]);
    }

    [Fact]
    public void Resolving_Always_Has_Defaults()
    {
        var actual = TokenTheme.CreateFromParsedTokenTheme([], []);
        var colorMap = new ColorMap();
        var a = colorMap.GetId("000000");
        var b = colorMap.GetId("ffffff");
        actual.GetColorMap().ShouldBe(colorMap.GetColorMap());
        actual.GetThemeTrieElement().ShouldBe(Node(new ThemeTrieElementRule(FontStyle.None, a, b)));
    }

    [Theory]
    [InlineData(-1, null, null, 0, "000000", "ffffff")]
    [InlineData(0, null, null, 0, "000000", "ffffff")]
    [InlineData(2, null, null, 2, "000000", "ffffff")]
    [InlineData(-1, "ff0000", null, 0, "ff0000", "ffffff")]
    [InlineData(-1, null, "ff0000", 0, "000000", "ff0000")]
    public void Resolving_Respects_Incoming_Defaults(int fontStyle, string? foreground, string? background, int expectedFontStyle, string expectedForeground, string expectedBackground)
    {
        var actual = TokenTheme.CreateFromParsedTokenTheme([new ParsedTokenThemeRule("", -1, (FontStyle)fontStyle, foreground, background)], []);
        var colorMap = new ColorMap();
        var a = colorMap.GetId(expectedForeground);
        var b = colorMap.GetId(expectedBackground);
        actual.GetColorMap().ShouldBe(colorMap.GetColorMap());
        actual.GetThemeTrieElement().ShouldBe(Node(new ThemeTrieElementRule((FontStyle)expectedFontStyle, a, b)));
    }

    [Fact]
    public void Resolving_Can_Merge_Incoming_Defaults()
    {
        var actual = TokenTheme.CreateFromParsedTokenTheme(
        [
            new ParsedTokenThemeRule("", -1, FontStyle.NotSet, null, "ff0000"),
            new ParsedTokenThemeRule("", -1, FontStyle.NotSet, "00ff00", null),
            new ParsedTokenThemeRule("", -1, FontStyle.Bold, null, null),
        ], []);
        var colorMap = new ColorMap();
        var a = colorMap.GetId("00ff00");
        var b = colorMap.GetId("ff0000");
        actual.GetColorMap().ShouldBe(colorMap.GetColorMap());
        actual.GetThemeTrieElement().ShouldBe(Node(new ThemeTrieElementRule(FontStyle.Bold, a, b)));
    }

    [Fact]
    public void Resolving_Defaults_Are_Inherited()
    {
        var actual = TokenTheme.CreateFromParsedTokenTheme(
        [
            new ParsedTokenThemeRule("", -1, FontStyle.NotSet, "F8F8F2", "272822"),
            new ParsedTokenThemeRule("var", -1, FontStyle.NotSet, "ff0000", null),
        ], []);
        var colorMap = new ColorMap();
        var a = colorMap.GetId("F8F8F2");
        var b = colorMap.GetId("272822");
        var c = colorMap.GetId("ff0000");
        actual.GetColorMap().ShouldBe(colorMap.GetColorMap());
        var root = Node(new ThemeTrieElementRule(FontStyle.None, a, b),
            ("var", Node(new ThemeTrieElementRule(FontStyle.None, c, b))));
        actual.GetThemeTrieElement().ShouldBe(root);
    }

    [Fact]
    public void Resolving_Same_Rules_Get_Merged()
    {
        var actual = TokenTheme.CreateFromParsedTokenTheme(
        [
            new ParsedTokenThemeRule("", -1, FontStyle.NotSet, "F8F8F2", "272822"),
            new ParsedTokenThemeRule("var", 1, FontStyle.Bold, null, null),
            new ParsedTokenThemeRule("var", 0, FontStyle.NotSet, "ff0000", null),
        ], []);
        var colorMap = new ColorMap();
        var a = colorMap.GetId("F8F8F2");
        var b = colorMap.GetId("272822");
        var c = colorMap.GetId("ff0000");
        actual.GetColorMap().ShouldBe(colorMap.GetColorMap());
        var root = Node(new ThemeTrieElementRule(FontStyle.None, a, b),
            ("var", Node(new ThemeTrieElementRule(FontStyle.Bold, c, b))));
        actual.GetThemeTrieElement().ShouldBe(root);
    }

    [Fact]
    public void Resolving_Rules_Are_Inherited_1()
    {
        var actual = TokenTheme.CreateFromParsedTokenTheme(
        [
            new ParsedTokenThemeRule("", -1, FontStyle.NotSet, "F8F8F2", "272822"),
            new ParsedTokenThemeRule("var", -1, FontStyle.Bold, "ff0000", null),
            new ParsedTokenThemeRule("var.identifier", -1, FontStyle.NotSet, "00ff00", null),
        ], []);
        var colorMap = new ColorMap();
        var a = colorMap.GetId("F8F8F2");
        var b = colorMap.GetId("272822");
        var c = colorMap.GetId("ff0000");
        var d = colorMap.GetId("00ff00");
        actual.GetColorMap().ShouldBe(colorMap.GetColorMap());
        var root = Node(new ThemeTrieElementRule(FontStyle.None, a, b),
            ("var", Node(new ThemeTrieElementRule(FontStyle.Bold, c, b),
                ("identifier", Node(new ThemeTrieElementRule(FontStyle.Bold, d, b))))));
        actual.GetThemeTrieElement().ShouldBe(root);
    }

    [Fact]
    public void Resolving_Rules_Are_Inherited_2()
    {
        var actual = TokenTheme.CreateFromParsedTokenTheme(
        [
            new ParsedTokenThemeRule("", -1, FontStyle.NotSet, "F8F8F2", "272822"),
            new ParsedTokenThemeRule("var", -1, FontStyle.Bold, "ff0000", null),
            new ParsedTokenThemeRule("var.identifier", -1, FontStyle.NotSet, "00ff00", null),
            new ParsedTokenThemeRule("constant", 4, FontStyle.Italic, "100000", null),
            new ParsedTokenThemeRule("constant.numeric", 5, FontStyle.NotSet, "200000", null),
            new ParsedTokenThemeRule("constant.numeric.hex", 6, FontStyle.Bold, null, null),
            new ParsedTokenThemeRule("constant.numeric.oct", 7, FontStyle.Bold | FontStyle.Italic | FontStyle.Underline, null, null),
            new ParsedTokenThemeRule("constant.numeric.dec", 8, FontStyle.None, "300000", null),
        ], []);
        var colorMap = new ColorMap();
        var a = colorMap.GetId("F8F8F2");
        var b = colorMap.GetId("272822");
        var c = colorMap.GetId("100000");
        var d = colorMap.GetId("200000");
        var e = colorMap.GetId("300000");
        var f = colorMap.GetId("ff0000");
        var g = colorMap.GetId("00ff00");
        actual.GetColorMap().ShouldBe(colorMap.GetColorMap());
        var root = Node(new ThemeTrieElementRule(FontStyle.None, a, b),
            ("var", Node(new ThemeTrieElementRule(FontStyle.Bold, f, b),
                ("identifier", Node(new ThemeTrieElementRule(FontStyle.Bold, g, b))))),
            ("constant", Node(new ThemeTrieElementRule(FontStyle.Italic, c, b),
                ("numeric", Node(new ThemeTrieElementRule(FontStyle.Italic, d, b),
                    ("hex", Node(new ThemeTrieElementRule(FontStyle.Bold, d, b))),
                    ("oct", Node(new ThemeTrieElementRule(FontStyle.Bold | FontStyle.Italic | FontStyle.Underline, d, b))),
                    ("dec", Node(new ThemeTrieElementRule(FontStyle.None, e, b))))))));
        actual.GetThemeTrieElement().ShouldBe(root);
    }

    [Fact]
    public void Resolving_Custom_Colors_Are_First_In_Color_Map()
    {
        var actual = TokenTheme.CreateFromParsedTokenTheme(
            [new ParsedTokenThemeRule("var", -1, FontStyle.NotSet, "F8F8F2", null)],
            ["000000", "FFFFFF", "0F0F0F"]);
        var colorMap = new ColorMap();
        colorMap.GetId("000000");
        colorMap.GetId("FFFFFF");
        colorMap.GetId("0F0F0F");
        colorMap.GetId("F8F8F2");
        actual.GetColorMap().ShouldBe(colorMap.GetColorMap());
    }

    [Fact]
    public void Match_Sets_Language_And_Standard_Token_Type()
    {
        var theme = TokenTheme.CreateFromRawTokenTheme([new("comment") { Foreground = "008000", FontStyle = "italic" }], []);
        var metadata = theme.Match((LanguageId)5, "comment.line.cs");

        TokenMetadata.GetLanguageId(metadata).ShouldBe((LanguageId)5);
        TokenMetadata.GetTokenType(metadata).ShouldBe(StandardTokenType.Comment);
        TokenMetadata.GetFontStyle(metadata).ShouldBe(FontStyle.Italic);
        theme.GetColorMap()[(int)TokenMetadata.GetForeground(metadata)].ShouldBe(RgbaColor.FromHex("#008000"));
        TokenThemeParser.ToStandardTokenType("string.quoted").ShouldBe(StandardTokenType.String);
        TokenThemeParser.ToStandardTokenType("regexp").ShouldBe(StandardTokenType.RegEx);
        TokenThemeParser.ToStandardTokenType("keyword.regexpx").ShouldBe(StandardTokenType.Other);
    }

    [Fact]
    public void Css_For_Color_Map()
    {
        var css = TokenThemeParser.GenerateTokensCSSForColorMap([default, RgbaColor.FromHex("#000000"), RgbaColor.FromHex("#FFFFFE")]);
        css.ShouldStartWith("\n.mtk1 { color: #000000; }\n.mtk2 { color: #fffffe; }\n.mtki { font-style: italic; }");
    }

    [Theory]
    [InlineData("vs", "#fffffe", "#000000", "#0000ff")]
    [InlineData("vs-dark", "#1e1e1e", "#d4d4d4", "#569cd6")]
    [InlineData("hc-black", "#000000", "#ffffff", "#569cd6")]
    [InlineData("hc-light", "#ffffff", "#292929", "#0000ff")]
    public void Builtin_Themes_Resolve_Editor_Colors_And_Keywords(string name, string background, string foreground, string keyword)
    {
        var theme = new StandaloneTheme(name, StandaloneTheme.GetBuiltinRules(name));
        var colorMap = theme.TokenTheme.GetColorMap();
        var defaults = theme.TokenTheme.MatchRule("");
        colorMap[(int)defaults.Background].ToHex().ShouldBe(background);
        colorMap[(int)defaults.Foreground].ToHex().ShouldBe(foreground);
        colorMap[(int)theme.TokenTheme.MatchRule("keyword.cs").Foreground].ToHex().ShouldBe(keyword);
    }

    [Fact]
    public void Inherited_Theme_Overrides_Base_Rules_And_Colors()
    {
        var data = new StandaloneThemeData("vs-dark", Inherit: true,
            [new("keyword") { Foreground = "FF8800", FontStyle = "bold" }],
            new Dictionary<string, string> { ["editor.background"] = "#101010" });
        var theme = new StandaloneTheme("my-theme", data);

        theme.Id.ShouldBe("vs-dark my-theme");
        theme.Type.ShouldBe(ColorScheme.Dark);
        var colorMap = theme.TokenTheme.GetColorMap();
        colorMap[(int)theme.TokenTheme.MatchRule("").Background].ToHex().ShouldBe("#101010");
        var keyword = theme.TokenTheme.MatchRule("keyword.cs");
        colorMap[(int)keyword.Foreground].ToHex().ShouldBe("#ff8800");
        keyword.FontStyle.ShouldBe(FontStyle.Bold);
        colorMap[(int)theme.TokenTheme.MatchRule("comment").Foreground].ToHex().ShouldBe("#608b4e");
        theme.GetColor("editor.foreground").ShouldBe(RgbaColor.FromHex("#D4D4D4"));
        theme.GetColor("editor.background").ShouldBe(RgbaColor.FromHex("#101010"));

        // The base theme's rules were not changed by the inheriting theme.
        Themes.VsDark.Rules.Count(r => r.Token.Length == 0).ShouldBe(1);
    }

    [Fact]
    public void Theme_Service_Publishes_The_Color_Map()
    {
        var registry = new TokenizationRegistry<ITokenizationSupport>();
        var service = new StandaloneThemeService(registry);
        registry.GetColorMap().ShouldBe(service.GetColorTheme().TokenTheme.GetColorMap());

        service.SetTheme("vs-dark");
        service.GetColorTheme().ThemeName.ShouldBe("vs-dark");
        registry.GetDefaultBackground().ShouldBe(RgbaColor.FromHex("#1E1E1E"));

        service.DefineTheme("mine", new StandaloneThemeData("vs", Inherit: true, [], new Dictionary<string, string> { ["editor.background"] = "#123456" }));
        service.SetTheme("mine");
        registry.GetDefaultBackground().ShouldBe(RgbaColor.FromHex("#123456"));

        Should.Throw<ArgumentException>(() => service.DefineTheme("bad name", Themes.Vs));
        Should.Throw<ArgumentException>(() => service.DefineTheme("x", Themes.Vs with { Base = "nope" }));
    }
}
