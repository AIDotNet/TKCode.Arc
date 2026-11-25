// Ported from vscode (src/vs/editor/contrib/indentation/test/browser/indentation.test.ts: Language,
// registerLanguageConfiguration, StandardTokenTypeData and registerTokenizationSupport, commit b57d5c9e), MIT license.
// Differences from upstream:
//  - Language is a static class of string constants;
//  - there is no language service: registerLanguage has no equivalent (a test model's codec knows its language);
//  - registerTokenizationSupport sets the model's stateless test tokenizer, which gives line n the tokens at index
//    n - 1. Upstream's tokenizer hands out the token lists in the order the lines are tokenized, which is the same
//    when each line is tokenized once, top to bottom.

using TKCode.Arc.Editor.Languages;
using TKCode.Arc.Editor.Model;
using TKCode.Arc.Editor.Tokens;

namespace TKCode.Arc.Editor.Tests;

internal static class Language
{
    public const string TypeScript = "ts-test";
    public const string Ruby = "ruby-test";
    public const string PHP = "php-test";
    public const string Go = "go-test";
    public const string CPP = "cpp-test";
    public const string HTML = "html-test";
    public const string VB = "vb-test";
    public const string Latex = "latex-test";
    public const string Lua = "lua-test";
}

internal readonly record struct StandardTokenTypeData(int StartIndex, StandardTokenType StandardTokenType);

internal static class IndentationTestUtils
{
    public static IDisposable RegisterLanguageConfiguration(LanguageConfigurationService languageConfigurationService, string language)
    {
        switch (language)
        {
            case Language.TypeScript:
                return languageConfigurationService.Register(language, new LanguageConfiguration
                {
                    Brackets = BracketRules.TypescriptBracketRules,
                    Comments = new CommentRule
                    {
                        LineComment = "//",
                        BlockComment = ("/*", "*/"),
                    },
                    AutoClosingPairs = AutoClosingPairsRules.JavascriptAutoClosingPairsRules,
                    IndentationRules = IndentationRules.JavascriptIndentationRules,
                    OnEnterRules = OnEnterRules.JavascriptOnEnterRules,
                });
            case Language.Ruby:
                return languageConfigurationService.Register(language, new LanguageConfiguration
                {
                    Brackets = BracketRules.RubyBracketRules,
                    IndentationRules = IndentationRules.RubyIndentationRules,
                });
            case Language.PHP:
                return languageConfigurationService.Register(language, new LanguageConfiguration
                {
                    Brackets = BracketRules.PhpBracketRules,
                    IndentationRules = IndentationRules.PhpIndentationRules,
                    OnEnterRules = OnEnterRules.PhpOnEnterRules,
                });
            case Language.Go:
                return languageConfigurationService.Register(language, new LanguageConfiguration
                {
                    Brackets = BracketRules.GoBracketRules,
                    IndentationRules = IndentationRules.GoIndentationRules,
                });
            case Language.CPP:
                return languageConfigurationService.Register(language, new LanguageConfiguration
                {
                    Brackets = BracketRules.CppBracketRules,
                    OnEnterRules = OnEnterRules.CppOnEnterRules,
                });
            case Language.HTML:
                return languageConfigurationService.Register(language, new LanguageConfiguration
                {
                    Brackets = BracketRules.HtmlBracketRules,
                    IndentationRules = IndentationRules.HtmlIndentationRules,
                    OnEnterRules = OnEnterRules.HtmlOnEnterRules,
                });
            case Language.VB:
                return languageConfigurationService.Register(language, new LanguageConfiguration
                {
                    Brackets = BracketRules.VbBracketRules,
                    IndentationRules = IndentationRules.VbIndentationRules,
                    OnEnterRules = OnEnterRules.VbOnEnterRules,
                });
            case Language.Latex:
                return languageConfigurationService.Register(language, new LanguageConfiguration
                {
                    Brackets = BracketRules.LatexBracketRules,
                    AutoClosingPairs = AutoClosingPairsRules.LatexAutoClosingPairsRules.Select(p => new AutoClosingPairConditional(p.Open, p.Close)).ToList(),
                    IndentationRules = IndentationRules.LatexIndentationRules,
                });
            case Language.Lua:
                return languageConfigurationService.Register(language, new LanguageConfiguration
                {
                    Brackets = BracketRules.LuaBracketRules,
                    IndentationRules = IndentationRules.LuaIndentationRules,
                });
            default:
                throw new ArgumentOutOfRangeException(nameof(language), language, null);
        }
    }

    public static void RegisterTokenizationSupport(TextModel model, StandardTokenTypeData[][] tokens, string languageId)
    {
        var encodedLanguageId = (uint)model.Tokenization.LanguageIdCodec.EncodeLanguageId(languageId);
        TestTokenizationSupports.RegisterByLine(model, (lineNumber, line) =>
        {
            var tokensOnLine = tokens[lineNumber - 1];
            var result = new uint[2 * tokensOnLine.Length];
            for (var i = 0; i < tokensOnLine.Length; i++)
            {
                result[2 * i] = (uint)tokensOnLine[i].StartIndex;
                result[(2 * i) + 1] =
                    (encodedLanguageId << MetadataConsts.LanguageIdOffset)
                    | ((uint)tokensOnLine[i].StandardTokenType << MetadataConsts.TokenTypeOffset);
            }
            return result;
        });
    }
}
