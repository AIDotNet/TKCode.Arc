// Ported from vscode (src/vs/platform/contextkey/test/common/scanner.test.ts, commit b57d5c9e), MIT license.
// All 34 upstream tests. The inputs and expected tokens were taken from the upstream file as JavaScript evaluates
// its string literals; non-ASCII characters are written as \u escapes. A token is compared as (type, offset, lexeme),
// the lexeme only for the token types that have one, as upstream's scan() helper does.

using Shouldly;
using TKCode.Arc.Editor.Platform;
using Xunit;

namespace TKCode.Arc.Editor.Tests;

public sealed class ContextKeyScannerTests
{
    private static string TokenTypeToStr(Token token)
    {
        switch (token.Type)
        {
            case TokenType.LParen:
                return "(";
            case TokenType.RParen:
                return ")";
            case TokenType.Neg:
                return "!";
            case TokenType.Eq:
                return token.IsTripleEq ? "===" : "==";
            case TokenType.NotEq:
                return token.IsTripleEq ? "!==" : "!=";
            case TokenType.Lt:
                return "<";
            case TokenType.LtEq:
                return "<=";
            case TokenType.Gt:
                return ">";
            case TokenType.GtEq:
                return ">=";
            case TokenType.RegexOp:
                return "=~";
            case TokenType.RegexStr:
                return "RegexStr";
            case TokenType.True:
                return "true";
            case TokenType.False:
                return "false";
            case TokenType.In:
                return "in";
            case TokenType.Not:
                return "not";
            case TokenType.And:
                return "&&";
            case TokenType.Or:
                return "||";
            case TokenType.Str:
                return "Str";
            case TokenType.QuotedStr:
                return "QuotedStr";
            case TokenType.Error:
                return "ErrorToken";
            case TokenType.EOF:
                return "EOF";
            default:
                throw new InvalidOperationException(token.ToString());
        }
    }

    private static (string Type, int Offset, string? Lexeme) T(string type, int offset, string? lexeme = null) => (type, offset, lexeme);

    private static (string Type, int Offset, string? Lexeme)[] Scan(string input)
    {
        return new Scanner().Reset(input).Scan().Select(token => (TokenTypeToStr(token), token.Offset, token.Lexeme)).ToArray();
    }

    [Fact]
    public void Scans_foo_bar_C_shift_2()
    {
        // scanning various cases of context keys / foo.bar<C-shift+2>
        var input = "foo.bar<C-shift+2>";
        Scan(input).ShouldBe(new[]
        {
            T("Str", 0, "foo.bar<C-shift+2>"),
            T("EOF", 18),
        });
    }

    [Fact]
    public void Scans_foo()
    {
        // scanning various cases of context keys / !foo
        var input = "!foo";
        Scan(input).ShouldBe(new[]
        {
            T("!", 0),
            T("Str", 1, "foo"),
            T("EOF", 4),
        });
    }

    [Fact]
    public void Scans_foo_bar()
    {
        // scanning various cases of context keys / foo === bar
        var input = "foo === bar";
        Scan(input).ShouldBe(new[]
        {
            T("Str", 0, "foo"),
            T("===", 4),
            T("Str", 8, "bar"),
            T("EOF", 11),
        });
    }

    [Fact]
    public void Scans_foo_bar_2()
    {
        // scanning various cases of context keys / foo  !== bar
        var input = "foo  !== bar";
        Scan(input).ShouldBe(new[]
        {
            T("Str", 0, "foo"),
            T("!==", 5),
            T("Str", 9, "bar"),
            T("EOF", 12),
        });
    }

    [Fact]
    public void Scans_foo_bar_3()
    {
        // scanning various cases of context keys / !(foo && bar)
        var input = "!(foo && bar)";
        Scan(input).ShouldBe(new[]
        {
            T("!", 0),
            T("(", 1),
            T("Str", 2, "foo"),
            T("&&", 6),
            T("Str", 9, "bar"),
            T(")", 12),
            T("EOF", 13),
        });
    }

    [Fact]
    public void Scans_RegexOp()
    {
        // scanning various cases of context keys / =~ 
        var input = "=~ ";
        Scan(input).ShouldBe(new[]
        {
            T("=~", 0),
            T("EOF", 3),
        });
    }

    [Fact]
    public void Scans_foo_bar_4()
    {
        // scanning various cases of context keys / foo =~ /bar/
        var input = "foo =~ /bar/";
        Scan(input).ShouldBe(new[]
        {
            T("Str", 0, "foo"),
            T("=~", 4),
            T("RegexStr", 7, "/bar/"),
            T("EOF", 12),
        });
    }

    [Fact]
    public void Scans_foo_zee_i()
    {
        // scanning various cases of context keys / foo =~ /zee/i
        var input = "foo =~ /zee/i";
        Scan(input).ShouldBe(new[]
        {
            T("Str", 0, "foo"),
            T("=~", 4),
            T("RegexStr", 7, "/zee/i"),
            T("EOF", 13),
        });
    }

    [Fact]
    public void Scans_foo_zee_gm()
    {
        // scanning various cases of context keys / foo =~ /zee/gm
        var input = "foo =~ /zee/gm";
        Scan(input).ShouldBe(new[]
        {
            T("Str", 0, "foo"),
            T("=~", 4),
            T("RegexStr", 7, "/zee/gm"),
            T("EOF", 14),
        });
    }

    [Fact]
    public void Scans_foo_in_barrr()
    {
        // scanning various cases of context keys / foo in barrr  
        var input = "foo in barrr  ";
        Scan(input).ShouldBe(new[]
        {
            T("Str", 0, "foo"),
            T("in", 4),
            T("Str", 7, "barrr"),
            T("EOF", 14),
        });
    }

    [Fact]
    public void Scans_resource_FileCabinet_SuiteScrip()
    {
        // scanning various cases of context keys / resource =~ //FileCabinet/(SuiteScripts|Templates/(E-mail%20Templates|Marketing%20Templates)|Web%20Site%20Hosting%20Files)(/.*)*$/
        var input = "resource =~ //FileCabinet/(SuiteScripts|Templates/(E-mail%20Templates|Marketing%20Templates)|Web%20Site%20Hosting%20Files)(/.*)*$/";
        Scan(input).ShouldBe(new[]
        {
            T("Str", 0, "resource"),
            T("=~", 9),
            T("RegexStr", 12, "//"),
            T("Str", 14, "FileCabinet/"),
            T("(", 26),
            T("Str", 27, "SuiteScripts"),
            T("ErrorToken", 39, "|"),
            T("Str", 40, "Templates/"),
            T("(", 50),
            T("Str", 51, "E-mail%20Templates"),
            T("ErrorToken", 69, "|"),
            T("Str", 70, "Marketing%20Templates"),
            T(")", 91),
            T("ErrorToken", 92, "|"),
            T("Str", 93, "Web%20Site%20Hosting%20Files"),
            T(")", 121),
            T("(", 122),
            T("RegexStr", 123, "/.*)*$/"),
            T("EOF", 130),
        });
    }

    [Fact]
    public void Scans_editorLangId_in_testely_support()
    {
        // scanning various cases of context keys / editorLangId in testely.supportedLangIds && resourceFilename =~ /^.+(.test.(w+))$/gm
        var input = "editorLangId in testely.supportedLangIds && resourceFilename =~ /^.+(.test.(w+))$/gm";
        Scan(input).ShouldBe(new[]
        {
            T("Str", 0, "editorLangId"),
            T("in", 13),
            T("Str", 16, "testely.supportedLangIds"),
            T("&&", 41),
            T("Str", 44, "resourceFilename"),
            T("=~", 61),
            T("RegexStr", 64, "/^.+(.test.(w+))$/gm"),
            T("EOF", 84),
        });
    }

    [Fact]
    public void Scans_foo_bar_baz()
    {
        // scanning various cases of context keys / !(foo && bar) && baz
        var input = "!(foo && bar) && baz";
        Scan(input).ShouldBe(new[]
        {
            T("!", 0),
            T("(", 1),
            T("Str", 2, "foo"),
            T("&&", 6),
            T("Str", 9, "bar"),
            T(")", 12),
            T("&&", 14),
            T("Str", 17, "baz"),
            T("EOF", 20),
        });
    }

    [Fact]
    public void Scans_foo_bar_zed_completed_equality()
    {
        // scanning various cases of context keys / foo.bar:zed==completed - equality with no space
        var input = "foo.bar:zed==completed";
        Scan(input).ShouldBe(new[]
        {
            T("Str", 0, "foo.bar:zed"),
            T("==", 11),
            T("Str", 13, "completed"),
            T("EOF", 22),
        });
    }

    [Fact]
    public void Scans_a_b_c()
    {
        // scanning various cases of context keys / a && b || c
        var input = "a && b || c";
        Scan(input).ShouldBe(new[]
        {
            T("Str", 0, "a"),
            T("&&", 2),
            T("Str", 5, "b"),
            T("||", 7),
            T("Str", 10, "c"),
            T("EOF", 11),
        });
    }

    [Fact]
    public void Scans_fooBar_baz_jar_fee_bee_K_loo_1()
    {
        // scanning various cases of context keys / fooBar && baz.jar && fee.bee<K-loo+1>
        var input = "fooBar && baz.jar && fee.bee<K-loo+1>";
        Scan(input).ShouldBe(new[]
        {
            T("Str", 0, "fooBar"),
            T("&&", 7),
            T("Str", 10, "baz.jar"),
            T("&&", 18),
            T("Str", 21, "fee.bee<K-loo+1>"),
            T("EOF", 37),
        });
    }

    [Fact]
    public void Scans_foo_barBaz_C_r_2()
    {
        // scanning various cases of context keys / foo.barBaz<C-r> < 2
        var input = "foo.barBaz<C-r> < 2";
        Scan(input).ShouldBe(new[]
        {
            T("Str", 0, "foo.barBaz<C-r>"),
            T("<", 16),
            T("Str", 18, "2"),
            T("EOF", 19),
        });
    }

    [Fact]
    public void Scans_foo_bar_1()
    {
        // scanning various cases of context keys / foo.bar >= -1
        var input = "foo.bar >= -1";
        Scan(input).ShouldBe(new[]
        {
            T("Str", 0, "foo.bar"),
            T(">=", 8),
            T("Str", 11, "-1"),
            T("EOF", 13),
        });
    }

    [Fact]
    public void Scans_foo_bar_1_2()
    {
        // scanning various cases of context keys / foo.bar <= -1
        var input = "foo.bar <= -1";
        Scan(input).ShouldBe(new[]
        {
            T("Str", 0, "foo.bar"),
            T("<=", 8),
            T("Str", 11, "-1"),
            T("EOF", 13),
        });
    }

    [Fact]
    public void Scans_resource_Objects_xml()
    {
        // scanning various cases of context keys / resource =~ /\/Objects\/.+\.xml$/
        var input = "resource =~ /\\/Objects\\/.+\\.xml$/";
        Scan(input).ShouldBe(new[]
        {
            T("Str", 0, "resource"),
            T("=~", 9),
            T("RegexStr", 12, "/\\/Objects\\/.+\\.xml$/"),
            T("EOF", 33),
        });
    }

    [Fact]
    public void Scans_view_vsc_packages_activitybar_f()
    {
        // scanning various cases of context keys / view == vsc-packages-activitybar-folders\xa0&& vsc-packages-folders-loaded
        var input = "view == vsc-packages-activitybar-folders\u00A0&& vsc-packages-folders-loaded";
        Scan(input).ShouldBe(new[]
        {
            T("Str", 0, "view"),
            T("==", 5),
            T("Str", 8, "vsc-packages-activitybar-folders"),
            T("&&", 41),
            T("Str", 44, "vsc-packages-folders-loaded"),
            T("EOF", 71),
        });
    }

    [Fact]
    public void Scans_sfdx_project_opened_resource_fu()
    {
        // scanning various cases of context keys / sfdx:project_opened && resource =~ /.*\/functions\/.*\/[^\/]+(\/[^\/]+.(ts|js|java|json|toml))?$/ && resourceFilename != package.json && resourceFilename != package-lock.json && resourceFilename != tsconfig.json
        var input = "sfdx:project_opened && resource =~ /.*\\/functions\\/.*\\/[^\\/]+(\\/[^\\/]+.(ts|js|java|json|toml))?$/ && resourceFilename != package.json && resourceFilename != package-lock.json && resourceFilename != tsconfig.json";
        Scan(input).ShouldBe(new[]
        {
            T("Str", 0, "sfdx:project_opened"),
            T("&&", 20),
            T("Str", 23, "resource"),
            T("=~", 32),
            T("RegexStr", 35, "/.*\\/functions\\/.*\\/[^\\/]+(\\/[^\\/]+.(ts|js|java|json|toml))?$/"),
            T("&&", 98),
            T("Str", 101, "resourceFilename"),
            T("!=", 118),
            T("Str", 121, "package.json"),
            T("&&", 134),
            T("Str", 137, "resourceFilename"),
            T("!=", 154),
            T("Str", 157, "package-lock.json"),
            T("&&", 175),
            T("Str", 178, "resourceFilename"),
            T("!=", 195),
            T("Str", 198, "tsconfig.json"),
            T("EOF", 211),
        });
    }

    [Fact]
    public void Scans_view_servers_viewItem_Starting()
    {
        // scanning various cases of context keys / view =~ '/(servers)/' && viewItem =~ '/^(Starting|Started|Debugging|Stopping|Stopped)/'
        var input = "view =~ '/(servers)/' && viewItem =~ '/^(Starting|Started|Debugging|Stopping|Stopped)/'";
        Scan(input).ShouldBe(new[]
        {
            T("Str", 0, "view"),
            T("=~", 5),
            T("QuotedStr", 9, "/(servers)/"),
            T("&&", 22),
            T("Str", 25, "viewItem"),
            T("=~", 34),
            T("QuotedStr", 38, "/^(Starting|Started|Debugging|Stopping|Stopped)/"),
            T("EOF", 87),
        });
    }

    [Fact]
    public void Scans_resourcePath_md_yml_txt_gim()
    {
        // scanning various cases of context keys / resourcePath =~ /.md(.yml|.txt)*$/gim
        var input = "resourcePath =~ /.md(.yml|.txt)*$/gim";
        Scan(input).ShouldBe(new[]
        {
            T("Str", 0, "resourcePath"),
            T("=~", 13),
            T("RegexStr", 16, "/.md(.yml|.txt)*$/gim"),
            T("EOF", 37),
        });
    }

    [Fact]
    public void Scans_foo_bar_with_stray_quote()
    {
        // foo === bar'
        var input = "foo === bar'";
        Scan(input).ShouldBe(new[]
        {
            T("Str", 0, "foo"),
            T("===", 4),
            T("Str", 8, "bar"),
            T("ErrorToken", 11, "'"),
            T("EOF", 12),
        });
    }

    [Fact]
    public void LexicalError_foo()
    {
        // handling lexical errors / foo === '
        var input = "foo === '";
        Scan(input).ShouldBe(new[]
        {
            T("Str", 0, "foo"),
            T("===", 4),
            T("ErrorToken", 8, "'"),
            T("EOF", 9),
        });
    }

    [Fact]
    public void LexicalError_foo_bar_unterminated_single_quote()
    {
        // handling lexical errors / foo && 'bar - unterminated single quote
        var input = "foo && 'bar";
        Scan(input).ShouldBe(new[]
        {
            T("Str", 0, "foo"),
            T("&&", 4),
            T("ErrorToken", 7, "'bar"),
            T("EOF", 11),
        });
    }

    [Fact]
    public void LexicalError_vim_c_r_1_vim_2_3()
    {
        // handling lexical errors / vim<c-r> == 1 && vim<2 <= 3
        var input = "vim<c-r> == 1 && vim<2 <= 3";
        Scan(input).ShouldBe(new[]
        {
            T("Str", 0, "vim<c-r>"),
            T("==", 9),
            T("Str", 12, "1"),
            T("&&", 14),
            T("Str", 17, "vim<2"),
            T("<=", 23),
            T("Str", 26, "3"),
            T("EOF", 27),
        });
    }

    [Fact]
    public void LexicalError_vim_c_r_1_vim_2_3_2()
    {
        // handling lexical errors / vim<c-r>==1 && vim<2<=3
        var input = "vim<c-r>==1 && vim<2<=3";
        Scan(input).ShouldBe(new[]
        {
            T("Str", 0, "vim<c-r>"),
            T("==", 8),
            T("Str", 10, "1"),
            T("&&", 12),
            T("Str", 15, "vim<2<"),
            T("ErrorToken", 21, "="),
            T("Str", 22, "3"),
            T("EOF", 23),
        });
    }

    [Fact]
    public void LexicalError_foo_bar()
    {
        // handling lexical errors / foo|bar
        var input = "foo|bar";
        Scan(input).ShouldBe(new[]
        {
            T("Str", 0, "foo"),
            T("ErrorToken", 3, "|"),
            T("Str", 4, "bar"),
            T("EOF", 7),
        });
    }

    [Fact]
    public void LexicalError_resource_foo_barr_door_Foo_Bar_20Templates_Soo()
    {
        // handling lexical errors / resource =~ //foo/(barr|door/(Foo-Bar%20Templates|Soo%20Looo)|Web%20Site%Jjj%20Llll)(/.*)*$/
        var input = "resource =~ //foo/(barr|door/(Foo-Bar%20Templates|Soo%20Looo)|Web%20Site%Jjj%20Llll)(/.*)*$/";
        Scan(input).ShouldBe(new[]
        {
            T("Str", 0, "resource"),
            T("=~", 9),
            T("RegexStr", 12, "//"),
            T("Str", 14, "foo/"),
            T("(", 18),
            T("Str", 19, "barr"),
            T("ErrorToken", 23, "|"),
            T("Str", 24, "door/"),
            T("(", 29),
            T("Str", 30, "Foo-Bar%20Templates"),
            T("ErrorToken", 49, "|"),
            T("Str", 50, "Soo%20Looo"),
            T(")", 60),
            T("ErrorToken", 61, "|"),
            T("Str", 62, "Web%20Site%Jjj%20Llll"),
            T(")", 83),
            T("(", 84),
            T("RegexStr", 85, "/.*)*$/"),
            T("EOF", 92),
        });
    }

    [Fact]
    public void LexicalError_foo_bar_src()
    {
        // handling lexical errors / /((/foo/(?!bar)(.*)/)|((/src/).*/)).*$/
        var input = "/((/foo/(?!bar)(.*)/)|((/src/).*/)).*$/";
        Scan(input).ShouldBe(new[]
        {
            T("RegexStr", 0, "/((/"),
            T("Str", 4, "foo/"),
            T("(", 8),
            T("Str", 9, "?"),
            T("!", 10),
            T("Str", 11, "bar"),
            T(")", 14),
            T("(", 15),
            T("Str", 16, ".*"),
            T(")", 18),
            T("RegexStr", 19, "/)|((/s"),
            T("Str", 26, "rc/"),
            T(")", 29),
            T("Str", 30, ".*/"),
            T(")", 33),
            T(")", 34),
            T("Str", 35, ".*$/"),
            T("EOF", 39),
        });
    }

    [Fact]
    public void LexicalError_resourcePath_foo_barr_resourcePath_view_jarrr()
    {
        // handling lexical errors / resourcePath =~ //foo/barr// || resourcePath =~ //view/(jarrr|doooor|bees)/(web|templates)// && resourceExtname in foo.Bar
        var input = "resourcePath =~ //foo/barr// || resourcePath =~ //view/(jarrr|doooor|bees)/(web|templates)// && resourceExtname in foo.Bar";
        Scan(input).ShouldBe(new[]
        {
            T("Str", 0, "resourcePath"),
            T("=~", 13),
            T("RegexStr", 16, "//"),
            T("Str", 18, "foo/barr//"),
            T("||", 29),
            T("Str", 32, "resourcePath"),
            T("=~", 45),
            T("RegexStr", 48, "//"),
            T("Str", 50, "view/"),
            T("(", 55),
            T("Str", 56, "jarrr"),
            T("ErrorToken", 61, "|"),
            T("Str", 62, "doooor"),
            T("ErrorToken", 68, "|"),
            T("Str", 69, "bees"),
            T(")", 73),
            T("RegexStr", 74, "/(web|templates)/"),
            T("ErrorToken", 91, "/ && resourceExtname in foo.Bar"),
            T("EOF", 122),
        });
    }

    [Fact]
    public void LexicalError_foo_file_bar()
    {
        // handling lexical errors / foo =~ /file:// || bar
        var input = "foo =~ /file:// || bar";
        Scan(input).ShouldBe(new[]
        {
            T("Str", 0, "foo"),
            T("=~", 4),
            T("RegexStr", 7, "/file:/"),
            T("ErrorToken", 14, "/ || bar"),
            T("EOF", 22),
        });
    }
}
