// Ported from vscode (src/vs/editor/test/common/services/semanticTokensProviderStyling.test.ts, commit b57d5c9e), MIT
// license: all 3 upstream tests. The theme stub answers the number in the token type name as the foreground, as
// upstream's does. Below them, tests written for this port: modifiers, the theme's absent style, the cache, the
// standalone theme's rules and the model's store merging semantic over syntax tokens.

using Shouldly;
using TKCode.Arc.Editor.Languages;
using TKCode.Arc.Editor.Services;
using TKCode.Arc.Editor.Standalone;
using TKCode.Arc.Editor.Tokens;
using Xunit;

namespace TKCode.Arc.Editor.Tests;

public sealed class SemanticTokensProviderStylingTests
{
    internal sealed class StubTheme(Func<string, IReadOnlyList<string>, TokenStyle?> style, bool semanticHighlighting = true) : ISemanticTokensColorTheme
    {
        public int Calls { get; private set; }

        public bool SemanticHighlighting { get; } = semanticHighlighting;

        public TokenStyle? GetTokenStyleMetadata(string type, IReadOnlyList<string> modifiers, string modelLanguage)
        {
            Calls++;
            return style(type, modifiers);
        }
    }

    internal sealed class StubThemeService(ISemanticTokensColorTheme theme) : ISemanticTokensThemeService
    {
        private readonly List<Action> _listeners = new();

        public ISemanticTokensColorTheme Theme { get; private set; } = theme;

        public int ListenerCount => _listeners.Count;

        public event Action? DidColorThemeChange
        {
            add
            {
                if (value is not null)
                {
                    _listeners.Add(value);
                }
            }

            remove
            {
                if (value is not null)
                {
                    _listeners.Remove(value);
                }
            }
        }

        public ISemanticTokensColorTheme GetColorTheme() => Theme;

        public void SetTheme(ISemanticTokensColorTheme theme)
        {
            Theme = theme;
            foreach (var listener in _listeners.ToArray())
            {
                listener();
            }
        }
    }

    internal static StubTheme NumberTheme(bool semanticHighlighting = true) =>
        new((type, _) => new TokenStyle(int.Parse(type[2..], System.Globalization.CultureInfo.InvariantCulture)), semanticHighlighting);

    private static SemanticTokensProviderStyling CreateStyling(IReadOnlyList<string> types, IReadOnlyList<string> modifiers, ISemanticTokensColorTheme? theme = null) =>
        new(new SemanticTokensLegend(types, modifiers), new StubThemeService(theme ?? NumberTheme()), new LanguageIdCodec());

    private static uint Fg(uint color) => MetadataConsts.SemanticUseForeground | (color << MetadataConsts.ForegroundOffset);

    [Fact]
    public void Issue_134973_invalid_semantic_tokens_should_be_handled_better()
    {
        var styling = CreateStyling(["st0", "st1", "st2", "st3", "st4", "st5", "st6", "st7", "st8", "st9", "st10"], []);
        var badTokens = new SemanticTokens(
        [
            0, 13, 16, 1, 0,
            1, 2, 6, 2, 0,
            0, 7, 6, 3, 0,
            0, 15, 8, 4, 0,
            0, 17, 1, 5, 0,
            0, 7, 5, 6, 0,
            1, 12, 8, 7, 0,
            0, 19, 5, 8, 0,
            0, 7, 1, 9, 0,
            0, 4294967294, 5, 10, 0,
        ]);
        var result = SemanticTokensConversion.ToMultilineTokens2(badTokens, styling, "java");
        var expected = SparseMultilineTokens.Create(1,
        [
            0, 13, 29, Fg(1),
            1, 2, 8, Fg(2),
            1, 9, 15, Fg(3),
            1, 24, 32, Fg(4),
            1, 41, 42, Fg(5),
            1, 48, 53, Fg(6),
            2, 12, 20, Fg(7),
            2, 31, 36, Fg(8),
            2, 38, 39, Fg(9),
        ]);
        string.Join(",", result.Select(r => r.ToString())).ShouldBe(expected.ToString());
    }

    [Fact]
    public void Issue_148651_semantic_token_with_negative_values_does_not_hang()
    {
        var styling = CreateStyling(["st0", "st1", "st2", "st3", "st4", "st5", "st6", "st7", "st8", "st9"], ["stm0", "stm1", "stm2"]);
        var badTokens = new SemanticTokens(
        [
            0, 0, 3, 0, 0,
            0, 4, 2, 2, 0,
            0, 2, 3, 8, 0,
            0, 3, 1, 9, 0,
            0, 1, 1, 10, 0,
            0, 1, 4, 8, 0,
            0, 4, 4294967292, 2, 0,
            0, 4294967292, 4294967294, 8, 0,
            0, 4294967294, 1, 9, 0,
            0, 1, 1, 10, 0,
            0, 1, 3, 8, 0,
            0, 3, 4294967291, 8, 0,
            0, 4294967291, 1, 9, 0,
            0, 1, 1, 10, 0,
            0, 1, 4, 8, 0,
        ]);
        var result = SemanticTokensConversion.ToMultilineTokens2(badTokens, styling, "dockerfile");
        var expected = SparseMultilineTokens.Create(1,
        [
            0, 4, 6, Fg(1),
            0, 6, 9, Fg(2),
            0, 9, 10, Fg(3),
            0, 11, 15, Fg(4),
        ]);
        string.Join(",", result.Select(r => r.ToString())).ShouldBe(expected.ToString());
    }

    [Fact]
    public void Issue_149130_vscode_freezes_because_of_bracket_pair_colorization()
    {
        var styling = CreateStyling(["st0", "st1", "st2", "st3", "st4", "st5"], ["stm0", "stm1", "stm2"]);
        var badTokens = new SemanticTokens(
        [
            0, 11, 1, 1, 0,
            0, 4, 1, 1, 0,
            0, 4294967289, 1, 1, 0,
        ]);
        var result = SemanticTokensConversion.ToMultilineTokens2(badTokens, styling, "q");
        var expected = SparseMultilineTokens.Create(1,
        [
            0, 11, 12, Fg(1),
            0, 15, 16, Fg(1),
        ]);
        string.Join(",", result.Select(r => r.ToString())).ShouldBe(expected.ToString());
    }

    // ---- written for the port

    [Fact]
    public void Modifiers_are_read_from_the_bits_in_legend_order_and_font_styles_are_encoded()
    {
        IReadOnlyList<string>? seen = null;
        var theme = new StubTheme((type, modifiers) =>
        {
            seen = modifiers;
            return new TokenStyle(5, Bold: true, Italic: false);
        });
        var styling = CreateStyling(["variable"], ["declaration", "readonly", "static"], theme);

        var metadata = styling.GetMetadata(0, 0b101, "plaintext");

        seen.ShouldBe(["declaration", "static"]);
        TokenMetadata.GetForeground(metadata).ShouldBe((ColorId)5);
        (metadata & MetadataConsts.SemanticUseBold).ShouldNotBe(0u);
        (metadata & MetadataConsts.SemanticUseItalic).ShouldNotBe(0u);
        TokenMetadata.GetFontStyle(metadata).ShouldBe(FontStyle.Bold);
        (metadata & MetadataConsts.SemanticUseUnderline).ShouldBe(0u);
    }

    [Fact]
    public void A_type_outside_the_legend_or_without_a_theme_style_is_not_styled_and_results_are_cached()
    {
        var theme = new StubTheme((type, _) => type == "unstyled" ? null : new TokenStyle(0));
        var styling = CreateStyling(["unstyled", "empty"], [], theme);

        styling.GetMetadata(7, 0, "plaintext").ShouldBe(SemanticTokensProviderStyling.NoStyling);
        styling.GetMetadata(0, 0, "plaintext").ShouldBe(SemanticTokensProviderStyling.NoStyling);
        styling.GetMetadata(1, 0, "plaintext").ShouldBe(SemanticTokensProviderStyling.NoStyling);
        var calls = theme.Calls;
        for (uint i = 0; i < 50; i++)
        {
            styling.GetMetadata(0, 0, "plaintext");
            styling.GetMetadata(1, i, "plaintext");
        }

        // the 50 modifier sets of type 1 are asked once each (the first was asked above), the cache grows through its sizes
        theme.Calls.ShouldBe(calls + 49);
    }

    [Fact]
    public void Standalone_theme_matches_type_and_modifiers_against_its_token_rules()
    {
        var themeService = new StandaloneThemeService();
        themeService.DefineTheme("semantic", new StandaloneThemeData(
            StandaloneTheme.VsDarkThemeName,
            true,
            [
                new TokenThemeRule("variable") { Foreground = "112233" },
                new TokenThemeRule("variable.readonly") { Foreground = "445566", FontStyle = "italic" },
            ],
            new Dictionary<string, string>()));
        themeService.SetTheme("semantic");
        using var adapter = new StandaloneSemanticTokensThemeService(themeService);
        var colorMap = themeService.GetColorMap();

        var plain = adapter.GetColorTheme().GetTokenStyleMetadata("variable", [], "plaintext")!;
        var readonlyVariable = adapter.GetColorTheme().GetTokenStyleMetadata("variable", ["readonly"], "plaintext")!;

        colorMap[plain.Foreground!.Value].ShouldBe(RgbaColor.FromHex("#112233"));
        colorMap[readonlyVariable.Foreground!.Value].ShouldBe(RgbaColor.FromHex("#445566"));
        readonlyVariable.Italic.ShouldBe(true);
        plain.Italic.ShouldBe(false);
        adapter.GetColorTheme().SemanticHighlighting.ShouldBeFalse();
    }

    [Fact]
    public void Semantic_tokens_are_merged_over_the_syntax_tokens_of_a_line()
    {
        using var model = TestTextModel.Create("let value = 1;");
        var before = model.Tokenization.GetLineTokens(1);
        var defaultForeground = before.GetForeground(0);

        var styling = CreateStyling(["st0", "st1", "st2", "st3", "st4", "st5", "st6", "st7"], []);
        // "value" (characters 4..9) is a st7
        var tokens = SemanticTokensConversion.ToMultilineTokens2(new SemanticTokens([0, 4, 5, 7, 0]), styling, "plaintext");
        var changes = 0;
        model.DidChangeTokens += e =>
        {
            changes++;
            e.SemanticTokensApplied.ShouldBeTrue();
        };
        model.Tokenization.SetSemanticTokens(tokens, true);

        changes.ShouldBe(1);
        model.Tokenization.HasCompleteSemanticTokens().ShouldBeTrue();
        var line = model.Tokenization.GetLineTokens(1);
        line.GetCount().ShouldBe(3);
        line.GetEndOffset(0).ShouldBe(4);
        line.GetForeground(0).ShouldBe(defaultForeground);
        line.GetEndOffset(1).ShouldBe(9);
        line.GetForeground(1).ShouldBe((ColorId)7);
        line.GetForeground(2).ShouldBe(defaultForeground);

        // an edit before the token moves it
        model.ApplyEdits([new SingleEditOperation(new TextRange(1, 1, 1, 1), "  ")]);
        var moved = model.Tokenization.GetLineTokens(1);
        moved.GetEndOffset(0).ShouldBe(6);
        moved.GetForeground(1).ShouldBe((ColorId)7);

        // partial tokens are ignored once the tokens are complete; null clears them
        model.Tokenization.SetSemanticTokens(null, false);
        model.Tokenization.HasSomeSemanticTokens().ShouldBeFalse();
        model.Tokenization.GetLineTokens(1).GetCount().ShouldBe(1);
    }
}
