// Ported from vscode (src/vs/editor/contrib/indentation/test/browser/indentation.test.ts, commit b57d5c9e), MIT
// license: the suite 'Auto Indent On Type - TypeScript/JavaScript'.
// Differences from upstream: see IndentationTestBase.

using Shouldly;
using TKCode.Arc.Editor.Commands;
using TKCode.Arc.Editor.Config;
using TKCode.Arc.Editor.Contrib.Indentation;
using TKCode.Arc.Editor.Cursor;
using TKCode.Arc.Editor.Tokens;
using Xunit;
using static TKCode.Arc.Editor.Tests.Contrib.EditingAssert;

namespace TKCode.Arc.Editor.Tests.Contrib;

/// <summary>Upstream's suite 'Auto Indent On Type - TypeScript/JavaScript'.</summary>
public sealed class AutoIndentOnTypeTypeScriptTests() : IndentationTestBase(Language.TypeScript)
{
    // Failing tests from issues...

    [Fact]
    public void Issue208215IndentAfterArrowFunction()
    {

        // https://github.com/microsoft/vscode/issues/208215

        using var model = CreateTextModel("");

        WithIndentEditor(model, EditorAutoIndentStrategy.Full, (editor, viewModel) =>
        {
            viewModel.Type("const add1 = (n) =>");
            viewModel.Type("\n", "keyboard");
            AssertDeepStrictEqual(model.GetValue(), JoinLines([
                "const add1 = (n) =>",
                "    ",
            ]));
        });
    }


    [Fact]
    public void Issue208215IndentAfterArrowFunction2()
    {

        // https://github.com/microsoft/vscode/issues/208215

        using var model = CreateTextModel(JoinLines([
            "const array = [1, 2, 3, 4, 5];",
            "array.map(",
            "    v =>",
        ]));

        WithIndentEditor(model, EditorAutoIndentStrategy.Full, (editor, viewModel) =>
        {
            editor.SetSelection(new TextSelection(3, 9, 3, 9));
            viewModel.Type("\n", "keyboard");
            AssertDeepStrictEqual(model.GetValue(), JoinLines([
                "const array = [1, 2, 3, 4, 5];",
                "array.map(",
                "    v =>",
                "        "
            ]));
        });
    }


    [Fact]
    public void Issue116843IndentAfterArrowFunction()
    {

        // https://github.com/microsoft/vscode/issues/116843

        using var model = CreateTextModel("");

        WithIndentEditor(model, EditorAutoIndentStrategy.Full, (editor, viewModel) =>
        {
            viewModel.Type(JoinLines([
                "const add1 = (n) =>",
                "    n + 1;",
            ]));
            viewModel.Type("\n", "keyboard");
            AssertDeepStrictEqual(model.GetValue(), JoinLines([
                "const add1 = (n) =>",
                "    n + 1;",
                "",
            ]));
        });
    }


    [Fact]
    public void Issue29755DoNotAddIndentationOnEnterIfIndentationIsAlreadyValid()
    {

        //https://github.com/microsoft/vscode/issues/29755

        using var model = CreateTextModel(JoinLines([
            "function f() {",
            "    const one = 1;",
            "    const two = 2;",
            "}",
        ]));

        WithIndentEditor(model, EditorAutoIndentStrategy.Full, (editor, viewModel) =>
        {
            editor.SetSelection(new TextSelection(3, 1, 3, 1));
            viewModel.Type("\n", "keyboard");
            AssertDeepStrictEqual(model.GetValue(), JoinLines([
                "function f() {",
                "    const one = 1;",
                "",
                "    const two = 2;",
                "}",
            ]));
        });
    }


    [Fact]
    public void Issue36090()
    {

        // https://github.com/microsoft/vscode/issues/36090

        using var model = CreateTextModel(JoinLines([
            "class ItemCtrl {",
            "    getPropertiesByItemId(id) {",
            "        return this.fetchItem(id)",
            "            .then(item => {",
            "                return this.getPropertiesOfItem(item);",
            "            });",
            "    }",
            "}",
        ]));

        WithIndentEditor(model, EditorAutoIndentStrategy.Advanced, (editor, viewModel) =>
        {
            editor.SetSelection(new TextSelection(7, 6, 7, 6));
            viewModel.Type("\n", "keyboard");
            AssertDeepStrictEqual(model.GetValue(),
                JoinLines([
                    "class ItemCtrl {",
                    "    getPropertiesByItemId(id) {",
                    "        return this.fetchItem(id)",
                    "            .then(item => {",
                    "                return this.getPropertiesOfItem(item);",
                    "            });",
                    "    }",
                    "    ",
                    "}",
                ])
            );
            AssertDeepStrictEqual(editor.GetSelection(), new TextSelection(8, 5, 8, 5));
        });
    }


    [Fact]
    public void Issue115304IndentBlockCommentOnEnter()
    {

        // https://github.com/microsoft/vscode/issues/115304

        using var model = CreateTextModel(JoinLines([
            "/** */",
            "function f() {}",
        ]));

        WithIndentEditor(model, EditorAutoIndentStrategy.Advanced, (editor, viewModel) =>
        {
            editor.SetSelection(new TextSelection(1, 4, 1, 4));
            viewModel.Type("\n", "keyboard");
            AssertDeepStrictEqual(model.GetValue(),
                JoinLines([
                    "/**",
                    " * ",
                    " */",
                    "function f() {}",
                ])
            );
            AssertDeepStrictEqual(editor.GetSelection(), new TextSelection(2, 4, 2, 4));
        });
    }


    [Fact]
    public void Issue43244IndentWhenLambdaArrowFunctionIsDetectedOutdentWhenEndIsReached()
    {

        // https://github.com/microsoft/vscode/issues/43244

        using var model = CreateTextModel(JoinLines([
            "const array = [1, 2, 3, 4, 5];",
            "array.map(_)"
        ]));

        WithIndentEditor(model, EditorAutoIndentStrategy.Full, (editor, viewModel) =>
        {
            editor.SetSelection(new TextSelection(2, 12, 2, 12));
            viewModel.Type("\n", "keyboard");
            AssertDeepStrictEqual(model.GetValue(), JoinLines([
                "const array = [1, 2, 3, 4, 5];",
                "array.map(_",
                "    ",
                ")"
            ]));
        });
    }


    [Fact]
    public void Issue43244IncorrectIndentationAfterIfForWhileWithoutBraces()
    {

        // https://github.com/microsoft/vscode/issues/43244

        using var model = CreateTextModel(JoinLines([
            "function f() {",
            "    if (condition)",
            "}"
        ]));

        WithIndentEditor(model, EditorAutoIndentStrategy.Full, (editor, viewModel) =>
        {
            editor.SetSelection(new TextSelection(2, 19, 2, 19));
            viewModel.Type("\n", "keyboard");
            AssertDeepStrictEqual(model.GetValue(), JoinLines([
                "function f() {",
                "    if (condition)",
                "        ",
                "}",
            ]));

            viewModel.Type("return;");
            viewModel.Type("\n", "keyboard");
            AssertDeepStrictEqual(model.GetValue(), JoinLines([
                "function f() {",
                "    if (condition)",
                "        return;",
                "    ",
                "}",
            ]));
        });
    }


    [Fact]
    public void Issue208232IncorrectIndentationInsideOfComments()
    {

        // https://github.com/microsoft/vscode/issues/208232

        using var model = CreateTextModel(JoinLines([
            "/**",
            "indentation done for {",
            "*/"
        ]));

        WithIndentEditor(model, EditorAutoIndentStrategy.Full, (editor, viewModel) =>
        {
            StandardTokenTypeData[][] tokens = [
                [new(0, StandardTokenType.Comment)],
                [new(0, StandardTokenType.Comment)],
                [new(0, StandardTokenType.Comment)]
            ];
            IndentationTestUtils.RegisterTokenizationSupport(model, tokens, languageId);
            editor.SetSelection(new TextSelection(2, 23, 2, 23));
            viewModel.Type("\n", "keyboard");
            AssertDeepStrictEqual(model.GetValue(), JoinLines([
                "/**",
                "indentation done for {",
                "",
                "*/"
            ]));
        });
    }


    [Fact]
    public void Issue209802AllmanStyleBracesInJavaScript()
    {

        // https://github.com/microsoft/vscode/issues/209802

        using var model = CreateTextModel(JoinLines([
            "if (/*condition*/)",
        ]));

        WithIndentEditor(model, EditorAutoIndentStrategy.Full, (editor, viewModel) =>
        {
            editor.SetSelection(new TextSelection(1, 19, 1, 19));
            viewModel.Type("\n", "keyboard");
            AssertDeepStrictEqual(model.GetValue(), JoinLines([
                "if (/*condition*/)",
                "    "
            ]));
            viewModel.Type("{", "keyboard");
            AssertDeepStrictEqual(model.GetValue(), JoinLines([
                "if (/*condition*/)",
                "{}"
            ]));
            editor.SetSelection(new TextSelection(2, 2, 2, 2));
            viewModel.Type("\n", "keyboard");
            AssertDeepStrictEqual(model.GetValue(), JoinLines([
                "if (/*condition*/)",
                "{",
                "    ",
                "}"
            ]));
        });
    }


    [Fact]
    public void Issue225916SurroundingWithBracketsWorksAfterArrowFunction()
    {

        // https://github.com/microsoft/vscode/issues/225916

        (string, string)[] bracketPairs = [("{", "}"), ("[", "]"), ("(", ")")];

        foreach (var (open, close) in bracketPairs)
        {
            using var model = CreateTextModel(JoinLines([
                "const fn = () =>",
                "    doSomething()",
            ]));

            WithIndentEditor(model, EditorAutoIndentStrategy.Full, (editor, viewModel) =>
        {
                editor.SetSelection(new TextSelection(2, 5, 2, 18));
                viewModel.Type(open, "keyboard");
                model.GetValue().ShouldBe(JoinLines([
                    "const fn = () =>",
                    $"    {open}doSomething(){close}",
                ]), $"failed to surround selection with {open}{close}");
            });
        }
    }


    // Failing tests...

    [Fact(Skip = "test.skip upstream")]
    public void Issue43244IndentAfterEqualSignIsDetected()
    {

        // https://github.com/microsoft/vscode/issues/43244
        // issue: Should indent after an equal sign is detected followed by whitespace characters.
        // This should be outdented when a semi-colon is detected indicating the end of the assignment.

        // TODO: requires exploring indent/outdent pairs instead

        using var model = CreateTextModel(JoinLines([
            "const array ="
        ]));

        WithIndentEditor(model, EditorAutoIndentStrategy.Full, (editor, viewModel) =>
        {
            editor.SetSelection(new TextSelection(1, 14, 1, 14));
            viewModel.Type("\n", "keyboard");
            AssertDeepStrictEqual(model.GetValue(), JoinLines([
                "const array =",
                "    "
            ]));
        });
    }


    [Fact(Skip = "test.skip upstream")]
    public void Issue43244IndentAfterDotDetectedAfterObjectArraySignifyingAMethodCall()
    {

        // https://github.com/microsoft/vscode/issues/43244
        // issue: When a dot is written, we should detect that this is a method call and indent accordingly

        // TODO: requires exploring indent/outdent pairs instead

        using var model = CreateTextModel(JoinLines([
            "const array = [1, 2, 3];",
            "array."
        ]));

        WithIndentEditor(model, EditorAutoIndentStrategy.Full, (editor, viewModel) =>
        {
            editor.SetSelection(new TextSelection(2, 7, 2, 7));
            viewModel.Type("\n", "keyboard");
            AssertDeepStrictEqual(model.GetValue(), JoinLines([
                "const array = [1, 2, 3];",
                "array.",
                "    "
            ]));
        });
    }


    [Fact(Skip = "test.skip upstream")]
    public void Issue43244IndentAfterDotDetectedOnASubsequentLineAfterObjectArraySignifyingAMethodCall()
    {

        // https://github.com/microsoft/vscode/issues/43244
        // issue: When a dot is written, we should detect that this is a method call and indent accordingly

        // TODO: requires exploring indent/outdent pairs instead

        using var model = CreateTextModel(JoinLines([
            "const array = [1, 2, 3]",
        ]));

        WithIndentEditor(model, EditorAutoIndentStrategy.Full, (editor, viewModel) =>
        {
            editor.SetSelection(new TextSelection(2, 7, 2, 7));
            viewModel.Type("\n", "keyboard");
            viewModel.Type(".");
            AssertDeepStrictEqual(model.GetValue(), JoinLines([
                "const array = [1, 2, 3]",
                "    ."
            ]));
        });
    }


    [Fact(Skip = "test.skip upstream")]
    public void Issue43244KeepIndentationWhenMethodsCalledOnObjectArray()
    {

        // https://github.com/microsoft/vscode/issues/43244
        // Currently passes, but should pass with all the tests above too

        // TODO: requires exploring indent/outdent pairs instead

        using var model = CreateTextModel(JoinLines([
            "const array = [1, 2, 3]",
            "    .filter(() => true)"
        ]));

        WithIndentEditor(model, EditorAutoIndentStrategy.Full, (editor, viewModel) =>
        {
            editor.SetSelection(new TextSelection(2, 24, 2, 24));
            viewModel.Type("\n", "keyboard");
            AssertDeepStrictEqual(model.GetValue(), JoinLines([
                "const array = [1, 2, 3]",
                "    .filter(() => true)",
                "    "
            ]));
        });
    }


    [Fact(Skip = "test.skip upstream")]
    public void Issue43244KeepIndentationWhenChainedMethodsCalledOnObjectArray()
    {

        // https://github.com/microsoft/vscode/issues/43244
        // When the call chain is not finished yet, and we type a dot, we do not want to change the indentation

        // TODO: requires exploring indent/outdent pairs instead

        using var model = CreateTextModel(JoinLines([
            "const array = [1, 2, 3]",
            "    .filter(() => true)",
            "    "
        ]));

        WithIndentEditor(model, EditorAutoIndentStrategy.Full, (editor, viewModel) =>
        {
            editor.SetSelection(new TextSelection(3, 5, 3, 5));
            viewModel.Type(".");
            AssertDeepStrictEqual(model.GetValue(), JoinLines([
                "const array = [1, 2, 3]",
                "    .filter(() => true)",
                "    ." // here we don't want to increase the indentation because we have chained methods
            ]));
        });
    }


    [Fact(Skip = "test.skip upstream")]
    public void Issue43244OutdentWhenASemiColorIsDetectedIndicatingTheEndOfTheAssignment()
    {

        // https://github.com/microsoft/vscode/issues/43244

        // TODO: requires exploring indent/outdent pairs instead

        using var model = CreateTextModel(JoinLines([
            "const array = [1, 2, 3]",
            "    .filter(() => true);"
        ]));

        WithIndentEditor(model, EditorAutoIndentStrategy.Full, (editor, viewModel) =>
        {
            editor.SetSelection(new TextSelection(2, 25, 2, 25));
            viewModel.Type("\n", "keyboard");
            AssertDeepStrictEqual(model.GetValue(), JoinLines([
                "const array = [1, 2, 3]",
                "    .filter(() => true);",
                ""
            ]));
        });
    }



    [Fact(Skip = "test.skip upstream")]
    public void Issue40115KeepIndentationWhenAdded()
    {

        // https://github.com/microsoft/vscode/issues/40115

        using var model = CreateTextModel("function foo() {}");

        WithIndentEditor(model, EditorAutoIndentStrategy.Full, (editor, viewModel) =>
        {
            editor.SetSelection(new TextSelection(1, 17, 1, 17));
            viewModel.Type("\n", "keyboard");
            AssertDeepStrictEqual(model.GetValue(), JoinLines([
                "function foo() {",
                "    ",
                "}",
            ]));
            editor.SetSelection(new TextSelection(2, 5, 2, 5));
            viewModel.Type("\n", "keyboard");
            AssertDeepStrictEqual(model.GetValue(), JoinLines([
                "function foo() {",
                "    ",
                "    ",
                "}",
            ]));
        });
    }


    [Fact(Skip = "test.skip upstream")]
    public void Issue193875IncorrectIndentationOnEnter()
    {

        // https://github.com/microsoft/vscode/issues/193875

        using var model = CreateTextModel(JoinLines([
            "{",
            "    for(;;)",
            "    for(;;) {}",
            "}",
        ]));

        WithIndentEditor(model, EditorAutoIndentStrategy.Full, (editor, viewModel) =>
        {
            editor.SetSelection(new TextSelection(3, 14, 3, 14));
            viewModel.Type("\n", "keyboard");
            AssertDeepStrictEqual(model.GetValue(), JoinLines([
                "{",
                "    for(;;)",
                "    for(;;) {",
                "        ",
                "    }",
                "}",
            ]));
        });
    }


    [Fact(Skip = "test.skip upstream")]
    public void Issue67678IndentOnTypingCurlyBrace()
    {

        // https://github.com/microsoft/vscode/issues/67678

        using var model = CreateTextModel(JoinLines([
            "if (true) {",
            "console.log(\"a\")",
            "console.log(\"b\")",
            "",
        ]));

        WithIndentEditor(model, EditorAutoIndentStrategy.Full, (editor, viewModel) =>
        {
            editor.SetSelection(new TextSelection(4, 1, 4, 1));
            viewModel.Type("}", "keyboard");
            AssertDeepStrictEqual(model.GetValue(), JoinLines([
                "if (true) {",
                "    console.log(\"a\")",
                "    console.log(\"b\")",
                "}",
            ]));
        });
    }


    [Fact(Skip = "test.skip upstream")]
    public void Issue46401OutdentWhenEncounteringBracketOnLineAllmanStyleIndentation()
    {

        // https://github.com/microsoft/vscode/issues/46401

        using var model = CreateTextModel(JoinLines([
            "if (true)",
            "    ",
        ]));

        WithIndentEditor(model, EditorAutoIndentStrategy.Full, (editor, viewModel) =>
        {
            editor.SetSelection(new TextSelection(2, 5, 2, 5));
            viewModel.Type("{}", "keyboard");
            AssertDeepStrictEqual(model.GetValue(), JoinLines([
                "if (true)",
                "{}",
            ]));
            editor.SetSelection(new TextSelection(2, 2, 2, 2));
            viewModel.Type("\n", "keyboard");
            AssertDeepStrictEqual(model.GetValue(), JoinLines([
                "if (true)",
                "{",
                "    ",
                "}"
            ]));
        });
    }


    [Fact(Skip = "test.skip upstream")]
    public void Issue125261TypingClosingBraceDoesNotKeepTheCurrentIndentation()
    {

        // https://github.com/microsoft/vscode/issues/125261

        using var model = CreateTextModel(JoinLines([
            "foo {",
            "    ",
        ]));

        WithIndentEditor(model, EditorAutoIndentStrategy.Keep, (editor, viewModel) =>
        {
            editor.SetSelection(new TextSelection(2, 5, 2, 5));
            viewModel.Type("}", "keyboard");
            AssertDeepStrictEqual(model.GetValue(), JoinLines([
                "foo {",
                "}",
            ]));
        });
    }
}
