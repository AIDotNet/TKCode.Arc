// Ported from vscode (src/vs/platform/contextkey/test/common/parser.test.ts, commit b57d5c9e), MIT license.
// All 33 upstream tests. The inputs and expected outputs were taken from the upstream file as JavaScript evaluates its
// string literals; non-ASCII characters are written as \u escapes.

using System.Text;
using Shouldly;
using TKCode.Arc.Editor.Platform;
using Xunit;

namespace TKCode.Arc.Editor.Tests;

public sealed class ContextKeyParserTests
{
    private static string ParseToStr(string input)
    {
        var parser = new Parser();

        var prints = new StringBuilder();

        var expr = parser.Parse(input);
        if (expr is null)
        {
            if (parser.LexingErrors.Count > 0)
            {
                prints.Append("Lexing errors:").Append("\n\n");
                foreach (var lexingError in parser.LexingErrors)
                {
                    // upstream prints `${undefined}` as "undefined"
                    prints.Append($"Unexpected token '{lexingError.Lexeme}' at offset {lexingError.Offset}. {lexingError.AdditionalInfo ?? "undefined"}").Append('\n');
                }
            }

            if (parser.ParsingErrors.Count > 0)
            {
                if (parser.LexingErrors.Count > 0)
                {
                    prints.Append("\n --- \n");
                }
                prints.Append("Parsing errors:").Append("\n\n");
                foreach (var parsingError in parser.ParsingErrors)
                {
                    prints.Append($"Unexpected '{parsingError.Lexeme}' at offset {parsingError.Offset}.").Append('\n');
                }
            }
        }
        else
        {
            prints.Append(expr.Serialize());
        }

        return prints.ToString();
    }

    [Fact]
    public void Parses_foo()
    {
        //  foo
        var input = " foo";
        ParseToStr(input).ShouldBe("foo");
    }

    [Fact]
    public void Parses_foo_2()
    {
        // !foo
        var input = "!foo";
        ParseToStr(input).ShouldBe("!foo");
    }

    [Fact]
    public void Parses_foo_bar()
    {
        // foo =~ /bar/
        var input = "foo =~ /bar/";
        ParseToStr(input).ShouldBe("foo =~ /bar/");
    }

    [Fact]
    public void Parses_foo_foo_bar_baz()
    {
        // foo || (foo =~ /bar/ && baz)
        var input = "foo || (foo =~ /bar/ && baz)";
        ParseToStr(input).ShouldBe("foo || baz && foo =~ /bar/");
    }

    [Fact]
    public void Parses_foo_foo_bar_baz_2()
    {
        // foo || (foo =~ /bar/ || baz)
        var input = "foo || (foo =~ /bar/ || baz)";
        ParseToStr(input).ShouldBe("baz || foo || foo =~ /bar/");
    }

    [Fact]
    public void Parses_foo_bar_jee_jar()
    {
        // (foo || bar) && (jee || jar)
        var input = "(foo || bar) && (jee || jar)";
        ParseToStr(input).ShouldBe("bar && jar || bar && jee || foo && jar || foo && jee");
    }

    [Fact]
    public void Parses_foo_foo_zee_i()
    {
        // foo && foo =~ /zee/i
        var input = "foo && foo =~ /zee/i";
        ParseToStr(input).ShouldBe("foo && foo =~ /zee/i");
    }

    [Fact]
    public void Parses_foo_bar_enabled()
    {
        // foo.bar==enabled
        var input = "foo.bar==enabled";
        ParseToStr(input).ShouldBe("foo.bar == 'enabled'");
    }

    [Fact]
    public void Parses_foo_bar_enabled_2()
    {
        // foo.bar == 'enabled'
        var input = "foo.bar == 'enabled'";
        ParseToStr(input).ShouldBe("foo.bar == 'enabled'");
    }

    [Fact]
    public void Parses_foo_bar_zed_completed_equality_with_no_space()
    {
        // foo.bar:zed==completed - equality with no space
        var input = "foo.bar:zed==completed";
        ParseToStr(input).ShouldBe("foo.bar:zed == 'completed'");
    }

    [Fact]
    public void Parses_a_b_c()
    {
        // a && b || c
        var input = "a && b || c";
        ParseToStr(input).ShouldBe("c || a && b");
    }

    [Fact]
    public void Parses_fooBar_baz_jar_fee_bee_K_loo_1()
    {
        // fooBar && baz.jar && fee.bee<K-loo+1>
        var input = "fooBar && baz.jar && fee.bee<K-loo+1>";
        ParseToStr(input).ShouldBe("baz.jar && fee.bee<K-loo+1> && fooBar");
    }

    [Fact]
    public void Parses_foo_barBaz_C_r_2()
    {
        // foo.barBaz<C-r> < 2
        var input = "foo.barBaz<C-r> < 2";
        ParseToStr(input).ShouldBe("foo.barBaz<C-r> < 2");
    }

    [Fact]
    public void Parses_foo_bar_1()
    {
        // foo.bar >= -1
        var input = "foo.bar >= -1";
        ParseToStr(input).ShouldBe("foo.bar >= -1");
    }

    [Fact]
    public void Parses_key_contains_nbsp_view_vsc_packages_activitybar_folders_vsc_packages_f()
    {
        // key contains &nbsp: view == vsc-packages-activitybar-folders\xa0&& vsc-packages-folders-loaded
        var input = "view == vsc-packages-activitybar-folders\u00A0&& vsc-packages-folders-loaded";
        ParseToStr(input).ShouldBe("vsc-packages-folders-loaded && view == 'vsc-packages-activitybar-folders'");
    }

    [Fact]
    public void Parses_foo_bar_1_2()
    {
        // foo.bar <= -1
        var input = "foo.bar <= -1";
        ParseToStr(input).ShouldBe("foo.bar <= -1");
    }

    [Fact]
    public void Parses_cmake_hideBuildCommand_cmake_enableFullFeatureSet()
    {
        // !cmake:hideBuildCommand && cmake:enableFullFeatureSet
        var input = "!cmake:hideBuildCommand && cmake:enableFullFeatureSet";
        ParseToStr(input).ShouldBe("cmake:enableFullFeatureSet && !cmake:hideBuildCommand");
    }

    [Fact]
    public void Parses_foo_bar_2()
    {
        // !(foo && bar)
        var input = "!(foo && bar)";
        ParseToStr(input).ShouldBe("!bar || !foo");
    }

    [Fact]
    public void Parses_foo_bar_boar_deer()
    {
        // !(foo && bar || boar) || deer
        var input = "!(foo && bar || boar) || deer";
        ParseToStr(input).ShouldBe("deer || !bar && !boar || !boar && !foo");
    }

    [Fact]
    public void Parses_foo_3()
    {
        // !(!foo)
        var input = "!(!foo)";
        ParseToStr(input).ShouldBe("foo");
    }

    [Fact]
    public void Controversial_debugState_stopped()
    {
        // controversial / debugState == "stopped"
        var input = "debugState == \"stopped\"";
        ParseToStr(input).ShouldBe("debugState == '\"stopped\"'");
    }

    [Fact]
    public void Controversial_viewItem_VSCode_WorkSpace()
    {
        // controversial /  viewItem == VSCode WorkSpace
        var input = " viewItem == VSCode WorkSpace";
        ParseToStr(input).ShouldBe("Parsing errors:\n\nUnexpected 'WorkSpace' at offset 20.\n");
    }

    [Fact]
    public void Regex_resource_foo_barr_door_Foo_Bar_20Templates_Soo_20Looo_Web_20Site()
    {
        // regex / resource =~ //foo/(barr|door/(Foo-Bar%20Templates|Soo%20Looo)|Web%20Site%Jjj%20Llll)(/.*)*$/
        var input = "resource =~ //foo/(barr|door/(Foo-Bar%20Templates|Soo%20Looo)|Web%20Site%Jjj%20Llll)(/.*)*$/";
        ParseToStr(input).ShouldBe("resource =~ /\\/foo\\/(barr|door\\/(Foo-Bar%20Templates|Soo%20Looo)|Web%20Site%Jjj%20Llll)(\\/.*)*$/");
    }

    [Fact]
    public void Regex_resource_scratch_update_src()
    {
        // regex / resource =~ /((/scratch/(?!update)(.*)/)|((/src/).*/)).*$/
        var input = "resource =~ /((/scratch/(?!update)(.*)/)|((/src/).*/)).*$/";
        ParseToStr(input).ShouldBe("resource =~ /((\\/scratch\\/(?!update)(.*)\\/)|((\\/src\\/).*\\/)).*$/");
    }

    [Fact]
    public void Regex_resourcePath_md_yml_txt_giym()
    {
        // regex / resourcePath =~ /.md(.yml|.txt)*$/giym
        var input = "resourcePath =~ /.md(.yml|.txt)*$/giym";
        ParseToStr(input).ShouldBe("resourcePath =~ /.md(.yml|.txt)*$/im");
    }

    [Fact]
    public void Error_foo()
    {
        // error handling / /foo
        var input = "/foo";
        ParseToStr(input).ShouldBe("Lexing errors:\n\nUnexpected token '/foo' at offset 0. Did you forget to escape the '/' (slash) character? Put two backslashes before it to escape, e.g., '\\\\/'.\n\n --- \nParsing errors:\n\nUnexpected '/foo' at offset 0.\n");
    }

    [Fact]
    public void Error_b_true()
    {
        // error handling / !b == 'true'
        var input = "!b == 'true'";
        ParseToStr(input).ShouldBe("Parsing errors:\n\nUnexpected '==' at offset 3.\n");
    }

    [Fact]
    public void Error_foo_in_bar()
    {
        // error handling / !foo &&  in bar
        var input = "!foo &&  in bar";
        ParseToStr(input).ShouldBe("Parsing errors:\n\nUnexpected 'in' at offset 9.\n");
    }

    [Fact]
    public void Error_vim_c_r_1_vim_2_3()
    {
        // error handling / vim<c-r> == 1 && vim<2<=3
        var input = "vim<c-r> == 1 && vim<2<=3";
        ParseToStr(input).ShouldBe("Lexing errors:\n\nUnexpected token '=' at offset 23. Did you mean == or =~?\n\n --- \nParsing errors:\n\nUnexpected '=' at offset 23.\n");
    }

    [Fact]
    public void Error_foo_bar()
    {
        // error handling / foo && 'bar
        var input = "foo && 'bar";
        ParseToStr(input).ShouldBe("Lexing errors:\n\nUnexpected token ''bar' at offset 7. Did you forget to open or close the quote?\n\n --- \nParsing errors:\n\nUnexpected ''bar' at offset 7.\n");
    }

    [Fact]
    public void Error_config_foo_bar_foo_bar_foo_joo_jar_foo()
    {
        // error handling / config.foo &&  &&bar =~ /^foo$|^bar-foo$|^joo$|^jar$/ && !foo
        var input = "config.foo &&  &&bar =~ /^foo$|^bar-foo$|^joo$|^jar$/ && !foo";
        ParseToStr(input).ShouldBe("Parsing errors:\n\nUnexpected '&&' at offset 15.\n");
    }

    [Fact]
    public void Error_foo_test()
    {
        // error handling / !foo == 'test'
        var input = "!foo == 'test'";
        ParseToStr(input).ShouldBe("Parsing errors:\n\nUnexpected '==' at offset 5.\n");
    }

    [Fact]
    public void Error_foo_2()
    {
        // error handling / !!foo
        var input = "!!foo";
        ParseToStr(input).ShouldBe("Parsing errors:\n\nUnexpected '!' at offset 1.\n");
    }
}
