// Ported from vscode (src/vs/editor/contrib/snippet/test/browser/snippetController2.test.ts, commit b57d5c9e), MIT
// license. All 42 upstream tests (the skipped #112362 is skipped here too), plus 1 own (a choice's completions). The context keys are the editor's own
// (SnippetTestEditor.ContextKeyService, upstream's MockContextKeyService); editor.trigger('test', 'type' / 'cut' /
// 'tab') and editor.runCommand(CoreEditingCommands.X) run on the TestCodeEditor; assert.deepStrictEqual on selections
// compares the lists.

using Shouldly;
using TKCode.Arc.Editor.Commands;
using TKCode.Arc.Editor.Contrib.Snippet;
using TKCode.Arc.Editor.Model;
using Xunit;

namespace TKCode.Arc.Editor.Tests;

public sealed class SnippetController2Tests : IDisposable
{
    private readonly SnippetTestEditor _editor;
    private readonly TextModel _model;
    private SnippetController2? _ctrl;

    public SnippetController2Tests()
    {
        _editor = SnippetTestEditor.Create("if\n    $state\nfi");
        _model = _editor.Model;
        _editor.SetSelections([new TextSelection(1, 1, 1, 1), new TextSelection(2, 5, 2, 5)]);
        _model.GetEOL().ShouldBe("\n");
    }

    public void Dispose()
    {
        _ctrl?.Dispose();
        _editor.Dispose();
    }

    private SnippetController2 CreateController() => _ctrl = new SnippetController2(_editor);

    private static void AssertSelections(ISnippetEditor editor, params TextSelection[] s) => SnippetSessionTests.AssertSelections(editor, s);

    private void AssertSelections(params TextSelection[] s) => AssertSelections(_editor, s);

    internal static (bool InSnippet, bool HasPrev, bool HasNext) GetContextState(ISnippetEditor editor) =>
    (
        SnippetController2.InSnippetMode.GetValue(editor.ContextKeyService),
        SnippetController2.HasPrevTabstop.GetValue(editor.ContextKeyService),
        SnippetController2.HasNextTabstop.GetValue(editor.ContextKeyService)
    );

    private (bool InSnippet, bool HasPrev, bool HasNext) GetContextState() => GetContextState(_editor);

    private void AssertContextKeys(bool inSnippet, bool hasPrev, bool hasNext)
    {
        var state = GetContextState();
        state.InSnippet.ShouldBe(inSnippet, "inSnippetMode");
        state.HasPrev.ShouldBe(hasPrev, "HasPrevTabstop");
        state.HasNext.ShouldBe(hasNext, "HasNextTabstop");
    }

    [Fact]
    public void Creation()
    {
        CreateController();
        AssertContextKeys(false, false, false);
    }

    [Fact]
    public void Insert_insert_abort()
    {
        var ctrl = CreateController();

        ctrl.Insert("foo${1:bar}foo$0");
        AssertContextKeys(true, false, true);
        AssertSelections(new TextSelection(1, 4, 1, 7), new TextSelection(2, 8, 2, 11));

        ctrl.Cancel();
        AssertContextKeys(false, false, false);
        AssertSelections(new TextSelection(1, 4, 1, 7), new TextSelection(2, 8, 2, 11));
    }

    [Fact]
    public void Insert_insert_tab_tab_done()
    {
        var ctrl = CreateController();

        ctrl.Insert("${1:one}${2:two}$0");
        AssertContextKeys(true, false, true);

        ctrl.Next();
        AssertContextKeys(true, true, true);

        ctrl.Next();
        AssertContextKeys(false, false, false);

        _editor.Type("\t");
        AssertContextKeys(false, false, false);
    }

    [Fact]
    public void Insert_insert_cursor_moves_out_left_right()
    {
        var ctrl = CreateController();

        ctrl.Insert("foo${1:bar}foo$0");
        AssertContextKeys(true, false, true);
        AssertSelections(new TextSelection(1, 4, 1, 7), new TextSelection(2, 8, 2, 11));

        // bad selection change
        _editor.SetSelections([new TextSelection(1, 12, 1, 12), new TextSelection(2, 16, 2, 16)]);
        AssertContextKeys(false, false, false);
    }

    [Fact]
    public void Insert_insert_cursor_moves_out_up_down()
    {
        var ctrl = CreateController();

        ctrl.Insert("foo${1:bar}foo$0");
        AssertContextKeys(true, false, true);
        AssertSelections(new TextSelection(1, 4, 1, 7), new TextSelection(2, 8, 2, 11));

        // bad selection change
        _editor.SetSelections([new TextSelection(2, 4, 2, 7), new TextSelection(3, 8, 3, 11)]);
        AssertContextKeys(false, false, false);
    }

    [Fact]
    public void Insert_insert_cursors_collapse()
    {
        var ctrl = CreateController();

        ctrl.Insert("foo${1:bar}foo$0");
        GetContextState().InSnippet.ShouldBeTrue();
        AssertSelections(new TextSelection(1, 4, 1, 7), new TextSelection(2, 8, 2, 11));

        // bad selection change
        _editor.SetSelections([new TextSelection(1, 4, 1, 7)]);
        AssertContextKeys(false, false, false);
    }

    [Fact]
    public void Insert_insert_plain_text_no_snippet_mode()
    {
        var ctrl = CreateController();

        ctrl.Insert("foobar");
        AssertContextKeys(false, false, false);
        AssertSelections(new TextSelection(1, 7, 1, 7), new TextSelection(2, 11, 2, 11));
    }

    [Fact]
    public void Insert_delete_snippet_text()
    {
        var ctrl = CreateController();

        ctrl.Insert("${1:foobar}$0");
        AssertContextKeys(true, false, true);
        AssertSelections(new TextSelection(1, 1, 1, 7), new TextSelection(2, 5, 2, 11));

        _editor.Editor.Cut("test");
        AssertContextKeys(true, false, true);
        AssertSelections(new TextSelection(1, 1, 1, 1), new TextSelection(2, 5, 2, 5));

        _editor.Type("abc");
        AssertContextKeys(true, false, true);

        ctrl.Next();
        AssertContextKeys(false, false, false);

        CoreEditingCommands.Tab.RunEditorCommand(_editor.Editor, null);
        AssertContextKeys(false, false, false);
    }

    [Fact]
    public void Insert_nested_trivial_snippet()
    {
        var ctrl = CreateController();
        ctrl.Insert("${1:foo}bar$0");
        AssertContextKeys(true, false, true);
        AssertSelections(new TextSelection(1, 1, 1, 4), new TextSelection(2, 5, 2, 8));

        ctrl.Insert("FOO$0");
        AssertSelections(new TextSelection(1, 4, 1, 4), new TextSelection(2, 8, 2, 8));
        AssertContextKeys(true, false, true);

        ctrl.Next();
        AssertSelections(new TextSelection(1, 7, 1, 7), new TextSelection(2, 11, 2, 11));
        AssertContextKeys(false, false, false);
    }

    [Fact]
    public void Insert_nested_snippet()
    {
        var ctrl = CreateController();
        ctrl.Insert("${1:foobar}$0");
        AssertContextKeys(true, false, true);
        AssertSelections(new TextSelection(1, 1, 1, 7), new TextSelection(2, 5, 2, 11));

        ctrl.Insert("far$1boo$0");
        AssertSelections(new TextSelection(1, 4, 1, 4), new TextSelection(2, 8, 2, 8));
        AssertContextKeys(true, false, true);

        ctrl.Next();
        AssertSelections(new TextSelection(1, 7, 1, 7), new TextSelection(2, 11, 2, 11));
        AssertContextKeys(true, true, true);

        ctrl.Next();
        AssertSelections(new TextSelection(1, 7, 1, 7), new TextSelection(2, 11, 2, 11));
        AssertContextKeys(false, false, false);
    }

    [Fact]
    public void Insert_nested_plain_text()
    {
        var ctrl = CreateController();
        ctrl.Insert("${1:foobar}$0");
        AssertContextKeys(true, false, true);
        AssertSelections(new TextSelection(1, 1, 1, 7), new TextSelection(2, 5, 2, 11));

        ctrl.Insert("farboo");
        AssertSelections(new TextSelection(1, 7, 1, 7), new TextSelection(2, 11, 2, 11));
        AssertContextKeys(true, false, true);

        ctrl.Next();
        AssertSelections(new TextSelection(1, 7, 1, 7), new TextSelection(2, 11, 2, 11));
        AssertContextKeys(false, false, false);
    }

    [Fact]
    public void Nested_snippets_without_final_placeholder_jumps_to_next_outer_placeholder_27898()
    {
        var ctrl = CreateController();

        ctrl.Insert("for(const ${1:element} of ${2:array}) {$0}");
        AssertContextKeys(true, false, true);
        AssertSelections(new TextSelection(1, 11, 1, 18), new TextSelection(2, 15, 2, 22));

        ctrl.Next();
        AssertContextKeys(true, true, true);
        AssertSelections(new TextSelection(1, 22, 1, 27), new TextSelection(2, 26, 2, 31));

        ctrl.Insert("document");
        AssertContextKeys(true, true, true);
        AssertSelections(new TextSelection(1, 30, 1, 30), new TextSelection(2, 34, 2, 34));

        ctrl.Next();
        AssertContextKeys(false, false, false);
    }

    [Fact]
    public void Inconsistent_tab_stop_behaviour_with_recursive_snippets_27543()
    {
        var ctrl = CreateController();
        ctrl.Insert("1_calize(${1:nl}, '${2:value}')$0");

        AssertContextKeys(true, false, true);
        AssertSelections(new TextSelection(1, 10, 1, 12), new TextSelection(2, 14, 2, 16));

        ctrl.Insert("2_calize(${1:nl}, '${2:value}')$0");

        AssertSelections(new TextSelection(1, 19, 1, 21), new TextSelection(2, 23, 2, 25));

        ctrl.Next(); // inner `value`
        AssertSelections(new TextSelection(1, 24, 1, 29), new TextSelection(2, 28, 2, 33));

        ctrl.Next(); // inner `$0`
        AssertSelections(new TextSelection(1, 31, 1, 31), new TextSelection(2, 35, 2, 35));

        ctrl.Next(); // outer `value`
        AssertSelections(new TextSelection(1, 34, 1, 39), new TextSelection(2, 38, 2, 43));

        ctrl.Prev(); // inner `$0`
        AssertSelections(new TextSelection(1, 31, 1, 31), new TextSelection(2, 35, 2, 35));
    }

    [Fact]
    public void Snippet_tabstop_selecting_content_of_previously_entered_variable_23728()
    {
        var ctrl = CreateController();

        _model.SetValue("");
        _editor.SetSelection(new TextSelection(1, 1, 1, 1));

        ctrl.Insert("import ${2:${1:module}} from '${1:module}'$0");

        AssertContextKeys(true, false, true);
        AssertSelections(new TextSelection(1, 8, 1, 14), new TextSelection(1, 21, 1, 27));

        ctrl.Insert("foo");
        AssertSelections(new TextSelection(1, 11, 1, 11), new TextSelection(1, 21, 1, 21));

        ctrl.Next(); // ${2:...}
        AssertSelections(new TextSelection(1, 8, 1, 11));
    }

    [Fact]
    public void HTML_snippets_combine_32211()
    {
        var ctrl = CreateController();

        _model.SetValue("");
        _model.UpdateOptions(new TextModelUpdateOptions { InsertSpaces = false, TabSize = 4, TrimAutoWhitespace = false });
        _editor.SetSelection(new TextSelection(1, 1, 1, 1));

        ctrl.Insert("""

            			<!DOCTYPE html>
            			<html lang="en">
            			<head>
            				<meta charset="UTF-8">
            				<meta name="viewport" content="width=${2:device-width}, initial-scale=${3:1.0}">
            				<meta http-equiv="X-UA-Compatible" content="${5:ie=edge}">
            				<title>${7:Document}</title>
            			</head>
            			<body>
            				${8}
            			</body>
            			</html>

            """);
        ctrl.Next();
        ctrl.Next();
        ctrl.Next();
        ctrl.Next();
        AssertSelections(new TextSelection(11, 5, 11, 5));

        ctrl.Insert("<input type=\"${2:text}\">");
        AssertSelections(new TextSelection(11, 18, 11, 22));
    }

    [Fact]
    public void Problems_with_nested_snippet_insertion_39594()
    {
        var ctrl = CreateController();

        _model.SetValue("");
        _editor.SetSelection(new TextSelection(1, 1, 1, 1));

        ctrl.Insert("$1 = ConvertTo-Json $1");
        AssertSelections(new TextSelection(1, 1, 1, 1), new TextSelection(1, 19, 1, 19));

        _editor.SetSelection(new TextSelection(1, 19, 1, 19));

        // snippet mode should stop because $1 has two occurrences
        // and we only have one selection left
        AssertContextKeys(false, false, false);
    }

    [Fact]
    public void Problems_with_nested_snippet_insertion_39594_part2()
    {
        // ensure selection-change-to-cancel logic isn't too aggressive
        var ctrl = CreateController();

        _model.SetValue("a-\naaa-");
        _editor.SetSelections([new TextSelection(2, 5, 2, 5), new TextSelection(1, 3, 1, 3)]);

        ctrl.Insert("log($1);$0");
        AssertSelections(new TextSelection(2, 9, 2, 9), new TextSelection(1, 7, 1, 7));
        AssertContextKeys(true, false, true);
    }

    [Fact]
    public void Nested_snippets_terminating_abruptly_42012()
    {
        var ctrl = CreateController();
        _model.SetValue("");
        _editor.SetSelection(new TextSelection(1, 1, 1, 1));
        ctrl.Insert("var ${2:${1:name}} = ${1:name} + 1;${0}");

        AssertSelections(new TextSelection(1, 5, 1, 9), new TextSelection(1, 12, 1, 16));
        AssertContextKeys(true, false, true);

        ctrl.Next();
        AssertContextKeys(true, true, true);
    }

    [Fact]
    public void Placeholders_order_58267()
    {
        var ctrl = CreateController();
        _model.SetValue("");
        _editor.SetSelection(new TextSelection(1, 1, 1, 1));
        ctrl.Insert("\\pth{$1}$0");

        AssertSelections(new TextSelection(1, 6, 1, 6));
        AssertContextKeys(true, false, true);

        ctrl.Insert("\\itv{${1:left}}{${2:right}}{${3:left_value}}{${4:right_value}}$0");
        AssertSelections(new TextSelection(1, 11, 1, 15));

        ctrl.Next();
        AssertSelections(new TextSelection(1, 17, 1, 22));

        ctrl.Next();
        AssertSelections(new TextSelection(1, 24, 1, 34));

        ctrl.Next();
        AssertSelections(new TextSelection(1, 36, 1, 47));

        ctrl.Next();
        AssertSelections(new TextSelection(1, 48, 1, 48));

        ctrl.Next();
        AssertSelections(new TextSelection(1, 49, 1, 49));
        AssertContextKeys(false, false, false);
    }

    [Fact]
    public void Must_tab_through_deleted_tab_stops_in_snippets_31619()
    {
        var ctrl = CreateController();
        _model.SetValue("");
        _editor.SetSelection(new TextSelection(1, 1, 1, 1));
        ctrl.Insert("foo${1:a${2:bar}baz}end$0");
        AssertSelections(new TextSelection(1, 4, 1, 11));

        _editor.Editor.Cut("test");
        AssertSelections(new TextSelection(1, 4, 1, 4));

        ctrl.Next();
        AssertSelections(new TextSelection(1, 7, 1, 7));
        AssertContextKeys(false, false, false);
    }

    [Fact]
    public void Cancelling_snippet_mode_should_discard_added_cursors_68512_soft_cancel()
    {
        var ctrl = CreateController();
        _model.SetValue("");
        _editor.SetSelection(new TextSelection(1, 1, 1, 1));

        ctrl.Insert(".REGION ${2:FUNCTION_NAME}\nCREATE.FUNCTION ${1:VOID} ${2:FUNCTION_NAME}(${3:})\n\t${4:}\nEND\n.ENDREGION$0");
        AssertSelections(new TextSelection(2, 17, 2, 21));

        ctrl.Next();
        AssertSelections(new TextSelection(1, 9, 1, 22), new TextSelection(2, 22, 2, 35));
        AssertContextKeys(true, true, true);

        _editor.SetSelections([new TextSelection(1, 22, 1, 22), new TextSelection(2, 35, 2, 35)]);
        AssertContextKeys(true, true, true);

        _editor.SetSelections([new TextSelection(2, 1, 2, 1), new TextSelection(2, 36, 2, 36)]);
        AssertContextKeys(false, false, false);
        AssertSelections(new TextSelection(2, 1, 2, 1), new TextSelection(2, 36, 2, 36));
    }

    [Fact]
    public void Cancelling_snippet_mode_should_discard_added_cursors_68512_hard_cancel()
    {
        var ctrl = CreateController();
        _model.SetValue("");
        _editor.SetSelection(new TextSelection(1, 1, 1, 1));

        ctrl.Insert(".REGION ${2:FUNCTION_NAME}\nCREATE.FUNCTION ${1:VOID} ${2:FUNCTION_NAME}(${3:})\n\t${4:}\nEND\n.ENDREGION$0");
        AssertSelections(new TextSelection(2, 17, 2, 21));

        ctrl.Next();
        AssertSelections(new TextSelection(1, 9, 1, 22), new TextSelection(2, 22, 2, 35));
        AssertContextKeys(true, true, true);

        _editor.SetSelections([new TextSelection(1, 22, 1, 22), new TextSelection(2, 35, 2, 35)]);
        AssertContextKeys(true, true, true);

        ctrl.Cancel(true);
        AssertContextKeys(false, false, false);
        AssertSelections(new TextSelection(1, 22, 1, 22));
    }

    [Fact]
    public void User_defined_snippet_tab_stops_ignored_72862()
    {
        var ctrl = CreateController();
        _model.SetValue("");
        _editor.SetSelection(new TextSelection(1, 1, 1, 1));

        ctrl.Insert("export default $1");
        AssertContextKeys(true, false, true);
    }

    [Fact]
    public void Optional_tabstop_in_snippets_72358()
    {
        var ctrl = CreateController();
        _model.SetValue("");
        _editor.SetSelection(new TextSelection(1, 1, 1, 1));

        ctrl.Insert("${1:prop: {$2\\},}\nmore$0");
        AssertContextKeys(true, false, true);

        AssertSelections(new TextSelection(1, 1, 1, 10));
        _editor.Editor.Cut("test");

        AssertSelections(new TextSelection(1, 1, 1, 1));

        ctrl.Next();
        AssertSelections(new TextSelection(2, 5, 2, 5));
        AssertContextKeys(false, false, false);
    }

    [Fact]
    public void Issue_90135_confusing_trim_whitespace_edits()
    {
        var ctrl = CreateController();
        _model.SetValue("");
        CoreEditingCommands.Tab.RunEditorCommand(_editor.Editor, null);

        ctrl.Insert("\nfoo");
        AssertSelections(new TextSelection(2, 8, 2, 8));
    }

    [Fact]
    public void Issue_145727_insertSnippet_can_put_snippet_selections_in_wrong_positions_1_of_2()
    {
        var ctrl = CreateController();
        _model.SetValue("");
        CoreEditingCommands.Tab.RunEditorCommand(_editor.Editor, null);

        ctrl.Insert("\naProperty: aClass<${2:boolean}> = new aClass<${2:boolean}>();\n", new SnippetInsertOptions { AdjustWhitespace = false });
        AssertSelections(new TextSelection(2, 19, 2, 26), new TextSelection(2, 41, 2, 48));
    }

    [Fact]
    public void Issue_145727_insertSnippet_can_put_snippet_selections_in_wrong_positions_2_of_2()
    {
        var ctrl = CreateController();
        _model.SetValue("");
        CoreEditingCommands.Tab.RunEditorCommand(_editor.Editor, null);

        ctrl.Insert("\naProperty: aClass<${2:boolean}> = new aClass<${2:boolean}>();\n");
        // This will insert \n    aProperty....
        AssertSelections(new TextSelection(2, 23, 2, 30), new TextSelection(2, 45, 2, 52));
    }

    [Fact]
    public void Leading_TAB_by_snippets_wont_replace_by_spaces_101870()
    {
        var ctrl = CreateController();
        _model.SetValue("");
        _model.UpdateOptions(new TextModelUpdateOptions { InsertSpaces = true, TabSize = 4 });
        ctrl.Insert("\tHello World\n\tNew Line");
        _model.GetValue().ShouldBe("    Hello World\n    New Line");
    }

    [Fact]
    public void Leading_TAB_by_snippets_wont_replace_by_spaces_101870_part_2()
    {
        var ctrl = CreateController();
        _model.SetValue("");
        _model.UpdateOptions(new TextModelUpdateOptions { InsertSpaces = true, TabSize = 4 });
        ctrl.Insert("\tHello World\n\tNew Line\n${1:\tmore}");
        _model.GetValue().ShouldBe("    Hello World\n    New Line\n    more");
    }

    [Fact(Skip = "Skipped upstream (test.skip): nested snippet into a transformed placeholder, #112362")]
    public void Snippet_transformation_does_not_work_after_inserting_variable_using_intellisense_112362()
    {
        var ctrl = CreateController();
        _model.SetValue("");
        _model.UpdateOptions(new TextModelUpdateOptions { InsertSpaces = true, TabSize = 4 });
        ctrl.Insert("$1\n\n${1/([A-Za-z0-9]+): ([A-Za-z]+).*/$1: '$2',/gm}");

        AssertSelections(new TextSelection(1, 1, 1, 1), new TextSelection(3, 1, 3, 1));
        _editor.Type("foo: ");
        ctrl.Insert("number;");
        ctrl.Next();
        _model.GetValue().ShouldBe("foo: number;\n\nfoo: 'number',");
    }

    // ---- suite('createEditsAndSnippetsFromEdits') ----

    [Fact]
    public void Apply_tab_done()
    {
        var ctrl = CreateController();

        _model.SetValue("foo(\"bar\")");

        ctrl.Apply(
        [
            new SnippetEdit(new TextRange(1, 5, 1, 10), "$1"),
            new SnippetEdit(new TextRange(1, 1, 1, 1), "const ${1:new_const} = \"bar\";\n"),
        ]);

        _model.GetValue().ShouldBe("const new_const = \"bar\";\nfoo(new_const)");
        AssertContextKeys(true, false, true);
        _editor.GetSelections().ShouldBe([new TextSelection(1, 7, 1, 16), new TextSelection(2, 5, 2, 14)]);

        ctrl.Next();
        AssertContextKeys(false, false, false);
        _editor.GetSelections().ShouldBe([new TextSelection(2, 14, 2, 14)]);
    }

    [Fact]
    public void Apply_tab_done_with_special_final_tabstop()
    {
        _model.SetValue("foo(\"bar\")");

        var ctrl = CreateController();
        ctrl.Apply(
        [
            new SnippetEdit(new TextRange(1, 5, 1, 10), "$1"),
            new SnippetEdit(new TextRange(1, 1, 1, 1), "const ${1:new_const}$0 = \"bar\";\n"),
        ]);

        _model.GetValue().ShouldBe("const new_const = \"bar\";\nfoo(new_const)");
        AssertContextKeys(true, false, true);
        _editor.GetSelections().ShouldBe([new TextSelection(1, 7, 1, 16), new TextSelection(2, 5, 2, 14)]);

        ctrl.Next();
        AssertContextKeys(false, false, false);
        _editor.GetSelections().ShouldBe([new TextSelection(1, 16, 1, 16)]);
    }

    [Fact]
    public void Apply_tab_tab_done()
    {
        _model.SetValue("foo\nbar");

        var ctrl = CreateController();
        ctrl.Apply(
        [
            new SnippetEdit(new TextRange(1, 4, 1, 4), "${3}"),
            new SnippetEdit(new TextRange(2, 4, 2, 4), "$3"),
            new SnippetEdit(new TextRange(1, 1, 1, 1), "### ${2:Header}\n"),
        ]);

        _model.GetValue().ShouldBe("### Header\nfoo\nbar");
        GetContextState().ShouldBe((true, false, true));
        _editor.GetSelections().ShouldBe([new TextSelection(1, 5, 1, 11)]);

        ctrl.Next();
        GetContextState().ShouldBe((true, true, true));
        _editor.GetSelections().ShouldBe([new TextSelection(2, 4, 2, 4), new TextSelection(3, 4, 3, 4)]);

        ctrl.Next();
        GetContextState().ShouldBe((false, false, false));
        _editor.GetSelections().ShouldBe([new TextSelection(3, 4, 3, 4)]);
    }

    [Fact]
    public void Nested_into_apply_works()
    {
        var ctrl = CreateController();
        _model.SetValue("onetwo");

        _editor.SetSelections([new TextSelection(1, 1, 1, 1), new TextSelection(2, 1, 2, 1)]);

        ctrl.Apply([new SnippetEdit(new TextRange(1, 7, 1, 7), "$0${1:three}")]);

        _model.GetValue().ShouldBe("onetwothree");
        GetContextState().ShouldBe((true, false, true));
        _editor.GetSelections().ShouldBe([new TextSelection(1, 7, 1, 12)]);

        ctrl.Insert("foo$1bar$1");
        _model.GetValue().ShouldBe("onetwofoobar");
        _editor.GetSelections().ShouldBe([new TextSelection(1, 10, 1, 10), new TextSelection(1, 13, 1, 13)]);
        GetContextState().ShouldBe((true, false, true));

        ctrl.Next();
        GetContextState().ShouldBe((true, true, true));
        _editor.GetSelections().ShouldBe([new TextSelection(1, 13, 1, 13)]);

        ctrl.Next();
        GetContextState().ShouldBe((false, false, false));
        _editor.GetSelections().ShouldBe([new TextSelection(1, 7, 1, 7)]);
    }

    [Fact]
    public void Nested_into_insert_abort_outer_snippet()
    {
        var ctrl = CreateController();
        _model.SetValue("one\ntwo");

        _editor.SetSelections([new TextSelection(1, 1, 1, 1), new TextSelection(2, 1, 2, 1)]);

        ctrl.Insert("foo${1:bar}bazz${1:bang}");
        _editor.GetSelections().ShouldBe([new TextSelection(1, 4, 1, 7), new TextSelection(1, 11, 1, 14), new TextSelection(2, 4, 2, 7), new TextSelection(2, 11, 2, 14)]);
        GetContextState().ShouldBe((true, false, true));

        ctrl.Apply([new SnippetEdit(new TextRange(1, 4, 1, 7), "$0A")]);

        _model.GetValue().ShouldBe("fooAbazzbarone\nfoobarbazzbartwo");
        GetContextState().ShouldBe((false, false, false));
        _editor.GetSelections().ShouldBe([new TextSelection(1, 4, 1, 4)]);
    }

    [Fact]
    public void Nested_into_insert_abort_outer_snippet_2()
    {
        var ctrl = CreateController();
        _model.SetValue("one\ntwo");

        _editor.SetSelections([new TextSelection(1, 1, 1, 1), new TextSelection(2, 1, 2, 1)]);

        ctrl.Insert("foo${1:bar}bazz${1:bang}");
        _editor.GetSelections().ShouldBe([new TextSelection(1, 4, 1, 7), new TextSelection(1, 11, 1, 14), new TextSelection(2, 4, 2, 7), new TextSelection(2, 11, 2, 14)]);
        GetContextState().ShouldBe((true, false, true));

        ctrl.Apply(
        [
            new SnippetEdit(new TextRange(1, 4, 1, 7), "A"),
            new SnippetEdit(new TextRange(1, 11, 1, 14), "B"),
            new SnippetEdit(new TextRange(2, 4, 2, 7), "C"),
            new SnippetEdit(new TextRange(2, 11, 2, 14), "D"),
        ]);

        _model.GetValue().ShouldBe("fooAbazzBone\nfooCbazzDtwo");
        GetContextState().ShouldBe((false, false, false));
        _editor.GetSelections().ShouldBe([new TextSelection(1, 5, 1, 5), new TextSelection(1, 10, 1, 10), new TextSelection(2, 5, 2, 5), new TextSelection(2, 10, 2, 10)]);
    }

    [Fact]
    public void Bug_cursor_position_0_with_user_snippets_163808()
    {
        var ctrl = CreateController();
        _model.SetValue("");

        ctrl.Insert("<Element1 Attr1=\"foo\" $1>\n  <Element2 Attr1=\"$2\"/>\n$0\"\n</Element1>");
        _editor.GetSelections().ShouldBe([new TextSelection(1, 23, 1, 23)]);

        ctrl.Insert("Qualifier=\"$0\"");
        _model.GetValue().ShouldBe("<Element1 Attr1=\"foo\" Qualifier=\"\">\n  <Element2 Attr1=\"\"/>\n\"\n</Element1>");
        _editor.GetSelections().ShouldBe([new TextSelection(1, 34, 1, 34)]);
    }

    [Fact]
    public void EOL_sequence_CRLF_shifts_tab_stop_in_isFileTemplate_snippets_167386()
    {
        var ctrl = CreateController();
        _model.SetValue("");
        _model.SetEOL(EndOfLineSequence.CRLF);

        ctrl.Apply([new SnippetEdit(_model.GetFullModelRange(), "line 54321${1:FOO}\nline 54321${1:FOO}\n(no tab stop)\nline 54321${1:FOO}\nline 54321")]);

        _editor.GetSelections().ShouldBe([new TextSelection(1, 11, 1, 14), new TextSelection(2, 11, 2, 14), new TextSelection(4, 11, 4, 14)]);
    }

    [Fact]
    public void Surround_with_code_action_snippets_use_incorrect_indentation_169319()
    {
        _model.SetValue("function foo(f, x, condition) {\n    f();\n    return x;\n}");
        var sel = new TextRange(2, 5, 3, 14);
        _editor.Editor.SetSelection(sel);
        var ctrl = CreateController();
        ctrl.Apply([new SnippetEdit(sel, "if (${1:condition}) {\n\t$TM_SELECTED_TEXT$0\n}")]);

        _model.GetValue().ShouldBe("function foo(f, x, condition) {\n    if (condition) {\n        f();\n        return x;\n    }\n}");
    }

    [Fact]
    public void TM_SELECTED_TEXT_resolves_per_edit_not_the_last_selection_206121()
    {
        _model.SetValue("aaa\nbbb\nccc");
        TextRange[] ranges = [new TextRange(1, 1, 1, 4), new TextRange(2, 1, 2, 4), new TextRange(3, 1, 3, 4)];
        _editor.SetSelections(ranges.Select(r => new TextSelection(r.StartLineNumber, r.StartColumn, r.EndLineNumber, r.EndColumn)).ToArray());

        var ctrl = CreateController();
        ctrl.Apply(ranges.Select(range => new SnippetEdit(range, "if(${1:cond}) {$TM_SELECTED_TEXT}$0")).ToArray());

        _model.GetValue().ShouldBe("if(cond) {aaa}\nif(cond) {bbb}\nif(cond) {ccc}");
    }

    [Fact]
    public void Apply_with_multiple_cursors_threads_CURSOR_NUMBER_per_edit()
    {
        _model.SetValue("aa\nbb\ncc");

        var ctrl = CreateController();
        ctrl.Apply(
        [
            new SnippetEdit(new TextRange(1, 3, 1, 3), ":$CURSOR_NUMBER"),
            new SnippetEdit(new TextRange(2, 3, 2, 3), ":$CURSOR_NUMBER"),
            new SnippetEdit(new TextRange(3, 3, 3, 3), ":$CURSOR_NUMBER"),
        ]);

        _model.GetValue().ShouldBe("aa:1\nbb:2\ncc:3");
    }

    [Fact]
    public async Task Undo_restores_original_selection_after_apply_170041()
    {
        _model.SetValue("Some text and more text");
        _editor.SetSelection(new TextSelection(1, 1, 1, 1));

        var ctrl = CreateController();
        ctrl.Apply([new SnippetEdit(new TextRange(1, 6, 1, 10), "${0:hi}")]);

        _model.GetValue().ShouldBe("Some hi and more text");

        await _editor.Editor.Undo();

        _model.GetValue().ShouldBe("Some text and more text");
        _editor.GetSelections().ShouldBe([new TextSelection(1, 1, 1, 1)]);
    }

    // ---- not in upstream: choices ----

    [Fact]
    public async Task Choice_placeholder_shows_its_options()
    {
        var ctrl = CreateController();
        _model.SetValue("");
        _editor.SetSelection(new TextSelection(1, 1, 1, 1));

        ctrl.Insert("let x = ${1|one,two,three|};$0");
        _model.GetValue().ShouldBe("let x = one;");
        SnippetController2.InSnippetChoice.GetValue(_editor.ContextKeyService).ShouldBeTrue();
        _editor.ShownSuggestions.Count.ShouldBe(1);

        var list = await _editor.ShownSuggestions[0].ProvideCompletionItems(_model, new TextPosition(1, 12), new Languages.CompletionContext(Languages.CompletionTriggerKind.Invoke), CancellationToken.None);
        list.ShouldNotBeNull();
        list.Suggestions.Select(s => s.InsertText).ShouldBe(["one", "two", "three"]);
        list.Suggestions[1].Range.ShouldBe((Languages.CompletionItemRanges)new TextRange(1, 9, 1, 12));

        ctrl.Next();
        SnippetController2.InSnippetChoice.GetValue(_editor.ContextKeyService).ShouldBeFalse();
    }
}
