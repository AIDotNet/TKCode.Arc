// Ported from vscode (src/vs/editor/contrib/indentation/test/browser/indentation.test.ts, commit b57d5c9e), MIT
// license: the 'Auto Indent On Type' suites of the other languages.
// Differences from upstream: see IndentationTestBase.

using TKCode.Arc.Editor.Commands;
using TKCode.Arc.Editor.Config;
using TKCode.Arc.Editor.Contrib.Indentation;
using TKCode.Arc.Editor.Cursor;
using TKCode.Arc.Editor.Tokens;
using Xunit;
using static TKCode.Arc.Editor.Tests.Contrib.EditingAssert;

namespace TKCode.Arc.Editor.Tests.Contrib;

/// <summary>Upstream's suite 'Auto Indent On Type - Ruby'.</summary>
public sealed class AutoIndentOnTypeRubyTests() : IndentationTestBase(Language.Ruby)
{
    [Fact]
    public void Issue198350InOrWhenIncorrectlyMatchNonKeywordsForRuby()
    {

        // https://github.com/microsoft/vscode/issues/198350

        using var model = CreateTextModel("");

        WithIndentEditor(model, EditorAutoIndentStrategy.Full, (editor, viewModel) =>
        {
            viewModel.Type("def foo\n        i");
            viewModel.Type("n", "keyboard");
            AssertDeepStrictEqual(model.GetValue(), "def foo\n        in");
            viewModel.Type(" ", "keyboard");
            AssertDeepStrictEqual(model.GetValue(), "def foo\nin ");

            viewModel.Model.SetValue("");
            viewModel.Type("  # in");
            AssertDeepStrictEqual(model.GetValue(), "  # in");
            viewModel.Type(" ", "keyboard");
            AssertDeepStrictEqual(model.GetValue(), "  # in ");
        });
    }


    // Failing tests...

    [Fact(Skip = "test.skip upstream")]
    public void Issue199846InOrWhenIncorrectlyMatchNonKeywordsForRuby()
    {

        // https://github.com/microsoft/vscode/issues/199846
        // explanation: happening because the # is detected probably as a comment

        using var model = CreateTextModel("");

        WithIndentEditor(model, EditorAutoIndentStrategy.Full, (editor, viewModel) =>
        {
            viewModel.Type("method(\"#foo\") do");
            viewModel.Type("\n", "keyboard");
            AssertDeepStrictEqual(model.GetValue(), JoinLines([
                "method(\"#foo\") do",
                "    "
            ]));
        });
    }
}

/// <summary>Upstream's suite 'Auto Indent On Type - PHP'.</summary>
public sealed class AutoIndentOnTypePhpTests() : IndentationTestBase(Language.PHP)
{
    [Fact]
    public void Issue199050ShouldNotIndentAfterDetectedInAString()
    {

        // https://github.com/microsoft/vscode/issues/199050

        using var model = CreateTextModel("preg_replace(\"{\");");

        WithIndentEditor(model, EditorAutoIndentStrategy.Full, (editor, viewModel) =>
        {
            StandardTokenTypeData[][] tokens = [
                [
                    new(0, StandardTokenType.Other),
                    new(13, StandardTokenType.String),
                    new(16, StandardTokenType.Other),
                ]
            ];
            IndentationTestUtils.RegisterTokenizationSupport(model, tokens, languageId);
            editor.SetSelection(new TextSelection(1, 54, 1, 54));
            viewModel.Type("\n", "keyboard");
            AssertDeepStrictEqual(model.GetValue(), JoinLines([
                "preg_replace(\"{\");",
                ""
            ]));
        });
    }
}

/// <summary>Upstream's suite 'Auto Indent On Type - CPP'.</summary>
public sealed class AutoIndentOnTypeCppTests() : IndentationTestBase(Language.CPP)
{
    [Fact(Skip = "test.skip upstream")]
    public void Issue178334IncorrectOutdentOfWhenSignatureSpansMultipleLines()
    {

        // https://github.com/microsoft/vscode/issues/178334

        using var model = CreateTextModel(JoinLines([
            "int WINAPI WinMain(bool instance,",
            "    int nshowcmd) {}",
        ]));

        WithIndentEditor(model, EditorAutoIndentStrategy.Full, (editor, viewModel) =>
        {
            editor.SetSelection(new TextSelection(2, 20, 2, 20));
            viewModel.Type("\n", "keyboard");
            AssertDeepStrictEqual(model.GetValue(), JoinLines([
                "int WINAPI WinMain(bool instance,",
                "    int nshowcmd) {",
                "    ",
                "}"
            ]));
        });
    }


    [Fact(Skip = "test.skip upstream")]
    public void Issue118929IncorrectIndentWhenFollowsCurlyBrace()
    {

        // https://github.com/microsoft/vscode/issues/118929

        using var model = CreateTextModel(JoinLines([
            "if (true) { // jaja",
            "}",
        ]));

        WithIndentEditor(model, EditorAutoIndentStrategy.Full, (editor, viewModel) =>
        {
            editor.SetSelection(new TextSelection(1, 20, 1, 20));
            viewModel.Type("\n", "keyboard");
            AssertDeepStrictEqual(model.GetValue(), JoinLines([
                "if (true) { // jaja",
                "    ",
                "}",
            ]));
        });
    }


    [Fact(Skip = "test.skip upstream")]
    public void Issue111265AutoIndentationSetToNoneStillChangesTheIndentation()
    {

        // https://github.com/microsoft/vscode/issues/111265

        using var model = CreateTextModel(JoinLines([
            "int func() {",
            "\t\t",
        ]));

        WithIndentEditor(model, EditorAutoIndentStrategy.None, (editor, viewModel) =>
        {
            editor.SetSelection(new TextSelection(2, 3, 2, 3));
            viewModel.Type("}", "keyboard");
            AssertDeepStrictEqual(model.GetValue(), JoinLines([
                "int func() {",
                "\t\t}",
            ]));
        });
    }
}

/// <summary>Upstream's suite 'Auto Indent On Type - HTML'.</summary>
public sealed class AutoIndentOnTypeHtmlTests() : IndentationTestBase(Language.HTML)
{
    [Fact(Skip = "test.skip upstream")]
    public void Issue61510IncorrectIndentationAfterInHtmlFile()
    {

        // https://github.com/microsoft/vscode/issues/178334

        using var model = CreateTextModel(JoinLines([
            "<pre>",
            "  foo //I press <Enter> at the end of this line",
            "</pre>",
        ]));

        WithIndentEditor(model, EditorAutoIndentStrategy.Full, (editor, viewModel) =>
        {
            editor.SetSelection(new TextSelection(2, 48, 2, 48));
            viewModel.Type("\n", "keyboard");
            AssertDeepStrictEqual(model.GetValue(), JoinLines([
                "<pre>",
                "  foo //I press <Enter> at the end of this line",
                "  ",
                "</pre>",
            ]));
        });
    }
}

/// <summary>Upstream's suite 'Auto Indent On Type - Visual Basic'.</summary>
public sealed class AutoIndentOnTypeVisualBasicTests() : IndentationTestBase(Language.VB)
{
    [Fact]
    public void Issue118932NoIndentationInVisualBasicFiles()
    {

        // https://github.com/microsoft/vscode/issues/118932

        using var model = CreateTextModel(JoinLines([
            "If True Then",
            "    Some code",
            "    End I",
        ]));

        WithIndentEditor(model, EditorAutoIndentStrategy.Full, (editor, viewModel) =>
        {
            editor.SetSelection(new TextSelection(3, 10, 3, 10));
            viewModel.Type("f", "keyboard");
            AssertDeepStrictEqual(model.GetValue(), JoinLines([
                "If True Then",
                "    Some code",
                "End If",
            ]));
        });
    }


    [Fact]
    public void Issue118932IndentAfterModuleDeclaration()
    {

        // https://github.com/microsoft/vscode/issues/118932

        using var model = CreateTextModel("");

        WithIndentEditor(model, EditorAutoIndentStrategy.Full, (editor, viewModel) =>
        {
            viewModel.Type("Module Test");
            viewModel.Type("\n", "keyboard");
            AssertDeepStrictEqual(model.GetValue(), JoinLines([
                "Module Test",
                "    ",
            ]));
        });
    }


    [Fact]
    public void Issue118932IndentAfterSubDeclaration()
    {

        // https://github.com/microsoft/vscode/issues/118932

        using var model = CreateTextModel(JoinLines([
            "Module Test",
            "    ",
        ]));

        WithIndentEditor(model, EditorAutoIndentStrategy.Full, (editor, viewModel) =>
        {
            editor.SetSelection(new TextSelection(2, 5, 2, 5));
            viewModel.Type("Sub Main()");
            viewModel.Type("\n", "keyboard");
            AssertDeepStrictEqual(model.GetValue(), JoinLines([
                "Module Test",
                "    Sub Main()",
                "        ",
            ]));
        });
    }


    [Fact]
    public void Issue118932DedentOnEndSub()
    {

        // https://github.com/microsoft/vscode/issues/118932

        using var model = CreateTextModel(JoinLines([
            "Module Test",
            "    Sub Main()",
            "        Console.WriteLine(\"Hello\")",
            "        End Su",
        ]));

        WithIndentEditor(model, EditorAutoIndentStrategy.Full, (editor, viewModel) =>
        {
            editor.SetSelection(new TextSelection(4, 15, 4, 15));
            viewModel.Type("b", "keyboard");
            AssertDeepStrictEqual(model.GetValue(), JoinLines([
                "Module Test",
                "    Sub Main()",
                "        Console.WriteLine(\"Hello\")",
                "    End Sub",
            ]));
        });
    }


    [Fact]
    public void Issue118932DedentOnEndModule()
    {

        // https://github.com/microsoft/vscode/issues/118932
        // When End Module is typed right after Module (no nested blocks), it dedents correctly

        using var model = CreateTextModel(JoinLines([
            "Module Test",
            "    Private x As Integer",
            "    End Modul",
        ]));

        WithIndentEditor(model, EditorAutoIndentStrategy.Full, (editor, viewModel) =>
        {
            editor.SetSelection(new TextSelection(3, 14, 3, 14));
            viewModel.Type("e", "keyboard");
            AssertDeepStrictEqual(model.GetValue(), JoinLines([
                "Module Test",
                "    Private x As Integer",
                "End Module",
            ]));
        });
    }


    [Fact]
    public void Issue118932IndentAfterFunctionDeclaration()
    {

        // https://github.com/microsoft/vscode/issues/118932

        using var model = CreateTextModel(JoinLines([
            "Module Test",
            "    ",
        ]));

        WithIndentEditor(model, EditorAutoIndentStrategy.Full, (editor, viewModel) =>
        {
            editor.SetSelection(new TextSelection(2, 5, 2, 5));
            viewModel.Type("Function Add(a As Integer, b As Integer) As Integer");
            viewModel.Type("\n", "keyboard");
            AssertDeepStrictEqual(model.GetValue(), JoinLines([
                "Module Test",
                "    Function Add(a As Integer, b As Integer) As Integer",
                "        ",
            ]));
        });
    }


    [Fact]
    public void Issue118932DedentOnEndFunction()
    {

        // https://github.com/microsoft/vscode/issues/118932

        using var model = CreateTextModel(JoinLines([
            "Module Test",
            "    Function Add(a, b)",
            "        Return a + b",
            "        End Functio",
        ]));

        WithIndentEditor(model, EditorAutoIndentStrategy.Full, (editor, viewModel) =>
        {
            editor.SetSelection(new TextSelection(4, 20, 4, 20));
            viewModel.Type("n", "keyboard");
            AssertDeepStrictEqual(model.GetValue(), JoinLines([
                "Module Test",
                "    Function Add(a, b)",
                "        Return a + b",
                "    End Function",
            ]));
        });
    }


    [Fact]
    public void Issue118932IndentAfterIfThen()
    {

        // https://github.com/microsoft/vscode/issues/118932

        using var model = CreateTextModel(JoinLines([
            "Sub Test()",
            "    ",
        ]));

        WithIndentEditor(model, EditorAutoIndentStrategy.Full, (editor, viewModel) =>
        {
            editor.SetSelection(new TextSelection(2, 5, 2, 5));
            viewModel.Type("If x > 0 Then");
            viewModel.Type("\n", "keyboard");
            AssertDeepStrictEqual(model.GetValue(), JoinLines([
                "Sub Test()",
                "    If x > 0 Then",
                "        ",
            ]));
        });
    }


    [Fact]
    public void Issue118932IndentAfterElseIfThen()
    {

        // https://github.com/microsoft/vscode/issues/118932

        using var model = CreateTextModel(JoinLines([
            "Sub Test()",
            "    If x > 0 Then",
            "        DoSomething()",
            "    ElseIf x < 0 Then",
        ]));

        WithIndentEditor(model, EditorAutoIndentStrategy.Full, (editor, viewModel) =>
        {
            editor.SetSelection(new TextSelection(4, 22, 4, 22));
            viewModel.Type("\n", "keyboard");
            AssertDeepStrictEqual(model.GetValue(), JoinLines([
                "Sub Test()",
                "    If x > 0 Then",
                "        DoSomething()",
                "    ElseIf x < 0 Then",
                "        ",
            ]));
        });
    }


    [Fact]
    public void Issue118932DedentAndIndentOnElse()
    {

        // https://github.com/microsoft/vscode/issues/118932

        using var model = CreateTextModel(JoinLines([
            "Sub Test()",
            "    If x > 0 Then",
            "        DoSomething()",
            "        Els",
        ]));

        WithIndentEditor(model, EditorAutoIndentStrategy.Full, (editor, viewModel) =>
        {
            editor.SetSelection(new TextSelection(4, 12, 4, 12));
            viewModel.Type("e", "keyboard");
            AssertDeepStrictEqual(model.GetValue(), JoinLines([
                "Sub Test()",
                "    If x > 0 Then",
                "        DoSomething()",
                "    Else",
            ]));
        });
    }


    [Fact]
    public void Issue118932IndentAfterWhile()
    {

        // https://github.com/microsoft/vscode/issues/118932

        using var model = CreateTextModel(JoinLines([
            "Sub Test()",
            "    ",
        ]));

        WithIndentEditor(model, EditorAutoIndentStrategy.Full, (editor, viewModel) =>
        {
            editor.SetSelection(new TextSelection(2, 5, 2, 5));
            viewModel.Type("While x > 0");
            viewModel.Type("\n", "keyboard");
            AssertDeepStrictEqual(model.GetValue(), JoinLines([
                "Sub Test()",
                "    While x > 0",
                "        ",
            ]));
        });
    }


    [Fact]
    public void Issue118932DedentOnEndWhile()
    {

        // https://github.com/microsoft/vscode/issues/118932

        using var model = CreateTextModel(JoinLines([
            "Sub Test()",
            "    While x > 0",
            "        x = x - 1",
            "        End Whil",
        ]));

        WithIndentEditor(model, EditorAutoIndentStrategy.Full, (editor, viewModel) =>
        {
            editor.SetSelection(new TextSelection(4, 17, 4, 17));
            viewModel.Type("e", "keyboard");
            AssertDeepStrictEqual(model.GetValue(), JoinLines([
                "Sub Test()",
                "    While x > 0",
                "        x = x - 1",
                "    End While",
            ]));
        });
    }


    [Fact]
    public void Issue118932IndentAfterFor()
    {

        // https://github.com/microsoft/vscode/issues/118932

        using var model = CreateTextModel(JoinLines([
            "Sub Test()",
            "    ",
        ]));

        WithIndentEditor(model, EditorAutoIndentStrategy.Full, (editor, viewModel) =>
        {
            editor.SetSelection(new TextSelection(2, 5, 2, 5));
            viewModel.Type("For i = 1 To 10");
            viewModel.Type("\n", "keyboard");
            AssertDeepStrictEqual(model.GetValue(), JoinLines([
                "Sub Test()",
                "    For i = 1 To 10",
                "        ",
            ]));
        });
    }


    [Fact]
    public void Issue118932DedentOnNext()
    {

        // https://github.com/microsoft/vscode/issues/118932

        using var model = CreateTextModel(JoinLines([
            "Sub Test()",
            "    For i = 1 To 10",
            "        DoSomething(i)",
            "        Nex",
        ]));

        WithIndentEditor(model, EditorAutoIndentStrategy.Full, (editor, viewModel) =>
        {
            editor.SetSelection(new TextSelection(4, 12, 4, 12));
            viewModel.Type("t", "keyboard");
            AssertDeepStrictEqual(model.GetValue(), JoinLines([
                "Sub Test()",
                "    For i = 1 To 10",
                "        DoSomething(i)",
                "    Next",
            ]));
        });
    }


    [Fact]
    public void Issue118932IndentAfterDo()
    {

        // https://github.com/microsoft/vscode/issues/118932

        using var model = CreateTextModel(JoinLines([
            "Sub Test()",
            "    ",
        ]));

        WithIndentEditor(model, EditorAutoIndentStrategy.Full, (editor, viewModel) =>
        {
            editor.SetSelection(new TextSelection(2, 5, 2, 5));
            viewModel.Type("Do");
            viewModel.Type("\n", "keyboard");
            AssertDeepStrictEqual(model.GetValue(), JoinLines([
                "Sub Test()",
                "    Do",
                "        ",
            ]));
        });
    }


    [Fact]
    public void Issue118932DedentOnLoop()
    {

        // https://github.com/microsoft/vscode/issues/118932

        using var model = CreateTextModel(JoinLines([
            "Sub Test()",
            "    Do",
            "        x = x + 1",
            "        Loo",
        ]));

        WithIndentEditor(model, EditorAutoIndentStrategy.Full, (editor, viewModel) =>
        {
            editor.SetSelection(new TextSelection(4, 12, 4, 12));
            viewModel.Type("p", "keyboard");
            AssertDeepStrictEqual(model.GetValue(), JoinLines([
                "Sub Test()",
                "    Do",
                "        x = x + 1",
                "    Loop",
            ]));
        });
    }


    [Fact]
    public void Issue118932IndentAfterSelectCase()
    {

        // https://github.com/microsoft/vscode/issues/118932

        using var model = CreateTextModel(JoinLines([
            "Sub Test()",
            "    ",
        ]));

        WithIndentEditor(model, EditorAutoIndentStrategy.Full, (editor, viewModel) =>
        {
            editor.SetSelection(new TextSelection(2, 5, 2, 5));
            viewModel.Type("Select Case x");
            viewModel.Type("\n", "keyboard");
            AssertDeepStrictEqual(model.GetValue(), JoinLines([
                "Sub Test()",
                "    Select Case x",
                "        ",
            ]));
        });
    }


    [Fact]
    public void Issue118932DedentOnEndSelect()
    {

        // https://github.com/microsoft/vscode/issues/118932
        // When End Select is typed, it dedents to match Select Case level

        using var model = CreateTextModel(JoinLines([
            "Sub Test()",
            "    Select Case x",
            "        End Selec",
        ]));

        WithIndentEditor(model, EditorAutoIndentStrategy.Full, (editor, viewModel) =>
        {
            editor.SetSelection(new TextSelection(3, 18, 3, 18));
            viewModel.Type("t", "keyboard");
            AssertDeepStrictEqual(model.GetValue(), JoinLines([
                "Sub Test()",
                "    Select Case x",
                "    End Select",
            ]));
        });
    }


    [Fact]
    public void Issue118932IndentAfterTry()
    {

        // https://github.com/microsoft/vscode/issues/118932

        using var model = CreateTextModel(JoinLines([
            "Sub Test()",
            "    ",
        ]));

        WithIndentEditor(model, EditorAutoIndentStrategy.Full, (editor, viewModel) =>
        {
            editor.SetSelection(new TextSelection(2, 5, 2, 5));
            viewModel.Type("Try");
            viewModel.Type("\n", "keyboard");
            AssertDeepStrictEqual(model.GetValue(), JoinLines([
                "Sub Test()",
                "    Try",
                "        ",
            ]));
        });
    }


    [Fact]
    public void Issue118932DedentAndIndentOnCatch()
    {

        // https://github.com/microsoft/vscode/issues/118932

        using var model = CreateTextModel(JoinLines([
            "Sub Test()",
            "    Try",
            "        DoSomething()",
            "        Catc",
        ]));

        WithIndentEditor(model, EditorAutoIndentStrategy.Full, (editor, viewModel) =>
        {
            editor.SetSelection(new TextSelection(4, 13, 4, 13));
            viewModel.Type("h", "keyboard");
            AssertDeepStrictEqual(model.GetValue(), JoinLines([
                "Sub Test()",
                "    Try",
                "        DoSomething()",
                "    Catch",
            ]));
        });
    }


    [Fact]
    public void Issue118932DedentAndIndentOnFinally()
    {

        // https://github.com/microsoft/vscode/issues/118932

        using var model = CreateTextModel(JoinLines([
            "Sub Test()",
            "    Try",
            "        DoSomething()",
            "    Catch",
            "        HandleError()",
            "        Finall",
        ]));

        WithIndentEditor(model, EditorAutoIndentStrategy.Full, (editor, viewModel) =>
        {
            editor.SetSelection(new TextSelection(6, 15, 6, 15));
            viewModel.Type("y", "keyboard");
            AssertDeepStrictEqual(model.GetValue(), JoinLines([
                "Sub Test()",
                "    Try",
                "        DoSomething()",
                "    Catch",
                "        HandleError()",
                "    Finally",
            ]));
        });
    }


    [Fact]
    public void Issue118932DedentOnEndTry()
    {

        // https://github.com/microsoft/vscode/issues/118932

        using var model = CreateTextModel(JoinLines([
            "Sub Test()",
            "    Try",
            "        DoSomething()",
            "    Catch",
            "        HandleError()",
            "    Finally",
            "        Cleanup()",
            "        End Tr",
        ]));

        WithIndentEditor(model, EditorAutoIndentStrategy.Full, (editor, viewModel) =>
        {
            editor.SetSelection(new TextSelection(8, 15, 8, 15));
            viewModel.Type("y", "keyboard");
            AssertDeepStrictEqual(model.GetValue(), JoinLines([
                "Sub Test()",
                "    Try",
                "        DoSomething()",
                "    Catch",
                "        HandleError()",
                "    Finally",
                "        Cleanup()",
                "    End Try",
            ]));
        });
    }


    [Fact]
    public void Issue118932IndentAfterClass()
    {

        // https://github.com/microsoft/vscode/issues/118932

        using var model = CreateTextModel("");

        WithIndentEditor(model, EditorAutoIndentStrategy.Full, (editor, viewModel) =>
        {
            viewModel.Type("Class MyClass");
            viewModel.Type("\n", "keyboard");
            AssertDeepStrictEqual(model.GetValue(), JoinLines([
                "Class MyClass",
                "    ",
            ]));
        });
    }


    [Fact]
    public void Issue118932DedentOnEndClass()
    {

        // https://github.com/microsoft/vscode/issues/118932

        using var model = CreateTextModel(JoinLines([
            "Class MyClass",
            "    Private x As Integer",
            "    End Clas",
        ]));

        WithIndentEditor(model, EditorAutoIndentStrategy.Full, (editor, viewModel) =>
        {
            editor.SetSelection(new TextSelection(3, 14, 3, 14));
            viewModel.Type("s", "keyboard");
            AssertDeepStrictEqual(model.GetValue(), JoinLines([
                "Class MyClass",
                "    Private x As Integer",
                "End Class",
            ]));
        });
    }


    [Fact]
    public void Issue118932FullProgramIndentationFlow()
    {

        // https://github.com/microsoft/vscode/issues/118932
        // Verify the complete flow as described in the verification comment
        // Note: Auto-indent only triggers on typing the last character that completes a keyword
        // and only decreases by one indentation level per keyword completion

        using var model = CreateTextModel("");

        WithIndentEditor(model, EditorAutoIndentStrategy.Full, (editor, viewModel) =>
        {
            // Type Module Test and press Enter
            viewModel.Type("Module Test");
            viewModel.Type("\n", "keyboard");
            AssertDeepStrictEqual(model.GetValue(), JoinLines([
                "Module Test",
                "    ",
            ]), "After Module Test");

            // Type Sub Main() and press Enter
            viewModel.Type("Sub Main()");
            viewModel.Type("\n", "keyboard");
            AssertDeepStrictEqual(model.GetValue(), JoinLines([
                "Module Test",
                "    Sub Main()",
                "        ",
            ]), "After Sub Main()");

            // Type Console.WriteLine and press Enter
            viewModel.Type("Console.WriteLine(\"Hello, World!\")");
            viewModel.Type("\n", "keyboard");
            AssertDeepStrictEqual(model.GetValue(), JoinLines([
                "Module Test",
                "    Sub Main()",
                "        Console.WriteLine(\"Hello, World!\")",
                "        ",
            ]), "After Console.WriteLine");

            // Type End Su then 'b' to complete End Sub (auto-indent triggers on last char)
            viewModel.Type("End Su");
            viewModel.Type("b", "keyboard");
            AssertDeepStrictEqual(model.GetValue(), JoinLines([
                "Module Test",
                "    Sub Main()",
                "        Console.WriteLine(\"Hello, World!\")",
                "    End Sub",
            ]), "After End Sub");

            // Press Enter - should maintain same indent level after End Sub
            viewModel.Type("\n", "keyboard");
            AssertDeepStrictEqual(model.GetValue(), JoinLines([
                "Module Test",
                "    Sub Main()",
                "        Console.WriteLine(\"Hello, World!\")",
                "    End Sub",
                "    ",
            ]), "After Enter after End Sub");
        });
    }
}

/// <summary>Upstream's suite 'Auto Indent On Type - Latex'.</summary>
public sealed class AutoIndentOnTypeLatexTests() : IndentationTestBase(Language.Latex)
{
    [Fact(Skip = "test.skip upstream")]
    public void Issue178075NoAutoClosingPairWhenIndentationDone()
    {

        // https://github.com/microsoft/vscode/issues/178075

        using var model = CreateTextModel(JoinLines([
            "\\begin{theorem}",
            "    \\end",
        ]));

        WithIndentEditor(model, EditorAutoIndentStrategy.Full, (editor, viewModel) =>
        {
            editor.SetSelection(new TextSelection(2, 9, 2, 9));
            viewModel.Type("{", "keyboard");
            AssertDeepStrictEqual(model.GetValue(), JoinLines([
                "\\begin{theorem}",
                "\\end{}",
            ]));
        });
    }
}

/// <summary>Upstream's suite 'Auto Indent On Type - Lua'.</summary>
public sealed class AutoIndentOnTypeLuaTests() : IndentationTestBase(Language.Lua)
{
    [Fact(Skip = "test.skip upstream")]
    public void Issue178075NoAutoClosingPairWhenIndentationDone2()
    {

        // https://github.com/microsoft/vscode/issues/178075

        using var model = CreateTextModel(JoinLines([
            "print(\"asdf function asdf\")",
        ]));

        WithIndentEditor(model, EditorAutoIndentStrategy.Full, (editor, viewModel) =>
        {
            editor.SetSelection(new TextSelection(1, 28, 1, 28));
            viewModel.Type("\n", "keyboard");
            AssertDeepStrictEqual(model.GetValue(), JoinLines([
                "print(\"asdf function asdf\")",
                ""
            ]));
        });
    }
}
