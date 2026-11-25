// Ported from vscode (src/vs/editor/contrib/find/test/browser/replacePattern.test.ts, commit b57d5c9e), MIT license.
// All 8 upstream tests. JavaScript's RegExp.exec is a .NET match of the regular expression literal (JsRegex.FromLiteral)
// turned into the match and its groups, null for a group that did not participate; the expected values upstream
// computes with String.prototype.replace are written out.

using Shouldly;
using TKCode.Arc.Editor.Contrib.Find;
using TKCode.Arc.Editor.Model;
using Xunit;

namespace TKCode.Arc.Editor.Tests;

public sealed class ReplacePatternTests
{
    private static string?[]? Exec(string target, string regexSource)
    {
        var match = JsRegex.FromLiteral(regexSource).Match(target);
        if (!match.Success)
        {
            return null;
        }

        var result = new string?[match.Groups.Count];
        for (var i = 0; i < result.Length; i++)
        {
            result[i] = match.Groups[i].Success ? match.Groups[i].Value : null;
        }

        return result;
    }

    private static void TestParse(string input, params ReplacePiece[] expectedPieces)
    {
        var actual = ReplacePatternParser.ParseReplaceString(input);
        var expected = new ReplacePattern(expectedPieces);
        actual.ShouldBe(expected, "Parsing " + input);
    }

    private static void AssertReplace(string target, string search, string replaceString, string expected)
    {
        var replacePattern = ReplacePatternParser.ParseReplaceString(replaceString);
        var m = Exec(target, search);
        var actual = replacePattern.BuildReplaceString(m);
        actual.ShouldBe(expected, $"{target}.replace(/{search}/, {replaceString}) === {expected}");
    }

    private static ReplacePiece S(string value) => ReplacePiece.FromStaticValue(value);

    private static ReplacePiece M(int index) => ReplacePiece.FromMatchIndex(index);

    private static ReplacePiece C(int index, string caseOps) => ReplacePiece.FromCaseOps(index, caseOps.ToCharArray());

    [Fact]
    public void Parse_replace_string()
    {
        // no backslash => no treatment
        TestParse("hello", S("hello"));

        // \t => TAB
        TestParse("\\thello", S("\thello"));
        TestParse("h\\tello", S("h\tello"));
        TestParse("hello\\t", S("hello\t"));

        // \n => LF
        TestParse("\\nhello", S("\nhello"));

        // \\t => \t
        TestParse("\\\\thello", S("\\thello"));
        TestParse("h\\\\tello", S("h\\tello"));
        TestParse("hello\\\\t", S("hello\\t"));

        // \\\t => \TAB
        TestParse("\\\\\\thello", S("\\\thello"));

        // \\\\t => \\t
        TestParse("\\\\\\\\thello", S("\\\\thello"));

        // \ at the end => no treatment
        TestParse("hello\\", S("hello\\"));

        // \ with unknown char => no treatment
        TestParse("hello\\x", S("hello\\x"));

        // \ with back reference => no treatment
        TestParse("hello\\0", S("hello\\0"));

        TestParse("hello$&", S("hello"), M(0));
        TestParse("hello$0", S("hello"), M(0));
        TestParse("hello$02", S("hello"), M(0), S("2"));
        TestParse("hello$1", S("hello"), M(1));
        TestParse("hello$2", S("hello"), M(2));
        TestParse("hello$9", S("hello"), M(9));
        TestParse("$9hello", M(9), S("hello"));

        TestParse("hello$12", S("hello"), M(12));
        TestParse("hello$99", S("hello"), M(99));
        TestParse("hello$99a", S("hello"), M(99), S("a"));
        TestParse("hello$1a", S("hello"), M(1), S("a"));
        TestParse("hello$100", S("hello"), M(10), S("0"));
        TestParse("hello$100a", S("hello"), M(10), S("0a"));
        TestParse("hello$10a0", S("hello"), M(10), S("a0"));
        TestParse("hello$$", S("hello$"));
        TestParse("hello$$0", S("hello$0"));

        TestParse("hello$`", S("hello$`"));
        TestParse("hello$'", S("hello$'"));
    }

    [Fact]
    public void Parse_replace_string_with_case_modifiers()
    {
        // \U, \u => uppercase  \L, \l => lowercase  \E => cancel

        TestParse("hello\\U$1", S("hello"), C(1, "U"));
        AssertReplace("func privateFunc(", "func (\\w+)\\(", "func \\U$1(", "func PRIVATEFUNC(");

        TestParse("hello\\u$1", S("hello"), C(1, "u"));
        AssertReplace("func privateFunc(", "func (\\w+)\\(", "func \\u$1(", "func PrivateFunc(");

        TestParse("hello\\L$1", S("hello"), C(1, "L"));
        AssertReplace("func privateFunc(", "func (\\w+)\\(", "func \\L$1(", "func privatefunc(");

        TestParse("hello\\l$1", S("hello"), C(1, "l"));
        AssertReplace("func PrivateFunc(", "func (\\w+)\\(", "func \\l$1(", "func privateFunc(");

        TestParse("hello$1\\u\\u\\U$4goodbye", S("hello"), M(1), C(4, "uuU"), S("goodbye"));
        AssertReplace("hellogooDbye", "hello(\\w+)", "hello\\u\\u\\l\\l\\U$1", "helloGOodBYE");
    }

    [Fact]
    public void Replace_has_JavaScript_semantics()
    {
        static void TestJsReplaceSemantics(string target, string search, string replaceString, string expected)
        {
            var replacePattern = ReplacePatternParser.ParseReplaceString(replaceString);
            var m = Exec(target, search);
            replacePattern.BuildReplaceString(m).ShouldBe(expected, $"{target}.replace(/{search}/, {replaceString})");
        }

        TestJsReplaceSemantics("hi", "hi", "hello", "hello");
        TestJsReplaceSemantics("hi", "hi", "\\t", "\t");
        TestJsReplaceSemantics("hi", "hi", "\\n", "\n");
        TestJsReplaceSemantics("hi", "hi", "\\\\t", "\\t");
        TestJsReplaceSemantics("hi", "hi", "\\\\n", "\\n");

        // implicit capture group 0
        TestJsReplaceSemantics("hi", "hi", "hello$&", "hellohi");
        TestJsReplaceSemantics("hi", "hi", "hello$0", "hellohi");
        TestJsReplaceSemantics("hi", "hi", "hello$&1", "hellohi1");
        TestJsReplaceSemantics("hi", "hi", "hello$01", "hellohi1");

        // capture groups have funny semantics in replace strings
        // the replace string interprets $nn as a captured group only if it exists in the search regex
        TestJsReplaceSemantics("hi", "(hi)", "hello$10", "hellohi0");
        TestJsReplaceSemantics("hi", "(hi)()()()()()()()()()", "hello$10", "hello");
        TestJsReplaceSemantics("hi", "(hi)", "hello$100", "hellohi00");
        TestJsReplaceSemantics("hi", "(hi)", "hello$20", "hello$20");
    }

    [Fact]
    public void Get_replace_string_if_given_text_is_a_complete_match()
    {
        AssertReplace("bla", "bla", "hello", "hello");
        AssertReplace("bla", "(bla)", "hello", "hello");
        AssertReplace("bla", "(bla)", "hello$0", "hellobla");

        const string searchRegex = "let\\s+(\\w+)\\s*=\\s*require\\s*\\(\\s*['\"]([\\w\\.\\-/]+)\\s*['\"]\\s*\\)\\s*";
        AssertReplace("let fs = require('fs')", searchRegex, "import * as $1 from '$2';", "import * as fs from 'fs';");
        AssertReplace("let something = require('fs')", searchRegex, "import * as $1 from '$2';", "import * as something from 'fs';");
        AssertReplace("let something = require('fs')", searchRegex, "import * as $1 from '$1';", "import * as something from 'something';");
        AssertReplace("let something = require('fs')", searchRegex, "import * as $2 from '$1';", "import * as fs from 'something';");
        AssertReplace("let something = require('fs')", searchRegex, "import * as $0 from '$0';", "import * as let something = require('fs') from 'let something = require('fs')';");
        AssertReplace("let fs = require('fs')", searchRegex, "import * as $1 from '$2';", "import * as fs from 'fs';");
        AssertReplace("for ()", "for(.*)", "cat$1", "cat ()");

        // issue #18111
        AssertReplace("HRESULT OnAmbientPropertyChange(DISPID   dispid);", "\\b\\s{3}\\b", " ", " ");
    }

    [Fact]
    public void Get_replace_string_if_match_is_sub_string_of_the_text()
    {
        AssertReplace("this is a bla text", "bla", "hello", "hello");
        AssertReplace("this is a bla text", "this(?=.*bla)", "that", "that");
        AssertReplace("this is a bla text", "(th)is(?=.*bla)", "$1at", "that");
        AssertReplace("this is a bla text", "(th)is(?=.*bla)", "$1e", "the");
        AssertReplace("this is a bla text", "(th)is(?=.*bla)", "$1ere", "there");
        AssertReplace("this is a bla text", "(th)is(?=.*bla)", "$1", "th");
        AssertReplace("this is a bla text", "(th)is(?=.*bla)", "ma$1", "math");
        AssertReplace("this is a bla text", "(th)is(?=.*bla)", "ma$1s", "maths");
        AssertReplace("this is a bla text", "(th)is(?=.*bla)", "$0", "this");
        AssertReplace("this is a bla text", "(th)is(?=.*bla)", "$0$1", "thisth");
        AssertReplace("this is a bla text", "bla(?=\\stext$)", "foo", "foo");
        AssertReplace("this is a bla text", "b(la)(?=\\stext$)", "f$1", "fla");
        AssertReplace("this is a bla text", "b(la)(?=\\stext$)", "f$0", "fbla");
        AssertReplace("this is a bla text", "b(la)(?=\\stext$)", "$0ah", "blaah");
    }

    [Fact]
    public void Issue_19740_find_and_replace_capture_group_inserts_undefined_instead_of_empty_string()
    {
        var replacePattern = ReplacePatternParser.ParseReplaceString("a{$1}");
        var matches = Exec("abcd", "a(z)?");
        replacePattern.BuildReplaceString(matches).ShouldBe("a{}");
    }

    private static readonly (string[] Target, string Replace, string Expected)[] s_casePreservedCases =
    [
        (["abc"], "Def", "def"),
        (["Abc"], "Def", "Def"),
        (["ABC"], "Def", "DEF"),
        (["abc", "Abc"], "Def", "def"),
        (["Abc", "abc"], "Def", "Def"),
        (["ABC", "abc"], "Def", "DEF"),
        (["aBc", "abc"], "Def", "def"),
        (["AbC"], "Def", "Def"),
        (["aBC"], "Def", "def"),
        (["aBc"], "DeF", "deF"),
        (["Foo-Bar"], "newfoo-newbar", "Newfoo-Newbar"),
        (["Foo-Bar-Abc"], "newfoo-newbar-newabc", "Newfoo-Newbar-Newabc"),
        (["Foo-Bar-abc"], "newfoo-newbar", "Newfoo-newbar"),
        (["foo-Bar"], "newfoo-newbar", "newfoo-Newbar"),
        (["foo-BAR"], "newfoo-newbar", "newfoo-NEWBAR"),
        (["foO-BAR"], "NewFoo-NewBar", "newFoo-NEWBAR"),
        (["Foo_Bar"], "newfoo_newbar", "Newfoo_Newbar"),
        (["Foo_Bar_Abc"], "newfoo_newbar_newabc", "Newfoo_Newbar_Newabc"),
        (["Foo_Bar_abc"], "newfoo_newbar", "Newfoo_newbar"),
        (["Foo_Bar-abc"], "newfoo_newbar-abc", "Newfoo_newbar-abc"),
        (["foo_Bar"], "newfoo_newbar", "newfoo_Newbar"),
    ];

    [Fact]
    public void BuildReplaceStringWithCasePreserved_test()
    {
        foreach (var (target, replace, expected) in s_casePreservedCases)
        {
            Search.BuildReplaceStringWithCasePreserved(target, replace).ShouldBe(expected);
        }

        Search.BuildReplaceStringWithCasePreserved(["Foo_BAR"], "newfoo_newbar").ShouldBe("Newfoo_NEWBAR");
    }

    [Fact]
    public void Preserve_case()
    {
        foreach (var (target, replace, expected) in s_casePreservedCases)
        {
            ReplacePatternParser.ParseReplaceString(replace).BuildReplaceString(target, true).ShouldBe(expected);
        }

        ReplacePatternParser.ParseReplaceString("newfoo_newbar").BuildReplaceString(["foo_BAR"], true).ShouldBe("newfoo_NEWBAR");
    }
}
