// Ported from vscode (src/vs/editor/contrib/snippet/test/browser/snippetController2.old.test.ts, commit b57d5c9e), MIT
// license. 14 of the 15 upstream tests (the commented-out ones are left out, as upstream does). Not ported: 'Stops when
// disconnecting editor model' (the editor's model cannot be swapped here, so there is no setModel(null)). The
// TestSnippetController is a SnippetController2 over a SnippetTestEditor; isInSnippetMode reads the editor's
// inSnippetMode key.

using Shouldly;
using TKCode.Arc.Editor.Contrib.Snippet;
using TKCode.Arc.Editor.Model;
using Xunit;

namespace TKCode.Arc.Editor.Tests;

public sealed class SnippetController2OldTests
{
    private const string DefaultTemplate =
        "for (var ${1:index}; $1 < ${2:array}.length; $1++) {\n" +
        "\tvar element = $2[$1];\n" +
        "\t$0\n" +
        "}";

    private static readonly string[] s_defaultLines =
    [
        "function test() {",
        "\tvar x = 3;",
        "\tvar arr = [];",
        "\t",
        "}",
    ];

    private static void SnippetTest(Action<SnippetTestEditor, string, SnippetController2> cb, string[]? lines = null)
    {
        using var editor = SnippetTestEditor.Create(string.Join('\n', lines ?? s_defaultLines));
        editor.Model.UpdateOptions(new TextModelUpdateOptions { InsertSpaces = false });
        var snippetController = new SnippetController2(editor);
        cb(editor, DefaultTemplate, snippetController);
        snippetController.Dispose();
    }

    private static bool IsInSnippetMode(ISnippetEditor editor) => SnippetController2.InSnippetMode.GetValue(editor.ContextKeyService);

    private static void AssertEmptyAt(TextSelection selection, int lineNumber, int column) =>
        ((TextRange)selection).ShouldBe(new TextRange(lineNumber, column, lineNumber, column), selection.ToString());

    [Fact]
    public void Simple_accepted()
    {
        SnippetTest((editor, template, snippetController) =>
        {
            var model = editor.Model;
            editor.Editor.SetPosition(new TextPosition(4, 2));

            snippetController.Insert(template);
            model.GetLineContent(4).ShouldBe("\tfor (var index; index < array.length; index++) {");
            model.GetLineContent(5).ShouldBe("\t\tvar element = array[index];");
            model.GetLineContent(6).ShouldBe("\t\t");
            model.GetLineContent(7).ShouldBe("\t}");

            editor.Type("i");
            model.GetLineContent(4).ShouldBe("\tfor (var i; i < array.length; i++) {");
            model.GetLineContent(5).ShouldBe("\t\tvar element = array[i];");
            model.GetLineContent(6).ShouldBe("\t\t");
            model.GetLineContent(7).ShouldBe("\t}");

            snippetController.Next();
            editor.Type("arr");
            model.GetLineContent(4).ShouldBe("\tfor (var i; i < arr.length; i++) {");
            model.GetLineContent(5).ShouldBe("\t\tvar element = arr[i];");
            model.GetLineContent(6).ShouldBe("\t\t");
            model.GetLineContent(7).ShouldBe("\t}");

            snippetController.Prev();
            editor.Type("j");
            model.GetLineContent(4).ShouldBe("\tfor (var j; j < arr.length; j++) {");
            model.GetLineContent(5).ShouldBe("\t\tvar element = arr[j];");
            model.GetLineContent(6).ShouldBe("\t\t");
            model.GetLineContent(7).ShouldBe("\t}");

            snippetController.Next();
            snippetController.Next();
            editor.Editor.GetPosition().ShouldBe(new TextPosition(6, 3));
        });
    }

    [Fact]
    public void Simple_canceled()
    {
        SnippetTest((editor, template, snippetController) =>
        {
            var model = editor.Model;
            editor.Editor.SetPosition(new TextPosition(4, 2));

            snippetController.Insert(template);
            model.GetLineContent(4).ShouldBe("\tfor (var index; index < array.length; index++) {");
            model.GetLineContent(5).ShouldBe("\t\tvar element = array[index];");
            model.GetLineContent(6).ShouldBe("\t\t");
            model.GetLineContent(7).ShouldBe("\t}");

            snippetController.Cancel();
            editor.Editor.GetPosition().ShouldBe(new TextPosition(4, 16));
        });
    }

    [Fact]
    public void Stops_when_calling_model_setValue()
    {
        SnippetTest((editor, codeSnippet, snippetController) =>
        {
            editor.Editor.SetPosition(new TextPosition(4, 2));
            snippetController.Insert(codeSnippet);

            editor.Model.SetValue("goodbye");

            IsInSnippetMode(editor).ShouldBeFalse();
        });
    }

    [Fact]
    public void Stops_when_undoing()
    {
        SnippetTest((editor, codeSnippet, snippetController) =>
        {
            editor.Editor.SetPosition(new TextPosition(4, 2));
            snippetController.Insert(codeSnippet);

            _ = editor.Model.Undo();

            IsInSnippetMode(editor).ShouldBeFalse();
        });
    }

    [Fact]
    public void Stops_when_moving_cursor_outside()
    {
        SnippetTest((editor, codeSnippet, snippetController) =>
        {
            editor.Editor.SetPosition(new TextPosition(4, 2));
            snippetController.Insert(codeSnippet);

            editor.Editor.SetPosition(new TextPosition(1, 1));

            IsInSnippetMode(editor).ShouldBeFalse();
        });
    }

    [Fact]
    public void Stops_when_disposing_editor()
    {
        SnippetTest((editor, codeSnippet, snippetController) =>
        {
            editor.Editor.SetPosition(new TextPosition(4, 2));
            snippetController.Insert(codeSnippet);

            snippetController.Dispose();

            IsInSnippetMode(editor).ShouldBeFalse();
        });
    }

    [Fact]
    public void Final_tabstop_with_multiple_selections()
    {
        SnippetTest((editor, _, snippetController) =>
        {
            editor.SetSelections([new TextSelection(1, 1, 1, 1), new TextSelection(2, 1, 2, 1)]);

            snippetController.Insert("foo$0");

            editor.GetSelections().Count.ShouldBe(2);
            AssertEmptyAt(editor.GetSelections()[0], 1, 4);
            AssertEmptyAt(editor.GetSelections()[1], 2, 4);
        });

        SnippetTest((editor, _, snippetController) =>
        {
            editor.SetSelections([new TextSelection(1, 1, 1, 1), new TextSelection(2, 1, 2, 1)]);

            snippetController.Insert("foo$0bar");

            editor.GetSelections().Count.ShouldBe(2);
            AssertEmptyAt(editor.GetSelections()[0], 1, 4);
            AssertEmptyAt(editor.GetSelections()[1], 2, 4);
        });

        SnippetTest((editor, _, snippetController) =>
        {
            editor.SetSelections([new TextSelection(1, 1, 1, 1), new TextSelection(1, 5, 1, 5)]);

            snippetController.Insert("foo$0bar");

            editor.GetSelections().Count.ShouldBe(2);
            AssertEmptyAt(editor.GetSelections()[0], 1, 4);
            AssertEmptyAt(editor.GetSelections()[1], 1, 14);
        });

        SnippetTest((editor, _, snippetController) =>
        {
            editor.SetSelections([new TextSelection(1, 1, 1, 1), new TextSelection(1, 5, 1, 5)]);

            snippetController.Insert("foo\n$0\nbar");

            editor.GetSelections().Count.ShouldBe(2);
            AssertEmptyAt(editor.GetSelections()[0], 2, 1);
            AssertEmptyAt(editor.GetSelections()[1], 4, 1);
        });

        SnippetTest((editor, _, snippetController) =>
        {
            editor.SetSelections([new TextSelection(1, 1, 1, 1), new TextSelection(1, 5, 1, 5)]);

            snippetController.Insert("foo\n$0\nbar");

            editor.GetSelections().Count.ShouldBe(2);
            AssertEmptyAt(editor.GetSelections()[0], 2, 1);
            AssertEmptyAt(editor.GetSelections()[1], 4, 1);
        });

        SnippetTest((editor, _, snippetController) =>
        {
            editor.SetSelections([new TextSelection(2, 7, 2, 7)]);

            snippetController.Insert("xo$0r", new SnippetInsertOptions { OverwriteBefore = 1 });

            editor.GetSelections().Count.ShouldBe(1);
            AssertEmptyAt(editor.GetSelection(), 2, 8);
        });
    }

    [Fact]
    public void Final_tabstop_11742_simple()
    {
        SnippetTest((editor, _, controller) =>
        {
            editor.SetSelection(new TextSelection(1, 19, 1, 19));

            controller.Insert("{{% url_**$1** %}}", new SnippetInsertOptions { OverwriteBefore = 2 });

            editor.GetSelections().Count.ShouldBe(1);
            AssertEmptyAt(editor.GetSelection(), 1, 27);
            editor.Model.GetValue().ShouldBe("example example {{% url_**** %}}");
        }, ["example example sc"]);

        SnippetTest((editor, _, controller) =>
        {
            editor.SetSelection(new TextSelection(1, 3, 1, 3));

            controller.Insert("afterEach((done) => {\n\t${1}test\n});", new SnippetInsertOptions { OverwriteBefore = 2 });

            editor.GetSelections().Count.ShouldBe(1);
            AssertEmptyAt(editor.GetSelection(), 2, 2);
            editor.Model.GetValue().ShouldBe("afterEach((done) => {\n\ttest\n});");
        }, ["af"]);

        SnippetTest((editor, _, controller) =>
        {
            editor.SetSelection(new TextSelection(1, 3, 1, 3));

            controller.Insert("afterEach((done) => {\n${1}\ttest\n});", new SnippetInsertOptions { OverwriteBefore = 2 });

            editor.GetSelections().Count.ShouldBe(1);
            AssertEmptyAt(editor.GetSelection(), 2, 1);
            editor.Model.GetValue().ShouldBe("afterEach((done) => {\n\ttest\n});");
        }, ["af"]);

        SnippetTest((editor, _, controller) =>
        {
            editor.SetSelection(new TextSelection(1, 9, 1, 9));

            controller.Insert("aft${1}er", new SnippetInsertOptions { OverwriteBefore = 8 });

            editor.Model.GetValue().ShouldBe("after");
            editor.GetSelections().Count.ShouldBe(1);
            AssertEmptyAt(editor.GetSelection(), 1, 4);
        }, ["afterone"]);
    }

    [Fact]
    public void Final_tabstop_11742_different_indents()
    {
        SnippetTest((editor, _, controller) =>
        {
            editor.SetSelections([new TextSelection(2, 4, 2, 4), new TextSelection(1, 3, 1, 3)]);

            controller.Insert("afterEach((done) => {\n\t${0}test\n});", new SnippetInsertOptions { OverwriteBefore = 2 });

            editor.GetSelections().Count.ShouldBe(2);
            AssertEmptyAt(editor.GetSelections()[0], 5, 3);
            AssertEmptyAt(editor.GetSelections()[1], 2, 2);
        }, ["af", "\taf"]);
    }

    [Fact]
    public void Final_tabstop_11890_stay_at_the_beginning()
    {
        SnippetTest((editor, _, controller) =>
        {
            editor.SetSelections([new TextSelection(1, 5, 1, 5)]);

            controller.Insert("afterEach((done) => {\n${1}\ttest\n});", new SnippetInsertOptions { OverwriteBefore = 2 });

            editor.GetSelections().Count.ShouldBe(1);
            AssertEmptyAt(editor.GetSelections()[0], 2, 3);
        }, ["  af"]);
    }

    [Fact]
    public void Final_tabstop_no_tabstop()
    {
        SnippetTest((editor, _, controller) =>
        {
            editor.SetSelections([new TextSelection(1, 3, 1, 3)]);

            controller.Insert("afterEach", new SnippetInsertOptions { OverwriteBefore = 2 });

            AssertEmptyAt(editor.GetSelection(), 1, 10);
        }, ["af", "\taf"]);
    }

    [Fact]
    public void Multiple_cursor_and_overwriteBefore_After_11060()
    {
        SnippetTest((editor, _, controller) =>
        {
            editor.SetSelections([new TextSelection(1, 7, 1, 7), new TextSelection(2, 4, 2, 4)]);

            controller.Insert("_foo", new SnippetInsertOptions { OverwriteBefore = 1 });
            editor.Model.GetValue().ShouldBe("this._foo\nabc_foo");
        }, ["this._", "abc"]);

        SnippetTest((editor, _, controller) =>
        {
            editor.SetSelections([new TextSelection(1, 7, 1, 7), new TextSelection(2, 4, 2, 4)]);

            controller.Insert("XX", new SnippetInsertOptions { OverwriteBefore = 1 });
            editor.Model.GetValue().ShouldBe("this.XX\nabcXX");
        }, ["this._", "abc"]);

        SnippetTest((editor, _, controller) =>
        {
            editor.SetSelections([new TextSelection(1, 7, 1, 7), new TextSelection(2, 4, 2, 4), new TextSelection(3, 5, 3, 5)]);

            controller.Insert("_foo", new SnippetInsertOptions { OverwriteBefore = 1 });
            editor.Model.GetValue().ShouldBe("this._foo\nabc_foo\ndef_foo");
        }, ["this._", "abc", "def_"]);

        SnippetTest((editor, _, controller) =>
        {
            editor.SetSelections([new TextSelection(1, 7, 1, 7), new TextSelection(2, 4, 2, 4), new TextSelection(3, 6, 3, 6)]);

            controller.Insert("._foo", new SnippetInsertOptions { OverwriteBefore = 2 });
            editor.Model.GetValue().ShouldBe("this._foo\nabc._foo\ndef._foo");
        }, ["this._", "abc", "def._"]);

        SnippetTest((editor, _, controller) =>
        {
            editor.SetSelections([new TextSelection(3, 6, 3, 6), new TextSelection(1, 7, 1, 7), new TextSelection(2, 4, 2, 4)]);

            controller.Insert("._foo", new SnippetInsertOptions { OverwriteBefore = 2 });
            editor.Model.GetValue().ShouldBe("this._foo\nabc._foo\ndef._foo");
        }, ["this._", "abc", "def._"]);

        SnippetTest((editor, _, controller) =>
        {
            editor.SetSelections([new TextSelection(2, 4, 2, 4), new TextSelection(3, 6, 3, 6), new TextSelection(1, 7, 1, 7)]);

            controller.Insert("._foo", new SnippetInsertOptions { OverwriteBefore = 2 });
            editor.Model.GetValue().ShouldBe("this._._foo\na._foo\ndef._._foo");
        }, ["this._", "abc", "def._"]);
    }

    [Fact]
    public void Multiple_cursor_and_overwriteBefore_After_16277()
    {
        SnippetTest((editor, _, controller) =>
        {
            editor.SetSelections([new TextSelection(1, 5, 1, 5), new TextSelection(2, 5, 2, 5)]);

            controller.Insert("document", new SnippetInsertOptions { OverwriteBefore = 3 });
            editor.Model.GetValue().ShouldBe("{document}\n{document && true}");
        }, ["{foo}", "{foo && true}"]);
    }

    [Fact]
    public void Insert_snippet_twice_19449()
    {
        SnippetTest((editor, _, controller) =>
        {
            editor.SetSelections([new TextSelection(1, 1, 1, 1)]);

            controller.Insert("for (var ${1:i}=0; ${1:i}<len; ${1:i}++) { $0 }");
            editor.Model.GetValue().ShouldBe("for (var i=0; i<len; i++) {  }for (var i=0; i<len; i++) {  }");
        }, ["for (var i=0; i<len; i++) {  }"]);

        SnippetTest((editor, _, controller) =>
        {
            editor.SetSelections([new TextSelection(1, 1, 1, 1)]);

            controller.Insert("for (let ${1:i}=0; ${1:i}<len; ${1:i}++) { $0 }");
            editor.Model.GetValue().ShouldBe("for (let i=0; i<len; i++) {  }for (var i=0; i<len; i++) {  }");
        }, ["for (var i=0; i<len; i++) {  }"]);
    }
}
