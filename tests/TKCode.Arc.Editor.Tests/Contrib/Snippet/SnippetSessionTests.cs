// Ported from vscode (src/vs/editor/contrib/snippet/test/browser/snippetSession.test.ts, commit b57d5c9e), MIT license.
// All 47 upstream tests (40 in 'SnippetSession', 7 in 'createEditsAndSnippetsFromEdits'). The editor is a
// SnippetTestEditor over TestCodeEditor; editor.trigger('test', 'type' / 'cut') is Type / Cut; the language
// configuration service is the editor's; OneSnippet._snippet is the internal OneSnippet.Snippet.

using Shouldly;
using TKCode.Arc.Editor.Contrib.Snippet;
using TKCode.Arc.Editor.Model;
using Xunit;

namespace TKCode.Arc.Editor.Tests;

public sealed class SnippetSessionTests : IDisposable
{
    private readonly SnippetTestEditor _editor;
    private readonly TextModel _model;

    public SnippetSessionTests()
    {
        _editor = SnippetTestEditor.Create("function foo() {\n    console.log(a);\n}");
        _model = _editor.Model;
        _editor.SetSelections([new TextSelection(1, 1, 1, 1), new TextSelection(2, 5, 2, 5)]);
        _model.GetEOL().ShouldBe("\n");
    }

    public void Dispose() => _editor.Dispose();

    internal static void AssertSelections(ISnippetEditor editor, params TextSelection[] s)
    {
        var actual = editor.GetSelections();
        actual.Count.ShouldBe(s.Length, $"actual=[{string.Join(", ", actual)}] <> expected=[{string.Join(", ", (IEnumerable<TextSelection>)s)}]");
        for (var i = 0; i < s.Length; i++)
        {
            (actual[i] == s[i]).ShouldBeTrue($"actual={actual[i]} <> expected={s[i]}");
        }
    }

    private void AssertSelections(params TextSelection[] s) => AssertSelections(_editor, s);

    private SnippetSession Session(string template, SnippetSessionInsertOptions? options = null) => new(_editor, template, options);

    [Fact]
    public void Normalize_whitespace()
    {
        void AssertNormalized(TextPosition position, string input, string expected)
        {
            var snippet = new SnippetParser().Parse(input);
            SnippetSession.AdjustWhitespace(_model, position, true, snippet);
            snippet.ToTextmateString().ShouldBe(expected);
        }

        AssertNormalized(new TextPosition(1, 1), "foo", "foo");
        AssertNormalized(new TextPosition(1, 1), "foo\rbar", "foo\nbar");
        AssertNormalized(new TextPosition(1, 1), "foo\rbar", "foo\nbar");
        AssertNormalized(new TextPosition(2, 5), "foo\r\tbar", "foo\n        bar");
        AssertNormalized(new TextPosition(2, 3), "foo\r\tbar", "foo\n    bar");
        AssertNormalized(new TextPosition(2, 5), "foo\r\tbar\nfoo", "foo\n        bar\n    foo");

        //Indentation issue with choice elements that span multiple lines #46266
        AssertNormalized(new TextPosition(2, 5), "a\nb${1|foo,\nbar|}", "a\n    b${1|foo,\nbar|}");
    }

    [Fact]
    public void Adjust_selection_overwrite_before_after()
    {
        TextRange range = SnippetSession.AdjustSelection(_model, new TextSelection(1, 2, 1, 2), 1, 0);
        range.ShouldBe(new TextRange(1, 1, 1, 2));
        range = SnippetSession.AdjustSelection(_model, new TextSelection(1, 2, 1, 2), 1111, 0);
        range.ShouldBe(new TextRange(1, 1, 1, 2));
        range = SnippetSession.AdjustSelection(_model, new TextSelection(1, 2, 1, 2), 0, 10);
        range.ShouldBe(new TextRange(1, 2, 1, 12));
        range = SnippetSession.AdjustSelection(_model, new TextSelection(1, 2, 1, 2), 0, 10111);
        range.ShouldBe(new TextRange(1, 2, 1, 17));
    }

    [Fact]
    public void Text_edits_and_selection()
    {
        var session = Session("foo${1:bar}foo$0");
        session.Insert();
        _model.GetValue().ShouldBe("foobarfoofunction foo() {\n    foobarfooconsole.log(a);\n}");

        AssertSelections(new TextSelection(1, 4, 1, 7), new TextSelection(2, 8, 2, 11));
        session.Next();
        AssertSelections(new TextSelection(1, 10, 1, 10), new TextSelection(2, 14, 2, 14));
    }

    [Fact]
    public void Text_edit_with_reversed_selection()
    {
        var session = Session("${1:bar}$0");
        _editor.SetSelections([new TextSelection(2, 5, 2, 5), new TextSelection(1, 1, 1, 1)]);

        session.Insert();
        _model.GetValue().ShouldBe("barfunction foo() {\n    barconsole.log(a);\n}");
        AssertSelections(new TextSelection(2, 5, 2, 8), new TextSelection(1, 1, 1, 4));
    }

    [Fact]
    public void Snippets_repeated_tabstops()
    {
        var session = Session("${1:abc}foo${1:abc}$0");
        session.Insert();
        AssertSelections(
            new TextSelection(1, 1, 1, 4), new TextSelection(1, 7, 1, 10),
            new TextSelection(2, 5, 2, 8), new TextSelection(2, 11, 2, 14));
        session.Next();
        AssertSelections(new TextSelection(1, 10, 1, 10), new TextSelection(2, 14, 2, 14));
    }

    [Fact]
    public void Snippets_just_text()
    {
        var session = Session("foobar");
        session.Insert();
        _model.GetValue().ShouldBe("foobarfunction foo() {\n    foobarconsole.log(a);\n}");
        AssertSelections(new TextSelection(1, 7, 1, 7), new TextSelection(2, 11, 2, 11));
    }

    [Fact]
    public void Snippets_selections_and_new_text_with_newlines()
    {
        var session = Session("foo\n\t${1:bar}\n$0");
        session.Insert();

        _model.GetValue().ShouldBe("foo\n    bar\nfunction foo() {\n    foo\n        bar\n    console.log(a);\n}");

        AssertSelections(new TextSelection(2, 5, 2, 8), new TextSelection(5, 9, 5, 12));

        session.Next();
        AssertSelections(new TextSelection(3, 1, 3, 1), new TextSelection(6, 5, 6, 5));
    }

    [Fact]
    public void Snippets_newline_NO_whitespace_adjust()
    {
        _editor.SetSelection(new TextSelection(2, 5, 2, 5));
        var session = Session("abc\n    foo\n        bar\n$0", new SnippetSessionInsertOptions { AdjustWhitespace = false });
        session.Insert();
        _model.GetValue().ShouldBe("function foo() {\n    abc\n    foo\n        bar\nconsole.log(a);\n}");
    }

    [Fact]
    public void Snippets_selections_next_prev()
    {
        var session = Session("f$1oo${2:bar}foo$0");
        session.Insert();

        // @ $2
        AssertSelections(new TextSelection(1, 2, 1, 2), new TextSelection(2, 6, 2, 6));
        // @ $1
        session.Next();
        AssertSelections(new TextSelection(1, 4, 1, 7), new TextSelection(2, 8, 2, 11));
        // @ $2
        session.Prev();
        AssertSelections(new TextSelection(1, 2, 1, 2), new TextSelection(2, 6, 2, 6));
        // @ $1
        session.Next();
        AssertSelections(new TextSelection(1, 4, 1, 7), new TextSelection(2, 8, 2, 11));
        // @ $0
        session.Next();
        AssertSelections(new TextSelection(1, 10, 1, 10), new TextSelection(2, 14, 2, 14));
    }

    [Fact]
    public void Snippets_selections_and_typing()
    {
        var session = Session("f${1:oo}_$2_$0");
        session.Insert();

        _editor.Type("X");
        session.Next();
        _editor.Type("bar");

        // go back to ${2:oo} which is now just 'X'
        session.Prev();
        AssertSelections(new TextSelection(1, 2, 1, 3), new TextSelection(2, 6, 2, 7));

        // go forward to $1 which is now 'bar'
        session.Next();
        AssertSelections(new TextSelection(1, 4, 1, 7), new TextSelection(2, 8, 2, 11));

        // go to final tabstop
        session.Next();
        _model.GetValue().ShouldBe("fX_bar_function foo() {\n    fX_bar_console.log(a);\n}");
        AssertSelections(new TextSelection(1, 8, 1, 8), new TextSelection(2, 12, 2, 12));
    }

    [Fact]
    public void Snippets_insert_shorter_snippet_into_non_empty_selection()
    {
        _model.SetValue("foo_bar_foo");
        _editor.SetSelections([new TextSelection(1, 1, 1, 4), new TextSelection(1, 9, 1, 12)]);

        Session("x$0").Insert();
        _model.GetValue().ShouldBe("x_bar_x");
        AssertSelections(new TextSelection(1, 2, 1, 2), new TextSelection(1, 8, 1, 8));
    }

    [Fact]
    public void Snippets_insert_longer_snippet_into_non_empty_selection()
    {
        _model.SetValue("foo_bar_foo");
        _editor.SetSelections([new TextSelection(1, 1, 1, 4), new TextSelection(1, 9, 1, 12)]);

        Session("LONGER$0").Insert();
        _model.GetValue().ShouldBe("LONGER_bar_LONGER");
        AssertSelections(new TextSelection(1, 7, 1, 7), new TextSelection(1, 18, 1, 18));
    }

    [Fact]
    public void Snippets_dont_grow_final_tabstop()
    {
        _model.SetValue("foo_zzz_foo");
        _editor.SetSelection(new TextSelection(1, 5, 1, 8));
        var session = Session("$1bar$0");
        session.Insert();

        AssertSelections(new TextSelection(1, 5, 1, 5));
        _editor.Type("foo-");

        session.Next();
        _model.GetValue().ShouldBe("foo_foo-bar_foo");
        AssertSelections(new TextSelection(1, 12, 1, 12));

        _editor.Type("XXX");
        _model.GetValue().ShouldBe("foo_foo-barXXX_foo");
        session.Prev();
        AssertSelections(new TextSelection(1, 5, 1, 9));
        session.Next();
        AssertSelections(new TextSelection(1, 15, 1, 15));
    }

    [Fact]
    public void Snippets_dont_merge_touching_tabstops_1_of_2()
    {
        var session = Session("$1$2$3$0");
        session.Insert();
        AssertSelections(new TextSelection(1, 1, 1, 1), new TextSelection(2, 5, 2, 5));

        session.Next();
        AssertSelections(new TextSelection(1, 1, 1, 1), new TextSelection(2, 5, 2, 5));

        session.Next();
        AssertSelections(new TextSelection(1, 1, 1, 1), new TextSelection(2, 5, 2, 5));

        session.Next();
        AssertSelections(new TextSelection(1, 1, 1, 1), new TextSelection(2, 5, 2, 5));

        session.Prev();
        session.Prev();
        session.Prev();
        AssertSelections(new TextSelection(1, 1, 1, 1), new TextSelection(2, 5, 2, 5));
        _editor.Type("111");

        session.Next();
        _editor.Type("222");

        session.Next();
        _editor.Type("333");

        session.Next();
        _model.GetValue().ShouldBe("111222333function foo() {\n    111222333console.log(a);\n}");
        AssertSelections(new TextSelection(1, 10, 1, 10), new TextSelection(2, 14, 2, 14));

        session.Prev();
        AssertSelections(new TextSelection(1, 7, 1, 10), new TextSelection(2, 11, 2, 14));
        session.Prev();
        AssertSelections(new TextSelection(1, 4, 1, 7), new TextSelection(2, 8, 2, 11));
        session.Prev();
        AssertSelections(new TextSelection(1, 1, 1, 4), new TextSelection(2, 5, 2, 8));
    }

    [Fact]
    public void Snippets_dont_merge_touching_tabstops_2_of_2()
    {
        var session = Session("$1$2$3$0");
        session.Insert();
        AssertSelections(new TextSelection(1, 1, 1, 1), new TextSelection(2, 5, 2, 5));

        _editor.Type("111");

        session.Next();
        AssertSelections(new TextSelection(1, 4, 1, 4), new TextSelection(2, 8, 2, 8));
        _editor.Type("222");

        session.Next();
        AssertSelections(new TextSelection(1, 7, 1, 7), new TextSelection(2, 11, 2, 11));
        _editor.Type("333");

        session.Next();
        session.IsAtLastPlaceholder.ShouldBeTrue();
    }

    [Fact]
    public void Snippets_gracefully_move_over_final_tabstop()
    {
        var session = Session("${1}bar$0");
        session.Insert();

        session.IsAtLastPlaceholder.ShouldBeFalse();
        AssertSelections(new TextSelection(1, 1, 1, 1), new TextSelection(2, 5, 2, 5));

        session.Next();
        session.IsAtLastPlaceholder.ShouldBeTrue();
        AssertSelections(new TextSelection(1, 4, 1, 4), new TextSelection(2, 8, 2, 8));

        session.Next();
        session.IsAtLastPlaceholder.ShouldBeTrue();
        AssertSelections(new TextSelection(1, 4, 1, 4), new TextSelection(2, 8, 2, 8));
    }

    [Fact]
    public void Snippets_overwriting_nested_placeholder()
    {
        var session = Session("log(${1:\"$2\"});$0");
        session.Insert();
        AssertSelections(new TextSelection(1, 5, 1, 7), new TextSelection(2, 9, 2, 11));

        _editor.Type("XXX");
        _model.GetValue().ShouldBe("log(XXX);function foo() {\n    log(XXX);console.log(a);\n}");

        session.Next();
        session.IsAtLastPlaceholder.ShouldBeFalse();

        session.Next();
        session.IsAtLastPlaceholder.ShouldBeTrue();
        AssertSelections(new TextSelection(1, 10, 1, 10), new TextSelection(2, 14, 2, 14));
    }

    [Fact]
    public void Snippets_selections_and_snippet_ranges()
    {
        var session = Session("${1:foo}farboo${2:bar}$0");
        session.Insert();
        _model.GetValue().ShouldBe("foofarboobarfunction foo() {\n    foofarboobarconsole.log(a);\n}");
        AssertSelections(new TextSelection(1, 1, 1, 4), new TextSelection(2, 5, 2, 8));

        session.IsSelectionWithinPlaceholders().ShouldBeTrue();

        _editor.SetSelections([new TextSelection(1, 1, 1, 1)]);
        session.IsSelectionWithinPlaceholders().ShouldBeFalse();

        _editor.SetSelections([new TextSelection(1, 6, 1, 6), new TextSelection(2, 10, 2, 10)]);
        session.IsSelectionWithinPlaceholders().ShouldBeFalse(); // in snippet, outside placeholder

        _editor.SetSelections([new TextSelection(1, 6, 1, 6), new TextSelection(2, 10, 2, 10), new TextSelection(1, 1, 1, 1)]);
        session.IsSelectionWithinPlaceholders().ShouldBeFalse(); // in snippet, outside placeholder

        _editor.SetSelections([new TextSelection(1, 6, 1, 6), new TextSelection(2, 10, 2, 10), new TextSelection(2, 20, 2, 21)]);
        session.IsSelectionWithinPlaceholders().ShouldBeFalse();

        // reset selection to placeholder
        session.Next();
        session.IsSelectionWithinPlaceholders().ShouldBeTrue();
        AssertSelections(new TextSelection(1, 10, 1, 13), new TextSelection(2, 14, 2, 17));

        // reset selection to placeholder
        session.Next();
        session.IsSelectionWithinPlaceholders().ShouldBeTrue();
        session.IsAtLastPlaceholder.ShouldBeTrue();
        AssertSelections(new TextSelection(1, 13, 1, 13), new TextSelection(2, 17, 2, 17));
    }

    [Fact]
    public void Snippets_nested_sessions()
    {
        _model.SetValue("");
        _editor.SetSelection(new TextSelection(1, 1, 1, 1));

        var first = Session("foo${2:bar}foo$0");
        first.Insert();
        _model.GetValue().ShouldBe("foobarfoo");
        AssertSelections(new TextSelection(1, 4, 1, 7));

        var second = Session("ba${1:zzzz}$0");
        second.Insert();
        _model.GetValue().ShouldBe("foobazzzzfoo");
        AssertSelections(new TextSelection(1, 6, 1, 10));

        second.Next();
        second.IsAtLastPlaceholder.ShouldBeTrue();
        AssertSelections(new TextSelection(1, 10, 1, 10));

        first.Next();
        first.IsAtLastPlaceholder.ShouldBeTrue();
        AssertSelections(new TextSelection(1, 13, 1, 13));
    }

    [Fact]
    public void Snippets_typing_at_final_tabstop()
    {
        var session = Session("farboo$0");
        session.Insert();
        session.IsAtLastPlaceholder.ShouldBeTrue();
        session.IsSelectionWithinPlaceholders().ShouldBeFalse();

        _editor.Type("XXX");
        session.IsSelectionWithinPlaceholders().ShouldBeFalse();
    }

    [Fact]
    public void Snippets_typing_at_beginning()
    {
        _editor.SetSelection(new TextSelection(1, 2, 1, 2));
        var session = Session("farboo$0");
        session.Insert();

        _editor.SetSelection(new TextSelection(1, 2, 1, 2));
        session.IsSelectionWithinPlaceholders().ShouldBeFalse();
        session.IsAtLastPlaceholder.ShouldBeTrue();

        _editor.Type("XXX");
        _model.GetLineContent(1).ShouldBe("fXXXfarboounction foo() {");
        session.IsSelectionWithinPlaceholders().ShouldBeFalse();

        session.Next();
        AssertSelections(new TextSelection(1, 11, 1, 11));
    }

    [Fact]
    public void Snippets_typing_with_nested_placeholder()
    {
        _editor.SetSelection(new TextSelection(1, 1, 1, 1));
        var session = Session("This ${1:is ${2:nested}}.$0");
        session.Insert();
        AssertSelections(new TextSelection(1, 6, 1, 15));

        session.Next();
        AssertSelections(new TextSelection(1, 9, 1, 15));

        _editor.Editor.Cut("test");
        AssertSelections(new TextSelection(1, 9, 1, 9));

        _editor.Type("XXX");
        session.Prev();
        AssertSelections(new TextSelection(1, 6, 1, 12));
    }

    [Fact]
    public void Snippets_snippet_with_variables()
    {
        var session = Session("@line=$TM_LINE_NUMBER$0");
        session.Insert();

        _model.GetValue().ShouldBe("@line=1function foo() {\n    @line=2console.log(a);\n}");
        AssertSelections(new TextSelection(1, 8, 1, 8), new TextSelection(2, 12, 2, 12));
    }

    [Fact]
    public void Snippets_merge()
    {
        _editor.SetSelection(new TextSelection(1, 1, 1, 1));
        var session = Session("This ${1:is ${2:nested}}.$0");
        session.Insert();
        session.Next();
        AssertSelections(new TextSelection(1, 9, 1, 15));

        session.Merge("really ${1:nested}$0");
        AssertSelections(new TextSelection(1, 16, 1, 22));

        session.Next();
        AssertSelections(new TextSelection(1, 22, 1, 22));
        session.IsAtLastPlaceholder.ShouldBeFalse();

        session.Next();
        session.IsAtLastPlaceholder.ShouldBeTrue();
        AssertSelections(new TextSelection(1, 23, 1, 23));

        session.Prev();
        _editor.Type("AAA");

        // back to `really ${1:nested}`
        session.Prev();
        AssertSelections(new TextSelection(1, 16, 1, 22));

        // back to `${1:is ...}` which now grew
        session.Prev();
        AssertSelections(new TextSelection(1, 6, 1, 25));
    }

    [Fact]
    public void Snippets_next_does_not_throw_when_placeholder_decorations_are_missing()
    {
        _model.SetValue("");
        _editor.SetSelection(new TextSelection(1, 1, 1, 1));

        var session = Session("${1:foo}$0");
        session.Insert();

        var decorationIds = _model.GetAllDecorations().Select(decoration => decoration.Id).ToArray();
        decorationIds.Length.ShouldBeGreaterThan(0);
        _model.DeltaDecorations(decorationIds, [], _editor.DecorationOwnerId);

        Should.NotThrow(() => session.Next());
    }

    [Fact]
    public void Snippets_deep_merge_does_not_produce_phantom_cursors_279349()
    {
        _model.SetValue("");
        _editor.SetSelection(new TextSelection(1, 1, 1, 1));
        var session = Session("\\sin\\left(${1:x}\\right) ");
        session.Insert();
        for (var i = 0; i < 25; i++)
        {
            session.Merge("\\sin\\left(${1:x}\\right) ");
            _editor.GetSelections().Count.ShouldBe(1, $"selection count after {i + 1} merges");
        }
    }

    [Fact]
    public void Snippets_merge_preserves_mirrors_in_nested_snippet()
    {
        _model.SetValue("");
        _editor.SetSelection(new TextSelection(1, 1, 1, 1));
        var session = Session("(${1:x})");
        session.Insert();
        session.Merge("${1:y}-${1}");
        _model.GetValue().ShouldBe("(y-y)");
        _editor.GetSelections().Count.ShouldBe(2);
    }

    [Fact]
    public void Snippets_merge_does_not_throw_when_placeholder_occurrences_collapse_to_same_position()
    {
        _model.SetValue("");
        _editor.SetSelection(new TextSelection(1, 1, 1, 1));
        var session = Session("$1$1$0");
        session.Insert();

        Should.NotThrow(() => session.Merge("${1:nested}$0"));
        _model.GetValue().ShouldBe("nested");
    }

    [Fact]
    public void Snippets_transform()
    {
        _model.SetValue("");
        _editor.SetSelection(new TextSelection(1, 1, 1, 1));
        var session = Session("${1/foo/bar/}$0");
        session.Insert();
        AssertSelections(new TextSelection(1, 1, 1, 1));

        _editor.Type("foo");
        session.Next();

        _model.GetValue().ShouldBe("bar");
        session.IsAtLastPlaceholder.ShouldBeTrue();
        AssertSelections(new TextSelection(1, 4, 1, 4));
    }

    [Fact]
    public void Snippets_multi_placeholder_same_index_one_transform()
    {
        _model.SetValue("");
        _editor.SetSelection(new TextSelection(1, 1, 1, 1));
        var session = Session("$1 baz ${1/foo/bar/}$0");
        session.Insert();
        AssertSelections(new TextSelection(1, 1, 1, 1), new TextSelection(1, 6, 1, 6));

        _editor.Type("foo");
        session.Next();

        _model.GetValue().ShouldBe("foo baz bar");
        session.IsAtLastPlaceholder.ShouldBeTrue();
        AssertSelections(new TextSelection(1, 12, 1, 12));
    }

    [Fact]
    public void Snippets_transform_example()
    {
        _model.SetValue("");
        _editor.SetSelection(new TextSelection(1, 1, 1, 1));
        var session = Session("${1:name} : ${2:type}${3/\\s:=(.*)/${1:+ :=}${1}/};\n$0");
        session.Insert();

        AssertSelections(new TextSelection(1, 1, 1, 5));
        _editor.Type("clk");
        session.Next();

        AssertSelections(new TextSelection(1, 7, 1, 11));
        _editor.Type("std_logic");
        session.Next();

        AssertSelections(new TextSelection(1, 16, 1, 16));
        session.Next();

        _model.GetValue().ShouldBe("clk : std_logic;\n");
        session.IsAtLastPlaceholder.ShouldBeTrue();
        AssertSelections(new TextSelection(2, 1, 2, 1));
    }

    [Fact]
    public void Snippets_transform_with_indent()
    {
        var snippet = string.Join('\n',
            "private readonly ${1} = new Emitter<$2>();",
            "readonly ${1/^_(.*)/$1/}: Event<$2> = this.$1.event;",
            "$0");
        var expected = string.Join('\n',
            "{",
            "\tprivate readonly _prop = new Emitter<string>();",
            "\treadonly prop: Event<string> = this._prop.event;",
            "\t",
            "}");
        var @base = string.Join('\n', "{", "\t", "}");

        _model.SetValue(@base);
        _model.UpdateOptions(new TextModelUpdateOptions { InsertSpaces = false });
        _editor.SetSelection(new TextSelection(2, 2, 2, 2));

        var session = Session(snippet);
        session.Insert();

        AssertSelections(new TextSelection(2, 19, 2, 19), new TextSelection(3, 11, 3, 11), new TextSelection(3, 28, 3, 28));
        _editor.Type("_prop");
        session.Next();

        AssertSelections(new TextSelection(2, 39, 2, 39), new TextSelection(3, 23, 3, 23));
        _editor.Type("string");
        session.Next();

        _model.GetValue().ShouldBe(expected);
        session.IsAtLastPlaceholder.ShouldBeTrue();
        AssertSelections(new TextSelection(4, 2, 4, 2));
    }

    [Fact]
    public void Snippets_transform_example_hit_if()
    {
        _model.SetValue("");
        _editor.SetSelection(new TextSelection(1, 1, 1, 1));
        var session = Session("${1:name} : ${2:type}${3/\\s:=(.*)/${1:+ :=}${1}/};\n$0");
        session.Insert();

        AssertSelections(new TextSelection(1, 1, 1, 5));
        _editor.Type("clk");
        session.Next();

        AssertSelections(new TextSelection(1, 7, 1, 11));
        _editor.Type("std_logic");
        session.Next();

        AssertSelections(new TextSelection(1, 16, 1, 16));
        _editor.Type(" := '1'");
        session.Next();

        _model.GetValue().ShouldBe("clk : std_logic := '1';\n");
        session.IsAtLastPlaceholder.ShouldBeTrue();
        AssertSelections(new TextSelection(2, 1, 2, 1));
    }

    [Fact]
    public void Snippet_tab_stop_selection_issue_96545_transform_adjacent_to_previous_placeholder()
    {
        _model.SetValue("");
        _editor.SetSelection(new TextSelection(1, 1, 1, 1));
        var session = Session("${1:{}${2:fff}${1/{/}/}");
        session.Insert();

        AssertSelections(new TextSelection(1, 1, 1, 2), new TextSelection(1, 5, 1, 6));
        session.Next();

        _model.GetValue().ShouldBe("{fff}");
        AssertSelections(new TextSelection(1, 2, 1, 5));
        _editor.Type("ggg");
        session.Next();

        _model.GetValue().ShouldBe("{ggg}");
        session.IsAtLastPlaceholder.ShouldBeTrue();
        AssertSelections(new TextSelection(1, 6, 1, 6));
    }

    [Fact]
    public void Snippet_tab_stop_selection_issue_96545()
    {
        _model.SetValue("");
        var session = Session("${1:{}${2:fff}${1/[\\{]/}/}$0");
        session.Insert();
        _model.GetValue().ShouldBe("{fff{");

        AssertSelections(new TextSelection(1, 1, 1, 2), new TextSelection(1, 5, 1, 6));
        session.Next();
        AssertSelections(new TextSelection(1, 2, 1, 5));
    }

    [Fact]
    public void Snippet_placeholder_index_after_2_snippets_in_a_row_ending_with_a_placeholder_30769()
    {
        _model.SetValue("");
        _editor.SetSelection(new TextSelection(1, 1, 1, 1));
        var session = Session("test ${1:replaceme}");
        session.Insert();

        _editor.Type("1");
        _editor.Type("\n");
        _model.GetValue().ShouldBe("test 1\n");

        session.Merge("test ${1:replaceme}");
        _editor.Type("2");
        _editor.Type("\n");

        _model.GetValue().ShouldBe("test 1\ntest 2\n");

        session.Merge("test ${1:replaceme}");
        _editor.Type("3");
        _editor.Type("\n");

        _model.GetValue().ShouldBe("test 1\ntest 2\ntest 3\n");

        session.Merge("test ${1:replaceme}");
        _editor.Type("4");
        _editor.Type("\n");

        _model.GetValue().ShouldBe("test 1\ntest 2\ntest 3\ntest 4\n");
    }

    [Fact]
    public void Snippet_variable_text_isnt_whitespace_normalised_31124()
    {
        _model.SetValue(string.Join('\n', "start", "\t\t-one", "\t\t-two", "end"));

        _model.UpdateOptions(new TextModelUpdateOptions { InsertSpaces = false });
        _editor.SetSelection(new TextSelection(2, 2, 3, 7));

        Session("<div>\n\t$TM_SELECTED_TEXT\n</div>$0").Insert();

        var expected = string.Join('\n', "start", "\t<div>", "\t\t\t-one", "\t\t\t-two", "\t</div>", "end");
        _model.GetValue().ShouldBe(expected);

        _model.SetValue(string.Join('\n', "start", "\t\t-one", "\t-two", "end"));

        _model.UpdateOptions(new TextModelUpdateOptions { InsertSpaces = false });
        _editor.SetSelection(new TextSelection(2, 2, 3, 7));

        Session("<div>\n\t$TM_SELECTED_TEXT\n</div>$0").Insert();

        expected = string.Join('\n', "start", "\t<div>", "\t\t\t-one", "\t\t-two", "\t</div>", "end");
        _model.GetValue().ShouldBe(expected);
    }

    [Fact]
    public void Selecting_text_from_left_to_right_and_choosing_item_messes_up_code_31199()
    {
        _model.SetValue("console.log");

        var actual = SnippetSession.AdjustSelection(_model, new TextSelection(1, 12, 1, 9), 3, 0);
        actual.ShouldBe(new TextSelection(1, 9, 1, 6));

        actual = SnippetSession.AdjustSelection(_model, new TextSelection(1, 9, 1, 12), 3, 0);
        actual.ShouldBe(new TextSelection(1, 9, 1, 12));

        _editor.SetSelections([new TextSelection(1, 9, 1, 12)]);
        Session("far", new SnippetSessionInsertOptions { OverwriteBefore = 3, OverwriteAfter = 0, AdjustWhitespace = true }).Insert();
        _model.GetValue().ShouldBe("console.far");
    }

    [Fact]
    public void Tabs_dont_get_replaced_with_spaces_in_snippet_transformations_103818()
    {
        _model.SetValue("\n{\n  \n}");
        _model.UpdateOptions(new TextModelUpdateOptions { InsertSpaces = true, IndentSize = 2 });
        _editor.SetSelections([new TextSelection(1, 1, 1, 1), new TextSelection(3, 6, 3, 6)]);
        var session = Session(string.Join('\n',
            "function animate () {",
            "\tvar ${1:a} = 12;",
            "\tconsole.log(${1/(.*)/\n\t\t$1\n\t/})",
            "}"));

        session.Insert();

        _model.GetValue().ShouldBe(string.Join('\n',
            "function animate () {",
            "  var a = 12;",
            "  console.log(a)",
            "}",
            "{",
            "  function animate () {",
            "    var a = 12;",
            "    console.log(a)",
            "  }",
            "}"));

        _editor.Type("bbb");
        session.Next();

        _model.GetValue().ShouldBe(string.Join('\n',
            "function animate () {",
            "  var bbb = 12;",
            "  console.log(",
            "    bbb",
            "  )",
            "}",
            "{",
            "  function animate () {",
            "    var bbb = 12;",
            "    console.log(",
            "      bbb",
            "    )",
            "  }",
            "}"));
    }

    // ---- suite('createEditsAndSnippetsFromEdits') ----

    private (List<SingleEditOperation> Edits, List<OneSnippet> Snippets) FromEdits(params SnippetEdit[] edits) =>
        SnippetSession.CreateEditsAndSnippetsFromEdits(_editor, edits, true, true, null, null);

    [Fact]
    public void FromEdits_empty()
    {
        var result = FromEdits();

        result.Edits.ShouldBeEmpty();
        result.Snippets.ShouldBeEmpty();
    }

    [Fact]
    public void FromEdits_basic()
    {
        _model.SetValue("foo(\"bar\")");

        var result = FromEdits(new SnippetEdit(new TextRange(1, 5, 1, 9), "$1"), new SnippetEdit(new TextRange(1, 1, 1, 1), "const ${1:new_const} = \"bar\""));

        result.Edits.Count.ShouldBe(2);
        result.Edits[0].Range.ShouldBe(new TextRange(1, 1, 1, 1));
        result.Edits[0].Text.ShouldBe("const new_const = \"bar\"");
        result.Edits[1].Range.ShouldBe(new TextRange(1, 5, 1, 9));
        result.Edits[1].Text.ShouldBe("new_const");

        result.Snippets.Count.ShouldBe(1);
        result.Snippets[0].IsTrivialSnippet.ShouldBeFalse();
    }

    [Fact]
    public void FromEdits_with_SELECTION_variable()
    {
        _model.SetValue("Some text and a selection");
        _editor.SetSelections([new TextSelection(1, 17, 1, 26)]);

        var result = FromEdits(new SnippetEdit(new TextRange(1, 17, 1, 26), "wrapped <$SELECTION>"));

        result.Edits.Count.ShouldBe(1);
        result.Edits[0].Range.ShouldBe(new TextRange(1, 17, 1, 26));
        result.Edits[0].Text.ShouldBe("wrapped <selection>");

        result.Snippets.Count.ShouldBe(1);
        result.Snippets[0].IsTrivialSnippet.ShouldBeTrue();
    }

    [Fact]
    public void FromEdits_TM_SELECTED_TEXT_resolves_per_edit_not_the_primary_selection_206121()
    {
        _model.SetValue("aaa\nbbb\nccc");
        // primary selection covers "aaa", but the edits target other lines
        _editor.SetSelections([new TextSelection(1, 1, 1, 4)]);

        var result = FromEdits(
            new SnippetEdit(new TextRange(2, 1, 2, 4), "[$TM_SELECTED_TEXT]"),
            new SnippetEdit(new TextRange(3, 1, 3, 4), "[$TM_SELECTED_TEXT]"));

        result.Edits.Count.ShouldBe(2);
        result.Edits[0].Range.ShouldBe(new TextRange(2, 1, 2, 4));
        result.Edits[0].Text.ShouldBe("[bbb]");
        result.Edits[1].Range.ShouldBe(new TextRange(3, 1, 3, 4));
        result.Edits[1].Text.ShouldBe("[ccc]");
    }

    [Fact]
    public void FromEdits_TM_LINE_NUMBER_resolves_per_edit()
    {
        _model.SetValue("a\nb\nc");
        _editor.SetSelections([new TextSelection(1, 1, 1, 1)]);

        var result = FromEdits(
            new SnippetEdit(new TextRange(1, 2, 1, 2), "$TM_LINE_NUMBER"),
            new SnippetEdit(new TextRange(3, 2, 3, 2), "$TM_LINE_NUMBER"));

        result.Edits.Count.ShouldBe(2);
        result.Edits[0].Text.ShouldBe("1");
        result.Edits[1].Text.ShouldBe("3");
    }

    [Fact]
    public void FromEdits_per_edit_resolution_does_not_corrupt_earlier_edits_when_value_lengths_differ()
    {
        // 100 lines so $TM_LINE_NUMBER produces values of differing widths (1 vs 100)
        _model.SetValue(string.Join('\n', Enumerable.Range(1, 100).Select(i => $"line{i}")));
        _editor.SetSelections([new TextSelection(1, 1, 1, 1)]);

        var result = FromEdits(
            new SnippetEdit(new TextRange(1, 2, 1, 2), "$TM_LINE_NUMBER"),
            new SnippetEdit(new TextRange(100, 2, 100, 2), "$TM_LINE_NUMBER"));

        result.Edits.Count.ShouldBe(2);
        result.Edits[0].Text.ShouldBe("1");
        result.Edits[1].Text.ShouldBe("100");
    }

    [Fact]
    public void FromEdits_CURSOR_NUMBER_uses_caller_supplied_edit_order_not_range_sorted_order()
    {
        _model.SetValue("xx\nyy");
        _editor.SetSelections([new TextSelection(1, 1, 1, 1)]);

        var result = FromEdits(
            new SnippetEdit(new TextRange(2, 3, 2, 3), "$CURSOR_NUMBER"),
            new SnippetEdit(new TextRange(1, 3, 1, 3), "$CURSOR_NUMBER"));

        result.Edits.Count.ShouldBe(2);
        result.Edits[0].Range.ShouldBe(new TextRange(1, 3, 1, 3));
        result.Edits[0].Text.ShouldBe("2");
        result.Edits[1].Range.ShouldBe(new TextRange(2, 3, 2, 3));
        result.Edits[1].Text.ShouldBe("1");
    }

    [Fact]
    public void FromEdits_cross_edit_placeholder_backfill_resolves_variables_in_earlier_edit()
    {
        // parseFragment backfills the earlier $1 with a clone of the later default;
        // the cloned $TM_LINE_NUMBER lives outside the second edit's newNodes
        _model.SetValue("aaa\nbbb");
        _editor.SetSelections([new TextSelection(1, 1, 1, 1)]);

        var result = FromEdits(
            new SnippetEdit(new TextRange(1, 2, 1, 2), "$1"),
            new SnippetEdit(new TextRange(2, 2, 2, 2), "${1:$TM_LINE_NUMBER}"));

        var hasUnresolvedVariable = false;
        result.Snippets[0].Snippet.Walk(marker =>
        {
            if (marker is Variable && marker.Children.Count == 0)
            {
                hasUnresolvedVariable = true;
            }

            return true;
        });
        hasUnresolvedVariable.ShouldBeFalse("backfilled $TM_LINE_NUMBER in earlier edit should be resolved");
    }
}
