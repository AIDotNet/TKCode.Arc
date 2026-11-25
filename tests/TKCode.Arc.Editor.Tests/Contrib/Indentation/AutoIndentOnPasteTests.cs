// Ported from vscode (src/vs/editor/contrib/indentation/test/browser/indentation.test.ts, commit b57d5c9e), MIT
// license: the suites 'Indent With Tab', 'Auto Indent On Paste - TypeScript/JavaScript' and 'Auto Indent On Paste - Go'.
// Differences from upstream: see IndentationTestBase.

using TKCode.Arc.Editor.Commands;
using TKCode.Arc.Editor.Config;
using TKCode.Arc.Editor.Contrib.Indentation;
using TKCode.Arc.Editor.Cursor;
using TKCode.Arc.Editor.Tokens;
using Xunit;
using static TKCode.Arc.Editor.Tests.Contrib.EditingAssert;

namespace TKCode.Arc.Editor.Tests.Contrib;

/// <summary>Upstream's suite 'Indent With Tab - TypeScript/JavaScript'.</summary>
public sealed class IndentWithTabTests() : IndentationTestBase(Language.TypeScript)
{
    [Fact(Skip = "test.skip upstream")]
    public void Issue63388PerserveCorrectIndentationOnTab1()
    {

        // https://github.com/microsoft/vscode/issues/63388

        using var model = CreateTextModel(JoinLines([
            "/*",
            " * Comment",
            " * /",
        ]));

        WithIndentEditor(model, EditorAutoIndentStrategy.Full, (editor, viewModel) =>
        {
            editor.SetSelection(new TextSelection(1, 1, 3, 5));
            editor.ExecuteCommands("editor.action.indentLines", TypeOperations.Indent(viewModel.CursorConfig, editor.GetModel(), editor.GetSelections()));
            AssertDeepStrictEqual(model.GetValue(), JoinLines([
                "    /*",
                "     * Comment",
                "     * /",
            ]));
        });
    }


    [Fact(Skip = "test.skip upstream")]
    public void Issue63388PerserveCorrectIndentationOnTab2()
    {

        // https://github.com/microsoft/vscode/issues/63388

        using var model = CreateTextModel(JoinLines([
            "switch (something) {",
            "  case 1:",
            "    whatever();",
            "    break;",
            "}",
        ]));

        WithIndentEditor(model, EditorAutoIndentStrategy.Full, (editor, viewModel) =>
        {
            editor.SetSelection(new TextSelection(1, 1, 5, 2));
            editor.ExecuteCommands("editor.action.indentLines", TypeOperations.Indent(viewModel.CursorConfig, editor.GetModel(), editor.GetSelections()));
            AssertDeepStrictEqual(model.GetValue(), JoinLines([
                "    switch (something) {",
                "        case 1:",
                "            whatever();",
                "            break;",
                "    }",
            ]));
        });
    }
}

/// <summary>Upstream's suite 'Auto Indent On Paste - TypeScript/JavaScript'.</summary>
public sealed class AutoIndentOnPasteTypeScriptTests() : IndentationTestBase(Language.TypeScript)
{
    [Fact]
    public void Issue119225DoNotAddExtraLeadingSpaceWhenPastingJSDoc()
    {

        using var model = CreateTextModel("");

        WithIndentEditor(model, EditorAutoIndentStrategy.Full, (editor, viewModel) =>
        {
            var pasteText = JoinLines([
                "/**",
                " * JSDoc",
                " */",
                "function a() {}"
            ]);
            StandardTokenTypeData[][] tokens = [
                [
                    new(0, StandardTokenType.Comment),
                    new(3, StandardTokenType.Comment),
                ],
                [
                    new(0, StandardTokenType.Comment),
                    new(2, StandardTokenType.Comment),
                    new(8, StandardTokenType.Comment),
                ],
                [
                    new(0, StandardTokenType.Comment),
                    new(1, StandardTokenType.Comment),
                    new(3, StandardTokenType.Other),
                ],
                [
                    new(0, StandardTokenType.Other),
                    new(8, StandardTokenType.Other),
                    new(9, StandardTokenType.Other),
                    new(10, StandardTokenType.Other),
                    new(11, StandardTokenType.Other),
                    new(12, StandardTokenType.Other),
                    new(13, StandardTokenType.Other),
                    new(14, StandardTokenType.Other),
                    new(15, StandardTokenType.Other),
                ]
            ];
            IndentationTestUtils.RegisterTokenizationSupport(model, tokens, languageId);
            viewModel.Paste(pasteText, true, null, "keyboard");
            AutoIndentOnPaste.Trigger(editor, new TextRange(1, 1, 4, 16));
            AssertDeepStrictEqual(model.GetValue(), pasteText);
        });
    }


    [Fact]
    public void Issue167299BlankLineRemovesIndent()
    {

        using var model = CreateTextModel("");

        WithIndentEditor(model, EditorAutoIndentStrategy.Full, (editor, viewModel) =>
        {

            // no need for tokenization because there are no comments
            var pasteText = JoinLines([
                "",
                "export type IncludeReference =",
                "\t| BaseReference",
                "\t| SelfReference",
                "\t| RelativeReference;",
                "",
                "export const enum IncludeReferenceKind {",
                "\tBase,",
                "\tSelf,",
                "\tRelativeReference,",
                "}"
            ]);
            viewModel.Paste(pasteText, true, null, "keyboard");
            AutoIndentOnPaste.Trigger(editor, new TextRange(1, 1, 11, 2));
            AssertDeepStrictEqual(model.GetValue(), pasteText);
        });
    }


    [Fact]
    public void Issue29803DoNotIndentWhenPastingTextWithOnlyOneLine()
    {

        // https://github.com/microsoft/vscode/issues/29803

        using var model = CreateTextModel(JoinLines([
            "const linkHandler = new Class(a, b, c,",
            "    d)"
        ]));

        WithIndentEditor(model, EditorAutoIndentStrategy.Full, (editor, viewModel) =>
        {
            editor.SetSelection(new TextSelection(2, 6, 2, 6));
            var text = ", null";
            viewModel.Paste(text, true, null, "keyboard");
            AutoIndentOnPaste.Trigger(editor, new TextRange(2, 6, 2, 11));
            AssertDeepStrictEqual(model.GetValue(), JoinLines([
                "const linkHandler = new Class(a, b, c,",
                "    d, null)"
            ]));
        });
    }


    [Fact]
    public void Issue29753IncorrectIndentationAfterComment()
    {

        // https://github.com/microsoft/vscode/issues/29753

        using var model = CreateTextModel(JoinLines([
            "class A {",
            "    /**",
            "     * used only for debug purposes.",
            "     */",
            "    private _codeInfo: KeyMapping[];",
            "}",
        ]));

        WithIndentEditor(model, EditorAutoIndentStrategy.Full, (editor, viewModel) =>
        {
            editor.SetSelection(new TextSelection(5, 24, 5, 34));
            var text = "IMacLinuxKeyMapping";
            viewModel.Paste(text, true, null, "keyboard");
            AutoIndentOnPaste.Trigger(editor, new TextRange(5, 24, 5, 43));
            AssertDeepStrictEqual(model.GetValue(), JoinLines([
                "class A {",
                "    /**",
                "     * used only for debug purposes.",
                "     */",
                "    private _codeInfo: IMacLinuxKeyMapping[];",
                "}",
            ]));
        });
    }


    [Fact]
    public void Issue29753IncorrectIndentationOfHeaderComment()
    {

        // https://github.com/microsoft/vscode/issues/29753

        using var model = CreateTextModel("");

        WithIndentEditor(model, EditorAutoIndentStrategy.Full, (editor, viewModel) =>
        {
            var text = JoinLines([
                "/*----------------",
                " *  Copyright (c) ",
                " *  Licensed under ...",
                " *-----------------*/",
            ]);
            viewModel.Paste(text, true, null, "keyboard");
            AutoIndentOnPaste.Trigger(editor, new TextRange(1, 1, 4, 22));
            AssertDeepStrictEqual(model.GetValue(), text);
        });
    }


    [Fact]
    public void Issue209859DoNotDoChangeIndentationWhenPastedInsideOfAString()
    {

        // issue: https://github.com/microsoft/vscode/issues/209859
        // issue: https://github.com/microsoft/vscode/issues/209418

        var initialText = JoinLines([
            "const foo = \"some text",
            "         which is strangely",
            "    indented\""
        ]);
        using var model = CreateTextModel(initialText);

        WithIndentEditor(model, EditorAutoIndentStrategy.Full, (editor, viewModel) =>
        {
            StandardTokenTypeData[][] tokens = [
                [
                    new(0, StandardTokenType.Other),
                    new(12, StandardTokenType.String),
                ],
                [
                    new(0, StandardTokenType.String),
                ],
                [
                    new(0, StandardTokenType.String),
                ]
            ];
            IndentationTestUtils.RegisterTokenizationSupport(model, tokens, languageId);

            editor.SetSelection(new TextSelection(2, 10, 2, 15));
            viewModel.Paste("which", true, null, "keyboard");
            AutoIndentOnPaste.Trigger(editor, new TextRange(2, 1, 2, 28));
            AssertDeepStrictEqual(model.GetValue(), initialText);
        });
    }


    // Failing tests found in issues...

    [Fact(Skip = "test.skip upstream")]
    public void Issue181065IncorrectPasteOfObjectWithinComment()
    {

        // https://github.com/microsoft/vscode/issues/181065

        using var model = CreateTextModel("");

        WithIndentEditor(model, EditorAutoIndentStrategy.Full, (editor, viewModel) =>
        {
            var text = JoinLines([
                "/**",
                " * @typedef {",
                " * }",
                " */"
            ]);
            StandardTokenTypeData[][] tokens = [
                [
                    new(0, StandardTokenType.Comment),
                    new(3, StandardTokenType.Comment),
                ],
                [
                    new(0, StandardTokenType.Comment),
                    new(2, StandardTokenType.Comment),
                    new(3, StandardTokenType.Comment),
                    new(11, StandardTokenType.Comment),
                    new(12, StandardTokenType.Other),
                    new(13, StandardTokenType.Other),
                ],
                [
                    new(0, StandardTokenType.Comment),
                    new(2, StandardTokenType.Other),
                    new(3, StandardTokenType.Other),
                    new(4, StandardTokenType.Other),
                ],
                [
                    new(0, StandardTokenType.Comment),
                    new(1, StandardTokenType.Comment),
                    new(3, StandardTokenType.Other),
                ]
            ];
            IndentationTestUtils.RegisterTokenizationSupport(model, tokens, languageId);
            viewModel.Paste(text, true, null, "keyboard");
            AutoIndentOnPaste.Trigger(editor, new TextRange(1, 1, 4, 4));
            AssertDeepStrictEqual(model.GetValue(), text);
        });
    }


    [Fact(Skip = "test.skip upstream")]
    public void Issue86301PreserveCursorAtInsertedIndentationLevel()
    {

        // https://github.com/microsoft/vscode/issues/86301

        using var model = CreateTextModel(JoinLines([
            "() => {",
            "",
            "}",
        ]));

        WithIndentEditor(model, EditorAutoIndentStrategy.Full, (editor, viewModel) =>
        {
            editor.SetSelection(new TextSelection(2, 1, 2, 1));
            var text = JoinLines([
                "() => {",
                "",
                "}",
                ""
            ]);
            viewModel.Paste(text, true, null, "keyboard");
            AutoIndentOnPaste.Trigger(editor, new TextRange(2, 1, 5, 1));

            // notes:
            // why is line 3 not indented to the same level as line 2?
            // looks like the indentation is inserted correctly at line 5, but the cursor does not appear at the maximum indentation level?
            AssertDeepStrictEqual(model.GetValue(), JoinLines([
                "() => {",
                "    () => {",
                "    ", // <- should also be indented
                "    }",
                "    ", // <- cursor should be at the end of the indentation
                "}",
            ]));

            var selection = viewModel.GetSelection();
            AssertDeepStrictEqual(selection, new TextSelection(5, 5, 5, 5));
        });
    }


    [Fact(Skip = "test.skip upstream")]
    public void Issue85781IndentLineWithExtraWhiteSpace()
    {

        // https://github.com/microsoft/vscode/issues/85781
        // note: still to determine whether this is a bug or not

        using var model = CreateTextModel(JoinLines([
            "() => {",
            "    console.log(\"a\");",
            "}",
        ]));

        WithIndentEditor(model, EditorAutoIndentStrategy.Full, (editor, viewModel) =>
        {
            editor.SetSelection(new TextSelection(2, 5, 2, 5));
            var text = JoinLines([
                "() => {",
                "    console.log(\"b\")",
                "}",
                " "
            ]);
            viewModel.Paste(text, true, null, "keyboard");
            // todo@aiday-mar, make sure range is correct, and make test work as in real life
            AutoIndentOnPaste.Trigger(editor, new TextRange(2, 5, 5, 6));
            AssertDeepStrictEqual(model.GetValue(), JoinLines([
                "() => {",
                "    () => {",
                "        console.log(\"b\")",
                "    }",
                "    console.log(\"a\");",
                "}",
            ]));
        });
    }


    [Fact(Skip = "test.skip upstream")]
    public void Issue29589IncorrectIndentationOfClosingBraceOnPaste()
    {

        // https://github.com/microsoft/vscode/issues/29589

        using var model = CreateTextModel("");

        WithIndentEditor(model, EditorAutoIndentStrategy.Full, (editor, viewModel) =>
        {
            editor.SetSelection(new TextSelection(2, 5, 2, 5));
            var text = JoinLines([
                "function makeSub(a,b) {",
                "subsent = sent.substring(a,b);",
                "return subsent;",
                "}",
            ]);
            viewModel.Paste(text, true, null, "keyboard");
            // todo@aiday-mar, make sure range is correct, and make test work as in real life
            AutoIndentOnPaste.Trigger(editor, new TextRange(1, 1, 4, 2));
            AssertDeepStrictEqual(model.GetValue(), JoinLines([
                "function makeSub(a,b) {",
                "subsent = sent.substring(a,b);",
                "return subsent;",
                "}",
            ]));
        });
    }


    [Fact(Skip = "test.skip upstream")]
    public void Issue201420IncorrectIndentationWhenFirstLineIsComment()
    {

        // https://github.com/microsoft/vscode/issues/201420

        using var model = CreateTextModel(JoinLines([
            "function bar() {",
            "",
            "}",
        ]));

        WithIndentEditor(model, EditorAutoIndentStrategy.Full, (editor, viewModel) =>
        {
            StandardTokenTypeData[][] tokens = [
                [
                    new(0, StandardTokenType.Other),
                    new(8, StandardTokenType.Other),
                    new(9, StandardTokenType.Other),
                    new(12, StandardTokenType.Other),
                    new(13, StandardTokenType.Other),
                    new(14, StandardTokenType.Other),
                    new(15, StandardTokenType.Other),
                    new(16, StandardTokenType.Other)
                ],
                [
                    new(0, StandardTokenType.Comment),
                    new(2, StandardTokenType.Comment),
                    new(3, StandardTokenType.Comment),
                    new(10, StandardTokenType.Comment)
                ],
                [
                    new(0, StandardTokenType.Other),
                    new(5, StandardTokenType.Other),
                    new(6, StandardTokenType.Other),
                    new(9, StandardTokenType.Other),
                    new(10, StandardTokenType.Other),
                    new(11, StandardTokenType.Other),
                    new(12, StandardTokenType.Other),
                    new(14, StandardTokenType.Other)],
                [
                    new(0, StandardTokenType.Other),
                    new(1, StandardTokenType.Other)]
            ];
            IndentationTestUtils.RegisterTokenizationSupport(model, tokens, languageId);

            editor.SetSelection(new TextSelection(2, 1, 2, 1));
            var text = JoinLines([
                "// comment",
                "const foo = 42",
            ]);
            viewModel.Paste(text, true, null, "keyboard");
            AutoIndentOnPaste.Trigger(editor, new TextRange(2, 1, 3, 15));
            AssertDeepStrictEqual(model.GetValue(), JoinLines([
                "function bar() {",
                "    // comment",
                "    const foo = 42",
                "}",
            ]));
        });
    }
}

/// <summary>Upstream's suite 'Auto Indent On Paste - Go'.</summary>
public sealed class AutoIndentOnPasteGoTests() : IndentationTestBase(Language.Go)
{
    [Fact(Skip = "test.skip upstream")]
    public void Issue199050ShouldNotIndentAfterDetectedInAString2()
    {

        // https://github.com/microsoft/vscode/issues/199050

        using var model = CreateTextModel(JoinLines([
            "var s = `",
            "quick  brown",
            "fox",
            "`",
        ]));

        WithIndentEditor(model, EditorAutoIndentStrategy.Full, (editor, viewModel) =>
        {
            editor.SetSelection(new TextSelection(3, 1, 3, 1));
            var text = "  ";
            viewModel.Paste(text, true, null, "keyboard");
            AutoIndentOnPaste.Trigger(editor, new TextRange(3, 1, 3, 3));
            AssertDeepStrictEqual(model.GetValue(), JoinLines([
                "var s = `",
                "quick  brown",
                "  fox",
                "`",
            ]));
        });
    }
}
