// Ported from vscode (src/vs/editor/test/common/modes/supports/onEnter.test.ts, commit b57d5c9e), MIT license.
// The `/gm` rule of Issue #121125 is a Regex with the multiline option; a .NET Regex has no global state to reset.

using Shouldly;
using TKCode.Arc.Editor.Config;
using TKCode.Arc.Editor.Languages;
using Xunit;

namespace TKCode.Arc.Editor.Tests;

public sealed class OnEnterTests
{
    [Fact]
    public void Uses_Brackets()
    {
        CharacterPair[] brackets =
        [
            ("(", ")"),
            ("begin", "end"),
        ];
        var support = new OnEnterSupport(new OnEnterSupportOptions
        {
            Brackets = brackets,
        });
        void TestIndentAction(string beforeText, string afterText, IndentAction expected)
        {
            var actual = support.OnEnter(EditorAutoIndentStrategy.Advanced, "", beforeText, afterText);
            if (expected == IndentAction.None)
            {
                actual.ShouldBeNull();
            }
            else
            {
                actual!.IndentAction.ShouldBe(expected);
            }
        }

        TestIndentAction("a", "", IndentAction.None);
        TestIndentAction("", "b", IndentAction.None);
        TestIndentAction("(", "b", IndentAction.Indent);
        TestIndentAction("a", ")", IndentAction.None);
        TestIndentAction("begin", "ending", IndentAction.Indent);
        TestIndentAction("abegin", "end", IndentAction.None);
        TestIndentAction("begin", ")", IndentAction.Indent);
        TestIndentAction("begin", "end", IndentAction.IndentOutdent);
        TestIndentAction("begin ", " end", IndentAction.IndentOutdent);
        TestIndentAction(" begin", "end//as", IndentAction.IndentOutdent);
        TestIndentAction("(", ")", IndentAction.IndentOutdent);
        TestIndentAction("( ", ")", IndentAction.IndentOutdent);
        TestIndentAction("a(", ")b", IndentAction.IndentOutdent);

        TestIndentAction("(", "", IndentAction.Indent);
        TestIndentAction("(", "foo", IndentAction.Indent);
        TestIndentAction("begin", "foo", IndentAction.Indent);
        TestIndentAction("begin", "", IndentAction.Indent);
    }

    private static void TestIndentAction(
        OnEnterSupport support,
        string previousLineText,
        string beforeText,
        string afterText,
        IndentAction? expectedIndentAction,
        string? expectedAppendText,
        int removeText = 0)
    {
        var actual = support.OnEnter(EditorAutoIndentStrategy.Advanced, previousLineText, beforeText, afterText);
        if (expectedIndentAction is null)
        {
            actual.ShouldBeNull("isNull:" + beforeText);
        }
        else
        {
            actual.ShouldNotBeNull("isNotNull:" + beforeText);
            actual.IndentAction.ShouldBe(expectedIndentAction.Value, "indentAction:" + beforeText);
            if (expectedAppendText is not null)
            {
                actual.AppendText.ShouldBe(expectedAppendText, "appendText:" + beforeText);
            }
            if (removeText != 0)
            {
                actual.RemoveText.ShouldBe(removeText, "removeText:" + beforeText);
            }
        }
    }

    [Fact]
    public void Issue_121125_OnEnterRules_With_Global_Modifier()
    {
        var support = new OnEnterSupport(new OnEnterSupportOptions
        {
            OnEnterRules =
            [
                new OnEnterRule(
                    JsRegExp.Of(@"^\s*\/{3}.*$", "gm"),
                    new EnterAction(IndentAction.Outdent) { AppendText = "/// " }),
            ],
        });

        TestIndentAction(support, "/// line", "/// line", "", IndentAction.Outdent, "/// ");
        TestIndentAction(support, "/// line", "/// line", "", IndentAction.Outdent, "/// ");
    }

    [Fact]
    public void Uses_RegExpRules()
    {
        var support = new OnEnterSupport(new OnEnterSupportOptions
        {
            OnEnterRules = OnEnterRules.JavascriptOnEnterRules,
        });
        void T(string previousLineText, string beforeText, string afterText, IndentAction? expectedIndentAction, string? expectedAppendText, int removeText = 0)
        {
            TestIndentAction(support, previousLineText, beforeText, afterText, expectedIndentAction, expectedAppendText, removeText);
        }

        T("", "\t/**", " */", IndentAction.IndentOutdent, " * ");
        T("", "\t/**", "", IndentAction.None, " * ");
        T("", "\t/** * / * / * /", "", IndentAction.None, " * ");
        T("", "\t/** /*", "", IndentAction.None, " * ");
        T("", "/**", "", IndentAction.None, " * ");
        T("", "\t/**/", "", null, null);
        T("", "\t/***/", "", null, null);
        T("", "\t/*******/", "", null, null);
        T("", "\t/** * * * * */", "", null, null);
        T("", "\t/** */", "", null, null);
        T("", "\t/** asdfg */", "", null, null);
        T("", "\t/* asdfg */", "", null, null);
        T("", "\t/* asdfg */", "", null, null);
        T("", "\t/** asdfg */", "", null, null);
        T("", "*/", "", null, null);
        T("", "\t/*", "", null, null);
        T("", "\t*", "", null, null);

        T("\t/**", "\t *", "", IndentAction.None, "* ");
        T("\t * something", "\t *", "", IndentAction.None, "* ");
        T("\t *", "\t *", "", IndentAction.None, "* ");

        T("", "\t */", "", IndentAction.None, null, 1);
        T("", "\t * */", "", IndentAction.None, null, 1);
        T("", "\t * * / * / * / */", "", null, null);

        T("\t/**", "\t * ", "", IndentAction.None, "* ");
        T("\t * something", "\t * ", "", IndentAction.None, "* ");
        T("\t *", "\t * ", "", IndentAction.None, "* ");

        T("/**", " * ", "", IndentAction.None, "* ");
        T(" * something", " * ", "", IndentAction.None, "* ");
        T(" *", " * asdfsfagadfg", "", IndentAction.None, "* ");

        T("/**", " * asdfsfagadfg * * * ", "", IndentAction.None, "* ");
        T(" * something", " * asdfsfagadfg * * * ", "", IndentAction.None, "* ");
        T(" *", " * asdfsfagadfg * * * ", "", IndentAction.None, "* ");

        T("/**", " * /*", "", IndentAction.None, "* ");
        T(" * something", " * /*", "", IndentAction.None, "* ");
        T(" *", " * /*", "", IndentAction.None, "* ");

        T("/**", " * asdfsfagadfg * / * / * /", "", IndentAction.None, "* ");
        T(" * something", " * asdfsfagadfg * / * / * /", "", IndentAction.None, "* ");
        T(" *", " * asdfsfagadfg * / * / * /", "", IndentAction.None, "* ");

        T("/**", " * asdfsfagadfg * / * / * /*", "", IndentAction.None, "* ");
        T(" * something", " * asdfsfagadfg * / * / * /*", "", IndentAction.None, "* ");
        T(" *", " * asdfsfagadfg * / * / * /*", "", IndentAction.None, "* ");

        T("", " */", "", IndentAction.None, null, 1);
        T(" */", " * test() {", "", IndentAction.Indent, null, 0);
        T("", "\t */", "", IndentAction.None, null, 1);
        T("", "\t\t */", "", IndentAction.None, null, 1);
        T("", "   */", "", IndentAction.None, null, 1);
        T("", "     */", "", IndentAction.None, null, 1);
        T("", "\t     */", "", IndentAction.None, null, 1);
        T("", " *--------------------------------------------------------------------------------------------*/", "", IndentAction.None, null, 1);

        // issue #43469
        T("class A {", "    * test() {", "", IndentAction.Indent, null, 0);
        T("", "    * test() {", "", IndentAction.Indent, null, 0);
        T("    ", "    * test() {", "", IndentAction.Indent, null, 0);
        T("class A {", "  * test() {", "", IndentAction.Indent, null, 0);
        T("", "  * test() {", "", IndentAction.Indent, null, 0);
        T("  ", "  * test() {", "", IndentAction.Indent, null, 0);
    }

    [Fact]
    public void Issue_141816()
    {
        var support = new OnEnterSupport(new OnEnterSupportOptions
        {
            OnEnterRules = OnEnterRules.JavascriptOnEnterRules,
        });
        void TestIndentAction(string beforeText, string afterText, IndentAction expected)
        {
            var actual = support.OnEnter(EditorAutoIndentStrategy.Advanced, "", beforeText, afterText);
            if (expected == IndentAction.None)
            {
                actual.ShouldBeNull();
            }
            else
            {
                actual!.IndentAction.ShouldBe(expected);
            }
        }

        TestIndentAction("const r = /{/;", "", IndentAction.None);
        TestIndentAction("const r = /{[0-9]/;", "", IndentAction.None);
        TestIndentAction("const r = /[a-zA-Z]{/;", "", IndentAction.None);
    }
}
